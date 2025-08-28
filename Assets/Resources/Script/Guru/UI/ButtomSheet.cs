using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BottomSheet : MonoBehaviour
{
    [Header("Panels (urut: Grading, Ranking, Avg)")]
    [SerializeField] private List<GameObject> menus = new List<GameObject>();

    [Header("Header")]
    [SerializeField] private Image headerImageTarget;   // drag komponen Image dari HeaderObject
    [SerializeField] private List<Sprite> headerImages = new List<Sprite>(); // opsional, sesuai tab
    [SerializeField] private ScrollRect scrollRect;

    [SerializeField] private GameObject Header1;
    [SerializeField] private GameObject Scroll;
    [SerializeField] private GameObject ScrollNilai;

    [SerializeField] private GameObject Button;
    [SerializeField] private GameObject Upload;
    [SerializeField] private GameObject Parent;

    private const int IDX_GRADING = 0;
    private const int IDX_RANKING = 1;
    private const int IDX_AVG = 2;

    private void Awake()
    {
        Parent.SetActive(false);

        // Validasi sederhana
        if (menus == null || menus.Count == 0)
            Debug.LogWarning($"{nameof(BottomSheet)}: 'menus' kosong. Isi minimal 1 panel.");

        if (scrollRect == null)
            Debug.LogWarning($"{nameof(BottomSheet)}: 'scrollRect' belum di-assign.");
    }

    private void OnEnable()
    {
        // Default buka tab pertama jika ada
        if (menus.Count > 0) SetTab(Mathf.Min(IDX_GRADING, menus.Count - 1));
    }

    // === Public API untuk tombol ===
    public void GradingButton() => SetTab(IDX_GRADING);
    public void Ranking() => SetTab(IDX_RANKING);
    public void Avg() => SetTab(IDX_AVG);

    public void PostTest()
    {
        headerImageTarget.sprite = headerImages[3];
        Button.SetActive(true);
        Header1.SetActive(false);
        Scroll.SetActive(false);
        Upload.SetActive(true);
        Parent.SetActive(true);
    }
    public void Render3D()
    {
        // TODO: isi sesuai kebutuhan render 3D-mu
        Debug.Log("Render3D() dipanggil.");
    }

    // === Inti logika pindah tab ===
    private void SetTab(int index)
    {
        Scroll.SetActive(true);
        ScrollNilai.SetActive(false);
        Button.SetActive(false);
        Header1.SetActive(true);
        Scroll.SetActive(true);
        Upload.SetActive(false);
        Parent.SetActive(false);

        if (menus == null || menus.Count == 0) return;

        // Clamp index agar aman
        index = Mathf.Clamp(index, 0, menus.Count - 1);

        // Aktif/nonaktifkan panel
        for (int i = 0; i < menus.Count; i++)
        {
            if (menus[i] != null) menus[i].SetActive(i == index);
        }

        // Pasang content ScrollRect ke panel aktif & reset posisi scroll ke atas
        if (scrollRect != null && menus[index] != null)
        {
            var rt = menus[index].GetComponent<RectTransform>();
            if (rt != null) scrollRect.content = rt;

            // Reset posisi (1 = atas untuk verticalNormalizedPosition)
            scrollRect.verticalNormalizedPosition = 1f;
            scrollRect.horizontalNormalizedPosition = 0f;
        }

        // Update header image bila tersedia
        if (headerImageTarget != null && index < headerImages.Count && headerImages[index] != null)
        {
            headerImageTarget.sprite = headerImages[index];
            headerImageTarget.enabled = true;
        }
        else if (headerImageTarget != null && headerImages.Count > 0)
        {
            // Jika tidak ada sprite untuk tab ini, bisa nonaktifkan gambar agar tidak menampilkan sprite lama
            headerImageTarget.enabled = false;
        }
    }
}
