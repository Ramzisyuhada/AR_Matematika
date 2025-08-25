using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class SubmissionsVM : MonoBehaviour
{
    public ApiClient apiClient;
    public Endpoints endpoints;
    public IEnumerator Post(string jsonBody, Action<string> onJson, Action<string> onErr)
    {
        yield return apiClient.Post(endpoints.Post, jsonBody, onJson, onErr);
    }
}
