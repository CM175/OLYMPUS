using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    public int maxHealth = 100;
    public int currentHealth;
    [Tooltip("If true, the agent/dummy will not be destroyed on death, allowing the training system to manage resets.")]
    public bool isTrainingDummy = false;

    [HideInInspector] public Transform spawnPoint;
    [HideInInspector] public MatchManager matchManager;
    [HideInInspector] public Hero heroPrefab;
    [HideInInspector] public Team team;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsInvincible { get; set; } = false;
    public bool IsDead { get; set; } = false;

    public event Action<int, GameObject> OnDamageReceived;
    public event Action<GameObject> OnDied;
    public UnityEvent<GameObject> OnDeathWithKiller;
    public static event Action<int, GameObject> OnDamageDealt;
    public static event Action<int, GameObject> OnHealDealt;

    public void OnHeroLoaded(Hero hero)
    {
        maxHealth = hero.health;
        currentHealth = maxHealth;
        IsDead = false;
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        IsDead = false;
    }

    public void TakeDamage(int damage) => TakeDamage(damage, DamageContext.CurrentAttacker);

    public void TakeDamage(int damage, GameObject attacker)
    {
        if (IsInvincible || IsDead) return;
        currentHealth = Mathf.Max(0, currentHealth - damage);
        OnDamageReceived?.Invoke(damage, attacker);
        OnDamageDealt?.Invoke(damage, attacker);
        if (currentHealth <= 0) Die(attacker);
    }

    public void ApplyHeal(int heal) => ApplyHeal(heal, DamageContext.CurrentAttacker);
    public void ApplyHeal(int heal, GameObject healer)
    {
        if (IsDead) return;
        currentHealth = Mathf.Min(currentHealth + heal, maxHealth);
        OnHealDealt?.Invoke(heal, healer);
    }

    public void HealOverTime(int totalHeal, float duration, GameObject healer)
    {
        if (IsDead) return;
        if (duration <= 0f) 
        { 
            ApplyHeal(totalHeal, healer); 
            return; 
        }
        StartCoroutine(SmoothHealRoutine(totalHeal, duration, healer));
    }

    private IEnumerator SmoothHealRoutine(int totalHeal, float duration, GameObject healer)
    {
        float elapsed = 0f, accumulatedHeal = 0f;
        float healRate = totalHeal / duration;
        while (elapsed < duration)
        {
            if (IsDead) yield break;
            elapsed += Time.deltaTime;
            accumulatedHeal += healRate * Time.deltaTime;
            if (accumulatedHeal >= 1f)
            {
                int amount = Mathf.FloorToInt(accumulatedHeal);
                ApplyHeal(amount, healer);
                accumulatedHeal -= amount;
            }
            yield return null;
        }
        if (accumulatedHeal > 0f) ApplyHeal(Mathf.RoundToInt(accumulatedHeal), healer);
    }

    public void DamageOverTime(int totalDamage, float duration) => DamageOverTime(totalDamage, duration, DamageContext.CurrentAttacker);

    public void DamageOverTime(int totalDamage, float duration, GameObject attacker)
    {
        if (IsDead) return;
        if (duration <= 0f) { TakeDamage(totalDamage, attacker); return; }
        StartCoroutine(SmoothDamageRoutine(totalDamage, duration, attacker));
    }

    private IEnumerator SmoothDamageRoutine(int totalDamage, float duration, GameObject attacker)
    {
        float elapsed = 0f, accumulatedDamage = 0f;
        float damageRate = totalDamage / duration;
        while (elapsed < duration)
        {
            if (IsDead) yield break;
            elapsed += Time.deltaTime;
            accumulatedDamage += damageRate * Time.deltaTime;
            if (accumulatedDamage >= 1f)
            {
                int amount = Mathf.FloorToInt(accumulatedDamage);
                TakeDamage(amount, attacker);
                accumulatedDamage -= amount;
            }
            yield return null;
        }
        if (accumulatedDamage > 0f) TakeDamage(Mathf.RoundToInt(accumulatedDamage), attacker);
    }

    private void Die(GameObject attacker)
    {
        if (IsDead) return;
        IsDead = true;
        StopAllCoroutines();
        OnDied?.Invoke(attacker);
        OnDeathWithKiller?.Invoke(attacker);
        if (isTrainingDummy) return;

        if (matchManager == null)
            matchManager = FindFirstObjectByType<MatchManager>();
        if (matchManager != null && heroPrefab != null && spawnPoint != null)
        {
            float ultCharge = GetComponent<PlayerStats>().currentUltimateCharge;
            matchManager.RespawnAI(heroPrefab, team, spawnPoint, ultCharge);
        }
        Destroy(gameObject);
    }
}
