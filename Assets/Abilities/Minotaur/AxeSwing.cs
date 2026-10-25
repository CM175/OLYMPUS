using UnityEngine;

public class AxeSwing : Attack, IInitializable
{
    [Header("Melee Combo Settings")]
    [Tooltip("The cone angle width for the axe slash (e.g., 90 degrees total front arc).")]
    [SerializeField] private float swingAngle = 90f;

    [Header("Combo Alternation Visuals")]
    [SerializeField] private GameObject vfxSwingLeftPrefab;
    [SerializeField] private GameObject vfxSwingRightPrefab;

    private bool _isLeftSwing = true;
    private Transform _rootTransform;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
    }

    protected override void Activate()
    {
        if (_rootTransform == null) _rootTransform = transform.root;

        Vector3 lookDir = playerCamera != null 
            ? playerCamera.transform.forward 
            : (movementController != null ? movementController.GetAimDirection() : _rootTransform.forward);
        lookDir.y = 0f;
        lookDir.Normalize();

        Vector3 spawnPos = (playerCamera != null)
            ? playerCamera.transform.position + (playerCamera.transform.forward * 1.5f)
            : _rootTransform.position + Vector3.up * 1.5f + (lookDir * 1.5f);

        GameObject chosenVFX = _isLeftSwing ? vfxSwingLeftPrefab : vfxSwingRightPrefab;
        if (chosenVFX == null) chosenVFX = vfxPrefab;
        if (chosenVFX != null)
        {
            Instantiate(chosenVFX, spawnPos, Quaternion.LookRotation(lookDir), _rootTransform);
        }

        _isLeftSwing = !_isLeftSwing;

        Collider[] potentialTargets = Physics.OverlapSphere(_rootTransform.position, range);
        float cosHalfAngle = Mathf.Cos(swingAngle * 0.5f * Mathf.Deg2Rad);
        var myTeam = _rootTransform.GetComponent<TeamMember>();

        foreach (var col in potentialTargets)
        {
            Transform victimRoot = col.transform.root;
            if (victimRoot == _rootTransform) continue;
            if (myTeam != null && !myTeam.IsEnemy(victimRoot.gameObject)) continue;

            Vector3 toTargetFlat = victimRoot.position - _rootTransform.position;
            toTargetFlat.y = 0f;
            if (toTargetFlat.sqrMagnitude > 0.01f)
            {
                toTargetFlat.Normalize();
                if (Vector3.Dot(lookDir, toTargetFlat) < cosHalfAngle) continue;
            }

            IDamageable targetDamage = victimRoot.GetComponent<IDamageable>() ?? col.GetComponent<IDamageable>();
            targetDamage?.TakeDamage(damage, _rootTransform.gameObject);
        }
    }
}