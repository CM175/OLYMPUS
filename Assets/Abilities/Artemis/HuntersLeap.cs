using UnityEngine;

public class HuntersLeap : Ability, IInitializable
{
    public float leapForce = 10f;
    public float leapHeight = 0.5f;
    private CharacterController _controller;

    public void Initialize(GameObject playerRoot)
    {
        _controller = playerRoot.GetComponent<CharacterController>();
    }

    protected override void Activate()
    {
        GameObject player = transform.root.gameObject;
        PlayerController pc = player.GetComponent<PlayerController>();

        if (pc != null)
        {
            // 1. Calculate the direction (Backwards and slightly Up)
            Vector3 leapDirection = -player.transform.forward;
            leapDirection.y = leapHeight; // Add upward tilt for the "leap" feel
            

            // 2. Call a function on the PC to apply this force
            pc.ApplyImpulse(leapDirection * leapForce);
        }

        StartCooldown();
    }
}