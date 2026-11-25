using UnityEngine;

public class OlympusSpawnPoint : MonoBehaviour
{
    public Team team = Team.TeamA;

    void OnDrawGizmos()
    {
        Gizmos.color = team == Team.TeamA
            ? new Color(0.25f, 0.55f, 1f)
            : new Color(1f, 0.35f, 0.3f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.35f);
        Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, transform.forward * 1.2f);
    }
}