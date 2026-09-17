using UnityEngine;
using UnityEngine.Rendering;

namespace LightShaftLab
{
    public sealed class LightShaftDemoControls : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("volume")]
        public Volume m_volume;
        [UnityEngine.Serialization.FormerlySerializedAs("directional")]
        public LightShaftEmitter m_directional;
        [UnityEngine.Serialization.FormerlySerializedAs("spots")]
        public LightShaftEmitter[] m_spots;
        [UnityEngine.Serialization.FormerlySerializedAs("localFog")]
        public LocalFogVolume[] m_localFog;
        [UnityEngine.Serialization.FormerlySerializedAs("dust")]
        public ParticleSystemRenderer m_dust;
        LightShaftVolume m_settings;
        [SerializeField] bool m_showPanel = true;
        Rect m_panel = new Rect(16, 16, 340, 510);
        Vector2 m_scroll;
        bool m_resizeFlag;
        Vector2 m_resizeOrigin, m_originalSize;
        public bool IsPointerOverPanel(Vector2 screenPosition) => m_panel.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        void Start()
        {
            if (m_volume)
                m_volume.profile.TryGet(out m_settings);
            if (GetComponent<Camera>() && !GetComponent<LightShaftFlyCamera>())
                gameObject.AddComponent<LightShaftFlyCamera>();
        }

        void OnDisable()
        {
            m_resizeFlag = false;
        }

        void OnGUI()
        {
            if (!m_showPanel)
                return;
            if (!m_settings)
                return;
            UpdatePanelSize();
            DrawPanel();
        }

        void UpdatePanelSize()
        {
            // リサイズ操作は設定UIより先に処理する。
            var currentEvent = Event.current;
            var grip = new Rect(m_panel.xMax - 24, m_panel.yMax - 24, 24, 24);
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && grip.Contains(currentEvent.mousePosition))
            {
                m_resizeFlag = true;
                m_resizeOrigin = currentEvent.mousePosition;
                m_originalSize = m_panel.size;
                currentEvent.Use();
            }

            if (m_resizeFlag && currentEvent.type == EventType.MouseDrag)
            {
                m_panel.size = m_originalSize + currentEvent.mousePosition - m_resizeOrigin;
                currentEvent.Use();
            }

            if (currentEvent.rawType == EventType.MouseUp)
                m_resizeFlag = false;
            m_panel.width = Mathf.Clamp(m_panel.width, Mathf.Min(260, Screen.width - 20), Mathf.Max(1, Screen.width - 20));
            m_panel.height = Mathf.Clamp(m_panel.height, Mathf.Min(180, Screen.height - 20), Mathf.Max(1, Screen.height - 20));
        }

        void DrawPanel()
        {
            GUI.Box(m_panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(m_panel.x + 8, m_panel.y + 8, m_panel.width - 16, m_panel.height - 34));
            m_scroll = GUILayout.BeginScrollView(m_scroll);
            DrawEffectSettings();
            DrawQualitySettings();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.Label(new Rect(m_panel.x + 8, m_panel.yMax - 25, m_panel.width - 36, 22), "Drag bottom-right to resize");
            GUI.Label(new Rect(m_panel.xMax - 24, m_panel.yMax - 24, 24, 24), "◢");
        }

        void DrawEffectSettings()
        {
            GUILayout.Label("LIGHT SHAFT / VOLUMETRIC ATMOSPHERE");
            m_settings.m_enableFlag.value = GUILayout.Toggle(m_settings.m_enableFlag.value, "Volumetric atmosphere");
            if (m_directional)
                m_directional.enabled = GUILayout.Toggle(m_directional.enabled, "Directional light shaft");
            bool enableSpotsFlag = m_spots != null && m_spots.Length > 0 && m_spots[0] && m_spots[0].enabled;
            bool changeSpotsFlag = GUILayout.Toggle(enableSpotsFlag, "Spot light shafts");
            if (changeSpotsFlag != enableSpotsFlag && m_spots != null)
                foreach (var spot in m_spots)
                    if (spot)
                        spot.enabled = changeSpotsFlag;
            if (m_localFog != null && m_localFog.Length > 0)
            {
                bool enableFogFlag = m_localFog[0] && m_localFog[0].enabled;
                bool changeFogFlag = GUILayout.Toggle(enableFogFlag, "Local box / sphere fog");
                if (changeFogFlag != enableFogFlag)
                    foreach (var fog in m_localFog)
                        if (fog)
                            fog.enabled = changeFogFlag;
            }

            if (m_dust)
                m_dust.enabled = GUILayout.Toggle(m_dust.enabled, "Light-reactive dust");
            m_settings.m_enableGodRaysFlag.value = GUILayout.Toggle(m_settings.m_enableGodRaysFlag.value, "God Ray (screen-space sun)");
            m_settings.m_enableSpatialGodRaysFlag.value = GUILayout.Toggle(m_settings.m_enableSpatialGodRaysFlag.value, "God Ray (world-space shadows)");
            if (m_settings.m_enableSpatialGodRaysFlag.value)
            {
                GUILayout.Label($"Spatial sun strength {m_settings.m_spatialGodRayIntensity.value:F2}");
                m_settings.m_spatialGodRayIntensity.value = GUILayout.HorizontalSlider(m_settings.m_spatialGodRayIntensity.value, 0, 5);
                GUILayout.Label($"Scattering directionality {m_settings.m_anisotropy.value:F2}");
                m_settings.m_anisotropy.value = GUILayout.HorizontalSlider(m_settings.m_anisotropy.value, -0.5f, 0.8f);
            }

        }

        void DrawQualitySettings()
        {
            GUILayout.Label("Quality");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("High"))
            {
                m_settings.m_resolutionDivisor.value = 1;
                m_settings.m_steps.value = 64;
            }

            if (GUILayout.Button("Balanced"))
            {
                m_settings.m_resolutionDivisor.value = 2;
                m_settings.m_steps.value = 48;
            }

            if (GUILayout.Button("Fast"))
            {
                m_settings.m_resolutionDivisor.value = 4;
                m_settings.m_steps.value = 32;
            }

            GUILayout.EndHorizontal();
            GUILayout.Label($"Raymarch resolution 1/{m_settings.m_resolutionDivisor.value}");
            GUILayout.Label($"Density {m_settings.m_density.value:F3}");
            m_settings.m_density.value = GUILayout.HorizontalSlider(m_settings.m_density.value, 0, 0.15f);
            GUILayout.Label($"Scattering {m_settings.m_intensity.value:F2}");
            m_settings.m_intensity.value = GUILayout.HorizontalSlider(m_settings.m_intensity.value, 0, 3);
            GUILayout.Label($"Samples {m_settings.m_steps.value}");
            m_settings.m_steps.value = Mathf.RoundToInt(GUILayout.HorizontalSlider(m_settings.m_steps.value, 16, 96));
            GUILayout.Label($"God Ray intensity {m_settings.m_godRayIntensity.value:F2}");
            m_settings.m_godRayIntensity.value = GUILayout.HorizontalSlider(m_settings.m_godRayIntensity.value, 0, 3);
        }
    }
}
