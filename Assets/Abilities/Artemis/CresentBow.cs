using UnityEngine;

public class CrescentBow : Ability
{
    [Header("Bow Stats")]
    public GameObject arrowPrefab;
    public float maxChargeTime = 1.2f;
    public float minDamage = 25f;
    public float maxDamage = 80f;
    public float projectileSpeed = 50f;

    private float _currentCharge = 0f;
    private bool _isCharging = false;

    // Called by the player's input system
    public void StartCharging() => _isCharging = true;

    private void Update()
    {
        if (_isCharging)
        {
            _currentCharge = Mathf.Min(_currentCharge + Time.deltaTime, maxChargeTime);
        }
    }

    protected override void Activate()
    {
        _isCharging = false;
        float chargeRatio = _currentCharge / maxChargeTime;
        float finalDamage = Mathf.Lerp(minDamage, maxDamage, chargeRatio);

        FireArrow(finalDamage);
        
        _currentCharge = 0f;
        StartCooldown();
    }

    private void FireArrow(float dmg)
    {
        if (playerCamera == null) return;

        GameObject arrow = Instantiate(arrowPrefab, playerCamera.transform.position + playerCamera.transform.forward, playerCamera.transform.rotation);
        // Assuming your projectile script has a 'damage' field
        // arrow.GetComponent<Projectile>().damage = dmg; 
        
        Debug.Log($"[CrescentBow] Fired arrow for {dmg} damage.");
    }
}