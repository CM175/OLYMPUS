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
    [SerializeField] private float winReward = 1f;
    [SerializeField] private float lossReward = -1f;

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
    private int _matchId;
    private bool _finishPending;

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
        // Prevent huge physics/movement jumps when training at high time scales
        Time.maximumDeltaTime = 0.05f;
        TeamAScore = 0;
        TeamBScore = 0;
        TimeRemaining = matchDuration;
        _respawning.Clear();
        _restartQueued = false;
        _finishPending = false;
        _matchId++;   // invalidates respawn timers left over from the previous match
        State = TrainingMatchState.Playing;
        ReviveDeadAgents();
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

        // Don't finish inside the death event: the killer's kill reward is booked by a later subscriber,
        // and EndEpisode() before it would push that reward into the next episode. Resolve in LateUpdate.
        if (TeamAScore >= eliminationScoreLimit || TeamBScore >= eliminationScoreLimit)
            _finishPending = true;
    }

    private void LateUpdate()
    {
        if (!_finishPending || !IsMatchActive) return;
        _finishPending = false;

        if (TeamAScore > TeamBScore) FinishMatch(Team.TeamA);
        else if (TeamBScore > TeamAScore) FinishMatch(Team.TeamB);
        else FinishMatch(null);
    }

    private void ReviveDeadAgents()
    {
        if (_arena == null) return;
        if (_arena.agent != null && _arena.agent.IsTrainingDead) _arena.agent.RespawnForTraining();
        if (_arena.opponent != null && _arena.opponent.IsTrainingDead) _arena.opponent.RespawnForTraining();
    }

    public void ScheduleRespawn(RLEnemyCombatAgent agent)
    {
        if (!IsMatchActive || agent == null || !_respawning.Add(agent)) return;
        StartCoroutine(RespawnRoutine(agent, _matchId));
    }

    private IEnumerator RespawnRoutine(RLEnemyCombatAgent agent, int matchId)
    {
        yield return new WaitForSeconds(respawnDelay);
        if (matchId != _matchId) yield break;   // match ended/restarted while waiting
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
        if (_arena != null)
        {
            _arena.ResetObjectiveState();
            _arena.RerollHeroes();
            if (_arena.agent != null) _arena.agent.ResyncAfterHeroChange();
            if (_arena.opponent != null) _arena.opponent.ResyncAfterHeroChange();
        }
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