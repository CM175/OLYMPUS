using System.Text;
using UnityEngine;

/// <summary>
/// TEMPORARY, read-only diagnostic. Changes nothing in the game: it only logs the first frame a bot drops
/// below the floor, with what the bot was doing. Auto-attaches to every RLEnemyCombatAgent in the scene.
/// Delete this file when you're done.
/// </summary>
public class FallDiagnostics : MonoBehaviour
{
    [SerializeField] private float belowY = -0.3f;   // training arena floors are at world y = 0

    private AbilityManager _abilities;
    private AIMovementController _movement;
    private HeroManager _hero;
    private float _prevY;
    private float _prevDt;
    private bool _prevGrounded;
    private bool _hasPrev;
    private bool _reported;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        foreach (var agent in FindObjectsByType<RLEnemyCombatAgent>(FindObjectsSortMode.None))
            if (agent.GetComponent<FallDiagnostics>() == null)
                agent.gameObject.AddComponent<FallDiagnostics>();
    }

    private void LateUpdate()   // runs after abilities' coroutines have moved the bot this frame
    {
        if (_movement == null)
        {
            _abilities = GetComponent<AbilityManager>();
            _movement = GetComponent<AIMovementController>();
            _hero = GetComponent<HeroManager>();
            if (_movement == null) return;
        }

        float y = transform.position.y;
        bool grounded = _movement.IsGrounded;
        if (y > -0.1f) _reported = false;

        if (!_reported && _hasPrev && y < belowY)
        {
            _reported = true;
            var sb = new StringBuilder();
            string hero = (_hero != null && _hero.currentHero != null) ? _hero.currentHero.heroName : "?";
            sb.Append($"[FallDiagnostics] {name} ({hero}) went below the floor: y {_prevY:F2} -> {y:F2} (dropped {_prevY - y:F2} m in ONE frame). ");
            sb.Append($"dt={Time.deltaTime:F3}s (previous frame {_prevDt:F3}s), timeScale={Time.timeScale}, fixedDt={Time.fixedDeltaTime}, maxDt={Time.maximumDeltaTime:F3}. ");
            sb.Append($"groundedNow={grounded}, groundedPrevFrame={_prevGrounded}, dashing={_movement.IsDashing}, ");
            sb.Append($"externalForce={_movement.IsUnderExternalForce}, stunned={_movement.IsStunned}. ");
            sb.Append("Executing: " + Executing());
            Debug.LogWarning(sb.ToString(), this);
        }

        _prevY = y;
        _prevDt = Time.deltaTime;
        _prevGrounded = grounded;
        _hasPrev = true;
    }

    private string Executing()
    {
        if (_abilities == null) return "(no AbilityManager)";
        var sb = new StringBuilder();
        void Add(string slot, Ability a)
        {
            if (a != null && a.isExecuting) sb.Append($"{slot}={a.GetType().Name} ");
        }
        Add("primaryAttack", _abilities.primaryAttack);
        Add("primaryAbility", _abilities.primaryAbility);
        Add("secondaryAbility", _abilities.secondaryAbility);
        Add("mobilityAbility", _abilities.mobilityAbility);
        Add("ultimateAbility", _abilities.ultimateAbility);
        return sb.Length > 0 ? sb.ToString().Trim() : "none";
    }
}