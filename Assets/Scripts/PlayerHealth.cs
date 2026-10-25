using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public int maxHealth = 100;
    public Transform spawnPoint;
    public int currentHealth;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    public bool IsInvincible { get; set; } = false;
    public bool IsDead { get; set; } = false;
    public float DamageReduction { get; set; } = 0f;

    [HideInInspector] public MatchManager matchManager;

    public event Action<int, GameObject> OnDamageReceived;
    public event Action<GameObject> OnDied;

    private void OnEnable() => PlayerRegistry.Register(transform);
    private void OnDisable() => PlayerRegistry.Unregister();

    public void OnHeroLoaded(Hero hero)
    {
        maxHealth = hero.health;
        currentHealth = maxHealth;
        IsInvincible = false;
        DamageReduction = 0f;
        IsDead = false;
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, DamageContext.CurrentAttacker);
    }

    public void TakeDamage(int damage, GameObject attacker)
    {
        if (IsInvincible || IsDead) return;

        int reduced = Mathf.RoundToInt(damage * (1f - DamageReduction));
        currentHealth -= reduced;
        currentHealth = Mathf.Max(currentHealth, 0);

        OnDamageReceived?.Invoke(reduced, attacker);

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

        while (elapsed < duration && currentHealth < maxHealth)
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

        while (elapsed < duration && currentHealth > 0)
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

    private void Die(GameObject attacker = null)
    {
        if (IsDead) return;
        IsDead = true;
        currentHealth = 0;

        OnDied?.Invoke(attacker);

        if (matchManager == null)
            matchManager = FindFirstObjectByType<MatchManager>();

        if (matchManager != null)
        {
            if (matchManager.hudController != null)
            {
                matchManager.hudController.ShowDeathScreen();
            }
            matchManager.RespawnPlayer();
        }

        Destroy(gameObject);
    }
}