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
    // Damage absorbed by DamageReduction (raw - applied). Lets an RL agent credit defensive abilities.
    public event Action<int, GameObject> OnDamageMitigated;
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
        if (IsDead) return;
        if (IsInvincible)
        {
            OnDamageMitigated?.Invoke(damage, attacker); 
            return;
        }

        int reduced = Mathf.RoundToInt(damage * (1f - DamageReduction));
        int mitigated = damage - reduced;
        if (mitigated > 0) OnDamageMitigated?.Invoke(mitigated, attacker);
        currentHealth -= reduced;
        currentHealth = Mathf.Max(currentHealth, 0);

        OnDamageReceived?.Invoke(reduced, attacker);

        if (currentHealth <= 0)
        {
            Die(attacker);
        }
    }

    public void ApplyHeal(int heal) => ApplyHeal(heal, DamageContext.CurrentAttacker);
    public void ApplyHeal(int heal, GameObject healer)
    {
        if (IsDead) return;
        currentHealth = Mathf.Min(currentHealth + heal, maxHealth);
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
                ApplyHeal(healAmount, healer);
                accumulatedHeal -= healAmount;
            }
            yield return null;
        }

        if (accumulatedHeal > 0f)
        {
            ApplyHeal(Mathf.RoundToInt(accumulatedHeal), healer);
        }
    }

    public void DamageOverTime(int totalDamage, float duration) => DamageOverTime(totalDamage, duration, DamageContext.CurrentAttacker);

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

        while (elapsed < duration && currentHealth > 0)
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