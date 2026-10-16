using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Cinemachine;

public class TargetSelector : MonoBehaviour
{
    private struct TargetInfo 
    {
        public GameObject m_target;
        public Vector3 m_screenPos;
        public bool m_isSelectedFlag;


        public TargetInfo(GameObject target, Vector3 screenPos) 
        {
            m_target = target;
            m_screenPos = screenPos;
            m_isSelectedFlag = false;
        }
    }

    [Header("参照")]

    [SerializeField] private float m_targetRange = 10.0f;

    [SerializeField] private float m_centerAreaWidth = 0.6f;

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
            return;

        // 左右エリアの幅
        float sideWidth = (1.0f - m_centerAreaWidth) * 0.5f;

        // 各エリアの基準点
        float[] centerX =
        {
        sideWidth * 0.5f,
        0.5f,
        1.0f - sideWidth * 0.5f
    };

        for (int i = 0; i < m_targetArray.Length; i++)
        {
            float nearestDistance = float.MaxValue;

            foreach (TargetInfo target in m_targetList)
            {
                float x = target.m_screenPos.x;

                // エリアを判定
                int areaIndex;

                if (x < sideWidth)
                {
                    areaIndex = 0; // 左
                }
                else if (x < 1.0f - sideWidth)
                {
                    areaIndex = 1; // 中央
                }
                else
                {
                    areaIndex = 2; // 右
                }

                // 対象エリア以外ならスキップ
                if (areaIndex != i)
                    continue;

                float distance = Mathf.Abs(x - centerX[i]);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;

                    m_targetArray[i] = target;
                    m_targetArray[i].m_isSelectedFlag = true;
                }
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
            return;

        float sideWidth = (1.0f - m_centerAreaWidth) * 0.5f;

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        Rect leftRect = new Rect(0, 0, screenWidth * sideWidth, screenHeight);
        Rect centerRect = new Rect(screenWidth * sideWidth, 0, screenWidth * m_centerAreaWidth, screenHeight);
        Rect rightRect = new Rect(screenWidth * (1.0f - sideWidth), 0, screenWidth * sideWidth, screenHeight);

        DrawArea(leftRect, new Color(0f, 0.5f, 1f, 0.2f), "LEFT");
        DrawArea(centerRect, new Color(0f, 1f, 0f, 0.2f), "CENTER");
        DrawArea(rightRect, new Color(1f, 0.5f, 0f, 0.2f), "RIGHT");
    }

    private void DrawArea(Rect rect, Color color, string label)
    {
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 10, rect.y + 10, 100, 30), label);

        GUI.color = oldColor;
    }
}
