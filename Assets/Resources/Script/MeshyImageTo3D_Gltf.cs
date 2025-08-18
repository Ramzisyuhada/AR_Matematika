using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using GLTFast;

/// <summary>
/// Client Meshy Image-to-3D yang menampilkan hasil langsung ke Scene
/// TANPA menyimpan file GLB ke disk:
/// 1) Submit job dari 1 gambar (URL / Data URI)
/// 2) Dapat taskId
/// 3) Pantau progres (SSE → fallback ke polling)
/// 4) Unduh GLB ke memori (byte[]) dan load via GLTFast (InstantiateMainScene)
///
/// Catatan:
/// - Pasang GLTFast (com.atteneder.gltfast).
/// - Ganti apiKey & imageUrl.
/// - Fast Mode mempercepat (tanpa tekstur & PBR, poly lebih rendah).
/// </summary>
public class MeshyImageTo3D_RuntimeOnly : MonoBehaviour
{
    // =======================
    //   KONFIG ENDPOINT
    // =======================

    private const string SubmitUrl = "https://api.meshy.ai/openapi/v1/image-to-3d";
    private static string StatusUrl(string taskId) => $"https://api.meshy.ai/openapi/v1/image-to-3d/{taskId}";
    private static string StreamUrl(string taskId) => $"https://api.meshy.ai/openapi/v1/image-to-3d/{taskId}/stream";

    // =======================
    //   INSPECTOR: AUTH/INPUT
    // =======================

    [Header("Auth")]
    [Tooltip("API Key Meshy kamu. Header: Authorization: Bearer <apiKey>")]
    public string apiKey = "YOUR_API_KEY";

    [Header("Input")]
    [Tooltip("URL gambar publik ATAU data URI (data:image/png;base64,...)")]
    public string imageUrl = "https://example.com/your_image.png";

    // =======================
    //   INSPECTOR: MODE/OPTIONS
    // =======================

    [Header("Mode Cepat")]
    [Tooltip("ON = tanpa tekstur & PBR, low poly. Lebih cepat.")]
    public bool fastMode = true;

    [Header("Options (non-fast mode)")]
    public bool enablePbr = true;
    public bool shouldRemesh = true;
    public bool shouldTexture = true;
    [Tooltip("triangle / quad")]
    public string topology = "triangle";
    [Tooltip("100..300000 (lebih rendah = lebih cepat)")]
    public int targetPolycount = 30000;

    // =======================
    //   INSPECTOR: PROGRESS/TIMING
    // =======================

    [Header("Progress Streaming")]
    [Tooltip("Pakai SSE progress real-time. Jika gagal, auto fallback ke polling.")]
    public bool useSse = true;

    [Header("Polling (fallback)")]
    public float pollIntervalSeconds = 3f;
    public float timeoutSeconds = 600f; // maksimal 10 menit nunggu

    // =======================
    //   INSPECTOR: AUTOSTART/SPAWN
    // =======================

    [Header("Autostart")]
    [Tooltip("Jalankan otomatis saat Play.")]
    public bool runOnStart = true;
    [Tooltip("Tunda autostart (detik).")]
    public float startDelay = 0f;

    [Header("Spawn Options")]
    [Tooltip("Jika ON, jadikan GameObject ini parent hasil model.")]
    public bool parentUnderThis = true;
    public Vector3 spawnLocalPosition = Vector3.zero;
    public Vector3 spawnLocalRotationEuler = Vector3.zero;
    public Vector3 spawnLocalScale = Vector3.one;

    // =======================
    //   INSPECTOR: UI HOOKS
    // =======================

    [Header("UI Hooks (opsional)")]
    [Range(0, 1f)] public float simulatedProgress;
    public UnityEvent<float> onProgress;     // hubungkan ke Slider.value
    public UnityEvent<string> onStatusText;  // hubungkan ke Text/TMP.text

    // =======================
    //   RUNTIME STATE
    // =======================

    private string _taskId;
    private bool _cancelRequested;
    private Coroutine _running;

    // =======================
    //   UNITY LIFECYCLE
    // =======================
    // =======================
    //   INSPECTOR: LOADING UI
    // =======================
    [Header("Loading UI")]
    [Tooltip("Drag GameObject loading/spinner kamu ke sini. Default: non-aktif saat Start.")]
    public GameObject loadingObject;
    public GameObject Camera;
    [Tooltip("Sembunyikan loading otomatis saat selesai/gagal/cancel.")]
    public bool autoHideLoading = true;
    private void ShowLoading(string text = null)
    {
        if (loadingObject != null && !loadingObject.activeSelf)
            loadingObject.SetActive(true);
        Camera.SetActive(false);
        if (!string.IsNullOrEmpty(text))
            onStatusText?.Invoke(text);
    }

    private void HideLoading()
    {
        if (loadingObject != null && loadingObject.activeSelf)
            loadingObject.SetActive(false);
    }
    private void Start()
    {
        if (runOnStart) StartCoroutine(AutoStartRoutine());
    }

    private IEnumerator AutoStartRoutine()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
        //Run();
    }

    // =======================
    //   PUBLIC CONTROLS
    // =======================

    /// <summary>Mulai proses submit → pantau → download memori → load ke Scene.</summary>
    [ContextMenu("Run Image->3D")]
    public void Run()
    {
        if (_running != null) { Warn("Masih berjalan. Cancel dulu untuk restart."); return; }
        _cancelRequested = false;
        ShowLoading("Menyiapkan...");

        _running = StartCoroutine(RunFlowRoutine());
    }

    /// <summary>Batalkan proses yang sedang berjalan.</summary>
    [ContextMenu("Cancel")]
    public void Cancel()
    {
        _cancelRequested = true;
        if (_running != null) { StopCoroutine(_running); _running = null; }
        EmitStatus("Dibatalkan oleh pengguna.");
        EmitProgress(0f);
    }

    // =======================
    //   CORE FLOW
    // =======================

    /// <summary> Submit → (SSE atau polling) → download GLB (byte[]) → load & spawn. </summary>
    private IEnumerator RunFlowRoutine()
    {
        EmitStatus("Submitting job...");

        // 1) Submit job
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
                // Fallback jika POST langsung memberi model_urls
                var direct = JsonUtility.FromJson<MeshyDirectResult>(submitText);
                if (direct != null && direct.model_urls != null && !string.IsNullOrEmpty(direct.model_urls.glb))
                {
                    EmitStatus("Mengunduh GLB (direct)...");
                    yield return DownloadAndLoadGlbBytesRoutine(direct.model_urls.glb);
                    yield break;
                }
                Fail("task_id tidak ditemukan pada respons submit. Cek API Meshy.");
                yield break;
            }
        }

        // 2) Pantau progress & ambil hasil
        if (useSse)
        {
            bool finished = false;
            yield return StartCoroutine(SseStreamRoutine(_taskId, ok => finished = ok));
            if (!finished && !_cancelRequested)
            {
                Warn("SSE terputus/gagal. Fallback ke polling...");
                yield return StartCoroutine(PollStatusRoutine(_taskId));
            }
        }
        else
        {
            yield return StartCoroutine(PollStatusRoutine(_taskId));
        }
    }

    /// <summary>Bangun payload Meshy, pilih cepat/berat.</summary>
    private ImageTo3DPayload BuildPayload()
    {
        if (fastMode)
        {
            // Mode cepat: geometry only + low poly
            return new ImageTo3DPayload
            {
                image_url = imageUrl,
                enable_pbr = false,
                should_remesh = false,
                should_texture = false,
                topology = "triangle",
                target_polycount = 8000
            };
        }

        // Mode kustom: bisa tekstur/PBR, polycount lebih tinggi
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

    // =======================
    //   SSE PROGRESS
    // =======================

    /// <summary>
    /// Progres real-time via SSE. Saat SUCCEEDED, langsung unduh GLB (ke memori) lalu load ke Scene.
    /// </summary>
    private IEnumerator SseStreamRoutine(string taskId, Action<bool> done)
    {
        done?.Invoke(false);
        EmitStatus("Menunggu progres (SSE)...");

        using (var req = new UnityWebRequest(StreamUrl(taskId), UnityWebRequest.kHttpVerbGET))
        {
            var handler = new SseDownloadHandler();
            req.downloadHandler = handler;
            req.SetRequestHeader("Accept", "text/event-stream");
            req.SetRequestHeader("Cache-Control", "no-cache");
            req.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            var op = req.SendWebRequest();

            while (!op.isDone)
            {
                if (_cancelRequested) { req.Abort(); EmitStatus("Dibatalkan."); break; }

                // Proses event 'data: {json}'
                while (handler.TryDequeueEvent(out var json))
                {
                    var st = JsonUtility.FromJson<MeshyTaskStatusResponse>(json);
                    if (st == null || string.IsNullOrEmpty(st.status)) continue;

                    if (IsSuccess(st.status))
                    {
                        EmitStatus("Selesai. Mengunduh GLB...");
                        string glbUrl = st.model_urls != null ? st.model_urls.glb : null;
                        if (!string.IsNullOrEmpty(glbUrl))
                        {
                            yield return DownloadAndLoadGlbBytesRoutine(glbUrl);
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

                yield return null; // jangan blokir frame
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

        // tidak sukses/gagal eksplisit → fallback
        yield return null;
    }

    // =======================
    //   POLLING (FALLBACK)
    // =======================

    /// <summary>
    /// Poll status berkala hingga sukses/gagal/timeout, lalu unduh GLB (ke memori) dan load ke Scene.
    /// </summary>
    private IEnumerator PollStatusRoutine(string taskId)
    {
        float t0 = Time.time;

        while (Time.time - t0 < timeoutSeconds)
        {
            if (_cancelRequested) { EmitStatus("Dibatalkan."); yield break; }

            using (var req = UnityWebRequest.Get(StatusUrl(taskId)))
            {
                req.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                yield return req.SendWebRequest();

                if (!IsReqOK(req)) { Fail($"Poll failed: {req.responseCode} {req.error}\n{req.downloadHandler.text}"); yield break; }

                var st = JsonUtility.FromJson<MeshyTaskStatusResponse>(req.downloadHandler.text);
                if (st == null || string.IsNullOrEmpty(st.status))
                {
                    EmitStatus("Status? (format tak dikenali)...");
                }
                else
                {
                    if (IsSuccess(st.status))
                    {
                        EmitStatus("Selesai. Mengunduh GLB...");
                        string glbUrl = st.model_urls != null ? st.model_urls.glb : null;
                        if (!string.IsNullOrEmpty(glbUrl))
                        {
                            yield return DownloadAndLoadGlbBytesRoutine(glbUrl);
                        }
                        else
                        {
                            Fail("Selesai, namun model_urls.glb kosong.");
                        }
                        yield break;
                    }

                    if (IsFail(st.status))
                    {
                        Fail($"Task gagal: {(st.task_error != null ? st.task_error.message : "unknown error")}");
                        yield break;
                    }

                    UpdateProgressUi(st.status, st.progress);
                }
            }

            // jeda polling non-blocking
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

    // =======================
    //   DOWNLOAD (BYTE[]) + LOAD
    // =======================

    /// <summary>
    /// Mengunduh GLB → byte[] (tanpa simpan) → load via GLTFast → spawn ke Scene.
    /// </summary>
    private IEnumerator DownloadAndLoadGlbBytesRoutine(string url)
    {
        if (_cancelRequested) yield break;

        // Unduh GLB sebagai byte[]
        byte[] glbBytes = null;
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (!IsReqOK(req)) { Fail($"Download gagal: {req.responseCode} {req.error}\n{req.downloadHandler.text}"); yield break; }
            glbBytes = req.downloadHandler.data;
        }

        if (glbBytes == null || glbBytes.Length == 0) { Fail("Data GLB kosong."); yield break; }

        // Load langsung dari byte[]
        var gltf = new GltfImport();
        Task<bool> loadTask = gltf.LoadGltfBinary(glbBytes);

        while (!loadTask.IsCompleted)
        {
            if (_cancelRequested) yield break;
            yield return null;
        }

        if (!loadTask.Result) { Fail("GLTFast load gagal (byte[])."); yield break; }

        // Spawn ke Scene
        var parent = parentUnderThis ? this.transform : null;
        bool instantiated = gltf.InstantiateMainScene(parent);
        if (!instantiated) { Fail("GLTFast instantiate gagal."); yield break; }

        if (parent != null && parent.childCount > 0)
        {
            var root = parent.GetChild(parent.childCount - 1);
            root.localPosition = spawnLocalPosition;
            root.localRotation = Quaternion.Euler(spawnLocalRotationEuler);
            root.localScale = spawnLocalScale;
        }

        Log("Model di-spawn langsung (tanpa save).");
        if (autoHideLoading) HideLoading();

        Done();
    }

    // =======================
    //   API CONTRACTS (JSON)
    // =======================

    [Serializable]
    public class ImageTo3DPayload
    {
        public string image_url;
        public bool enable_pbr;
        public bool should_remesh;
        public bool should_texture;
        public string topology;       // "triangle" | "quad"
        public int target_polycount;  // 100..300000
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
        public float progress;         // 0..1 (kadang 0 jika server tidak expose)
        public string thumbnail_url;
        public ModelUrls model_urls;   // ambil .glb di sini
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

    // =======================
    //   UTILS: REQUEST/STATUS/UI/LOG
    // =======================

    /// <summary>Buat request JSON (POST/PUT) lengkap header Content-Type & Authorization.</summary>
    private UnityWebRequest NewJsonRequest(string url, string method, string jsonBody)
    {
        var req = new UnityWebRequest(url, method)
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        return req;
    }

    /// <summary>Cek sukses request (Unity 2020+).</summary>
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

    /// <summary>Update UI status & progress.</summary>
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
        Debug.LogError($"[Meshy] {msg}");
        onStatusText?.Invoke(msg);
        _running = null;
    }

    private static void Log(string msg) => Debug.Log($"[Meshy] {msg}");
    private static void Warn(string msg) => Debug.LogWarning($"[Meshy] {msg}");
}

/// <summary>
/// DownloadHandler SSE sederhana:
/// - Mengumpulkan stream text/event-stream
/// - Mem-parse blok event dipisah oleh \n\n
/// - Mengeluarkan payload JSON dari baris "data: {...}" via queue
/// </summary>
internal class SseDownloadHandler : DownloadHandlerScript
{
    private readonly StringBuilder _buffer = new StringBuilder();
    private readonly Queue<string> _events = new Queue<string>();

    protected override bool ReceiveData(byte[] data, int dataLength)
    {
        if (data == null || dataLength <= 0) return true;

        string chunk = Encoding.UTF8.GetString(data, 0, dataLength);
        _buffer.Append(chunk);

        // Event SSE dipisah dua newline
        string all = _buffer.ToString();
        int sep;
        while ((sep = all.IndexOf("\n\n", StringComparison.Ordinal)) >= 0)
        {
            string block = all.Substring(0, sep);
            all = all.Substring(sep + 2);

            // Ambil baris yang diawali "data:"
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

        // Sisakan sisa parsial untuk chunk berikutnya
        _buffer.Length = 0;
        _buffer.Append(all);
        return true;
    }

    /// <summary>Ambil satu event JSON dari queue (jika ada).</summary>
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
