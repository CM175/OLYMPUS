using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkyCharge : Ability, IInitializable
{    public float dashSpeed = 50f;
    
    public float dashDuration = 0.15f;
    public bool allowVerticalDashing = true;

    public float hitSweepRadius = 2.5f;
    public int dashDamage = 25;

    private PlayerController _playerController;
    private CharacterController _characterController;

    public void Initialize(GameObject playerRoot)
    {
        _playerController = playerRoot.GetComponent<PlayerController>();
        _characterController = playerRoot.GetComponent<CharacterController>();
        
        // Grab the camera from the player root if it isn't assigned via inspector
        if (playerCamera == null)
        {
            playerCamera = playerRoot.GetComponentInChildren<Camera>();
        }
    }

    protected override void Activate()
    {
        if (_playerController == null || _characterController == null)
        {
            isExecuting = false;
            return;
        }

        StartCoroutine(Dash());
    }

    private IEnumerator Dash()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        // 2. TURN OFF physics contact between Player and Enemy layers
        if (playerLayer != -1 && enemyLayer != -1)
        {
            Physics.IgnoreLayerCollision(playerLayer, enemyLayer, true);
        }

        // 1. Lock out standard WASD / Gravity controls
        _playerController.IsDashing = true;

        // 2. Determine dash trajectory based on crosshair (playerCamera)
        Vector3 dashDirection = playerCamera != null ? playerCamera.transform.forward : _characterController.transform.forward;

        if (!allowVerticalDashing)
        {
            dashDirection.y = 0f;
            dashDirection.Normalize();
        }

        // Optional: Instantiated visual effects right at start position
        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, _characterController.transform.position, Quaternion.LookRotation(dashDirection));
        }

        float elapsedTime = 0f;
        HashSet<Transform> hitTargets = new HashSet<Transform>();

        // 3. Execution Translation Loop
        while (elapsedTime < dashDuration)
        {
            // Lock movement to a pure translation vector ignoring physics engine drag/gravity
            Vector3 movementThisFrame = dashDirection * dashSpeed * Time.deltaTime;
            _characterController.Move(movementThisFrame);

            // Dynamic Hit Detection Sweep (Pass-through damage engine)
            Vector3 sweepCenter = _characterController.transform.position + _characterController.center;
            Collider[] hits = Physics.OverlapSphere(sweepCenter, hitSweepRadius);
            
            foreach (var hit in hits)
            {
                Transform enemyRoot = hit.transform.root;
                
                if (enemyRoot == _characterController.transform || hitTargets.Contains(enemyRoot)) 
                    continue;

                IDamageable target = hit.GetComponent<Collider>().GetComponent<IDamageable>();
                if (target != null)
                {
                    target.TakeDamage(dashDamage);
                    hitTargets.Add(enemyRoot);
                    
                }
            }

            elapsedTime += Time.deltaTime;
            yield return null; 
        }

        // 4. --- DASH COMPLETE ---
        // TURN ON physics contact back so they solid-collide again
        if (playerLayer != -1 && enemyLayer != -1)
        {
            Physics.IgnoreLayerCollision(playerLayer, enemyLayer, false);
        }

        _playerController.SetVelocity(Vector3.zero);

        // Restore control states
        _playerController.IsDashing = false;
        
        // 5. Complete Ability lifecycle inside your system architecture
        StartCooldown();
    }
}