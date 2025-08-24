using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SimpleJSON;
using System;

public class CardRank : MonoBehaviour
{

    [Header("UI (drag dari prefab Card)")]
    [SerializeField] private RawImage profile;
    [SerializeField] private TMP_Text nama;
    [SerializeField] private Image warnaBG;
    [SerializeField] private TMP_Text nilai;

    // Start is called before the first frame update
    public void Bind(JSONNode item, Texture2D[] iconSet)
    {
        if (item == null) return;

        // Simpan GradeId (dipakai saat user berinteraksi)

        var user = item["user"];
        string gender = user?["gender"]?.Value ?? "";
        string name = user?["name"]?.Value ?? "(tanpa nama)";

        bool isMale =
            gender.Equals("laki-laki", StringComparison.OrdinalIgnoreCase) ||
            gender.Equals("male", StringComparison.OrdinalIgnoreCase) ||
            gender.Equals("l", StringComparison.OrdinalIgnoreCase);

        int iconIndex = isMale ? 0 : 1;
        if (iconSet != null && iconSet.Length > 0)
        {
            if (iconIndex >= iconSet.Length) iconIndex = 0;
            if (profile != null) profile.texture = iconSet[iconIndex];
        }

        if (nama != null) nama.text = name;

        // Set nilai awal
        float score = item["score"].AsFloat;
        if (nilai != null) nilai.text = score.ToString("0.##");

        // Warna nilai + kondisi
       

        // Warna background berdasarkan gender
        if (warnaBG != null)
        {
            Color c = Color.white;
            if (isMale) ColorUtility.TryParseHtmlString("#00B2FF", out c);
            else ColorUtility.TryParseHtmlString("#FF6584", out c);
            warnaBG.color = c;
        }
    }
}
