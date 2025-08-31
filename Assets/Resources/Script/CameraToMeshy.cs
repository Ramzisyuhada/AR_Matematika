using UnityEngine;
using System;
using System.IO;

public class CameraToMeshy : MonoBehaviour
{
    public MeshyImageTo3D_RuntimeOnlyV2 meshyRunner; // drag komponen Meshy ke sini di Inspector

    private WebCamTexture webcamTex;

    void Start()
    {
        // Buka kamera default
        webcamTex = new WebCamTexture();
        Renderer r = GetComponent<Renderer>();
        if (r != null) r.material.mainTexture = webcamTex; // tampilkan preview di objek
        webcamTex.Play();
    }

    [ContextMenu("Capture & Run Meshy")]
    public void CaptureAndRun()
    {
        if (webcamTex == null || !webcamTex.isPlaying)
        {
            Debug.LogError("Kamera belum aktif");
            return;
        }

        // Buat Texture2D dari frame kamera
        Texture2D snap = new Texture2D(webcamTex.width, webcamTex.height, TextureFormat.RGB24, false);
        snap.SetPixels(webcamTex.GetPixels());
        snap.Apply();

        // Encode ke PNG → Base64
        byte[] pngBytes = snap.EncodeToPNG();
        string base64 = Convert.ToBase64String(pngBytes);

        // Buat Data URI
        string dataUri = $"data:image/png;base64,{base64}";

        // Pasang ke runner
        meshyRunner.imageUrl = dataUri;

        Debug.Log("[CameraToMeshy] Capture berhasil, kirim ke Meshy...");

        // Jalankan proses Meshy
        meshyRunner.Run();
    }
}