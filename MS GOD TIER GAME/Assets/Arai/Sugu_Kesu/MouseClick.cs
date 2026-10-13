using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;


public class MouseClickRaycaster : MonoBehaviour
{
    [SerializeField] private Camera m_camera;
    [SerializeField] private float m_maxDistance = 100f;
    [SerializeField] private LayerMask m_layerMask = ~0;

    private IHitable m_current;
    private HitData m_lastHitData;

    private void Awake()
    {
        if (m_camera == null) m_camera = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {


            Ray ray = m_camera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (!Physics.Raycast(ray, out RaycastHit hit, m_maxDistance, m_layerMask))
            {
                return;
            }

            HitReceiverController target = hit.collider.GetComponentInParent<HitReceiverController>();
            HitData data = CreateHitData(hit.collider.gameObject, hit.point, hit.normal);

            target.OnHit(data);
        }
    }

    private HitData CreateHitData(GameObject hitObject, Vector3 point, Vector3 normal)
    {
        HitData hitData = new HitData();

        hitData.SetHitObject(this.gameObject);

        return hitData;
    }
}