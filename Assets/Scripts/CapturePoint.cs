// CapturePoint.cs
using UnityEngine;

public class CapturePoint : MonoBehaviour
{
    public float captureRadius = 8f;
    public float captureSpeed = 10f; // points per second while held

    [Range(0f, 100f)]
    public float captureProgress = 50f; // starts neutral

    private int teamAPlayers = 0;
    private int teamBPlayers = 0;

    void Update()
    {
        int net = teamAPlayers - teamBPlayers;

        if (net > 0)
            captureProgress = Mathf.Min(100f, captureProgress + captureSpeed * Time.deltaTime);
        else if (net < 0)
            captureProgress = Mathf.Max(0f, captureProgress - captureSpeed * Time.deltaTime);

        // TODO: award match points when captureProgress hits 0 or 100
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerTeam>(out var p))
        {
            if (p.team == Team.A) teamAPlayers++;
            else teamBPlayers++;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerTeam>(out var p))
        {
            if (p.team == Team.A) teamAPlayers--;
            else teamBPlayers--;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, captureRadius);
    }
}