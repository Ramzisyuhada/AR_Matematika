using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DisableKeyboard : MonoBehaviour
{
    public TMP_InputField inputField;

    void Start()
    {
        inputField.shouldHideMobileInput = true;
        inputField.onSelect.AddListener(_ => DisableMobileKeyboard());
    }

    void DisableMobileKeyboard()
    {
        inputField.ReleaseSelection(); // mencegah keyboard sistem pop-up
    }
}
