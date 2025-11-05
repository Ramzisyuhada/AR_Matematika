using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class PdfPresignClient : MonoBehaviour
{
    [Header("Base API (tanpa /presign/...)")]
    public string backendBase = "http://127.0.0.1:8000/api";

    [Header("Limit ukuran PDF (MB) client-side")]
    public int maxPdfSizeMB = 5;

    // ================== PUBLIC API ==================

    public void UploadPdfFromPath(string absolutePath, Action<string> onOk, Action<string> onErr)
    {
        try
        {
            if (!File.Exists(absolutePath)) { onErr?.Invoke("File tidak ditemukan"); return; }
            var bytes = File.ReadAllBytes(absolutePath);
            var name = Path.GetFileName(absolutePath);
            if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) name += ".pdf";
            if (!ValidatePdf(bytes, name, out var reason)) { onErr?.Invoke(reason); return; }
            UploadPdfBytes(bytes, name, onOk, onErr);
        }
        catch (Exception ex) { onErr?.Invoke("Gagal baca file: " + ex.Message); }
    }

    public void UploadPdfBytes(byte[] pdfBytes, string fileName, Action<string> onOk, Action<string> onErr)
    {
        if (pdfBytes == null || pdfBytes.Length == 0) { onErr?.Invoke("Bytes kosong"); return; }
        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) fileName += ".pdf";

        long max = (long)maxPdfSizeMB * 1024L * 1024L;
        if (pdfBytes.Length > max) { onErr?.Invoke($"Ukuran > {maxPdfSizeMB} MB"); return; }

        var url = JoinUrl(backendBase, "presign/upload");
        var body = "{\"key\":\"" + JsonEscape(fileName) + "\",\"contentType\":\"application/pdf\"}";
        Debug.Log($"[POST] {url} body={body}");

        StartCoroutine(PostJson(url, body, json =>
        {
            // expect: {"url":"...","key":"uploads/pdf/xxx.pdf"}
            var presignUrl = GetJsonString(json, "url");
            var presignKey = GetJsonString(json, "key");
            if (string.IsNullOrEmpty(presignUrl) || string.IsNullOrEmpty(presignKey))
            {
                onErr?.Invoke("Respon presign tidak valid: " + json);
                return;
            }

            StartCoroutine(PutToS3(presignUrl, pdfBytes,
                () => onOk?.Invoke(presignKey),
                e => onErr?.Invoke("Upload failed: " + e)));
        },
        e => onErr?.Invoke("Presign failed: " + e)));
    }

    public void DownloadPdfBytes(string s3Key, Action<byte[]> onOk, Action<string> onErr)
    {
        if (string.IsNullOrWhiteSpace(s3Key)) { onErr?.Invoke("key kosong"); return; }
        if (!s3Key.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) { onErr?.Invoke("Key harus .pdf"); return; }

        var url = JoinUrl(backendBase, "presign/download");
        var body = "{\"key\":\"" + JsonEscape(s3Key) + "\"}";
        Debug.Log($"[POST] {url} body={body}");

        StartCoroutine(PostJson(url, body, json =>
        {
            var presignUrl = GetJsonString(json, "url");
            if (string.IsNullOrEmpty(presignUrl)) { onErr?.Invoke("Respon presign tidak valid: " + json); return; }
            StartCoroutine(GetFromS3(presignUrl, onOk, onErr));
        },
        e => onErr?.Invoke("Presign failed: " + e)));
    }

    public void DownloadPdfToFile(string s3Key, string saveFileName, Action<string> onOk, Action<string> onErr)
    {
        DownloadPdfBytes(s3Key, bytes =>
        {
            try
            {
                var safe = saveFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? saveFileName : (saveFileName + ".pdf");
                var full = Path.Combine(Application.persistentDataPath, safe);
                File.WriteAllBytes(full, bytes);
                Debug.Log("[SAVE] " + full);
                onOk?.Invoke(full);
            }
            catch (Exception ex) { onErr?.Invoke("Gagal simpan file: " + ex.Message); }
        }, onErr);
    }

    public void OpenDownloadedPdf(string s3Key, string saveFileName, Action<string> onErr = null)
    {
        DownloadPdfToFile(s3Key, saveFileName, path =>
        {
            Debug.Log("[OPEN] " + path);
#if UNITY_ANDROID
            Application.OpenURL("file://" + path);
#else
            Application.OpenURL(path);
#endif
        }, onErr);
    }

    // ================== HTTP HELPERS ==================

    IEnumerator PostJson(string url, string json, Action<string> onOk, Action<string> onErr)
    {
        using (var req = new UnityWebRequest(url, "POST"))
        {
            var data = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(data);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 15;

            yield return req.SendWebRequest();

            int code = (int)req.responseCode;
            string text = req.downloadHandler != null ? req.downloadHandler.text : "";
            Debug.Log($"[RESP] {url} code={code} result={req.result} text={text}");

            if (req.result == UnityWebRequest.Result.Success) onOk?.Invoke(text);
            else onErr?.Invoke($"{code} {req.error} {text}");
        }
    }

    IEnumerator PutToS3(string presignedUrl, byte[] data, Action onOk, Action<string> onErr)
    {
        var put = new UnityWebRequest(presignedUrl, UnityWebRequest.kHttpVerbPUT);
        put.uploadHandler = new UploadHandlerRaw(data);
        put.downloadHandler = new DownloadHandlerBuffer();

        // WAJIB match dgn yang DISIGN di Laravel:
        put.SetRequestHeader("Content-Type", "application/pdf");
        put.SetRequestHeader("x-goog-content-sha256", "UNSIGNED-PAYLOAD");

        // Penting utk GCS V4: harus ada Content-Length (tidak chunked)
        put.chunkedTransfer = false;

        // Jangan set "Expect" manual (Unity sudah ngatur; warning kamu benar)
        yield return put.SendWebRequest();

        var code = (int)put.responseCode;
        var body = put.downloadHandler != null ? put.downloadHandler.text : "";
        Debug.Log($"[PUT RESP] code={code} result={put.result} body={body}");

        if (put.result == UnityWebRequest.Result.Success) onOk?.Invoke();
        else onErr?.Invoke($"{code} {put.error} {body}");
    }





    IEnumerator GetFromS3(string url, Action<byte[]> onOk, Action<string> onErr)
    {
        using (var get = UnityWebRequest.Get(url))
        {
            get.timeout = 30;
            yield return get.SendWebRequest();
            Debug.Log($"[GET RESP] code={(int)get.responseCode} result={get.result} bytes={get.downloadedBytes}");
            if (get.result == UnityWebRequest.Result.Success) onOk?.Invoke(get.downloadHandler.data);
            else onErr?.Invoke($"{get.responseCode} {get.error}");
        }
    }

    // ================== UTILS ==================

    string JoinUrl(string baseUrl, string path)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = "";
        if (baseUrl.EndsWith("/")) baseUrl = baseUrl.TrimEnd('/');
        if (string.IsNullOrEmpty(path)) path = "";
        if (path.StartsWith("/")) path = path.Substring(1);
        return $"{baseUrl}/{path}";
    }

    // JSON mini-escape untuk string
    string JsonEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    // Ambil nilai string dari JSON sederhana: "field":"value"
    string GetJsonString(string json, string field)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(field)) return null;
        // match "field":"..."; tangkap isi di dalam kutip (support \/")
        var m = Regex.Match(json, $"\"{Regex.Escape(field)}\"\\s*:\\s*\"(.*?)\"");
        if (!m.Success) return null;
        var val = m.Groups[1].Value;
        // unescape paling umum
        val = val.Replace("\\/", "/").Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\\", "\\");
        return val;
    }

    bool ValidatePdf(byte[] bytes, string name, out string reason)
    {
        if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) { reason = "Hanya .pdf"; return false; }
        if (bytes == null || bytes.Length < 5) { reason = "File kosong/kecil"; return false; }
        long max = (long)maxPdfSizeMB * 1024L * 1024L;
        if (bytes.Length > max) { reason = $"Ukuran > {maxPdfSizeMB} MB"; return false; }
        // header %PDF-
        if (!(bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D))
        { reason = "Header %PDF- tidak valid"; return false; }
        reason = null; return true;
    }

    // ================== DEBUG ==================

    [ContextMenu("DEBUG: Hit Hardcoded URL")]
    public void DebugHitHardcoded()
    {
        var url = "http://127.0.0.1:8000/api/presign/upload";
        var body = "{\"key\":\"test.pdf\",\"contentType\":\"application/pdf\"}";
        StartCoroutine(PostJson(url, body,
            json => Debug.Log("[HARDCODED OK] " + json),
            err => Debug.LogError("[HARDCODED ERR] " + err)));
    }

    void Start()
    {
        Debug.Log("[PdfPresignClient] backendBase=" + backendBase + " platform=" + Application.platform);
    }

    public void GetDownloadLink(string s3Key, Action<string> onOk, Action<string> onErr)
    {
        if (string.IsNullOrWhiteSpace(s3Key)) { onErr?.Invoke("key kosong"); return; }
        if (!s3Key.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) { onErr?.Invoke("Key harus .pdf"); return; }

        var url = JoinUrl(backendBase, "presign/download");
        var body = "{\"key\":\"" + JsonEscape(s3Key) + "\"}";
        Debug.Log($"[POST] {url} body={body}");

        StartCoroutine(PostJson(url, body, json =>
        {
            var presignUrl = GetJsonString(json, "url");
            if (string.IsNullOrEmpty(presignUrl))
            {
                onErr?.Invoke("Respon presign tidak valid: " + json);
                return;
            }
            onOk?.Invoke(presignUrl);
        },
        e => onErr?.Invoke("Presign failed: " + e)));
    }

}
