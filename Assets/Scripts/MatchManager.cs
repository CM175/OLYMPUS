using System.Collections;
using UnityEngine;

public class MatchManager : MonoBehaviour
{
    public HUDController hudController;

    [Header("Prefabs Layout")]
    public GameObject humanPlayerRootPrefab;
    public GameObject aiBotRootPrefab;

    [Header("Character Source")]
    public CharacterDatabase characterDatabase;

    [Header("Spawn Points")]
    public Transform[] teamASpawns;
    public Transform[] teamBSpawns;
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
        if (deathSpectatorCamera != null)
            deathSpectatorCamera.gameObject.SetActive(false);

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
        // TEAM A (AI Companions)
        for (int i = 1; i < teamAMaxPlayers; i++)
        {
            Hero hero = characterDatabase.GetHeroAtIndex((i+Random.Range(0, totalAvailableHeroes)) % totalAvailableHeroes);
            SpawnAIBot(hero, Team.TeamA, teamASpawns[i % teamASpawns.Length]);
        }

        // TEAM B (AI Competitors)
        for (int i = 0; i < teamBMaxPlayers; i++)
        {
            Hero hero = characterDatabase.GetHeroAtIndex((i+Random.Range(0, totalAvailableHeroes)) % totalAvailableHeroes);
            SpawnAIBot(hero, Team.TeamB, teamBSpawns[i% teamBSpawns.Length]);
        }
    }

    public void SpawnHumanPlayer(Hero selectedHeroPrefab)
    {
        _savedPlayerHero = selectedHeroPrefab;

        // Disable death spectator camera once the player entity is back in world
        if (deathSpectatorCamera != null)
            deathSpectatorCamera.gameObject.SetActive(false);

        GameObject playerRoot = Instantiate(humanPlayerRootPrefab, teamASpawns[0].position, teamASpawns[0].rotation);
        
        if (playerRoot.TryGetComponent<PlayerHealth>(out var pHealth))
        {
            pHealth.spawnPoint = teamASpawns[0];
            pHealth.matchManager = this;
        }

        if (hudController != null)
            hudController.Initialize(playerRoot);

        var teamConfig = playerRoot.GetComponent<TeamMember>();
        teamConfig.team = Team.TeamA;

        var manager = playerRoot.GetComponent<HeroManager>();
        manager.LoadHero(selectedHeroPrefab);
    }

    public void SpawnAIBot(Hero heroPrefab, Team team, Transform spawnPoint)
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
    }

    public void RespawnPlayer()
    {
        StartCoroutine(RespawnPlayerRoutine());
    }

    private IEnumerator RespawnPlayerRoutine()
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
            SpawnHumanPlayer(_savedPlayerHero);
        }
    }

    public void RespawnAI(Hero heroPrefab, Team team, Transform spawnPoint)
    {
        StartCoroutine(RespawnAIRoutine(heroPrefab, team, spawnPoint));
    }

    private IEnumerator RespawnAIRoutine(Hero heroPrefab, Team team, Transform spawnPoint)
    {
        yield return new WaitForSeconds(aiRespawnDelay);
        if (heroPrefab != null && spawnPoint != null)
        {
            SpawnAIBot(heroPrefab, team, spawnPoint);
        }
    }
}