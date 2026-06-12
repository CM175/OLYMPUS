using UnityEngine;

public class GoldenBow : Attack
{
    public enum ArrowMode { Damage, Healing }

    [Header("Stance Settings")]
    public ArrowMode currentMode = ArrowMode.Damage;

    [Header("Charge Settings")]
    public float maxChargeTime = 1.5f;
    private float _chargeStartTime;
    private bool _isCharging;

    [Header("Prefabs")]
    public GameObject lightArrowPrefab;  // Damaging arrow variant
    public GameObject solarFlarePrefab;  // Healing arrow variant
    private GameObject selectedPrefab;
    public override void OnPress()
    {
        _isCharging = true;
        _chargeStartTime = Time.time;
    }

    public override void OnRelease()
    {
        if (!_isCharging) return;
        if (!CanUse()) return;
        float chargeTime = Time.time - _chargeStartTime;
        float chargePercent = Mathf.Clamp01(chargeTime / maxChargeTime);

        FireArrow(chargePercent, currentMode);
        StartCooldown();
        

        _isCharging = false;
    }

    /// <summary>
    /// Explicit function to toggle back and forth between arrow configurations.
    /// Can be bound to an ability button (e.g., PrimaryAbility or SecondaryAbility).
    /// </summary>
    public void ToggleArrowMode()
    {
        currentMode = (currentMode == ArrowMode.Damage) ? ArrowMode.Healing : ArrowMode.Damage;
        
        Debug.Log($"[GoldenBow] Apollo swapped to {currentMode} Arrows!");
        
    }

    private void FireArrow(float chargePercent, ArrowMode mode)
    {
        
        GameObject prefab = mode == ArrowMode.Healing ? solarFlarePrefab : lightArrowPrefab;
        if (prefab == null || playerCamera == null) return;

        Vector3 spawnPos = playerCamera.transform.position + playerCamera.transform.forward;
        GameObject arrow = Instantiate(prefab, spawnPos, playerCamera.transform.rotation);
        
        SolarArrow arrowScript = arrow.GetComponent<SolarArrow>();
        if (arrowScript != null)
        {
            arrowScript.SetAttacker(transform.root.gameObject);
            arrowScript.isHealingArrow = mode == ArrowMode.Healing;
            arrowScript.InitializeChargedProjectile(chargePercent);
        }
    }
}