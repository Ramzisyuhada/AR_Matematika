using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ApiClient : MonoBehaviour,IApiClient
{
    [SerializeField] Endpoints endpoints;
    [SerializeField] string bearerToken; // opsional

    public IEnumerator GetById(string path, Action<string> ok, Action<string> err)
    {
        Debug.Log(path);
        using var req = UnityWebRequest.Get(endpoints.baseUrl + path);
        if (!string.IsNullOrEmpty(bearerToken))
            req.SetRequestHeader("Authorization", "Bearer " + bearerToken);

        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success) ok?.Invoke(req.downloadHandler.text);
        else err?.Invoke($"{req.responseCode} | {req.error}");
    }

    public IEnumerator Get(Action<string> ok, Action<string> err)
    {
        Debug.Log(endpoints.baseUrl + endpoints.getBy);
        using var req = UnityWebRequest.Get(endpoints.baseUrl + endpoints.getBy );
        if (!string.IsNullOrEmpty(bearerToken))
            req.SetRequestHeader("Authorization", "Bearer " + bearerToken);

        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success) ok?.Invoke(req.downloadHandler.text);
        else err?.Invoke($"{req.responseCode} | {req.error}");
    }


    public IEnumerator Put(string path, string json, Action<string> ok, Action<string> err)
    {
        using var req = new UnityWebRequest(endpoints.baseUrl + path, "PUT");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(bearerToken))
            req.SetRequestHeader("Authorization", "Bearer " + bearerToken);

        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success) ok?.Invoke(req.downloadHandler.text);
        else err?.Invoke($"{req.responseCode} | {req.error}");
    }
}
