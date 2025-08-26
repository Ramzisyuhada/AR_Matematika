using System.Collections;
using TMPro;
using UnityEngine;
using SimpleJSON;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class GradeView : MonoBehaviour
{
    [Header("Logic")]
    [SerializeField] private GradeVM ViewModel;
    [SerializeField] private AnswerView answerView;

    [Header("UI Root")]
    [SerializeField] private Transform listParent;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private GameObject LoadingScreen;
    [Header("Assets")]
    [SerializeField] private Texture2D[] Icon;

    [Header("Identikas  Soal")]
    private string SubmissionId;
    private string UserId;
    void Start()
    {
        SubmissionId = PlayerPrefs.GetString("SubmissionId", string.Empty); // <-- ganti
        UserId = PlayerPrefs.GetString("user_identifier", "Tidak Ada");
        Refresh();
    }
    string Assesment = "A_001";

    public void SetAssesment (string value)
    {
        Assesment = value;
        SetLoading(false); // pastikan awalnya off

        Refresh();

    }
    /// <summary>
    /// Tombol global: Update semua card yang ada di listParent
    /// </summary>
    public void UpdateNilai()
    {
        if (listParent == null || listParent.childCount == 0)
        {
            Debug.LogWarning("Tidak ada card untuk di-update.");
            return;
        }

        foreach (Transform child in listParent)
        {
            var card = child.GetComponent<GradeCard>();
            if (card == null) continue;

            var input = child.GetComponentInChildren<TMP_InputField>();
            if (input == null) { Debug.LogWarning("TMP_InputField tidak ketemu."); continue; }
            if (!float.TryParse(input.text, out var nilaiFloat))
            {
                Debug.LogWarning($"Nilai bukan angka valid (gradeId={card.GradeId}): '{input.text}'");
                continue;
            }

            StartCoroutine(ViewModel.UpdateGrade(
                card.GradeId,
                nilaiFloat,
                onOk: () => Debug.Log($"Update BERHASIL (gradeId={card.GradeId}, nilai={nilaiFloat})"),
                onErr: (e) => Debug.LogError($"Update GAGAL (gradeId={card.GradeId}): {e}")
            ));
        }
    }
    // Tambahkan helper di dalam GradeView
    private static bool HasUsefulData(JSONNode n)
    {
        if (n == null) return false;

        // Naik/turun level kalau dibungkus "data"/"attributes"
        if (n["data"] != null && n["data"].IsObject) n = n["data"];
        if (n["attributes"] != null && n["attributes"].IsObject) n = n["attributes"];

        // Kriteria minimum sebuah "grade" layak ditampilkan
        string gradeId = n["grade_id"]?.Value ?? n["id"]?.Value ?? "";
        string subId = n["submission_id"]?.Value ?? n["submission"]?["id"]?.Value ?? "";
        string userId = n["user_identifier"]?.Value ?? n["user"]?["identifier"]?.Value ?? "";

        // anggap valid kalau ada minimal grade_id ATAU submission_id
        if (!string.IsNullOrEmpty(gradeId)) return true;
        if (!string.IsNullOrEmpty(subId)) return true;

        // kalau tidak ada keduanya, anggap tidak berguna
        return false;
    }

    private JSONNode NormalizeItem(JSONNode n)
    {
        if (n == null) return n;
        if (n["data"] != null && n["data"].IsObject) n = n["data"];
        if (n["attributes"] != null && n["attributes"].IsObject) n = n["attributes"];
        return n;
    }


    public void Refresh()
    {
        if (listParent != null)
        {
            for (int i = listParent.childCount - 1; i >= 0; i--)
                Destroy(listParent.GetChild(i).gameObject);
        }
        SetLoading(true);

        StartCoroutine(ViewModel.LoadGrade(Assesment,
            onJson: (json) =>
            {
                try
                {
                    var root = JSON.Parse(json);
                    var items = new List<JSONNode>();

                    if (root != null)
                    {
                        if (root.IsArray)
                            foreach (var n in root.Children) items.Add(n);

                        if (root["data"] != null && root["data"].IsArray)
                            foreach (var n in root["data"].AsArray.Children) items.Add(n);

                        if (root["items"] != null && root["items"].IsArray)
                            foreach (var n in root["items"].AsArray.Children) items.Add(n);

                        if (root["data"] != null && root["data"]["items"] != null && root["data"]["items"].IsArray)
                            foreach (var n in root["data"]["items"].AsArray.Children) items.Add(n);
                    }

                    // ⛔ HAPUS fallback yang memasukkan object kosong
                    // Dulu: kalau items kosong, items.Add(root) → ini bikin card hantu

                    // Normalisasi level & saring yang benar-benar punya data
                    var filtered = new List<JSONNode>();
                    foreach (var it in items)
                    {
                        var norm = NormalizeItem(it);
                        if (HasUsefulData(norm))
                            filtered.Add(norm);
                    }

                    if (filtered.Count == 0)
                    {
                        Debug.Log("[GradeView] Tidak ada grade yang valid.");
                        // (Opsional) tampilkan placeholder 'no data' di UI
                        SetLoading(false);
                        return;
                    }

                    foreach (var it in filtered)
                    {
                        Debug.Log("[GradeView] Item valid: " + it.ToString());
                        SpawnCard(it);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[GradeView] Parse error: " + ex.Message);
                }
                finally
                {
                    SetLoading(false);
                }
            },
            onErr: (err) =>
            {
                Debug.LogError("LoadGrades error: " + err);
                SetLoading(false);
            }
        ));
    }


    private void SpawnCard(JSONNode item)
    {
        if (cardPrefab == null || listParent == null)
        {
            Debug.LogError("CardPrefab atau ListParent belum di-assign.");
            return;
        }

        item = NormalizeItem(item);
        if (!HasUsefulData(item))
        {
            Debug.Log("[GradeView] Skip card: item tidak valid/ kosong.");
            return;
        }

        var go = Instantiate(cardPrefab, listParent);
        var card = go.GetComponent<GradeCard>();
        if (card == null)
        {
            Debug.LogError("Prefab Card tidak memiliki komponen GradeCard.");
            Destroy(go);
            return;
        }

        card.Init(answerView);

        try
        {
            card.Bind(item, Icon, Assesment);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Bind error: {ex.Message}\n{ex.StackTrace}");
            Destroy(go); // jangan biarkan card kosong nongol
        }

        if (answerView == null)
        {
            Debug.LogError("AnswerView belum di-assign di Inspector.");
        }
    }



    public void PostNilai()
    {
        StartCoroutine(PostNilaiFlow());
    }

    private IEnumerator PostNilaiFlow()
    {

        SetLoading(true); // ⬅️ mulai loading

        // 1) Tunggu sampai SubmissionId tersedia (hasil PostSubs dari AnswerView)
        yield return StartCoroutine(WaitForSubmissionId());

        if (!IsValidSubmissionId(SubmissionId))
        {
            SetLoading(false); // ⬅️ selesai loading (gagal)

            // sudah di-log error di WaitForSubmissionId
            yield break;
        }

        // 2) Siapkan payload grade (gunakan SubmissionId yang valid)
        string jsonBody = JsonConvert.SerializeObject(new
        {
            submission_id = PlayerPrefs.GetString("SubmissionId", ""),

            user_identifier = UserId,
            score = 0f
        });

        bool done = false;
        bool ok = false;
        string errMsg = null;

        // 3) POST grade
        yield return StartCoroutine(ViewModel.Post(jsonBody,
            onJson: res =>
            {
                ok = true; done = true;
            },
            onErr: Err =>
            {
                ok = false; done = true; errMsg = Err;
                Debug.LogWarning($"[GradeView] Post nilai GAGAL: {Err}");
            }
        ));

        while (!done) yield return null;
        SetLoading(false); // ⬅️ selesai loading (apapun hasilnya)

        if (!ok)
        {
            // jika perlu, tampilkan UI error di sini
            yield break;
        }

        // sukses → kalau mau refresh list grade, panggil:
        // Refresh();
    }


    // tambahkan di dalam GradeView
    private void SetLoading(bool on)
    {
        if (LoadingScreen) LoadingScreen.SetActive(on);
    }

    private bool IsValidSubmissionId(string sid)
    {
        return !string.IsNullOrEmpty(sid) && sid != "-" && sid != "Tidak Ada";
    }

    // Nunggu sampai SubmissionId terisi (prefer PlayerPrefs), dengan timeout
    private IEnumerator WaitForSubmissionId(float timeoutSeconds = 15f, float pollInterval = 0.2f)
    {
        float elapsed = 0f;

        while (elapsed < timeoutSeconds)
        {
            // update dari PlayerPrefs (AnswerView.RunSequentially() akan SetString)
            var sid = PlayerPrefs.GetString("SubmissionId", string.Empty);
            if (IsValidSubmissionId(sid))
            {
                SubmissionId = sid; // simpan ke field juga
                yield break;
            }

            elapsed += pollInterval;
            yield return new WaitForSeconds(pollInterval);
        }

        Debug.LogError("[GradeView] Timeout menunggu SubmissionId. Pastikan AnswerView sudah finalize (PostSubs).");
    }
}
