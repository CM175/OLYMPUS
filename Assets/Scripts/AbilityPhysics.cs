using UnityEngine;

/// <summary>
/// Shared physics queries for abilities. Overlap/cast queries ignore the Layer Collision Matrix and only
/// respect the mask passed in, so without one they still find characters on the ShadowRealm layer
/// (Hades' ShadowStep). Route ability sweeps through here so shadowed characters are untouchable.
/// </summary>
public static class AbilityPhysics
{
    private static int _hittableMask;
    private static bool _maskBuilt;

    /// <summary>Every layer except ShadowRealm.</summary>
    public static int HittableMask
    {
        get
        {
            if (!_maskBuilt)
            {
                _hittableMask = ~LayerMask.GetMask("ShadowRealm");
                _maskBuilt = true;
            }
            return _hittableMask;
        }
    }

    public static Collider[] OverlapSphere(Vector3 center, float radius)
        => Physics.OverlapSphere(center, radius, HittableMask);
}