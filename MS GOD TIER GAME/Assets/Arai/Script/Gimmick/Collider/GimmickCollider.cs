using System;
using UnityEngine;

public class GimmickCollider : MonoBehaviour 
{
    [SerializeField] protected LayerMask m_targetLayer;

    public Action<Collision> OnEnter { get; }
    public Action<Collision> OnStay { get; }
    public Action<Collision> OnExit { get; }


}
