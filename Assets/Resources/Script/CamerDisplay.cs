using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Vuforia;

public class VuforiaToRawImage : MonoBehaviour
{
    [Header("UI Target")]
    [Tooltip("RawImage (UI) yang akan menampilkan feed kamera Vuforia")]
    public RawImage targetRawImage;   // drag RawImage kotak putih

    [Header("Behaviour")]
    [Tooltip("Berapa detik maksimal menunggu texture kamera siap")]
    public float waitTimeoutSeconds = 10f;

    private Texture vuforiaTex;
    private bool subscribed;

    private void OnEnable()
    {
        if (!targetRawImage)
        {
            Debug.LogError("[VuforiaToRawImage] targetRawImage belum di-assign.");
            enabled = false;
            return;
        }

        targetRawImage.texture = null;
        StartCoroutine(BindWhenReady());
    }

    private void OnDisable()
    {
        // Lepas event bila ada
        var vb = VuforiaBehaviour.Instance;
        if (vb != null && vb.VideoBackground != null && subscribed)
        {
            vb.VideoBackground.OnVideoBackgroundChanged -= OnVideoBackgroundChanged;
            subscribed = false;
        }
        // Jangan sentuh kamera (Vuforia 10.x mengelola sendiri)
        vuforiaTex = null;
        if (targetRawImage) targetRawImage.texture = null;
    }

    private IEnumerator BindWhenReady()
    {
        // 1) Tunggu ARCamera (VuforiaBehaviour) muncul
        float t = 0f;
        while (VuforiaBehaviour.Instance == null && t < waitTimeoutSeconds)
        {
            t += Time.deltaTime;
            yield return null;
        }
        if (VuforiaBehaviour.Instance == null)
        {
            Debug.LogError("[VuforiaToRawImage] VuforiaBehaviour tidak ditemukan di scene.");
            yield break;
        }

        // 2) Subscribe event perubahan video background (jika tersedia)
        var vb = VuforiaBehaviour.Instance;
        if (vb.VideoBackground != null && !subscribed)
        {
            vb.VideoBackground.OnVideoBackgroundChanged += OnVideoBackgroundChanged;
            subscribed = true;
        }

        // 3) Coba pasang texture sekarang (atau menunggu sampai ada)
        yield return StartCoroutine(TrySetTextureLoop());
    }

    private IEnumerator TrySetTextureLoop()
    {
        float t = 0f;
        while (t < waitTimeoutSeconds)
        {
            if (TrySetTextureOnce()) yield break; // sukses
            t += Time.deltaTime;
            yield return null;
        }
        Debug.LogWarning("[VuforiaToRawImage] Timeout: texture kamera belum tersedia.");
    }

    // Dipanggil saat VideoBackground berubah
    private void OnVideoBackgroundChanged()
    {
        TrySetTextureOnce();
    }

    // Mengembalikan true bila sukses memasang texture
    private bool TrySetTextureOnce()
    {
        var vb = VuforiaBehaviour.Instance;
        if (vb == null || vb.VideoBackground == null) return false;

        var tex = vb.VideoBackground.VideoBackgroundTexture;
        if (tex == null || tex.width <= 0 || tex.height <= 0) return false;

        if (vuforiaTex != tex)
        {
            vuforiaTex = tex;
            targetRawImage.texture = vuforiaTex;
            // Catatan: RawImage tidak punya preserveAspect; atur ukuran via layout/anchors.
            Debug.Log($"[VuforiaToRawImage] Set RawImage ke video {tex.width}x{tex.height}");
        }
        return true;
    }
}
