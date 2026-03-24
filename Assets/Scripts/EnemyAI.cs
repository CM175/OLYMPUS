using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [Header("Detection")]
    public float detectionRange = 20f;
    public float loseRange      = 25f;

    [Header("Patrol")]
    public float patrolWaitTime = 2f;
    public float patrolRadius   = 10f;

    private NavMeshAgent  _agent;
    private Transform     _player;
    private EnemyAttack   _attack;

    private enum State { Idle, Patrol, Chase, Stunned }
    private State _state = State.Idle;

    private float _patrolWaitTimer;
    private bool  _waitingAtPoint;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        _agent  = GetComponent<NavMeshAgent>();
        _attack = GetComponent<EnemyAttack>();
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            _player = playerObj.transform;

        _state = State.Patrol;
        SetNewPatrolPoint();
    }

    // ── Update ───────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_player == null || _state == State.Stunned) return;

        float distToPlayer = Vector3.Distance(transform.position, _player.position);

        switch (_state)
        {
            case State.Idle:
            case State.Patrol:
                HandlePatrol();
                if (distToPlayer <= detectionRange)
                    _state = State.Chase;
                break;

            case State.Chase:
                HandleChase();
                if (distToPlayer > loseRange)
                {
                    _state = State.Patrol;
                    SetNewPatrolPoint();
                }
                break;
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Briefly interrupts the enemy's AI and movement (called by ShieldBash).
    /// Safe to call while already stunned — just extends the duration.
    /// </summary>
    public void Stun(float duration)
    {
        StartCoroutine(StunRoutine(duration));
    }

    // ── Patrol ───────────────────────────────────────────────────────────────

    private void HandlePatrol()
    {
        if (_agent.remainingDistance <= _agent.stoppingDistance)
        {
            if (!_waitingAtPoint)
            {
                _waitingAtPoint    = true;
                _patrolWaitTimer   = patrolWaitTime;
            }

            _patrolWaitTimer -= Time.deltaTime;

            if (_patrolWaitTimer <= 0f)
            {
                _waitingAtPoint = false;
                SetNewPatrolPoint();
            }
        }
    }

    private void SetNewPatrolPoint()
    {
        Vector3 randomDir = Random.insideUnitSphere * patrolRadius + transform.position;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDir, out hit, patrolRadius, NavMesh.AllAreas))
            _agent.SetDestination(hit.position);
    }

    // ── Chase ────────────────────────────────────────────────────────────────

    private void HandleChase()
    {
        float stopAt = _attack != null ? _attack.attackRange * 0.9f : 1.5f;
        _agent.stoppingDistance = stopAt;
        _agent.SetDestination(_player.position);
    }

    // ── Stun coroutine ───────────────────────────────────────────────────────

    private IEnumerator StunRoutine(float duration)
    {
        State previousState = _state;
        _state           = State.Stunned;
        _agent.isStopped = true;

        yield return new WaitForSeconds(duration);

        _agent.isStopped = false;
        _state           = previousState == State.Stunned ? State.Patrol : previousState;
    }

    // ── Gizmos ───────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, loseRange);
    }
}