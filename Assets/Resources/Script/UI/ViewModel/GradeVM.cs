using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class GradeVM : MonoBehaviour
{
    public ApiClient apiClient;
    public Endpoints endpoints;
    [SerializeField] private string tokenBearer = ""; // isi kalau butuh Authorization

    public IEnumerator LoadGradeById(string path, Action<string> onJson, Action<string> onErr)
    {
        string url = endpoints.getById.Replace("{id}", path.ToString()); 
        yield return apiClient.GetById(path, onJson, onErr);
    }

    public IEnumerator UpdateGrade(string gradeId, float nilai, Action onOk, Action<string> onErr)
    {
        // ganti {id} dengan gradeId
        string path = endpoints.update.Replace("{id}", gradeId);
        string url = endpoints.baseUrl + path;

        // payload JSON
        string jsonBody = "{\"score\":" + nilai.ToString("0.##") + "}";

        using (var req = new UnityWebRequest(url, "PUT"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            if (!string.IsNullOrEmpty(tokenBearer))
                req.SetRequestHeader("Authorization", "Bearer " + tokenBearer);

            req.timeout = 20;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[UpdateGrade] OK: " + req.downloadHandler.text);
                onOk?.Invoke();
            }
            else
            {
                string detail = $"[{req.result}] {req.error} | {req.downloadHandler.text}";
                Debug.LogError("[UpdateGrade] ERR " + detail);
                onErr?.Invoke(detail);
            }
        }
    }


    public IEnumerator LoadGrade(string path, Action<string> onJson, Action<string> onErr)
    {
        string url = endpoints.userByQuery.Replace("{id}", UnityWebRequest.EscapeURL(path));
        yield return apiClient.GetById(url, onJson, onErr);
    }
    public IEnumerator LoadAllGrade(Action<string> onJson, Action<string> onErr)
    {
        yield return apiClient.Get( onJson, onErr);

    }
    public IEnumerator Post(string jsonBody, Action<string> onJson, Action<string> onErr)
    {
        yield return apiClient.Post(endpoints.Post, jsonBody, onJson, onErr);
    }
}
