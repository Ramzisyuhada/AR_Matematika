using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using SimpleJSON;
using Newtonsoft.Json;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class AssessmentView : MonoBehaviour
{
    // ======================= REFS =======================
    [Header("Refs")]
    public PdfPresignClient client;          // tetap pakai client kamu
    public Button btnBukaFile;
    public Button btnUpload;
    public Button btnBukaHasil;              // optional: tombol untuk buka hasil terakhir (lokal)

    [Header("Logic")]
    [SerializeField] private AssessmentVM ViewModel;
    [SerializeField] private AnswerVM AnswerViewModel;
    [SerializeField] private SubmissionsVM SubmissionsViewModel;
    [SerializeField] private GradeVM GradeViewModel;

    [Header("Limit")]
    public int maxSizeMB = 5;

    [Header("UI")]
    [SerializeField] private TMP_Text NamaFile;
    [SerializeField] private GameObject Aplod;
    [SerializeField] private GameObject NamaFileObj;
    [SerializeField] private GameObject ButtonNext;
    [SerializeField] private GameObject Hasil;
    [SerializeField] private TMP_Text NamaFileSiswa;
    [SerializeField] private GameObject LoadingScreen;

    // ======================= STATE =======================
    private string selectedPath;
    private byte[] selectedBytes;
    private string selectedName;
    private bool ISFileSudahada;
    private bool isProcessing;
    private string s3key;                        // key/path di storage
    private string lastDownloadLocalPath;        // file lokal terakhir yang diunduh

    // ======================= UI Flow =======================
    public void Answer()
    {
        if (Aplod) Aplod.SetActive(true);
        if (NamaFileObj) NamaFileObj.SetActive(false);
        if (ButtonNext) ButtonNext.SetActive(false);
    }

    void Awake()
    {
        if (btnBukaFile != null) btnBukaFile.onClick.AddListener(OnPickFile);

        if (btnUpload != null)
        {
            btnUpload.onClick.AddListener(OnUpload);
            btnUpload.interactable = false;
        }

        if (btnBukaHasil != null) btnBukaHasil.onClick.AddListener(OpenLastDownloadedFile);
    }

    private void Start()
    {
        if (LoadingScreen) LoadingScreen.SetActive(true);
        Refresh();
    }

    private void Refresh()
    {
        StartCoroutine(ViewModel.Get("A_002",
            onJson: res =>
            {
                try
                {
                    var root = JSON.Parse(res);
                    if (root == null)
                    {
                        SetNamaFileText("Tidak ada file");
                        return;
                    }

                    JSONNode data = root["data"];
                    if (data == null || data.IsNull)
                    {
                        SetNamaFileText("Tidak ada file");
                        return;
                    }

                    if (data.IsArray && data.Count > 0) data = data[0];

                    string url = data?["file_url_soal"]?.Value ?? string.Empty;
                    string fileNameWithoutExt = string.IsNullOrWhiteSpace(url)
                        ? "Tidak ada file"
                        : Path.GetFileNameWithoutExtension(url);

                    SetNamaFileText(fileNameWithoutExt);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Refresh JSON error: {ex.Message}");
                    SetNamaFileText("Tidak ada file");
                }
                finally
                {
                    if (LoadingScreen) LoadingScreen.SetActive(false);
                }
            },
            onErr: Err =>
            {
                Debug.LogWarning("Refresh error: " + Err);
                if (LoadingScreen) LoadingScreen.SetActive(false);
            }));
    }

    // ======================= PICK FILE =======================
    private void OnPickFile()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanelWithFilters(
            "Pilih Dokumen",
            "",
            new string[] { "Dokumen", "pdf,doc,docx", "PDF", "pdf", "Word", "doc,docx" }
        );
        if (!string.IsNullOrEmpty(path)) TryLoadDocument(path);
        else UpdateInfo("Dibatalkan.");
#elif UNITY_ANDROID
        try
        {
            // Prefer array MIME (NativeFilePicker versi baru)
            string[] mimes = new string[] {
                "application/pdf",
                "application/msword",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            };

            NativeFilePicker.PickFile(
                (path) =>
                {
                    if (string.IsNullOrEmpty(path)) { UpdateInfo("Dibatalkan."); return; }
                    TryLoadDocument(path);
                },
                mimes
            );
        }
        catch (System.Exception)
        {
            // Fallback (plugin lama hanya 1 mime)
            NativeFilePicker.PickFile(
                (path) =>
                {
                    if (string.IsNullOrEmpty(path)) { UpdateInfo("Dibatalkan."); return; }
                    TryLoadDocument(path);
                },
                "application/*"
            );
        }
#else
        UpdateInfo("File picker belum diimplementasi untuk platform ini.");
#endif
    }

    enum DocumentType { PDF, DOC, DOCX, Unknown }

    private void TryLoadDocument(string path)
    {
        try
        {
            var name = Path.GetFileName(path);
            var bytes = File.ReadAllBytes(path);

            if (!IsAllowedDocument(bytes, name, out DocumentType dtype, out string reason))
            {
                UpdateInfo("✖ " + reason);
                btnUpload.interactable = false;
                selectedPath = null; selectedBytes = null; selectedName = null;
                return;
            }

            selectedPath = path;
            selectedBytes = bytes;

            string ext = Path.GetExtension(name);
            if (string.IsNullOrEmpty(ext))
            {
                ext = (dtype == DocumentType.PDF) ? ".pdf" :
                      (dtype == DocumentType.DOC) ? ".doc" :
                      (dtype == DocumentType.DOCX) ? ".docx" : "";
            }
            selectedName = Path.GetFileNameWithoutExtension(name) + ext;

            UpdateInfo($"✔ File siap: {selectedName} ({bytes.Length / 1024f / 1024f:0.00} MB)");
            if (NamaFile) NamaFile.text = selectedName;
            btnUpload.interactable = true;
        }
        catch (Exception e)
        {
            UpdateInfo("Gagal baca file: " + e.Message);
            btnUpload.interactable = false;
        }
    }

    private bool IsAllowedDocument(byte[] bytes, string name, out DocumentType dtype, out string reason)
    {
        dtype = DocumentType.Unknown;

        if (bytes == null || bytes.Length == 0) { reason = "File kosong"; return false; }

        long max = (long)maxSizeMB * 1024L * 1024L;
        if (bytes.Length > max) { reason = $"Ukuran > {maxSizeMB} MB tidak diizinkan"; return false; }

        string ext = Path.GetExtension(name)?.ToLowerInvariant();
        if (ext == ".pdf") dtype = DocumentType.PDF;
        else if (ext == ".doc") dtype = DocumentType.DOC;
        else if (ext == ".docx") dtype = DocumentType.DOCX;
        else { reason = "Hanya .pdf, .doc, atau .docx yang diperbolehkan"; return false; }

        // Signature minimal
        if (dtype == DocumentType.PDF)
        {
            if (bytes.Length < 5 || bytes[0] != 0x25 || bytes[1] != 0x50 || bytes[2] != 0x44 || bytes[3] != 0x46 || bytes[4] != 0x2D)
            { reason = "Bukan PDF valid (header %PDF- tidak ditemukan)"; return false; }
        }
        else if (dtype == DocumentType.DOCX)
        {
            if (bytes.Length < 2 || bytes[0] != 0x50 || bytes[1] != 0x4B)
            { reason = "DOCX tidak valid (header ZIP 'PK' tidak ditemukan)"; return false; }
        }
        else if (dtype == DocumentType.DOC)
        {
            byte[] sig = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
            bool ok = bytes.Length >= sig.Length;
            for (int i = 0; ok && i < sig.Length; i++) if (bytes[i] != sig[i]) ok = false;
            if (!ok) { reason = "DOC tidak valid (signature OLE tidak cocok)"; return false; }
        }

        reason = null;
        return true;
    }

    // ======================= UPLOAD (PDF & DOCS) =======================
    private void OnUpload()
    {
        if (selectedBytes == null) { UpdateInfo("Pilih file dulu."); return; }

        btnUpload.interactable = false;
        UpdateInfo("Mengunggah...");

        // Panggil upload generik kalau ada; kalau tidak, fallback ke UploadPdfBytes (biasanya tetap bisa untuk DOC/DOCX).
        DoUploadBytes(
            selectedBytes,
            selectedName,
            onOk: _s3key =>
            {
                this.s3key = _s3key;
                UpdateInfo("✅ Upload OK → " + _s3key);

                // Optional: update assessment file_url_soal
                string jsonBody = JsonConvert.SerializeObject(new { file_url_soal = _s3key });

                StartCoroutine(ViewModel.Put("A_002", jsonBody,
                    onJson: res =>
                    {
                        UpdateInfo("✓ Data assessment terbarui.");
                        btnUpload.interactable = true;
                        ISFileSudahada = true;
                    },
                    onErr: Err =>
                    {
                        UpdateInfo("Upload OK, tapi gagal update assessment: " + Err);
                        btnUpload.interactable = true;
                    }));
            },
            onErr: err =>
            {
                UpdateInfo("✖ " + err);
                btnUpload.interactable = true;
            });
    }

    public void OnUploadSiswa()
    {
        if (LoadingScreen) LoadingScreen.SetActive(true);

        if (selectedBytes == null)
        {
            UpdateInfo("Pilih file dulu.");
            if (LoadingScreen) LoadingScreen.SetActive(false);
            return;
        }

        btnUpload.interactable = false;
        UpdateInfo("Mengunggah...");

        DoUploadBytes(
            selectedBytes,
            selectedName,
            onOk: _s3key =>
            {
                this.s3key = _s3key;
                UpdateInfo("✅ Upload OK → " + _s3key);
                ISFileSudahada = true;

                if (!isProcessing) StartCoroutine(MulaiFunction());
            },
            onErr: err =>
            {
                UpdateInfo("✖ " + err);
                btnUpload.interactable = true;
                if (LoadingScreen) LoadingScreen.SetActive(false);
            });
    }

    /// <summary>
    /// Upload generik: kalau client punya UploadBytes(data,name,ok,err) dipakai.
    /// Jika tidak, fallback ke UploadPdfBytes(data,name,ok,err).
    /// </summary>
    private void DoUploadBytes(byte[] data, string fileName, Action<string> onOk, Action<string> onErr)
    {
        if (client == null) { onErr?.Invoke("Client null"); return; }

        var t = client.GetType();

        // Coba cari UploadBytes(byte[], string, Action<string>, Action<string>)
        var method = t.GetMethod("UploadBytes", BindingFlags.Public | BindingFlags.Instance);
        if (method != null)
        {
            try
            {
                method.Invoke(client, new object[] { data, fileName, onOk, onErr });
                return;
            }
            catch (Exception e)
            {
                Debug.LogWarning("UploadBytes invoke gagal, fallback ke UploadPdfBytes. " + e.Message);
            }
        }

        // Fallback: UploadPdfBytes(byte[], string, Action<string>, Action<string>)
        var pdfMethod = t.GetMethod("UploadPdfBytes", BindingFlags.Public | BindingFlags.Instance);
        if (pdfMethod != null)
        {
            try
            {
                pdfMethod.Invoke(client, new object[] { data, fileName, onOk, onErr });
                return;
            }
            catch (Exception e)
            {
                onErr?.Invoke("UploadPdfBytes gagal: " + e.Message);
                return;
            }
        }

        onErr?.Invoke("Tidak menemukan method upload pada client.");
    }

    // ======================= RANGKAIAN SUBMISSION → ANSWER → GRADE =======================
    public void AplodTugas() => StartCoroutine(MulaiFunction());

    private IEnumerator MulaiFunction()
    {
        isProcessing = true;

        string UserID = PlayerPrefs.GetString("user_identifier", "1829310");
        string IdSubmission = "";
        var payload = new { user_identifier = UserID, assessment_id = "A_002" };
        bool submitOk = false;

        string body = JsonConvert.SerializeObject(payload);
        yield return StartCoroutine(SubmissionsViewModel.Post(body,
            onJson: raw =>
            {
                var root = JsonConvert.DeserializeObject<Dictionary<string, object>>(raw);
                if (root != null && root.ContainsKey("data"))
                {
                    var dataJson = root["data"].ToString();
                    var dataObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(dataJson);
                    if (dataObj != null && dataObj.ContainsKey("submission_id"))
                    {
                        IdSubmission = dataObj["submission_id"].ToString();
                        submitOk = true;
                    }
                }
            },
            onErr: Err => Debug.Log("Error : " + Err + " gagal Submission")));

        if (!submitOk)
        {
            isProcessing = false;
            if (LoadingScreen) LoadingScreen.SetActive(false);
            yield break;
        }

        if (!ISFileSudahada)
        {
            yield return new WaitUntil(() => ISFileSudahada);
        }

        string jsonAnswer = JsonConvert.SerializeObject(new
        {
            submission_id = IdSubmission,
            question_id = "Q005",
            answer_text = s3key
        });

        yield return StartCoroutine(AnswerViewModel.PostAnswer(jsonAnswer,
            onJson: _ =>
            {
                ISFileSudahada = false;
            },
            onErr: Err => Debug.Log("Error : " + Err + " gagal Answer")));

        string jsonGrade = JsonConvert.SerializeObject(new
        {
            submission_id = IdSubmission,
            user_identifier = UserID,
            score = 0.0f
        });

        yield return StartCoroutine(GradeViewModel.Post(jsonGrade,
            onJson: _ => { },
            onErr: Err => Debug.Log("Error : " + Err + " gagal Grade")));

        if (Hasil) Hasil.SetActive(true);
        if (Aplod) Aplod.SetActive(false);
        if (LoadingScreen) LoadingScreen.SetActive(false);

        SceneManager.LoadScene("Home");
        isProcessing = false;
    }

    // ======================= OPEN FILE (Unity-Android-Files-Opener) =======================
    /// <summary>
    /// Buka file lokal memakai plugin Unity-Android-Files-Opener jika ada.
    /// Jika tidak ada, fallback ke Intent/FileProvider bawaan.
    /// </summary>
    private void OpenLocalWithAndroidFilesOpener(string localPath)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            // Coba panggil kelas C# plugin: AndroidFilesOpener.OpenFile(string path)
            var openerType = Type.GetType("AndroidFilesOpener");
            if (openerType != null)
            {
                var m = openerType.GetMethod("OpenFile", BindingFlags.Public | BindingFlags.Static);
                if (m != null)
                {
                    m.Invoke(null, new object[] { localPath });
                    return;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("AndroidFilesOpener tidak tersedia / gagal dipanggil: " + e.Message);
        }

        // Fallback ke Intent manual
        OpenLocalFileAndroid_Fallback(localPath, MimeFromExt(Path.GetExtension(localPath)?.ToLowerInvariant()));
#else
        Application.OpenURL(localPath);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void OpenLocalFileAndroid_Fallback(string localPath, string mime)
    {
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var file = new AndroidJavaObject("java.io.File", localPath))
        {
            if (!file.Call<bool>("exists")) { Debug.LogWarning("File not found"); return; }

            string authority = activity.Call<string>("getPackageName") + ".fileprovider";
            using (var fileProvider = new AndroidJavaClass("androidx.core.content.FileProvider"))
            {
                var uri = fileProvider.CallStatic<AndroidJavaObject>("getUriForFile", activity, authority, file);

                using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW"))
                {
                    intent.Call<AndroidJavaObject>("setDataAndType", uri, mime);
                    intent.Call<AndroidJavaObject>("addFlags", 1);           // FLAG_GRANT_READ_URI_PERMISSION
                    intent.Call<AndroidJavaObject>("addFlags", 268435456);   // FLAG_ACTIVITY_NEW_TASK

                    try { activity.Call("startActivity", intent); }
                    catch (AndroidJavaException)
                    {
                        Debug.LogWarning("Tidak ada app yang bisa membuka file ini.");
                    }
                }
            }
        }
    }
#endif

    private string MimeFromExt(string extLower)
    {
        switch (extLower)
        {
            case ".pdf": return "application/pdf";
            case ".doc": return "application/msword";
            case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            default: return "application/octet-stream";
        }
    }

    // Minta presigned URL → download → simpan → buka
    public void DownloadAndOpenFromS3Key()
    {
        if (string.IsNullOrEmpty(s3key)) { UpdateInfo("Belum ada file."); return; }

        // Pastikan client punya GetDownloadLink(s3key, ok, err)
        var t = client?.GetType();
        var m = t?.GetMethod("GetDownloadLink", BindingFlags.Public | BindingFlags.Instance);
        if (m == null) { UpdateInfo("Client tidak punya GetDownloadLink."); return; }

        Action<string> onOk = (url) => StartCoroutine(DownloadToLocalAndOpen(url, Path.GetFileName(s3key)));
        Action<string> onErr = (err) => UpdateInfo("Gagal dapat link: " + err);
        m.Invoke(client, new object[] { s3key, onOk, onErr });
    }

    private IEnumerator DownloadToLocalAndOpen(string url, string fileName)
    {
        string localPath = Path.Combine(Application.persistentDataPath, fileName);

#if UNITY_2020_3_OR_NEWER
        using (var req = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(localPath);
            yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                UpdateInfo("Gagal download: " + req.error);
                yield break;
            }
        }
#else
        using (var req = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.isNetworkError || req.isHttpError)
            {
                UpdateInfo("Gagal download: " + req.error);
                yield break;
            }
            File.WriteAllBytes(localPath, req.downloadHandler.data);
        }
#endif

        lastDownloadLocalPath = localPath;
        UpdateInfo("✓ Tersimpan di: " + localPath);
        OpenLocalWithAndroidFilesOpener(localPath);
    }

    private void OpenLastDownloadedFile()
    {
        if (string.IsNullOrEmpty(lastDownloadLocalPath) || !File.Exists(lastDownloadLocalPath))
        {
            UpdateInfo("Belum ada file yang diunduh.");
            return;
        }
        OpenLocalWithAndroidFilesOpener(lastDownloadLocalPath);
    }

    // ======================= UTIL =======================
    private void SetNamaFileText(string text)
    {
        if (NamaFile != null) NamaFile.text = text;
        else Debug.LogError("Object 'NamaFile' belum di-assign di Inspector.");
    }

    public void Upload() // legacy binder
    {
        if (btnBukaFile != null && btnUpload != null)
        {
            btnBukaFile.onClick.AddListener(OnPickFile);
            btnUpload.onClick.AddListener(OnUpload);
        }
    }

    private void UpdateInfo(string msg)
    {
        Debug.Log(msg);
        if (NamaFileSiswa != null) NamaFileSiswa.text = msg;
    }
}
