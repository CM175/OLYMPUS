using System.Collections;
using UnityEngine;

public class LightningBolt : Ability
{
    public float range = 100f;
    public int damage = 25;
    public float chainRange = 15f;

    public LineRenderer boltLine; // Assign in Inspector
    public float beamDuration = 0.1f;

    protected override void Activate()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        Vector3 endPoint = ray.GetPoint(range);

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            endPoint = hit.point;

            IDamageable target = hit.collider.GetComponent<IDamageable>();
            if (target != null)
            {
                // No more PlayerStats parameter — charge awards via static event
                target.TakeDamage(damage);

                Collider[] nearby = Physics.OverlapSphere(hit.point, chainRange);
                foreach (var col in nearby)
                {
                    if (col.gameObject == hit.collider.gameObject) continue;
                    IDamageable chainTarget = col.GetComponent<IDamageable>();
                    if (chainTarget == null) continue;

                    chainTarget.TakeDamage((int)(damage * 0.5f));
                    StartCoroutine(ShowChainArc(hit.point, col.transform.position));
                    break;
                }
            }
        }

        StartCoroutine(ShowBolt(transform.position, endPoint));
        StartCooldown();
    }

    private System.Collections.IEnumerator ShowBolt(Vector3 start, Vector3 end)
    {
        boltLine.enabled = true;
        float timer = 0f;
        int segments = 12; // More segments = more jagged lightning
        boltLine.positionCount = segments;
        boltLine.startWidth = 0.3f;
        boltLine.endWidth = 0.05f;
        while (timer < beamDuration)
        {
            boltLine.SetPosition(0, start); // Origin stays at Zeus
            
            for (int i = 1; i < segments - 1; i++)
            {
                // Calculate a point along the straight line
                float fraction = (float)i / (segments - 1);
                Vector3 midPoint = Vector3.Lerp(start, end, fraction);

                // Add random "divine jitter" to make it look like lightning
                float jitterAmount = 0.5f; 
                midPoint += Random.insideUnitSphere * jitterAmount;
                
                boltLine.SetPosition(i, midPoint);
            }

            boltLine.SetPosition(segments - 1, end); // End stays at target
            
            timer += Time.deltaTime;
            yield return null; // This causes the bolt to "flicker" every frame
        }
        
        boltLine.enabled = false;
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