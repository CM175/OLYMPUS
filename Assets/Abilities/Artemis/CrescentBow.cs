using UnityEngine;

public class CrescentBow : Attack
{
    [Header("Charge Settings")]
    public float maxChargeTime = 1.5f;
    private float _chargeStartTime;
    private bool _isCharging;

    [Header("Projectile")]
    public GameObject arrowPrefab;

    private bool _isUltActive = false;
    private float _ultChargeSpeedMultiplier = 1f;

    public void SetUltimateModifications(bool active, float chargeSpeedMultiplier = 2.5f)
    {
        _isUltActive = active;
        _ultChargeSpeedMultiplier = active ? chargeSpeedMultiplier : 1f;
    }

    public override void OnPress()
    {
        if (!CanUse()) return;
        _isCharging = true;
        _chargeStartTime = Time.time;
    }

    public override void OnRelease()
    {
        if (!_isCharging) return;

        float heldDuration = Time.time - _chargeStartTime;
        // Accelerate the effective charge accumulation during ultimate
        float effectiveChargeTime = heldDuration * _ultChargeSpeedMultiplier;
        float chargePercent = Mathf.Clamp01(effectiveChargeTime / maxChargeTime);

        TryFireCharged(chargePercent);
        _isCharging = false;
    }

    private void TryFireCharged(float chargePercent)
    {
        FireArrow(chargePercent);
        StartCooldown();
    }

    private void FireArrow(float chargePercent)
    {
        if (playerCamera == null) return;

        Vector3 spawnPos = playerCamera.transform.position + playerCamera.transform.forward;
        GameObject arrow = Instantiate(arrowPrefab, spawnPos, playerCamera.transform.rotation);
        
        StormArrow stormArrow = arrow.GetComponent<StormArrow>();
        if (stormArrow != null)
        {
            stormArrow.SetAttacker(transform.root.gameObject);
            stormArrow.SetCharge(chargePercent);
            stormArrow.piercesTargets = _isUltActive;
        }
    }

    public override void Cancel()
    {
        base.Cancel();
        _isCharging = false;
    }
}