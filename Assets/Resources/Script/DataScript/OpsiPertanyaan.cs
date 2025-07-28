using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class OpsiPertanyaan
{
    public int Id;
    public string teksPertanyaan;
    public string OpsiJawabanA;
    public string OpsiJawabanB;
    public string OpsiJawabanC;
    public string jawabanTerpilih;

    public OpsiPertanyaan(int id, string teksPertanyaan, string opsiA, string opsiB, string opsiC)
    {
        this.Id = id;
        this.teksPertanyaan = teksPertanyaan;
        this.OpsiJawabanA = opsiA;
        this.OpsiJawabanB = opsiB;
        this.OpsiJawabanC = opsiC;
        this.jawabanTerpilih = ""; // Kosong dulu, belum dijawab
    }


}
