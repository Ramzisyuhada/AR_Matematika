using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class APIManager : MonoBehaviour
{
    [Header("API")]
    [SerializeField] private string apiUrl = "https://107-23-209-11.nip.io/api/login";

    [Header("UI References")]
    [SerializeField] private TMP_InputField userIdentifierInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button loginButton;

    [Header("Extras (Opsional)")]
    [SerializeField] private Toggle showPasswordToggle;
    [SerializeField] private Image userFieldBorder;
    [SerializeField] private Image passFieldBorder;
    [SerializeField] private Color errorColor = new Color(1f, 0.5f, 0.5f);
    [SerializeField] private Color normalColor = Color.white;

    [Header("Debug & Keamanan")]
    [SerializeField] private bool verboseLogs = false;            // matikan di produksi
    [SerializeField] private bool allowInsecureHttpsDev = false;  // hanya DEV! (self-signed cert)

    [Header("Disconnected UI")]
    [SerializeField] private GameObject disconnectedPanel;         // drag panel "Kamu sedang terputus"
    [SerializeField] private bool autoWatchReachability = true;    // pantau internet device

    private void Awake()
    {
        SetPasswordMasked(true);
        if (showPasswordToggle != null)
            showPasswordToggle.onValueChanged.AddListener(OnToggleShowPasswordChanged);

        SetDisconnected(false);

        if (autoWatchReachability) StartCoroutine(ReachabilityWatcher());
    }

    public void OnLoginButtonClicked()
    {
        string userIdentifier = userIdentifierInput ? userIdentifierInput.text.Trim() : "";
        string password = passwordInput ? passwordInput.text.Trim() : "";

        // reset warna border
        SetFieldNormal(userFieldBorder, userIdentifierInput);
        SetFieldNormal(passFieldBorder, passwordInput);

        // validasi
        if (string.IsNullOrEmpty(userIdentifier))
        {
            SetStatus("Username tidak boleh kosong.");
            SetFieldError(userFieldBorder, userIdentifierInput);
            userIdentifierInput?.Select();
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            SetStatus("Password tidak boleh kosong.");
            SetFieldError(passFieldBorder, passwordInput);
            passwordInput?.Select();
            return;
        }

        StartCoroutine(Login(userIdentifier, password));
    }

    private IEnumerator Login(string userIdentifier, string password)
    {
        SetInteractable(false);
        SetStatus("Memproses...");

        // jelas tidak ada internet
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            SetStatus("Tidak ada koneksi internet.");
            SetDisconnected(true);
            SetInteractable(true);
            yield break;
        }

        // Body JSON
        string jsonData = JsonUtility.ToJson(new LoginRequest(userIdentifier, password));
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            // cegah auto-redirect POST → GET
            request.redirectLimit = 0;

            // timeout
            request.timeout = 15;

            // DEV only: terima semua sertifikat
            CertificateHandler cert = null;
            if (allowInsecureHttpsDev)
            {
                cert = new AcceptAllCerts();
                request.certificateHandler = cert;
            }

            yield return request.SendWebRequest();

            if (cert != null) cert.Dispose();

            // ===== ERROR: koneksi putus / DNS / timeout
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                LogErr($"[LOGIN] ConnectionError: {request.error}");
                SetStatus(MapConnectionError(request.error));
                SetDisconnected(true);
                SetInteractable(true);
                yield break;
            }

            // ===== ERROR: parsing
            if (request.result == UnityWebRequest.Result.DataProcessingError)
            {
                LogErr($"[LOGIN] DataProcessingError: {request.error}");
                SetStatus("Terjadi kesalahan pemrosesan data.");
                SetInteractable(true);
                yield break;
            }

            // ===== ERROR: HTTP non-2xx
            if (request.result == UnityWebRequest.Result.ProtocolError)
            {
                long st = request.responseCode;
                string b = request.downloadHandler?.text ?? "";
                LogWarn($"[LOGIN] ProtocolError HTTP {st} | {b}");

                string serverMsg = ExtractServerMessage(b);

                if (st >= 300 && st < 400)
                {
                    SetStatus(serverMsg ?? $"Server redirect (HTTP {st}). Pastikan endpoint API benar.");
                }
                else if (st == 401)
                {
                    SetStatus(serverMsg ?? "User atau password salah.");
                    SetFieldError(passFieldBorder, passwordInput);
                }
                else if (st == 422)
                {
                    // AUTO RETRY SEKALI pakai form-encoded
                    SetStatus(serverMsg ?? "Input tidak valid (422). Mencoba ulang...");
                    yield return StartCoroutine(LoginFormUrlencoded(userIdentifier, password));
                }
                else if (st == 403)
                {
                    SetStatus(serverMsg ?? "Akses ditolak (403).");
                }
                else if (st == 404)
                {
                    SetStatus(serverMsg ?? "Endpoint tidak ditemukan (404). Periksa URL API.");
                }
                else if (st >= 500)
                {
                    SetStatus(serverMsg ?? "Server sedang bermasalah (5xx).");
                }
                else
                {
                    SetStatus(serverMsg ?? $"Login gagal (HTTP {st}).");
                }

                SetInteractable(true);
                yield break;
            }

            // ===== SUKSES (2xx)
            long code = request.responseCode;
            string body = request.downloadHandler.text ?? "";
            if (verboseLogs) Debug.Log($"[LOGIN OK] HTTP {code} | {body}");

            SetDisconnected(false);

            LoginResponse response = null;
            try { response = JsonUtility.FromJson<LoginResponse>(body); } catch { }

            if (response == null || response.user == null)
            {
                SetStatus("Login berhasil, tetapi format respons tidak sesuai ekspektasi.");
                if (verboseLogs) Debug.LogWarning($"[LOGIN] Unexpected JSON: {body}");
                SetInteractable(true);
                yield break;
            }

            // simpan
            PlayerPrefs.SetString("user_identifier", response.user.user_identifier ?? "");
            PlayerPrefs.SetString("name", response.user.name ?? "");
            PlayerPrefs.SetString("role", response.user.role ?? "");
            PlayerPrefs.Save();

            SetStatus("Login berhasil!");

            yield return StartCoroutine(GoToSceneByRole(response.user.role));
        }
    }

    // === Retry sekali via form-urlencoded saat 422 JSON ===
    private IEnumerator LoginFormUrlencoded(string userIdentifier, string password)
    {
        SetInteractable(false);

        WWWForm form = new WWWForm();
        form.AddField("user_identifier", userIdentifier);
        form.AddField("password", password);

        using (UnityWebRequest req = UnityWebRequest.Post(apiUrl, form))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Accept", "application/json");
            req.redirectLimit = 0;
            req.timeout = 15;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.ConnectionError)
            {
                LogErr($"[RETRY] ConnectionError: {req.error}");
                SetStatus(MapConnectionError(req.error));
                SetDisconnected(true);
                yield break;
            }
            if (req.result == UnityWebRequest.Result.DataProcessingError)
            {
                LogErr($"[RETRY] DataProcessingError: {req.error}");
                SetStatus("Kesalahan pemrosesan data (retry).");
                yield break;
            }
            if (req.result == UnityWebRequest.Result.ProtocolError)
            {
                long st = req.responseCode;
                string b = req.downloadHandler?.text ?? "";
                string serverMsg = ExtractServerMessage(b);
                SetStatus(serverMsg ?? $"Gagal (retry) HTTP {st}.");
                yield break;
            }

            // sukses
            string body = req.downloadHandler.text ?? "";
            if (verboseLogs) Debug.Log($"[RETRY OK] {body}");

            LoginResponse resp = null;
            try { resp = JsonUtility.FromJson<LoginResponse>(body); } catch { }

            if (resp == null || resp.user == null)
            {
                SetStatus("Login berhasil (retry), tapi format respons tidak sesuai.");
                yield break;
            }

            PlayerPrefs.SetString("user_identifier", resp.user.user_identifier ?? "");
            PlayerPrefs.SetString("name", resp.user.name ?? "");
            PlayerPrefs.SetString("role", resp.user.role ?? "");
            PlayerPrefs.Save();

            SetDisconnected(false);
            SetStatus("Login berhasil!");

            yield return StartCoroutine(GoToSceneByRole(resp.user.role));
        }
    }

    private IEnumerator GoToSceneByRole(string roleRaw)
    {
        string role = (roleRaw ?? "").ToLowerInvariant();
        string targetScene = role == "guru" ? "Guru" :
                             role == "siswa" ? "Home" : null;

        if (string.IsNullOrEmpty(targetScene))
        {
            LogWarn($"Role tidak dikenali: {role}");
            SetStatus("Role tidak dikenali. Hubungi admin.");
            SetInteractable(true);
            yield break;
        }

        if (!IsSceneInBuild(targetScene))
        {
            LogErr($"Scene '{targetScene}' belum ada di Build Settings.");
            SetStatus($"Scene '{targetScene}' belum terdaftar di Build Settings.");
            SetInteractable(true);
            yield break;
        }

        SceneManager.LoadScene(targetScene);
        yield return null;
    }

    // ===== Helpers =====

    private void OnToggleShowPasswordChanged(bool isOn) => SetPasswordMasked(!isOn);

    private void SetPasswordMasked(bool masked)
    {
        if (passwordInput == null) return;
        passwordInput.contentType = masked ? TMP_InputField.ContentType.Password
                                           : TMP_InputField.ContentType.Standard;
        passwordInput.ForceLabelUpdate();
    }

    private void SetStatus(string message)
    {
        if (statusText) statusText.text = message;
        if (verboseLogs) Debug.Log($"[STATUS] {message}");
    }

    private void SetInteractable(bool interactable)
    {
        if (loginButton) loginButton.interactable = interactable;
        if (userIdentifierInput) userIdentifierInput.interactable = interactable;
        if (passwordInput) passwordInput.interactable = interactable;
    }

    private void SetFieldError(Image border, Selectable inputForFallback)
    {
        if (border != null) { border.color = errorColor; return; }
        if (inputForFallback && inputForFallback.targetGraphic)
            inputForFallback.targetGraphic.color = errorColor;
    }

    private void SetFieldNormal(Image border, Selectable inputForFallback)
    {
        if (border != null) { border.color = normalColor; }
        if (inputForFallback && inputForFallback.targetGraphic)
            inputForFallback.targetGraphic.color = normalColor;
    }

    private void LogWarn(string msg) { if (verboseLogs) Debug.LogWarning(msg); }
    private void LogErr(string msg) { if (verboseLogs) Debug.LogError(msg); }

    private bool IsSceneInBuild(string sceneName)
    {
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName) return true;
        }
        return false;
    }

    // === Disconnected handling ===
    private void SetDisconnected(bool on)
    {
        if (disconnectedPanel && disconnectedPanel.activeSelf != on)
            disconnectedPanel.SetActive(on);
    }

    private string MapConnectionError(string err)
    {
        if (string.IsNullOrEmpty(err)) return "Gagal koneksi ke server.";
        string e = err.ToLowerInvariant();
        if (e.Contains("timed out") || e.Contains("timeout"))
            return "Permintaan habis waktu (timeout). Coba lagi.";
        if (e.Contains("resolve host") || e.Contains("dns"))
            return "Gagal resolve host. Periksa nama domain.";
        if (Application.internetReachability == NetworkReachability.NotReachable)
            return "Tidak ada koneksi internet.";
        return "Tidak dapat terhubung ke server. Cek internet/API URL.";
    }

    // Pantau internet device → set panel sesuai kondisi
    private IEnumerator ReachabilityWatcher()
    {
        while (true)
        {
            bool offline = (Application.internetReachability == NetworkReachability.NotReachable);
            SetDisconnected(offline);
            yield return new WaitForSeconds(1.5f);
        }
    }

    // ===== JSON Models =====
    [System.Serializable]
    private class LoginRequest
    {
        public string user_identifier;
        public string password;
        public LoginRequest(string user_identifier, string password)
        {
            this.user_identifier = user_identifier;
            this.password = password;
        }
    }

    [System.Serializable]
    private class LoginResponse
    {
        public string message;
        public UserData user;
        // public string token;
    }

    [System.Serializable]
    private class UserData
    {
        public string user_identifier;
        public string name;
        public string role;
    }

    [System.Serializable]
    private class MessageOnly { public string message; }

    private string ExtractServerMessage(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var msgOnly = JsonUtility.FromJson<MessageOnly>(json);
            if (msgOnly != null && !string.IsNullOrEmpty(msgOnly.message))
                return msgOnly.message;
        }
        catch { }
        return null;
    }

    // DEV ONLY: terima semua sertifikat (danger!)
    private class AcceptAllCerts : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData) => true;
    }
}
