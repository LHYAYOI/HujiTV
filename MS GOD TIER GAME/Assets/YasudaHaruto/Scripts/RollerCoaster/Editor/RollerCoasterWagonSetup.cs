using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace YasudaHaruto.RollerCoaster.Editor
{
    /// <summary>乗車視点に表示するワゴンの縁と補助ライトを配置する。</summary>
    internal static class RollerCoasterWagonSetup
    {
        internal const string Source = "Assets/map/wagon/wagon.fbx";

        [MenuItem("Tools/Roller Coaster/Add Wagon Ride Foreground")]
        internal static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("編集モードで実行してください。");
            var ride = Object.FindFirstObjectByType<RollerCoasterTrialRide>();
            if (ride == null || ride.gameObject.scene.path != RollerCoasterCourseBuilder.ScenePath)
                throw new System.InvalidOperationException("RollerCoaster Sceneが必要です。");
            var old = ride.Cart.transform.Find("Ride Wagon Foreground");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Ride Wagon Foreground");
            root.transform.SetParent(ride.Cart.transform, false);
            root.transform.localPosition = new Vector3(0, .65f, .25f);
            root.transform.localScale = Vector3.one * 1.15f;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source), root.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.gameObject.layer = 5;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source => AssetDatabase.LoadAssetAtPath<Material>(
                    source.name.ToLowerInvariant().Contains("warning") ? "Assets/map/wagon/warning.mat" : "Assets/map/wagon/main.mat")).ToArray();
            }
            foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            // 近距離の車体だけが走行用スポットライトで白飛びしないようにする。
            foreach (var light in ride.Cart.GetComponentsInChildren<Light>(true))
                light.cullingMask &= ~(1 << 5);
            var fill = new GameObject("Wagon Soft Fill");
            fill.transform.SetParent(root.transform, false);
            fill.transform.position = ride.Cart.transform.TransformPoint(new Vector3(0, 1.05f, .15f));
            var lamp = fill.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 2.5f;
            lamp.intensity = .08f;
            lamp.shadows = LightShadows.None;
            lamp.cullingMask = 1 << 5;
            ride.SetForegroundVisual(root.transform);
            EditorUtility.SetDirty(ride);
            EditorSceneManager.MarkSceneDirty(ride.gameObject.scene);
            EditorSceneManager.SaveScene(ride.gameObject.scene);
        }

        internal static void Inspect()
        {
            Directory.CreateDirectory("Temp/RollerCoasterStoryboard");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            var log = new StringBuilder();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                log.AppendLine($"{renderer.name}: bounds={renderer.bounds}, materials={string.Join(",", renderer.sharedMaterials.Select(x => x.name))}");
            File.WriteAllText("Temp/RollerCoasterStoryboard/wagon-inspect.txt", log.ToString());
            var preview = new PreviewRenderUtility();
            try
            {
                var instance = Object.Instantiate(model);
                preview.AddSingleGO(instance);
                var bounds = instance.GetComponentsInChildren<Renderer>().Select(x => x.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                preview.camera.transform.position = bounds.center + new Vector3(1, .7f, 1).normalized * bounds.size.magnitude;
                preview.camera.transform.LookAt(bounds.center);
                preview.camera.nearClipPlane = .01f;
                preview.camera.farClipPlane = 1000;
                preview.camera.backgroundColor = new Color(.15f, .15f, .15f);
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.lights[0].intensity = 2;
                preview.lights[0].transform.rotation = Quaternion.Euler(40, 40, 0);
                preview.lights[1].intensity = 1;
                preview.BeginStaticPreview(new Rect(0, 0, 960, 540));
                preview.Render(true);
                var texture = preview.EndStaticPreview();
                File.WriteAllBytes("Temp/RollerCoasterStoryboard/wagon-preview.png", texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            finally { preview.Cleanup(); }
        }
    }
}
