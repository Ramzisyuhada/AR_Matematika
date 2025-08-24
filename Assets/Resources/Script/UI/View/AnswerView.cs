using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
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

    [Header("Assets")]
    [SerializeField] private Texture2D[] Icon;


   private string LinkDownload;

    private string Nilai;


    public void ShowDetail(string gradeId,string nilai)
    {
        
        Nilai = nilai;
        if (string.IsNullOrEmpty(gradeId))
        {
            Debug.LogWarning("GradeId kosong saat buka detail");
            return;
        }

        Debug.Log("ID : "+gradeId);
        StartCoroutine(ViewModel.LoadAnswerById(gradeId,
            onJson: (json) =>
            {
                var root = JSON.Parse(json);
                if (root == null) { Debug.LogError("JSON null"); return; }

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
                if (arr == null || arr.Count == 0)
                {
                    Debug.LogWarning("[AnswerView.ShowDetail] Data kosong / bukan array");
                    return;
                }

                // Ambil info user dari jawaban pertama
                var first = arr[0];
                string userName = "-";
                string userIdentifier = "-";
                string gender = "-";

                var submissionObj = first["submission"];
                if (submissionObj != null)
                {
                    userIdentifier = submissionObj["user_identifier"] ?? "-";
                    var userObj = submissionObj["user"];
                    if (userObj != null)
                    {
                        userName = userObj["name"] ?? "-";
                        gender = userObj["gender"] ?? "-";
                    }
                }

           

                Dictionary<int, string> answers = new Dictionary<int, string>();
                foreach (JSONNode item in arr)
                {
                    int qNum = item["question"]["question_number"].AsInt;
                    string ans = item["answer_text"] ?? "-";
                    Debug.Log(ans);

                    if (qNum > 0) answers[qNum] = ans;
                }

                if (UsernameHead != null)
                {
                    UsernameHead.text = string.IsNullOrEmpty(userName) ? userIdentifier : userName;
                    Username.text = string.IsNullOrEmpty(userName) ? userIdentifier : userName;
                }
                if (GayaBelajar != null)
                    GayaBelajar.text = "-"; // Belum ada di JSON

                if (JawabanSoal1 != null)
                    JawabanSoal1.text = answers.ContainsKey(1) ? answers[1] : "-";
                if (JawabanSoal2 != null)
                    JawabanSoal2.text = answers.ContainsKey(2) ? answers[2] : "-";
                if (JawabanSoal3 != null)
                    JawabanSoal3.text = answers.ContainsKey(3) ? answers[3] : "-";
                if (LinkDownload != null)
                    LinkDownload = answers.ContainsKey(4) ? answers[4] : "-";
                Debug.Log(arr);

            },
            onErr: (err) => Debug.LogError("LoadAnswer error: " + err)
        ));
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
