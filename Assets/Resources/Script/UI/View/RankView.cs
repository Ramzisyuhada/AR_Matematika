using SimpleJSON;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RankView : MonoBehaviour
{
    [Header("Logic")]
    [SerializeField] private RankVM ViewModel;

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

    public void SetAssesment(string value)
    {
        Assesment = value;
        Refresh();

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
        var card = go.GetComponent<CardRank>();
        if (card == null)
        {
            Debug.LogError("Prefab Card tidak memiliki komponen GradeCard.");
            return;
        }

        card.Bind(item, Icon);

      
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
