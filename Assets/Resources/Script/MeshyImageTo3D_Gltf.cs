using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using GLTFast;

public class MeshyImageTo3D_RuntimeOnlyV2 : MonoBehaviour
{
    // ======================= QUALITY / STARTUP =======================
    private void Awake()
    {
        QualitySettings.asyncUploadTimeSlice = 4;
        QualitySettings.asyncUploadBufferSize = 16; // MB
        QualitySettings.asyncUploadPersistentBuffer = true;

        QualitySettings.streamingMipmapsActive = true;
        Texture2D.streamingTextureDiscardUnusedMips = true;

        QualitySettings.antiAliasing = 0;
        QualitySettings.shadows = ShadowQuality.Disable;
        Application.targetFrameRate = 30;

#if UNITY_ANDROID && !UNITY_EDITOR
        useSse = false; // SSE sering tidak stabil di Android
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
    [Header("Mode Cepat")]
    public bool fastMode = true;

    [Header("Options (non-fast mode)")]
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

    // ======================= AUTOSTART/SPAWN =======================
    [Header("Autostart")]
    public bool runOnStart = true;
    public float startDelay = 0f;

    [Header("Spawn Options")]
    public bool parentUnderThis = true;
    public Vector3 spawnLocalPosition = Vector3.zero;
    public Vector3 spawnLocalRotationEuler = Vector3.zero;
    public Vector3 spawnLocalScale = Vector3.one;

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

    // ======================= UNITY LIFECYCLE =======================
    private void Start()
    {
        if (runOnStart) StartCoroutine(AutoStartRoutine());
    }

    private IEnumerator AutoStartRoutine()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
        Run();
    }

    // ======================= PUBLIC CONTROLS =======================
    [ContextMenu("Run Image->3D")]
    public void Run()
    {
        if (_running != null) { Warn("Masih berjalan. Cancel dulu untuk restart."); return; }
        _cancelRequested = false;
        ShowLoading("Menyiapkan...");
        _running = StartCoroutine(RunFlowRoutine());
    }

    [ContextMenu("Cancel")]
    public void Cancel()
    {
        _cancelRequested = true;
        if (_running != null) { StopCoroutine(_running); _running = null; }
        EmitStatus("Dibatalkan oleh pengguna.");
        EmitProgress(0f);
        if (autoHideLoading) HideLoading();
    }

    // ======================= CORE FLOW =======================
    private IEnumerator RunFlowRoutine()
    {
        EmitStatus("Submitting job...");

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
            yield return StartCoroutine(SseStreamRoutine(_taskId, ok => finished = ok));
            if (!finished && !_cancelRequested)
            {
                Warn("SSE gagal → fallback polling...");
                yield return StartCoroutine(PollStatusRoutine(_taskId));
            }
        }
        else
        {
            yield return StartCoroutine(PollStatusRoutine(_taskId));
        }
    }

    private ImageTo3DPayload BuildPayload()
    {
        if (fastMode)
        {
            return new ImageTo3DPayload
            {
                image_url = imageUrl,
                enable_pbr = false,
                should_remesh = false,
                should_texture = false,
                topology = "triangle",
                target_polycount = 300
            };
        }
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

        using (var req = new UnityWebRequest(BuildStreamUrl(taskId), UnityWebRequest.kHttpVerbGET))
        {
            var handler = new SseDownloadHandlerV2();
            req.downloadHandler = handler;
            req.timeout = requestTimeoutSeconds;
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

        yield return null; // biar lanjut ke polling
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

            // jeda polling
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

        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
string path = System.IO.Path.Combine(Application.persistentDataPath, "meshy_tmp.glb");

// 1) Download ke file
using (var req = UnityWebRequest.Get(url)) {
    req.timeout = requestTimeoutSeconds;
    req.downloadHandler = new DownloadHandlerFile(path);
    var op = req.SendWebRequest();
    while (!op.isDone) {
        if (_cancelRequested) { Fail("Dibatalkan."); yield break; }
        if (Time.realtimeSinceStartup > hardDeadline) { Fail("Timeout saat download."); yield break; }
        yield return null;
    }
    if (!IsReqOK(req)) { Fail($"Download gagal: {req.responseCode} {req.error}"); yield break; }
}

// 2) Load via glTFast dari file://
var gltf = new GLTFast.GltfImport();
string uri = path.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ? path : "file://" + path;

var loadTask = gltf.Load(uri); // Task<bool>
while (!loadTask.IsCompleted) {
    if (_cancelRequested) { Fail("Dibatalkan."); yield break; }
    if (Time.realtimeSinceStartup > hardDeadline) { Fail("Timeout saat load."); yield break; }
    yield return null;
}
if (!loadTask.Result) { Fail("GLTFast load gagal (Android). Cek Draco/KTX/Meshopt + ARM64."); yield break; }

// 3) Instantiate async (lebih aman di perangkat)
var parent = parentUnderThis ? this.transform : null;
var instTask = gltf.InstantiateMainSceneAsync(parent);
while (!instTask.IsCompleted) {
    if (_cancelRequested) { Fail("Dibatalkan."); yield break; }
    yield return null;
}
if (!instTask.Result) { Fail("Instantiate gagal (Android)."); yield break; }

// 4) Terapkan transform
if (parent) {
    for (int i = 0; i < parent.childCount; i++) {
        var r = parent.GetChild(i);
        r.gameObject.SetActive(true);
        //SetLayerRecursively(r.gameObject, 0);
        r.localPosition = spawnLocalPosition;
        r.localRotation = Quaternion.Euler(spawnLocalRotationEuler);
        r.localScale    = spawnLocalScale;
    }
}

#else
            byte[] glbBytes = null;
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = requestTimeoutSeconds;
                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    if (_cancelRequested) { Fail("Dibatalkan."); yield break; }
                    if (Time.realtimeSinceStartup > hardDeadline) { Fail("Timeout saat download."); yield break; }
                    yield return null;
                }
                if (!IsReqOK(req)) { Fail($"Download gagal: {req.responseCode} {req.error}"); yield break; }
                glbBytes = req.downloadHandler.data;
            }
            if (glbBytes == null || glbBytes.Length == 0) { Fail("Data GLB kosong."); yield break; }

            var gltf = new GLTFast.GltfImport();
            var t = gltf.LoadGltfBinary(glbBytes);
            while (!t.IsCompleted)
            {
                if (_cancelRequested) { Fail("Dibatalkan."); yield break; }
                if (Time.realtimeSinceStartup > hardDeadline) { Fail("Timeout saat load."); yield break; }
                yield return null;
            }
            if (!t.Result) { Fail("GLTFast load gagal (Editor/Non-Android)."); yield break; }

            var parent = parentUnderThis ? this.transform : null;
            if (!gltf.InstantiateMainScene(parent)) { Fail("Instantiate gagal (Editor/Non-Android)."); yield break; }
#endif

            Done();
        }
        finally
        {
            if (autoHideLoading) HideLoading();
        }
    }

    // ======================= UI & LOG =======================
    private void ShowLoading(string text = null)
    {
        if (loadingObject != null && !loadingObject.activeSelf)
            loadingObject.SetActive(true);
        if (Camera != null) Camera.SetActive(false);
        if (!string.IsNullOrEmpty(text)) onStatusText?.Invoke(text);
        Rotate.SetActive(true);

    }

    private void HideLoading()
    {
        if (loadingObject != null && loadingObject.activeSelf)
            loadingObject.SetActive(false);
     //   if (Camera != null) Camera.SetActive(true);
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
        Log(msg);
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
        if (autoHideLoading) HideLoading();
    }

    private static void Log(string msg) => Debug.Log($"[MeshyV2] {msg}");
    private static void Warn(string msg) => Debug.LogWarning($"[MeshyV2] {msg}");

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        var t = go.transform;
        for (int i = 0; i < t.childCount; i++) SetLayerRecursively(t.GetChild(i).gameObject, layer);
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
