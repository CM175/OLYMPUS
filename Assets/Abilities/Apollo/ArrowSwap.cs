using UnityEngine;

public class ArrowSwap : Ability, IInitializable
{
    private GoldenBow _bow;

    public void Initialize(GameObject playerRoot)
    {
        // Automatically fetch the bow from the player layout hierarchy
        _bow = playerRoot.GetComponentInChildren<GoldenBow>();
    }

    protected override void Activate()
    {
        if (_bow != null)
        {
            _bow.ToggleArrowMode();
        }
        
        // Standard instant cast cycle execution completion
        StartCooldown();
    }
}