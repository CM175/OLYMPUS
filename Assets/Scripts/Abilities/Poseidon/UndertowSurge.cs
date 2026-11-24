using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;

public class UntertowSurge : Ability, IInitializable
{
    [Header("Surge Movement Settings")]
    [SerializeField] private float surgeSpeed = 25f;
    [SerializeField] private float surgeDuration = 2f;
    [SerializeField] private float sweepRadius = 2.5f;
    [SerializeField] private float turnRate = 180f;

    [Header("Wave Surfing Setup")]
    [SerializeField] private GameObject tidalWavePrefab; // Assign TidalWave.prefab here
    [SerializeField] private Transform poseidonVisualModel; // Assign Poseidon's mesh/rig child object here
    [SerializeField] private float rideHeight = 1.8f; // Matches TidalWaveMeshGenerator height (2.0)
    [SerializeField] private Vector3 waveGroundOffset = new Vector3(0f, 0f, 0.5f); // Slightly forward so he stands on the crest

    [Header("Displacement & Damage")]
    [SerializeField] private int surgeDamage = 30;
    [SerializeField] private float pushForce = 20f;

    [Header("Visuals")]
    [SerializeField] private GameObject wallSplashVFXPrefab;

    private Transform _rootTransform;
    private IMovementController _movementController;
    private GameObject _activeWaveInstance;
    private Vector3 _originalVisualLocalPos;
    private bool _hasSavedVisualPos;

    public void Initialize(GameObject playerRoot)
    {
        _rootTransform = playerRoot.transform;
        _movementController = playerRoot.GetComponent<IMovementController>();
        poseidonVisualModel = _rootTransform.Find("Poseidon(Clone)/PoseidonModel");
        
        if (poseidonVisualModel != null)
        {
            _originalVisualLocalPos = poseidonVisualModel.localPosition;
            _hasSavedVisualPos = true;
        }
    }

    protected override void Activate()
    {
        if (_movementController == null)
        {
            isExecuting = false;
            return;
        }
        StartCoroutine(SurgeRoutine());
    }

    private IEnumerator SurgeRoutine()
    {
        _movementController.IsDashing = true;

        int charLayer = LayerMask.NameToLayer("Character");
        if (charLayer != -1) Physics.IgnoreLayerCollision(charLayer, charLayer, true);

        Vector3 surgeDirection = _movementController.GetAimDirection();
        surgeDirection.y = 0f;
        surgeDirection = surgeDirection.sqrMagnitude > 0.001f ? surgeDirection.normalized : _rootTransform.forward;

        // 1. Spawn the TidalWave at ground level under his feet
        SetPoseidonRideHeight(SpawnSurfWave(surgeDirection) - 0.2f);

        float elapsed = 0f;
        HashSet<Transform> sweptTargets = new HashSet<Transform>();
        var myTeam = _rootTransform.GetComponent<TeamMember>();

        while (elapsed < surgeDuration)
        {
            Vector3 targetDirection = _movementController.GetAimDirection();
            targetDirection.y = 0f;
            if (targetDirection.sqrMagnitude > 0.001f)
            {
                targetDirection.Normalize();
                float step = turnRate * Mathf.Deg2Rad * Time.deltaTime;
                surgeDirection = Vector3.RotateTowards(surgeDirection, targetDirection, step, 0.0f);
            }

            _rootTransform.rotation = Quaternion.LookRotation(surgeDirection);

            Vector3 moveStep = surgeSpeed * Time.deltaTime * surgeDirection;
            _movementController.Move(moveStep);

            Collider[] hits = AbilityPhysics.OverlapSphere(_rootTransform.position + Vector3.up * 0.5f, sweepRadius);
            foreach (var hit in hits)
            {
                Transform enemyRoot = hit.transform.root;
                if (enemyRoot == _rootTransform) continue;
                if (myTeam != null && !myTeam.IsEnemy(enemyRoot.gameObject)) continue;

                IDamageable targetDmg = enemyRoot.GetComponent<IDamageable>() ?? enemyRoot.GetComponentInChildren<IDamageable>();
                IMovementController targetMove = enemyRoot.GetComponent<IMovementController>();

                if (targetDmg != null && !sweptTargets.Contains(enemyRoot))
                {
                    sweptTargets.Add(enemyRoot);
                    Deal(targetDmg, surgeDamage);
                }

                if (targetMove != null)
                {
                    targetMove.HaltVelocity();
                    ApplyDisplacement(targetMove, enemyRoot);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        CleanupSurge(charLayer);
        StartCooldown();
    }

    private float SpawnSurfWave(Vector3 direction)
    {
        if (tidalWavePrefab == null) return 0f;

        // Parent to _rootTransform so it stays locked beneath him as he steers
        _activeWaveInstance = Instantiate(tidalWavePrefab, _rootTransform);
        _activeWaveInstance.transform.localPosition = waveGroundOffset;
        _activeWaveInstance.transform.localRotation = Quaternion.identity;
        _activeWaveInstance.transform.localScale *= 0.5f; 

        // Disable independent projectile physics so it rides with Poseidon
        if (_activeWaveInstance.TryGetComponent<TidalWaveProjectile>(out var projectile))
        {
            projectile.enabled = false;
        }
        if (_activeWaveInstance.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
        }
        if (_activeWaveInstance.TryGetComponent<Collider>(out var col))
        {
            col.enabled = false;
        }

        if (_activeWaveInstance.TryGetComponent<TidalWaveMeshGenerator>(out var meshGen))
        {
            rideHeight = meshGen.height;
        }

        return waveGroundOffset.y + (rideHeight * _activeWaveInstance.transform.localScale.y);
    }

    private void SetPoseidonRideHeight(float heightOffset)
    {
        if (poseidonVisualModel == null) return;

        if (!_hasSavedVisualPos)
        {
            _originalVisualLocalPos = poseidonVisualModel.localPosition;
            _hasSavedVisualPos = true;
        }

        poseidonVisualModel.localPosition = _originalVisualLocalPos + Vector3.up * heightOffset;
    }

    private void CleanupSurge(int charLayer)
    {
        if (charLayer != -1) Physics.IgnoreLayerCollision(charLayer, charLayer, false);

        // Return Poseidon's visual model back to ground level
        if (poseidonVisualModel != null && _hasSavedVisualPos)
        {
            poseidonVisualModel.localPosition = _originalVisualLocalPos;
        }

        if (_activeWaveInstance != null)
        {
            Destroy(_activeWaveInstance);
            _activeWaveInstance = null;
        }

        if (_movementController != null)
        {
            _movementController.HaltVelocity();
            _movementController.IsDashing = false;
        }
    }

    private void OnDisable()
    {
        CleanupSurge(LayerMask.NameToLayer("Character"));
    }

    private void OnDestroy()
    {
        if (_activeWaveInstance != null)
        {
            Destroy(_activeWaveInstance);
            _activeWaveInstance = null;
        }
    }

    public override void Cancel()
    {
        base.Cancel();
        StopAllCoroutines();

        int charLayer = LayerMask.NameToLayer("Character");
        CleanupSurge(charLayer);

        StartCooldown();
    }

    private void ApplyDisplacement(IMovementController targetMovement, Transform targetTransform)
    {
        Vector3 forward = _rootTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 toEnemy = targetTransform.position - _rootTransform.position;
        toEnemy.y = 0f;

        Vector3 sideDir = Vector3.Cross(forward, Vector3.up).normalized;

        if (toEnemy.sqrMagnitude > 0.001f && Vector3.Dot(toEnemy.normalized, sideDir) < 0)
        {
            sideDir = -sideDir;
        }

        Vector3 impulse = sideDir * pushForce;
        targetMovement.HaltVelocity();
        targetMovement.ApplyImpulseForce(impulse);
    }
}