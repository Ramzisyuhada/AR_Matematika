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
    [SerializeField] private Toggle showPasswordToggle; // centang untuk show/hide password
    [SerializeField] private Image userFieldBorder;      // drag Image border field (opsional)
    [SerializeField] private Image passFieldBorder;      // drag Image border field (opsional)
    [SerializeField] private Color errorColor = new Color(1f, 0.5f, 0.5f);
    [SerializeField] private Color normalColor = Color.white;

    private void Awake()
    {
        // Pastikan password dimask (bintang) saat start
        SetPasswordMasked(true);

        if (showPasswordToggle != null)
            showPasswordToggle.onValueChanged.AddListener(OnToggleShowPasswordChanged);
    }

    public void OnLoginButtonClicked()
    {
        // Trim input
        string userIdentifier = userIdentifierInput ? userIdentifierInput.text.Trim() : "";
        string password = passwordInput ? passwordInput.text.Trim() : "";

        // Reset warna border
        SetFieldNormal(userFieldBorder);
        SetFieldNormal(passFieldBorder);

        // Validasi
        if (string.IsNullOrEmpty(userIdentifier))
        {
            SetStatus("Username tidak boleh kosong.");
            SetFieldError(userFieldBorder);
            userIdentifierInput?.Select();
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            SetStatus("Password tidak boleh kosong.");
            SetFieldError(passFieldBorder);
            passwordInput?.Select();
            return;
        }

        // Mulai login
        StartCoroutine(Login(userIdentifier, password));
    }

    private IEnumerator Login(string userIdentifier, string password)
    {
        SetInteractable(false);
        SetStatus("Memproses...");

        // Body JSON
        string jsonData = JsonUtility.ToJson(new LoginRequest(userIdentifier, password));
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            // Error koneksi/data
            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
            {
                Debug.LogError($"[LOGIN ERR] {request.result} | {request.error}");
                SetStatus("Gagal koneksi ke server.");
                SetInteractable(true);
                yield break;
            }

            // Cek kode HTTP
            long code = request.responseCode;
            string body = request.downloadHandler.text;
            Debug.Log($"[LOGIN RESP] HTTP {code} | {body}");

            if (code < 200 || code >= 300)
            {
                // Laravel kamu balikin 401 kalau salah kredensial
                if (code == 401)
                {
                    SetStatus("User atau password salah.");
                    SetFieldError(passFieldBorder);
                }
                else
                {
                    SetStatus($"Login gagal (HTTP {code}).");
                }
                SetInteractable(true);
                yield break;
            }

            // Parse respons
            LoginResponse response = null;
            try
            {
                response = JsonUtility.FromJson<LoginResponse>(body);
            }
            catch
            {
                // Format tak sesuai model
            }

            if (response == null || response.user == null)
            {
                SetStatus("Login berhasil, tetapi format respons tidak sesuai.");
                SetInteractable(true);
                yield break;
            }

            // Simpan info user
            PlayerPrefs.SetString("user_identifier", response.user.user_identifier ?? "");
            PlayerPrefs.SetString("name", response.user.name ?? "");
            PlayerPrefs.SetString("role", response.user.role ?? "");
            PlayerPrefs.Save();

            SetStatus("Login berhasil!");

            // Pindah scene berdasarkan role
            string role = (response.user.role ?? "").ToLower();
            if (role == "guru")
            {
                SceneManager.LoadScene("Guru");
            }
            else if (role == "siswa")
            {
                SceneManager.LoadScene("Home");
            }
            else
            {
                Debug.Log($"Role tidak dikenali: {role}");
                // Tetap enable UI supaya user bisa coba lagi
                SetInteractable(true);
            }
        }
    }

    // ===== Helpers =====

    private void OnToggleShowPasswordChanged(bool isOn)
    {
        // isOn = true → tampilkan password; false → mask
        SetPasswordMasked(!isOn);
    }

    private void SetPasswordMasked(bool masked)
    {
        if (passwordInput == null) return;

        passwordInput.contentType = masked
            ? TMP_InputField.ContentType.Password
            : TMP_InputField.ContentType.Standard;

        // Agar tampilan langsung update
        passwordInput.ForceLabelUpdate();
    }

    private void SetStatus(string message)
    {
        if (statusText) statusText.text = message;
    }

    private void SetInteractable(bool interactable)
    {
        if (loginButton) loginButton.interactable = interactable;
        if (userIdentifierInput) userIdentifierInput.interactable = interactable;
        if (passwordInput) passwordInput.interactable = interactable;
    }

    private void SetFieldError(Image img)
    {
        if (img != null) img.color = errorColor;

        // Kalau tidak pakai Image border, coba pakai targetGraphic bawaan input
        // (opsional fallback)
        if (img == null && passwordInput != null && passwordInput.targetGraphic != null)
            passwordInput.targetGraphic.color = errorColor;
    }

    private void SetFieldNormal(Image img)
    {
        if (img != null) img.color = normalColor;

        if (passwordInput != null && passwordInput.targetGraphic != null)
            passwordInput.targetGraphic.color = normalColor;
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
    }

    [System.Serializable]
    private class UserData
    {
        public string user_identifier;
        public string name;
        public string role;
    }

}
