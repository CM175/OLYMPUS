using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlinkDash : Ability, IInitializable
{
    public float dashSpeed = 50f;
    public float dashDuration = 0.15f;
    public bool allowVerticalDashing = true;
    public float hitSweepRadius = 2.5f;
    public int dashDamage = 25;

    // Use decoupled abstraction here instead of PlayerController
    private IMovementController _movementController;
    private Transform _rootTransform;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
        _movementController = playerRoot.GetComponent<IMovementController>();
        
        if (playerCamera == null)
        {
            playerCamera = playerRoot.GetComponentInChildren<Camera>();
        }
    }

    protected override void Activate()
    {
        if (_movementController == null)
        {
            isExecuting = false;
            return;
        }

        StartCoroutine(Dash());
    }

    private IEnumerator Dash()
    {
        int characterLayer = LayerMask.NameToLayer("Character");
        if (characterLayer != -1)
        {
            // Ignore collision dynamically during phase execution windows
            Physics.IgnoreLayerCollision(characterLayer, characterLayer, true);
        }

        _movementController.IsDashing = true;

        // FIXED: Camera directional check safe fallback routine
        Vector3 dashDirection = (playerCamera != null) ? playerCamera.transform.forward : _rootTransform.forward;

        if (!allowVerticalDashing)
        {
            dashDirection.y = 0f;
            dashDirection.Normalize();
        }

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, _rootTransform.position, Quaternion.LookRotation(dashDirection));
        }

        float elapsedTime = 0f;
        HashSet<Transform> hitTargets = new HashSet<Transform>();

        while (elapsedTime < dashDuration)
        {
            Vector3 movementThisFrame = dashDirection * dashSpeed * Time.deltaTime;
            
            // Decoupled execution call works for both AI and Human systems seamlessly
            _movementController.TeleportOrMove(movementThisFrame);

            // Dynamic Hit Detection Sweep logic safely stays intact
            Collider[] hits = Physics.OverlapSphere(_rootTransform.position, hitSweepRadius);
            foreach (var hit in hits)
            {
                Transform enemyRoot = hit.transform.root;
                var myTeam = _rootTransform.GetComponent<TeamMember>();
                
                if (myTeam != null && !myTeam.IsEnemy(enemyRoot.gameObject) || hitTargets.Contains(enemyRoot))
                {
                    continue;
                }

                IDamageable target = hit.GetComponent<Collider>()?.GetComponent<IDamageable>();
                if (target != null)
                {
                    target.TakeDamage(dashDamage);
                    hitTargets.Add(enemyRoot);
                }
            }

            elapsedTime += Time.deltaTime;
            yield return null; 
        }

        if (characterLayer != -1)
        {
            Physics.IgnoreLayerCollision(characterLayer, characterLayer, false);
        }

        _movementController.HaltVelocity();
        _movementController.IsDashing = false;
        
        StartCooldown();
    }
}