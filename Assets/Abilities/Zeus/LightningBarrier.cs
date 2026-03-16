using UnityEngine;
using System.Collections;

public class LightningBarrier : Ability
{
    public float duration = 4f;
    public int contactDamage = 10;

    protected override void Activate()
    {
        if (vfxPrefab == null) return;

        Transform playerTransform = transform.root;
        // Add the vertical offset to the spawn position
        Vector3 spawnPos = playerTransform.position + new Vector3(0, 1, 0);
        
        GameObject barrier = Instantiate(vfxPrefab, spawnPos, playerTransform.rotation);
        
        // PARENT the barrier to the player so it follows their movement
        barrier.transform.SetParent(playerTransform);
        barrier.transform.localPosition = new Vector3(0, 1, 0);
        
        // FIX: Assign the attacker stats to the barrier's damage script
        BarrierDamage damageScript = barrier.GetComponent<BarrierDamage>();
        if (damageScript != null)
        {
            damageScript.attackerStats = playerTransform.GetComponent<PlayerStats>();
        }

        Destroy(barrier, duration);
        Debug.Log("Lightning Barrier activated");
        StartCoroutine(WaitToStartCooldown(duration));
    }

    private IEnumerator WaitToStartCooldown(float time)
    {
        yield return new WaitForSeconds(time);
        StartCooldown();
    }
}


