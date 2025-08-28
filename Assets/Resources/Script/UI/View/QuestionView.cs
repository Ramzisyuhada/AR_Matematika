using Newtonsoft.Json;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Text.RegularExpressions;

using UnityEngine.Networking;

#if UNITY_EDITOR
using UnityEditor;
#endif
public class QuestionView : MonoBehaviour
{
    [Header("Refs")]
    public PdfPresignClient client;
    public Button btnBukaFile;
    public Button btnUpload;
    private string s3Key;

    public Text infoText; // boleh ganti TextMeshProUGUI jika pakai TMP
    [Header("Logic")]
    [SerializeField] private QuestionVM ViewModel;

    [Header("Limit")]
    public int maxSizeMB = 5;

    [SerializeField] TMP_Text NamaFile;
    private string selectedPath;
    private byte[] selectedBytes;
    private string selectedName;

    void Awake()
    {
        if (btnBukaFile != null && btnUpload != null)
        {
            btnBukaFile.onClick.AddListener(OnPickFile);
            btnUpload.onClick.AddListener(OnUpload);
            btnUpload.interactable = false;

        }

        UpdateInfo("Belum ada file dipilih");
    }

    private void Start()
    {
        Refresh();
    }
    private void Refresh()
    {
        // Misal "Q005" adalah id soal; silakan ganti sesuai kebutuhan
       StartCoroutine( ViewModel.Get("Q005",
            onJson: raw =>
            {
                Debug.Log("Hello world");

                try
                {
                    var root = JSON.Parse(raw);
                    if (root == null)
                    {
                        Debug.LogError("[Refresh] JSON parse null");
                        return;
                    }

                    // CASE 1: API langsung kirim { "question_text": "..." }
                    string qText = root["question_text"]?.Value;

                    // CASE 2 (umum di Laravel): { "success": true, "data": { "question_text": "..." } }
                    if (string.IsNullOrEmpty(qText))
                        qText = root["data"]?["question_text"]?.Value;

                    if (string.IsNullOrEmpty(qText))
                    {
                        Debug.LogWarning("[Refresh] 'question_text' tidak ditemukan di payload: " + raw);
                        return;
                    }

                    s3Key = qText;
                    Debug.Log("[Refresh] question_text: " + qText);

                    if (NamaFile == null)
                    {
                        Debug.LogError("[Refresh] NamaFile == null (assign gagal)");
                        return; // <-- sekarang return hanya saat null
                    }

                    NamaFile.text = qText;
                    Debug.Log("[Refresh] Berhasil set NamaFile");
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Refresh] Exception: " + ex);
                }
            },
            onErr: err =>
            {
                Debug.LogError("[Refresh] Error ViewModel.Get: " + err);
            }
        ));
    }

    void OnPickFile()
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Pilih PDF", "", "pdf");
        if (!string.IsNullOrEmpty(path))
            TryLoadPdf(path);
#elif UNITY_ANDROID
    try
    {
        // SIGNATURE LAMA: PickFile(PickCallback, string mime)
        NativeFilePicker.PickFile(
            (path) =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    UpdateInfo("Dibatalkan.");
                    return;
                }
                TryLoadPdf(path);
            },
            "application/pdf" // <- STRING (bukan string[])
        );
    }
    catch (System.Exception ex)
    {
        UpdateInfo("Gagal membuka file picker: " + ex.Message);
    }
#else
    UpdateInfo("File picker runtime belum diimplementasi untuk platform ini.");
#endif
    }


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

                    question_text = s3key
                });

                StartCoroutine(ViewModel.Put("Q005", jsonBody, onJson: res =>
                {
                    Debug.Log("Presigned URL: " + presignUrl);
                    btnUpload.interactable = true;


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
        if (infoText != null) infoText.text = msg;
        Debug.Log(msg);
    }
    private void OpenWithNativeShare(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning("[OpenWithNativeShare] File tidak ada: " + path);
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
    // Detect MIME dari ekstensi supaya viewer tepat
    string mime = GetMimeTypeForShare(path);
    new NativeShare()
        .AddFile(path, mime)
        .SetSubject("Open file")
        .SetText(" ")
        .Share(); // tampil chooser → pilih app yang bisa buka
#else
        Application.OpenURL(path);
#endif
    }

    private string GetMimeTypeForShare(string path)
    {
        string p = path.ToLowerInvariant();
        if (p.EndsWith(".pdf")) return "application/pdf";
        if (p.EndsWith(".jpg") || p.EndsWith(".jpeg")) return "image/jpeg";
        if (p.EndsWith(".png")) return "image/png";
        if (p.EndsWith(".doc")) return "application/msword";
        if (p.EndsWith(".docx")) return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        if (p.EndsWith(".ppt")) return "application/vnd.ms-powerpoint";
        if (p.EndsWith(".pptx")) return "application/vnd.openxmlformats-officedocument.presentationml.presentation";
        if (p.EndsWith(".xls")) return "application/vnd.ms-excel";
        if (p.EndsWith(".xlsx")) return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        if (p.EndsWith(".txt")) return "text/plain";
        if (p.EndsWith(".mp4")) return "video/mp4";
        if (p.EndsWith(".zip")) return "application/zip";
        return "*/*";
    }


    public void OnClickOpenLocalOrDownload()
    {
        if (string.IsNullOrWhiteSpace(s3Key))
        {
            UpdateInfo("[OpenLocalOrDownload] s3Key kosong");
            return;
        }

        string fileName = Path.GetFileName(s3Key);
        if (string.IsNullOrWhiteSpace(fileName)) fileName = "file.pdf";
        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        if (File.Exists(localPath))
        {
            Debug.Log("[OpenLocalOrDownload] File lokal sudah ada → buka");
            OpenWithNativeShare(localPath);
            return;
        }

        // Belum ada → presign & download
        UpdateInfo("Mengambil link unduh...");
        client.GetDownloadLink(
            s3Key,
            url => StartCoroutine(DownloadToFile(url, fileName)),
            err => UpdateInfo("[OpenLocalOrDownload] Presign gagal: " + err)
        );
    }

    // ============ DOWNLOAD ============
    private IEnumerator DownloadToFile(string url, string fallbackFileName)
    {
        // 1) Tentukan nama file yang rapi
        string finalName = string.IsNullOrWhiteSpace(fallbackFileName) ? "file" : fallbackFileName;
        try
        {
            var uri = new Uri(url);
            var fromUrl = Path.GetFileName(uri.AbsolutePath);
            if (!string.IsNullOrEmpty(fromUrl)) finalName = fromUrl;
        }
        catch { /* ignore */ }

        string savePath = Path.Combine(Application.persistentDataPath, finalName);
        Debug.Log("[DownloadToFile] → " + savePath);

        // 2) Download langsung ke file (hemat RAM)
        using (var req = UnityWebRequest.Get(url))
        {
            req.downloadHandler = new DownloadHandlerFile(savePath, true);
            req.timeout = 90;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Download failed: {req.responseCode} {req.error}");
                yield break;
            }

            // 3) Coba rename dari Content-Disposition (jika ada)
            string cd = req.GetResponseHeader("Content-Disposition");
            if (!string.IsNullOrEmpty(cd))
            {
                var m = Regex.Match(cd, "filename\\*=UTF-8''([^;]+)|filename=\"?([^\";]+)\"?");
                if (m.Success)
                {
                    string headerName = Uri.UnescapeDataString(!string.IsNullOrEmpty(m.Groups[1].Value)
                                                               ? m.Groups[1].Value
                                                               : m.Groups[2].Value);
                    var newPath = Path.Combine(Application.persistentDataPath, headerName);
                    try
                    {
                        if (!savePath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
                        {
                            if (File.Exists(newPath)) File.Delete(newPath);
                            File.Move(savePath, newPath);
                            savePath = newPath;
                        }
                    }
                    catch (Exception ex) { Debug.LogWarning("[DownloadToFile] Rename error: " + ex.Message); }
                }
            }

            // 4) Pastikan ada ekstensi (khususnya PDF)
            string contentType = req.GetResponseHeader("Content-Type") ?? "";
            if (!Path.HasExtension(savePath) && contentType.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                string withExt = savePath + ".pdf";
                try
                {
                    if (File.Exists(withExt)) File.Delete(withExt);
                    File.Move(savePath, withExt);
                    savePath = withExt;
                }
                catch (Exception ex) { Debug.LogWarning("[DownloadToFile] Add .pdf error: " + ex.Message); }
            }

            long size = new FileInfo(savePath).Length;
            Debug.Log($"[DownloadToFile] Saved {size} bytes at {savePath} (Content-Type={contentType})");

            // 5) Buka dengan NativeShare (chooser ke PDF viewer / app terkait)
            OpenWithNativeShare(savePath);
        }
    }

    // ============ OPEN FILE (ANDROID aman dengan FileProvider) ============
    private void OpenFile(string localPath)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
    try
    {
        if (!System.IO.File.Exists(localPath))
        {
            Debug.LogError("[OpenFile] File tidak ditemukan: " + localPath);
            return;
        }

        long size = new System.IO.FileInfo(localPath).Length;
        Debug.Log($"[OpenFile] path={localPath} size={size}B");

        AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");
        AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent");
        intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_VIEW"));

        AndroidJavaObject activity = GetUnityActivity();
        string authority = Application.identifier + ".fileprovider";
        AndroidJavaClass uriClass = new AndroidJavaClass("androidx.core.content.FileProvider");
        AndroidJavaObject fileObj = new AndroidJavaObject("java.io.File", localPath);
        AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("getUriForFile", activity, authority, fileObj);

        string mime = GetMimeType(localPath);
        intent.Call<AndroidJavaObject>("setDataAndType", uri, mime);

        const int FLAG_GRANT_READ_URI_PERMISSION = 1;
        const int FLAG_ACTIVITY_CLEAR_TOP = 0x04000000;
        intent.Call<AndroidJavaObject>("addFlags", FLAG_GRANT_READ_URI_PERMISSION);
        intent.Call<AndroidJavaObject>("addFlags", FLAG_ACTIVITY_CLEAR_TOP);

        // ClipData agar grant URI lebih konsisten di beberapa OEM
        AndroidJavaClass clipDataClass = new AndroidJavaClass("android.content.ClipData");
        AndroidJavaObject clip = clipDataClass.CallStatic<AndroidJavaObject>(
            "newUri",
            new AndroidJavaObject("java.lang.String", "File"),
            new AndroidJavaObject("java.lang.String", "text/uri-list"),
            uri
        );
        intent.Call("setClipData", clip);

        // (Opsional) chooser – gunakan String sbg judul (CharSequence tidak bisa di-new)
        AndroidJavaObject chooser =
            intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, new AndroidJavaObject("java.lang.String", "Buka dengan"));

        Debug.Log($"[OpenFile] uri={uri?.Call<string>("toString")} mime={mime} authority={authority}");
        activity.Call("startActivity", chooser); // atau pakai 'intent' langsung
    }
    catch (AndroidJavaException aje)
    {
        Debug.LogError("[OpenFile] AndroidJavaException: " + aje);
        if (aje.ToString().Contains("ActivityNotFoundException"))
            Debug.LogWarning("Tidak ada app viewer. Install PDF viewer (Google Drive/Adobe/WPS).");

        Application.OpenURL("file://" + localPath); // fallback
    }
    catch (System.Exception e)
    {
        Debug.LogError("[OpenFile] Exception: " + e.Message);
        Application.OpenURL("file://" + localPath); // fallback
    }
#else
        // Editor / non-Android
        Application.OpenURL(localPath);
#endif
    }




    private string GetMimeType(string path)
    {
        string p = path.ToLowerInvariant();
        if (p.EndsWith(".pdf")) return "application/pdf";
        if (p.EndsWith(".jpg") || p.EndsWith(".jpeg")) return "image/jpeg";
        if (p.EndsWith(".png")) return "image/png";
        if (p.EndsWith(".doc")) return "application/msword";
        if (p.EndsWith(".docx")) return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        if (p.EndsWith(".ppt")) return "application/vnd.ms-powerpoint";
        if (p.EndsWith(".pptx")) return "application/vnd.openxmlformats-officedocument.presentationml.presentation";
        if (p.EndsWith(".xls")) return "application/vnd.ms-excel";
        if (p.EndsWith(".xlsx")) return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        if (p.EndsWith(".txt")) return "text/plain";
        if (p.EndsWith(".mp4")) return "video/mp4";
        if (p.EndsWith(".zip")) return "application/zip";
        return "*/*";
    }

    private AndroidJavaObject GetUnityActivity()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
#else
        return null;
#endif
    }
}
