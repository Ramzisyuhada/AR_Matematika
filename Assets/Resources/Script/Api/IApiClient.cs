using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IApiClient 
{
    IEnumerator GetById(string path, Action<string> onSuccess, Action<string> onError);
    IEnumerator Put(string path, string jsonBody, Action<string> onSuccess, Action<string> onError);
    IEnumerator Get( Action<string> onSuccess, Action<string> onError);

}
