using UnityEngine;

public class WeaponShoot : MonoBehaviour
{
    public float range = 100f;
    public int damage = 25;

    public Camera fpsCamera;

    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            Shoot();
        }
    }

    void Shoot()
    {
        RaycastHit hit;

        if (Physics.Raycast(fpsCamera.transform.position, fpsCamera.transform.forward, out hit, range))
        {
            Debug.Log("Hit " + hit.transform.name);

            EnemyHealth enemy = hit.transform.GetComponent<EnemyHealth>();

            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }
        }
    }
}