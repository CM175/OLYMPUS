using System.Collections;
using UnityEngine;

public class ChronosShift : Ultimate
{
    [Header("Chronos Stats")]
    public float duration = 6f;
    public float hyperSpeedMultiplier = 2.0f;
    public float reducedBlinkCooldown = 1.0f;

    protected override void Activate()
    {
        base.Activate(); // Consumes charge
        StartCoroutine(ChronosRoutine());
    }

    private IEnumerator ChronosRoutine()
    {
        PlayerController controller = transform.root.GetComponent<PlayerController>();
        BlinkDash blink = transform.root.GetComponentInChildren<BlinkDash>();

        float originalSpeed = controller != null ? controller.SpeedMultiplier : 1f;
        float originalBlinkCD = 0f;

        // Apply Hyper-Speed
        if (controller != null) controller.SpeedMultiplier = hyperSpeedMultiplier;

        // Reduce Blink Dash cooldown
        if (blink != null)
        {
            originalBlinkCD = blink.cooldown;
            blink.cooldown = reducedBlinkCooldown;
        }

        Debug.Log("[ChronosShift] Time is mine!");

        yield return new WaitForSeconds(duration);

        // Reset Stats
        if (controller != null) controller.SpeedMultiplier = originalSpeed;
        if (blink != null) blink.cooldown = originalBlinkCD;

        Debug.Log("[ChronosShift] Time resumed.");
        StartCooldown();
    }
}