using UnityEngine;

public enum Team { TeamA, TeamB }

public class TeamMember : MonoBehaviour
{
    public Team team;

    // Helper method to check if another object is an enemy
    public bool IsEnemy(GameObject other)
    {
        if (other.TryGetComponent<TeamMember>(out var otherTeam))
        {
            return otherTeam.team != this.team;
        }
        return false;
    }
}