using UnityEngine;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Check if the object has a PlayerController
        if (other.TryGetComponent<PlayerController>(out PlayerController player))
        {
            // Reset position to a spawn point or apply damage
            Debug.Log("Player fell into the clouds!");
            other.transform.position = new Vector3(0, 5, 45); // Example Spawn
        }
    }
}