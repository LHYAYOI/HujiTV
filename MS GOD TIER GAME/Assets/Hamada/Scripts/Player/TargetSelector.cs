using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public class TargetSelector : MonoBehaviour
{
    private struct TargetInfo 
    {
        public GameObject m_target;
        public Vector3 m_screenPos;

        public TargetInfo(GameObject target, Vector3 screenPos) 
        {
            m_target = target;
            m_screenPos = screenPos;
        }
    }

    [Header("参照")]

    [SerializeField] private float m_targetRange = 10.0f;

    List<TargetInfo> m_targetList = new List<TargetInfo>();

    TargetInfo[] m_targetArray = new TargetInfo[3];

    public void SortTargets() 
    {
        m_targetList.Clear();

        //敵のリスト作成
        GameObject[] targets = GameObject.FindGameObjectsWithTag("Target");

        //範囲に入った敵をリストに保管
        CheckIfAddToTargetList(targets);

        //ターゲットを配列に割り当てる
        AssignTarget();
    }

    private void AssignTarget()
    {
        m_targetArray = new TargetInfo[3];

        if (m_targetList.Count == 0) 
        {
            return;
        }

        //基準点
        float[] distanceX =
        {
            1.0f / 6.0f,
            0.5f,
            5.0f / 6.0f
        };

        for (int i = 0; i < m_targetArray.Length; i++)
        {
            float nearestDistance = float.MaxValue;

            //基準点に最も近いターゲットを探す
            foreach (TargetInfo target in m_targetList)
            {
                float distance = Mathf.Abs(target.m_screenPos.x - distanceX[i]);    

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    m_targetArray[i] = target;
                }
            }
        }
    }

    private void CheckIfAddToTargetList(GameObject[] array) 
    {
        foreach (GameObject target in array)
        {
            if (target == null)
            {
                continue;
            }

            Vector3 viewPos = Camera.main.WorldToViewportPoint(target.transform.position);

            //範囲内か
            if (IsInsideScreen(viewPos) && IsTargetInRange(target))
            {
                m_targetList.Add(new TargetInfo(target, viewPos));
            }
        }
    }

    private bool IsTargetInRange(GameObject target) 
    {
        float distance = Vector3.Distance(Camera.main.transform.position, target.transform.position);

        return distance <= m_targetRange;
    }

    //画面ないかどうか調べる関数
    private bool IsInsideScreen(Vector3 targetViewPos) 
    {
        if (targetViewPos.x > 0 && targetViewPos.x <= 1 &&
            targetViewPos.y > 0 && targetViewPos.y <= 1 &&
            targetViewPos.z > 0) 
        {
            return true;
        }

        return false;
    }

    public GameObject GetSelectedTarget(MAGIC_CAST_DIRECTION index) 
    {
        int intIndex = (int)index;

        if (intIndex < 0 || intIndex >= m_targetArray.Length) 
        {
            return null;
        }

        GameObject target = m_targetArray[intIndex].m_target;

        if (target == null) 
        {
            return null;
        }

        return target;
    }
}
