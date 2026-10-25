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
        Debug.Log($"[EnemyHealth] {gameObject.name} taking {damage} damage from {(attacker != null ? attacker.name : "Unknown")}");
        if (IsInvincible || IsDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        OnDamageReceived?.Invoke(damage, attacker);
        OnDamageDealt?.Invoke(damage, attacker);
        OnAnyEnemyDamaged?.Invoke(damage);

        if (currentHealth <= 0)
        {
            Debug.Log($"[EnemyHealth] {gameObject.name} has reached 0 health and will die. Attacker: {(attacker != null ? attacker.name : "None")}");
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

    public void DamageOverTime(int totalDamage, float duration)
    {
        if (IsDead) return;
        if (duration <= 0f)
        {
            TakeDamage(totalDamage);
            return;
        }
        StartCoroutine(SmoothDamageRoutine(totalDamage, duration));
    }

    private IEnumerator SmoothDamageRoutine(int totalDamage, float duration)
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
                TakeDamage(damageAmount);
                accumulatedDamage -= damageAmount;
            }
            yield return null;
        }

        if (accumulatedDamage > 0f)
        {
            TakeDamage(Mathf.RoundToInt(accumulatedDamage));
        }
    }

    private void Die(GameObject attacker)
    {
        Debug.Log($"[EnemyHealth] {gameObject.name} has died. Attacker: {(attacker != null ? attacker.name : "None")}");
        if (IsDead) return;
        IsDead = true;

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