using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ChestDrawer : MonoBehaviour
{
    [Header("引き出しの移動先のローカル位置")]
    [SerializeField] private Vector3 m_targetPosition;

    [Header("引き出しの移動時間")]
    [SerializeField] private float m_moveDuration = 1.0f;

    private Vector3 m_initialPosition;
    
    private Coroutine m_moveCoroutine; // 移動用コルーチン

    private void Awake()
    {
        m_initialPosition = transform.localPosition;
    }

    private void Update()
    {
    }

    // 引き出しを開ける
    public void OpenDrawer()
    {
        StartDrawerMove(m_targetPosition, true);
    }

    // 引き出しを閉める
    public void CloseDrawer()
    {
        StartDrawerMove(m_initialPosition, false);
    }

    // 引き出しの移動を開始する
    private void StartDrawerMove(Vector3 targetPosition, bool isOpening)
    {
        // 移動中なら現在のコルーチンを停止する
        if (m_moveCoroutine != null)
        {
            StopCoroutine(m_moveCoroutine);
            m_moveCoroutine = null;
        }

        // 移動時間が0以下なら即座に移動する
        if (m_moveDuration <= 0f)
        {
            transform.localPosition = targetPosition;
            return;
        }

        m_moveCoroutine = StartCoroutine(MoveDrawerCoroutine(targetPosition, isOpening));
    }

    // イージングを使って引き出しを移動させる
    private IEnumerator MoveDrawerCoroutine(Vector3 targetPosition, bool isOpening)
    {
        // 移動開始時点の位置
        Vector3 startPosition = transform.localPosition;

        float elapsedTime = 0f;

        while (elapsedTime < m_moveDuration)
        {
            elapsedTime += Time.deltaTime;

            float t = Mathf.Clamp01(elapsedTime / m_moveDuration);

            float easeT = t;

            if (isOpening)
            {
                easeT = Easing.EaseOutSine(t);
            }
            else
            {
                easeT = Easing.EaseInOutSine(t);
            }

            transform.localPosition = Vector3.Lerp(
                startPosition,
                targetPosition,
                easeT);

            yield return null;
        }

        // 最後に目標位置へ正確に合わせる
        transform.localPosition = targetPosition;

        m_moveCoroutine = null;
    }

    // コルーチンが終わったかどうかを取得する
    public bool IsMoveCoroutineFinished()
    {
        return m_moveCoroutine == null;
    }

    // 引き出しの移動先のローカル位置を取得する
    public Vector3 GetTargetPosition()
    {
        return m_targetPosition;
    }
}