using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SoalManager : MonoBehaviour
{
    [Header("Soal")]
    [SerializeField] Soal[] Soal;

    [Header("UI")]
    [SerializeField] TMP_Text TextPertanyaan;
    [SerializeField] RawImage NoSoal;
    [SerializeField] GameObject Tabel;
    [SerializeField] GameObject Pertanyaan;

    int IndexSoal = 0;
    public GameObject ButtomSheet;
    Vector3 ukuranAwal;
    public GameObject targetObjek;

    private void Start()
    {
        float inchToMeter = 0.0254f;
        ukuranAwal = new Vector3(8f, 0.5f, 2f) * inchToMeter;

        transform.localScale = ukuranAwal;

        NoSoal.texture = Soal[IndexSoal].NoSoal.texture;
        TextPertanyaan.text = Soal[IndexSoal].Pertanyaan;
        Tabel.SetActive(false);

        //NextSoal();
    }
    public void NextSoal()
    {
        if(IndexSoal < 2) IndexSoal++;

        if (IndexSoal == 2)
        {
            IndexSoal++;
            TextPertanyaan.text = Soal[IndexSoal].Pertanyaan;

            Tabel.SetActive(true);
            Pertanyaan.transform.SetSiblingIndex(1);
            NoSoal.gameObject.SetActive(false);
            return;
        }
        if(Soal[IndexSoal].NoSoal.texture != null) NoSoal.texture = Soal[IndexSoal].NoSoal.texture;
        TextPertanyaan.text = Soal[IndexSoal].Pertanyaan;
    }

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
        ButtomSheet.SetActive(false);
        Vector3 target = new Vector3(
            ukuranAwal.x * (1 + persenPanjang / 100f),
            ukuranAwal.y * (1 + persenTinggi / 100f),
            ukuranAwal.z * (1 + persenLebar / 100f)
        );

        LeanTween.scale(targetObjek, target, 1.2f).setEase(LeanTweenType.easeInOutSine);
    }
}
