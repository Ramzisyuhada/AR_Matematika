using Newtonsoft.Json;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class AnswerView : MonoBehaviour
{
    [Header("Logic")]
    [SerializeField] private AnswerVM ViewModel;
    [SerializeField] private GradeVM ViewModel1;
    [SerializeField] private SubmissionsView ViewModel3;

    [SerializeField] private GradeCard nilai;
    [Header("UI Root")]
    [SerializeField] private TMP_Text UsernameHead;
    [SerializeField] private TMP_Text Username;

    [SerializeField] private TMP_Text GayaBelajar;
    [SerializeField] private RawImage Profile;
    [SerializeField] private TMP_Text JawabanSoal1;
    [SerializeField] private TMP_Text JawabanSoal2;
    [SerializeField] private TMP_Text JawabanSoal3;
    [SerializeField] private TMP_Text NamFile;
    [SerializeField] private TMP_InputField IsiNilai;
    [SerializeField] private GameObject ScrollView;
    [SerializeField] private GameObject PreviewNilai;

    [Header("Siswa")]
    [SerializeField] private GameObject UploadFile;
    [SerializeField] private GameObject Canvas1;
    [SerializeField] private GameObject Canvas2;


    [SerializeField] private TMP_InputField InputJawaban;

    [Header("Assets")]
    [SerializeField] private Texture2D[] Icon;

    [Header("Identikas  Soal")]
    private string SubmissionId;
    private string QuestionId;


    private string LinkDownload;

    private string Nilai;

    private int NoSoal = 1;
    private bool isSubmitting = false;
    private bool isFinalized = false;



    private string GradeIdUntukUpdate;      

    // ====== PENAMBAH ======
    [System.Serializable]

    private class PendingAnswer
    {
        public string submission_id;
        public string question_id;
        public string answer_text;
    }

    public void PrefillHeaderFromGradev3(
      string name, string gender, float score, Texture avatar,
      string userid,JSONNode item)
    {
        // UI awal
        if (UsernameHead) UsernameHead.text = name ?? "(tanpa nama)";
        if (Username) Username.text = name ?? "(tanpa nama)";
        if (Profile && avatar) Profile.texture = avatar;
        if (GayaBelajar) GayaBelajar.text = "-";

    
        userid = (userid ?? "").Trim();
        string GradeId = item["grade_id"]?.Value ?? item["id"]?.Value ?? "";
        string SubmissionId = item["submission_id"]?.Value ?? "";

        var objectsubmission = item["submission"];
        string Assesemntid = item["submission"]?["assessment_id"]?.Value ?? "";
        Debug.Log("Datas : "+item.ToString());
        Debug.Log($"[AnswerView#{GetInstanceID()}] PrefillHeaderFromGrade sub='{SubmissionId}' assess='{Assesemntid}' user='{userid}'");

        StartCoroutine(ViewModel.LoadAnswerById1(
            SubmissionId,                        // path
            Assesemntid,
            userid,// query
            onJson: (json) =>
            {
                var root = JSON.Parse(json);
                Debug.Log("[LoadAnswerById1] Response: " + root);
                if (root == null) { Debug.LogError("JSON null"); return; }

                JSONArray arr = null;
                if (root.IsArray) arr = root.AsArray;
                else if (root.IsObject)
                {
                    if (root["data"] != null && root["data"].IsArray) arr = root["data"].AsArray;
                    else if (root["items"] != null && root["items"].IsArray) arr = root["items"].AsArray;
                }

                if (arr == null || arr.Count == 0)
                {
                    Debug.LogWarning("[AnswerView] Data kosong / bukan array");
                    return;
                }

                var first = arr[0];
                string userName = "-";
                string userIdentifier = "-";
                string genderFromJson = "-"; // ⬅️ hindari shadowing 'gender' param

                var submissionObj = first["submission"];
                if (submissionObj != null)
                {
                    userIdentifier = submissionObj["user_identifier"];
                    var userObj = submissionObj["user"];
                    if (userObj != null)
                    {
                        userName = userObj["name"];
                        genderFromJson = userObj["gender"];
                    }
                }

                var answers = new System.Collections.Generic.Dictionary<int, string>();
                foreach (JSONNode item in arr)
                {
                    int qNum = item["question"]["question_number"].AsInt;
                    string ans = item["answer_text"] ?? "-";
                    if (qNum > 0) answers[qNum] = ans;
                }

                if (UsernameHead != null) UsernameHead.text = string.IsNullOrEmpty(userName) ? userIdentifier : userName;
                if (Username != null) Username.text = string.IsNullOrEmpty(userName) ? userIdentifier : userName;
                if (GayaBelajar != null) GayaBelajar.text = "-";

                if (JawabanSoal1 != null) JawabanSoal1.text = answers.ContainsKey(1) ? answers[1] : "-";
                if (JawabanSoal2 != null) JawabanSoal2.text = answers.ContainsKey(2) ? answers[2] : "-";
                if (JawabanSoal3 != null) JawabanSoal3.text = answers.ContainsKey(3) ? answers[3] : "-";
                if (LinkDownload != null) LinkDownload = answers.ContainsKey(4) ? answers[4] : "-";
            },
            onErr: (err) => Debug.LogError("LoadAnswer error: " + err)
        ));
    }


    // penampung semua jawaban sampai final submit
    private readonly List<PendingAnswer> pendingAnswers = new List<PendingAnswer>();
    private void Start()
    {
        SubmissionId = PlayerPrefs.GetString("SubmissionId", "S001");
        QuestionId = PlayerPrefs.GetString("QuestionId", "Tidak Ada");

    }
    // Panggil ini saat user klik Next (soal 1 -> 2 -> 3)
    public void SaveCurrentAnswer()
    {
        if (InputJawaban == null || string.IsNullOrWhiteSpace(InputJawaban.text))
        {
            Debug.LogWarning("[SaveCurrentAnswer] Input jawaban kosong.");
            return;
        }
        if (string.IsNullOrEmpty(SubmissionId))
        {
            Debug.LogWarning("[SaveCurrentAnswer] (info) SubmissionId belum ada (akan dibuat saat final).");
        }

        // Simpan jawaban untuk soal saat ini
        int currentIndex = NoSoal;                       // simpan nomor saat ini (1..3)
        string qid = "Q" + currentIndex.ToString("000"); // Q001/Q002/Q003

        int existing = pendingAnswers.FindIndex(p => p.question_id == qid);
        var payload = new PendingAnswer
        {
            submission_id = "",                          // diisi nanti setelah PostSubs
            question_id = qid,
            answer_text = InputJawaban.text.Trim()
        };
        if (existing >= 0) pendingAnswers[existing] = payload;
        else pendingAnswers.Add(payload);

        Debug.Log($"[SaveCurrentAnswer] cached {qid}: {payload.answer_text}");

        // Kosongkan input untuk UX rapi
        InputJawaban.text = string.Empty;

        // Jika ini jawaban ke-3 -> langsung finalize (buat submission + kirim semua)
        if (currentIndex == 3)
        {
            if (!isSubmitting && !isFinalized)
            {
                // Tampilkan UI upload kalau perlu
                if (UploadFile) UploadFile.SetActive(true);
                if (Canvas1) Canvas1.SetActive(false);
                if (Canvas2) Canvas2.SetActive(false);

            }
            return;
        }

        // Kalau masih soal 1 atau 2, lanjutkan ke nomor berikutnya
        NoSoal = currentIndex + 1;
    }


    // Panggil ini saat user klik Final (setelah 3 soal)
    public void FinalSubmitAnswers()
    {
        if (isSubmitting || isFinalized)
        {
            Debug.LogWarning("[FinalSubmitAnswers] Sudah dalam proses / sudah finalize.");
            return;
        }
        if (pendingAnswers.Count == 0)
        {
            Debug.LogWarning("[FinalSubmitAnswers] Tidak ada jawaban yang disimpan.");
            return;
        }
        StartCoroutine(RunSequentially());
    }

    public IEnumerator RunSequentially()
    {
        isSubmitting = true;
        string newSubmissionId = null;

        // Buat submission baru
        yield return StartCoroutine(ViewModel3.PostSubs(id => {
            newSubmissionId = id;
        }));

        if (string.IsNullOrEmpty(newSubmissionId))
        {
            isSubmitting = false;
            Debug.LogError("[RunSequentially] Gagal membuat submission, hentikan proses.");
            yield break;
        }

        SubmissionId = newSubmissionId;
        PlayerPrefs.SetString("SubmissionId", SubmissionId);
        PlayerPrefs.Save();

        // Isi submission_id untuk semua jawaban
        for (int i = 0; i < pendingAnswers.Count; i++)
            pendingAnswers[i].submission_id = SubmissionId;

        // Kirim semua jawaban
        yield return StartCoroutine(PostAllSequentially());

        isSubmitting = false;
        isFinalized = true;
    }


    private System.Collections.IEnumerator PostAllSequentially()
    {
        for (int i = 0; i < pendingAnswers.Count; i++)
        {
            string body = JsonConvert.SerializeObject(pendingAnswers[i]);
            bool done = false;
            bool ok = false;
            string errMsg = null;

            yield return StartCoroutine(ViewModel.PostAnswer(
                body,
                onJson: res => { ok = true; done = true; Debug.Log($"  ✓ Submit {pendingAnswers[i].question_id} OK"); },
                onErr: err => { ok = false; done = true; errMsg = err; Debug.LogWarning($"  ✗ Submit {pendingAnswers[i].question_id} GAGAL: {err}"); }
            ));

            // tunggu callback (kalau implementasi ViewModel kamu langsung meng-invoke callback, flag ini sudah true)
            while (!done) yield return null;

            if (!ok)
            {
                Debug.LogError("[FinalSubmitAnswers] Dihentikan karena ada error.");
                yield break;
            }
        }

        // Bersihkan cache & reset state jika perlu
        pendingAnswers.Clear();
        NoSoal = 1;
        InputJawaban.text = string.Empty;
    }

    public void ShowDetail(string iduser,string idas)
    {
        
        if (string.IsNullOrEmpty(iduser))
        {
            Debug.LogWarning("GradeId kosong saat buka detail");
            return;
        }

        
    }

    public void Download()
    {
        if (string.IsNullOrEmpty(LinkDownload) || LinkDownload == "-")
        {
            Debug.LogWarning("Link download kosong");
            return;
        }

        Debug.Log("Mulai download: " + LinkDownload);
        StartCoroutine(DownloadFile(LinkDownload));
    }

    private IEnumerator DownloadFile(string url)
    {
        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Download gagal: " + request.error);
        }
        else
        {
            // Simpan file di persistentDataPath
            string fileName = System.IO.Path.GetFileName(url);
            string path = System.IO.Path.Combine(Application.persistentDataPath, fileName);
            System.IO.File.WriteAllBytes(path, request.downloadHandler.data);

            Debug.Log("File berhasil diunduh: " + path);
            Application.OpenURL(path); // Buka file (di luar app)
        }
    }

    public void PostNilai()
    {

        if (float.TryParse(IsiNilai.text, out float nilaiFloat))
        {

            StartCoroutine(ViewModel1.UpdateGrade(
                Nilai,
                nilaiFloat,
                onOk: () => Debug.Log($"Update BERHASIL (gradeId={Nilai}, nilai={nilaiFloat})"),
                onErr: (e) => Debug.LogError($"Update GAGAL (gradeId={Nilai}): {e}")
            ));
        }
        else
        {
            Debug.LogError("Input nilai tidak valid, gagal parse ke float.");
        }
    }



}
