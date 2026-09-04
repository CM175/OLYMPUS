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
    [SerializeField] private float scoreRate = 0.83f; // ~120s to reach 100%
    [SerializeField] private float overtimeFuseDuration = 4.0f;

    [Header("Live Match Status")]
    public PointOwner controllingTeam = PointOwner.Neutral;
    public Team currentChallenger = Team.TeamA;
    [Range(0f, 100f)] public float captureProgress = 0f;
    [Range(0f, 100f)] public float teamAScore = 0f;
    [Range(0f, 100f)] public float teamBScore = 0f;
    public bool isContested = false;
    public bool isOvertime = false;

    // Track active TeamMembers directly instead of raw Colliders
    private readonly HashSet<TeamMember> teamAPlayers = new();
    private readonly HashSet<TeamMember> teamBPlayers = new();
    private float overtimeFuseTimer = 0f;

    private void Awake()
    {
        // Ensure trigger configuration
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

    // Handles player death, disables, or despawns mid-fight
    private void PurgeInvalidPlayers()
    {
        teamAPlayers.RemoveWhere(p => p == null || !p.gameObject.activeInHierarchy);
        teamBPlayers.RemoveWhere(p => p == null || !p.gameObject.activeInHierarchy);
    }

    private void EvaluateCaptureLogic()
    {
        int countA = teamAPlayers.Count;
        int countB = teamBPlayers.Count;

        isContested = (countA > 0 && countB > 0);

        // Progress freezes immediately if both teams contest
        if (isContested) return;

        if (countA > 0)
        {
            ProcessCaptureTick(Team.TeamA, countA);
        }
        else if (countB > 0)
        {
            ProcessCaptureTick(Team.TeamB, countB);
        }
        else
        {
            // Point is empty:
            // 1. If currently held by a team, any enemy neutralizing progress decays back up to 100%
            // 2. If completely neutral, partial neutral capture progress decays down to 0%
            if (controllingTeam != PointOwner.Neutral)
            {
                if (captureProgress < 100f)
                {
                    captureProgress = Mathf.Min(100f, captureProgress + decayRate * Time.deltaTime);
                }
            }
            else
            {
                if (captureProgress > 0f)
                {
                    captureProgress = Mathf.Max(0f, captureProgress - decayRate * Time.deltaTime);
                }
            }
        }
    }

    private void ProcessCaptureTick(Team advancingTeam, int playerCount)
    {
        PointOwner advancingOwner = (advancingTeam == Team.TeamA) ? PointOwner.TeamA : PointOwner.TeamB;
        float speedMult = Mathf.Min(1f + (playerCount - 1) * 0.5f, maxCaptureMultiplier);
        float progressDelta = baseCaptureRate * speedMult * Time.deltaTime;

        // SCENARIO 1: Point is completely Neutral (start of match or fully wiped)
        if (controllingTeam == PointOwner.Neutral)
        {
            if (currentChallenger != advancingTeam)
            {
                // Enemy had partial progress while neutral: decay it to 0 first
                captureProgress -= progressDelta;
                if (captureProgress <= 0f)
                {
                    captureProgress = 0f;
                    currentChallenger = advancingTeam;
                }
            }
            else
            {
                // Build meter up to 100% to lock ownership
                captureProgress += progressDelta;
                if (captureProgress >= 100f)
                {
                    captureProgress = 100f;
                    controllingTeam = advancingOwner;
                    if (isOvertime) isOvertime = false;
                }
            }
        }
        // SCENARIO 2: Friendly team controls the point
        else if (controllingTeam == advancingOwner)
        {
            // Friendly players push the meter back up to 100% if enemies partially neutralized it
            if (captureProgress < 100f)
            {
                captureProgress = Mathf.Min(100f, captureProgress + progressDelta);
            }
        }
        // SCENARIO 3: Enemy team controls the point (Advancing team is attacking)
        else
        {
            // Phase 1: Burn defender's hold from 100% down to 0% (Neutralize)
            if (captureProgress > 0f && currentChallenger != advancingTeam)
            {
                captureProgress -= progressDelta;
                if (captureProgress <= 0f)
                {
                    captureProgress = 0f;
                    controllingTeam = PointOwner.Neutral; // Point is now neutralized!
                    currentChallenger = advancingTeam;
                }
            }
            // Phase 2: Now neutral, build meter up to 100% to take over
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
            // Normal score tick: ticks even while contested up to 99%
            currentHolderScore = Mathf.Min(99f, currentHolderScore + scoreRate * Time.deltaTime);
        }
        else
        {
            // Controlling team is at 99%
            if (attackingCount > 0)
            {
                // Enemy is actively touching/contesting: hold Overtime indefinitely
                isOvertime = true;
                overtimeFuseTimer = overtimeFuseDuration;
            }
            else if (isOvertime)
            {
                // Overtime active but attackers stepped off: burn down the fuse
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
                // Reached 99% with nobody contesting: award the final point
                currentHolderScore = 100f;
                DeclareWinner(controllingTeam);
            }
        }
    }

    private void DeclareWinner(PointOwner winner)
    {
        enabled = false;
        Debug.Log($"Temple secured! Victor: {winner}");
    }
}