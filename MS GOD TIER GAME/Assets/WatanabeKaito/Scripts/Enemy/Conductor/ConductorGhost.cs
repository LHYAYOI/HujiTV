using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class ConductorGhost : MonoBehaviour
{
    [Header("群れお化けのマネージャー")]
    [SerializeField] private SwarmGhostManager m_swarmGhostManager;

    [Header("右のタンス")]
    [SerializeField] private ChestShaker n_rightChestShaker;

    [Header("左のタンス")]
    [SerializeField] private ChestShaker n_leftChestShaker;

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

    private bool m_isUpperVisibleFlag = false;   // 上にに姿を現わしているかどうか

    private bool m_isLowerVisibleFlag = false;    // 下に姿を現わしているかどうか

    private bool m_isBattleStartedFlag = false; // 戦闘が始まったかどうか

    private bool m_upperDrawerLockedFlag = false; // 上の引き出しが閉まったかどうか

    private bool m_lowerDrawerLockedFlag = false; // 下の引き出しが閉まったかどうか

    private bool m_isExpandingFlag = false; // 拡大中かどうか

    private bool m_isShrinkingFlag = false; // 縮小中かどうか

    private void Start()
    {
        TurnToPlayerImmediately();

        // アニメーションを止める
        m_animator.SetFloat("fSpeed", 0f);

        // 上の引き出しの位置に移動
        transform.position = m_upperHidePosition.position;
        //transform.position = m_lowerHidePosition.position;

        // 引き出しの子オブジェクトにする
        transform.SetParent(m_upperChestDrawer.transform);
        //transform.SetParent(m_lowerChestDrawer.transform);

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
        TurnToPlayer();

        if (m_isBattleStartedFlag)
        {
            UpdateCallTimer();
        }
    }

    private void UpdateCallTimer()
    {
        // すでに群れお化けを呼び出している場合は何もしない
        if (m_callMiniGhostFlag)
        {
            return;
        }

        // 両方の引き出しがロックされている場合または群れお化けの体力が最大の場合はタイマーをリセット
        if ((m_upperDrawerLockedFlag && m_lowerDrawerLockedFlag)
            || m_swarmGhostManager.GetCurrentHealth() >= m_swarmGhostManager.GetMaxHealth())
        {
            m_callMiniGhostTimer = 0f;
            return;
        }

        m_callMiniGhostTimer += Time.deltaTime;

        if (m_callMiniGhostTimer < m_callMiniGhostInterval)
        {
            return;
        }

        bool openUpper;

        // 上がロックされている場合は下を開ける
        if (m_upperDrawerLockedFlag)
        {
            openUpper = false;
        }
        // 下がロックされている場合は上を開ける
        else if (m_lowerDrawerLockedFlag)
        {
            openUpper = true;
        }
        // 両方開けられる場合はランダム
        else
        {
            openUpper = Random.value < 0.5f;
        }

        // 呼び出し中のフラグを先に立てる
        m_callMiniGhostFlag = true;
        m_callMiniGhostTimer = 0f;

        if (openUpper)
        {
            StartCoroutine(OpenAndCloseUpperChest());
        }
        else
        {
            StartCoroutine(OpenAndCloseLowerChest());
        }
    }

    public void StartBattle()
    {
        StartCoroutine(StartBattleCoroutine());
    }

    public IEnumerator StartBattleCoroutine()
    {
        m_isUpperVisibleFlag = true;

        StartCoroutine(ExpandCoroutine());

        // アニメーションを再生
        m_animator.SetFloat("fSpeed", 1f);
        m_animator.Play("swing", 0, 0f);

        yield return new WaitForSeconds(m_hideDelay * 0.4f);

        // 群れお化けを出現させる
        StartCoroutine(m_swarmGhostManager.SpawnAllSwarmGhostCoroutine());

        yield return new WaitForSeconds(m_hideDelay * 0.4f);

        StartCoroutine(ShrinkCoroutine());

        yield return new WaitForSeconds(m_hideDelay * 0.2f);

        // 上の引き出しを閉める
        //StartCoroutine(CloseUpperChest());

        // 下の引き出しを閉める
        //yield return StartCoroutine(CloseLowerChest());

        m_isUpperVisibleFlag = false;

        m_isBattleStartedFlag = true;
    }

    // 上の引き出しを開けて、閉める
    public IEnumerator OpenAndCloseUpperChest()
    {
        m_isUpperVisibleFlag = true;

        // 上の引き出しに移動して開ける
        yield return StartCoroutine(MoveToUpperChestAndOpen());

        yield return new WaitForSeconds(m_hideDelay * 0.4f);

        if (!m_upperDrawerLockedFlag)
        {
            // 群れお化けを出現させる
            StartCoroutine(m_swarmGhostManager.SpawnAllSwarmGhostCoroutine());
        }

        yield return new WaitForSeconds(m_hideDelay * 0.4f);

        StartCoroutine(ShrinkCoroutine());

        yield return new WaitForSeconds(m_hideDelay * 0.2f);

        // 上の引き出しを閉める
        yield return StartCoroutine(CloseUpperChest());
    }

    // 下の引き出しを開けて、閉める
    public IEnumerator OpenAndCloseLowerChest()
    {
        m_isLowerVisibleFlag = true;

        // 下の引き出しに移動して開ける
        yield return StartCoroutine(MoveToLowerChestAndOpen());

      
        yield return new WaitForSeconds(m_hideDelay * 0.4f);

        if(!m_lowerDrawerLockedFlag)
        {
            // 群れお化けを出現させる
            StartCoroutine(m_swarmGhostManager.SpawnAllSwarmGhostCoroutine());
        }

        yield return new WaitForSeconds(m_hideDelay * 0.4f);

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
        //m_upperChestDrawer.OpenDrawer();

        //yield return new WaitUntil(() => m_upperChestDrawer.IsMoveCoroutineFinished());

       

        StartCoroutine(ExpandCoroutine());

        // アニメーションを再生
        m_animator.SetFloat("fSpeed", 1f);
        m_animator.Play("swing", 0, 0f);

        yield return null;
    }

    // 上の引き出しを閉める
    private IEnumerator CloseUpperChest()
    {
        // 上の引き出しを閉める
        //m_upperChestDrawer.CloseDrawer();

        //StartCoroutine(ShrinkCoroutine());

        //yield return new WaitUntil(() => m_upperChestDrawer.IsMoveCoroutineFinished());

        // アニメーションを止める
        m_animator.SetFloat("fSpeed", 0f);

        m_callMiniGhostFlag = false;
        m_callMiniGhostTimer = 0.0f;

        m_isUpperVisibleFlag = false;

        // タンスを揺らす
        //m_chestShaker.StartUpperChestAnimation();

        yield return null;
    }

    // 下の引き出しに移動して開ける
    private IEnumerator MoveToLowerChestAndOpen()
    {
        // 下の引き出しの位置に移動
        transform.position = m_lowerHidePosition.position;

        // 引き出しの子オブジェクトにする
        transform.SetParent(m_lowerChestDrawer.transform);

        // 下の引き出しを開ける
        //m_lowerChestDrawer.OpenDrawer();

        //yield return new WaitUntil(() => m_lowerChestDrawer.IsMoveCoroutineFinished());

        StartCoroutine(ExpandCoroutine());

        // アニメーションを再生
        m_animator.SetFloat("fSpeed", 1f);
        m_animator.Play("swing", 0, 0f);

        yield return null;
    }

    // 下の引き出しを閉める
    private IEnumerator CloseLowerChest()
    {
        // 下の引き出しを閉める
        //m_lowerChestDrawer.CloseDrawer();

        //yield return new WaitUntil(() => m_lowerChestDrawer.IsMoveCoroutineFinished());
        // アニメーションを止める
        m_animator.SetFloat("fSpeed", 0f);

        // 元の回転に戻す
        transform.eulerAngles = m_originalRotation;

        m_callMiniGhostFlag = false;
        m_callMiniGhostTimer = 0.0f;

        m_isLowerVisibleFlag = false;

        // タンスを揺らす
        //m_chestShaker.StartLowerChestAnimation();

        yield return null;
    }

    // 下の引き出しを閉めて、ロックする
    public IEnumerator LockLowerChest()
    {
        if (m_lowerDrawerLockedFlag)
        {
            yield break;
        }

        // 下の引き出しを閉める
        m_lowerChestDrawer.CloseDrawer();
        yield return new WaitUntil(() => m_lowerChestDrawer.IsMoveCoroutineFinished());
       
        // タンスを揺らす
        n_rightChestShaker.StartLowerChestAnimation();

        m_lowerDrawerLockedFlag = true;
    }

    // 上の引き出しを閉めて、ロックする
    public IEnumerator LockUpperChest()
    {
        if (m_upperDrawerLockedFlag)
        {
            yield break;
        }

        // 上の引き出しを閉める
        m_upperChestDrawer.CloseDrawer();
        yield return new WaitUntil(() => m_upperChestDrawer.IsMoveCoroutineFinished());
  
        // タンスを揺らす
        n_leftChestShaker.StartUpperChestAnimation();

        m_upperDrawerLockedFlag = true;
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
        if(m_isExpandingFlag)
        {
            Debug.Log("拡大要求を無視");
            yield break;
        }

        m_isExpandingFlag = true;
        Debug.Log($"拡大開始 ID:{GetInstanceID()}");
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

        m_isExpandingFlag = false;
    }


    // 縮小のコルーチン
    private IEnumerator ShrinkCoroutine()
    {
        if (m_isShrinkingFlag)
        {
            yield break;
        }

        m_isShrinkingFlag = true;
        Debug.Log(
    $"縮小開始 ID:{GetInstanceID()} " +
    $"Frame:{Time.frameCount}"
);
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

        m_isShrinkingFlag = false;
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

    public bool GetIsUpperVisibleFlag()
    {
        return m_isUpperVisibleFlag;
    }

    public bool GetIsLowerVisibleFlag()
    {
        return m_isLowerVisibleFlag;
    }
}
