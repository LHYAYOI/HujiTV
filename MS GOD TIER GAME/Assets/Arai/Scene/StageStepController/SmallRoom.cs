using System.Collections.Generic;
using UnityEngine;

public class SmallRoom : MonoBehaviour
{
    public enum STATE : int
    {
        // í‚É
        // ‚R•C‚ÌŒ®‚¨‰»‚¯‚ªoŒ»‚µ‚Ä‚¢‚é

        IDLE,

        DOOR_OPEN_QUICK,// Œ®”à‚É˜b‚µ‚©‚¯‚ç‚ê‚é‘O‚É‚R•C“|‚µ‚½ê‡

        // OR

        DOOR_TALK,      // Œ®”à‚É˜b‚©‚¯‚ç‚ê‚é
        GET_KEY,        // ‚R•C“|‚· & ‹ß“¹ƒ‹[ƒg‰ğ•ú
        DOOR_OPEN,      // Œ®”à‚Æ˜b‚µ‚ÄŒ®‰ğœ

        MAX,
    }
    private STATE m_state = STATE.IDLE;



    [System.Serializable]
    public class StateObjects
    {
        [SerializeField] private List<GameObject> m_objects = new List<GameObject>();

        public List<GameObject> Objects { get { return m_objects; } }
        public void SetActive(bool active)
        {
            foreach (var obj in m_objects)
            {
                obj?.SetActive(active);
            }
        }
    }

    [SerializeField] private List<StateObjects> m_stateObjes = new();


    private void Awake()
    {
        if(m_stateObjes.Count <= (int)STATE.MAX)
        {
                Destroy(gameObject);
            return;
        }


        // ˆê’Ufalse
        foreach (StateObjects so in m_stateObjes)
        {
            so.SetActive(false);
        }
        // IDLE Object‚ğtrue
        m_stateObjes[(int)m_state].SetActive(true);
    }



    public void StateChange(int stateIdx)
    {
        if(stateIdx < 0 || (int)STATE.MAX <= stateIdx)
        {
            return;
        }

        StateChange((STATE)stateIdx);   
    }
    public void StateChange(STATE state)
    {
        m_stateObjes[(int)m_state].SetActive(false);


        m_state = state;
        m_stateObjes[(int)m_state].SetActive(true);
    }


}
