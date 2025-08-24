using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class AnswerVM : MonoBehaviour
{
    public ApiClient apiClient;
    public Endpoints endpoints;
    [SerializeField] private string tokenBearer = ""; // isi kalau butuh Authorization
    public IEnumerator LoadAnswerById(string path, Action<string> onJson, Action<string> onErr)
    {
        string url = endpoints.getById.Replace("{id}", path.ToString());
        yield return apiClient.GetById(url, onJson, onErr);
    }

}
