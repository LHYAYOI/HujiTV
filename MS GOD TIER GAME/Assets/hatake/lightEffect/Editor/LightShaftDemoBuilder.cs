using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LightShaftLab.Editor
{
    public static class LightShaftDemoBuilder
    {
        const string Root = "Assets/LightShaft/Demo";
        [MenuItem("Tools/Light Shaft/Create Demo and Install Renderer Feature")]
        public static void Build()
        {
            Directory.CreateDirectory(Root); AssetDatabase.Refresh();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/LightShaft/Shaders/LightShaft.shader");
            foreach (string name in new[] { "PC_Renderer", "Mobile_Renderer" })
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/" + name + ".asset");
                var feature = data.rendererFeatures.OfType<LightShaftFeature>().FirstOrDefault();
                if (!feature)
                {
                    feature = ScriptableObject.CreateInstance<LightShaftFeature>(); feature.name = "Volumetric Light Shaft";
                    AssetDatabase.AddObjectToAsset(feature, data); data.rendererFeatures.Add(feature);
                }
                feature.m_shader = shader; feature.SetActive(true); feature.Create();
                var serialized = new SerializedObject(data);
                var featureMap = serialized.FindProperty("m_RendererFeatureMap");
                featureMap.arraySize = data.rendererFeatures.Count;
                for (int i = 0; i < data.rendererFeatures.Count; i++)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i], out string guid, out long id);
                    featureMap.GetArrayElementAtIndex(i).longValue = id;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(feature); EditorUtility.SetDirty(data); data.SetDirty();
            }
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.06f, 0.07f, 0.09f); RenderSettings.fog = false;
            var stone = Material("Stone", new Color(0.2f, 0.24f, 0.28f));
            var warm = Material("WarmPanels", new Color(0.42f, 0.28f, 0.15f));
            Box("Floor", new Vector3(0, -0.15f, 3), new Vector3(24, 0.3f, 24), stone);
            Box("Back wall", new Vector3(0, 3.5f, 12), new Vector3(20, 7, 0.3f), stone);
            for (int i = 0; i < 8; i++) Box("Roof slat " + i, new Vector3(-7 + i * 2, 6, 6), new Vector3(1.1f, 0.25f, 12), stone);
            for (int i = 0; i < 4; i++) Box("Pillar " + i, new Vector3(-6 + i * 4, 2.5f, 8), new Vector3(0.55f, 5, 0.55f), stone);
            Box("Spot occluder", new Vector3(-3, 1, 4), new Vector3(0.6f, 2, 0.6f), warm);
            Box("Foreground crate", new Vector3(2, 0.7f, 0), new Vector3(1.4f, 1.4f, 1.4f), warm);
            var sun = new GameObject("Directional - Roof Shafts").AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(1, 0.85f, 0.65f); sun.intensity = 1.4f;
            sun.transform.rotation = Quaternion.Euler(55, -25, 0); sun.shadows = LightShadows.Soft; sun.shadowBias = 0.03f;
            RenderSettings.sun = sun;
            var sunEmitter = sun.gameObject.AddComponent<LightShaftEmitter>(); sunEmitter.m_scattering = 0.85f;
            var a = Spot("Spot - Amber", new Vector3(-4, 5, 1), new Vector3(-3, 0, 5), new Color(1, 0.55f, 0.2f));
            var b = Spot("Spot - Cyan", new Vector3(4, 5, 5), new Vector3(2, 0, 1), new Color(0.25f, 0.65f, 1));
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(10, 4.5f, -12); camera.transform.LookAt(new Vector3(0, 2.3f, 4));
            camera.fieldOfView = 53; camera.nearClipPlane = 0.1f; camera.farClipPlane = 80;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.025f, 0.03f, 0.045f); camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData(); cameraData.renderPostProcessing = true;
            cameraData.requiresDepthTexture = true; cameraData.volumeLayerMask = 1;
            var volume = new GameObject("Atmosphere Volume").AddComponent<Volume>(); volume.isGlobal = true;
            string path = Root + "/Atmosphere.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            if (!profile.TryGet<LightShaftVolume>(out var settings))
            { settings = profile.Add<LightShaftVolume>(true); AssetDatabase.AddObjectToAsset(settings, profile); }
            settings.SetAllOverridesTo(true); settings.m_enableFlag.value = true; settings.m_density.value = 0.035f;
            settings.m_intensity.value = 1.3f; settings.m_steps.value = 48; settings.m_resolutionDivisor.value = 2; settings.m_heightFalloff.value = 0.18f;
            settings.m_scatteringTint.value = new Color(0.88f, 0.93f, 1); settings.m_noiseAmount.value = 0.25f;
            volume.sharedProfile = profile; EditorUtility.SetDirty(settings); EditorUtility.SetDirty(profile);
            var controls = camera.gameObject.AddComponent<LightShaftDemoControls>();
            controls.m_volume = volume; controls.m_directional = sunEmitter; controls.m_spots = new[] { a, b };
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, "Assets/scene/04_LightShaft_Volumetric.unity");
            if (Application.isBatchMode)
            {
                settings.m_enableFlag.value = false; Capture(camera, "LightShaft_Off");
                settings.m_enableFlag.value = true; Capture(camera, "LightShaft_All");
                a.enabled = b.enabled = false; Capture(camera, "LightShaft_Directional");
                a.enabled = b.enabled = true; sunEmitter.enabled = false; Capture(camera, "LightShaft_Spot");
                a.m_castShadowsFlag = b.m_castShadowsFlag = false; Capture(camera, "LightShaft_SpotNoShadows"); a.m_castShadowsFlag = b.m_castShadowsFlag = true;
                sunEmitter.enabled = true;
                sunEmitter.m_castShadowsFlag = a.m_castShadowsFlag = b.m_castShadowsFlag = false; Capture(camera, "LightShaft_NoShadows");
                sunEmitter.m_castShadowsFlag = a.m_castShadowsFlag = b.m_castShadowsFlag = true;
                foreach (var message in ShaderUtil.GetShaderMessages(shader)) Debug.Log(message.severity + ": " + message.file + ":" + message.line + " " + message.message);
                if (ShaderUtil.ShaderHasError(shader)) throw new System.Exception("Light shaft shader compilation failed");
                ValidateImages();
                Debug.Log("LIGHT_SHAFT_DEMO_OK directional=1 spots=2 steps=48 resolutionDivisor=2");
            }
        }
        static void ValidateImages()
        {
            var report = new System.Text.StringBuilder();
            foreach (var pair in new[] { new[] { "Off", "All" }, new[] { "Directional", "All" }, new[] { "Spot", "All" }, new[] { "NoShadows", "All" }, new[] { "SpotNoShadows", "Spot" } })
            {
                var a = new Texture2D(2, 2); var b = new Texture2D(2, 2);
                try
                {
                    a.LoadImage(File.ReadAllBytes("Artifacts/LightShaft_" + pair[0] + ".png"));
                    b.LoadImage(File.ReadAllBytes("Artifacts/LightShaft_" + pair[1] + ".png"));
                    var pa = a.GetPixels32(); var pb = b.GetPixels32(); double difference = 0;
                    for (int i = 0; i < pa.Length; i++) difference += Mathf.Abs(pa[i].r - pb[i].r) + Mathf.Abs(pa[i].g - pb[i].g) + Mathf.Abs(pa[i].b - pb[i].b);
                    difference /= pa.Length * 3.0 * 255;
                    if (difference < 0.00001) throw new System.Exception("No visible contribution: " + pair[0] + " / " + pair[1]);
                    report.AppendLine($"{pair[0]} vs {pair[1]} mean RGB difference={difference:F6} PASS");
                }
                finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
            }
            File.WriteAllText("Artifacts/LightShaft-validation.txt", report.ToString()); Debug.Log(report);
        }
        static LightShaftEmitter Spot(string name, Vector3 position, Vector3 target, Color color)
        {
            var light = new GameObject(name).AddComponent<Light>(); light.type = LightType.Spot;
            light.transform.position = position; light.transform.LookAt(target); light.color = color;
            light.range = 12; light.spotAngle = 40; light.innerSpotAngle = 28; light.intensity = 22;
            light.shadows = LightShadows.Soft; light.shadowBias = 0.03f; light.shadowNormalBias = 0;
            var emitter = light.gameObject.AddComponent<LightShaftEmitter>(); emitter.m_scattering = 3;
            return emitter;
        }
        static Material Material(string name, Color color)
        {
            string path = Root + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); EditorUtility.SetDirty(material); return material;
        }
        static void Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.position = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
        }
        static void Capture(Camera camera, string name)
        {
            VolumeManager.instance.Update(camera.transform, 1);
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGBHalf);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); camera.Render();
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Artifacts"); File.WriteAllBytes("Artifacts/" + name + ".png", pixels.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = previous; Object.DestroyImmediate(pixels); Object.DestroyImmediate(target); }
        }
    }
}
