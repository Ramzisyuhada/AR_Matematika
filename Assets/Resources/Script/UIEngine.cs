using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIEngine : MonoBehaviour
{
    public void Materi()
    {
        SceneManager.LoadScene("Halaman Pilihan Materi");
    }

    public void Mulai()
    {
        SceneManager.LoadScene("Home Screen");
    }

    public void BangunDatar()
    {
        SceneManager.LoadScene("Halaman Materi ruang sisi datar");
    }
    public void PertanyaanSimantik()
    {

        SceneManager.LoadScene("ARBalok");

    }
    
    private void Awake()
    {
        Screen.orientation = ScreenOrientation.Portrait;

    }
}
