using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CameraOnUIWebcam : MonoBehaviour
{
    [Header("UI Target (RawImage)")]
    public RawImage targetRawImage;   // drag RawImage di Canvas ke sini

    [Header("Kompensasi Rotasi Manual (derajat)")]
    public int rotationOffset = 0;

    [Header("Mirror (opsional)")]
    public bool flipX = false;
    public bool flipY = false;

    [Header("Webcam")]
    [Tooltip("Kosongkan untuk auto pilih kamera belakang (Android)")]
    public string preferredDeviceName;
    public int requestedWidth = 1280;
    public int requestedHeight = 720;
    public int requestedFPS = 30;

    [Header("Capture → Meshy")]
    public MeshyImageTo3D_RuntimeOnlyV2 meshyRunner; // drag komponen Meshy ke sini
    public bool autoRunOnStart = false;               // kalau true, auto capture+run sekali saat start

    // Runtime
    private WebCamTexture webcam;
    private Texture sourceTex;
    private RenderTexture rt;
    private Material blitMat;

    // Shader prop IDs
    private static readonly int PID_RotationDeg = Shader.PropertyToID("_RotationDeg");
    private static readonly int PID_FlipX = Shader.PropertyToID("_FlipX");
    private static readonly int PID_FlipY = Shader.PropertyToID("_FlipY");
    private static readonly int PID_MainTex = Shader.PropertyToID("_MainTex");

    void OnEnable()
    {
        StartCoroutine(StartCameraRoutine());
    }

    IEnumerator StartCameraRoutine()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        }
#endif
        // Tunggu device list siap
        yield return new WaitForEndOfFrame();

        var devices = WebCamTexture.devices;
        if (devices == null || devices.Length == 0)
        {
            Debug.LogError("[Webcam] Tidak ada kamera terdeteksi.");
            yield break;
        }

        string deviceName = null;
        if (!string.IsNullOrEmpty(preferredDeviceName))
        {
            foreach (var d in devices)
            {
                if (d.name.Contains(preferredDeviceName)) { deviceName = d.name; break; }
            }
        }

#if UNITY_ANDROID
        // default pilih belakang
        if (string.IsNullOrEmpty(deviceName))
        {
            foreach (var d in devices)
            {
                if (!d.isFrontFacing) { deviceName = d.name; break; }
            }
        }
#endif
        if (string.IsNullOrEmpty(deviceName)) deviceName = devices[0].name;

        webcam = new WebCamTexture(deviceName, requestedWidth, requestedHeight, requestedFPS);
        webcam.Play();

        // Tunggu sampai ada resolusi valid
        float timeout = 5f;
        while (webcam.width <= 16 && webcam.height <= 16 && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        if (webcam.width <= 16 || webcam.height <= 16)
        {
            Debug.LogWarning("[Webcam] Resolusi belum siap, lanjut paksa.");
        }

        sourceTex = webcam;
        SetupBlitAndMaterial();

        if (autoRunOnStart)
        {
            // kasih 1 frame supaya RT terisi
            yield return new WaitForEndOfFrame();
            CaptureAndRun();
        }
    }

    void Update()
    {
        if (sourceTex == null || rt == null || blitMat == null) return;
        DoBlit();
    }

    void DoBlit()
    {
        float camRot = 0f;
        bool camMirror = false;
        if (webcam != null)
        {
            camRot = webcam.videoRotationAngle;        // 0/90/180/270
            camMirror = webcam.videoVerticallyMirrored;
        }

        float rot = Mathf.Repeat(rotationOffset - camRot, 360f);

        blitMat.SetFloat(PID_RotationDeg, rot);
        blitMat.SetFloat(PID_FlipX, (flipX ^ camMirror) ? 1f : 0f); // XOR dgn mirror kamera
        blitMat.SetFloat(PID_FlipY, flipY ? 1f : 0f);

        Graphics.Blit(sourceTex, rt, blitMat);

        if (targetRawImage) targetRawImage.texture = rt;
    }

    void SetupBlitAndMaterial()
    {
        if (sourceTex == null)
        {
            Debug.LogWarning("[Webcam] setup batal: sourceTex null.");
            return;
        }

        int w = Mathf.Max(64, sourceTex.width);
        int h = Mathf.Max(64, sourceTex.height);

        if (rt == null || rt.width != w || rt.height != h)
        {
            if (rt != null)
            {
                if (rt.IsCreated()) rt.Release();
                Destroy(rt);
            }
            rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false
            };
            rt.Create();
        }

        var shader = Shader.Find("Hidden/RotateWebcamBlit");
        if (shader == null)
        {
            Debug.LogError("Shader 'Hidden/RotateWebcamBlit' tidak ditemukan. Pastikan shader ini ada di project.");
            return;
        }
        if (blitMat != null) Destroy(blitMat);
        blitMat = new Material(shader);

        if (targetRawImage) targetRawImage.texture = rt;
    }

    void OnDisable()
    {
        if (webcam != null)
        {
            if (webcam.isPlaying) webcam.Stop();
            Destroy(webcam);
            webcam = null;
        }
        if (rt != null)
        {
            if (rt.IsCreated()) rt.Release();
            Destroy(rt);
            rt = null;
        }
        if (blitMat != null)
        {
            Destroy(blitMat);
            blitMat = null;
        }
    }

    // ================== CAPTURE → MESHY ==================
    [ContextMenu("Capture & Run Meshy (Webcam)")]
    public void CaptureAndRun()
    {
        if (meshyRunner == null)
        {
            Debug.LogError("meshyRunner belum di-assign di Inspector.");
            return;
        }
        if (rt == null)
        {
            Debug.LogError("RenderTexture belum siap.");
            return;
        }
        StartCoroutine(CaptureAndRunRoutine());
    }

    IEnumerator CaptureAndRunRoutine()
    {
        // pastikan frame terbaru sudah keblit
        yield return new WaitForEndOfFrame();

        Texture2D snap = GrabTexture2D(rt);
        if (snap == null)
        {
            Debug.LogError("Gagal capture dari RT.");
            yield break;
        }

        byte[] pngBytes = snap.EncodeToPNG();
        string base64 = Convert.ToBase64String(pngBytes);
        string dataUri = $"data:image/png;base64,{base64}";
        Destroy(snap);

        meshyRunner.imageUrl = dataUri;   // kirim sebagai Data URI
        Debug.Log("[CameraOnUIWebcam] Capture OK, kirim ke Meshy…");
        meshyRunner.Run();
    }

    Texture2D GrabTexture2D(RenderTexture source)
    {
        if (source == null || source.width <= 0 || source.height <= 0) return null;
        var prev = RenderTexture.active;
        RenderTexture.active = source;

        Texture2D tex = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        tex.Apply(false, false);

        RenderTexture.active = prev;
        return tex;
    }
}
