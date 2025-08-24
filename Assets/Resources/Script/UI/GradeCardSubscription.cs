using UnityEngine;
using System;

public class GradeCardSubscription : MonoBehaviour
{
    private GradeCard _card;
    private Action<string> _handler;

    public void Attach(GradeCard card, Action<string> handler)
    {
        _card = card;
        _handler = handler;
        _card.OnOpenDetail += _handler;
    }

    private void OnDestroy()
    {
        if (_card != null && _handler != null)
            _card.OnOpenDetail -= _handler;
    }
}
