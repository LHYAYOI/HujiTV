using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;

#endif
namespace LightShaftLab
{
    // UI上で開始した操作はカメラに渡さない。
    [RequireComponent(typeof(Camera))]
    public sealed class LightShaftFlyCamera : MonoBehaviour
    {
        [Min(0.1f)]
        [UnityEngine.Serialization.FormerlySerializedAs("speed")]
        public float m_speed = 5;
        [UnityEngine.Serialization.FormerlySerializedAs("sensitivity")]
        public float m_sensitivity = 0.15f;
        [UnityEngine.Serialization.FormerlySerializedAs("panSensitivity")]
        public float m_panSensitivity = 0.01f;
        bool m_lookFlag, m_panFlag;
        CursorLockMode m_previousLock;
        bool m_restoreCursorVisibleFlag;
        LightShaftDemoControls m_controls;
        void Awake()
        {
            m_controls = GetComponent<LightShaftDemoControls>();
        }

        void Update()
        {
            Vector2 position, delta;
            float wheel;
            bool pressRightFlag, holdRightFlag, pressMiddleFlag, holdMiddleFlag, cancelFlag, accelerateFlag;
            Vector3 move;

// ===================================================================================
// 新InputSystemが有効な場合はそちらを優先する。
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null || keyboard == null)
            {
                Release();
                return;
            }

            position = mouse.position.ReadValue();
            delta = mouse.delta.ReadValue();
            wheel = mouse.scroll.ReadValue().y * 3;
            pressRightFlag = mouse.rightButton.wasPressedThisFrame;
            holdRightFlag = mouse.rightButton.isPressed;
            pressMiddleFlag = mouse.middleButton.wasPressedThisFrame;
            holdMiddleFlag = mouse.middleButton.isPressed;
            cancelFlag = keyboard.escapeKey.wasPressedThisFrame;
            accelerateFlag = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            move = new Vector3((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0), (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
#else
            position = Input.mousePosition;
            delta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10;
            wheel = Input.mouseScrollDelta.y* 3;
            pressRightFlag = Input.GetMouseButtonDown(1);
            holdRightFlag = Input.GetMouseButton(1);
            pressMiddleFlag = Input.GetMouseButtonDown(2);
            holdMiddleFlag = Input.GetMouseButton(2);
            cancelFlag = Input.GetKeyDown(KeyCode.Escape);
            accelerateFlag = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            move=new Vector3((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),
                (Input.GetKey(KeyCode.E)?1:0)-(Input.GetKey(KeyCode.Q)?1:0),
                (Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0));
#endif// if ENABLE_INPUT_SYSTEM


            if (cancelFlag)
            {
                Release();
                return;
            }

            if (m_lookFlag && !holdRightFlag)
                Release();
            if (!holdMiddleFlag)
                m_panFlag = false;
            bool capturePointerFlag = m_controls && m_controls.isActiveAndEnabled && m_controls.IsPointerOverPanel(position);
            if (pressRightFlag && !capturePointerFlag)
            {
                m_previousLock = Cursor.lockState;
                m_restoreCursorVisibleFlag = Cursor.visible;
                m_lookFlag = true;
                m_panFlag = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                delta = Vector2.zero;
            }

            if (pressMiddleFlag && !capturePointerFlag && !m_lookFlag)
                m_panFlag = true;
            if (m_lookFlag)
            {
                MoveCamera(delta, wheel, move, accelerateFlag);
            }
            else if (m_panFlag)
                transform.position += (-transform.right * delta.x - transform.up * delta.y) * m_panSensitivity;
            else if (!capturePointerFlag)
                transform.position += transform.forward * wheel * m_speed * 0.2f;
        }

        void MoveCamera(Vector2 delta, float wheel, Vector3 movement, bool accelerateFlag)
        {
            var angles = transform.eulerAngles;
            float pitch = Mathf.DeltaAngle(0, angles.x);
            transform.rotation = Quaternion.Euler(
                Mathf.Clamp(pitch - delta.y * m_sensitivity, -89, 89),
                angles.y + delta.x * m_sensitivity, 0);
            m_speed = Mathf.Clamp(m_speed * Mathf.Pow(1.2f, wheel), 0.1f, 100);
            Vector3 direction = transform.right * movement.x + Vector3.up * movement.y + transform.forward * movement.z;
            transform.position += Vector3.ClampMagnitude(direction, 1) * m_speed * (accelerateFlag ? 4 : 1) * Time.unscaledDeltaTime;
        }

        void Release()
        {
            // 操作開始前のカーソル状態に戻す。
            if (m_lookFlag)
            {
                Cursor.lockState = m_previousLock;
                Cursor.visible = m_restoreCursorVisibleFlag;
            }

            m_lookFlag = false;
            m_panFlag = false;
        }

        void OnDisable()
        {
            Release();
        }

        void OnApplicationFocus(bool receiveFocusFlag)
        {
            if (!receiveFocusFlag)
                Release();
        }
    }
}
