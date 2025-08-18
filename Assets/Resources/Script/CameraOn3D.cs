using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class CameraOnCubeWebcam : MonoBehaviour
{
    [Header("Material Target (Cube)")]
    public Renderer targetRenderer; // kosong => pakai Renderer di GO ini

    [Header("Kompensasi Rotasi Manual (derajat)")]
    [Tooltip("Tambahan rotasi untuk texture hasil blit (0/90/180/270/-90). Biasanya 0.")]
    public int rotationOffset = 0;

    [Header("Mirror (opsional)")]
    public bool flipX = false;
    public bool flipY = false;

    [Header("Webcam")]
    [Tooltip("Biarkan kosong untuk otomatis pilih kamera belakang (Android).")]
    public string preferredDeviceName;
    public int requestedWidth = 1280;
    public int requestedHeight = 720;
    public int requestedFPS = 30;

    [Header("Capture → Meshy")]
    public MeshyImageTo3D_RuntimeOnly meshyRunner; // drag komponen Meshy ke sini

    // Runtime
    private WebCamTexture webcam;
    private Texture sourceTex;         // sumber untuk blit (webcam)
    private RenderTexture rt;          // hasil blit (yang ditempel ke Cube & dicapture)
    private Material blitMat;          // material blit (Hidden/RotateWebcamBlit)

    // Shader prop IDs
    private static readonly int PID_RotationDeg = Shader.PropertyToID("_RotationDeg");
    private static readonly int PID_FlipX = Shader.PropertyToID("_FlipX");
    private static readonly int PID_FlipY = Shader.PropertyToID("_FlipY");
    private static readonly int PID_MainTex = Shader.PropertyToID("_MainTex");

    void Awake()
    {
        if (!targetRenderer) targetRenderer = GetComponent<Renderer>();
    }

    void OnEnable()
    {
        if (!targetRenderer) targetRenderer = GetComponent<Renderer>();
        StartCoroutine(StartCameraRoutine());
    }

    IEnumerator StartCameraRoutine()
    {
        // Android/iOS: minta permission kamera
#if UNITY_ANDROID || UNITY_IOS
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        }
#endif
        // Tunggu daftar device siap
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
        // Otomatis pilih kamera belakang kalau ada (Android)
        if (string.IsNullOrEmpty(deviceName))
        {
            foreach (var d in devices)
            {
                if (!d.isFrontFacing) { deviceName = d.name; break; }
            }
        }
#endif
        // Fallback: ambil device pertama
        if (string.IsNullOrEmpty(deviceName)) deviceName = devices[0].name;

        webcam = new WebCamTexture(deviceName, requestedWidth, requestedHeight, requestedFPS);
        webcam.Play();

        // Tunggu sampai webcam punya ukuran frame
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
    }

    void OnDisable()
    {
        CleanupRT();
        if (webcam != null)
        {
            if (webcam.isPlaying) webcam.Stop();
            Destroy(webcam);
            webcam = null;
        }
    }

    void Update()
    {
        if (sourceTex == null || rt == null || blitMat == null) return;
        DoBlit();
    }

    void DoBlit()
    {
        // Rotasi dari kamera (mis. rotasi portrait di Android) + offset manual
        float camRot = 0f;
        bool camMirror = false;
        if (webcam != null)
        {
            camRot = webcam.videoRotationAngle; // 0/90/180/270
            camMirror = webcam.videoVerticallyMirrored; // true jika perlu flip
        }

        float rot = (rotationOffset + camRot) % 360f;
        blitMat.SetFloat(PID_RotationDeg, rot);
        blitMat.SetFloat(PID_FlipX, (flipX ^ camMirror) ? 1f : 0f); // XOR agar mirror kamera ikut dibalik bila perlu
        blitMat.SetFloat(PID_FlipY, flipY ? 1f : 0f);

        Graphics.Blit(sourceTex, rt, blitMat);
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
            CleanupRT();
            rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false
            };
            rt.Create();
        }

        if (blitMat == null)
        {
            var shader = Shader.Find("Hidden/RotateWebcamBlit");
            if (shader == null)
            {
                Debug.LogError("Shader 'Hidden/RotateWebcamBlit' tidak ditemukan. Tambahkan shader di bawah ke project (File 2).");
                return;
            }
            blitMat = new Material(shader);
        }

        if (!targetRenderer) targetRenderer = GetComponent<Renderer>();
        var mat = targetRenderer.material;
        bool applied = false;

        if (mat.HasProperty("_BaseMap")) { mat.SetTexture("_BaseMap", rt); applied = true; }
        if (mat.HasProperty("_MainTex")) { mat.SetTexture("_MainTex", rt); applied = true; }

        if (!applied)
        {
            var fallback = new Material(Shader.Find("Unlit/Texture"));
            fallback.SetTexture(PID_MainTex, rt);
            targetRenderer.material = fallback;
            Debug.LogWarning("Material target tidak punya slot tekstur; memakai Unlit/Texture.");
        }
    }

    void CleanupRT()
    {
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
        if (sourceTex == null || rt == null)
        {
            Debug.LogError("Webcam/RenderTexture belum siap.");
            return;
        }
        StartCoroutine(CaptureAndRunRoutine());
    }

    IEnumerator CaptureAndRunRoutine()
    {
        // Tunggu end-of-frame supaya RT sudah terisi blit terbaru
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

        meshyRunner.imageUrl = dataUri;
        Debug.Log("[CameraOnCubeWebcam] Capture OK, kirim ke Meshy…");
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