using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class Pertanyaan 
{
    public int Id;
    public string teksPertanyaan;
    public string opsiA;
    public string opsiB;
    public string opsiC;

    public string jawabanTerpilih;
    public Pertanyaan(string teks, string a, string b, string c)
    {
        teksPertanyaan = teks;
        opsiA = a;
        opsiB = b;
        opsiC = c;
        jawabanTerpilih = ""; 
    }
}
