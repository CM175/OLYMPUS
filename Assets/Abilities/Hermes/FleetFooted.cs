using UnityEngine;

public class FleetFooted : PassiveAbility, IInitializable
{
    public float movementSpeedBonus = 1.25f; // 25% faster movement
    private PlayerController _controller;

    public void Initialize(GameObject playerRoot)
    {
        _controller = playerRoot.GetComponent<PlayerController>();
    }

    protected override void Activate()
    {
        if (_controller != null)
            _controller.SpeedMultiplier = movementSpeedBonus;
    }
}