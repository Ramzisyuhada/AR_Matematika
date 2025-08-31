using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// Zoom & Pan untuk UI Image/RawImage di Canvas:
// - Pinch (HP) & Scroll Wheel (PC)
// - Drag/Pan
// - Clamp agar gambar tidak keluar viewport
// - Auto-Fit sekali saat start (tunggu viewport stabil), lalu *dikunci* agar tidak berubah sendiri
// - Double-Tap: toggle Fit <-> Zoom cepat
// - Auto reset ke Fit saat zoom-out mendekati Fit
public class UIImageZoomPan : MonoBehaviour
{
    [Header("Target & Viewport (UI)")]
    public RectTransform target;           // Image/RawImage yang di-zoom
    public RectTransform viewport;         // Panel/Viewport pembungkus
    public Canvas canvas;                  // Boleh kosong untuk Overlay

    [Header("Zoom")]
    public float minScale = 0.5f;
    public float maxScale = 6f;
    public float pinchSpeed = 0.01f;       // HP/Emulator
    public float wheelSpeed = 0.12f;       // PC/Editor
    public bool useFitAsMinScale = true;   // minScale = skala Fit jika lebih besar

    [Header("Pan / Drag")]
    public float dragSpeed = 1f;

    [Header("Clamp")]
    public bool centerIfSmaller = true;    // kalau lebih kecil dari viewport → center

    [Header("Double Tap")]
    public float doubleTapZoom = 2f;               // zoom cepat (kali dari fit)
    public float doubleTapResetThreshold = 1.05f;  // <= dianggap sedang di mode Fit

    [Header("Auto Reset (Zoom-out)")]
    public bool autoResetOnZoomOut = true;
    [Tooltip("Jika scale <= fitScale * ini → reset ke Fit & center")]
    public float resetWhenUnderFitMul = 1.02f;

    [Header("Stabilizer Init")]
    public bool startHiddenUntilReady = true;  // sembunyikan dulu sampai layout stabil
    [Tooltip("Viewport.rect harus sama N frame berturut-turut sebelum AutoFit")]
    public int stableFramesNeeded = 3;
    [Tooltip("Batas maksimal frame menunggu stabil")]
    public int maxWaitFrames = 60;

    [Header("Lock setelah Init")]
    public bool lockScaleAndPositionAfterInit = true;   // kunci skala/posisi supaya tidak berubah sendiri
    public bool keepAspectRatioFitterDisabled = true;   // kalau target ada ARF, tetap dimatikan setelah init

    // ===== Runtime =====
    Vector2 _nativeSize;
    float _fitScale = 1f;
    Vector2 _prevMouseLocal;
    bool _draggingMouse;

    bool _initDone;
    bool _userInteracted;
    Rect _initialViewportRect;

    CanvasGroup _cg;

    Camera UICamera =>
        (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

    void Awake()
    {
        if (!target) target = transform as RectTransform;
        if (!viewport && target) viewport = target.parent as RectTransform;
        if (!canvas) canvas = GetComponentInParent<Canvas>();

        if (target)
        {
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchorMin = target.anchorMax = new Vector2(0.5f, 0.5f);
        }

        if (startHiddenUntilReady && target)
        {
            _cg = target.GetComponent<CanvasGroup>();
            if (!_cg) _cg = target.gameObject.AddComponent<CanvasGroup>();
            _cg.alpha = 0f;
            _cg.interactable = false;
            _cg.blocksRaycasts = false;
        }
    }

    void OnEnable() { StartCoroutine(InitFitRoutine()); }

    IEnumerator InitFitRoutine()
    {
        // Tahan AspectRatioFitter (ARF) jika ada—dia suka mengubah size di LateUpdate
        var arf = target ? target.GetComponent<AspectRatioFitter>() : null;
        var savedArfMode = AspectRatioFitter.AspectMode.None;
        if (arf)
        {
            savedArfMode = arf.aspectMode;
            arf.enabled = false;
        }

        CacheNativeSize();

        // Tunggu hingga viewport.rect stabil beberapa frame berturut-turut
        int stable = 0, waited = 0;
        Rect prev = default;
        while (viewport && waited < maxWaitFrames)
        {
            var r = viewport.rect;
            if (ApproximatelyRect(r, prev)) stable++;
            else stable = 0;

            prev = r;
            waited++;

            if (stable >= Mathf.Max(1, stableFramesNeeded)) break;
            yield return new WaitForEndOfFrame();
        }

        AutoFit();          // Fit sekali (final)
        ClampToViewport();

        _initialViewportRect = viewport ? viewport.rect : new Rect(0, 0, 0, 0);
        _initDone = true;

        // Pulihkan/biarkan ARF
        if (arf)
        {
            if (keepAspectRatioFitterDisabled)
            {
                arf.enabled = false;       // tetap dimatikan agar ukuran tidak berubah lagi
            }
            else
            {
                arf.enabled = true;
                arf.aspectMode = savedArfMode;
            }
        }

        if (_cg)
        {
            _cg.alpha = 1f;
            _cg.interactable = true;
            _cg.blocksRaycasts = true;
        }
    }

    void CacheNativeSize()
    {
        if (!target) return;

        var img = target.GetComponent<Image>();
        if (img && img.sprite)
        {
            img.preserveAspect = true;
            img.SetNativeSize();
            _nativeSize = target.sizeDelta;
            return;
        }

        var raw = target.GetComponent<RawImage>();
        if (raw && raw.texture)
        {
            float ppu = 100f;
            var scaler = canvas ? canvas.GetComponent<CanvasScaler>() : null;
            if (scaler) ppu = scaler.referencePixelsPerUnit;

            _nativeSize = new Vector2(raw.texture.width / ppu, raw.texture.height / ppu);
            target.sizeDelta = _nativeSize;
            return;
        }

        _nativeSize = target.rect.size; // fallback
    }

    void AutoFit()
    {
        if (!target || !viewport) return;
        var vr = viewport.rect;
        if (_nativeSize.x <= 0f || _nativeSize.y <= 0f) return;

        float sx = vr.width / _nativeSize.x;
        float sy = vr.height / _nativeSize.y;
        _fitScale = Mathf.Clamp(Mathf.Min(sx, sy), 0.01f, maxScale);

        if (useFitAsMinScale) minScale = Mathf.Min(minScale, _fitScale);

        target.localScale = new Vector3(_fitScale, _fitScale, 1f);
        target.anchoredPosition = Vector2.zero;
    }

    void Update()
    {
        if (!target) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        // Scroll zoom (PC)
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            _userInteracted = true;
            ApplyZoom(scroll * wheelSpeed, Input.mousePosition);
            ClampToViewport();
        }

        // Mouse drag (PC)
        if (Input.GetMouseButtonDown(0))
        {
            _userInteracted = true;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport, Input.mousePosition, UICamera, out _prevMouseLocal);
            _draggingMouse = true;
        }
        else if (Input.GetMouseButton(0) && _draggingMouse)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport, Input.mousePosition, UICamera, out var curr))
            {
                target.anchoredPosition += (curr - _prevMouseLocal) * dragSpeed;
                _prevMouseLocal = curr;
                ClampToViewport();
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _draggingMouse = false;
        }
#endif

        // Pinch 2 jari (HP)
        if (Input.touchCount == 2)
        {
            _userInteracted = true;
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            Vector2 prev0 = t0.position - t0.deltaPosition;
            Vector2 prev1 = t1.position - t1.deltaPosition;

            float prevMag = (prev0 - prev1).magnitude;
            float currMag = (t0.position - t1.position).magnitude;
            float deltaMag = currMag - prevMag;

            Vector2 center = (t0.position + t1.position) * 0.5f;
            ApplyZoom(deltaMag * pinchSpeed, center);
            ClampToViewport();
        }
        // Pan 1 jari + double tap (HP)
        else if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);

            // Double tap → toggle Fit <-> zoom cepat
            if (t.phase == TouchPhase.Ended && t.tapCount == 2)
            {
                _userInteracted = true;
                float s = target.localScale.x;
                float targetScale = (s <= _fitScale * doubleTapResetThreshold)
                    ? Mathf.Clamp(_fitScale * doubleTapZoom, minScale, maxScale)
                    : _fitScale;

                ApplyZoom(targetScale, t.position, absolute: true);
                ClampToViewport();
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, t.position - t.deltaPosition, UICamera, out var prevLocal) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, t.position, UICamera, out var currLocal))
            {
                _userInteracted = true;
                target.anchoredPosition += (currLocal - prevLocal) * dragSpeed;
                ClampToViewport();
            }
        }
    }

    /// Zoom terhadap titik kursor/jari.
    /// absolute=false → delta skala relatif; absolute=true → set skala langsung.
    void ApplyZoom(float delta, Vector2 screenPoint, bool absolute = false)
    {
        float oldScale = target.localScale.x;
        float wantedScale = absolute
            ? Mathf.Clamp(delta, minScale, maxScale)
            : Mathf.Clamp(oldScale + delta, minScale, maxScale);

        // Auto reset ke Fit jika mendekati Fit
        if (autoResetOnZoomOut && wantedScale <= _fitScale * resetWhenUnderFitMul)
        {
            target.localScale = new Vector3(_fitScale, _fitScale, 1f);
            target.anchoredPosition = Vector2.zero;
            return;
        }

        if (Mathf.Approximately(oldScale, wantedScale)) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(target, screenPoint, UICamera, out var before);
        target.localScale = new Vector3(wantedScale, wantedScale, 1f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(target, screenPoint, UICamera, out var after);

        Vector2 localDelta = after - before;
        target.anchoredPosition += localDelta;
        ClampToViewport();
    }

    // Jaga supaya gambar tidak keluar viewport
    void ClampToViewport()
    {
        if (!viewport || !target) return;

        var b = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
        var vr = viewport.rect;
        Vector3 delta = Vector3.zero;

        // X
        if (b.size.x <= vr.width)
        {
            if (centerIfSmaller) delta.x = (vr.center.x - b.center.x);
        }
        else
        {
            if (b.min.x > vr.xMin) delta.x = vr.xMin - b.min.x;
            if (b.max.x < vr.xMax) delta.x = vr.xMax - b.max.x;
        }

        // Y
        if (b.size.y <= vr.height)
        {
            if (centerIfSmaller) delta.y = (vr.center.y - b.center.y);
        }
        else
        {
            if (b.min.y > vr.yMin) delta.y = vr.yMin - b.min.y;
            if (b.max.y < vr.yMax) delta.y = vr.yMax - b.max.y;
        }

        if (delta.sqrMagnitude > 0f)
        {
            var worldDelta = viewport.TransformVector(delta);
            var localDelta = target.parent.InverseTransformVector(worldDelta);
            target.localPosition += localDelta;
        }
    }

    // Dipanggil Unity saat ukuran RT berubah (rotasi/resolusi/safe area)
    void OnRectTransformDimensionsChange()
    {
        if (!_initDone || !isActiveAndEnabled) return;

        // KUNCI: setelah init, jangan pernah AutoFit lagi (supaya tidak "tiba-tiba zoom")
        if (lockScaleAndPositionAfterInit)
        {
            ClampToViewport(); // hanya rapikan posisi
            return;
        }

        // Kalau mau support refit pas resize, bisa panggil AutoFit() di sini (non default)
        // AutoFit();
        // ClampToViewport();
    }

    static bool ApproximatelyRect(Rect a, Rect b)
    {
        return Mathf.Approximately(a.x, b.x) &&
               Mathf.Approximately(a.y, b.y) &&
               Mathf.Approximately(a.width, b.width) &&
               Mathf.Approximately(a.height, b.height);
    }
}
