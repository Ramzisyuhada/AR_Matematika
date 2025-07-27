using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AngketManager : MonoBehaviour
{
    public Angket angket;
    [Header("UI")]
    [SerializeField] TMP_Text Pertanyaan;
    [SerializeField] TMP_Text A;
    [SerializeField] TMP_Text B;
    [SerializeField] TMP_Text C;
    [SerializeField] TMP_Text Nomer;
    [SerializeField] private Button buttonA;
    [SerializeField] private Button buttonB;
    [SerializeField] private Button buttonC;
    [SerializeField] private GameObject Sumbit;
    [SerializeField] private GameObject Next;

    int IndexAngket = 0;

    private Button tombolTerakhirDipilih;

    private void SetAllText()
    {
        if (IndexAngket >= 0 && IndexAngket < angket.PertanyaanList.Count)
        {
            var p = angket.PertanyaanList[IndexAngket];

            Nomer.text = p.Id.ToString(); // Gunakan nomor urut
            Pertanyaan.text = p.teksPertanyaan;
            A.text = p.opsiA;
            B.text = p.opsiB;
            C.text = p.opsiC;
        }

    }

    public void NextAngket()
    {
        Debug.Log(angket.PertanyaanList.Count);
        Debug.Log(IndexAngket);

        if (IndexAngket < angket.PertanyaanList.Count - 1)
        {
            IndexAngket++;
            SetAllText();
            ColorButton();
          

        }
        if (IndexAngket == angket.PertanyaanList.Count - 1)
        {
            Sumbit.SetActive(true);
            Next.SetActive(false);
        }
    }

    public void PrevAngket()
    {
        if (IndexAngket > 0)
        {
            IndexAngket--;
            SetAllText();
            ColorButton();

            Sumbit.SetActive(false);
            Next.SetActive(true);
        }
    }


    void Start()
    {
        SetAllText();

    }
    public void SetJawaban(string Jawaban)
    {
        if (tombolTerakhirDipilih != null)
        {
            tombolTerakhirDipilih.GetComponent<RawImage>().color = Color.white;
        }

        Button tombolSekarang = EventSystem.current.currentSelectedGameObject.GetComponent<Button>();
        tombolTerakhirDipilih = tombolSekarang;

        tombolSekarang.GetComponent<RawImage>().color = Color.green;

        // Simpan jawaban
        angket.PertanyaanList[IndexAngket].jawabanTerpilih = Jawaban;
    }


    private void ColorButton()
    {
        buttonA.GetComponent<RawImage>().color = Color.white;
        buttonB.GetComponent<RawImage>().color = Color.white;
        buttonC.GetComponent<RawImage>().color = Color.white;

        string jawaban = angket.PertanyaanList[IndexAngket].jawabanTerpilih;

        switch (jawaban)
        {
            case "A":
                buttonA.GetComponent<RawImage>().color = Color.green;
                tombolTerakhirDipilih = buttonA;
                break;
            case "B":
                buttonB.GetComponent<RawImage>().color = Color.green;
                tombolTerakhirDipilih = buttonB;
                break;
            case "C":
                buttonC.GetComponent<RawImage>().color = Color.green;
                tombolTerakhirDipilih = buttonC;
                break;
        }
    }
    void Update()
    {
        
    }



}
