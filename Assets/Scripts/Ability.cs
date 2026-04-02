using UnityEngine;

public abstract class Ability : MonoBehaviour
{
    public float cooldown;
    public Camera playerCamera;
    [System.NonSerialized]
    public float lastUsedTime = -999f;
    [System.NonSerialized] public bool isExecuting = false;

    public GameObject vfxPrefab;
    public Sprite sprite;

    public virtual void OnPress() {}
    public virtual void OnRelease() {}
    public void Use()
    {
        if (!CanUse()) return;
        isExecuting = true;
        Activate();
    }

    protected virtual bool CanUse() => !IsOnCooldown() && !isExecuting;

    protected abstract void Activate();
    
    public bool IsOnCooldown()
    {
        return Time.time < lastUsedTime + cooldown;
    }

    public void StartCooldown()
    {
        lastUsedTime = Time.time;
        isExecuting = false;
    }
}
