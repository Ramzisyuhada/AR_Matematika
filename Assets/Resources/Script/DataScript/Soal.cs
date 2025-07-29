using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;



[System.Serializable]
public class Soal 
{

    public Sprite NoSoal;
    public string Pertanyaan;

    public string Jawaban; 


    public Soal(Sprite noSoal, string pertanyaan, string jawaban)
    {
        NoSoal = noSoal;
        Pertanyaan = pertanyaan;
        Jawaban = jawaban;
    }
}
