using System.Collections;

using UnityEngine;

public class LightningBolt : Attack
{
    public float chainRange = 15f;

    public LineRenderer boltLine; // Assign in Inspector
    public float beamDuration = 0.1f;
    public float damageOverTimeDuration; 

    private void Awake()
    {
        damageOverTimeDuration = cooldown * 0.8f;
        
    }

    protected override void Activate()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        Vector3 endPoint = ray.GetPoint(range);

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            endPoint = hit.point;
            var myTeam = transform.root.GetComponent<TeamMember>();
            Collider hitObject = hit.collider;
            if (myTeam != null && !myTeam.IsEnemy(hitObject.transform.root.gameObject))
            {
                StartCoroutine(ShowBolt(transform.position, endPoint));
                return;
            }
            
            IDamageable target = hitObject.GetComponent<IDamageable>();
            if (target != null)
            {
                // No more PlayerStats parameter — charge awards via static event
                target.DamageOverTime(damage, damageOverTimeDuration);

                Collider[] nearby = Physics.OverlapSphere(hit.point, chainRange);
                Collider bestTarget = null;
                float   bestDist    = float.MaxValue;

                foreach (var col in nearby)
                {
                    
                    if (!myTeam.IsEnemy(col.transform.root.gameObject)) continue;
                    if (col.transform.root.gameObject == hitObject.transform.root.gameObject) continue;
                    if (col.GetComponent<IDamageable>() == null)   continue;

                    float dist = Vector3.Distance(hit.point, col.transform.position);
                    if (dist < bestDist)
                    {
                        bestDist   = dist;
                        bestTarget = col;
                    }
                }

                if (bestTarget != null)
                {
                    bestTarget.GetComponent<IDamageable>().DamageOverTime((int)(damage * 0.5f), damageOverTimeDuration);
                    StartCoroutine(ShowChainArc(hit.point, bestTarget.transform.position));
                }
            }
        }

        StartCoroutine(ShowBolt(transform.position, endPoint));
    }

    private System.Collections.IEnumerator ShowBolt(Vector3 start, Vector3 end)
    {
        LineRenderer arc = Instantiate(boltLine); 
        arc.enabled = true;
        float timer = 0f;
        int segments = 12; // More segments = more jagged lightning
        arc.positionCount = segments;
        arc.startWidth = 0.3f;
        arc.endWidth = 0.05f;
        while (timer < beamDuration)
        {
            arc.SetPosition(0, start); // Origin stays at Zeus
            
            for (int i = 1; i < segments - 1; i++)
            {
                // Calculate a point along the straight line
                float fraction = (float)i / (segments - 1);
                Vector3 midPoint = Vector3.Lerp(start, end, fraction);

                // Add random "divine jitter" to make it look like lightning
                float jitterAmount = 0.5f; 
                midPoint += Random.insideUnitSphere * jitterAmount;
                arc.SetPosition(i, midPoint);
            }

            arc.SetPosition(segments - 1, end); // End stays at target
            
            timer += Time.deltaTime;
            yield return null; // This causes the bolt to "flicker" every frame
        }
        
        
        Destroy(arc.gameObject);
    }

    private IEnumerator ShowChainArc(Vector3 start, Vector3 end)
    {
        // Create a temporary LineRenderer for the arc
        LineRenderer arc = Instantiate(boltLine); 
        arc.enabled = true;
        
        float elapsed = 0;
        float arcDuration = 0.15f;
        int segments = 8;
        arc.positionCount = segments;

        while (elapsed < arcDuration)
        {
            arc.SetPosition(0, start);
            for (int i = 1; i < segments - 1; i++)
            {
                float lerpVal = (float)i / (segments - 1);
                Vector3 pos = Vector3.Lerp(start, end, lerpVal);
                pos += Random.insideUnitSphere * 0.3f; // Smaller jitter for arcs
                arc.SetPosition(i, pos);
            }
            arc.SetPosition(segments - 1, end);
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(arc.gameObject);
    }
}