using UnityEngine;

public class BlinkDash : Ability, IInitializable
{
    public float dashDistance = 10f;
    private CharacterController _controller;

    public void Initialize(GameObject playerRoot)
    {
        _controller = playerRoot.GetComponent<CharacterController>();
    }

    protected override void Activate()
    {
        if (_controller == null) return;

        Vector3 dashVector = _controller.transform.forward * dashDistance;
        
        _controller.enabled = false;
        _controller.transform.position += dashVector;
        _controller.enabled = true;

        StartCooldown();
    }
}