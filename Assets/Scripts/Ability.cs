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
    public virtual void Use()
    {
        if (IsOnCooldown() || isExecuting) return;
        
        isExecuting = true;
        Activate();
    }
    public bool IsOnCooldown()
    {
        return Time.time < lastUsedTime + cooldown;
    }

    public void StartCooldown()
    {
        lastUsedTime = Time.time;
        isExecuting = false;
    }
    protected abstract void Activate();
}
