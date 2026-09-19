using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace LightShaftLab
{
    public sealed class ShaftPerformanceDemo : MonoBehaviour
    {
        public GameObject[] m_sets;
        [Range(0, 50)] public int m_count = 1;
        public bool m_autoRun;
        public int m_spotCandidates, m_renderedSpots, m_fogCandidates, m_renderedFog;
        int m_applied = -1;
        bool m_running;
        LightShaftVolume m_settings;
        Vector3 m_savedPosition;
        Quaternion m_savedRotation;
        bool m_savedProfiling;
        LightShaftFlyCamera m_flyCamera;
        bool m_restoreFlyCamera;
        string m_status = "Ready";
        readonly List<string> m_rows = new List<string>();
        static readonly int[] g_counts = { 0, 1, 5, 10, 20, 30, 40, 50 };
        void OnEnable() => LightShaftFeature.SelectionUpdated += OnSelection;
        void OnDisable() { LightShaftFeature.SelectionUpdated -= OnSelection; StopAllCoroutines(); RestoreCamera(); m_running = false; }
        void Start() { var volume=UnityEngine.Object.FindFirstObjectByType<UnityEngine.Rendering.Volume>(); if(volume)volume.profile.TryGet(out m_settings); SetCount(m_count); if (m_autoRun) StartCoroutine(Measure()); }
        void Update() { if (m_applied != m_count) SetCount(m_count); }
        void OnSelection(Camera camera, int spots, int renderedSpots, int fog, int renderedFog)
        {
            if (camera != GetComponent<Camera>()) return;
            m_spotCandidates = spots; m_renderedSpots = renderedSpots;
            m_fogCandidates = fog; m_renderedFog = renderedFog;
        }
        public void SetCount(int count)
        {
            m_count = Mathf.Clamp(count, 0, m_sets == null ? 0 : m_sets.Length); m_applied = m_count;
            if (m_sets != null) for (int i = 0; i < m_sets.Length; i++) if (m_sets[i]) m_sets[i].SetActive(i < m_count);
        }
        public bool IsPointerOverPanel(Vector2 position) => new Rect(15, 15, 520, 340).Contains(new Vector2(position.x, Screen.height - position.y));
        void RestoreCamera()
        {
            if(!m_running)return;
            if (m_flyCamera) m_flyCamera.enabled = m_restoreFlyCamera;
            transform.SetPositionAndRotation(m_savedPosition,m_savedRotation);
            ShaftProfiling.SetEnabled(m_savedProfiling);
        }
        IEnumerator Measure()
        {
            m_savedPosition=transform.position;m_savedRotation=transform.rotation;
            m_savedProfiling=ShaftProfiling.Enabled;ShaftProfiling.SetEnabled(true);
            m_running = true; m_rows.Clear();
            m_flyCamera = GetComponent<LightShaftFlyCamera>();
            m_restoreFlyCamera = m_flyCamera && m_flyCamera.enabled;
            if (m_flyCamera) m_flyCamera.enabled = false;
            try
            {
            var gpuRows=new List<string>{"sets,round,frame,pass,gpu_ms,cpu_command_ms"};
            m_rows.Add("sets,round,median_frame_ms,p95_frame_ms,spot_candidates,rendered_spots,fog_candidates,rendered_fog");
            foreach (int count in g_counts)
            for(int round=1;round<=3;round++)
            {
                SetCount(count); m_status = "Warmup " + count;
                ShaftBenchmarkPath.Apply(GetComponent<Camera>(),0,180);
                yield return new WaitForSecondsRealtime(2);
                var samples = new List<float>();
                m_status = "Measuring " + count;
                for (int i = 0; i < 180; i++)
                {
                    ShaftBenchmarkPath.Apply(GetComponent<Camera>(),i,180);
                    yield return null;samples.Add(Time.unscaledDeltaTime*1000);
                    foreach(ShaftProfiling.Pass pass in System.Enum.GetValues(typeof(ShaftProfiling.Pass)))
                    {
                        float gpu=ShaftProfiling.GpuMilliseconds(pass),cpu=ShaftProfiling.CpuMilliseconds(pass);
                        gpuRows.Add($"{count},{round},{i},{pass},{Number(gpu)},{Number(cpu)}");
                    }
                }
                samples.Sort();
                m_rows.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:F3},{3:F3},{4},{5},{6},{7}", count, round, samples[90], samples[170], m_spotCandidates, m_renderedSpots, m_fogCandidates, m_renderedFog));
            }
            string path = Path.Combine(Application.persistentDataPath, "ShaftPerformance.csv");
            File.WriteAllLines(path, m_rows);File.WriteAllLines(Path.Combine(Application.persistentDataPath,"ShaftGPU.csv"),gpuRows); m_status = "Saved: " + path;
            Debug.Log("Frame intervals include VSync, Editor and all scene rendering; not GPU pass timing. " + path);
            }
            finally { RestoreCamera(); m_running = false; }
        }
        static string Number(float value)=>float.IsNaN(value)?"":value.ToString("F6",CultureInfo.InvariantCulture);
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(15, 15, 520, 340), GUI.skin.box);
            GUILayout.Label("LIGHT SHAFT / 50 SET PERFORMANCE DEMO");
            GUI.enabled = !m_running;
            GUILayout.BeginHorizontal();
            foreach (int count in g_counts) if (GUILayout.Button(count.ToString())) SetCount(count);
            GUILayout.EndHorizontal();
            m_count = Mathf.RoundToInt(GUILayout.HorizontalSlider(m_count, 0, 50));
            if (GUILayout.Button("Measure 0 -> 50 sets (fixed route, 3 rounds)")) StartCoroutine(Measure());
            if(m_settings)
            {
                GUILayout.BeginHorizontal();
                foreach(ShaftQualityPreset preset in System.Enum.GetValues(typeof(ShaftQualityPreset)))
                    if(GUILayout.Button(preset.ToString()))ShaftQualityPresets.Apply(m_settings,preset);
                GUILayout.EndHorizontal();
                m_settings.m_temporalFlag.Override(GUILayout.Toggle(m_settings.m_temporalFlag.value,"Temporal reuse"));
                m_settings.m_tileCullingFlag.Override(GUILayout.Toggle(m_settings.m_tileCullingFlag.value,"Tile culling"));
            }
            GUI.enabled = true;
            GUILayout.Label($"Sets: {m_count} | Spot: {m_renderedSpots}/4 ({m_spotCandidates} candidates) | Fog: {m_renderedFog}/16 ({m_fogCandidates} candidates)");
            GUILayout.Label("Fixed slots / priority selection / fade-out then fade-in");
            GUILayout.Label("RMB + WASD: move | Highlight region: local sun boost");
            GUILayout.Label(m_status);
            GUILayout.EndArea();
        }
    }
}
