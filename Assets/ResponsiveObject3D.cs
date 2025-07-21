using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ResponsiveObject3D : MonoBehaviour
{
    public float w = 1.0f;
    public float h = 0.5f;

    private void Awake()
    {
      //  Input.gyro.enabled = false;

    }
    public void PrefScane()
    {
        SceneManager.LoadScene("Home Screen");
    }
    void Start()
    {
        // Posisi di tengah layar (viewport 0.5, 0.5)
        Vector3 viewportPos = new Vector3(w, h, transform.position.z);

        // Konversi ke world space
        Vector3 worldPos = Camera.main.ViewportToWorldPoint(viewportPos);

        // Tempatkan objek
        transform.position = worldPos;
    }

    private void Update()
    {
        Vector3 viewportPos = new Vector3(w, h, transform.position.z);

        // Konversi ke world space
        Vector3 worldPos = Camera.main.ViewportToWorldPoint(viewportPos);

        // Tempatkan objek
        transform.position = worldPos;
    }

    void LateUpdate()
    {
   
    }
}
