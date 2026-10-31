using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class ConductorGhost : MonoBehaviour
{
    [Header("群れお化けのマネージャー")]
    [SerializeField] private SwarmGhostManager m_swarmGhostManager;

    [Header("タンス")]
    [SerializeField] private ChestShaker m_chestShaker;

    [Header("アニメーター")]
    [SerializeField] private Animator m_animator;

    [Header("カメラを向く高さの補正")]
    [SerializeField] private float m_cameraHeightOffset = 1.5f;

    [Header("毎フレームプレイヤーに向く角度")]
    [SerializeField] private float m_turnSpeed = 60f;

    [Header("上の引き出し")]
    [SerializeField] private ChestDrawer m_upperChestDrawer;

    [Header("上の引き出しの隠れる位置")]
    [SerializeField] private Transform m_upperHidePosition;

    [Header("下の引き出し")]
    [SerializeField] private ChestDrawer m_lowerChestDrawer;

    [Header("下の引き出しの隠れる位置")]
    [SerializeField] private Transform m_lowerHidePosition;

    [Header("出てきたから隠れるまでのディレイ")]
    [SerializeField] private float m_hideDelay = 1.0f;

    [Header("拡大にかかる時間（秒）")]
    [SerializeField] private float m_expandDuration = 1.0f;

    [Header("縮小にかかる時間（秒）")]
    [SerializeField] private float m_shrinkDuration = 0.5f;

    [Header("群れお化けを呼び出す間隔")]
    [SerializeField] private float m_callMiniGhostInterval = 10.0f;

    private bool m_callMiniGhostFlag = false; // 群れお化けを呼び出すフラグ

    private float m_callMiniGhostTimer = 0.0f; // 群れお化けを呼び出すタイマー

    private Vector3 m_originalScale; // 元のスケール

    private Vector3 m_originalRotation; // 元の回転

    private bool m_isVisibleFlag = false;   // 姿を現わしているかどうか

    private bool m_isBattleStartedFlag = false; // 戦闘が始まったかどうか

    private bool m_upperDrawerLockedFlag = false; // 上の引き出しが閉まったかどうか

    private bool m_lowerDrawerLockedFlag = false; // 下の引き出しが閉まったかどうか

    private void Start()
    {
        TurnToPlayerImmediately();

        // アニメーションを止める
        m_animator.SetFloat("fSpeed", 0f);

        // 上の引き出しの位置に移動
        transform.position = m_upperHidePosition.position;

        // 少し後ろに下がる
        transform.position += transform.forward * 0.5f;

        // 引き出しの子オブジェクトにする
        transform.SetParent(m_upperChestDrawer.transform);

        // 元のスケールを保存
        m_originalScale = transform.localScale;

        // 最初は小さくしておく
        transform.localScale = Vector3.zero;

        // 引き出しを開けた状態にしておく
        m_upperChestDrawer.transform.localPosition = m_upperChestDrawer.GetTargetPosition();
        m_lowerChestDrawer.transform.localPosition = m_lowerChestDrawer.GetTargetPosition();
    }

    private void Update()
    {
        if (m_isVisibleFlag)
        {
            // プレイヤーの方向を向く
            TurnToPlayer();
        }
        else
        {
            // 正面方向を向く
            TurnToFront();
        }

        if(m_isBattleStartedFlag)
        {
            UpdateCallTimer();
        }
    }

    private void UpdateCallTimer()
    {
        m_callMiniGhostTimer += Time.deltaTime;

        if (m_callMiniGhostTimer >= m_callMiniGhostInterval)
        {
            // 呼び出しフラグが立っているか、両方の引き出しが閉まっている場合は何もしない
            if (m_callMiniGhostFlag || m_upperDrawerLockedFlag && m_lowerDrawerLockedFlag)
            {
                return;
            }

            // ランダムに引き出しを選んで開ける
            if (Random.value < 0.5f)
            {
                if (m_upperDrawerLockedFlag)
                {
                    StartCoroutine(OpenAndCloseLowerChest());
                }
                else
                {
                    StartCoroutine(OpenAndCloseUpperChest());
                }
            }
            else
            {
                if (m_lowerDrawerLockedFlag)
                {
                    StartCoroutine(OpenAndCloseUpperChest());
                }
                else
                {
                    StartCoroutine(OpenAndCloseLowerChest());
                }
            }

            m_callMiniGhostFlag = true;
        }
    }

    public void StartBattle()
    {
        StartCoroutine(StartBattleCoroutine());
    }

    public IEnumerator StartBattleCoroutine()
    {
        m_isVisibleFlag = true;

        StartCoroutine(ExpandCoroutine());

        // アニメーションを再生
        m_animator.SetFloat("fSpeed", 1f);
        m_animator.Play("swing", 0, 0f);

        yield return new WaitForSeconds(m_hideDelay * 0.8f);

        // 群れお化けを出現させる
        StartCoroutine(m_swarmGhostManager.SpawnAllSwarmGhostCoroutine());

        StartCoroutine(ShrinkCoroutine());

        yield return new WaitForSeconds(m_hideDelay * 0.2f);

        // 上の引き出しを閉める
        StartCoroutine(CloseUpperChest());

        // 下の引き出しを閉める
        yield return StartCoroutine(CloseLowerChest());

        m_isVisibleFlag = false;

        m_isBattleStartedFlag = true;
    }

    // 上の引き出しを開けて、閉める
    public IEnumerator OpenAndCloseUpperChest()
    {
        // 上の引き出しに移動して開ける
        yield return StartCoroutine(MoveToUpperChestAndOpen());

        yield return new WaitForSeconds(m_hideDelay * 0.8f);

        if (!m_upperDrawerLockedFlag && m_lowerDrawerLockedFlag)
        {
            // 群れお化けを出現させる
            StartCoroutine(m_swarmGhostManager.SpawnAllSwarmGhostCoroutine());
        }

        StartCoroutine(ShrinkCoroutine());

        yield return new WaitForSeconds(m_hideDelay * 0.2f);

        // 上の引き出しを閉める
        yield return StartCoroutine(CloseUpperChest());
    }

    // 下の引き出しを開けて、閉める
    public IEnumerator OpenAndCloseLowerChest()
    {
        // 下の引き出しに移動して開ける
        yield return StartCoroutine(MoveToLowerChestAndOpen());

      
        yield return new WaitForSeconds(m_hideDelay * 0.8f);

        if(!m_upperDrawerLockedFlag && m_lowerDrawerLockedFlag)
        {
            // 群れお化けを出現させる
            StartCoroutine(m_swarmGhostManager.SpawnAllSwarmGhostCoroutine());
        }

        StartCoroutine(ShrinkCoroutine());

        yield return new WaitForSeconds(m_hideDelay * 0.2f);

        // 下の引き出しを閉める
        yield return StartCoroutine(CloseLowerChest());
    }

    // 上の引き出しに移動して開ける
    private IEnumerator MoveToUpperChestAndOpen()
    {
        // 上の引き出しの位置に移動
        transform.position = m_upperHidePosition.position;

        // 引き出しの子オブジェクトにする
        transform.SetParent(m_upperChestDrawer.transform);

        // 上の引き出しを開ける
        m_upperChestDrawer.OpenDrawer();

        yield return new WaitUntil(() => m_upperChestDrawer.IsMoveCoroutineFinished());

        m_isVisibleFlag = true;

        StartCoroutine(ExpandCoroutine());

        // アニメーションを再生
        m_animator.SetFloat("fSpeed", 1f);
        m_animator.Play("swing", 0, 0f);
    }

    // 上の引き出しを閉める
    private IEnumerator CloseUpperChest()
    {
        // 上の引き出しを閉める
        m_upperChestDrawer.CloseDrawer();

        //StartCoroutine(ShrinkCoroutine());

        yield return new WaitUntil(() => m_upperChestDrawer.IsMoveCoroutineFinished());

        // アニメーションを止める
        m_animator.SetFloat("fSpeed", 0f);

        m_callMiniGhostFlag = false;
        m_callMiniGhostTimer = 0.0f;

        m_isVisibleFlag = false;

        // タンスを揺らす
        m_chestShaker.StartUpperChestAnimation();
    }

    // 下の引き出しに移動して開ける
    private IEnumerator MoveToLowerChestAndOpen()
    {
        // 下の引き出しの位置に移動
        transform.position = m_lowerHidePosition.position;

        // 引き出しの子オブジェクトにする
        transform.SetParent(m_lowerChestDrawer.transform);

        // 下の引き出しを開ける
        m_lowerChestDrawer.OpenDrawer();

        yield return new WaitUntil(() => m_lowerChestDrawer.IsMoveCoroutineFinished());

        m_isVisibleFlag = true;

        StartCoroutine(ExpandCoroutine());

        // アニメーションを再生
        m_animator.SetFloat("fSpeed", 1f);
        m_animator.Play("swing", 0, 0f);
    }

    // 下の引き出しを閉める
    private IEnumerator CloseLowerChest()
    {
        // 下の引き出しを閉める
        m_lowerChestDrawer.CloseDrawer();

        yield return new WaitUntil(() => m_lowerChestDrawer.IsMoveCoroutineFinished());
        // アニメーションを止める
        m_animator.SetFloat("fSpeed", 0f);

        // 元の回転に戻す
        transform.eulerAngles = m_originalRotation;

        m_callMiniGhostFlag = false;
        m_callMiniGhostTimer = 0.0f;

        m_isVisibleFlag = false;

        // タンスを揺らす
        m_chestShaker.StartLowerChestAnimation();
    }

   

    // プレイヤーの方向へ即座に向く
    private void TurnToPlayerImmediately()
    {
        Vector3 playerPosition = Camera.main.transform.position;

        // プレイヤーの狙う位置を調整
        playerPosition.y -= m_cameraHeightOffset;

        Vector3 direction = playerPosition - transform.position;

        // 方向ベクトルがほぼゼロなら回転しない
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        // プレイヤーの方向へ即座に回転
        transform.rotation = Quaternion.LookRotation(-direction.normalized, Vector3.up);
    }

    // プレイヤーの方向を向く
    private void TurnToPlayer()
    {
        Vector3 playerPosition = Camera.main.transform.position;

        // プレイヤーの狙う位置を調整
        playerPosition.y -= m_cameraHeightOffset;

        Vector3 direction = playerPosition - transform.position;

        // 方向ベクトルがほぼゼロなら回転しない
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        // プレイヤーの方向を向く回転
        Quaternion targetRotation = Quaternion.LookRotation(-direction.normalized, Vector3.up);

        // 毎フレーム少しずつ回転する
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            m_turnSpeed * Time.deltaTime
        );
    }

    // 正面方向を向く
    private void TurnToFront()
    {
        // 正面方向を向く回転
        Quaternion targetRotation = Quaternion.Euler(m_originalRotation);
        // 毎フレーム少しずつ回転する
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            m_turnSpeed * Time.deltaTime * 2
        );
    }

    // 拡大のコルーチン
    private IEnumerator ExpandCoroutine()
    {
        float elapsed = 0f;
        if (m_expandDuration <= 0f)
        {
            transform.localScale = m_originalScale;
            yield break;
        }
        while (elapsed < m_expandDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / m_expandDuration);
            float easedT = Easing.EaseOutSine(t);
            transform.localScale = Vector3.Lerp(Vector3.zero, m_originalScale, easedT);
            yield return null;
        }
        transform.localScale = m_originalScale;
    }


    // 縮小のコルーチン
    private IEnumerator ShrinkCoroutine()
    {
        float elapsed = 0f;

        if (m_shrinkDuration <= 0f)
        {
            transform.localScale = Vector3.zero;
            yield break;
        }

        while (elapsed < m_shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / m_shrinkDuration);
            float easedT = Easing.EaseInSine(t);
            transform.localScale = Vector3.Lerp(m_originalScale, Vector3.zero, easedT);
            yield return null;
        }

        transform.localScale = Vector3.zero;
    }

    // 群れお化けを全て削除する
    public void RemoveAllSwarmGhosts()
    {
        m_swarmGhostManager.RemoveAllSwarmGhosts();
    }

    public void SetUpperDrawerLockedFlag(bool flag)
    {
        m_upperDrawerLockedFlag = flag;
    }

    public void SetLowerDrawerLockedFlag(bool flag)
    {
        m_lowerDrawerLockedFlag = flag;
    }

    public bool GetUpperDrawerLockedFlag()
    {
        return m_upperDrawerLockedFlag;
    }

    public bool GetLowerDrawerLockedFlag()
    {
        return m_lowerDrawerLockedFlag;
    }

    public bool GetIsVisibleFlag()
    {
        return m_isVisibleFlag;
    }
}
