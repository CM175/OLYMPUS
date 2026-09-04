using UnityEngine;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[KillZone] Object entered kill zone: {other.gameObject.name}");
        if (other.TryGetComponent<IDamageable>(out IDamageable damageable))
        {
            damageable.TakeDamage(9999); 
        }
    }
}