using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float spawnInterval = 5f;
    public int maxEnemies = 10;
    private float nextSpawnTime = 0f;
    private Vector3 Spawnposition;
    public Vector3 spawnAreaSize = new Vector3(10f, 0f, 10f);
    void Start()
    {

    }


    void Update()
    {


        if (Time.time >= nextSpawnTime && GameObject.FindGameObjectsWithTag("Enemy").Length < maxEnemies)
        {
            SpawnEnemy();
            nextSpawnTime = Time.time + spawnInterval;
        }
    }
    void SpawnEnemy()
    {
        Spawnposition = new Vector3(
            transform.position.x + Random.Range(-spawnAreaSize.x / 2, spawnAreaSize.x / 2),
            transform.position.y,
            transform.position.z + Random.Range(-spawnAreaSize.z / 2, spawnAreaSize.z / 2)
        );
        Instantiate(enemyPrefab, Spawnposition, Quaternion.identity);
        

    }
}
