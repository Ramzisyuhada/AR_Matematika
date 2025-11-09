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
    [SerializeField] GameObject ButtonSumbit;
    [SerializeField] TMP_Text TextButton;
    [SerializeField] TMP_Text SoalText;

    [Header("Navigasi (opsional)")]
    [SerializeField] Button BtnPrev;   // drag kalau pakai tombol Prev
    [SerializeField] Button BtnNext;   // drag kalau pakai tombol Next

    [Header("Lainnya")]
    public GameObject RootJawaban;
    public GameObject UploadObkect;
    public GameObject CanvasPenjelasan;

    [Header("Validasi Jawaban")]
    [SerializeField] TMP_InputField InputJawaban;   // drag dari Inspector (input untuk soal teks)
    [SerializeField] bool requireAnswerPerSoal = true; // kalau false, lewati validasi
    [SerializeField] GameObject PopupKosong; // drag pop-up kamu ke sini

    public int IndexSoal = 0;
    public GameObject ButtomSheet;
    Vector3 ukuranAwal;
    public GameObject targetObjek;

    // simpan ukuran default untuk dikembalikan saat keluar dari index spesial
    float defaultRootJawabanHeight = -1f;

    private void Start()
    {
        float inchToMeter = 0.0254f;
        ukuranAwal = new Vector3(8f, 0.5f, 2f) * inchToMeter;
        transform.localScale = ukuranAwal;

        if (RootJawaban != null)
        {
            var rt = RootJawaban.GetComponent<RectTransform>();
            if (rt != null) defaultRootJawabanHeight = rt.rect.height;
        }

        // init pertama
        SetIndex(Mathf.Clamp(IndexSoal, 0, Soal != null && Soal.Length > 0 ? Soal.Length - 1 : 0));
    }

    // =========================
    // PUBLIC: Next & Prev
    // =========================
    public void NextSoal()
    {
        // MATIKAN overlay / sheet (supaya tombol tidak ketutupan)

        // === VALIDASI JAWABAN KOSONG ===
        if (InputJawaban != null)
        {
            if (string.IsNullOrWhiteSpace(InputJawaban.text))
            {
                // munculkan popup kalau ada
                if (PopupKosong != null)
                {
                    ShowPopupKosong();
                    //PopupKosong.SetActive(true);
                   
                }

                // efek goyang pada input
                LeanTween.cancel(InputJawaban.gameObject);
                Vector3 startPos = InputJawaban.transform.localPosition;
                LeanTween.moveLocalX(InputJawaban.gameObject, startPos.x + 10, 0.05f).setLoopPingPong(3);

                // JANGAN LANJUT KE SOAL BERIKUTNYA
                return;
            }
        }

        // Jika lolos validasi → lanjut
        SetIndex(IndexSoal + 1);

        // Kosongkan input untuk soal berikutnya (optional)
      
    }


    public void PrevSoal()
    {
        ButtomSheet.SetActive(true);
        CanvasPenjelasan.SetActive(true);
        UploadObkect.SetActive(false);
        SetIndex(IndexSoal - 1);
    }

    // =========================
    // INTI: Set index + Update UI
    // =========================
    private void SetIndex(int newIndex)
    {
        if (Soal == null || Soal.Length == 0) return;

        newIndex = Mathf.Clamp(newIndex, 0, Soal.Length - 1);
        IndexSoal = newIndex;

        // MATIKAN overlay yang bisa blok klik
     //   if (ButtomSheet != null) ButtomSheet.SetActive(false);

        // update UI dasar
        if (Soal[IndexSoal].NoSoal != null && Soal[IndexSoal].NoSoal.texture != null)
            NoSoal.texture = Soal[IndexSoal].NoSoal.texture;
        if (TextPertanyaan != null && !string.IsNullOrEmpty(Soal[IndexSoal].Pertanyaan))
            TextPertanyaan.text = Soal[IndexSoal].Pertanyaan;

        ApplyDefaultUIState();

        // aturan khusus index 2
        if (IndexSoal == 2)
        {
            if (RootJawaban != null)
            {
                RectTransform rt = RootJawaban.GetComponent<RectTransform>();
                if (rt != null) rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 180.4487f);
            }
            if (SoalText != null) SoalText.gameObject.SetActive(false);
         //   if (ButtonSumbit != null) ButtonSumbit.SetActive(true);
            if (Tabel != null)
            {
                Tabel.SetActive(true);
                Tabel.transform.SetSiblingIndex(1);
            }
        }

        // tombol navigasi → SELALU atur di sini
       if (BtnPrev != null) BtnPrev.interactable = IndexSoal > 0;
        //if (BtnNext != null) BtnNext.interactable = IndexSoal < Soal.Length - 1;
    }

    private void ShowPopupKosong()
    {
        if (PopupKosong == null) return;

        // Pastikan aktif
        PopupKosong.SetActive(true);

        // Reset posisi & scale
        PopupKosong.transform.localScale = Vector3.zero;
        var cg = PopupKosong.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 0f;

        // 1) Fade + scale muncul
        LeanTween.scale(PopupKosong, Vector3.one, 0.25f).setEaseOutBack();
        if (cg != null)
            LeanTween.value(PopupKosong, 0f, 1f, 0.25f)
                     .setOnUpdate((float a) => cg.alpha = a);

        // 2) Shake ringan (opsional)
        LeanTween.moveLocalX(PopupKosong, PopupKosong.transform.localPosition.x + 12f, 0.08f)
                 .setLoopPingPong(2);

        // 3) Auto-hide (fade out + scale out)
        LeanTween.delayedCall(1.5f, () =>
        {
            LeanTween.scale(PopupKosong, Vector3.zero, 0.25f).setEaseInBack();
            if (cg != null)
                LeanTween.value(PopupKosong, 1f, 0f, 0.25f)
                         .setOnUpdate((float a) => cg.alpha = a)
                         .setOnComplete(() => PopupKosong.SetActive(false));
            else
            {
                LeanTween.delayedCall(0.25f, () => PopupKosong.SetActive(false));
            }
        });
    }

    private void ApplyDefaultUIState()
    {
        // kembalikan tinggi RootJawaban kalau sebelumnya diubah
        if (RootJawaban != null && defaultRootJawabanHeight > 0f)
        {
            var rt = RootJawaban.GetComponent<RectTransform>();
            if (rt != null)
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, defaultRootJawabanHeight);
        }

        if (Tabel != null) Tabel.SetActive(false);
        if (Pertanyaan != null) Pertanyaan.SetActive(true);
        if (ButtonSumbit != null) ButtonSumbit.SetActive(true);
        if (TextButton != null) TextButton.text = "Submit";
        if (SoalText != null) SoalText.gameObject.SetActive(true);
    }

    // =========================
    // SKENARIO SCALE (biarkan seperti semula)
    // =========================
    public void JalankanSkenario1() => JalankanSkenario(-20, +25, -20);
    public void JalankanSkenario2() => JalankanSkenario(-50, +20, +50);
    public void JalankanSkenario3() => JalankanSkenario(-50, +100, -20);
    public void JalankanSkenario4() => JalankanSkenario(-36, +25, 0);

    private void JalankanSkenario(float persenPanjang, float persenTinggi, float persenLebar)
    {
        ButtomSheet.SetActive(false);
        Vector3 target = new Vector3(
            ukuranAwal.x * (1 + persenPanjang / 100f),
            ukuranAwal.y * (1 + persenTinggi / 100f),
            ukuranAwal.z * (1 + persenLebar / 100f)
        );

        if (targetObjek != null)
            LeanTween.scale(targetObjek, target, 1.2f).setEase(LeanTweenType.easeInOutSine);
    }
}
