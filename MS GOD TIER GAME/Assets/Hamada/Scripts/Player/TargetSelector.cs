using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Cinemachine;
using UnityEngine;

[ExecuteAlways]
public class TargetSelector : MonoBehaviour
{
    [Serializable]
    private struct SelectAreaData 
    {
       public bool m_hasDualityFlag;
       [UnityEngine.Range(0, 1)] public float m_positionX;
       public List<int> m_directionIndexList; 
       public float m_maxTargetDistance;
       public Color m_areaColor;
       public string m_AreaName;
       public float m_labelHeight;
    }

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

    [SerializeField] private bool m_drawDebugArea = false;

    [SerializeField] List<SelectAreaData> m_selectAreaDataList; 

    List<TargetInfo> m_targetList = new List<TargetInfo>();

    TargetInfo[] m_targetArray = new TargetInfo[3];

    public void SortTargets() 
    {
        m_targetList.Clear();

        //範囲に入った敵をリストに保管
        CheckIfAddToTargetList();

        //ターゲットを配列に割り当てる

        bool assignAllFlag = m_targetList.Count == 3;

        if (assignAllFlag)
        {
            AssignTargetSimple();
        }
        else
        {
            AssignTarget();
        }
    }

    private void AssignTargetSimple() 
    {
        List<TargetInfo> sortList = m_targetList;

        sortList.Sort((a, b) => a.m_screenPos.x.CompareTo(b.m_screenPos.x));

        for (int i = 0; i < m_targetList.Count; i++) 
        {
            m_targetArray[i] = sortList[i];
        }
    }

    private void AssignTarget()
    {
        // 左・上・右の3方向
        m_targetArray = new TargetInfo[3];

        //候補ターゲットリスト
        var candidates = new List<(float distance, int areaIndex, TargetInfo target)>();

        //各方向エリアの候補となるターゲットを洗い出す
        for (int i = 0; i < m_selectAreaDataList.Count; i++)
        {
            SelectAreaData area = m_selectAreaDataList[i];

            if (area.m_directionIndexList == null || area.m_directionIndexList.Count == 0)
            {
                continue;
            }

            //各方向エリアの範囲内にターゲットがいるかどうかを見る
            foreach (TargetInfo target in m_targetList)
            {
                float distance = Mathf.Abs(target.m_screenPos.x - area.m_positionX);

                if (distance > area.m_maxTargetDistance)
                {
                    continue;
                }

                candidates.Add((distance, i, target));
            }
        }

        candidates.Sort((a, b) =>
        {
            //N面性をチェックし1面性エリアにいるターゲットを優先する
            bool aIsDual = m_selectAreaDataList[a.areaIndex].m_hasDualityFlag;
            bool bIsDual = m_selectAreaDataList[b.areaIndex].m_hasDualityFlag;

            if (aIsDual && !bIsDual) 
            {//bを先に
                return 1;
            }

            if (!aIsDual && bIsDual)
            {//aを先に
                return -1;
            }

            //同じN面性方向タイプならそれぞれ持ってる基準点までの距離で並べ替え
            return a.distance.CompareTo(b.distance);
        });

        //既に割り当ててるターゲットを保持するリスト
        var selectedTargets = new HashSet<GameObject>();

        foreach (var candidate in candidates)
        {
            SelectAreaData area = m_selectAreaDataList[candidate.areaIndex];

            // 同じ敵を別のエリアから重複して割り当てない
            if (selectedTargets.Contains(candidate.target.m_target))
            {
                continue;
            }

            bool assigned = false;

            //方向配列にターゲットを割り当てる
            foreach (int directionIndex in area.m_directionIndexList)
            {
                if (directionIndex < 0 || directionIndex >= m_targetArray.Length)
                {
                    continue;
                }

                // この方向に先に選ばれたターゲットがいればここはスキップ
                if (m_targetArray[directionIndex].m_target != null)
                {
                    continue;
                }

                m_targetArray[directionIndex] = candidate.target;
                assigned = true;
            }

            if (assigned)
            {
                selectedTargets.Add(candidate.target.m_target);
            }
        }
    }

    private void CheckIfAddToTargetList() 
    {
        GameObject[] targets = GameObject.FindGameObjectsWithTag("Target");

        foreach (GameObject target in targets)
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

    private void OnGUI()
    {
        if (!m_drawDebugArea)
        {
            return;
        }

        for(int i = 0; i < m_selectAreaDataList.Count; i++) 
        {
            SelectAreaData area = m_selectAreaDataList[i];

            DrawTargetRange(area.m_positionX, area.m_maxTargetDistance, area.m_areaColor, area.m_AreaName, area.m_labelHeight);
        }
    }

    private void DrawTargetRange(float centerViewportX, float range, Color color, string label, float labelHeight = 0)
    {
        float minX = Mathf.Clamp01(centerViewportX - range);
        float maxX = Mathf.Clamp01(centerViewportX + range);

        float screenMinX = minX * Screen.width;
        float screenWidth = (maxX - minX) * Screen.width;

        Rect areaRect = new Rect(screenMinX, 0f, screenWidth, Screen.height);

        Color oldColor = GUI.color;

        // 範囲の帯
        GUI.color = color;
        GUI.DrawTexture(areaRect, Texture2D.whiteTexture);

        // 基準点の線
        float centerScreenX = centerViewportX * Screen.width;
        Rect lineRect = new Rect(centerScreenX - 1f, 0f, 2f, Screen.height);
        GUI.color = Color.white;
        GUI.DrawTexture(lineRect, Texture2D.whiteTexture);

        // ラベル
        GUI.Label(new Rect(screenMinX + 8f, 8f + labelHeight, 120f, 25f), label);

        GUI.color = oldColor;
    }
}
