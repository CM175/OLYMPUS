using UnityEngine;

public class LightningBolt : Ability
{
    public Camera fpsCamera;
    public float range = 100f;
    public int damage = 25;

    protected override void Activate()
    {
        Debug.Log("Activated");
        RaycastHit hit;

        if (Physics.Raycast(fpsCamera.transform.position, fpsCamera.transform.forward, out hit, range))
        {
            EnemyHealth enemy = hit.transform.GetComponent<EnemyHealth>();

            if (enemy != null)
            {
                Debug.Log("hit enemy" );
                enemy.TakeDamage(damage);
            }
        }
    }
}