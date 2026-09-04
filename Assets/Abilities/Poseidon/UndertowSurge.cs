using System.Collections;
using UnityEngine;

public class UndertowSurge : Ability, IInitializable
{
    [Header("Dash Settings")]
    public float dashSpeed = 22f;
    public float dashDuration = 0.45f;

    [Header("Emerge Impact")]
    public float popUpRadius = 4.5f;
    public int emergeDamage = 30;
    public float verticalImpulse = 8f;
    public float interruptStun = 0.2f;

    private IMovementController _movement;
    private Transform _rootTransform;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
        _movement = playerRoot.GetComponent<IMovementController>();
    }

    protected override void Activate()
    {
        if (_movement == null)
        {
            isExecuting = false;
            return;
        }
        StartCoroutine(SurgeRoutine());
    }

    private IEnumerator SurgeRoutine()
    {
        _movement.IsDashing = true;
        int charLayer = LayerMask.NameToLayer("Character");
        if (charLayer != -1) Physics.IgnoreLayerCollision(charLayer, charLayer, true);

        Vector3 moveDir = _movement.GetAimDirection();
        moveDir.y = 0f;
        moveDir.Normalize();

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            _movement.Move(moveDir * (dashSpeed * Time.deltaTime));
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (charLayer != -1) Physics.IgnoreLayerCollision(charLayer, charLayer, false);
        _movement.HaltVelocity();
        _movement.IsDashing = false;

        ExecuteEmergeImpact();
        StartCooldown();
    }

    private void ExecuteEmergeImpact()
    {
        Collider[] victims = Physics.OverlapSphere(_rootTransform.position, popUpRadius);
        TeamMember myTeam = _rootTransform.GetComponent<TeamMember>();

        foreach (var victim in victims)
        {
            Transform tRoot = victim.transform.root;
            if (tRoot == _rootTransform) continue;
            if (myTeam != null && !myTeam.IsEnemy(tRoot.gameObject)) continue;

            IDamageable dmg = tRoot.GetComponent<IDamageable>() ?? tRoot.GetComponentInChildren<IDamageable>();
            dmg?.TakeDamage(emergeDamage);

            IMovementController move = tRoot.GetComponent<IMovementController>();
            if (move != null)
            {
                move.HaltVelocity();
                move.Stun(interruptStun);
                move.ApplyImpulseForce(Vector3.up * verticalImpulse);
            }
        }

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, _rootTransform.position, Quaternion.identity);
        }
    }
}