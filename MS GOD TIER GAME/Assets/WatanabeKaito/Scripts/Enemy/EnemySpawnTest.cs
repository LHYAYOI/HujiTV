using UnityEngine;
using UnityEngine.InputSystem;

public class EnemySpawnTest : MonoBehaviour
{
    [SerializeField] private EnemySpawnManager m_enemySpawnManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_enemySpawnManager.SpawnSplineMoveEnemy("MiniGhost_A", "test_spline1", "testPosition1");

        m_enemySpawnManager.SpawnSplineMoveEnemy("MiniGhost_B", "test_spline2", "testPosition2");

        m_enemySpawnManager.SpawnSplineMoveEnemy("MiniGhost_C", "test_spline3", "testPosition3");


    }

    // Update is called once per frame
    void Update()
    {
       if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            m_enemySpawnManager.RemoveAllActiveEnemies();
        }


    }

    public void OnSpawnEnemy()
    {
        m_enemySpawnManager.SpawnSplineMoveEnemy("MiniGhost_A", "test_spline", "testPosition");
    }
}
