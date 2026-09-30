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
    public struct SplinePathData    // スプラインパスのデータ構造
    {
        public string m_id; // スプラインの識別子
        public SplineContainer m_container; // スプラインコンテナ
    }

    [Header("エネミーのプレハブ")]
    [SerializeField] private List<EnemyBase> m_enemyPrefabs = new List<EnemyBase>();

    [Header("スプラインパスのデータ")]
    [SerializeField] private List<SplinePathData> m_splinePaths = new List<SplinePathData>();

    
    private Dictionary<string, EnemyBase> m_enemyDictionary = new Dictionary<string, EnemyBase>();  // エネミーIDをキーにしてエネミープレハブを格納する辞書
    private Dictionary<string, SplineContainer> m_splineDictionary = new Dictionary<string, SplineContainer>(); // スプラインIDをキーにしてスプラインコンテナを格納する辞書

    // 位置を指定するように位置データのリストを作ったほうがいいかも

    private void Awake()
    {
       InitializeEnemyDictionary();
       InitializeSplineDictionary();
    }

    // エネミーのプレハブを辞書に登録するメソッド
    private void InitializeEnemyDictionary()
    {
        foreach (var prefab in m_enemyPrefabs)
        {
            if (prefab != null && !string.IsNullOrEmpty(prefab.GetEnemyData().m_id) && !m_enemyDictionary.ContainsKey(prefab.GetEnemyData().m_id))
            {
                m_enemyDictionary.Add(prefab.GetEnemyData().m_id, prefab);
            }
        }
    }

    // スプラインパスを辞書に登録するメソッド
    private void InitializeSplineDictionary()
    {
        foreach (var path in m_splinePaths)
        {
            if (path.m_container != null && !string.IsNullOrEmpty(path.m_id) && !m_splineDictionary.ContainsKey(path.m_id))
            {
                m_splineDictionary.Add(path.m_id, path.m_container);
            }
        }
    }

    // エネミーIDとスプラインID（辞書のキー）を指定してスプラインエネミーを生成
    public void SpawnEnemy(string enemyId, string splineId)
    {
        // 辞書からエネミーを取得（TryGetValueを使うと安全かつ高速です）
        if (!m_enemyDictionary.TryGetValue(enemyId, out EnemyBase prefab))
        {
            Debug.LogError($"辞書にエネミーID '{enemyId}' が登録されていません。");
            return;
        }

        // 辞書からスプラインを取得
        if (!m_splineDictionary.TryGetValue(splineId, out SplineContainer spline))
        {
            Debug.LogError($"辞書にスプラインID '{splineId}' が登録されていません。");
            return;
        }

        // 生成と初期化
        Vector3 startPos = spline.EvaluatePosition(0f);
        EnemyBase enemyObj = Instantiate(prefab, startPos, Quaternion.identity);

        if (enemyObj is SplineMoveEnemy splineMoveEnemy)
        {
            splineMoveEnemy.SetupSpline(spline);
            splineMoveEnemy.Initialize();
        }
    }

    // エネミーIDと座標を指定して固定位置でエネミーを生成
    public void SpawnStaticEnemy(string enemyId, Vector3 spawnPosition)
    {
        if (!m_enemyDictionary.TryGetValue(enemyId, out EnemyBase prefab))
        {
            Debug.LogError($"辞書にエネミーID '{enemyId}' が登録されていません。");
            return;
        }

        EnemyBase enemyObj = Instantiate(prefab, spawnPosition, Quaternion.identity);
        enemyObj.Initialize();
    }
}