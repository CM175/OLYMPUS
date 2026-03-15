using UnityEngine;

public class OlympianWrath : Ability
{
    public float radius = 20f;
    public int damage = 50;

    protected override void Activate()
    {
        Collider[] targets = Physics.OverlapSphere(
            transform.position, radius);

        int hits = 0;
        foreach (var col in targets)
        {
            if (col.gameObject == gameObject) continue;

            var enemy = col.GetComponent<EnemyHealth>();
            var player = col.GetComponent<PlayerHealth>();

            if (enemy != null) { enemy.TakeDamage(damage); hits++; }
            else if (player != null) { player.TakeDamage(damage); hits++; }
        }

        Debug.Log($"Olympian Wrath hit {hits} targets");
    }
}