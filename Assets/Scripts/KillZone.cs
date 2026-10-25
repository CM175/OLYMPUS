using UnityEngine;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.root.TryGetComponent<IDamageable>(out IDamageable damageable))
        {
            damageable.TakeDamage(9999); 
        }
    }
}