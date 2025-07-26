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




    void LateUpdate()
    {
   
    }
}
