using System;
using UnityEngine;

public class ShadowedEnabledToggleProbe : MonoBehaviour
{
    // The Inspector controls Behaviour.enabled, not an unrelated shadowing property.
    public new bool enabled
    {
        get => throw new InvalidOperationException("The shadowing getter must not be used.");
        set => throw new InvalidOperationException("The shadowing setter must not be used.");
    }

    private void Update()
    {
    }
}
