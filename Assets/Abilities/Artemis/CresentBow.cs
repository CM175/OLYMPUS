using UnityEngine;

public class CrescentBow : Ability, IInitializable
{
    [Header("Bow Stats")]
    public GameObject arrowPrefab;
    public float maxChargeTime = 0.8f;
    
    private PlayerController _controller;
    private float _currentCharge = 0f;
    private bool _isCharging = false;

    public void Initialize(GameObject playerRoot)
    {
        _controller = playerRoot.GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (_isCharging)
        {
            // Add the time passed since the last frame
            _currentCharge += Time.deltaTime;

            // Clamp the value so it doesn't exceed your defined maxChargeTime
            if (_currentCharge > maxChargeTime)
            {
                _currentCharge = maxChargeTime;
            }
        }
        
    }
    public void StartCharging()
    {
        // Don't start charging if we can't use the ability yet
        if (!CanUse()) return; 
        
        _controller.SpeedMultiplier *= 0.75f;
        _isCharging = true;
        _currentCharge = 0f;
    }

    protected override void Activate()
    {
        // Prevent firing if we weren't actually charging (e.g., button released during cooldown)
        if (!_isCharging) 
        {
            isExecuting = false; 
            return;
        }

        _isCharging = false;
        float chargeRatio = _currentCharge / maxChargeTime;

        FireArrow(chargeRatio);
        _controller.SpeedMultiplier /=0.75f;
        _currentCharge = 0f;
        StartCooldown();
    }

    private void FireArrow(float charge)
    {
        if (playerCamera == null) return;

        // Instantiate at the camera position
        GameObject arrow = Instantiate(arrowPrefab, playerCamera.transform.position + playerCamera.transform.forward, playerCamera.transform.rotation);
        
        // Get the specific Hanzo-style arrow component
        StormArrow stormArrow = arrow.GetComponent<StormArrow>();
        
        if (stormArrow != null)
        {
            // Pass the charge ratio (0.0 to 1.0) to the arrow
            stormArrow.SetCharge(charge);
        }
        
        Debug.Log($"[CrescentBow] Fired with {charge * 100}% charge");
    }
}