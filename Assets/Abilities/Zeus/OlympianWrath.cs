using System.Collections;
using UnityEngine;

public class OlympianWrath : Ultimate
{
    public float radius = 15f;
    public float duration = 6f;
    public float strikeInterval = 0.5f;
    public int damagePerStrike = 15;

    protected override void Activate()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");

        if (playerObj == null)
        {
            Debug.LogError("OlympianWrath: Player not found.");
            return;
        }

        StartCoroutine(StormZone(playerObj.transform.position));
    }

    IEnumerator StormZone(Vector3 center)
    {
        Debug.Log("Olympian Wrath activated — storm zone created");

        // Spawn visual if assigned
        GameObject visual = null;
        if (vfxPrefab != null)
        {
            visual = Instantiate(vfxPrefab, center, Quaternion.identity);
            visual.transform.localScale = Vector3.one * (radius * 2);
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Find all enemies inside the radius
            Collider[] targets = Physics.OverlapSphere(center, radius);

            foreach (var col in targets)
            {
                // Skip the player
                if (col.CompareTag("Player")) continue;

                EnemyHealth enemy = col.GetComponent<EnemyHealth>();
                if (enemy != null)
                {
                    enemy.TakeDamage(damagePerStrike);
                    Debug.Log($"OlympianWrath: Struck {col.name} for {damagePerStrike}");
                }
            }

            yield return new WaitForSeconds(strikeInterval);
            elapsed += strikeInterval;
        }

        // Clean up visual
        if (visual != null)
            Destroy(visual);

        Debug.Log("Olympian Wrath ended");
    }
}