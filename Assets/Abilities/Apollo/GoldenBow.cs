using UnityEngine;

public class GoldenBow : Attack
{
    [Header("Charge Settings")]
    public float maxChargeTime = 1.5f;
    private float _chargeStartTime;
    private bool _isCharging;

    [Header("Prefabs")]
    public GameObject lightArrowPrefab;
    public GameObject solarFlarePrefab; // Healing variant

    public override void OnPress()
    {
        // Default behavior: charge a standard damaging shot
        _isCharging = true;
        _chargeStartTime = Time.time;
    }

    public override void OnRelease()
    {
        if (!_isCharging) return;

        float chargeTime = Time.time - _chargeStartTime;
        float chargePercent = Mathf.Clamp01(chargeTime / maxChargeTime);

        if (CanUse())
        {
            FireArrow(lightArrowPrefab, chargePercent);
            StartCooldown();
        }

        _isCharging = false;
    }

    // Triggered explicitly via Alt-Fire mapping or context buttons if needed
    public void UseAltFireHealing()
    {
        if (!CanUse()) return;

        // Alternative Fire: Instantly fires a flat-rate solar healing projectile
        FireArrow(solarFlarePrefab, 0.5f);
        StartCooldown();
    }

    private void FireArrow(GameObject prefab, float chargePercent)
    {
        Vector3 spawnPos = playerCamera.transform.position + playerCamera.transform.forward;
        GameObject arrow = Instantiate(prefab, spawnPos, playerCamera.transform.rotation);
        
        SolarArrow arrowScript = arrow.GetComponent<SolarArrow>();
        if (arrowScript != null)
        {
            arrowScript.InitializeChargedProjectile(chargePercent);
        }
    }
}