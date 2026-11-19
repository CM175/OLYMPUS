using UnityEngine;
using UnityEngine.Serialization;
using System.Collections;

public class PlayerStats : MonoBehaviour
{
    public float currentUltimateCharge = 0f;
    public float maxUltimateCharge;

    [Header("Charge On Hit")]
    [FormerlySerializedAs("chargePerHit")]
    public int chargePerDamagePoint = 1;

    [Header("Passive Charge")]
    public float pointsPerSecond = 5f;

    private float _chargeAccumulator = 0f;

    /// <summary>
    /// Multiplier applied to all outgoing ability damage.
    /// 1 = normal. Set by BattleCry, AchillesFury, etc.
    /// </summary>
    public float DamageMultiplier { get; set; } = 1f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        // Subscribe once — works for all enemies regardless of when they spawn
        EnemyHealth.OnDamageDealt += OnEnemyDamaged;
        EnemyHealth.OnHealDealt += OnEnemyHealed;
    }

    private void OnDisable()
    {
        // Always unsubscribe — prevents memory leaks and ghost callbacks
        EnemyHealth.OnDamageDealt -= OnEnemyDamaged;
        EnemyHealth.OnHealDealt -= OnEnemyHealed;
    }

    // ── Event handler ─────────────────────────────────────────────────────────

    private void OnEnemyDamaged(int damage, GameObject attacker)
    {
        // Award charge proportional to damage dealt
        if (attacker == gameObject)
        {
            AddCharge(damage * chargePerDamagePoint);
        }
    }

    private void OnEnemyHealed(int heal, GameObject healer)
    {
        // Award charge proportional to healing done
        if (healer == gameObject)
        {
            AddCharge(heal * chargePerDamagePoint);
        }
    }

    // ── Charge ────────────────────────────────────────────────────────────────

    public void AddCharge(int points)
    {
        currentUltimateCharge = Mathf.Clamp(
            currentUltimateCharge + points, 0, maxUltimateCharge);
    }

    private void Update()
    {
        if (currentUltimateCharge >= maxUltimateCharge) return;

        _chargeAccumulator += pointsPerSecond * Time.deltaTime;

        if (_chargeAccumulator >= 1f)
        {
            int toAdd = Mathf.FloorToInt(_chargeAccumulator);
            _chargeAccumulator -= toAdd;
            AddCharge(toAdd);
        }
    }

    public bool ConsumeCharge(int points)
    {
        if (currentUltimateCharge >= points)
        {
            currentUltimateCharge -= points;
            return true;
        }
        return false;
    }

    public int ultCharge()
    {
        if (maxUltimateCharge <= 0) return 0;
        return Mathf.RoundToInt(currentUltimateCharge / maxUltimateCharge * 100);
    }
}