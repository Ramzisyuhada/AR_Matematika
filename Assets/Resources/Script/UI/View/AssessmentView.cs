using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;
using UnityEditor;
using System.Runtime.CompilerServices;
using TMPro;
using SimpleJSON;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
#if UNITY_EDITOR
using UnityEditor;
#endif
public class AssessmentView : MonoBehaviour
{
    [Header("Refs")]
    public PdfPresignClient client;
    public Button btnBukaFile;
    public Button btnUpload;
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

    private string selectedPath;
    private byte[] selectedBytes;
    private string selectedName;
    private bool ISFileSudahada;
    private bool isProcessing;

    public void Answer()
    {
        Aplod.SetActive(true);
        NamaFileObj.SetActive(false);
        ButtonNext.SetActive(false);
    }
    void Awake()
    {
        if (btnBukaFile != null)
        {
            btnBukaFile.onClick.AddListener(OnPickFile);
            btnUpload.onClick.AddListener(OnUpload);
            btnUpload.interactable = false;

        }

    }
    private void Start()
    {
        LoadingScreen.SetActive(true);
        Refresh();

    }
    private void Refresh()
    {
        StartCoroutine(ViewModel.Get("A_002", 
        onJson: res => {
            try
            {
                var root = JSON.Parse(res);
                if (root == null)
                {
                    Debug.LogWarning("Gagal parse JSON: null");
                    return;
                }

                // Ambil node "data" (bisa object atau array tergantung API)
                JSONNode data = root["data"];
                if (data == null || data.IsNull)
                {
                    Debug.LogWarning("JSON tidak memiliki key 'data'.");
                    SetNamaFileText("Tidak ada file");
                    return;
                }

                // Jika "data" berupa array, ambil elemen pertama
                if (data.IsArray && data.Count > 0)
                    data = data[0];

                // Ambil url sebagai string aman
                string url = data?["file_url_soal"]?.Value ?? string.Empty;

                // Tentukan nama file tanpa ekstensi, fallback kalau kosong
                string fileNameWithoutExt = string.IsNullOrWhiteSpace(url)
                    ? "Tidak ada file"
                    : Path.GetFileNameWithoutExtension(url);

                SetNamaFileText(fileNameWithoutExt);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error saat memproses JSON: {ex.Message}");
                SetNamaFileText("Tidak ada file");
            }
            finally
            {
                if (LoadingScreen) LoadingScreen.SetActive(false);
            }

        }
        , onErr: Err =>
        {

        }));
    }
    private void OpenWithNativeShare(string path)
    {
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning("[OpenWithNativeShare] File tidak ada: " + path);
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
    // NativeShare otomatis pakai FileProvider + grant permission
    new NativeShare()
        .AddFile(path, "application/pdf")
        .SetSubject("Open PDF")
        .SetText(" ")
        .Share(); // tampil chooser → pilih PDF viewer
#else
        Application.OpenURL(path);
#endif
    }


    private void SetNamaFileText(string text)
    {
        if (NamaFile != null) NamaFile.text = text;
        else Debug.LogError("Object 'NamaFile' belum di-assign di Inspector.");
    }
    public void Upload()
    {
        btnBukaFile.onClick.AddListener(OnPickFile);
        btnUpload.onClick.AddListener(OnUpload);
    }

    private void OnPickFile()
    {
#if UNITY_EDITOR
        // Editor: panel file bawaan Unity
        string path = UnityEditor.EditorUtility.OpenFilePanel("Pilih PDF", "", "pdf");
        if (!string.IsNullOrEmpty(path))
            TryLoadPdf(path);
        else
            UpdateInfo("Dibatalkan.");
#elif UNITY_ANDROID
    try
    {
        // Versi lama NativeFilePicker:
        // Signature: PickFile(PickCallback callback, string mime)
        NativeFilePicker.PickFile(
            (path) =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    UpdateInfo("Dibatalkan.");
                    return;
                }

                TryLoadPdf(path); // validasi & load file kamu
            },
            "application/pdf" // <- STRING tunggal, bukan string[]
        );
    }
    catch (System.Exception ex)
    {
        UpdateInfo("Gagal membuka file picker: " + ex.Message);
    }
#else
    UpdateInfo("File picker belum diimplementasi untuk platform ini.");
#endif
    }



    public void AplodTugas()
    {
        StartCoroutine(MulaiFunction());
    }
    private IEnumerator MulaiFunction()
    {
        string UserID = PlayerPrefs.GetString("user_identifier", "1829310");
        string IdSubmission = "";
        var payload = new
        {
            user_identifier = UserID,
            assessment_id = "A_002",
        };
        bool submitOk = false;

        string body = JsonConvert.SerializeObject(payload);
        yield return StartCoroutine(SubmissionsViewModel.Post(body , onJson : raw =>
        {
            var root = JsonConvert.DeserializeObject<Dictionary<string, object>>(raw);

            if (root.ContainsKey("data"))
            {
                Debug.Log("Berhasil Submision");


                var dataJson = root["data"].ToString();
                var dataObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(dataJson);
                if (dataObj.ContainsKey("submission_id"))
                {
                    IdSubmission = dataObj["submission_id"].ToString();
                    Debug.Log($"✓ Submit OK, SubmissionID: {IdSubmission}");
                    submitOk = true;
                }
            }
            else
            {
            }
        }, onErr: Err =>
        {
            Debug.Log("Error : "+ Err + " gagal Submision");
        }));

        if (!submitOk)
        {
            isProcessing = false;
            yield break;
        }

        // 2) TUNGGU sampai file sudah ada (hasil upload)
        if (!ISFileSudahada)
        {
            Debug.Log("[MulaiFunction] Menunggu file diupload...");
            yield return new WaitUntil(() => ISFileSudahada);
        }
        string jsonBody = JsonConvert.SerializeObject(new
        {
            submission_id = IdSubmission,
            question_id = "Q005",
            answer_text = s3key
        });
        yield return StartCoroutine(AnswerViewModel.PostAnswer(jsonBody, onJson: res =>
        {
            
            Debug.Log("Berhasil Jawavab");
            ISFileSudahada = false;
        }, onErr: Err =>
        {
            Debug.Log("Error : " + Err + " gagal Answer");
        }));
        string jsonBody1 = JsonConvert.SerializeObject(new
        {
            submission_id = IdSubmission,
            user_identifier = UserID,
            score = 0.0f
        });
        yield return StartCoroutine(GradeViewModel.Post(jsonBody1, onJson: res =>
        {

            Debug.Log("Berhasil Nilai ");
            ISFileSudahada = false;
        }, onErr: Err =>
        {
            Debug.Log("Error : " + Err + " gagal Grade");
        }));
        Hasil.SetActive(true);
        Aplod.SetActive(false);
        LoadingScreen.SetActive(false);

        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(s3key);
        
        //var Pa
        //yield return StartCoroutine(AnswerViewModel.PostAnswer())

    }
    string s3key;
    void TryLoadPdf(string path)
    {
        try
        {
            var name = Path.GetFileName(path);
            var bytes = File.ReadAllBytes(path);

            string reason;
            if (!IsPdf(bytes, name, out reason))
            {
                UpdateInfo("✖ " + reason);
                btnUpload.interactable = false;
                selectedPath = null; selectedBytes = null; selectedName = null;
                return;
            }

            selectedPath = path;
            selectedBytes = bytes;
            selectedName = name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? name : (name + ".pdf");
            UpdateInfo($"✔ File siap: {selectedName} ({bytes.Length / 1024f / 1024f:0.00} MB)");
            btnUpload.interactable = true;
        }
        catch (Exception e)
        {
            UpdateInfo("Gagal baca file: " + e.Message);
            btnUpload.interactable = false;
        }
    }

    
    void OnUpload()
    {
        if (selectedBytes == null)
        {
            UpdateInfo("Pilih file dulu.");
            return;
        }
        btnUpload.interactable = false;
        UpdateInfo("Mengunggah...");

        // kunci path di S3 sisi backend ke uploads/pdf/, cukup kirim nama file ke presign
        client.UploadPdfBytes(selectedBytes, selectedName, s3key =>
        {
            UpdateInfo("✅ Upload OK → " + s3key);
            client.GetDownloadLink(s3key, presignUrl =>
            {
                // Tampilkan URL ke user
                UpdateInfo($"✅ Upload OK\nDownload: {presignUrl}");
                string s = PlayerPrefs.GetString("SubmissionId", "-");

                string jsonBody = JsonConvert.SerializeObject(new
                {
                    file_url_soal = s3key
                });
                this.s3key = s3key;

                StartCoroutine(ViewModel.Put("A_002", jsonBody, onJson: res =>
                {
                    Debug.Log("Presigned URL: " + presignUrl);
                    btnUpload.interactable = true;

                    ISFileSudahada = true;
                }, onErr: Err =>
                {
                    Debug.LogWarning("Error : " + Err);

                }));



            },
      err =>
      {
          UpdateInfo("Upload OK, tapi gagal ambil link: " + err);
          btnUpload.interactable = true;
      });
        },
        err =>
        {
            UpdateInfo("✖ " + err);
            btnUpload.interactable = true;
        });
    }
    public void OnUploadSiswa()
    {
        LoadingScreen.SetActive(true);

        if (selectedBytes == null)
        {
            UpdateInfo("Pilih file dulu.");
            return;
        }
        btnUpload.interactable = false;
        UpdateInfo("Mengunggah...");

        // kunci path di S3 sisi backend ke uploads/pdf/, cukup kirim nama file ke presign
        client.UploadPdfBytes(selectedBytes, selectedName, s3key =>
        {
            UpdateInfo("✅ Upload OK → " + s3key);
            client.GetDownloadLink(s3key, presignUrl =>
            {
                // Tampilkan URL ke user
                UpdateInfo($"✅ Upload OK\nDownload: {presignUrl}");
                string s = PlayerPrefs.GetString("SubmissionId", "-");

                string jsonBody = JsonConvert.SerializeObject(new
                {
                    file_url_soal = s3key
                });
                this.s3key= s3key;
              
                ISFileSudahada = true ;
                if (!isProcessing)
                {
                    StartCoroutine(MulaiFunction());
                }

            },
      err =>
      {
          UpdateInfo("Upload OK, tapi gagal ambil link: " + err);
          btnUpload.interactable = true;
      });
        },
        err =>
        {
            UpdateInfo("✖ " + err);
            btnUpload.interactable = true;
        });
    }
    bool IsPdf(byte[] bytes, string name, out string reason)
    {
        if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            reason = "Hanya .pdf yang diperbolehkan";
            return false;
        }
        if (bytes == null || bytes.Length == 0) { reason = "File kosong"; return false; }

        long max = (long)maxSizeMB * 1024L * 1024L;
        if (bytes.Length > max) { reason = $"Ukuran > {maxSizeMB} MB tidak diizinkan"; return false; }

        // header %PDF-
        if (bytes.Length < 5 || bytes[0] != 0x25 || bytes[1] != 0x50 || bytes[2] != 0x44 || bytes[3] != 0x46 || bytes[4] != 0x2D)
        {
            reason = "Bukan PDF valid (header %PDF- tidak ditemukan)";
            return false;
        }

        reason = null;
        return true;
    }

    void UpdateInfo(string msg)
    {
        Debug.Log(msg);
    }
}
