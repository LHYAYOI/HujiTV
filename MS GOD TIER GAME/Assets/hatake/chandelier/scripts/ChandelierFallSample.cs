using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace hatake
{
    /// <summary>
    /// サンプルシーン（ChandelierFall）の管理。開始時に部品ごとに分けたシャンデリアのモデルを置いて吊るし、
    /// 画面左上の操作（または Space / R キー）で落とす・戻す。
    /// 本編では、シーンに置いたモデルの根に ChandelierBreakable を付け、Drop() を呼べば同じように動く。
    /// </summary>
    public sealed class ChandelierFallSample : MonoBehaviour
    {
        [Tooltip("部品ごとに分けたモデル（hatake/chandelier/model/Shanderia_Broken.fbx）")]
        [SerializeField] private GameObject m_model;
        [Tooltip("衝突の演出")]
        [SerializeField] private ChandelierImpactEffect m_effect;
        [Tooltip("吊るす高さ（シャンデリアの下端の高さ、m）")]
        [SerializeField, Range(1f, 30f)] private float m_hangHeight = 9f;
        [Tooltip("置く位置（床の上の XZ）")]
        [SerializeField] private Vector2 m_position = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private Key m_dropKey = Key.Space;
        [SerializeField] private Key m_resetKey = Key.R;
#else
        [SerializeField] private KeyCode m_dropKey = KeyCode.Space;
        [SerializeField] private KeyCode m_resetKey = KeyCode.R;
#endif

        private ChandelierBreakable m_chandelier;
        private float m_placedHeight;

        private void Start()
        {
            if (m_model == null)
            {
                Debug.LogError("ChandelierFallSample : Model が未設定です", this);
                enabled = false;
                return;
            }
            //Spawn();
        }

        // モデルを置き、下端が Hang Height になるよう持ち上げてから、割れる仕組みを付ける
        private void Spawn()
        {
            // モデルの根の向き（Blender から来た X -90°）はそのまま使う。identity で置くと横倒しになる
            GameObject go = Instantiate(m_model, new Vector3(m_position.x, 0f, m_position.y), m_model.transform.rotation);
            go.name = "Chandelier";
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                go.transform.position += Vector3.up * (m_hangHeight - b.min.y);
            }
            m_placedHeight = m_hangHeight;
            m_chandelier = go.AddComponent<ChandelierBreakable>();
            m_chandelier.SetEffect(m_effect);
        }

        private void Update()
        {
            if (m_chandelier == null) return;
            if (KeyPressed(m_dropKey)) Drop();
            if (KeyPressed(m_resetKey)) ResetChandelier();
        }

        public void Drop()
        {
            if (m_chandelier.IsHanging) m_chandelier.Drop();
        }

        private void ResetChandelier()
        {
            // 吊るす高さを変えたときは置き直す
            if (!Mathf.Approximately(m_placedHeight, m_hangHeight))
            {
                Destroy(m_chandelier.gameObject);
                Spawn();
                return;
            }
            m_chandelier.ResetChandelier();
        }

#if ENABLE_INPUT_SYSTEM
        private static bool KeyPressed(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
#else
        private static bool KeyPressed(KeyCode key) => Input.GetKeyDown(key);
#endif

        private void OnGUI()
        {
            if (m_chandelier == null) return;
            GUILayout.BeginArea(new Rect(12f, 12f, 260f, 150f), GUI.skin.box);
            GUILayout.Label("Chandelier fall (Space: drop / R: reset)");
            GUI.enabled = m_chandelier.IsHanging;
            if (GUILayout.Button("Drop")) Drop();
            GUI.enabled = true;
            if (GUILayout.Button("Reset")) ResetChandelier();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Height " + m_hangHeight.ToString("0") + "m", GUILayout.Width(80f));
            m_hangHeight = GUILayout.HorizontalSlider(m_hangHeight, 1f, 30f);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
