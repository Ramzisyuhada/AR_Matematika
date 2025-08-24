using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class APIManager : MonoBehaviour
{

   
    private string apiUrl = "https://107-23-209-11.nip.io/api/";
    public string EndPoint;  

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(GetData());

    }
    IEnumerator GetData()
    {
        string url = apiUrl + EndPoint;
        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Response: " + request.downloadHandler.text);
        }
        else
        {
            Debug.LogError("Error: " + request.error);
        }
    }
  
}
