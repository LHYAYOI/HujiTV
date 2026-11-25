using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace YasudaHaruto
{
    // Uses the Alpha scene's original spawn and battle hit markers.
    public sealed class PageRewardBattle : GimmickSpline
    {
        [SerializeField] private PageRewardBattle m_battle;
        [SerializeField] private BattleData m_battleData;
        [SerializeField] private EnemySpawnManager m_enemyManager;
        [SerializeField] private ActiveEnemyManager m_activeEnemyManager;
        [SerializeField] private RoutePageRewards m_rewards;
        [SerializeField] private PageData m_rewardPage;
        [SerializeField] private UnityEvent m_onFinishBattle;

        private readonly List<EnemyBase> m_enemies = new();
        private readonly List<Vector3> m_lastPositions = new();
        private bool m_prepared;
        private bool m_started;
        private bool m_allEnemiesSpawned;

        public void SpawnStart()
        {
            (m_battle != null ? m_battle : this).Prepare();
        }

        public void BattleStart()
        {
            if (m_started)
                return;

            Prepare();
            m_started = true;
            StartCoroutine(RunBattle());
        }

        private void Prepare()
        {
            if (m_prepared)
                return;

            m_prepared = true;
            var active = m_activeEnemyManager.GetActiveEnemies();
            foreach (int spawnId in m_battleData.SpawnIds)
            {
                int previousCount = active.Count;
                m_enemyManager.SpawnEnemy(spawnId);
                for (int i = previousCount; i < active.Count; i++)
                {
                    m_enemies.Add(active[i]);
                    m_lastPositions.Add(active[i].transform.position);
                }
            }
            m_allEnemiesSpawned = m_enemies.Count == m_battleData.SpawnIds.Count;
        }

        private IEnumerator RunBattle()
        {
            var active = m_activeEnemyManager.GetActiveEnemies();
            foreach (EnemyBase enemy in m_enemies)
                if (enemy != null && active.Contains(enemy))
                    enemy.BattleStart();

            float startTime = Time.time;
            Vector3 lastDefeatPosition = transform.position;
            while (m_enemies.Count > 0)
            {
                yield return null;
                for (int i = m_enemies.Count - 1; i >= 0; i--)
                {
                    var enemy = m_enemies[i];
                    if (enemy != null)
                        m_lastPositions[i] = enemy.transform.position;
                    if (active.Contains(enemy))
                        continue;

                    lastDefeatPosition = m_lastPositions[i];
                    m_enemies.RemoveAt(i);
                    m_lastPositions.RemoveAt(i);
                }

                if (m_enemies.Count == 0)
                {
                    if (m_allEnemiesSpawned)
                        m_rewards.Drop(m_rewardPage, lastDefeatPosition);
                    break;
                }

                if (m_battleData.HasTimeLimit && Time.time - startTime > m_battleData.BattleTime)
                {
                    foreach (EnemyBase enemy in m_enemies)
                    {
                        if (enemy != null)
                            enemy.BattleEnd();
                    }
                    break;
                }
            }

            m_onFinishBattle?.Invoke();
        }
    }
}
