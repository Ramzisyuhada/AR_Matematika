using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AssessmentVM : MonoBehaviour
{

    public ApiClient apiClient;
    public Endpoints endpoints;
    
    public IEnumerator Put(string id , string payload , Action<string> onJson, Action<string> onErr)
    {
        string path = endpoints.update.Replace("{id}", id);
        yield return apiClient.Put(path, payload, onJson, onErr);

    }

    public IEnumerator Get(string id, Action<string> onJson, Action<string> onErr)
    {
        string path = endpoints.update.Replace("{id}", id);
        yield return apiClient.GetById(path, onJson, onErr);

    }

    public IEnumerator Post(string jsonBody , Action<string> onJson, Action<string> onErr)
    {
        yield return apiClient.Post(endpoints.Post, jsonBody, onJson, onErr);

    }
}
