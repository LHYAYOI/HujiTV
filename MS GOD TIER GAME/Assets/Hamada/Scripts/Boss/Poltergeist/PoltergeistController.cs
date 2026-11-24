using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class PoltergeistController : EnemyBase
{
    [SerializeField] StateMachine m_stateMachine;

    [SerializeField] List<TargetableObject> m_selectStateColliders;
    [SerializeField] List<TargetableObject> m_stunStateColliders;
    [SerializeField] List<BodyPartMover> m_upperBody;
    [SerializeField] List<BodyPartMover> m_legs;

    List<BodyPartMover> m_wholeBody;

    GameObject m_shotObject;

    int m_attackedCount = 0;

    // スポナーからの呼び出し用初期化関数
    public override void Initialize() 
    {
    }

    // バトル開始時の処理を行う関数
    public override void BattleStart() 
    {
    }

    // バトル終了時の処理を行う関数
    public override void BattleEnd() 
    {
    }

    private void Start()
    {
        m_wholeBody = m_upperBody.Concat(m_legs).ToList();
        SelectStateCollidersActivateFlag(false);
        StunStateCollidersActivateFlag(false);
        SetBodyPartsPosition(new Vector3(0, -5, 0));
        ChangeState("Encounter");
    }

    public void ChangeState(string stateName) 
    {
        if (m_stateMachine == null) 
        {
            return;
        }

        m_stateMachine.ChangeState(stateName);
    }

    public void SetShotObject(GameObject shotObject) 
    {
        m_shotObject = shotObject;
    }

    public GameObject GetShotObject() 
    {
        return m_shotObject;
    }

    public void AddAttackedCount() 
    {
        m_attackedCount++;
    }

    public int GetAttackedCount() 
    {
        return m_attackedCount;
    }

    //額縁が全て攻撃されたか
    public void CheckIfIsAllPictureFrameAttacked() 
    {
        foreach (TargetableObject collider in m_selectStateColliders) 
        {
            if (collider == null) 
            {
                continue;
            }

            if(collider.IsTargetable())
            {
                return;
            }
        }

        //スタン状態へ
        ChangeState("Stun");
    }

    public void StunColliderHitProcess() 
    {
        m_attackedCount++;

        StunStateCollidersActivateFlag(false);

        if (m_attackedCount >= 2)
        {
            ChangeState("Death");
        }
        else 
        {
            ChangeState("Attacked");
        }
    }

    public void SelectStateCollidersActivateFlag(bool flag) 
    {
        ActiveAllPictureFrameCollider(m_selectStateColliders, flag);
    }

    public void StunStateCollidersActivateFlag(bool flag)
    {
        ActiveAllPictureFrameCollider(m_stunStateColliders, flag);
    }

    //額縁のコライダーをまた狙えるようにする
    private void ActiveAllPictureFrameCollider(List<TargetableObject> colliderList, bool activeFlag)
    {
        foreach (TargetableObject collider in colliderList)
        {
            if (collider == null)
            {
                continue;
            }

            collider.SetTargetable(activeFlag);
            collider.gameObject.SetActive(activeFlag);
        }
    }

    public void SetBodyPartsPosition(Vector3 position) 
    {
        foreach (BodyPartMover part in m_wholeBody) 
        {
            Vector3 movePosition = part.GetAnchorPosition() + position;
            part.SetAnchorPosition(movePosition);
        }
    }

    //全部移動
    public void MoveAllParts(Vector3 targetPosition,float duration) 
    {
        StartCoroutine(MoveAllPartsRoutine(targetPosition, duration));
    }

    public IEnumerator MoveAllPartsRoutine(Vector3 targetPosition, float duration) 
    {
        float elapsedTime = 0;

        Vector3[] startPosition = new Vector3[m_wholeBody.Count];
        Vector3[] endPosition = new Vector3[m_wholeBody.Count];

        for (int i = 0; i < m_wholeBody.Count; i++)
        {
            startPosition[i] = m_wholeBody[i].GetAnchorPosition();
            endPosition[i] = startPosition[i] + targetPosition;
        }

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float rate = Easing.EaseInOutCubic(elapsedTime / duration);

            rate = Mathf.Clamp01(rate);

            for (int i = 0; i < m_wholeBody.Count; i++)
            {
                Vector3 position = Vector3.Lerp(startPosition[i], endPosition[i], rate);
                m_wholeBody[i].SetAnchorPosition(position);
            }

            yield return null;
        }
    }

    //上半身移動
    public IEnumerator MoveUpperBody(Vector3 targetPosition, float duration)
    {
        yield return MoveBodyPartsRoutine(m_upperBody,targetPosition, duration);
    }

    //足移動
    public IEnumerator MoveLegs(Vector3 targetPosition, float duration)
    {
        yield return MoveBodyPartsRoutine(m_legs, targetPosition, duration);
    }

    private IEnumerator MoveBodyPartsRoutine(List<BodyPartMover> parts, Vector3 targetPosition, float duration) 
    {
        float elapsedTime = 0;

        Vector3[] startPosition = new Vector3[parts.Count];
        Vector3[] endPosition = new Vector3[parts.Count];

        for (int i = 0; i < parts.Count; i++)
        {
            startPosition[i] = parts[i].GetAnchorPosition();
            endPosition[i] = startPosition[i] + targetPosition;
        }

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float rate = Easing.EaseInOutCubic(elapsedTime / duration);

            rate = Mathf.Clamp01(rate);

            for (int i = 0; i < parts.Count; i++)
            {
                Vector3 position = Vector3.Lerp(startPosition[i], endPosition[i], rate);
                parts[i].SetAnchorPosition(position);
            }

            yield return null;
        }
    }

    public IEnumerator ResetBodyPartsPosition(float duration) 
    {
        yield return ResetBodyPartsPositionRoutine(duration);
    }

    //体の各パーツを元の位置に戻す
    IEnumerator ResetBodyPartsPositionRoutine(float duration) 
    {
        float elapsedTime = 0;

        Vector3[] startPosition = new Vector3[m_wholeBody.Count];
        Vector3[] endPosition = new Vector3[m_wholeBody.Count];

        for (int i = 0; i < m_wholeBody.Count; i++)
        {
            startPosition[i] = m_wholeBody[i].GetAnchorPosition();
            endPosition[i] = m_wholeBody[i].GetAnchorStartPosition();
        }

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float rate = Easing.EaseInOutCubic(elapsedTime / duration);

            rate = Mathf.Clamp01(rate);

            for (int i = 0; i < m_wholeBody.Count; i++)
            {
                Vector3 position = Vector3.Lerp(startPosition[i], endPosition[i], rate);
                m_wholeBody[i].SetAnchorPosition(position);
            }

            yield return null;
        }
    }

    //上の３つだけ移動
}
