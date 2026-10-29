using UnityEngine;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"KillZone: {other.name} entered kill zone.");
        if (other.transform.root.TryGetComponent(out IDamageable damageable))
        {
            Debug.Log($"KillZone: {other.name} entered kill zone, killing it.");
            damageable.TakeDamage(damageable.CurrentHealth); 
        }
    }
}
