using UnityEngine;

public class MatchManager : MonoBehaviour
{

    public HUDController hudController; 
    [Header("Prefabs Layout")]
    public GameObject humanPlayerRootPrefab; // Shell with PlayerController, AbilityManager, PlayerHealth, etc.
    public GameObject aiBotRootPrefab;       // Shell with EnemyAI, AbilityManager, EnemyHealth, etc.

    [Header("Character Source")]
    public CharacterDatabase characterDatabase; // Drag your MainCharacterDatabase.asset here!

    [Header("Spawn Points")]
    public Transform[] teamASpawns; // 5 positions in base A
    public Transform[] teamBSpawns; // 5 positions in base B

    void Start()
    {
        
        if (characterDatabase == null || characterDatabase.CharacterCount == 0)
        {
            Debug.LogError("[MatchManager] MainCharacterDatabase is not assigned or contains no character prefabs!");
            return;
        }

        SetupAIs();
    }

    void SetupAIs()
    {
        int totalAvailableHeroes = characterDatabase.CharacterCount;

        // ── TEAM A (Human Player + 4 AI Companions) ──
        for (int i = 1; i < 1; i++)
        {
            GameObject characterRoot = Instantiate(aiBotRootPrefab, teamASpawns[i].position, teamASpawns[i].rotation);
            if (characterRoot.TryGetComponent<EnemyHealth>(out var eHealth))
            {
                eHealth.spawnPoint = teamASpawns[i];
            }

            var teamConfig = characterRoot.GetComponent<TeamMember>();
            teamConfig.team = Team.TeamA;

            var manager = characterRoot.GetComponent<HeroManager>();
            manager.heroPrefab = characterDatabase.GetHeroAtIndex(i % totalAvailableHeroes);
        }
        // ── TEAM B (5 AI Competitors) ──
        for (int i = 0; i < 1; i++)
        {
            GameObject characterRoot = Instantiate(aiBotRootPrefab, teamBSpawns[i].position, teamBSpawns[i].rotation);
            
            if (characterRoot.TryGetComponent<EnemyHealth>(out var eHealth))
            {
                eHealth.spawnPoint = teamBSpawns[i];
            }

            var teamConfig = characterRoot.GetComponent<TeamMember>();
            teamConfig.team = Team.TeamB;

            // Offset selection sequence (e.g., i + 2) so Team B gets a different composition variance than Team A
            var manager = characterRoot.GetComponent<HeroManager>();
            manager.heroPrefab = characterDatabase.GetHeroAtIndex(/*(i + 2) % totalAvailableHeroes*/ 5);
        }
    }

    public void SpawnHumanPlayer(Hero selectedHeroPrefab)
    {

        // Spawn the player root container at your designated Team A home pad (Index 0)
        GameObject playerRoot = Instantiate(humanPlayerRootPrefab, teamASpawns[0].position, teamASpawns[0].rotation);
        hudController.Initialize(playerRoot);
        if (playerRoot.TryGetComponent<PlayerHealth>(out var pHealth))
        {
            pHealth.spawnPoint = teamASpawns[0];
        }

        // Tag team affiliation
        var teamConfig = playerRoot.GetComponent<TeamMember>();
        teamConfig.team = Team.TeamA;

        // Assign the hero they chose dynamically and initialize
        var manager = playerRoot.GetComponent<HeroManager>();
        manager.LoadHero(selectedHeroPrefab);
    }
}