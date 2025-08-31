using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class RawImageWebcam : MonoBehaviour
{
    [Header("UI Target")]
    public RawImage targetRawImage;           // drag RawImage di Canvas
    public AspectRatioFitter aspectFitter;    // (opsional) biar rasio gambar benar

    [Header("Config")]
    public int requestedWidth = 1280;
    public int requestedHeight = 720;
    public int requestedFPS = 30;
    [Tooltip("Pilih kamera depan kalau true, belakang kalau false")]
    public bool useFrontCamera = false;

    private WebCamTexture webcam;
    private WebCamDevice? selectedDevice;
    private bool isPlaying;

    private void OnEnable()
    {
        if (!targetRawImage)
        {
            Debug.LogError("[RawImageWebcam] targetRawImage belum di-assign.");
            enabled = false;
            return;
        }
        StartCoroutine(StartCameraFlow());
    }

    private void OnDisable()
    {
        StopCamera();
    }

    IEnumerator StartCameraFlow()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Minta izin kamera (Android)
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
            // Tunggu 1–2 frame; di beberapa device, izin muncul pop-up
            yield return null; 
        }
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        {
            Debug.LogError("[RawImageWebcam] Izin kamera ditolak.");
            yield break;
        }
#endif

        // Tunggu daftar kamera tersedia
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Debug.LogError("[RawImageWebcam] Tidak ada otorisasi WebCam.");
            yield break;
        }

        // Pilih device
        var devices = WebCamTexture.devices;
        if (devices == null || devices.Length == 0)
        {
            Debug.LogError("[RawImageWebcam] Tidak ada kamera terdeteksi.");
            yield break;
        }

        WebCamDevice chosen = devices[0];
        if (useFrontCamera)
        {
            // Cari yang front-facing
            foreach (var d in devices)
                if (d.isFrontFacing) { chosen = d; break; }
        }
        else
        {
            // Cari yang bukan front-facing (kamera belakang)
            foreach (var d in devices)
                if (!d.isFrontFacing) { chosen = d; break; }
        }
        selectedDevice = chosen;

        // Mulai webcam
        webcam = new WebCamTexture(chosen.name, requestedWidth, requestedHeight, requestedFPS);
        targetRawImage.texture = webcam;
        webcam.Play();
        isPlaying = true;

        // Tunggu sampai ada ukuran tex yang valid
        float t = 0f;
        while (webcam.width <= 16 && t < 3f)
        {
            t += Time.deltaTime;
            yield return null;
        }

        // Set rasio (opsional)
        if (aspectFitter)
        {
            float w = webcam.width <= 0 ? requestedWidth : webcam.width;
            float h = webcam.height <= 0 ? requestedHeight : webcam.height;
            aspectFitter.aspectRatio = w / h;
        }

        // Perbaiki rotasi & mirror tiap frame
        StartCoroutine(ApplyOrientationLoop());
        Debug.Log($"[RawImageWebcam] Start {chosen.name} ({webcam.width}x{webcam.height})");
    }

    IEnumerator ApplyOrientationLoop()
    {
        var rectTransform = targetRawImage.rectTransform;
        while (isPlaying && webcam != null)
        {
            // Rotasi
            rectTransform.localEulerAngles = new Vector3(0, 0, -webcam.videoRotationAngle);

            // Mirror vertikal/horizontal sesuai device
            // Di banyak device: kamera depan sering mirrored horizontal, belakang biasanya tidak.
            bool mirror = webcam.videoVerticallyMirrored;
            Vector3 scale = rectTransform.localScale;
            scale.y = mirror ? -1 : 1;

            // Tambahan: kalau mau paksa mirror kamera depan
            if (selectedDevice.HasValue && selectedDevice.Value.isFrontFacing)
                scale.x = -1; // supaya tampak seperti selfie (optional)
            else
                scale.x = 1;

            rectTransform.localScale = scale;

            yield return null;
        }
    }

    public void StopCamera()
    {
        isPlaying = false;
        if (webcam != null)
        {
            if (webcam.isPlaying) webcam.Stop();
            Destroy(webcam);
            webcam = null;
        }
        if (targetRawImage) targetRawImage.texture = null;
    }
}
