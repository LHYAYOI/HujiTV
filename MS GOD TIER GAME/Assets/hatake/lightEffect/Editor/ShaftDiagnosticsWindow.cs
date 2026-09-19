using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;



/// <summary>
/// debug用のウィンドウ、シーン内のLightShaftの描画状況を確認することができる
/// プログラマ向けだからUI英語で、文句は受け付けない
 
namespace LightShaftLab.Editor
{
    public sealed class ShaftDiagnosticsWindow : EditorWindow
    {
        Camera m_camera;
        Volume m_volume;
        string m_selection = "Waiting for camera render", m_history = "Temporal off / no render";
        bool m_recording, m_previousRecording, m_overlay = true;
        readonly Dictionary<UnityEngine.Object,float> m_selected = new Dictionary<UnityEngine.Object,float>();
        readonly Queue<float> m_samples = new Queue<float>();
        double m_nextUpdate;
        Vector2 m_scroll;


        [MenuItem("Tools/Light Shaft/Quality Diagnostics")]
        public static void Open() => GetWindow<ShaftDiagnosticsWindow>("Shaft Diagnostics");
        void OnEnable()
        {
            m_previousRecording = ShaftProfiling.Enabled;
            LightShaftFeature.SelectionUpdated += Selection;
            LightShaftFeature.SelectedSource += Source;
            LightShaftFeature.HistoryUpdated += History;
            SceneView.duringSceneGui += DrawScene;
            EditorApplication.update += Tick;
        }

        void OnDisable()
        {
            LightShaftFeature.SelectionUpdated-=Selection;
            LightShaftFeature.SelectedSource-=Source;
            LightShaftFeature.HistoryUpdated-=History;
            SceneView.duringSceneGui-=DrawScene;
            EditorApplication.update-=Tick;
            if(m_recording)ShaftProfiling.SetEnabled(m_previousRecording);
            ShaftTileDebug.Clear();
        }

        void Selection(Camera camera,int spots,int renderedSpots,int fog,int renderedFog)
        {
            if(!m_camera)m_camera=camera;
            if(camera!=m_camera)return;
            m_selected.Clear();
            m_selection=$"Spot {renderedSpots}/4 of {spots} candidates | Fog {renderedFog}/16 of {fog}";
            m_history="Temporal off / no history pass";
        }

        void Source(Camera camera,UnityEngine.Object source,float weight){if(camera==m_camera)m_selected[source]=weight;}


        void History(Camera camera,string reason,int width,int height)
        {
            if(camera==m_camera)m_history=$"{reason} | {width}×{height} | history {width*(double)height*32/1048576:F2} MiB";
        }


        void Tick()
        {
            if(EditorApplication.timeSinceStartup<m_nextUpdate)return;
            m_nextUpdate=EditorApplication.timeSinceStartup+.1;
            float total=0;bool valid=false;
            foreach(ShaftProfiling.Pass pass in Enum.GetValues(typeof(ShaftProfiling.Pass)))
            {float value=ShaftProfiling.GpuMilliseconds(pass);if(!float.IsNaN(value)){total+=value;valid=true;}}
            if(m_recording&&valid){m_samples.Enqueue(total);while(m_samples.Count>120)m_samples.Dequeue();}
            Repaint();
        }

        void OnGUI()
        {
            m_scroll=EditorGUILayout.BeginScrollView(m_scroll);
            m_camera=(Camera)EditorGUILayout.ObjectField("Camera",m_camera,typeof(Camera),true);
            m_volume = (Volume)EditorGUILayout.ObjectField("Authoring Volume", m_volume, typeof(Volume), true);


            if (m_volume&&m_volume.sharedProfile&&m_volume.sharedProfile.TryGet<LightShaftVolume>(out var settings))
            {
                using(new EditorGUILayout.HorizontalScope())foreach(ShaftQualityPreset preset in Enum.GetValues(typeof(ShaftQualityPreset)))
                    if(GUILayout.Button(preset.ToString())){Undo.RecordObject(settings,"Shaft quality preset");ShaftQualityPresets.Apply(settings,preset);EditorUtility.SetDirty(settings);}
                EditorGUILayout.HelpBox("Presets change quality only. Temporal and tile culling are optional in the Volume inspector.",MessageType.Info);
            }
            EditorGUILayout.LabelField(m_selection);
            EditorGUILayout.LabelField(m_history,EditorStyles.wordWrappedLabel);
            EditorGUI.BeginChangeCheck();
            var tileMode=(ShaftTileDebugMode)EditorGUILayout.EnumPopup("Tile debug",ShaftTileDebug.Mode);
            float opacity=EditorGUILayout.Slider("Tile overlay opacity",ShaftTileDebug.Opacity,0,1);



            if(EditorGUI.EndChangeCheck() || ShaftTileDebug.Camera!=m_camera)
            {
                ShaftTileDebug.Camera=m_camera;ShaftTileDebug.Mode=tileMode;ShaftTileDebug.Opacity=opacity;
                SceneView.RepaintAll();EditorApplication.QueuePlayerLoopUpdate();
            }



            if(tileMode!=ShaftTileDebugMode.Off)
                EditorGUILayout.HelpBox("Tile grid: 8×8 low-resolution pixels. Numbers = candidate count, not GPU time. Purple 1 / blue 2 / green 3 / yellow 4–5 / orange 6–9 / red 10+. Forces tile generation for this camera; does not save to the Profile. Requires active volumetric fog. XR / unsupported formats fall back without overlay.",MessageType.Info);


            m_overlay=EditorGUILayout.Toggle("Selected volume gizmos",m_overlay);
            bool recording=EditorGUILayout.Toggle("Record pass timings",m_recording);


            if(recording!=m_recording){m_recording=recording;ShaftProfiling.SetEnabled(recording||m_previousRecording);}


            EditorGUILayout.HelpBox("GPU counters require a supported graphics backend and GPU profiling. N/A is not zero. Timings aggregate rendered cameras; isolate one camera for comparison. Root/edge refinement is included in Composite.",MessageType.Info);


            foreach(ShaftProfiling.Pass pass in Enum.GetValues(typeof(ShaftProfiling.Pass)))
            {
                float value=ShaftProfiling.GpuMilliseconds(pass);
                EditorGUILayout.LabelField(pass.ToString(),float.IsNaN(value)?"N/A":$"{value:F3} ms / invocation");
            }


            var rect=GUILayoutUtility.GetRect(100,85,GUILayout.ExpandWidth(true));EditorGUI.DrawRect(rect,new Color(.12f,.12f,.12f));


            if(m_samples.Count>1)
            {
                float maximum=.01f;foreach(float value in m_samples)maximum=Mathf.Max(maximum,value);
                var points=new Vector3[m_samples.Count];int i=0;
                foreach(float value in m_samples){points[i]=new Vector3(rect.x+rect.width*i/(m_samples.Count-1),rect.yMax-rect.height*value/maximum);i++;}
                Handles.BeginGUI();Handles.color=Color.cyan;Handles.DrawAAPolyLine(points);Handles.EndGUI();
                GUI.Label(rect,$"Sum of pass averages / max {maximum:F3} ms");
            }


            EditorGUILayout.EndScrollView();
        }
        void DrawScene(SceneView view)
        {
            if(!m_overlay)return;

            foreach(var pair in m_selected)
            {
                if(!pair.Key)continue;
                using(new Handles.DrawingScope(new Color(.2f,1,.4f,Mathf.Max(.15f,pair.Value))))
                {
                    if(pair.Key is LocalFogVolume fog)
                    {
                        using(new Handles.DrawingScope(fog.transform.localToWorldMatrix))
                            Handles.DrawWireCube(Vector3.zero,Vector3.one);
                        Handles.Label(fog.transform.position,$"Fog priority {fog.m_priority} / fade {pair.Value:F2}");
                    }


                    if(pair.Key is LightShaftEmitter emitter&&emitter.TryGetComponent<Light>(out var light))
                    {
                        Vector3 end=light.transform.position+light.transform.forward*light.range;
                        Handles.DrawLine(light.transform.position,end);
                        Handles.DrawWireDisc(end,light.transform.forward,light.range*Mathf.Tan(light.spotAngle*Mathf.Deg2Rad*.5f));
                        Handles.Label(light.transform.position,$"Spot priority {emitter.m_priority} / fade {pair.Value:F2}");
                    }
                }
            }
        }
    }
}
