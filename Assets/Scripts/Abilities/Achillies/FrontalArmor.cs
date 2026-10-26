using UnityEngine;

public class FrontalArmor : PassiveAbility, IInitializable
{
    [Header("Stats")]
    [Range(0f, 0.9f)]
    [Tooltip("0.25 = 25% damage reduction on all hits.")]
    public float damageReduction = 0.25f;

    public void Initialize(GameObject playerRoot)
    {
        /*PlayerHealth ph = playerRoot.GetComponent<>();
        if (ph == null)
        {
            Debug.LogError("[FrontalArmor] No PlayerHealth found on player root.");
            return;
        }
        ph.DamageReduction = damageReduction;
        Debug.Log($"[FrontalArmor] Damage reduction set to {damageReduction * 100f}%");
        */
    }

    protected override void Activate() { }
}