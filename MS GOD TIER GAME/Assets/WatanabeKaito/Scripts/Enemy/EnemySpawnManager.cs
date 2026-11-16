//-----------------------------------------------
// EnemySpawnManager.cs
// 制作日：2026/09/30
// 制作者：渡辺開斗
// 概要：エネミーのスポーン管理クラス
//-----------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class EnemySpawnManager : MonoBehaviour
{
    [System.Serializable]
    public class EnemySpawnData // エネミーのスポーンデータ構造
    {
        public EnemyBase m_enemyPrefab; // エネミーのプレハブ
        public SplineContainer m_spline; // スプラインコンテナ
        public Transform m_spawnPosition; // 出現位置のTransform
    }

    [Header("アクティブなエネミー管理マネージャー")]
    [SerializeField] private ActiveEnemyManager m_activeEnemyManager; // アクティブなエネミーを管理するマネージャー

    [Header("プレイヤーの位置")]
    [SerializeField] private Transform m_playerTransform; // プレイヤーの位置を取得するためのTransform

    [Header("生成データのリスト")]
    [SerializeField] private List<EnemySpawnData> m_spawnDataList = new List<EnemySpawnData>();


    // エネミーIDとスプラインIDを指定してスプラインエネミーを生成
    public void SpawnEnemy(int index)
    {
        // インデックスが範囲内か確認
        if (index < 0 || index >= m_spawnDataList.Count)
        {
            Debug.LogWarning("Invalid spawn index: " + index);
            return;
        }

        // 生成と初期化
        Vector3 spawnPosition = m_spawnDataList[index].m_spawnPosition.position;
        EnemyBase enemyObj = Instantiate(m_spawnDataList[index].m_enemyPrefab, spawnPosition, m_spawnDataList[index].m_enemyPrefab.transform.rotation);
        enemyObj.SetPlayerPosition(m_playerTransform);
        enemyObj.SetActiveEnemyManager(m_activeEnemyManager);

        // アクティブなエネミーリストに追加
        m_activeEnemyManager.AddActiveEnemy(enemyObj);

        if (enemyObj is SplineMoveEnemy splineMoveEnemy)
        {
            splineMoveEnemy.SetSpawnPosition(spawnPosition);
            splineMoveEnemy.SetupSpline(m_spawnDataList[index].m_spline);
            splineMoveEnemy.Initialize();
        }
        else
        {
            enemyObj.Initialize();
        }
    }

}