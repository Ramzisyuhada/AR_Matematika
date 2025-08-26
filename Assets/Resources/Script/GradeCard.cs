using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SimpleJSON;
using System;
using System.Globalization; // ⬅️ tambah ini

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


    private string displayName;
private string displayGender;
private float displayScore;
private Texture displayAvatar;
    // Simpan ID yang relevan
    public string GradeId; // biasanya "grade_id"
    public string SubmissionId; // biasanya "submission_id"
    public string UserIdentifier; // tambah property
    public string Assesemntid; // tambah property
    private string CardTag => $"[GradeCard#{GetInstanceID()}]";

    private void Awake()
    {
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();      // 🔒 pastikan tidak dobel
            btn.onClick.AddListener(OnCardClicked);
        }
        else
        {
            Debug.LogWarning("[GradeCard] Button belum di-assign di Inspector.");
        }
    }
    private void Start()
    {
        var views = FindObjectsOfType<AnswerView>(true);
        Debug.Log($"[Check] AnswerView count = {views.Length}");

    }
    private void OnDestroy()
    {
        if (btn != null)
            btn.onClick.RemoveListener(OnCardClicked);
    }
    JSONNode datas;
    public void Bind(JSONNode item, Texture2D[] iconSet)
    {
        Debug.Log("Hello Ini Data : " + item.ToString());
        if (item == null)
        {
            Debug.LogWarning("[GradeCard] Bind dipanggil dengan item null.");
            return;
        }
        datas = item;

        GradeId = item["grade_id"]?.Value ?? item["id"]?.Value ?? "";
        SubmissionId = item["submission"]?["submission_id"]?.Value ?? "";

        var objectsubmission = item["submission"];
        Assesemntid =  item["submission"]?["assessment_id"]?.Value ?? "";

        UserIdentifier = item["user_identifier"]?.Value ?? item["user"]?["user_identifier"]?.Value ?? "";
        Debug.Log($"{CardTag} Bind OK → grade={GradeId} sub={SubmissionId} assessment={Assesemntid} user={UserIdentifier}");

        // Data user
        var user = item["user"];
        string gender = user?["gender"]?.Value ?? "";
        string name = user?["name"]?.Value ?? "(tanpa nama)";

        bool isMale =
            gender.Equals("laki-laki", StringComparison.OrdinalIgnoreCase) ||
            gender.Equals("male", StringComparison.OrdinalIgnoreCase) ||
            gender.Equals("l", StringComparison.OrdinalIgnoreCase);

        // Ikon
        int iconIndex = isMale ? 0 : 1;
        if (iconSet != null && iconSet.Length > 0)
        {
            if (iconIndex < 0 || iconIndex >= iconSet.Length) iconIndex = 0;
            if (profile != null) profile.texture = iconSet[iconIndex];
        }

        if (nama != null) nama.text = name;

        // ⬇️ PARSE SCORE DENGAN INVARIANT
        float score = ParseFloatInvariant(item["score"]?.Value);
        if (nilai != null) nilai.text = score.ToString("0.##", CultureInfo.InvariantCulture);
        displayName = name;
        displayGender = gender;
        displayScore = score;
        // Warna nilai + kondisi
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

    private static float ParseFloatInvariant(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0f;
        // Coba invariant dulu (untuk "23.00")
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) return f;
        // Coba ganti koma/titik
        if (float.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out f)) return f;
        if (float.TryParse(s.Replace('.', ','), NumberStyles.Float, CultureInfo.GetCultureInfo("id-ID"), out f)) return f;
        // Terakhir: current culture (fallback)
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out f)) return f;
        return 0f;
    }

    // Klik pada card (atau tombol di card)
    private void OnCardClicked()
    {
        // Atur panel-panelmu seperti sebelumnya...
        transform.parent.parent.parent.gameObject.SetActive(false);
        transform.parent.parent.parent.parent.GetChild(4).gameObject.SetActive(true);
        transform.parent.parent.parent.parent.GetChild(1).gameObject.SetActive(false);
        Debug.Log($"[Klik] grade={GradeId}, submission={SubmissionId}, user={UserIdentifier}");
        var sid = (this.SubmissionId ?? "").Trim();
        var asid = (this.Assesemntid ?? "").Trim();
        if (View == null)
        {
            Debug.LogError("[GradeCard] AnswerView belum di-init. Panggil card.Init(answerView) saat spawn.");
            return;
        }

        Debug.Log($"{CardTag} OnCardClicked sub='{sid}' assess='{asid}' view='{(View ? View.GetInstanceID().ToString() : "null")}'");

        nama.text = displayName;
        View.PrefillHeaderFromGradev3(
    displayName,
    displayGender,
    displayScore,
    displayAvatar,
   
    UserIdentifier,
    datas

);
        string idForDetail = !string.IsNullOrEmpty(SubmissionId) ? SubmissionId : GradeId;
        if (!string.IsNullOrEmpty(idForDetail))
        {
           // Debug.Log($"[GradeCard] OpenDetail submission={SubmissionId}, grade={Assesemntid}");
            //View.ShowDetail(SubmissionId, Assesemntid);
        }
        else
        {
            Debug.LogWarning("[GradeCard] Tidak ada id untuk dibuka (SubmissionId & GradeId kosong).");
        }
    }

    public void Init(AnswerView view)
    {
        View = view;
    }

}
