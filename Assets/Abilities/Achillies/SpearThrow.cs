using System.Collections;
using UnityEngine;

public class SpearThrow : Ability
{
    [Header("Stats")]
    public float range = 50f;
    public int damage = 35;

    [Header("Visuals")]
    public LineRenderer spearLine;
    public float lineDuration = 0.12f;

    protected override void Activate()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, range);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        PlayerStats stats = transform.root.GetComponent<PlayerStats>();
        Vector3 endPoint = ray.GetPoint(range);

        foreach (var hit in hits)
        {
            IDamageable target = hit.collider.GetComponent<IDamageable>();
            if (target != null)
            {
                int finalDamage = stats != null
                    ? Mathf.RoundToInt(damage * stats.DamageMultiplier)
                    : damage;

                target.TakeDamage(finalDamage);
                endPoint = hit.point;
            }
        }

        if (spearLine != null)
            StartCoroutine(ShowSpear(playerCamera.transform.position, endPoint));

        StartCooldown();
    }

    private IEnumerator ShowSpear(Vector3 start, Vector3 end)
    {
        spearLine.enabled = true;
        spearLine.positionCount = 2;
        spearLine.startWidth = 0.08f;
        spearLine.endWidth = 0.03f;

        float elapsed = 0f;
        while (elapsed < lineDuration)
        {
            spearLine.SetPosition(0, start);
            spearLine.SetPosition(1, end);
            elapsed += Time.deltaTime;
            yield return null;
        }

        spearLine.enabled = false;
    }
}
