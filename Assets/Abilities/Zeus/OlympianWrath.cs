using System.Collections;
using UnityEngine;

public class OlympianWrath : Ultimate
{
    public float radius = 15f;
    public float duration = 6f;
    public float strikeInterval = 0.5f;
    public int damagePerStrike = 15;

    [Header("Visuals")]
    public LineRenderer strikeBoltPrefab; // Assign a LineRenderer prefab
    public float skyHeight = 20f;

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
                    SpawnSkyBolt(col.transform.position);
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

    void SpawnSkyBolt(Vector3 targetPos)
    {
        if (strikeBoltPrefab == null) return;

        Vector3 skyPos = targetPos + Vector3.up * skyHeight;
        LineRenderer bolt = Instantiate(strikeBoltPrefab);
        
        // 1. Define segments (More segments = more jagged)
        int segments = 15;
        bolt.positionCount = segments;

        // 2. Calculate jagged path
        bolt.SetPosition(0, skyPos); // Start at the sky
        for (int i = 1; i < segments - 1; i++)
        {
            float fraction = (float)i / (segments - 1);
            Vector3 midPoint = Vector3.Lerp(skyPos, targetPos, fraction);

            // 3. Add random horizontal offset to create the "bolt" shape
            float jitter = 1.2f; 
            midPoint += new Vector3(Random.Range(-jitter, jitter), 0, Random.Range(-jitter, jitter));
            
            bolt.SetPosition(i, midPoint);
        }
        bolt.SetPosition(segments - 1, targetPos); // End at the enemy
        
        Destroy(bolt.gameObject, 0.1f);
    }
}