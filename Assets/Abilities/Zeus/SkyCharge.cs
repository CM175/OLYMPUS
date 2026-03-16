using UnityEngine;

public class SkyCharge : Ability
{
    public float dashDistance = 8f;

    protected override void Activate()
    {
        CharacterController controller = FindFirstObjectByType<CharacterController>();

        if (controller == null)
        {
            Debug.LogError("SkyCharge: No CharacterController found.");
            return;
        }

        Vector3 origin = controller.transform.position;
        Vector3 forward = controller.transform.forward;
        Vector3 dashTarget = origin + forward * dashDistance;


        if (Physics.Raycast(origin, forward, out RaycastHit hit, dashDistance))
            dashTarget = hit.point - forward * 0.5f;

        controller.enabled = false;
        controller.transform.position = dashTarget;
        controller.enabled = true;

        
        StartCooldown();
    }
}