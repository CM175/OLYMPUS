using UnityEngine;

public class CrescentBow : Attack
{
    [Header("Charge Settings")]
    public float maxChargeTime = 2f;
    private float chargeStartTime;
    private bool isCharging;

    [Header("Projectile")]
    public GameObject arrowPrefab;

    // Inside CrescentBow.cs
    private bool _isUltActive = false;

    public void SetUltimateModifications(bool active)
    {
        _isUltActive = active;
        isChargeable = !active; 
    }


    public override void OnPress()
    {
        
        if (_isUltActive)
        {
            FireArrow(1.0f);
            return;
        }
        
        isCharging = true;
        chargeStartTime = Time.time;
    }

    public override void OnRelease()
    {
        if (!isCharging) return;

        float chargeTime = Time.time - chargeStartTime;
        float chargePercent = Mathf.Clamp01(chargeTime / maxChargeTime);

        TryFireCharged(chargePercent);

        isCharging = false;
    }

    private void TryFireCharged(float chargePercent)
    {
        if (!CanUse()) return;

        FireArrow(chargePercent);
        StartCooldown();
    }

    private void FireArrow(float chargePercent)
    {
        GameObject arrow = Instantiate(arrowPrefab, playerCamera.transform.position + playerCamera.transform.forward, playerCamera.transform.rotation);
        StormArrow stormArrow = arrow.GetComponent<StormArrow>();
        stormArrow.SetAttacker(transform.root.gameObject); 
        if (stormArrow != null)
        {
            
            //Debug.Log($"Storm Arrow");
            // Pass the charge ratio (0.0 to 1.0) to the arrow
            stormArrow.SetCharge(chargePercent);
            stormArrow.piercesTargets = _isUltActive;
        }

        //Debug.Log($"Fired bow with {chargePercent * 100f}% charge");
    }

    
    
}