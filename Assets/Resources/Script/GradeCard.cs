using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SimpleJSON;
using System;

public class GradeCard : MonoBehaviour
{
    [Header("UI (drag dari prefab Card)")]
    [SerializeField] private RawImage profile;
    [SerializeField] private TMP_Text nama;
    [SerializeField] private TMP_Text Kondisi;
    [SerializeField] private TMP_Text nilai;

    [Header("Buttons / Root")]
    [SerializeField] private Button btn;          // drag dari prefab
    public Button RefreshButton => btn;          // akses publik yg aman

    [Header("Warna BG")]
    [SerializeField] private RawImage warnaBGNilai;
    [SerializeField] private RawImage warnaBG;

    [Header("Panels")]
    [SerializeField] public GameObject Filter;


    [Header("Logic")]
    [SerializeField] public AnswerView View;

    public event Action<string> OnOpenDetail;     // kirim submissionId (umumnya buat detail)
    public event Action<string, float> OnUpdateClicked; // (gradeId, nilai) kalau dipakai

    // Simpan ID yang relevan
    public string GradeId { get; private set; } // biasanya "grade_id"
    public string SubmissionId { get; private set; } // biasanya "submission_id"

    private void Awake()
    {
        if (btn != null)
            btn.onClick.AddListener(OnCardClicked);
        else
            Debug.LogWarning("[GradeCard] Button belum di-assign di Inspector.");
    }

    private void OnDestroy()
    {
        if (btn != null)
            btn.onClick.RemoveListener(OnCardClicked);
    }

    public void Bind(JSONNode item, Texture2D[] iconSet)
    {
        if (item == null)
        {
            Debug.LogWarning("[GradeCard] Bind dipanggil dengan item null.");
            return;
        }

        // Ambil ID aman (utamakan grade_id untuk update, submission_id untuk detail)
        GradeId = item["grade_id"]?.Value ?? item["id"]?.Value ?? "";
        SubmissionId = item["submission_id"]?.Value ?? "";

        // Data user
        var user = item["user"];
        string gender = user?["gender"]?.Value ?? "";
        string name = user?["name"]?.Value ?? "(tanpa nama)";

        bool isMale =
            gender.Equals("laki-laki", StringComparison.OrdinalIgnoreCase) ||
            gender.Equals("male", StringComparison.OrdinalIgnoreCase) ||
            gender.Equals("l", StringComparison.OrdinalIgnoreCase);

        // Ikon aman
        int iconIndex = isMale ? 0 : 1;
        if (iconSet != null && iconSet.Length > 0)
        {
            if (iconIndex < 0 || iconIndex >= iconSet.Length) iconIndex = 0;
            if (profile != null) profile.texture = iconSet[iconIndex];
        }

        if (nama != null) nama.text = name;

        // Nilai
        float score = item["score"].AsFloat; // default 0 kalau kosong
        if (nilai != null) nilai.text = score.ToString("0.##");

        // Warna nilai + kondisi (aman jika parsing gagal)
        if (warnaBGNilai != null)
        {
            Color c = score <= 0f
                ? (ColorUtility.TryParseHtmlString("#F60032", out var red) ? red : Color.red)
                : (ColorUtility.TryParseHtmlString("#47F600", out var green) ? green : Color.green);
            warnaBGNilai.color = c;

            if (Kondisi != null)
                Kondisi.text = (score <= 0f) ? "Belum Dinilai" : "Sudah Dinilai";
        }

        // Warna background berdasarkan gender
        if (warnaBG != null)
        {
            bool ok = ColorUtility.TryParseHtmlString(isMale ? "#00B2FF" : "#FF6584", out var c);
            warnaBG.color = ok ? c : Color.white;
        }
    }

    // Klik pada card (atau tombol di card)
    private void OnCardClicked()
    {
        Debug.Log(transform.parent.root.gameObject.name);
        transform.parent.parent.parent.gameObject.SetActive(false);
        transform.parent.parent.parent.parent.GetChild(4).gameObject.SetActive(true);
        transform.parent.parent.parent.parent.GetChild(1).gameObject.SetActive(false);
        // Untuk buka detail jawaban, umumnya pakai submissionId
        string idForDetail = !string.IsNullOrEmpty(SubmissionId) ? SubmissionId : GradeId;

        if (!string.IsNullOrEmpty(idForDetail))
        {
            View = transform.parent.root.gameObject.GetComponent<AnswerView>();
            Debug.Log($"[GradeCard] OpenDetail id={idForDetail}");
            View.ShowDetail(SubmissionId, GradeId);

        }
        else
        {
            Debug.LogWarning("[GradeCard] Tidak ada id untuk dibuka (SubmissionId & GradeId kosong).");
        }
    }
}
