using UnityEngine;

public abstract class Ability : MonoBehaviour
{
    public float cooldown = 3f;
    protected float lastUsedTime;
    public Camera playerCamera;

    public virtual void Use()
    {
        if (Time.time < lastUsedTime + cooldown)
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
