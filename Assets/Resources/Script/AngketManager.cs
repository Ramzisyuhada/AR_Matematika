using System;
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
    [SerializeField] TMP_Text HasilText;
    [SerializeField] TMP_Text HasilText2;

    [SerializeField] RawImage IconHasil;
    [SerializeField] private RawImage[] iconImage;

    [Header("Game Object")]
    [SerializeField] private GameObject ListTutorial;
    [SerializeField] private GameObject ListJawaban;
    [SerializeField] private GameObject PembukaanList;
    [SerializeField] private GameObject Header;
    [SerializeField] private GameObject PrevObject;
    [SerializeField] private GameObject NextObject;
    [SerializeField] private GameObject MulaiObject;
    [SerializeField] private GameObject HasilButton;


    [SerializeField] private GameObject HasilBackground;
    [SerializeField] private GameObject Sumbit;
    [SerializeField] private GameObject Next;
    [SerializeField] private TMP_Dropdown[] dropdowns;

    [Header("Icon Opsi Dropdown")]
    [SerializeField] private Sprite[] opsiIcons; // 3 gambar sesuai urutan "1", "2", "3"


    private string Akhir = "";
    private bool MulaiAngket;
    private string[] semuaOpsi = { "1", "2", "3" };
    private string[] jawabanDipilih = new string[3];

    string[,] kunciJawaban = new string[30, 3]
    {
        {"Visual", "Auditori", "Kinestetik"}, {"Auditori", "Kinestetik", "Visual"}, {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"}, {"Auditori", "Kinestetik", "Visual"}, {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"}, {"Auditori", "Kinestetik", "Visual"}, {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"}, {"Auditori", "Kinestetik", "Visual"}, {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"}, {"Auditori", "Kinestetik", "Visual"}, {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"}, {"Auditori", "Kinestetik", "Visual"}, {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"}, {"Auditori", "Kinestetik", "Visual"}, {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"}, {"Auditori", "Kinestetik", "Visual"}, {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"}, {"Auditori", "Kinestetik", "Visual"}, {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"}, {"Auditori", "Kinestetik", "Visual"}, {"Kinestetik", "Visual", "Auditori"},
    };

    int IndexAngket = 0;
    private bool isUpdatingDropdowns = false;
    int UrutanHalaman = 0;

    private void Start()
    {
        jawabanDipilih = new string[dropdowns.Length];
        UrutanListHalaman();

        for (int i = 0; i < dropdowns.Length; i++)
        {
            int index = i;
            dropdowns[i].ClearOptions();
            dropdowns[i].value = 0;
            jawabanDipilih[i] = null;
            dropdowns[i].onValueChanged.AddListener((_) => OnDropdownChanged(index));
        }

        SetAllText();
    }

    private void SetAllText()
    {
        if (IndexAngket >= 0 && IndexAngket < angket.PertanyaanList.Count)
        {
            var p = angket.PertanyaanList[IndexAngket];

            Nomer.text = p.Id.ToString();
            Pertanyaan.text = p.teksPertanyaan;
            A.text = p.OpsiPertanyaan[0].teksPertanyaan;
            B.text = p.OpsiPertanyaan[1].teksPertanyaan;
            C.text = p.OpsiPertanyaan[2].teksPertanyaan;

            for (int i = 0; i < dropdowns.Length; i++)
            {
                UpdateDropdownOptions(i);

                string jawaban = p.OpsiPertanyaan[i].jawabanTerpilih;

                if (!string.IsNullOrEmpty(jawaban))
                {
                    jawabanDipilih[i] = jawaban;

                    int optionIndex = dropdowns[i].options.FindIndex(opt => opt.text == jawaban);
                    dropdowns[i].value = optionIndex >= 0 ? optionIndex : 0;

                    OnDropdownChanged(i); // ✅ tambahkan ini

                }
                else
                {
                    dropdowns[i].value = 0;
                    jawabanDipilih[i] = null;
                        OnDropdownChanged(i); // ✅ tambahkan ini juga

                }
            }
        }
    }
    public void StartAngket()
    {
        MulaiAngket = true;
        PrevObject.SetActive(true);
        NextObject.SetActive(true);
        MulaiObject.SetActive(false);
        IndexAngket = 0;
        UrutanHalaman = 2;
        UrutanListHalaman();
    }
    public void NextAngket()
    {
        if (IndexAngket < angket.PertanyaanList.Count - 1)
        {
            if (!MulaiAngket)
            {
                UrutanHalaman++;
                UrutanListHalaman();

                if (UrutanHalaman == 1)
                {
                    MulaiAngket = true;
                    PrevObject.SetActive(false);
                    NextObject.SetActive(false);
                    MulaiObject.SetActive(true);
                }

                return;
            }

            SimpanJawabanSaatIni();
            IndexAngket++;
            SetAllText();
        }

        Sumbit.SetActive(IndexAngket == angket.PertanyaanList.Count - 1);
        Next.SetActive(IndexAngket < angket.PertanyaanList.Count - 1);
    }

    public void PrevAngket()
    {
        if (IndexAngket > 0)
        {
            SimpanJawabanSaatIni();
            IndexAngket--;
            SetAllText();

            Sumbit.SetActive(false);
            Next.SetActive(true);
        }
        else if (!MulaiAngket && UrutanHalaman > 0)
        {
            UrutanHalaman--;
            UrutanListHalaman();
        }
    }
    void NampilIcon()
    {

    }
    void OnDropdownChanged(int changedIndex)
    {

        if (isUpdatingDropdowns) return;
        isUpdatingDropdowns = true;

        TMP_Dropdown changedDropdown = dropdowns[changedIndex];
        if (changedDropdown.value <= 0 || changedDropdown.value >= changedDropdown.options.Count)
        {
            dropdowns[changedIndex].GetComponentInChildren<RawImage>().enabled = true;
            jawabanDipilih[changedIndex] = null;
        }
        else
        {
            dropdowns[changedIndex].GetComponentInChildren<RawImage>().enabled = false;

            string dipilih = changedDropdown.options[changedDropdown.value].text;
            // rawImage = dropdowns[changedIndex].GetComponentInChildren<RawImage>(true);
         
            for (int i = 0; i < jawabanDipilih.Length; i++)
            {
                if (i != changedIndex && jawabanDipilih[i] == dipilih)
                {
                    jawabanDipilih[i] = null;
                    dropdowns[i].value = 0;
                }
            }

            jawabanDipilih[changedIndex] = dipilih;

        }

        for (int i = 0; i < dropdowns.Length; i++)
        {
            UpdateDropdownOptions(i);
          

        }

        isUpdatingDropdowns = false;
        if(MulaiAngket)CekValidasiSemuaDropdown();
    }

    void UrutanListHalaman()
    {
        switch (UrutanHalaman)
        {
            case 0: 
                PembukaanList.SetActive(true); 
                Header.SetActive(false);
                ListTutorial.SetActive(false);
                ListJawaban.SetActive(false);
                break;
            case 1:
                PembukaanList.SetActive(false);
                Header.SetActive(true);
                ListTutorial.SetActive(true);
                ListJawaban.SetActive(false);
                break;
            case 2:
                PembukaanList.SetActive(false);
                Header.SetActive(true);
                ListTutorial.SetActive(false);
                ListJawaban.SetActive(true);
                break;
        }
    }
    void UpdateDropdownOptions(int dropdownIndex)
    {
        TMP_Dropdown dd = dropdowns[dropdownIndex];
        string jawabanSaatIni = jawabanDipilih[dropdownIndex];

        List<string> opsiBaru = new List<string>();

        foreach (string opsi in semuaOpsi)
        {
            bool sudahDipakai = false;
            for (int i = 0; i < jawabanDipilih.Length; i++)
            {
                if (i != dropdownIndex && jawabanDipilih[i] == opsi)
                {
                    sudahDipakai = true;
                    break;
                }
            }

            if (!sudahDipakai || jawabanSaatIni == opsi)
                opsiBaru.Add(opsi);
        }

        List<TMP_Dropdown.OptionData> optionDatas = new List<TMP_Dropdown.OptionData>();
        optionDatas.Add(new TMP_Dropdown.OptionData("-")); // Kosong di awal

        foreach (string opsi in opsiBaru)
        {
            int index = System.Array.IndexOf(semuaOpsi, opsi);
            Sprite icon = (index >= 0 && index < opsiIcons.Length) ? opsiIcons[index] : null;
            optionDatas.Add(new TMP_Dropdown.OptionData(opsi, icon));
        }

        dd.ClearOptions();
        dd.AddOptions(optionDatas);

        if (!string.IsNullOrEmpty(jawabanSaatIni))
        {
            int valIndex = optionDatas.FindIndex(o => o.text == jawabanSaatIni);
            dd.value = valIndex >= 0 ? valIndex : 0;
        }
        else
        {
            dd.value = 0;
        }

        ResizeDropdownWidth(dd);
    }

    void ResizeDropdownWidth(TMP_Dropdown dropdown)
    {
        float maxWidth = 100f;
        float padding = 40f;

        TMP_Text tmpText = dropdown.captionText;
        if (tmpText == null) return;

        foreach (var option in dropdown.options)
        {
            Vector2 size = tmpText.GetPreferredValues(option.text);
            if (size.x > maxWidth)
                maxWidth = size.x;
        }

        RectTransform rt = dropdown.GetComponent<RectTransform>();
        if (rt != null)
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maxWidth + padding);
    }

    void SimpanJawabanSaatIni()
    {
        var p = angket.PertanyaanList[IndexAngket];

        for (int i = 0; i < p.OpsiPertanyaan.Count; i++)
        {
            if (i < jawabanDipilih.Length)
            {
                Debug.Log("Pertanyaan : " + angket.PertanyaanList[IndexAngket].teksPertanyaan);
                string pilihan = jawabanDipilih[i];
                Debug.Log("Jawaban nya adalah : " + pilihan);

                p.OpsiPertanyaan[i].jawabanTerpilih = string.IsNullOrEmpty(pilihan) ? null : pilihan;
            }
        }
    }

    void CekValidasiSemuaDropdown()
    {
        bool semuaTerisi = true;
        foreach (var jawaban in jawabanDipilih)
        {
            if (string.IsNullOrEmpty(jawaban))
            {
                semuaTerisi = false;
                break;
            }
        }

        Next.GetComponent<Button>().interactable = semuaTerisi;
        Sumbit.GetComponent<Button>().interactable = semuaTerisi;
    }

    public void SubmitAngket()
    {
        SimpanJawabanSaatIni();
        HasilBackground.SetActive(true);
        Header.SetActive(false);
        ListJawaban.SetActive(false);
        Sumbit.SetActive(false);
        PrevObject.SetActive(false);
        HasilButton.SetActive(true);

        int visual = 0, auditori = 0, kinestetik = 0;

        for (int i = 0; i < angket.PertanyaanList.Count; i++)
        {
            var pertanyaan = angket.PertanyaanList[i];
            for (int j = 0; j < pertanyaan.OpsiPertanyaan.Count; j++)
            {
                string jawaban = pertanyaan.OpsiPertanyaan[j].jawabanTerpilih;
                if (!string.IsNullOrEmpty(jawaban))
                {
                    int skor = int.Parse(jawaban);
                    string kategori = kunciJawaban[i, j];
                    if (kategori == "Visual") visual += skor;
                    else if (kategori == "Auditori") auditori += skor;
                    else if (kategori == "Kinestetik") kinestetik += skor;
                }
            }
        }

        string hasil = GetKesimpulanGayaBelajar(visual, auditori, kinestetik);
        HasilText.text = hasil+ " " + Akhir;
        Debug.Log($"Visual: {visual}, Auditori: {auditori}, Kinestetik: {kinestetik}");
        Debug.Log("Kesimpulan Gaya Belajar: " + hasil);
    }
    
    string GetKesimpulanGayaBelajar(int visual, int auditori, int kinestetik)
    {
        List<string> dominan = new List<string>();
        int max = Mathf.Max(visual, auditori, kinestetik);
        if (visual == max)
        {
            dominan.Add("Visual");
            Akhir = "yang efektif dengan melihat gambar, warna, diagram, atau video.";
        }
        if(auditori == max){
                dominan.Add("Auditori");
            Akhir = "yang efektif dengan mendengarkan dan berdiskusi.";
            }
        if (kinestetik == max)
        {
            dominan.Add("Kinestetik");
            Akhir = "yang efektif dengan bergerak, menyentuh, dan praktik langsung.";
        }

        
        HasilText2.text = dominan[0];
        return dominan.Count == 1 ?
            $"Gaya belajar Anda dominan adalah {dominan[0]}." :
            $"Gaya belajar Anda bersifat campuran: {string.Join(" - ", dominan)}.";
    }
}
