using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class UsersVM : MonoBehaviour
{
    public ApiClient apiClient;
    public Endpoints endpoints;
    public IEnumerator Put(string id, string payload, Action<string> onJson, Action<string> onErr)
    {
        string path = endpoints.putById.Replace("{id}", id);

        // JsonUtility BUTUH class [Serializable] → sekarang aman

        yield return apiClient.Put(path, payload, onJson, onErr);
    }
}
