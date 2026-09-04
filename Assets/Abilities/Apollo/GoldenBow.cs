using UnityEngine;

public class GoldenBow : Attack
{
    [Header("Charge Settings")]
    public float maxChargeTime = 1.5f;
    private float _chargeStartTime;
    private bool _isCharging;

    [Header("Prefabs")]
    public GameObject arrowPrefab;  // Damaging arrow variant
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

        FireArrow(chargePercent);
        StartCooldown();
        

        _isCharging = false;
    }

    private void FireArrow(float chargePercent)
    {
        
        if (playerCamera == null) return;

        Vector3 spawnPos = playerCamera.transform.position + playerCamera.transform.forward;
        GameObject arrow = Instantiate(arrowPrefab, spawnPos, playerCamera.transform.rotation);
        
        SolarArrow arrowScript = arrow.GetComponent<SolarArrow>();
        arrowScript?.SetAttacker(transform.root.gameObject);
        arrowScript?.SetCharge(chargePercent);
        
    }
}