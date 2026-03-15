using UnityEngine;

public abstract class Ability : MonoBehaviour
{
    public float cooldown;
    public Camera playerCamera;
    [System.NonSerialized]
    protected float lastUsedTime;

    protected virtual void Awake()
    {
        lastUsedTime = -999f;
    }
    public virtual void Use()
    {
        
        if (IsOnCooldown())
            return;
        Activate();
        lastUsedTime = Time.time;
    }
    public bool IsOnCooldown()
    {
        return Time.time < lastUsedTime + cooldown;
    }
    protected abstract void Activate();
}
