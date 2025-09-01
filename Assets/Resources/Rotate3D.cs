using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotate3D : MonoBehaviour
{
    public float rotateSpeed = 50f; // derajat per klik

    // Fungsi ini dipanggil dari Button OnClick
    public void RotateRight()
    {
        transform.Rotate(Vector3.up * rotateSpeed, Space.World);
    }

    public void RotateLeft()
    {
        transform.Rotate(Vector3.down * rotateSpeed, Space.World);
    }

    public void RotateUp()
    {
        transform.Rotate(Vector3.right * rotateSpeed, Space.World);
    }

    public void RotateDown()
    {
        transform.Rotate(Vector3.left * rotateSpeed, Space.World);
    }
}
