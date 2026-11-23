using UnityEngine;

public class HymnOfRadiance : Ability
{
    public float burstRadius = 8f;
    public int healAmount = 50;
    public float healOrDamageDuration =1.5f; 

    protected override void Activate()
    {
        //Debug.Log($"Activating hymn of radiance");
        Vector3 origin = transform.root.position;
        Collider[] targets = AbilityPhysics.OverlapSphere(origin, burstRadius);

        foreach (var target in targets)
        {
            Transform targetRoot = target.transform.root;
            
            var myTeam = transform.root.GetComponent<TeamMember>();
            
            if (myTeam != null && !myTeam.IsEnemy(targetRoot.gameObject))
            {
                var pHealth = targetRoot.GetComponent<IDamageable>();
                pHealth?.HealOverTime(healAmount, healOrDamageDuration);
            }
        }

        if (vfxPrefab != null)
        {
            Instantiate(vfxPrefab, origin, Quaternion.identity);
        }
        else
        {
            AbilityVfx.Ring(origin + Vector3.up * 0.1f, AbilityVfx.Gold, burstRadius, 0.55f);
            AbilityVfx.Burst(origin + Vector3.up, AbilityVfx.Gold, 1.8f, 36);
        }

        StartCooldown();
    }
}