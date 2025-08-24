using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[CreateAssetMenu(menuName = "Config/Endpoints")]

public class Endpoints : ScriptableObject
{
    public string baseUrl = "https://107-23-209-11.nip.io/api/";
    public string getById = "users/{id}";
    public string update = "users/{id}";
    public string getBy = "users";
    public string userByQuery = "/users?user_identifier={id}";
        public string answersBySubmission = "/api/answers?submission_id={id}";     // detail jawaban


}
