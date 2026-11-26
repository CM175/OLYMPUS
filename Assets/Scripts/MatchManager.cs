using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using System.Linq;

public class MatchManager : MonoBehaviour
{
    public HUDController hudController;

    [Header("Prefabs Layout")]
    public GameObject humanPlayerRootPrefab;
    public GameObject aiBotRootPrefab;

    [Header("Character Source")]
    public CharacterDatabase characterDatabase;

    [Header("Spawn Points")]
    public OlympusSpawnPoint[] teamASpawns;
    public OlympusSpawnPoint[] teamBSpawns;
    public float teamAMaxPlayers = 5;
    public float teamBMaxPlayers = 5;

    [Header("Respawn Settings")]
    public float playerRespawnDelay = 3f;
    public float aiRespawnDelay = 5f;

    [Header("Death Spectator View")]
    [Tooltip("Scene camera overlooking the arena, activated when the human player is waiting to respawn.")]
    public Camera deathSpectatorCamera;

    private Hero _savedPlayerHero;

    void Start()
    {
        SetupSpawnPoints();
        if (deathSpectatorCamera != null)
            deathSpectatorCamera.gameObject.SetActive(false);

        if (characterDatabase == null || characterDatabase.CharacterCount == 0)
        {
            Debug.LogError("[MatchManager] MainCharacterDatabase is not assigned or contains no character prefabs!");
            return;
        }
        SetupAIs();
    }

    public void SetupSpawnPoints()
    {
        OlympusSpawnPoint[] allSpawns = FindObjectsByType<OlympusSpawnPoint>(FindObjectsSortMode.None);

        teamASpawns = allSpawns.Where(s => s != null && s.team == Team.TeamA).ToArray();
        teamBSpawns = allSpawns.Where(s => s != null && s.team == Team.TeamB).ToArray();
    }
    void SetupAIs()
    {
        int totalAvailableHeroes = characterDatabase.CharacterCount;
        // TEAM A (AI Companions)
        for (int i = 1; i < teamAMaxPlayers; i++)
        {
            Hero hero = characterDatabase.GetHeroAtIndex((i+Random.Range(0, totalAvailableHeroes)) % totalAvailableHeroes);
            SpawnAIBot(hero, Team.TeamA, teamASpawns[Random.Range(0, teamASpawns.Length)].transform);
        }

        // TEAM B (AI Competitors)
        for (int i = 0; i < teamBMaxPlayers; i++)
        {
            Hero hero = characterDatabase.GetHeroAtIndex((i+Random.Range(0, totalAvailableHeroes)) % totalAvailableHeroes);
            SpawnAIBot(hero, Team.TeamB, teamBSpawns[Random.Range(0, teamBSpawns.Length)].transform);
        }
    }

    public void SpawnHumanPlayer(Hero selectedHeroPrefab, float savedUltimateCharge = 0f)
    {
        _savedPlayerHero = selectedHeroPrefab;

        // Disable death spectator camera once the player entity is back in world
        if (deathSpectatorCamera != null)
            deathSpectatorCamera.gameObject.SetActive(false);

        Transform spawnPoint = teamASpawns[Random.Range(0, teamASpawns.Length)].transform; // Assuming the first spawn point is for the human player
        GameObject playerRoot = Instantiate(humanPlayerRootPrefab, spawnPoint.position, spawnPoint.rotation);
        
        if (playerRoot.TryGetComponent<PlayerHealth>(out var pHealth))
        {
            pHealth.spawnPoint = spawnPoint;
            pHealth.matchManager = this;
        }

        if (hudController != null)
            hudController.Initialize(playerRoot);

        var teamConfig = playerRoot.GetComponent<TeamMember>();
        teamConfig.team = Team.TeamA;

        var manager = playerRoot.GetComponent<HeroManager>();
        manager.LoadHero(selectedHeroPrefab);

        var playerStats = playerRoot.GetComponent<PlayerStats>();
        playerStats.currentUltimateCharge = savedUltimateCharge; 
    }

    public void SpawnAIBot(Hero heroPrefab, Team team, Transform spawnPoint, float savedUltimateCharge = 0f)
    {
        GameObject aiRoot = Instantiate(aiBotRootPrefab, spawnPoint.position, spawnPoint.rotation);

        if (aiRoot.TryGetComponent<EnemyHealth>(out var eHealth))
        {
            eHealth.spawnPoint = spawnPoint;
            eHealth.matchManager = this;
            eHealth.heroPrefab = heroPrefab;
            eHealth.team = team;
        }

        var teamConfig = aiRoot.GetComponent<TeamMember>();
        teamConfig.team = team;

        var manager = aiRoot.GetComponent<HeroManager>();
        manager.LoadHero(heroPrefab);

        var aiStats = aiRoot.GetComponent<PlayerStats>();
        aiStats.currentUltimateCharge = savedUltimateCharge;
    }

    public void RespawnPlayer(float savedUltimateCharge = 0)
    {
        StartCoroutine(RespawnPlayerRoutine(savedUltimateCharge));
    }
    

    private IEnumerator RespawnPlayerRoutine(float savedUltimateCharge)
    {
        // 1. Enable spectator camera
        if (deathSpectatorCamera != null)
            deathSpectatorCamera.gameObject.SetActive(true);

        // 2. Display death UI panel
        if (hudController != null)
            hudController.ShowDeathScreen();

        // 3. Count down timer per frame
        float timer = playerRespawnDelay;
        while (timer > 0f)
        {
            if (hudController != null)
                hudController.UpdateRespawnTimer(timer);

            timer -= Time.deltaTime;
            yield return null;
        }

        // 4. Recreate player instance
        if (_savedPlayerHero != null)
        {
            SpawnHumanPlayer(_savedPlayerHero, savedUltimateCharge);
        }
    }

    public void RespawnAI(Hero heroPrefab, Team team, Transform spawnPoint, float savedUltimateCharge = 0f)
    {
        StartCoroutine(RespawnAIRoutine(heroPrefab, team, spawnPoint, savedUltimateCharge));
    }

    private IEnumerator RespawnAIRoutine(Hero heroPrefab, Team team, Transform spawnPoint, float savedUltimateCharge)
    {
        yield return new WaitForSeconds(aiRespawnDelay);
        if (heroPrefab != null && spawnPoint != null)
        {
            SpawnAIBot(heroPrefab, team, spawnPoint, savedUltimateCharge);
        }
    }

    
}