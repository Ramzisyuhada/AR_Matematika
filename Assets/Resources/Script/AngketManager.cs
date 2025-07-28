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

    [SerializeField] private GameObject HasilBackground;
    [SerializeField] private GameObject Sumbit;
    [SerializeField] private GameObject Next;
    [SerializeField] private TMP_Dropdown[] dropdowns;
    private string[] semuaOpsi = { "1", "2", "3" };
    private string[] jawabanDipilih = new string[3];
    string[,] kunciJawaban = new string[30, 3]
    {
        {"Visual", "Auditori", "Kinestetik"},
        {"Auditori", "Kinestetik", "Visual"},
        {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"},
        {"Auditori", "Kinestetik", "Visual"},
        {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"},
        {"Auditori", "Kinestetik", "Visual"},
        {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"},
        {"Auditori", "Kinestetik", "Visual"},
        {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"},
        {"Auditori", "Kinestetik", "Visual"},
        {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"},
        {"Auditori", "Kinestetik", "Visual"},
        {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"},
        {"Auditori", "Kinestetik", "Visual"},
        {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"},
        {"Auditori", "Kinestetik", "Visual"},
        {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"},
        {"Auditori", "Kinestetik", "Visual"},
        {"Kinestetik", "Visual", "Auditori"},
        {"Visual", "Auditori", "Kinestetik"},
        {"Auditori", "Kinestetik", "Visual"},
        {"Kinestetik", "Visual", "Auditori"},
    };


    int IndexAngket = 0;

    private bool isUpdatingDropdowns = false;

    private void Start()
    {
        jawabanDipilih = new string[dropdowns.Length];

        for (int i = 0; i < dropdowns.Length; i++)
        {
            int index = i;
            dropdowns[i].ClearOptions();
            List<string> opsiAwal = new List<string> { "" };
            opsiAwal.AddRange(semuaOpsi);
            dropdowns[i].AddOptions(opsiAwal);
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
                // Update pilihan dropdown
                UpdateDropdownOptions(i);

                // Ambil jawaban yang sudah dipilih sebelumnya
                string jawaban = p.OpsiPertanyaan[i].jawabanTerpilih;

                // Cek apakah sudah ada jawaban sebelumnya
                if (!string.IsNullOrEmpty(jawaban))
                {
                    jawabanDipilih[i] = jawaban;

                    // Temukan index dari jawaban dalam opsi dropdown
                    int optionIndex = dropdowns[i].options.FindIndex(opt => opt.text == jawaban);
                    if (optionIndex >= 0)
                    {
                        dropdowns[i].value = optionIndex;
                    }
                    else
                    {
                        dropdowns[i].value = 0;
                        jawabanDipilih[i] = null;
                    }
                }
                else
                {
                    // Belum ada jawaban, reset ke default (kosong)
                    dropdowns[i].value = 0;
                    jawabanDipilih[i] = null;
                }
            }
        }
    }



    public void NextAngket()
    {
        if (IndexAngket < angket.PertanyaanList.Count - 1)
        {
            SimpanJawabanSaatIni();

            IndexAngket++;
            SetAllText();
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
            SimpanJawabanSaatIni();

            IndexAngket--;
            SetAllText();
            Sumbit.SetActive(false);
            Next.SetActive(true);
        }
    }

    void OnDropdownChanged(int changedIndex)
    {
        if (isUpdatingDropdowns) return;

        isUpdatingDropdowns = true;

        TMP_Dropdown changedDropdown = dropdowns[changedIndex];

        if (changedDropdown.value <= 0 || changedDropdown.value >= changedDropdown.options.Count)
        {
            jawabanDipilih[changedIndex] = null;
        }
        else
        {
            string dipilih = changedDropdown.options[changedDropdown.value].text;

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

        // Perbarui semua opsi dropdown
        for (int i = 0; i < dropdowns.Length; i++)
        {
            UpdateDropdownOptions(i);
        }

        isUpdatingDropdowns = false;
        CekValidasiSemuaDropdown();

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
            {
                opsiBaru.Add(opsi);
            }
        }

        // Tambahkan opsi kosong di awal
        List<string> opsiFinal = new List<string> { "" };
        opsiFinal.AddRange(opsiBaru);

        dd.ClearOptions();
        dd.AddOptions(opsiFinal);

        // Tentukan nilai dropdown
        if (jawabanSaatIni != null && opsiBaru.Contains(jawabanSaatIni))
        {
            dd.value = opsiFinal.IndexOf(jawabanSaatIni);
        }
        else
        {
            dd.value = 0;
            jawabanDipilih[dropdownIndex] = null;
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
        {
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maxWidth + padding);
        }
    }


    void SimpanJawabanSaatIni()
    {
        var p = angket.PertanyaanList[IndexAngket];

        for (int i = 0; i < p.OpsiPertanyaan.Count; i++)
        {
            if (i < jawabanDipilih.Length)
            {
                string pilihan = jawabanDipilih[i];

                // Simpan jawaban, termasuk pengosongan
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
        int visual = 0;
        int auditori = 0;
        int kinestetik = 0;

        for (int i = 0; i < angket.PertanyaanList.Count; i++)
        {
            var pertanyaan = angket.PertanyaanList[i];

            for (int j = 0; j < pertanyaan.OpsiPertanyaan.Count; j++)
            {
                string jawaban = pertanyaan.OpsiPertanyaan[j].jawabanTerpilih;

                if (!string.IsNullOrEmpty(jawaban))
                {
                    int skor = int.Parse(jawaban); // Nilai 1, 2, atau 3
                    string kategori = kunciJawaban[i, j];

                    if (kategori == "Visual") visual += skor;
                    else if (kategori == "Auditori") auditori += skor;
                    else if (kategori == "Kinestetik") kinestetik += skor;
                }
            }
        }

        string hasil = GetKesimpulanGayaBelajar(visual, auditori, kinestetik);
        HasilText.text = "Kesimpulan Gaya Belajar: " + hasil;
        Debug.Log($"Visual: {visual}, Auditori: {auditori}, Kinestetik: {kinestetik}");
        Debug.Log("Kesimpulan Gaya Belajar: " + hasil);
    }



    string GetKesimpulanGayaBelajar(int visual, int auditori, int kinestetik)
    {
        List<string> dominan = new List<string>();
        int max = Mathf.Max(visual, auditori, kinestetik);

        if (visual == max) dominan.Add("Visual");
        if (auditori == max) dominan.Add("Auditori");
        if (kinestetik == max) dominan.Add("Kinestetik");

        if (dominan.Count == 1)
            return $"Gaya belajar Anda dominan adalah {dominan[0]}.";
        else
            return $"Gaya belajar Anda bersifat campuran: {string.Join(" - ", dominan)}.";
    }





}
