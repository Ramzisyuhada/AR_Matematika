using TMPro;
using UnityEngine;

public class NumericKeyboardSetter : MonoBehaviour
{
    public TMP_InputField input; // drag dari Inspector

    void Reset()
    {
        input = GetComponent<TMP_InputField>();
    }

    void Awake()
    {
        if (!input) return;

        // Hanya angka bulat:
        input.contentType = TMP_InputField.ContentType.IntegerNumber;

        // Paksa jenis keyboard angka (bantu beberapa keyboard OEM)
        input.keyboardType = TouchScreenKeyboardType.NumberPad;

        // Terapkan ulang setting agar langsung efektif
        input.ForceLabelUpdate();
    }
}
