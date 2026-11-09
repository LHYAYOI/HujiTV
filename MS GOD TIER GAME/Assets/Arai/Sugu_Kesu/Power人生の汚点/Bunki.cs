using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Bunki : GimmickSpline
{
    [SerializeField] private List<Transform> m_roomObject;
    [SerializeField] private GimmickSplineSwitchPoint m_switchPoint;
    [SerializeField] private Vector3 m_basePosition = Vector3.zero;


    [System.Serializable]
    public class BunkiData
    {
        [SerializeField] private Vector3 m_position;
        [SerializeField] private List<GameObject> m_door;
        [SerializeField] private SplineController2 m_toSpline;


        public Vector3 Position => m_position;
        public List<GameObject> Door => m_door; 
        public SplineController2 ToSpline => m_toSpline;
    }
    [SerializeField] private List<BunkiData> m_routeData;




    public void SetRoute(int index)
    {
        if (index < 0 || m_routeData.Count < index)
        {
            return;
        }
        BunkiData data = m_routeData[index];


        if(m_switchPoint != null)
        {
            m_switchPoint.TargetSplineController = data.ToSpline;
        }

        if (m_roomObject != null)
        {
            foreach (Transform t in m_roomObject) { t.transform.position = data.Position; }
        }

        foreach(GameObject go in data.Door)
        {
            go.SetActive(false);
        }
    }


    private void Update()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            SetRoute(0);
        }
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SetRoute(1);
        }
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            SetRoute(2);
        }
    }

    public void SetBasePosition()
    {


        if (m_roomObject != null)
        {
            foreach (Transform t in m_roomObject) { t.transform.position = m_basePosition; }
        }
    }
}
