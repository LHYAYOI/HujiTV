using UnityEngine;
using UnityEngine.InputSystem;

public class EnemySpawnTest : MonoBehaviour
{
    [SerializeField] private EnemySpawnManager m_enemySpawnManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_enemySpawnManager.SpawnEnemy("test", "test_spline");
        m_enemySpawnManager.SpawnEnemy("test", "test_spline2");


    }

    // Update is called once per frame
    void Update()
    {
       
    }
}
