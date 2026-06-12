using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public int maxHealth = 100;
    public Transform spawnPoint;
    public int currentHealth;

    public bool IsInvincible { get; set; } = false;
    public float DamageReduction { get; set; } = 0f;
    private bool respawning = false;

    CharacterController _controller;
    MeshRenderer _mesh;


    private void OnEnable()  => PlayerRegistry.Register(transform);
    private void OnDisable() => PlayerRegistry.Unregister();

    void Start()
    {
        _controller = GetComponent<CharacterController>();
        _mesh       = GetComponent<MeshRenderer>();
    }

    public void OnHeroLoaded(Hero hero)
    {
        maxHealth     = hero.health;
        currentHealth = maxHealth;
        IsInvincible   = false;
        DamageReduction = 0f;
    }

    public void TakeDamage(int damage)
    {
        if (IsInvincible) return;
        int reduced = UnityEngine.Mathf.RoundToInt(damage * (1f - DamageReduction));
        currentHealth -= reduced;
        currentHealth = Mathf.Max(currentHealth, 0);
        if (currentHealth <= 0) Die();
    }

    public void ApplyHeal(int heal)
    {
        if (respawning) return;
        currentHealth += heal;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
    }

    void Die()
    {
        StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        respawning = true;
        _controller.enabled = false;
        IsInvincible = true;

        if (_mesh != null) _mesh.enabled = false;

        yield return new WaitForSeconds(3f);

        transform.position  = spawnPoint.position;
        _controller.enabled = true;
        if (_mesh != null) _mesh.enabled = true;

        currentHealth   = maxHealth;
        IsInvincible    = false;
        DamageReduction = 0f;
        respawning = false;
    }
}