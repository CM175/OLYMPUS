using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    public Transform spawnPoint;
    public int currentHealth;

    /// <summary>Set by AchillesFury. While true, TakeDamage is a no-op.</summary>
    public bool IsInvincible { get; set; } = false;

    /// <summary>
    /// Flat damage reduction (0–1). Set by FrontalArmor passive on hero load.
    /// 0.25 = 25% less damage from all sources.
    /// </summary>
    public float DamageReduction { get; set; } = 0f;

    CharacterController _controller;
    MeshRenderer _mesh;

    void Start()
    {
        _controller = GetComponent<CharacterController>();
        _mesh       = GetComponent<MeshRenderer>();
    }

    public void OnHeroLoaded(Hero hero)
    {
        maxHealth     = hero.health;
        currentHealth = maxHealth;

        // Reset passive modifiers from any previously loaded hero
        IsInvincible   = false;
        DamageReduction = 0f;
    }

    public void TakeDamage(int damage)
    {
        if (IsInvincible) return;

        int reduced = Mathf.RoundToInt(damage * (1f - DamageReduction));
        currentHealth -= reduced;

        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        _controller.enabled = false;
        if (_mesh != null) _mesh.enabled = false;

        yield return new WaitForSeconds(3f);

        _controller.enabled = false;
        transform.position  = spawnPoint.position;
        _controller.enabled = true;
        if (_mesh != null) _mesh.enabled = true;

        currentHealth  = maxHealth;
        IsInvincible   = false;
        DamageReduction = 0f;
    }
}