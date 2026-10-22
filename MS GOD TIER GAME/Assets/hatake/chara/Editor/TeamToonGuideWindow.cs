using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TeamToon.Editor
{
    /// <summary>Shows the production ShaderGUI in a separate window for screenshots.</summary>
    public sealed class TeamToonGuideWindow : EditorWindow
    {
        const string OpenRequest = "Temp/TeamToon.OpenGuideInspector";
        static readonly string[] Tabs = { "基本", "陰影", "材質", "ツヤ", "照明", "顔・髪", "輪郭", "発光", "オバケ", "Renderer" };
        [SerializeField] Material material;
        [SerializeField] UniversalRendererData rendererData;
        [SerializeField] int tab;
        MaterialEditor materialEditor;
        UnityEditor.Editor rendererEditor;
        TeamToonShaderGUI gui;
        Vector2 scroll;

        [MenuItem("Tools/Team Toon/資料用Inspectorを開く")]
        public static void OpenWindow()
        {
            var window = GetWindow<TeamToonGuideWindow>(true, "Team Toon / Unity Inspector 撮影", true);
            window.minSize = new Vector2(600, 440);
            window.position = new Rect(30, 30, 740, 610);
            if (!window.material)
            {
                var selected = Selection.activeObject as Material;
                window.material = IsToon(selected) ? selected : AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/TeamToon/Samples/Ghost/Materials/Ghost_1_body_0.mat");
                if (!window.material)
                    window.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamToon/Samples/Materials/Standard.mat");
            }
            if (!window.rendererData)
                window.rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                    "Assets/TeamToon/Samples/Settings/TeamToon_ForwardPlus_Renderer.asset");
            window.Focus();
            window.Repaint();
        }

        // A one-shot local request lets an already running editor open after recompilation.
        [InitializeOnLoadMethod]
        static void ConsumeOpenRequest()
        {
            if (!File.Exists(OpenRequest)) return;
            File.Delete(OpenRequest);
            EditorApplication.delayCall += OpenWindow;
        }

        static bool IsToon(Material value) => value && value.shader && value.shader.name == "custom/hatake/ToonMaster";

        void OnGUI()
        {
            if (gui == null)
            {
                gui = new TeamToonShaderGUI();
                gui.ShowDocumentationSection(tab);
            }
            GUILayout.Label("custom/hatake/ToonMaster - 実際のMaterial Inspector", EditorStyles.boldLabel);
            GUILayout.Label("撮影用ウィンドウ：製品のShaderGUIをそのまま表示", EditorStyles.miniLabel);
            int next = GUILayout.Toolbar(tab, Tabs);
            if (next != tab)
            {
                tab = next;
                scroll = Vector2.zero;
                gui.ShowDocumentationSection(tab);
            }
            if (tab == 9)
            {
                var nextRenderer = (UniversalRendererData)EditorGUILayout.ObjectField("対象Renderer", rendererData, typeof(UniversalRendererData), false);
                if (nextRenderer != rendererData) { rendererData = nextRenderer; ReleaseEditors(); }
            }
            else
            {
                var nextMaterial = (Material)EditorGUILayout.ObjectField("対象Material", material, typeof(Material), false);
                if (nextMaterial != material) { material = nextMaterial; ReleaseEditors(); }
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            float labelWidth = EditorGUIUtility.labelWidth;
            float fieldWidth = EditorGUIUtility.fieldWidth;
            try
            {
                EditorGUIUtility.labelWidth = 280;
                EditorGUIUtility.fieldWidth = 65;
                if (tab == 9)
                {
                    if (rendererData)
                    {
                        if (!rendererEditor) rendererEditor = UnityEditor.Editor.CreateEditor(rendererData);
                        rendererEditor.OnInspectorGUI();
                    }
                    else EditorGUILayout.HelpBox("確認したいURP Renderer Dataを指定してください。", MessageType.Info);
                }
                else if (IsToon(material))
                {
                    if (!materialEditor) materialEditor = (MaterialEditor)UnityEditor.Editor.CreateEditor(material);
                    gui.OnGUI(materialEditor, MaterialEditor.GetMaterialProperties(new Object[] { material }));
                }
                else EditorGUILayout.HelpBox("ShaderがTeam/ToonのMaterialを指定してください。", MessageType.Info);
            }
            finally
            {
                EditorGUIUtility.labelWidth = labelWidth;
                EditorGUIUtility.fieldWidth = fieldWidth;
                EditorGUILayout.EndScrollView();
            }
        }

        void ReleaseEditors()
        {
            if (materialEditor) DestroyImmediate(materialEditor);
            if (rendererEditor) DestroyImmediate(rendererEditor);
            materialEditor = null;
            rendererEditor = null;
        }

        void OnDisable() => ReleaseEditors();
    }
}
