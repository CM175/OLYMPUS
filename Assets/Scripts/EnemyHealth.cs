using UnityEngine;
using UnityEngine.Events;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    public int maxHealth = 100;
    public int currentHealth;

    [HideInInspector] public Transform spawnPoint;
    [HideInInspector] public MatchManager matchManager;
    [HideInInspector] public Hero heroPrefab;
    [HideInInspector] public Team team;

    public UnityEvent<GameObject> OnDeathWithKiller;
    public event System.Action<int> OnDamageDealt;
    public static event System.Action<int> OnAnyEnemyDamaged;

    public bool IsInvincible { get; set; } = false;
    public bool IsDead { get; set; } = false;

    public void OnHeroLoaded(Hero hero)
    {
        maxHealth = hero.health;
        currentHealth = maxHealth;
        IsDead = false;
    }

    public void TakeDamage(int damage) => TakeDamage(damage, null);

    public void TakeDamage(int damage, GameObject attacker)
    {
        if (IsInvincible || IsDead) return;
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        OnDamageDealt?.Invoke(damage);
        OnAnyEnemyDamaged?.Invoke(damage);

        if (currentHealth <= 0)
            Die(attacker);
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

    private System.Collections.IEnumerator SmoothDamageRoutine(int totalDamage, float duration)
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
        if (IsDead) return;
        IsDead = true;

        OnDeathWithKiller?.Invoke(attacker);

        if (matchManager == null)
            matchManager = FindFirstObjectByType<MatchManager>();

        if (matchManager != null && heroPrefab != null && spawnPoint != null)
        {
            matchManager.RespawnAI(heroPrefab, team, spawnPoint);
        }

        Destroy(gameObject);
    }
}