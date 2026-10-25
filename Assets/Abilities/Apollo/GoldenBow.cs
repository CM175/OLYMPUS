using UnityEngine;

public class GoldenBow : Attack
{
    [Header("Charge Settings")]
    public float maxChargeTime = 1.5f;
    private float _chargeStartTime;
    private bool _isCharging;

    [Header("Prefabs")]
    public GameObject arrowPrefab;

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
        Transform root = transform.root;
        Vector3 spawnPos;
        Quaternion spawnRot;

        if (playerCamera != null)
        {
            spawnPos = playerCamera.transform.position + playerCamera.transform.forward;
            spawnRot = playerCamera.transform.rotation;
        }
        else
        {
            Vector3 aim = movementController != null ? movementController.GetAimDirection() : root.forward;
            spawnPos = root.position + Vector3.up * 1.5f + aim;
            spawnRot = Quaternion.LookRotation(aim);
        }

        GameObject arrow = Instantiate(arrowPrefab, spawnPos, spawnRot);
        SolarArrow arrowScript = arrow.GetComponent<SolarArrow>();
        arrowScript?.SetAttacker(root.gameObject);
        arrowScript?.SetCharge(chargePercent);
    }
}