using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AIMovementController : MonoBehaviour, IMovementController
{
    [Header("Physics & Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float horizontalDrag = 8f;
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Header("NavMesh Recovery")]
    [SerializeField] private float navMeshRecoveryRadius = 1.0f;
    [SerializeField] private float groundCheckTolerance = 0.25f;
    [SerializeField] private float aimErrorAngle = 5f;

    private NavMeshAgent _agent;
    private RLEnemyCombatAgent _enemyAI;
    private float _baseSpeed;
    private Vector3 _velocity;
    private Coroutine _stunCoroutine;
    private bool _isDashing;
    private bool _isStunned;
    private bool _isUnderExternalForce;

    public bool IsDashing
    {
        get => _isDashing;
        set
        {
            if (_isDashing == value) return;
            _isDashing = value;
            if (!_isDashing) SyncAgentToTransform();
            EvaluateAgentControl();
        }
    }

    public bool IsUnderExternalForce
    {
        get => _isUnderExternalForce;
        set
        {
            if (_isUnderExternalForce == value) return;
            _isUnderExternalForce = value;
            if (_isUnderExternalForce)
            {
                if (_agent != null) _agent.enabled = false;
            }
            else EndExternalForce();
        }
    }

    public bool IsStunned => _isStunned;
    public bool IsGrounded => CheckGrounded();
    private float _speedMultiplier = 1f;
    private float _gravityMultiplier = 1f;

    public float SpeedMultiplier
    {
        get => _speedMultiplier;
        set
        {
            _speedMultiplier = value;
            if (_agent != null) _agent.speed = _baseSpeed * _speedMultiplier;
        }
    }

    private Coroutine _slowCoroutine;
    private float _preSlowMultiplier = 1f;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _enemyAI = GetComponent<RLEnemyCombatAgent>();
        if (_agent != null) _baseSpeed = _agent.speed;
    }

    private void Update()
    {
        if (_agent == null) return;
        if (!CheckGrounded() && !_agent.isOnNavMesh)
            IsUnderExternalForce = true;
        if (_isUnderExternalForce) ApplyAirPhysics();
    }

    public void ApplyImpulseForce(Vector3 force)
    {
        _velocity += force;
        IsUnderExternalForce = true;
    }

    public void HaltVelocity()
    {
        _velocity = Vector3.zero;
        if (IsGrounded) SyncAgentToTransform();
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            _agent.ResetPath();
    }

    /// <summary>Hard reset used by the training respawn system.</summary>
    public void ResetForTraining(Vector3 position, Quaternion rotation)
    {
        if (_stunCoroutine != null)
        {
            StopCoroutine(_stunCoroutine);
            _stunCoroutine = null;
        }
        if (_slowCoroutine != null)
        {
            StopCoroutine(_slowCoroutine);
            _slowCoroutine = null;
        }

        _isStunned = false;
        _isDashing = false;
        _isUnderExternalForce = false;
        _velocity = Vector3.zero;
        _gravityMultiplier = 1f;
        _speedMultiplier = 1f;
        _preSlowMultiplier = 1f;

        if (_agent != null)
        {
            if (!_agent.enabled) _agent.enabled = true;
            _agent.speed = _baseSpeed;
        }

        transform.SetPositionAndRotation(position, rotation);
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshRecoveryRadius + 4f, NavMesh.AllAreas))
            transform.position = hit.position;

        if (_agent != null)
        {
            if (!_agent.enabled) _agent.enabled = true;
            if (_agent.isOnNavMesh) _agent.Warp(transform.position);
            else _agent.Warp(transform.position);
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
            _agent.isStopped = false;
            _agent.updatePosition = true;
        }
    }

    public void Move(Vector3 displacement)
    {
        if (_isUnderExternalForce || _isDashing) transform.position += displacement;
        else if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.Move(displacement);
    }

    public void Teleport(Vector3 position)
    {
        if (_agent != null && _agent.enabled) _agent.Warp(position);
        else transform.position = position;
    }

    public void Stun(float duration)
    {
        if (_stunCoroutine != null) StopCoroutine(_stunCoroutine);
        _stunCoroutine = StartCoroutine(StunRoutine(duration));
    }

    public Vector3 GetAimDirection() => transform.forward;

    private void ApplyAirPhysics()
    {
        if (!IsGrounded || _velocity.y > 0f)
            _velocity.y += gravity * _gravityMultiplier * Time.deltaTime;

        Vector3 horizontalVelocity = new Vector3(_velocity.x, 0f, _velocity.z);
        horizontalVelocity = Vector3.Lerp(horizontalVelocity, Vector3.zero, horizontalDrag * Time.deltaTime);
        _velocity = new Vector3(horizontalVelocity.x, _velocity.y, horizontalVelocity.z);
        Vector3 displacement = _velocity * Time.deltaTime;

        if (_velocity.y < 0f)
        {
            float downDistance = Mathf.Abs(displacement.y) + groundCheckTolerance;
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit groundHit,
                downDistance + 0.5f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                transform.position = new Vector3(transform.position.x + displacement.x, groundHit.point.y,
                    transform.position.z + displacement.z);
                _velocity.y = 0f;
                IsUnderExternalForce = false;
                return;
            }
        }

        if (displacement.sqrMagnitude > 0.0001f)
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            if (Physics.SphereCast(origin, _agent.radius, displacement.normalized, out RaycastHit hit,
                displacement.magnitude, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                displacement = Vector3.ProjectOnPlane(displacement, hit.normal);
                _velocity = Vector3.ProjectOnPlane(_velocity, hit.normal);
            }
        }

        transform.position += displacement;
        if (_velocity.magnitude < 0.01f && _velocity.y <= 0f && IsGrounded)
            IsUnderExternalForce = false;
    }

    private void EndExternalForce()
    {
        _velocity = Vector3.zero;
        if (_agent != null)
        {
            _agent.enabled = true;
            SyncAgentToTransform();
        }
        EvaluateAgentControl();
    }

    private void SyncAgentToTransform()
    {
        if (_agent == null) return;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshRecoveryRadius, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            if (_agent.enabled)
            {
                _agent.Warp(hit.position);
                _agent.nextPosition = hit.position;
            }
        }
        else if (_agent.enabled)
        {
            _agent.Warp(transform.position);
            _agent.nextPosition = transform.position;
        }
    }

    private void EvaluateAgentControl()
    {
        if (_agent == null || !_agent.enabled) return;
        bool overrideTransform = _isDashing || _isUnderExternalForce;
        _agent.updatePosition = !overrideTransform;
        if (overrideTransform)
        {
            if (_agent.isOnNavMesh)
            {
                _agent.ResetPath();
                _agent.isStopped = true;
            }
        }
        else if (_isStunned)
        {
            if (_agent.isOnNavMesh) _agent.isStopped = true;
        }
        else if (_agent.isOnNavMesh) _agent.isStopped = false;
    }

    private IEnumerator StunRoutine(float duration)
    {
        _isStunned = true;
        HaltVelocity();
        if (TryGetComponent<AbilityManager>(out var abilityManager))
            abilityManager.InterruptAllAbilities();
        EvaluateAgentControl();
        yield return new WaitForSeconds(duration);
        _isStunned = false;
        _stunCoroutine = null;
        EvaluateAgentControl();
    }

    private bool CheckGrounded()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down,
            out RaycastHit hit, groundCheckTolerance + 0.3f, obstacleMask, QueryTriggerInteraction.Ignore))
            return true;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 0.4f, NavMesh.AllAreas))
            return Mathf.Abs(transform.position.y - navHit.position.y) <= groundCheckTolerance;
        return false;
    }

    public void SetGravityMultiplier(float value) => _gravityMultiplier = value;
    public void HaltVerticalVelocity() => _velocity.y = 0f;

    public void ApplySlow(float slowMultiplier, float duration, bool decaying = false)
    {
        if (_slowCoroutine != null)
        {
            StopCoroutine(_slowCoroutine);
            SpeedMultiplier = _preSlowMultiplier;
        }
        _slowCoroutine = StartCoroutine(SlowRoutine(slowMultiplier, duration, decaying));
    }

    private IEnumerator SlowRoutine(float slowMultiplier, float duration, bool decaying)
    {
        _preSlowMultiplier = SpeedMultiplier;
        float targetSpeed = _preSlowMultiplier * slowMultiplier;
        SpeedMultiplier = targetSpeed;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (decaying) SpeedMultiplier = Mathf.Lerp(targetSpeed, _preSlowMultiplier, elapsed / duration);
            yield return null;
        }
        SpeedMultiplier = _preSlowMultiplier;
        _slowCoroutine = null;
    }
}
