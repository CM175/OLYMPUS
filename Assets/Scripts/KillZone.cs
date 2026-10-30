using UnityEngine;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.root.TryGetComponent(out IDamageable damageable))
        {
            damageable.TakeDamage(damageable.CurrentHealth); 
        }
    }
}
