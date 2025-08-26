using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using static UnityEditor.Profiling.HierarchyFrameDataView;
using Unity.VisualScripting;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;




#if UNITY_EDITOR
using UnityEditor;
#endif

public class PdfUploadUI : MonoBehaviour
{
    [Header("Refs")]
    public PdfPresignClient client;
    public Button btnBukaFile;
    public Button btnUpload;
    public Text infoText; // boleh ganti TextMeshProUGUI jika pakai TMP
    [Header("Logic")]
    [SerializeField] private AnswerVM ViewModel;

    [Header("Limit")]
    public int maxSizeMB = 5;

    private string selectedPath;
    private byte[] selectedBytes;
    private string selectedName;

    void Awake()
    {
        btnBukaFile.onClick.AddListener(OnPickFile);
        btnUpload.onClick.AddListener(OnUpload);
        UpdateInfo("Belum ada file dipilih");
        btnUpload.interactable = false;
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
                    submission_id = s,
                    question_id = "Q004",
                    answer_text = s3key
                });

                StartCoroutine(ViewModel.PostAnswer(jsonBody, onJson: res =>
                {
                    Debug.Log("Presigned URL: " + presignUrl);
                    btnUpload.interactable = true;
                    SceneManager.LoadScene("Home");


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
}
