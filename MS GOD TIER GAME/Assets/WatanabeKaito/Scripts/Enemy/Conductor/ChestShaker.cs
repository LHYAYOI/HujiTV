using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ChestShaker : MonoBehaviour
{
    [Header("アニメーター")]
    [SerializeField] private Animator m_chestAnimator;

   

    public void StartUpperChestAnimation()
    {
        if (m_chestAnimator != null)
        {
            m_chestAnimator.SetTrigger("tUpper");
        }
    }

    public void StartLowerChestAnimation()
    {
        if (m_chestAnimator != null)
        {
            m_chestAnimator.SetTrigger("tLower");
        }
    }

   
}
