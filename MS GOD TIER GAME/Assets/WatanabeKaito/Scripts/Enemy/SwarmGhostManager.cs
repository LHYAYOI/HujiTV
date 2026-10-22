using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwarmGhostManager : MonoBehaviour
{
    [Header("群れお化けの一個体リスト")]
    [SerializeField] private List<GameObject> m_swarmGhostPrefabs;

    [Header("出現位置リスト")]
    [SerializeField] private List<Transform> m_spawnPositions;

    [Header("出現間隔（秒）")]
    [SerializeField] private float m_spawnInterval = 0.1f;

    private void Start()
    {
        StartCoroutine(SpawnSwarmGhostCoroutine());
    }

    private IEnumerator SpawnSwarmGhostCoroutine()
    {
        // プレハブや出現位置が設定されていない場合は終了
        if (m_swarmGhostPrefabs == null ||
            m_spawnPositions == null ||
            m_spawnPositions.Count == 0)
        {
            yield break;
        }

        // 有効な出現位置だけをコピー
        List<Transform> availablePositions = new List<Transform>();

        for (int i = 0; i < m_spawnPositions.Count; i++)
        {
            if (m_spawnPositions[i] != null && !availablePositions.Contains(m_spawnPositions[i]))
            {
                availablePositions.Add(m_spawnPositions[i]);
            }
        }

        // 出現位置がなくなるまで繰り返す
        while (availablePositions.Count > 0)
        {
            // ランダムにお化けのプレハブを選択
            int randomPrefabIndex = Random.Range(0, m_swarmGhostPrefabs.Count);

            // ランダムに出現位置を選択
            int randomIndex = Random.Range(0, availablePositions.Count);
            Transform spawnPosition = availablePositions[randomIndex];

            // 使用した出現位置をリストから削除
            availablePositions.RemoveAt(randomIndex);

            // お化けを子オブジェクトとして生成
            /*GameObject swarmGhost = */
            Instantiate(m_swarmGhostPrefabs[randomPrefabIndex], spawnPosition.position, spawnPosition.rotation, transform);

            //お化けを出現させる
            //Instantiate(m_swarmGhostPrefab, spawnPosition.position, spawnPosition.rotation);

            // 次のお化けが出現するまで待機
            if (availablePositions.Count > 0)
            {
                yield return new WaitForSeconds(m_spawnInterval);
            }
        }
    }
}