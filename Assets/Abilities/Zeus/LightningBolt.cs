using System.Collections;
using UnityEngine;

public class LightningBolt : Attack
{
    public float chainRange = 15f;
    public LineRenderer boltLine;
    public float beamDuration = 0.1f;
    public float damageOverTimeDuration;

    private void Awake()
    {
        damageOverTimeDuration = cooldown * 0.8f;
    }

    protected override void Activate()
    {
        Transform root = transform.root;
        Vector3 rayOrigin = playerCamera != null ? playerCamera.transform.position : root.position + Vector3.up * 1.5f;
        Vector3 rayDir = playerCamera != null ? playerCamera.transform.forward : (movementController != null ? movementController.GetAimDirection() : root.forward);

        Ray ray = new Ray(rayOrigin, rayDir);
        Vector3 endPoint = ray.GetPoint(range);

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            endPoint = hit.point;
            var myTeam = root.GetComponent<TeamMember>();
            GameObject hitRoot = hit.collider.transform.root.gameObject;

            if (myTeam != null && !myTeam.IsEnemy(hitRoot))
            {
                StartCoroutine(ShowBolt(root.position + Vector3.up * 1.2f, endPoint));
                return;
            }

            IDamageable target = hitRoot.GetComponent<IDamageable>() ?? hit.collider.GetComponent<IDamageable>();
            if (target != null)
            {
                DealOverTime(target, damage, damageOverTimeDuration);

                Collider[] nearby = Physics.OverlapSphere(hit.point, chainRange);
                Collider bestTarget = null;
                float bestDist = float.MaxValue;

                foreach (var col in nearby)
                {
                    GameObject colRoot = col.transform.root.gameObject;
                    if (myTeam != null && !myTeam.IsEnemy(colRoot)) continue;
                    if (colRoot == hitRoot) continue;
                    if (colRoot.GetComponent<IDamageable>() == null && col.GetComponent<IDamageable>() == null) continue;

                    float dist = Vector3.Distance(hit.point, col.transform.position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestTarget = col;
                    }
                }

                if (bestTarget != null)
                {
                    IDamageable chainDamageable = bestTarget.transform.root.GetComponent<IDamageable>() ?? bestTarget.GetComponent<IDamageable>();
                    DealOverTime(chainDamageable, (int)(damage * 0.5f), damageOverTimeDuration);
                    StartCoroutine(ShowChainArc(hit.point, bestTarget.transform.position));
                }
            }
        }

        StartCoroutine(ShowBolt(root.position + Vector3.up * 1.2f, endPoint));
    }

    private IEnumerator ShowBolt(Vector3 start, Vector3 end)
    {
        if (boltLine == null) yield break;
        LineRenderer arc = Instantiate(boltLine);
        Destroy(arc.gameObject, beamDuration + 0.5f);
        arc.enabled = true;
        float timer = 0f;
        int segments = 12;
        arc.positionCount = segments;
        arc.startWidth = 0.3f;
        arc.endWidth = 0.05f;

        while (timer < beamDuration)
        {
            arc.SetPosition(0, start);
            for (int i = 1; i < segments - 1; i++)
            {
                float fraction = (float)i / (segments - 1);
                Vector3 midPoint = Vector3.Lerp(start, end, fraction);
                midPoint += Random.insideUnitSphere * 0.5f;
                arc.SetPosition(i, midPoint);
            }
            arc.SetPosition(segments - 1, end);
            timer += Time.deltaTime;
            yield return null;
        }
        Destroy(arc.gameObject);
    }

    private IEnumerator ShowChainArc(Vector3 start, Vector3 end)
    {
        if (boltLine == null) yield break;
        LineRenderer arc = Instantiate(boltLine);
        arc.enabled = true;
        float elapsed = 0;
        float arcDuration = 0.15f;
        int segments = 8;
        arc.positionCount = segments;
        Destroy(arc.gameObject, arcDuration + 0.5f);

        while (elapsed < arcDuration)
        {
            arc.SetPosition(0, start);
            for (int i = 1; i < segments - 1; i++)
            {
                float lerpVal = (float)i / (segments - 1);
                Vector3 pos = Vector3.Lerp(start, end, lerpVal);
                pos += Random.insideUnitSphere * 0.3f;
                arc.SetPosition(i, pos);
            }
            arc.SetPosition(segments - 1, end);
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(arc.gameObject);
    }
}