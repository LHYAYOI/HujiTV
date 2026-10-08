using UnityEngine;
using UnityEngine.InputSystem;

public class EnemySpawnTest : MonoBehaviour
{
    [SerializeField] private EnemySpawnManager m_enemySpawnManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_enemySpawnManager.SpawnSplineMoveEnemy("MiniGhost_A", "test_spline");


    }

    // Update is called once per frame
    void Update()
    {
       
    }
}
