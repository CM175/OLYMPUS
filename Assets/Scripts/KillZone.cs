using UnityEngine;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"KillZone triggered by: {other.name}");
        if (other.transform.root.TryGetComponent(out IDamageable damageable))
        {
            damageable.TakeDamage(damageable.CurrentHealth); 
        }
    }
}
