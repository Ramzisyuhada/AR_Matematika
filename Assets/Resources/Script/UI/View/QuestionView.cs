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
        string path = EditorUtility.OpenFilePanel("Pilih PDF", "", "pdf");
        if (!string.IsNullOrEmpty(path))
        {
            TryLoadPdf(path);
        }
#else
        UpdateInfo("File picker runtime belum diset. Di Android/iOS gunakan plugin (SimpleFileBrowser/NativeFilePicker).");
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

    public void OnClickDownloadByKey()
    {
        if (string.IsNullOrWhiteSpace(s3Key))
        {
            Debug.LogWarning("S3 key kosong");
            return;
        }

        // 1) Minta presigned URL GET dari backend
        client.GetDownloadLink(
            s3Key,
            url => StartCoroutine(DownloadToFile(url, System.IO.Path.GetFileName(s3Key))),
            err => Debug.LogError("Presign gagal: " + err)
        );
    }

    private IEnumerator DownloadToFile(string url, string fallbackFileName)
    {
        // Tentukan nama file dari URL (tanpa query) agar rapi
        string finalName = fallbackFileName;
        try
        {
            var uri = new System.Uri(url);
            var fromPath = System.IO.Path.GetFileName(uri.AbsolutePath);
            if (!string.IsNullOrEmpty(fromPath)) finalName = fromPath;
        }
        catch { }

        string savePath = System.IO.Path.Combine(Application.persistentDataPath, finalName);

        using (var req = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            // 2) Download langsung ke file → hemat RAM
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(savePath, true);
            req.timeout = 60;

            yield return req.SendWebRequest();

            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Download gagal: {req.responseCode} {req.error}");
                // Jika 403 (expired), tinggal panggil lagi GetDownloadLink untuk URL baru lalu retry sekali.
                yield break;
            }

            // (Opsional) rename dari header Content-Disposition kalau backend set
            var cd = req.GetResponseHeader("Content-Disposition");
            if (!string.IsNullOrEmpty(cd))
            {
                var m = System.Text.RegularExpressions.Regex.Match(cd, "filename\\*=UTF-8''([^;]+)|filename=\"?([^\";]+)\"?");
                if (m.Success)
                {
                    var headerName = System.Uri.UnescapeDataString(
                        !string.IsNullOrEmpty(m.Groups[1].Value) ? m.Groups[1].Value : m.Groups[2].Value
                    );
                    var newPath = System.IO.Path.Combine(Application.persistentDataPath, headerName);
                    try
                    {
                        if (!savePath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
                        {
                            if (System.IO.File.Exists(newPath)) System.IO.File.Delete(newPath);
                            System.IO.File.Move(savePath, newPath);
                            savePath = newPath;
                        }
                    }
                    catch { }
                }
            }

            Debug.Log("File tersimpan di: " + savePath);

#if UNITY_ANDROID
            Application.OpenURL("file://" + savePath);
#else
        Application.OpenURL(savePath);
#endif
        }
    }
}
