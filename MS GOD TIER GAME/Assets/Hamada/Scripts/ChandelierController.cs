using UnityEngine;

public class ChandelierController : MonoBehaviour
{
    [SerializeField] Rigidbody m_rigidbody;

    public void HitProcess(HitData data) 
    {
        if (data.GetHitObject.CompareTag("MagicBullet")) 
        {
            m_rigidbody.isKinematic = false;
            m_rigidbody.useGravity = true;
        }

        if (data.GetHitObject.CompareTag("Floor")) 
        {
            CameraManager.Instance.ShakeCameraInVHF(2.0f, 1.0f, 1.0f, 3.0f);

            CameraManager.Instance.ChangeCameraFOV010(3.0f, 70);

            Debug.Log("Chandelier hit the floor");
        }
    }
}
