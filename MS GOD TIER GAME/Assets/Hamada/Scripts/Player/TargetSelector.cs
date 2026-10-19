using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Cinemachine;
using UnityEngine;

[ExecuteAlways]
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

    [SerializeField] private float m_maxTargetDistance = 0.17f;

    [SerializeField] private bool m_drawDebugArea = false;

    List<TargetInfo> m_targetList = new List<TargetInfo>();

    TargetInfo[] m_targetArray = new TargetInfo[3];

    public void SortTargets() 
    {
        m_targetList.Clear();

        //範囲に入った敵をリストに保管
        CheckIfAddToTargetList();

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

        // 各エリアの基準点
        float[] centerX =
        {
        1.0f / 6.0f,
        0.5f,
        5.0f / 6.0f
        };

        var candidates = new List<(float distance, int index, TargetInfo target)>();

        bool assignAllFlag = m_targetList.Count == 3;

        for (int i = 0; i < centerX.Length; i++)
        {
            foreach (TargetInfo target in m_targetList) 
            {
                float distance = Mathf.Abs(target.m_screenPos.x - centerX[i]);
                
                //基準点の範囲内か
                if (!assignAllFlag && distance > m_maxTargetDistance) 
                {
                    continue;
                }

                candidates.Add((distance, i, target));
            }
        }

        //距離が近い順に処理
        candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

        HashSet<GameObject> selectedTargets = new HashSet<GameObject>();

        foreach (var candidate in candidates) 
        {
            //既にこの方向配列に登録されてるならスキップ
            if (m_targetArray[candidate.index].m_target != null) 
            {
                continue;
            }

            //別の方向に割り当てられてるか
            if (selectedTargets.Contains(candidate.target.m_target)) 
            {
                continue;
            }

            TargetInfo selectedTarget = candidate.target;

            m_targetArray[candidate.index] = selectedTarget;

            selectedTargets.Add(selectedTarget.m_target);
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

        // 各エリアの基準点
        float[] centerX =
        {
        0.5f / 6.0f,
        0.5f,
        5.5f / 6.0f
        };

        DrawTargetRange(centerX[0], m_maxTargetDistance, new Color(0f, 0.5f, 1f, 0.25f), "LEFT");
        DrawTargetRange(centerX[1], m_maxTargetDistance, new Color(0f, 1f, 0f, 0.25f), "CENTER");
        DrawTargetRange(centerX[2], m_maxTargetDistance, new Color(1f, 0.5f, 0f, 0.25f), "RIGHT");
    }

    private void DrawTargetRange(float centerViewportX, float range, Color color, string label)
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
        GUI.Label(new Rect(screenMinX + 8f, 8f, 120f, 25f), label);

        GUI.color = oldColor;
    }
}
