using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public int maxHealth = 100;
    public Transform spawnPoint;
    public int currentHealth;
    public bool IsInvincible { get; set; } = false;
    public float DamageReduction { get; set; } = 0f;

    [HideInInspector]
    public MatchManager matchManager;

    public bool IsDead { get; set; }= false;

    private void OnEnable()  => PlayerRegistry.Register(transform);
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
        if (IsInvincible || IsDead) return;

        int reduced = Mathf.RoundToInt(damage * (1f - DamageReduction));
        currentHealth -= reduced;
        currentHealth = Mathf.Max(currentHealth, 0);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void ApplyHeal(int heal)
    {
        if (IsDead) return;
        currentHealth += heal;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
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

    private System.Collections.IEnumerator SmoothHealRoutine(int totalHeal, float duration)
    {
        float elapsed = 0f;
        float accumulatedHeal = 0f;
        float healRate = totalHeal / duration;

        while (elapsed < duration && currentHealth < maxHealth)
        {
            if (IsDead) yield break;

            elapsed += Time.deltaTime;
            accumulatedHeal += healRate * Time.deltaTime;

            // Apply health only when a full integer point is accumulated
            if (accumulatedHeal >= 1f)
            {
                int healAmount = Mathf.FloorToInt(accumulatedHeal);
                ApplyHeal(healAmount);
                accumulatedHeal -= healAmount;
            }

            yield return null;
        }
        {
            if (IsDead) yield break;

            elapsed += Time.deltaTime;
            accumulatedHeal += healRate * Time.deltaTime;

            // Apply health only when a full integer point is accumulated
            if (accumulatedHeal >= 1f)
            {
                int healAmount = Mathf.FloorToInt(accumulatedHeal);
                ApplyHeal(healAmount);
                accumulatedHeal -= healAmount;
            }

            yield return null;
        }
        {
            if (IsDead) yield break;

            elapsed += Time.deltaTime;
            accumulatedHeal += healRate * Time.deltaTime;

            // Apply health only when a full integer point is accumulated
            if (accumulatedHeal >= 1f)
            {
                int healAmount = Mathf.FloorToInt(accumulatedHeal);
                ApplyHeal(healAmount);
                accumulatedHeal -= healAmount;
            }

            yield return null;
        }

        // Apply any leftover fraction once the timer finishes
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

    private System.Collections.IEnumerator SmoothDamageRoutine(int totalDamage, float duration)
    {
        float elapsed = 0f;
        float accumulatedDamage = 0f;
        float damageRate = totalDamage / duration;

        while (elapsed < duration && currentHealth > 0)
        {
            if (IsDead) yield break;

            elapsed += Time.deltaTime;
            accumulatedDamage += damageRate * Time.deltaTime;

            // Apply damage only when a full integer point is accumulated
            if (accumulatedDamage >= 1f)
            {
                int damageAmount = Mathf.FloorToInt(accumulatedDamage);
                TakeDamage(damageAmount);
                accumulatedDamage -= damageAmount;
            }

            yield return null;
        }

        // Apply any leftover fraction once the timer finishes
        if (accumulatedDamage > 0f)
        {
            TakeDamage(Mathf.RoundToInt(accumulatedDamage));
        }
    }
    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        // Ensure the current health is set flat to 0
        currentHealth = 0;

        if (matchManager == null)
        {
            matchManager = FindFirstObjectByType<MatchManager>();
        }
        
        if (matchManager != null)
        {
            // matchManager.hudController will now show the death screen & zeroed health
            if (matchManager.hudController != null)
            {
                matchManager.hudController.ShowDeathScreen();
            }

            matchManager.RespawnPlayer();
        }

        // Fully destroy the player root instance
        Destroy(gameObject);
    }
}