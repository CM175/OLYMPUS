using System.Collections;
using UnityEngine;

public class OlympianWrath : Ultimate
{
    public float radius = 15f;
    public float duration = 6f;
    public float strikeInterval = 0.5f;
    public int damagePerStrike = 15;

    [Header("Visuals")]
    public LineRenderer strikeBoltPrefab;
    public float skyHeight = 20f;

    protected override void Activate()
    {
        base.Activate();
        StartCoroutine(StormZone(transform.root.position));
    }

    private IEnumerator StormZone(Vector3 center)
    {
        Debug.Log("Olympian Wrath activated — storm zone created");

        GameObject visual = null;
        if (vfxPrefab != null)
        {
            visual = Instantiate(vfxPrefab, center, Quaternion.identity);
            visual.transform.localScale = Vector3.one * (radius * 2);
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            Collider[] targets = Physics.OverlapSphere(center, radius);

            foreach (var col in targets)
            {
                if (col.CompareTag("Player")) continue;

                IDamageable target = col.GetComponent<IDamageable>();
                if (target != null)
                {
                    Vector3 enemyPos = col.transform.position;
                    target.TakeDamage(damagePerStrike);
                    StartCoroutine(AnimateSkyBolt(enemyPos));
                    Debug.Log($"[OlympianWrath] Struck {col.name} for {damagePerStrike}");
                }
            }

            yield return new WaitForSeconds(strikeInterval);
            elapsed += strikeInterval;
        }

        if (visual != null)
            Destroy(visual);

        StartCooldown(); // clears isExecuting and starts cooldown timer
        Debug.Log("Olympian Wrath ended");
    }

    private IEnumerator AnimateSkyBolt(Vector3 targetPos)
    {
        if (strikeBoltPrefab == null)
        {
            Debug.LogWarning("OlympianWrath: strikeBoltPrefab is not assigned!");
            yield break;
        }

        Vector3 skyPos = targetPos + Vector3.up * skyHeight;
        LineRenderer bolt = Instantiate(strikeBoltPrefab);
        bolt.enabled = true;

        int segments = 15;
        bolt.positionCount = segments;

        float boltDuration = 0.15f;
        float elapsed = 0f;

        while (elapsed < boltDuration)
        {
            bolt.SetPosition(0, skyPos);
            for (int i = 1; i < segments - 1; i++)
            {
                float fraction = (float)i / (segments - 1);
                Vector3 mid = Vector3.Lerp(skyPos, targetPos, fraction);
                mid += Random.insideUnitSphere * 0.4f;
                bolt.SetPosition(i, mid);
            }
            bolt.SetPosition(segments - 1, targetPos);

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(bolt.gameObject);
    }
}