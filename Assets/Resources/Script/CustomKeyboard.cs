using UnityEngine;
using TMPro;

public class CustomKeyboard : MonoBehaviour
{
    public TMP_InputField input;

    public void AddNumber(string num)
    {
        input.text += num;
    }

    public void Delete()
    {
        if (input.text.Length > 0)
            input.text = input.text.Substring(0, input.text.Length - 1);
    }
}
