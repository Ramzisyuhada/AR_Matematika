using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SubmissionsView : MonoBehaviour
{

    [Header("Logic")]
    [SerializeField] private SubmissionsVM ViewModel;
    private string AssId;
    private string UserID;
    [System.Serializable]
    private class PendingAnswer
    {
        public string user_identifier;
        public string assessment_id;
    }
    private void Start()
    {
        AssId = PlayerPrefs.GetString("assessment_id", "A_001");
        UserID = PlayerPrefs.GetString("user_identifier", "Tidak Ada");
    }
    public IEnumerator PostSubs(Action<string> onDone = null)
    {
        var payload = new
        {
            user_identifier = UserID,
            assessment_id = AssId,
        };
        string body = JsonConvert.SerializeObject(payload);

        yield return ViewModel.Post(
            body,
            onJson: res => {
                try
                {
                    var root = JsonConvert.DeserializeObject<Dictionary<string, object>>(res);
                    
                    if (root.ContainsKey("data"))
                    {

                        
                        var dataJson = root["data"].ToString();
                        var dataObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(dataJson);
                        if (dataObj.ContainsKey("submission_id"))
                        {
                            string newId = dataObj["submission_id"].ToString();
                            Debug.Log($"✓ Submit OK, SubmissionID: {newId}");
                            onDone?.Invoke(newId);
                            return;
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Data Kosong");
                    }
                    onDone?.Invoke(null);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Parsing error: " + ex.Message);
                    onDone?.Invoke(null);
                }
            },
            onErr: err => {
                Debug.LogWarning("Gagal buat submission: " + err);
                onDone?.Invoke(null);
            }
        );
    }


}
