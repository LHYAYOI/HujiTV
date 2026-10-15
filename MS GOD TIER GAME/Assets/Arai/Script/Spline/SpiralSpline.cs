using UnityEngine;
using UnityEngine.Splines;

public class SpiralSpline : MonoBehaviour
{
    [SerializeField] private SplineContainer m_spline;

    [SerializeField] private float m_radius;

    // 時計回りフラグ
    [SerializeField] private bool m_clockRotFlag;

    // ポイントの設置情報
    [Header("螺旋情報")]
    [SerializeField] private float m_startAngle;
    [SerializeField] private float m_startHeight;
    [SerializeField] private float m_endHeight;

    [Tooltip("頂点数")]
    [SerializeField] private int m_pointNumber;
    [Tooltip("周回数")]
    [SerializeField] private int m_turnNumber;



#if UNITY_EDITOR

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }


        CreateSpiral();
    }

#endif


    private void CreateSpiral()
    {
        if (m_spline == null || m_pointNumber < 2)
        {
            return;
        }


        // 既存のSplineを削除
        // Splineが存在しなければ1本作る
        if (m_spline.Splines.Count == 0)
        {
            m_spline.AddSpline(new Spline());
        }

        // 既存のSplineを取得
        Spline spline = m_spline.Splines[0];
        spline.Clear();

        // 回転方向
        // 上から見て反時計回り = +1
        // 上から見て時計回り   = -1
        float direction = m_clockRotFlag ? -1.0f : 1.0f;

        for (int i = 0; i < m_pointNumber; i++)
        {
            // 0 ～ 1
            float t = i / (float)(m_pointNumber - 1);

            // 角度
            float angleDeg =
                m_startAngle
                + 360.0f * m_turnNumber * t * direction;

            float angleRad = angleDeg * Mathf.Deg2Rad;

            // XZ平面上で円を描く
            float x = Mathf.Cos(angleRad) * m_radius;
            float z = Mathf.Sin(angleRad) * m_radius;

            // 高さを線形に変化
            float y = Mathf.Lerp(
                m_startHeight,
                m_endHeight,
                t
            );

            Vector3 position = new Vector3(
                x,
                y,
                z
            );

            // SplineのKnotを追加
            BezierKnot knot = new BezierKnot(position);
            spline.Add(knot,TangentMode.AutoSmooth);
        }
    }
}
