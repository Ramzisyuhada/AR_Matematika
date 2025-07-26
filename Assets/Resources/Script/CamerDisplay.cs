using Kamgam.MeshExtractor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#endif

public class CamerDisplay : MonoBehaviour
{

    public RawImage rawImage; 
    public Texture2D backgroundTexture; 
    private WebCamTexture webcamTexture;
    public void Mulai()
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }
        Destroy(gameObject);
        SceneManager.LoadScene("Home Screen");

    }

    void Start()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
            return;
        }
#endif
        if (SceneManager.GetActiveScene().name != "Home Screen")
        {
            StartCamera();
        }
        else
        {
            // Ganti ke background gambar statis
            if (backgroundTexture != null)
            {
                rawImage.texture = backgroundTexture;
                rawImage.material.mainTexture = backgroundTexture;
            }
        }
    }

    void OnDisable()
    {
        StopCamera();
    }

    void OnDestroy()
    {
        StopCamera();
    }

    void StopCamera()
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }

        // Ganti rawImage ke background statis
        if (backgroundTexture != null)
        {
            rawImage.texture = backgroundTexture;
            rawImage.material.mainTexture = backgroundTexture;
        }
    }

    void StartCamera()
    {
        WebCamDevice[] devices = WebCamTexture.devices;

        for (int i = 0; i < devices.Length; i++)
        {
            if (!devices[i].isFrontFacing)
            {
                webcamTexture = new WebCamTexture(devices[i].name);
                break;
            }
        }

        if (webcamTexture == null && devices.Length > 0)
        {
            webcamTexture = new WebCamTexture(devices[0].name);
        }

        if (webcamTexture != null)
        {
            webcamTexture.Play();
            rawImage.texture = webcamTexture;
            rawImage.material.mainTexture = webcamTexture;
            //SetRawImageStretchLeft();
        }
    }

    void SetRawImageStretchLeft()
    {
        RectTransform rect = rawImage.GetComponent<RectTransform>();

        // Anchor stretch vertikal di kiri
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);

        float panelWidth = 400f; 
        rect.sizeDelta = new Vector2(panelWidth, 0f); 
        rect.anchoredPosition = Vector2.zero;
    }

    void Update()
    {
        
    }
}
