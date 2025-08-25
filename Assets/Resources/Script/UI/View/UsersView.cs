using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

public class UsersView : MonoBehaviour
{
    [Header("Logic")]
    [SerializeField] UsersVM usersVM;


    public void SetAngket(string angket)
    {
        string ID = PlayerPrefs.GetString("user_identifier", "1729910");
        Debug.Log("ID : " + ID);
        Debug.Log("Angket : " + angket  );
        string jsonBody = JsonConvert.SerializeObject(new
        {
            gayabelajar = angket,

        });
        StartCoroutine(usersVM.Put(ID, jsonBody , onJson: res => {
            Debug.Log("Berhasil");


        }, onErr : Err =>
        {
            Debug.LogWarning("Error : " + Err);

        }));

    }


}
