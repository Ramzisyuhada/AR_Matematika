using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class PertanyaanController : MonoBehaviour
{

    [Header("Pertanyaan Dan Jawaban")]
    public string[] Pertanyaan;
    private string[] Jawaban;
    private int Index = 0;
    [Header("UI Pertanyaan Dan Jawaban")]

    public TMP_InputField inputField;
    public TMP_Text UIPertanyaan;
    public GameObject tabel;

    Vector3 ukuranAwal;
    private float inchToMeter = 0.0254f;
    public GameObject targetObjek;

   
    public void JalankanSkenario1()
    {
        JalankanSkenario(-20, +25, -20);
    }

    public void JalankanSkenario2()
    {
        JalankanSkenario(-50, +20, +50);
    }

    public void JalankanSkenario3()
    {
        JalankanSkenario(-50, +100, -20);
    }

    public void JalankanSkenario4()
    {
        JalankanSkenario(-36, +25, 0);
    }

    private void JalankanSkenario(float persenPanjang, float persenTinggi, float persenLebar)
    {
        Vector3 target = new Vector3(
            ukuranAwal.x * (1 + persenPanjang / 100f),
            ukuranAwal.y * (1 + persenTinggi / 100f),
            ukuranAwal.z * (1 + persenLebar / 100f)
        );

        LeanTween.scale(targetObjek, target, 1.2f).setEase(LeanTweenType.easeInOutSine);
    }

    public void Jawab()
    {
        string teks = inputField.text;

        if (Index < Pertanyaan.Length)
        {
            // Simpan jawaban
            Jawaban[Index] = teks;
            Index++;

            // Kosongkan input field
            inputField.text = "";

            if (Index < Pertanyaan.Length)
            {
                UIPertanyaan.text = Pertanyaan[Index];
                tabel.SetActive(false);
            }
            else
            {
                // Semua pertanyaan sudah dijawab
                UIPertanyaan.text = "Terima kasih sudah menjawab!";
                tabel.SetActive(true);
                inputField.interactable = false;
            }
        }
    }
    void Start()
    {
        float inchToMeter = 0.0254f;
        ukuranAwal = new Vector3(8f, 0.5f, 2f) * inchToMeter;

        transform.localScale = ukuranAwal;

        Jawaban = new string[Pertanyaan.Length];

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
