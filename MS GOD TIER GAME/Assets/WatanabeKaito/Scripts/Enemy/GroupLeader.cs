using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GroupLeader : MonoBehaviour
{
    [Header("群れお化けのプレハブリスト")]
    [SerializeField] private List<Transform> m_swarmGhostPrefabs;

    [Header("生成数")]
    [SerializeField] private int m_spawnCount = 10;

    [Header("出現間隔（秒）")]
    [SerializeField] private float m_spawnInterval = 0.1f;

    [Header("リーダーとの距離")]
    [SerializeField] private Vector2 m_distanceMinMax = new Vector2(2f, 5f);

    [Header("移動速度")]
    [SerializeField] private Vector2 m_speedMinMax = new Vector2(2f, 5f);

    [Header("角度の回転速度")]
    [SerializeField] private Vector2 m_rotateSpeedMinMax = new Vector2(10f, 60f);

    [Header("目的地の手前で停止する距離")]
    [SerializeField] private float m_stopDistance = 0.1f;

    private List<GameObject> m_activeSwarmGhostList = new List<GameObject>(); // 現在出現している群れお化け
    
    private bool m_isSpawningFlag = false; // 出現処理が重複して実行されるのを防ぐ

    private bool m_isFollowingLeader = true; // リーダーを追従するかどうか

    private bool m_isDead = false; // 死亡フラグ

    // 個体ごとの移動情報
    private class ChildData
    {
        public Transform transform;

        // リーダーとの距離
        public float distance;

        // リーダーからの方向（ローカル座標）
        public Vector3 direction;

        // 回転軸
        public Vector3 rotationAxis;

        // 回転速度
        public float rotateSpeed;

        // 移動速度
        public float moveSpeed;

        // Z軸の回転角度
        public float rotationZ;
    }

    private List<ChildData> m_childData = new List<ChildData>();

    private void Update()
    {
        if (m_isDead)
        {
            return;
        }

        // 生成したお化けを移動させる
        foreach (ChildData child in m_childData)
        {
            UpdateChild(child);
        }

        if (Keyboard.current != null &&
            Keyboard.current.sKey.wasPressedThisFrame)
        {
            RemoveAllOyoguSwarmGhosts();
        }
    }

    // 生成したお化けの移動情報を登録する
    private void AddChildData(Transform childTransform)
    {
        ChildData data = new ChildData();

        data.transform = childTransform;

        // リーダーからの距離
        data.distance = Random.Range(
            m_distanceMinMax.x,
            m_distanceMinMax.y
        );

        // 初期方向をランダムに設定
        data.direction = Random.onUnitSphere;

        // 回転軸をランダムに設定
        data.rotationAxis = Random.onUnitSphere;

        // 回転方向をランダムに設定
        float direction = 1f;

        if (Random.value < 0.5f)
        {
            direction = -1f;
        }

        // 回転速度
        data.rotateSpeed = Random.Range(
            m_rotateSpeedMinMax.x,
            m_rotateSpeedMinMax.y
        ) * direction;

        // 移動速度
        data.moveSpeed = Random.Range(
            m_speedMinMax.x,
            m_speedMinMax.y
        );

        // Z軸の回転角度をランダムに設定
        data.rotationZ = Random.Range(0f, 360f);

        // 生成時の回転にも反映
        childTransform.rotation = transform.rotation *
            Quaternion.Euler(0f, 0f, data.rotationZ);

        m_childData.Add(data);
    }

    // お化けを移動させる
    private void UpdateChild(ChildData child)
    {
        if (child.transform == null)
        {
            return;
        }

        // リーダーへの追従が解除されている場合
        if (!m_isFollowingLeader)
        {
            // 現在向いている方向へ直進
            child.transform.position +=
                child.transform.forward *
                child.moveSpeed *
                Time.deltaTime;

            return;
        }

        // 3D方向を回転させる
        child.direction = Quaternion.AngleAxis(
            child.rotateSpeed * Time.deltaTime,
            child.rotationAxis
        ) * child.direction;

        // リーダーの位置と回転を反映した目的地
        Vector3 targetPosition = transform.TransformPoint(child.direction * child.distance);

        // お化けから目的地への方向
        Vector3 moveDirection = targetPosition - child.transform.position;

        float distance = moveDirection.magnitude;

        // 目的地の手前まで移動
        if (distance > m_stopDistance)
        {
            child.transform.position = Vector3.MoveTowards(
                child.transform.position,
                targetPosition,
                child.moveSpeed * Time.deltaTime
            );
        }

        // 移動方向へ向く
        if (moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(moveDirection.normalized);

            // ランダムなZ軸回転を加える
            child.transform.rotation = lookRotation * Quaternion.Euler(0f, 0f, child.rotationZ);
        }
    }

    // 群れお化けを出現させる
    public void SpawnAllSwarmGhost()
    {
        StartCoroutine(SpawnAllSwarmGhostCoroutine());
    }

    // 群れお化けを時間差で出現させる
    public IEnumerator SpawnAllSwarmGhostCoroutine()
    {
        if (m_isSpawningFlag)
        {
            yield break;
        }

        m_isSpawningFlag = true;

        // プレハブが設定されていない場合は終了
        if (m_swarmGhostPrefabs == null ||
            m_swarmGhostPrefabs.Count == 0)
        {
            m_isSpawningFlag = false;
            yield break;
        }

        // 指定した数だけ生成
        for (int i = 0; i < m_spawnCount; i++)
        {
            // ランダムにお化けのプレハブを選択
            int randomPrefabIndex = Random.Range(0, m_swarmGhostPrefabs.Count);

            Transform prefab = m_swarmGhostPrefabs[randomPrefabIndex];

            if (prefab == null)
            {
                continue;
            }

            // お化けを生成
            GameObject swarmGhost = Instantiate(
                prefab.gameObject,
                transform.position,
                transform.rotation,
                transform.parent
            );

            // 出現したお化けをリストに登録
            m_activeSwarmGhostList.Add(swarmGhost);

            // 移動情報を登録
            AddChildData(swarmGhost.transform);

            // 次のお化けが出現するまで待機
            yield return new WaitForSeconds(m_spawnInterval);
        }

        m_isSpawningFlag = false;
    }

    // すべての群れお化けを削除する
    public void RemoveAllOyoguSwarmGhosts()
    {
        m_isDead = true;

        for (int i = 0; i < m_activeSwarmGhostList.Count; i++)
        {
            OyoguSwarmGhost swarmGhost = m_activeSwarmGhostList[i].GetComponent<OyoguSwarmGhost>();

            if (swarmGhost != null)
            {
                swarmGhost.DeadAnimation();
            }
        }

        // お化けと出現位置の管理リストをクリア
        m_activeSwarmGhostList.Clear();
    }

    // 群れお化けのリーダーへの追従を解除する
    public void ReleaseSwarmGhosts()
    {
        m_isFollowingLeader = false;
    }
}