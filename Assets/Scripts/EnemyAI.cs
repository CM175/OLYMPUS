using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    Transform player;
    public float speed = 3f;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }
    void Update()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0;

        transform.position += direction.normalized * speed * Time.deltaTime;

        transform.LookAt(player);
    }
}