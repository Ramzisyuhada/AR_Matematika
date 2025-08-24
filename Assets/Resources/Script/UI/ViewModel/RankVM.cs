using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class RankVM : MonoBehaviour
{
    public ApiClient apiClient;
    public Endpoints endpoints;
    [SerializeField] private string tokenBearer = ""; // isi kalau butuh Authorization
    public IEnumerator LoadGrade(string path, Action<string> onJson, Action<string> onErr)
    {
        string url = endpoints.userByQuery.Replace("{id}", UnityWebRequest.EscapeURL(path));
        yield return apiClient.GetById(url, onJson, onErr);
    }


}
