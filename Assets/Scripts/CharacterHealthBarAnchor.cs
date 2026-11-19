using UnityEngine;

[RequireComponent(typeof(IDamageable))]
public class CharacterHealthBarAnchor : MonoBehaviour
{
    [Header("Prefab & Colors")]
    [SerializeField] private FloatingHealthBar healthBarPrefab;
    [SerializeField] private Color allyColor = new Color(0.2f, 0.85f, 0.3f);
    [SerializeField] private Color enemyColor = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField] private bool hideForLocalPlayer = true;

    private IDamageable damageable;
    private FloatingHealthBar spawnedBar;
    private int lastKnownHealth = -1;
    private int lastKnownMaxHealth = -1;
    private bool lastKnownDeadState = false;

    private void Awake()
    {
        damageable = GetComponent<IDamageable>();
    }

    private void Start()
    {
        // Don't clutter the local player's screen with their own floating bar
        if (hideForLocalPlayer && GetComponent<PlayerHealth>() != null)
        {
            enabled = false;
            return;
        }

        if (healthBarPrefab == null) return;

        spawnedBar = Instantiate(healthBarPrefab);
        spawnedBar.Initialize(transform, DetermineTeamColor());
        SyncHealthBar(force: true);
    }

    private void LateUpdate()
    {
        if (spawnedBar == null) return;
        SyncHealthBar(force: false);
    }

    private void SyncHealthBar(bool force)
    {
        int current = damageable.CurrentHealth;
        int max = damageable.MaxHealth;
        bool isDead = damageable.IsDead;

        if (!force && current == lastKnownHealth && max == lastKnownMaxHealth && isDead == lastKnownDeadState)
        {
            return;
        }

        lastKnownHealth = current;
        lastKnownMaxHealth = max;
        lastKnownDeadState = isDead;

        float ratio = max > 0 ? (float)current / max : 0f;
        spawnedBar.SetHealthRatio(ratio, isDead);
    }

    private Color DetermineTeamColor()
    {
        // Check if this character is an EnemyHealth with a Team assigned
        TeamMember teamMember = GetComponent<TeamMember>();

        if (teamMember != null)
        {
            return teamMember.team == Team.TeamA ? allyColor : enemyColor;
        }
        return enemyColor;
    }

    private void OnDestroy()
    {
        if (spawnedBar != null)
        {
            Destroy(spawnedBar.gameObject);
        }
    }
}