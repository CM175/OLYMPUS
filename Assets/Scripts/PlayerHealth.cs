using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    public Transform spawnPoint;

    public int currentHealth;

    HeroManager heroManager;
    CharacterController _controller;
    MeshRenderer _mesh;

    void Start()
    {
        heroManager = GetComponent<HeroManager>();
        maxHealth = heroManager.currentHero.health;
        currentHealth = maxHealth;
        _controller = GetComponent<CharacterController>();
        _mesh = GetComponent<MeshRenderer>();
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        // Disable visuals and control without deactivating the GameObject
        _controller.enabled = false;
        if (_mesh != null) _mesh.enabled = false;

        yield return new WaitForSeconds(3f);

        // Teleport to spawn, reset health, re-enable
        _controller.enabled = false; // must disable before moving CharacterController
        transform.position = spawnPoint.position;
        _controller.enabled = true;
        if (_mesh != null) _mesh.enabled = true;
        currentHealth = maxHealth;
    }
}