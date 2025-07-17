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
    public void Latihan()
    {
        SceneManager.LoadScene("Halaman Pilihan Pertanyaan");
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
    public void LatihanSoalDatar()
    {

        SceneManager.LoadScene("Latihan");

    }
    private void Awake()
    {
        Screen.orientation = ScreenOrientation.Portrait;

    }
}
