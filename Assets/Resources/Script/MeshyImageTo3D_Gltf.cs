using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.UI;           // UI Button
using GLTFast;

public class MeshyImageTo3D_RuntimeOnlyV2 : MonoBehaviour
{
    // ======================= QUALITY / STARTUP =======================
    private void Awake()
    {
        QualitySettings.asyncUploadTimeSlice = 8;
        QualitySettings.asyncUploadBufferSize = 32; // MB
        QualitySettings.asyncUploadPersistentBuffer = true;

        QualitySettings.streamingMipmapsActive = true;
        Texture2D.streamingTextureDiscardUnusedMips = true;

        QualitySettings.antiAliasing = 0;
        QualitySettings.shadows = ShadowQuality.Disable;
        Application.targetFrameRate = 30;

#if UNITY_ANDROID && !UNITY_EDITOR
        useSse = false; // SSE kurang stabil di Android
#endif
    }

    // ======================= ENDPOINT =======================
    private const string SubmitUrl = "https://api.meshy.ai/openapi/v1/image-to-3d";
    private static string BuildStatusUrl(string taskId) => $"https://api.meshy.ai/openapi/v1/image-to-3d/{taskId}";
    private static string BuildStreamUrl(string taskId) => $"https://api.meshy.ai/openapi/v1/image-to-3d/{taskId}/stream";

    // ======================= INSPECTOR: AUTH/INPUT =======================
    [Header("Auth")]
    public string apiKey = "YOUR_API_KEY";

    [Header("Input")]
    [Tooltip("URL publik atau data URI (data:image/jpeg;base64,...)")]
    public string imageUrl = "https://example.com/your_image.png";

    // ======================= MODE/OPTIONS =======================
    [Header("Preset: ColorFast (BERTEKSTUR) vs Fast (tanpa tekstur)")]
    public bool colorFastPreset = true; // ON = minta tekstur (sesuai foto)
    [Tooltip("Fast mode tanpa tekstur (akan otomatis kebalikan dari colorFastPreset)")]
    public bool fastMode = false;       // dikendalikan otomatis

    [Header("Options (non-fast mode manual)")]
    public bool enablePbr = true;
    public bool shouldRemesh = true;
    public bool shouldTexture = true;
    public string topology = "triangle";
    public int targetPolycount = 30000;

    // ======================= PROGRESS/TIMING =======================
    [Header("Progress & Timing")]
    public bool useSse = true;
    public float pollIntervalSeconds = 3f;
    public float timeoutSeconds = 600f;
    public int requestTimeoutSeconds = 30;
    public int sseTimeoutSeconds = 0; // 0 = infinite (disarankan untuk SSE)

    // ======================= AUTOSTART/SPAWN =======================
    [Header("Autostart")]
    public bool runOnStart = true;
    public float startDelay = 0f;

    [Header("Spawn Options")]
    public bool parentUnderThis = true;
    public Transform spawnParent; // container model
    public Vector3 spawnLocalPosition = Vector3.zero;
    public Vector3 spawnLocalRotationEuler = Vector3.zero;
    public Vector3 spawnLocalScale = Vector3.one;

    [Header("Buttons")]
    public Button btnHancurkan; // drag Button Hancurkan
    public Button btnPhoto;     // drag Button Photo
    public Button btnMode;      // drag tombol untuk toggle ColorFast / Fast

    // ======================= LOADING UI =======================
    [Header("Loading UI")]
    public GameObject loadingObject;
    public GameObject Camera;
    public bool autoHideLoading = true;
    public GameObject Rotate;

    // ======================= UI HOOKS & URL EVENTS =======================
    [Header("UI Hooks (opsional)")]
    [Range(0, 1f)] public float simulatedProgress;
    public UnityEvent<float> onProgress;
    public UnityEvent<string> onStatusText;

    [Header("URL Events")]
    public UnityEvent<string> onGlbUrlReceived;

    // ======================= LAST URLS (publik) =======================
    [Header("Last Result URLs")]
    public string lastGlbUrl;
    public string lastFbxUrl;
    public string lastObjUrl;
    public string lastUsdzUrl;

    // ======================= RUNTIME STATE =======================
    private string _taskId;
    private bool _cancelRequested;
    private Coroutine _running;

    // timing mark
    private float _tMark;

    // turunkan sementara resolusi tekstur saat loading
    [Header("Texture Load Throttle")]
    public bool lowerTextureResWhileLoading = true;

    // ======================= ALERT (GameObject ONLY) =======================
    [Header("Alert GameObjects (NO TEXT)")]
    public GameObject alertOn;   // muncul saat ColorFast ON
    public GameObject alertOff;  // muncul saat Fast ON

    public float alertAnimIn = 0.18f;
    public float alertHold = 0.8f;
    public float alertAnimOut = 0.18f;

    private void ShowAlertObject(GameObject obj)
    {
        if (obj == null) return;
        obj.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
        obj.SetActive(true);

        LeanTween.scale(obj, Vector3.one, alertAnimIn)
            .setEaseOutBack()
            .setOnComplete(() =>
            {
                LeanTween.delayedCall(alertHold, () =>
                {
                    LeanTween.scale(obj, new Vector3(0.25f, 0.25f, 1f), alertAnimOut)
                        .setEaseInBack()
                        .setOnComplete(() => obj.SetActive(false));
                });
            });
    }

    // ======================= UNITY LIFECYCLE =======================
    private void Start()
    {
        if (spawnParent == null)
        {
            var go = new GameObject("SpawnedContainer");
            go.transform.SetParent(this.transform, false);
            spawnParent = go.transform;
        }

        if (btnHancurkan != null)
        {
            btnHancurkan.onClick.RemoveAllListeners();
            btnHancurkan.onClick.AddListener(HancurkanModel);
            btnHancurkan.interactable = false;
        }

        if (btnMode != null)
        {
            btnMode.onClick.RemoveAllListeners();
            btnMode.onClick.AddListener(OnClickModeButton);
        }

        // sinkronkan fastMode <-> colorFastPreset
        SyncPresetFlags();
        UpdatePhotoButtonState();

        if (runOnStart) StartCoroutine(AutoStartRoutine());
    }

    private void SyncPresetFlags()
    {
        fastMode = !colorFastPreset; // selalu kebalikan
    }

    private IEnumerator AutoStartRoutine()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
        Run();
    }

    // ======================= BUTTON HANDLERS =======================
    public void OnClickModeButton()
    {
        // Toggle: jika sebelumnya ColorFast ON, sekarang OFF (berarti Fast ON)
        colorFastPreset = !colorFastPreset;
        SyncPresetFlags();

        // Alert
        if (colorFastPreset) ShowAlertObject(alertOn); else ShowAlertObject(alertOff);

        EmitStatus(colorFastPreset ? "Mode: COLOR FAST (bertekstur)" : "Mode: FAST (tanpa tekstur)");
    }

    // ======================= PUBLIC CONTROLS =======================
    [ContextMenu("Run Image->3D")]
    public void Run()
    {
     
        if (btnPhoto != null) btnPhoto.interactable = false; // sedang proses
        if (_running != null) { Warn("Masih berjalan. Cancel dulu untuk restart."); return; }
        _cancelRequested = false;
        ShowLoading("Menyiapkan...");
        _tMark = Time.realtimeSinceStartup;
        _running = StartCoroutine(RunFlowRoutine());
    }

    [ContextMenu("Cancel")]
    public void Cancel()
    {
        _cancelRequested = true;
        if (_running != null) { StopCoroutine(_running); _running = null; }
        EmitStatus("Dibatalkan oleh pengguna.");
        EmitProgress(0f);
        SetDestroyButtonState(false);
        UpdatePhotoButtonState();
        if (autoHideLoading) HideLoading();
    }

    // ======================= CORE FLOW =======================
    private IEnumerator RunFlowRoutine()
    {
        EmitStatus("Submitting job...");
        Mark("Mulai submit");

        var payload = BuildPayload();
        string payloadJson = JsonUtility.ToJson(payload);

        using (var req = NewJsonRequest(SubmitUrl, UnityWebRequest.kHttpVerbPOST, payloadJson))
        {
            yield return req.SendWebRequest();
            if (!IsReqOK(req)) { Fail($"Submit failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}"); yield break; }

            var submitText = req.downloadHandler.text;
            var submit = JsonUtility.FromJson<MeshySubmitResponse>(submitText);
            _taskId = submit != null ? submit.result : null;

            if (string.IsNullOrEmpty(_taskId))
            {
                var direct = JsonUtility.FromJson<MeshyDirectResult>(submitText);
                if (direct != null && direct.model_urls != null && !string.IsNullOrEmpty(direct.model_urls.glb))
                {
                    CaptureModelUrls(direct.model_urls);
                    onGlbUrlReceived?.Invoke(lastGlbUrl);
                    EmitStatus("Mengunduh GLB (direct)...");
                    Mark("Submit direct OK → unduh GLB");
                    yield return DownloadAndLoadGlbBytesRoutine(lastGlbUrl);
                    yield break;
                }
                Fail("task_id tidak ditemukan pada respons submit.");
                yield break;
            }
        }

        if (useSse)
        {
            bool finished = false;
            Mark("Submit OK → tunggu SSE");
            yield return StartCoroutine(SseStreamRoutine(_taskId, ok => finished = ok));
            if (!finished && !_cancelRequested)
            {
                Warn("SSE gagal → fallback polling...");
                yield return StartCoroutine(PollStatusRoutine(_taskId));
            }
        }
        else
        {
            Mark("Submit OK → polling");
            yield return StartCoroutine(PollStatusRoutine(_taskId));
        }
    }

    private ImageTo3DPayload BuildPayload()
    {
        // Fast: TANPA tekstur
        if (fastMode)
        {
            return new ImageTo3DPayload
            {
                image_url = imageUrl,
                enable_pbr = false,
                should_remesh = false,
                should_texture = false, // penting: no texture ⇒ objek putih
                topology = "triangle",
                target_polycount = 300
            };
        }

        // ColorFast: BERTEKSTUR (sesuai foto)
        if (colorFastPreset)
        {
            return new ImageTo3DPayload
            {
                image_url = imageUrl,
                enable_pbr = false,
                should_remesh = false,
                should_texture = true,  // penting: minta tekstur
                topology = "triangle",
                target_polycount = Mathf.Clamp(2000, 100, 10000)
            };
        }

        // Manual (kalau suatu saat mau dipakai)
        return new ImageTo3DPayload
        {
            image_url = imageUrl,
            enable_pbr = enablePbr,
            should_remesh = shouldRemesh,
            should_texture = shouldTexture,
            topology = string.IsNullOrEmpty(topology) ? "triangle" : topology,
            target_polycount = Mathf.Clamp(targetPolycount, 100, 300000)
        };
    }

    // ======================= SSE PROGRESS =======================
    private IEnumerator SseStreamRoutine(string taskId, Action<bool> done)
    {
        done?.Invoke(false);
        EmitStatus("Menunggu progres (SSE)...");
        Mark("Mulai SSE");

        using (var req = new UnityWebRequest(BuildStreamUrl(taskId), UnityWebRequest.kHttpVerbGET))
        {
            var handler = new SseDownloadHandlerV2();
            req.downloadHandler = handler;
            req.timeout = sseTimeoutSeconds; // 0 = infinite
            req.SetRequestHeader("Accept", "text/event-stream");
            req.SetRequestHeader("Cache-Control", "no-cache");
            req.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            var op = req.SendWebRequest();

            while (!op.isDone)
            {
                if (_cancelRequested) { req.Abort(); EmitStatus("Dibatalkan."); break; }

                while (handler.TryDequeueEvent(out var json))
                {
                    var st = JsonUtility.FromJson<MeshyTaskStatusResponse>(json);
                    if (st == null || string.IsNullOrEmpty(st.status)) continue;

                    if (IsSuccess(st.status))
                    {
                        CaptureModelUrls(st.model_urls);
                        onGlbUrlReceived?.Invoke(lastGlbUrl);
                        EmitStatus("Selesai. Mengunduh GLB...");
                        Mark("SSE: SUCCEEDED → unduh GLB");
                        if (!string.IsNullOrEmpty(lastGlbUrl))
                        {
                            yield return DownloadAndLoadGlbBytesRoutine(lastGlbUrl);
                            done?.Invoke(true);
                        }
                        else
                        {
                            Fail("Selesai, namun model_urls.glb kosong.");
                            done?.Invoke(true);
                        }
                        yield break;
                    }

                    if (IsFail(st.status))
                    {
                        Fail($"Task gagal: {(st.task_error != null ? st.task_error.message : "unknown error")}");
                        done?.Invoke(true);
                        yield break;
                    }

                    UpdateProgressUi(st.status, st.progress);
                }

                yield return null;
            }

#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success && !_cancelRequested)
#else
            if ((req.isNetworkError || req.isHttpError) && !_cancelRequested)
#endif
            {
                Warn($"SSE terputus: {req.responseCode} {req.error}");
            }
        }

        yield return null; // lanjut polling bila perlu
    }

    // ======================= POLLING (FALLBACK) =======================
    private IEnumerator PollStatusRoutine(string taskId)
    {
        float t0 = Time.time;
        while (Time.time - t0 < timeoutSeconds)
        {
            if (_cancelRequested) { EmitStatus("Dibatalkan."); yield break; }

            using (var req = UnityWebRequest.Get(BuildStatusUrl(taskId)))
            {
                req.timeout = requestTimeoutSeconds;
                req.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                yield return req.SendWebRequest();

                if (!IsReqOK(req)) { Fail($"Poll failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}"); yield break; }

                var st = JsonUtility.FromJson<MeshyTaskStatusResponse>(req.downloadHandler.text);
                if (st != null && !string.IsNullOrEmpty(st.status))
                {
                    if (IsSuccess(st.status))
                    {
                        CaptureModelUrls(st.model_urls);
                        onGlbUrlReceived?.Invoke(lastGlbUrl);
                        EmitStatus("Selesai. Mengunduh GLB...");
                        Mark("Polling: SUCCEEDED → unduh GLB");
                        if (!string.IsNullOrEmpty(lastGlbUrl))
                            yield return DownloadAndLoadGlbBytesRoutine(lastGlbUrl);
                        else
                            Fail("Selesai, namun model_urls.glb kosong.");
                        yield break;
                    }
                    if (IsFail(st.status))
                    {
                        Fail($"Task gagal: {(st.task_error != null ? st.task_error.message : "unknown error")}");
                        yield break;
                    }
                    UpdateProgressUi(st.status, st.progress);
                }
                else
                {
                    EmitStatus("Status? (format tak dikenali)...");
                }
            }

            float dt = 0f;
            while (dt < pollIntervalSeconds)
            {
                if (_cancelRequested) { EmitStatus("Dibatalkan."); yield break; }
                dt += Time.deltaTime;
                yield return null;
            }
        }

        Fail("Timeout menunggu hasil.");
    }

    [ContextMenu("Test Load Duck (GLB)")]
    public void TestLoadDuck()
    {
        StartCoroutine(DownloadAndLoadGlbBytesRoutine(
            "https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Models/master/2.0/Duck/glTF-Binary/Duck.glb"
        ));
    }

    // ======================= DOWNLOAD + LOAD =======================
    private IEnumerator DownloadAndLoadGlbBytesRoutine(string url)
    {
        if (_cancelRequested) yield break;

        float hardDeadline = Time.realtimeSinceStartup + Mathf.Max(60f, timeoutSeconds);
        ShowLoading("Mengunduh & memuat model...");
        Debug.Log($"[MeshyV2] Device: {SystemInfo.deviceModel} | GPU: {SystemInfo.graphicsDeviceName} | API: {SystemInfo.graphicsDeviceType}");

        int oldMasterTexLimit = QualitySettings.globalTextureMipmapLimit;
        if (lowerTextureResWhileLoading) QualitySettings.globalTextureMipmapLimit = 1;

        try
        {
            // 1) HEAD untuk Content-Length
            long? expectedSize = null;
            yield return TryGetRemoteFileSize(url, s => expectedSize = s);
            if (expectedSize.HasValue)
                EmitStatus($"Ukuran file (server): {FormatBytes(expectedSize.Value)}");
            else
                EmitStatus("Ukuran file (server) tidak tersedia.");

#if UNITY_ANDROID && !UNITY_EDITOR
            string path = System.IO.Path.Combine(Application.persistentDataPath, "meshy_tmp.glb");

            // 2) Download ke file + progres
            using (var req = UnityWebRequest.Get(url)) {
                req.timeout = requestTimeoutSeconds;
                req.downloadHandler = new DownloadHandlerFile(path);
                var op = req.SendWebRequest();
                var tStart = Time.realtimeSinceStartup;

                while (!op.isDone) {
                    if (_cancelRequested) { Fail("Dibatalkan."); yield break; }
                    if (Time.realtimeSinceStartup > hardDeadline) { Fail("Timeout saat download."); yield break; }

#if UNITY_2020_1_OR_NEWER
                    long got = (long)req.downloadedBytes;
                    string sizeText = expectedSize.HasValue ? $"{FormatBytes(got)} / {FormatBytes(expectedSize.Value)}" : $"{FormatBytes(got)}";
                    double speed = (Time.realtimeSinceStartup - tStart) > 0 ? req.downloadedBytes / (Time.realtimeSinceStartup - tStart) : 0;
                    EmitStatus($"Mengunduh… {req.downloadProgress:P0}  {sizeText}  ({FormatBytes((long)speed)}/s)");
#else
                    EmitStatus($"Mengunduh… {req.downloadProgress:P0}");
#endif
                    EmitProgress(Mathf.Clamp01(req.downloadProgress));
                    yield return null;
                }

                if (!IsReqOK(req)) { Fail($"Download gagal: {req.responseCode} {req.error}"); yield break; }

                long fileSize = 0;
                try { fileSize = new System.IO.FileInfo(path).Length; } catch {}
                EmitStatus($"Selesai unduh: {FormatBytes(fileSize)} dalam {(Time.realtimeSinceStartup - tStart):0.0}s");
            }

            // 3) Load via glTFast dari file://
            var gltf = new GLTFast.GltfImport();
            string uri = path.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ? path : "file://" + path;
            Mark("Mulai load GLB (Android)");

            var loadTask = gltf.Load(uri); // Task<bool>
            while (!loadTask.IsCompleted) {
                if (_cancelRequested) { Fail("Dibatalkan."); yield break; }
                if (Time.realtimeSinceStartup > hardDeadline) { Fail("Timeout saat load."); yield break; }
                yield return null;
            }
            if (!loadTask.Result) { Fail("GLTFast load gagal (Android). Cek Draco/KTX/Meshopt + ARM64."); yield break; }

            // 4) Instantiate async
            var parent = spawnParent != null ? spawnParent : (parentUnderThis ? this.transform : null);
            Mark("Mulai instantiate (Android)");
            var instTask = gltf.InstantiateMainSceneAsync(parent);
            while (!instTask.IsCompleted) {
                if (_cancelRequested) { Fail("Dibatalkan."); yield break; }
                yield return null;
            }
            if (!instTask.Result) { Fail("Instantiate gagal (Android)."); yield break; }

            // 5) Transform + materials
            if (parent) {
                for (int i = 0; i < parent.childCount; i++) {
                    var r = parent.GetChild(i);
                    r.gameObject.SetActive(true);
                    r.localPosition = spawnLocalPosition;
                    r.localRotation = Quaternion.Euler(spawnLocalRotationEuler);
                    r.localScale    = spawnLocalScale;
                }
                // kalau fastMode (tanpa tekstur), sederhanakan material supaya ringan
                if (fastMode) SimplifyMaterialsKeepColor(parent);
            }
            Mark("Instantiate selesai (Android)");
            SetDestroyButtonState(true);
            UpdatePhotoButtonState();

#else
            // ===== Non-Android: buffer RAM + progres =====
            byte[] glbBytes = null;
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = requestTimeoutSeconds;
                var op = req.SendWebRequest();

                var tStart = Time.realtimeSinceStartup;
                while (!op.isDone)
                {
                    if (_cancelRequested) { Fail("Dibatalkan."); yield break; }
                    if (Time.realtimeSinceStartup > hardDeadline) { Fail("Timeout saat download."); yield break; }

#if UNITY_2020_1_OR_NEWER
                    long got = (long)req.downloadedBytes;
                    string sizeText = expectedSize.HasValue ? $"{FormatBytes(got)} / {FormatBytes(expectedSize.Value)}" : $"{FormatBytes(got)}";
                    double speed = (Time.realtimeSinceStartup - tStart) > 0 ? req.downloadedBytes / (Time.realtimeSinceStartup - tStart) : 0;
                    EmitStatus($"Mengunduh… {req.downloadProgress:P0}  {sizeText}  ({FormatBytes((long)speed)}/s)");
#else
                    EmitStatus($"Mengunduh… {req.downloadProgress:P0}");
#endif
                    EmitProgress(Mathf.Clamp01(req.downloadProgress));
                    yield return null;
                }

                if (!IsReqOK(req)) { Fail($"Download gagal: {req.responseCode} {req.error}"); yield break; }

#if UNITY_2020_1_OR_NEWER
                long finalBytes = (long)req.downloadedBytes;
                EmitStatus($"Selesai unduh: {FormatBytes(finalBytes)} dalam {(Time.realtimeSinceStartup - tStart):0.0}s");
#else
                EmitStatus($"Selesai unduh: {FormatBytes(req.downloadHandler.data?.LongLength ?? 0)}");
#endif
                glbBytes = req.downloadHandler.data;
            }
            if (glbBytes == null || glbBytes.Length == 0) { Fail("Data GLB kosong."); yield break; }

            // Load glTFast dari byte[]
            var gltf = new GLTFast.GltfImport();
            Mark("Mulai load GLB (Editor/Non-Android)");
            var t = gltf.LoadGltfBinary(glbBytes);
            while (!t.IsCompleted)
            {
                if (_cancelRequested) { Fail("Dibatalkan."); yield break; }
                if (Time.realtimeSinceStartup > hardDeadline) { Fail("Timeout saat load."); yield break; }
                yield return null;
            }
            if (!t.Result) { Fail("GLTFast load gagal (Editor/Non-Android)."); yield break; }

            // Instantiate
            var parent = spawnParent != null ? spawnParent : (parentUnderThis ? this.transform : null);
            Mark("Mulai instantiate (Editor/Non-Android)");
            if (!gltf.InstantiateMainScene(parent)) { Fail("Instantiate gagal (Editor/Non-Android)."); yield break; }

            // Transform + materials
            if (parent)
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    var r = parent.GetChild(i);
                    r.gameObject.SetActive(true);
                    r.localPosition = spawnLocalPosition;
                    r.localRotation = Quaternion.Euler(spawnLocalRotationEuler);
                    r.localScale = spawnLocalScale;
                }
                if (fastMode) SimplifyMaterialsKeepColor(parent);
            }
            Mark("Instantiate selesai (Editor/Non-Android)");
            SetDestroyButtonState(true);
            UpdatePhotoButtonState();
#endif

            Done();
        }
        finally
        {
            if (lowerTextureResWhileLoading) QualitySettings.globalTextureMipmapLimit = oldMasterTexLimit;
            if (autoHideLoading) HideLoading();
        }
    }

    // ======================= MATERIAL HELPERS =======================
    private Shader TryGetSimpleShaderForPipeline()
    {
        var rp = GraphicsSettings.currentRenderPipeline;

        if (rp == null)
        {
            var s = Shader.Find("Mobile/Diffuse");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }
        else
        {
            var s = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("HDRP/Lit");
            return s;
        }
    }

    // Mode tanpa tekstur: material ringan (hapus map berat, jaga warna/tint yang ada)
    private void SimplifyMaterialsKeepColor(Transform parent)
    {
        if (!parent) return;

        var targetShader = TryGetSimpleShaderForPipeline();

        foreach (var r in parent.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (!mat) continue;

                if (mat.HasProperty("_BumpMap")) { mat.SetTexture("_BumpMap", null); mat.DisableKeyword("_NORMALMAP"); }
                if (mat.HasProperty("_MetallicGlossMap")) { mat.SetTexture("_MetallicGlossMap", null); }
                if (mat.HasProperty("_OcclusionMap")) { mat.SetTexture("_OcclusionMap", null); }
                if (mat.HasProperty("_SpecGlossMap")) { mat.SetTexture("_SpecGlossMap", null); }
                if (mat.HasProperty("_ParallaxMap")) { mat.SetTexture("_ParallaxMap", null); mat.DisableKeyword("_PARALLAXMAP"); }
                mat.DisableKeyword("_DETAIL_MULX2");
                mat.DisableKeyword("_EMISSION");

                if (targetShader != null)
                    mat.shader = targetShader;
            }
        }
    }

    // ======================= UI & LOG =======================
    private void ShowLoading(string text = null)
    {
        if (loadingObject != null && !loadingObject.activeSelf)
            loadingObject.SetActive(true);
        //if (Camera != null) Camera.SetActive(false);
        if (!string.IsNullOrEmpty(text)) onStatusText?.Invoke(text);
        if (Rotate != null) Rotate.SetActive(true);
    }

    private void HideLoading()
    {
        if (loadingObject != null && loadingObject.activeSelf)
            loadingObject.SetActive(false);
        //if (Rotate != null) Rotate.SetActive(false);
    }

    private void UpdateProgressUi(string status, float progress)
    {
        string suffix = progress > 0f ? $" ({progress:P0})" : "";
        EmitStatus($"Status: {status}{suffix}");
        if (progress > 0f && progress <= 1f) EmitProgress(progress);
        else EmitProgress(Mathf.PingPong(Time.time * 0.1f, 0.9f));
    }

    private void EmitProgress(float p)
    {
        simulatedProgress = Mathf.Clamp01(p);
        onProgress?.Invoke(simulatedProgress);
    }

    private void EmitStatus(string msg)
    {
        Debug.Log($"[MeshyV2] {msg}");
        onStatusText?.Invoke(msg);
    }

    private void Done()
    {
        EmitProgress(1f);
        EmitStatus("Selesai.");
        _running = null;
    }

    private void Fail(string msg)
    {
        Debug.LogError($"[MeshyV2] {msg}");
        onStatusText?.Invoke(msg);
        _running = null;
        SetDestroyButtonState(false);
        UpdatePhotoButtonState();
        if (autoHideLoading) HideLoading();
    }

    private static void Warn(string msg) => Debug.LogWarning($"[MeshyV2] {msg}");

    private void Mark(string label)
    {
        var now = Time.realtimeSinceStartup;
        Debug.Log($"[MeshyV2] {label} @ {now:0.00}s (+{now - _tMark:0.00}s)");
        _tMark = now;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        var t = go.transform;
        for (int i = 0; i < t.childCount; i++) SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }

    private void SetDestroyButtonState(bool on)
    {
        if (btnHancurkan != null) btnHancurkan.interactable = on;
    }

    private void UpdatePhotoButtonState()
    {
        if (btnPhoto == null || spawnParent == null) return;
        bool hasObject = spawnParent.childCount > 0;
        //btnPhoto.interactable = !hasObject; // aktif kalau belum ada objek
    }

    [ContextMenu("Hancurkan Model")]
    public void HancurkanModel()
    {
        if (spawnParent == null) return;
        btnPhoto.interactable = true;
        for (int i = spawnParent.childCount - 1; i >= 0; i--)
        {
            var child = spawnParent.GetChild(i);
            Destroy(child.gameObject);
        }

        EmitStatus("Model dihancurkan.");
        SetDestroyButtonState(false);
        UpdatePhotoButtonState(); // aktifkan kembali btnPhoto
    }

    // ======================= API CONTRACTS =======================
    [Serializable]
    public class ImageTo3DPayload
    {
        public string image_url;
        public bool enable_pbr;
        public bool should_remesh;
        public bool should_texture;
        public string topology;
        public int target_polycount;
    }

    [Serializable]
    public class MeshySubmitResponse
    {
        public string result; // taskId
        public string status;
        public string message;
    }

    [Serializable]
    public class MeshyDirectResult
    {
        public string status;
        public ModelUrls model_urls;
        public TaskError task_error;
    }

    [Serializable]
    public class MeshyTaskStatusResponse
    {
        public string id;
        public string status;          // PENDING / IN_PROGRESS / SUCCEEDED / FAILED
        public float progress;         // 0..1
        public string thumbnail_url;
        public ModelUrls model_urls;
        public TextureSet[] texture_urls;
        public TaskError task_error;
        public string message;
    }

    [Serializable]
    public class ModelUrls
    {
        public string glb;
        public string fbx;
        public string obj;
        public string usdz;
    }

    [Serializable]
    public class TextureSet
    {
        public string base_color;
        public string normal;
        public string roughness;
        public string metallic;
        public string ao;
    }

    [Serializable] public class TaskError { public string message; }

    // ======================= REQUEST UTILS =======================
    private UnityWebRequest NewJsonRequest(string url, string method, string jsonBody)
    {
        var req = new UnityWebRequest(url, method)
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = requestTimeoutSeconds
        };
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        return req;
    }

    private static bool IsReqOK(UnityWebRequest r)
    {
#if UNITY_2020_2_OR_NEWER
        return r.result == UnityWebRequest.Result.Success;
#else
        return !r.isNetworkError && !r.isHttpError;
#endif
    }

    private static bool IsSuccess(string s) => s.Equals("SUCCEEDED", StringComparison.OrdinalIgnoreCase);
    private static bool IsFail(string s) => s.Equals("FAILED", StringComparison.OrdinalIgnoreCase);

    // ======================= URL CAPTURE =======================
    private void CaptureModelUrls(ModelUrls urls)
    {
        lastGlbUrl = urls != null ? urls.glb : null;
        lastFbxUrl = urls != null ? urls.fbx : null;
        lastObjUrl = urls != null ? urls.obj : null;
        lastUsdzUrl = urls != null ? urls.usdz : null;

        if (!string.IsNullOrEmpty(lastGlbUrl))
            EmitStatus("GLB URL: " + lastGlbUrl);
    }

    // ======================= SIZE / PROGRESS HELPERS =======================
    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:0.0} KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:0.0} MB";
        double gb = mb / 1024.0;
        return $"{gb:0.00} GB";
    }

    private IEnumerator TryGetRemoteFileSize(string url, Action<long?> onDone)
    {
        using (var head = UnityWebRequest.Head(url))
        {
            head.timeout = requestTimeoutSeconds;
            yield return head.SendWebRequest();
            if (IsReqOK(head))
            {
                string len = head.GetResponseHeader("Content-Length");
                if (long.TryParse(len, out var size)) onDone(size);
                else onDone(null);
            }
            else onDone(null);
        }
    }

    // ======================= NESTED SSE HANDLER =======================
    private sealed class SseDownloadHandlerV2 : DownloadHandlerScript
    {
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly Queue<string> _events = new Queue<string>();

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0) return true;

            string chunk = Encoding.UTF8.GetString(data, 0, dataLength);
            _buffer.Append(chunk);

            string all = _buffer.ToString();
            int sep;
            while ((sep = all.IndexOf("\n\n", StringComparison.Ordinal)) >= 0)
            {
                string block = all.Substring(0, sep);
                all = all.Substring(sep + 2);

                foreach (var line in block.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        var json = trimmed.Substring(5).Trim();
                        if (!string.IsNullOrEmpty(json))
                            _events.Enqueue(json);
                    }
                }
            }

            _buffer.Length = 0;
            _buffer.Append(all);
            return true;
        }

        public bool TryDequeueEvent(out string json)
        {
            if (_events.Count > 0)
            {
                json = _events.Dequeue();
                return true;
            }
            json = null;
            return false;
        }
    }
}
