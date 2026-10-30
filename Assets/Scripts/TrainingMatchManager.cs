using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TrainingMatchState
{
    Waiting,
    Playing,
    Finished
}

public class TrainingMatchManager : MonoBehaviour
{
    [Header("Match Settings")]
    [SerializeField] private float matchDuration = 300f;
    [SerializeField] private int eliminationScoreLimit = 10;
    [SerializeField] private float respawnDelay = 5f;
    [SerializeField] private float restartDelay = 0.25f;

    [Header("Match Rewards")]
    [SerializeField] private float winReward = 2f;
    [SerializeField] private float lossReward = -2f;

    public TrainingMatchState State { get; private set; } = TrainingMatchState.Waiting;
    public int TeamAScore { get; private set; }
    public int TeamBScore { get; private set; }
    public float TimeRemaining { get; private set; }
    public float RespawnDelay => respawnDelay;
    public bool IsMatchActive => State == TrainingMatchState.Playing;

    public event Action OnMatchStarted;
    public event Action<Team?> OnMatchEnded;

    private TrainingArena _arena;
    private readonly HashSet<RLEnemyCombatAgent> _respawning = new HashSet<RLEnemyCombatAgent>();
    private bool _restartQueued;

    private void Awake()
    {
        _arena = GetComponent<TrainingArena>();
        if (_arena == null) _arena = GetComponentInParent<TrainingArena>();
    }

    private void Start()
    {
        StartMatch();
    }

    private void Update()
    {
        if (!IsMatchActive) return;

        TimeRemaining -= Time.deltaTime;
        if (TimeRemaining > 0f) return;

        TimeRemaining = 0f;
        if (TeamAScore > TeamBScore) FinishMatch(Team.TeamA);
        else if (TeamBScore > TeamAScore) FinishMatch(Team.TeamB);
        else FinishMatch(null);
    }

    public void StartMatch()
    {
        TeamAScore = 0;
        TeamBScore = 0;
        TimeRemaining = matchDuration;
        _respawning.Clear();
        _restartQueued = false;
        State = TrainingMatchState.Playing;
        Debug.Log("[TrainingMatch] Match started.");
        OnMatchStarted?.Invoke();
    }

    public void RegisterElimination(Team killerTeam)
    {
        if (!IsMatchActive) return;

        if (killerTeam == Team.TeamA) TeamAScore++;
        else if (killerTeam == Team.TeamB) TeamBScore++;
        else return;

        Debug.Log($"[TrainingMatch] Team A: {TeamAScore} | Team B: {TeamBScore}");

        if (TeamAScore >= eliminationScoreLimit) FinishMatch(Team.TeamA);
        else if (TeamBScore >= eliminationScoreLimit) FinishMatch(Team.TeamB);
    }

    public void ScheduleRespawn(RLEnemyCombatAgent agent)
    {
        if (!IsMatchActive || agent == null || !_respawning.Add(agent)) return;
        StartCoroutine(RespawnRoutine(agent));
    }

    private IEnumerator RespawnRoutine(RLEnemyCombatAgent agent)
    {
        yield return new WaitForSeconds(respawnDelay);
        _respawning.Remove(agent);

        if (IsMatchActive && agent != null && agent.IsTrainingDead)
            agent.RespawnForTraining();
    }

    public void FinishMatch(Team? winner)
    {
        if (!IsMatchActive) return;
        State = TrainingMatchState.Finished;
        Debug.Log(winner.HasValue
            ? $"[TrainingMatch] {winner.Value} wins!"
            : "[TrainingMatch] Match ended in a draw.");

        OnMatchEnded?.Invoke(winner);

        if (_arena != null)
        {
            if (_arena.agent != null)
            {
                _arena.agent.ApplyMatchOutcome(winner, winReward, lossReward);
                _arena.agent.EndEpisodeOnce();
            }
            if (_arena.opponent != null)
            {
                _arena.opponent.ApplyMatchOutcome(winner, winReward, lossReward);
                _arena.opponent.EndEpisodeOnce();
            }
        }

        if (!_restartQueued)
        {
            _restartQueued = true;
            StartCoroutine(RestartMatchRoutine());
        }
    }

    private IEnumerator RestartMatchRoutine()
    {
        yield return new WaitForSeconds(restartDelay);
        if (_arena != null) _arena.ResetObjectiveState();
        StartMatch();
    }

    public void FinishFromObjective(PointOwner winner)
    {
        switch (winner)
        {
            case PointOwner.TeamA: FinishMatch(Team.TeamA); break;
            case PointOwner.TeamB: FinishMatch(Team.TeamB); break;
            default: FinishMatch(null); break;
        }
    }
}
