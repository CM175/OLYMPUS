using UnityEngine;

public class WingedDaggers : Attack
{
    [Header("Hermes Velocity Damage Settings")]
    public float baseSpeedThreshold = 6f;
    public float velocityScaleFactor = 0.15f;

    private Vector3 _lastPos;
    private float _estimatedSpeed;

    private void Start()
    {
        _lastPos = transform.root.position;
    }

    private void Update()
    {
        _estimatedSpeed = (transform.root.position - _lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        _lastPos = transform.root.position;
    }

    protected override void Activate()
    {
        Transform root = transform.root;
        Vector3 attackPoint = root.position + root.forward * (range / 2f);
        Collider[] hitEnemies = Physics.OverlapSphere(attackPoint, range / 2f);
        var myTeam = root.GetComponent<TeamMember>();

        foreach (var enemy in hitEnemies)
        {
            Transform enemyRoot = enemy.transform.root;
            if (enemyRoot == root) continue;
            if (myTeam != null && !myTeam.IsEnemy(enemyRoot.gameObject)) continue;

            IDamageable target = enemyRoot.GetComponent<IDamageable>() ?? enemy.GetComponent<IDamageable>();
            if (target != null)
            {
                ApplyDamage(target);
            }
        }
    }

    private void ApplyDamage(IDamageable target)
    {
        Transform root = transform.root;
        PlayerStats stats = root.GetComponent<PlayerStats>();
        CharacterController cc = root.GetComponent<CharacterController>();

        int finalDamage = damage;
        if (stats != null)
        {
            finalDamage = Mathf.RoundToInt(finalDamage * stats.DamageMultiplier);
        }

        float actualSpeed = cc != null ? cc.velocity.magnitude : _estimatedSpeed;
        if (actualSpeed > baseSpeedThreshold)
        {
            float excessSpeed = actualSpeed - baseSpeedThreshold;
            float damageMultiplier = 1f + (excessSpeed * velocityScaleFactor);
            finalDamage = Mathf.RoundToInt(finalDamage * damageMultiplier);
        }

        target.TakeDamage(finalDamage, root.gameObject);
    }
}