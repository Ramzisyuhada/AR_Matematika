using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResponsiveObject3D : MonoBehaviour
{
    public float zDistance = 8.019809f; // seberapa jauh dari kamera (dalam meter)
    public Vector2 viewportAnchor = new Vector2(3f, -1.35f); // pojok kanan atas

    private Camera arCamera;

    void Start()
    {
        arCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (arCamera == null) return;

        Vector3 viewportPos = new Vector3(viewportAnchor.x, viewportAnchor.y, zDistance);
        Vector3 worldPos = arCamera.ViewportToWorldPoint(viewportPos);
        transform.position = worldPos;

        // Opsional: selalu menghadap ke kamera
        transform.LookAt(arCamera.transform);
    }
}
