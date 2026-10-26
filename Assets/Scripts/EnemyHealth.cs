using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    public int maxHealth = 100;
    public int currentHealth;

    [Tooltip("If true, the agent/dummy will not be destroyed on death, allowing ML-Agents OnEpisodeBegin() to manage resets.")]
    public bool isTrainingDummy = false;

    [HideInInspector] public Transform spawnPoint;
    [HideInInspector] public MatchManager matchManager;
    [HideInInspector] public Hero heroPrefab;
    [HideInInspector] public Team team;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    public bool IsInvincible { get; set; } = false;
    public bool IsDead { get; set; } = false;

    // Unified IDamageable events
    public event Action<int, GameObject> OnDamageReceived;
    public event Action<GameObject> OnDied;

    // Retained for backwards compatibility with existing UI/systems
    public UnityEvent<GameObject> OnDeathWithKiller;
    public event Action<int, GameObject> OnDamageDealt;
    public static event Action<int> OnAnyEnemyDamaged;

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

    public void TakeDamage(int damage) => TakeDamage(damage, null);

    public void TakeDamage(int damage, GameObject attacker)
    {
        if (IsInvincible || IsDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        OnDamageReceived?.Invoke(damage, attacker);
        OnDamageDealt?.Invoke(damage, attacker);
        OnAnyEnemyDamaged?.Invoke(damage);

        if (currentHealth <= 0)
        {
            Die(attacker);
        }
    }

    public void ApplyHeal(int heal)
    {
        if (IsDead) return;
        currentHealth = Mathf.Min(currentHealth + heal, maxHealth);
    }

    public void HealOverTime(int totalHeal, float duration)
    {
        if (IsDead) return;
        if (duration <= 0f)
        {
            ApplyHeal(totalHeal);
            return;
        }
        StartCoroutine(SmoothHealRoutine(totalHeal, duration));
    }

    private IEnumerator SmoothHealRoutine(int totalHeal, float duration)
    {
        float elapsed = 0f;
        float accumulatedHeal = 0f;
        float healRate = totalHeal / duration;

        while (elapsed < duration)
        {
            if (IsDead) yield break;
            elapsed += Time.deltaTime;
            accumulatedHeal += healRate * Time.deltaTime;

            if (accumulatedHeal >= 1f)
            {
                int healAmount = Mathf.FloorToInt(accumulatedHeal);
                ApplyHeal(healAmount);
                accumulatedHeal -= healAmount;
            }
            yield return null;
        }

        if (accumulatedHeal > 0f)
        {
            ApplyHeal(Mathf.RoundToInt(accumulatedHeal));
        }
    }

    public void DamageOverTime(int totalDamage, float duration) => DamageOverTime(totalDamage, duration, null);

    public void DamageOverTime(int totalDamage, float duration, GameObject attacker)
    {
        if (IsDead) return;
        if (duration <= 0f)
        {
            TakeDamage(totalDamage, attacker);
            return;
        }
        StartCoroutine(SmoothDamageRoutine(totalDamage, duration, attacker));
    }

    private IEnumerator SmoothDamageRoutine(int totalDamage, float duration, GameObject attacker)
    {
        float elapsed = 0f;
        float accumulatedDamage = 0f;
        float damageRate = totalDamage / duration;

        while (elapsed < duration)
        {
            if (IsDead) yield break;
            elapsed += Time.deltaTime;
            accumulatedDamage += damageRate * Time.deltaTime;

            if (accumulatedDamage >= 1f)
            {
                int damageAmount = Mathf.FloorToInt(accumulatedDamage);
                TakeDamage(damageAmount, attacker);
                accumulatedDamage -= damageAmount;
            }
            yield return null;
        }

        if (accumulatedDamage > 0f)
        {
            TakeDamage(Mathf.RoundToInt(accumulatedDamage), attacker);
        }
    }

    private void Die(GameObject attacker)
    {
        if (IsDead) return;
        IsDead = true;

        
        StopAllCoroutines();
        OnDied?.Invoke(attacker);
        OnDeathWithKiller?.Invoke(attacker);

        if (isTrainingDummy)
        {
            // Do not destroy during RL training loop
            return;
        }

        if (matchManager == null)
            matchManager = FindFirstObjectByType<MatchManager>();

        if (matchManager != null && heroPrefab != null && spawnPoint != null)
        {
            matchManager.RespawnAI(heroPrefab, team, spawnPoint);
        }

        Destroy(gameObject);
    }
}