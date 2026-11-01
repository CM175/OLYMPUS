using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkyCharge : Ability, IInitializable
{
    public float dashSpeed = 50f;
    public float dashDuration = 0.15f;
    public bool allowVerticalDashing = true;
    public float hitSweepRadius = 2.5f;
    public int dashDamage = 25;

    private IMovementController _movementController;
    private Transform _rootTransform;

    // Layer collision is a global physics setting: count active dashes so one dash ending (or being cancelled)
    // cannot re-enable collisions mid-dash for another character, and a cancelled dash cannot leave it off forever.
    private static int _activeDashes;
    private bool _dashCounted;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
        _movementController = playerRoot.GetComponent<IMovementController>();
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
        BeginDashCollisionIgnore();

        _movementController.IsDashing = true;

        // OPTIMIZED: Unified vector query through the controller interface layer
        Vector3 dashDirection = _movementController.GetAimDirection();

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
            _movementController.Move(movementThisFrame);

            Collider[] hits = Physics.OverlapSphere(_rootTransform.position, hitSweepRadius);
            foreach (var hit in hits)
            {
                Transform enemyRoot = hit.transform.root;
                var myTeam = _rootTransform.GetComponent<TeamMember>();
                
                if (myTeam != null && !myTeam.IsEnemy(enemyRoot.gameObject) || hitTargets.Contains(enemyRoot))
                {
                    continue;
                }

                IDamageable target = enemyRoot.GetComponent<IDamageable>() ?? enemyRoot.GetComponentInChildren<IDamageable>();
                if (target != null)
                {
                    Deal(target, dashDamage);
                    hitTargets.Add(enemyRoot);
                }
            }

            elapsedTime += Time.deltaTime;
            yield return null; 
        }

        EndDashCollisionIgnore();

        _movementController.HaltVelocity();
        _movementController.IsDashing = false;
        
        StartCooldown();
    }

    private void BeginDashCollisionIgnore()
    {
        if (_dashCounted) return;
        _dashCounted = true;
        _activeDashes++;
        int layer = LayerMask.NameToLayer("Character");
        if (layer != -1) Physics.IgnoreLayerCollision(layer, layer, true);
    }

    private void EndDashCollisionIgnore()
    {
        if (!_dashCounted) return;
        _dashCounted = false;
        _activeDashes = Mathf.Max(0, _activeDashes - 1);
        if (_activeDashes > 0) return;
        int layer = LayerMask.NameToLayer("Character");
        if (layer != -1) Physics.IgnoreLayerCollision(layer, layer, false);
    }

    public override void Cancel()
    {
        base.Cancel();
        EndDashCollisionIgnore();
    }

    private void OnDisable()
    {
        EndDashCollisionIgnore();
    }
}