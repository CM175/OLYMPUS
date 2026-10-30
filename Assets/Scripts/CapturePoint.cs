using System.Collections.Generic;
using UnityEngine;

public enum PointOwner { Neutral, TeamA, TeamB }

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class CapturePoint : MonoBehaviour
{
    [Header("Capture Timings")]
    [Tooltip("Capture % gained per second by 1 player")]
    [SerializeField] private float baseCaptureRate = 14.0f;
    [Tooltip("Cap speed multiplier for extra players (e.g. 1x, 1.5x, 2x)")]
    [SerializeField] private float maxCaptureMultiplier = 2.0f;
    [SerializeField] private float decayRate = 10.0f;
    [SerializeField] private float scoreRate = 0.83f;
    [SerializeField] private float overtimeFuseDuration = 4.0f;

    [Header("Live Match Status")]
    public PointOwner controllingTeam = PointOwner.Neutral;
    public Team currentChallenger = Team.TeamA;
    [Range(0f, 100f)] public float captureProgress = 0f;
    [Range(0f, 100f)] public float teamAScore = 0f;
    [Range(0f, 100f)] public float teamBScore = 0f;
    public bool isContested = false;
    public bool isOvertime = false;

    private readonly HashSet<TeamMember> teamAPlayers = new();
    private readonly HashSet<TeamMember> teamBPlayers = new();
    private float overtimeFuseTimer = 0f;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.root.TryGetComponent<TeamMember>(out var member))
        {
            if (member.team == Team.TeamA) teamAPlayers.Add(member);
            else if (member.team == Team.TeamB) teamBPlayers.Add(member);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.transform.root.TryGetComponent<TeamMember>(out var member))
        {
            if (member.team == Team.TeamA) teamAPlayers.Remove(member);
            else if (member.team == Team.TeamB) teamBPlayers.Remove(member);
        }
    }

    private void Update()
    {
        PurgeInvalidPlayers();
        EvaluateCaptureLogic();
        UpdateScoreAndOvertime();
    }

    private void PurgeInvalidPlayers()
    {
        teamAPlayers.RemoveWhere(IsInvalidCaptureMember);
        teamBPlayers.RemoveWhere(IsInvalidCaptureMember);
    }

    private bool IsInvalidCaptureMember(TeamMember member)
    {
        if (member == null || !member.gameObject.activeInHierarchy) return true;
        return member.TryGetComponent<EnemyHealth>(out var health) && health.IsDead;
    }

    private void EvaluateCaptureLogic()
    {
        int countA = teamAPlayers.Count;
        int countB = teamBPlayers.Count;
        isContested = countA > 0 && countB > 0;
        if (isContested) return;

        if (countA > 0) ProcessCaptureTick(Team.TeamA, countA);
        else if (countB > 0) ProcessCaptureTick(Team.TeamB, countB);
        else if (controllingTeam != PointOwner.Neutral)
        {
            if (captureProgress < 100f)
                captureProgress = Mathf.Min(100f, captureProgress + decayRate * Time.deltaTime);
        }
        else if (captureProgress > 0f)
        {
            captureProgress = Mathf.Max(0f, captureProgress - decayRate * Time.deltaTime);
        }
    }

    private void ProcessCaptureTick(Team advancingTeam, int playerCount)
    {
        PointOwner advancingOwner = advancingTeam == Team.TeamA ? PointOwner.TeamA : PointOwner.TeamB;
        float speedMult = Mathf.Min(1f + (playerCount - 1) * 0.5f, maxCaptureMultiplier);
        float progressDelta = baseCaptureRate * speedMult * Time.deltaTime;

        if (controllingTeam == PointOwner.Neutral)
        {
            if (currentChallenger != advancingTeam)
            {
                captureProgress -= progressDelta;
                if (captureProgress <= 0f)
                {
                    captureProgress = 0f;
                    currentChallenger = advancingTeam;
                }
            }
            else
            {
                captureProgress += progressDelta;
                if (captureProgress >= 100f)
                {
                    captureProgress = 100f;
                    controllingTeam = advancingOwner;
                    if (isOvertime) isOvertime = false;
                }
            }
        }
        else if (controllingTeam == advancingOwner)
        {
            if (captureProgress < 100f)
                captureProgress = Mathf.Min(100f, captureProgress + progressDelta);
        }
        else
        {
            if (captureProgress > 0f && currentChallenger != advancingTeam)
            {
                captureProgress -= progressDelta;
                if (captureProgress <= 0f)
                {
                    captureProgress = 0f;
                    controllingTeam = PointOwner.Neutral;
                    currentChallenger = advancingTeam;
                }
            }
            else
            {
                captureProgress += progressDelta;
                if (captureProgress >= 100f)
                {
                    captureProgress = 100f;
                    controllingTeam = advancingOwner;
                    if (isOvertime) isOvertime = false;
                }
            }
        }
    }

    private void UpdateScoreAndOvertime()
    {
        if (controllingTeam == PointOwner.Neutral) return;

        bool teamAHolding = controllingTeam == PointOwner.TeamA;
        ref float currentHolderScore = ref (teamAHolding ? ref teamAScore : ref teamBScore);
        int attackingCount = teamAHolding ? teamBPlayers.Count : teamAPlayers.Count;

        if (currentHolderScore < 99f)
        {
            currentHolderScore = Mathf.Min(99f, currentHolderScore + scoreRate * Time.deltaTime);
        }
        else if (attackingCount > 0)
        {
            isOvertime = true;
            overtimeFuseTimer = overtimeFuseDuration;
        }
        else if (isOvertime)
        {
            overtimeFuseTimer -= Time.deltaTime;
            if (overtimeFuseTimer <= 0f)
            {
                isOvertime = false;
                currentHolderScore = 100f;
                DeclareWinner(controllingTeam);
            }
        }
        else
        {
            currentHolderScore = 100f;
            DeclareWinner(controllingTeam);
        }
    }

    private void DeclareWinner(PointOwner winner)
    {
        if (!enabled) return;
        enabled = false;
        Debug.Log($"Temple secured! Victor: {winner}");

        TrainingArena arena = GetComponentInParent<TrainingArena>();
        if (arena != null && arena.MatchManager != null)
        {
            arena.MatchManager.FinishFromObjective(winner);
        }
        else
        {
            Debug.LogWarning("CapturePoint has no TrainingMatchManager; objective win was not forwarded.", this);
            enabled = true;
        }
    }

    /// <summary>Called by TrainingArena between matches. Keeps current trigger occupants tracked.</summary>
    public void ResetForTraining()
    {
        controllingTeam = PointOwner.Neutral;
        currentChallenger = Team.TeamA;
        captureProgress = 0f;
        teamAScore = 0f;
        teamBScore = 0f;
        isContested = false;
        isOvertime = false;
        overtimeFuseTimer = 0f;
        enabled = true;
        PurgeInvalidPlayers();
    }
}
