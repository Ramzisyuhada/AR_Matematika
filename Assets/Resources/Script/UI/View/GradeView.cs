using System.Collections;
using TMPro;
using UnityEngine;
using SimpleJSON;

public class GradeView : MonoBehaviour
{
    [Header("Logic")]
    [SerializeField] private GradeVM ViewModel;
    [SerializeField] private AnswerView answerView;

    [Header("UI Root")]
    [SerializeField] private Transform listParent;
    [SerializeField] private GameObject cardPrefab;

    [Header("Assets")]
    [SerializeField] private Texture2D[] Icon;

    void Start()
    {
        Refresh();
    }
    string Assesment = "A_001";

    public void SetAssesment (string value)
    {
        Assesment = value;
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

    public void Refresh()
    {
        if (listParent != null)
        {
            for (int i = listParent.childCount - 1; i >= 0; i--)
                Destroy(listParent.GetChild(i).gameObject);
        }

        StartCoroutine(ViewModel.LoadGrade(Assesment,
            onJson: (json) =>
            {
                var root = JSON.Parse(json);

                JSONArray arr = null;
                if (root.IsArray)
                {
                    arr = root.AsArray;
                }
                else if (root.IsObject)
                {
                    if (root["data"] != null && root["data"].IsArray)
                        arr = root["data"].AsArray;
                    else if (root["items"] != null && root["items"].IsArray)
                        arr = root["items"].AsArray;
                }

                if (arr == null)
                {
                    SpawnCard(root);
                    return;
                }

                foreach (var node in arr)
                    SpawnCard(node.Value);
            },
            onErr: (err) =>
            {
                Debug.LogError("LoadGrades error: " + err);
            }
        ));
    }

    private void SpawnCard(JSONNode item)
    {
        if (cardPrefab == null || listParent == null)
        {
            Debug.LogError("CardPrefab atau ListParent belum di-assign di Inspector.");
            return;
        }

        var go = Instantiate(cardPrefab, listParent);
        var card = go.GetComponent<GradeCard>();
        if (card == null)
        {
            Debug.LogError("Prefab Card tidak memiliki komponen GradeCard.");
            return;
        }

        // Bind data
        try
        {
            card.Bind(item, Icon);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Bind error: {ex.Message}\n{ex.StackTrace}");
        }

        // Pastikan answerView terisi
        if (answerView == null)
        {
            Debug.LogError("AnswerView belum di-assign di Inspector.");
            return;
        }

        //// >>> Perbaikan: subscribe pakai lambda agar pasti Action<string>
        //System.Action<string> handler = id => answerView.ShowDetail(id,id);

        //// Simpan handler & auto-unsubscribe saat GameObject di-destroy
        //var sub = go.AddComponent<GradeCardSubscription>();
        //sub.Attach(card, handler);
    }

}
