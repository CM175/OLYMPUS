using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public int maxHealth = 100;
    public Transform spawnPoint;
    public int currentHealth;

    public bool IsInvincible { get; set; } = false;
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
        IsInvincible   = false;
        DamageReduction = 0f;
    }

    public void TakeDamage(int damage)
    {
        if (IsInvincible) return;
        int reduced = UnityEngine.Mathf.RoundToInt(damage * (1f - DamageReduction));
        currentHealth -= reduced;
        if (currentHealth <= 0) Die();
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

        // Fix: was disabling twice before — now correctly moves then re-enables
        _controller.enabled = false;
        transform.position  = spawnPoint.position;
        _controller.enabled = true;
        if (_mesh != null) _mesh.enabled = true;

        currentHealth   = maxHealth;
        IsInvincible    = false;
        DamageReduction = 0f;
    }
}