using System.Collections;
using UnityEngine;

public class BattleCry : Ability
{
    [Header("Stats")]
    public float duration = 5f;
    public float damageMultiplier = 1.75f;

    protected override void Activate()
    {
        PlayerStats stats = transform.root.GetComponent<PlayerStats>();

        if (stats == null)
        {
            //Debug.LogWarning("[BattleCry] No PlayerStats found — ability has no effect.");
            StartCooldown();
            return;
        }

        StartCoroutine(ApplyBuff(stats));
    }

    private IEnumerator ApplyBuff(PlayerStats stats)
    {
        stats.DamageMultiplier = damageMultiplier;
        //Debug.Log($"[BattleCry] Active — {damageMultiplier}x damage for {duration}s");

        yield return new WaitForSeconds(duration);

        stats.DamageMultiplier = 1f;
        //Debug.Log("[BattleCry] Expired.");

        StartCooldown();
    }
}
