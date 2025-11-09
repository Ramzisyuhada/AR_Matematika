using Newtonsoft.Json;
using SimpleJSON;
using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class QuestionView : MonoBehaviour
{
    [Header("Refs")]
    public PdfPresignClient client;
    public Button btnBukaFile;
    public Button btnUpload;
    public GameObject Loading;

    private string s3Key;

    public Text infoText; // boleh ganti ke TextMeshProUGUI jika pakai TMP

    [Header("Logic")]
    [SerializeField] private QuestionVM ViewModel;

    [Header("Limit")]
    public int maxSizeMB = 5;

    [SerializeField] TMP_Text NamaFile;
    private string selectedPath;
    private byte[] selectedBytes;
    private string selectedName;

    [Header("UI Notif")]
    public GameObject ObjekFileTerlaluBesar; // drag di Inspector (misal panel merah)
    public GameObject ObjekBerhasil;

    // ----------------- Utility Busy -----------------
    void Busy(bool on)
    {
        if (Loading != null) Loading.SetActive(on);
        if (btnUpload != null) btnUpload.interactable = !on;
        if (btnBukaFile != null) btnBukaFile.interactable = !on;
    }

    void Awake()
    {
        if (btnBukaFile != null && btnUpload != null)
        {
            btnBukaFile.onClick.AddListener(OnPickFile);
            btnUpload.onClick.AddListener(OnUpload);
            btnUpload.interactable = false;
        }

        if (Loading != null) Loading.SetActive(false); // off di awal
        UpdateInfo("Belum ada file dipilih");
    }

    private void Start()
    {
        Refresh();
    }

    private void Refresh()
    {
        // NOTE: Busy() di Refresh bukan terkait download file pengguna, jadi tidak diubah.
        Busy(true);
        StartCoroutine(ViewModel.Get("Q005",
            onJson: raw =>
            {
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
                    if (NamaFile != null) NamaFile.text = qText;
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Refresh] Exception: " + ex);
                }
                finally
                {
                    Busy(false);
                }
            },
            onErr: err =>
            {
                Debug.LogError("[Refresh] Error ViewModel.Get: " + err);
                Busy(false);
            }
        ));
    }

    void OnPickFile()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("Pilih PDF", "", "pdf");
        if (!string.IsNullOrEmpty(path))
            TryLoadPdf(path);
#elif UNITY_ANDROID
        try
        {
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
                "application/pdf"
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

                // Khusus > 5MB → tampilkan panel
                if (!string.IsNullOrEmpty(reason) && reason.StartsWith("Ukuran >") && ObjekFileTerlaluBesar != null)
                    Show5mb();

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

    void Show5mb()
    {
        if (ObjekFileTerlaluBesar == null) return;

        ObjekFileTerlaluBesar.SetActive(true);
        ObjekFileTerlaluBesar.transform.localScale = Vector3.zero;
        LeanTween.scale(ObjekFileTerlaluBesar, Vector3.one, 0.4f).setEaseOutBack();
        LeanTween.moveLocalX(ObjekFileTerlaluBesar, ObjekFileTerlaluBesar.transform.localPosition.x + 10f, 0.05f)
            .setEaseShake()
            .setDelay(0.4f);
        LeanTween.delayedCall(2f, () =>
        {
            LeanTween.scale(ObjekFileTerlaluBesar, Vector3.zero, 0.3f)
                .setEaseInBack()
                .setOnComplete(() => ObjekFileTerlaluBesar.SetActive(false));
        });
    }

    void ShowPemberitahuan()
    {
        if (ObjekBerhasil == null) return;

        ObjekBerhasil.SetActive(true);
        ObjekBerhasil.transform.localScale = Vector3.zero;
        LeanTween.scale(ObjekBerhasil, Vector3.one, 0.4f).setEaseOutBack();
        LeanTween.moveLocalX(ObjekBerhasil, ObjekBerhasil.transform.localPosition.x + 10f, 0.05f)
            .setEaseShake()
            .setDelay(0.4f);
        LeanTween.delayedCall(2f, () =>
        {
            LeanTween.scale(ObjekBerhasil, Vector3.zero, 0.3f)
                .setEaseInBack()
                .setOnComplete(() => ObjekBerhasil.SetActive(false));
        });
    }

    void OnUpload()
    {
        if (selectedBytes == null)
        {
            UpdateInfo("Pilih file dulu.");
            return;
        }

        // Upload ke storage (ini bukan “download pengguna”, jadi tidak mengubah kebijakan Loading)
        Busy(true);
        UpdateInfo("Mengunggah...");

        client.UploadPdfBytes(selectedBytes, selectedName, s3key =>
        {
            UpdateInfo("✅ Upload OK → " + s3key);

            client.GetDownloadLink(s3key, presignUrl =>
            {
                UpdateInfo($"✅ Upload OK\nDownload: {presignUrl}");

                string jsonBody = JsonConvert.SerializeObject(new
                {
                    question_text = s3key
                });

                StartCoroutine(ViewModel.Put("Q005", jsonBody,
                    onJson: res =>
                    {
                        if (ObjekBerhasil != null) ShowPemberitahuan();
                        Debug.Log("Presigned URL: " + presignUrl);
                        Busy(false);
                    },
                    onErr: Err =>
                    {
                        Debug.LogWarning("Error simpan: " + Err);
                        Busy(false);
                    }
                ));
            },
            err =>
            {
                UpdateInfo("Upload OK, tapi gagal ambil link: " + err);
                Busy(false);
            });
        },
        err =>
        {
            UpdateInfo("✖ " + err);
            Busy(false);
        });
    }

    bool IsPdf(byte[] bytes, string name, out string reason)
    {
        if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            reason = "Hanya .pdf yang diperbolehkan";
            return false;
        }
        if (bytes == null || bytes.Length == 0)
        {
            reason = "File kosong";
            return false;
        }

        long max = (long)maxSizeMB * 1024L * 1024L;
        if (bytes.Length > max)
        {
            reason = $"Ukuran > {maxSizeMB} MB tidak diizinkan";
            return false;
        }

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

        // Jika file sudah ada lokal → buka TANPA loading
        if (File.Exists(localPath))
        {
            Debug.Log("[OpenLocalOrDownload] File lokal sudah ada → buka");
            OpenFile(localPath); // ← GANTI: selalu pakai viewer sesuai MIME
            return;
        }

        // Belum ada → presign + DOWNLOAD (aktifkan loading HANYA di jalur ini)
        UpdateInfo("Mengambil link unduh...");
        Busy(true);
        client.GetDownloadLink(
            s3Key,
            url => StartCoroutine(DownloadToFile(url, fileName)),
            err =>
            {
                UpdateInfo("[OpenLocalOrDownload] Presign gagal: " + err);
                Busy(false);
            }
        );
    }

    // ============ DOWNLOAD ============ (Loading aktif hanya di jalur ini)
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
                Busy(false); // matikan loading meskipun gagal
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

            // 5) Buka dengan viewer sesuai MIME
            OpenFile(savePath); // ← GANTI: bukan NativeShare
        }

        // 6) MATIKAN LOADING setelah download selesai (sukses/gagal sudah di-handle)
        Busy(false);
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

            AndroidJavaClass clipDataClass = new AndroidJavaClass("android.content.ClipData");
            AndroidJavaObject clip = clipDataClass.CallStatic<AndroidJavaObject>(
                "newUri",
                new AndroidJavaObject("java.lang.CharSequence", "File"),
                new AndroidJavaObject("java.lang.String", "text/uri-list"),
                uri
            );
            intent.Call("setClipData", clip);

            AndroidJavaObject chooser =
                intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, new AndroidJavaObject("java.lang.String", "Buka dengan"));

            Debug.Log($"[OpenFile] uri={uri?.Call<string>("toString")} mime={mime} authority={authority}");
            activity.Call("startActivity", chooser);
        }
        catch (AndroidJavaException aje)
        {
            Debug.LogError("[OpenFile] AndroidJavaException: " + aje);
            if (aje.ToString().Contains("ActivityNotFoundException"))
                Debug.LogWarning("Tidak ada app viewer. Install PDF/Word viewer (Google Drive/Adobe/WPS/Microsoft Word).");

            Application.OpenURL("file://" + localPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[OpenFile] Exception: " + e.Message);
            Application.OpenURL("file://" + localPath);
        }
#else
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
