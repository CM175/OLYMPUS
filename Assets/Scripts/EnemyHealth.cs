using UnityEngine;
using UnityEngine.Events;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    public int maxHealth = 100;
    public int currentHealth;
    
    [HideInInspector]
    public Transform spawnPoint; // Assigned by MatchManager during generation

    public UnityEvent<GameObject> OnDeathWithKiller;

    public event System.Action<int> OnDamageDealt;
    public static event System.Action<int> OnAnyEnemyDamaged;

    private UnityEngine.AI.NavMeshAgent _agent;

    private void Awake()
    {
        _agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
    }

    // This satisfies the hook called by HeroManager when the AI loads a Hero
    public void OnHeroLoaded(Hero hero)
    {
        maxHealth = hero.health;
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, null);
    }

    public void TakeDamage(int damage, GameObject attacker)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        
        OnDamageDealt?.Invoke(damage);
        OnAnyEnemyDamaged?.Invoke(damage);
        
        if (currentHealth <= 0) 
            Die(attacker);
    }

    private void Die(GameObject attacker)
    {
        OnDeathWithKiller?.Invoke(attacker);
        StartCoroutine(RespawnRoutine());
    }

    private System.Collections.IEnumerator RespawnRoutine()
    {
        // Disable AI processing and navigation while dead
        if (_agent != null) _agent.enabled = false;
        
        // Hide character models (nested inside the runtime hero child object)
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = false;

        yield return new WaitForSeconds(5f); // 5-second respawn timer

        // Relocate back to the team base spawn pad
        if (spawnPoint != null)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
        }

        // Re-enable graphics and AI systems
        foreach (var r in renderers) r.enabled = true;
        if (_agent != null) _agent.enabled = true;

        currentHealth = maxHealth;
    }
}