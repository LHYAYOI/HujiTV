using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SwarmGhostManager : MonoBehaviour
{
    [Header("群れお化けの一個体リスト")]
    [SerializeField] private List<SwarmGhost> m_swarmGhostPrefabs;

    [Header("出現位置リスト")]
    [SerializeField] private List<Transform> m_spawnPositions;

    [Header("出現間隔（秒）")]
    [SerializeField] private float m_spawnInterval = 0.1f;

    [Header("群れお化けのターゲット")]
    [SerializeField] private Transform m_target;


    private List<SwarmGhost> m_activeSwarmGhostList = new List<SwarmGhost>(); // 現在出現している群れお化け

    private List<Transform> m_activeSpawnPositions = new List<Transform>(); // 現在出現している群れお化けの位置

    private List<Transform> m_availableSpawnPositions = new List<Transform>();  // まだ出現に使用していない位置

    private bool m_isSpawningFlag = false;  // 出現処理が重複して実行されるのを防ぐ

    private void Start()
    {
        StartCoroutine(SpawnAllSwarmGhostCoroutine());
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            RemoveRandomHalfSwarmGhosts();
        }

        if (Keyboard.current != null &&
            Keyboard.current.enterKey.wasPressedThisFrame)
        {
            StartCoroutine(SpawnAllSwarmGhostCoroutine());
        }
    }

    // 群れお化けを時間差で出現させる
    private IEnumerator SpawnAllSwarmGhostCoroutine()
    {
        if (m_isSpawningFlag)
        {
            yield break;
        }

        m_isSpawningFlag = true;

        // プレハブや出現位置が設定されていない場合は終了
        if (m_swarmGhostPrefabs == null ||
            m_swarmGhostPrefabs.Count == 0 ||
            m_spawnPositions == null ||
            m_spawnPositions.Count == 0)
        {
            m_isSpawningFlag = false;
            yield break;
        }

        // nullではないプレハブだけを候補にする
        List<SwarmGhost> availablePrefabs = new List<SwarmGhost>();

        for (int i = 0; i < m_swarmGhostPrefabs.Count; i++)
        {
            if (m_swarmGhostPrefabs[i] != null)
            {
                availablePrefabs.Add(m_swarmGhostPrefabs[i]);
            }
        }

        if (availablePrefabs.Count == 0)
        {
            m_isSpawningFlag = false;
            yield break;
        }

        // 出現候補を初期化
        m_availableSpawnPositions.Clear();

        for (int i = 0; i < m_spawnPositions.Count; i++)
        {
            Transform spawnPosition = m_spawnPositions[i];

            if (spawnPosition == null)
            {
                continue;
            }

            // すでにお化けがいる位置は候補にしない
            if (m_activeSpawnPositions.Contains(spawnPosition))
            {
                continue;
            }

            // 同じ位置が重複して登録されるのを防ぐ
            if (!m_availableSpawnPositions.Contains(spawnPosition))
            {
                m_availableSpawnPositions.Add(spawnPosition);
            }
        }

        // 出現候補がなくなるまで繰り返す
        while (m_availableSpawnPositions.Count > 0)
        {
            // ランダムにお化けのプレハブを選択
            int randomPrefabIndex = Random.Range(0, availablePrefabs.Count);

            // ランダムに出現位置を選択
            int randomPositionIndex = Random.Range(0, m_availableSpawnPositions.Count);

            Transform spawnPosition = m_availableSpawnPositions[randomPositionIndex];

            // 使用した位置を候補リストから削除
            m_availableSpawnPositions.RemoveAt(randomPositionIndex);

            if (spawnPosition == null)
            {
                continue;
            }

            // お化けを子オブジェクトとして生成
            SwarmGhost swarmGhost = Instantiate(
                availablePrefabs[randomPrefabIndex],
                spawnPosition.position,
                spawnPosition.rotation,
                transform
            );

            swarmGhost.SetTarget(m_target);

            // お化けと出現位置を同じインデックスで登録
            m_activeSwarmGhostList.Add(swarmGhost);
            m_activeSpawnPositions.Add(spawnPosition);

            // 次のお化けが出現するまで待機
            if (m_availableSpawnPositions.Count > 0)
            {
                yield return new WaitForSeconds(m_spawnInterval);
            }
        }

        m_isSpawningFlag = false;
    }

    // ランダムに半分の群れお化けを削除する
    public void RemoveRandomHalfSwarmGhosts()
    {
        int countToRemove = m_activeSwarmGhostList.Count / 2;

        for (int i = 0; i < countToRemove; i++)
        {
            if (m_activeSwarmGhostList.Count == 0)
            {
                break;
            }

            // ランダムに削除するお化けを選択
            int randomIndex = Random.Range(
                0,
                m_activeSwarmGhostList.Count
            );

            // お化けと対応する位置を削除
            RemoveSwarmGhostAt(randomIndex);
        }
    }

    // 指定したインデックスのお化けと出現位置を削除する
    private void RemoveSwarmGhostAt(int index)
    {
        SwarmGhost swarmGhost = m_activeSwarmGhostList[index];

        Transform spawnPosition = m_activeSpawnPositions[index];

        // お化けを削除
        if (swarmGhost != null)
        {
            swarmGhost.Die();
        }

        // お化けがいた位置を候補リストから削除
        if (spawnPosition != null)
        {
            m_availableSpawnPositions.Remove(spawnPosition);
        }

        // お化けと位置を同じインデックスで削除
        m_activeSwarmGhostList.RemoveAt(index);
        m_activeSpawnPositions.RemoveAt(index);
    }

    // すべての群れお化けを削除する
    public void RemoveAllSwarmGhosts()
    {
        for (int i = 0; i < m_activeSwarmGhostList.Count; i++)
        {
            SwarmGhost swarmGhost = m_activeSwarmGhostList[i];

            if (swarmGhost != null)
            {
                swarmGhost.Die();
            }
        }

        // お化けと出現位置の管理リストをクリア
        m_activeSwarmGhostList.Clear();
        m_activeSpawnPositions.Clear();
        m_availableSpawnPositions.Clear();
    }
}