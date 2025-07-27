using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[System.Serializable]
public class Angket 
{
    public string Judul;
    public List<Pertanyaan> PertanyaanList;

    public Angket()
    {
        PertanyaanList = new List<Pertanyaan>();
    }

}
