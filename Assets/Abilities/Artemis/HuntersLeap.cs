using UnityEngine;

public class HuntersLeap : Ability, IInitializable
{
    public float leapStrength = 10f;
    public float leapHeight = 3f;
    private CharacterController _controller;

    public void Initialize(GameObject playerRoot)
    {
        _controller = playerRoot.GetComponent<CharacterController>();
    }

    protected override void Activate()
    {
        if (_controller == null) return;

        // Launch Artemis backwards and upwards
        Vector3 leapDir = -transform.root.forward * leapStrength + Vector3.up * leapHeight;
        
        // This simple version uses the existing CharacterController logic
        _controller.Move(leapDir * Time.deltaTime);

        Debug.Log("[HuntersLeap] Evasive maneuver executed.");
        StartCooldown();
    }
}