using UnityEngine;
using UnityEngine.InputSystem;

public class EnemySpawnTest : MonoBehaviour
{
    [SerializeField] private EnemySpawnManager m_enemySpawnManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //m_enemySpawnManager.SpawnEnemy("Ghost_A", "test_spline");
        //m_enemySpawnManager.SpawnEnemy("Ghost_A", "test_spline2");

        m_enemySpawnManager.SpawnPopUpEnemy("Ghost_B", "testPosition");
    }

    // Update is called once per frame
    void Update()
    {
       
    }
}
