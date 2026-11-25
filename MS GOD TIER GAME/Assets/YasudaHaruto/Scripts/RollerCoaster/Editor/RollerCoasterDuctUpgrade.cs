using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace YasudaHaruto.RollerCoaster.Editor
{
    /// <summary>新しいダクト素材を担当側へ適用し、囲いの開口と連続性を確認する。</summary>
    internal static class RollerCoasterDuctUpgrade
    {

        internal static void Refine()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/YasudaHaruto/Scenes/RollerCoaster.unity")
                throw new InvalidOperationException("Dedicated scene required");
            EditorSceneManager.SaveScene(scene, "Assets/YasudaHaruto/Backups~/RollerCoaster-before-rounded-rooms-scene-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
            var course = UnityEngine.Object.FindFirstObjectByType<RollerCoasterCourse>();
            var settings = course.GetComponentInChildren<RollerCoasterEnclosures>();
            Audit(settings, "before-rounded");
            var config = new SerializedObject(settings);
            config.FindProperty("m_cornerRadius").floatValue = .45f;
            config.ApplyModifiedPropertiesWithoutUndo();
            RollerCoasterEnclosureBuilder.Generate(settings);
            Audit(settings, "after-rounded");
            var ride = UnityEngine.Object.FindFirstObjectByType<RollerCoasterTrialRide>();
            var lighting = ride.GetComponent<RollerCoasterDuctLighting>();
            var data = new SerializedObject(lighting);
            data.FindProperty("m_darkAmbient").colorValue = new Color(.10f, .12f, .14f);
            data.FindProperty("m_darkFog").floatValue = .016f;
            var fill = ride.Cart.transform.Find("Ride Near Fill");
            if (fill == null)
            {
                var go = new GameObject("Ride Near Fill");
                fill = go.transform;
                fill.SetParent(ride.Cart.transform, false);
            }
            fill.localPosition = new Vector3(0, 1.2f, .2f);
            var light = fill.GetComponent<Light>();
            if (light == null)
                light = fill.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 9;
            light.intensity = 2;
            light.color = new Color(.85f, .9f, 1);
            light.shadows = LightShadows.None;
            light.enabled = false;
            data.FindProperty("m_nearFillLight").objectReferenceValue = light;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lighting);
            EditorUtility.SetDirty(light);
            var rideData = new SerializedObject(ride);
            var headlight = rideData.FindProperty("m_trialLight").objectReferenceValue as Light;
            headlight.range = 48;
            headlight.spotAngle = 130;
            headlight.innerSpotAngle = 105;
            headlight.intensity = 16;
            EditorUtility.SetDirty(headlight);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            RollerCoasterRideTuning.StartValidation();
            File.AppendAllText("Temp/RollerCoasterRideTuning/ride-tuning-result.txt", "Rounded room/shaft corners .45m; corrected face normals and rearward sparks; headlight 48m/130deg plus near fill and fog .016.\n");
        }


        internal static void Audit(RollerCoasterEnclosures settings, string name)
        {
            var root = new GameObject("Temporary enclosure coverage query") { hideFlags = HideFlags.HideAndDontSave };
            bool backfaces = Physics.queriesHitBackfaces;
            try
            {
                var filter = settings.GeneratedRoot.GetComponent<MeshFilter>();
                root.transform.SetParent(filter.transform, false);
                var collider = root.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                Physics.queriesHitBackfaces = true;
                Physics.SyncTransforms();
                var table = new RailDistanceTable(settings.Course.Spline);
                var log = new StringBuilder();
                int rays = 0, misses = 0;
                for (float d = 10; d < table.Length - 10; d += 1f)
                {
                    table.Frame(d, out var position, out var rotation);
                    Vector3 eye = position + rotation * Vector3.up * 1.1f;
                    for (int i = 0; i < 16; i++)
                    {
                        float angle = (i + .25f) * Mathf.PI / 8f;
                        Vector3 direction = rotation * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), .15f).normalized;
                        rays++;
                        if (!collider.Raycast(new Ray(eye, direction), out _, 1000))
                        {
                            misses++;
                            if (misses < 100)
                                log.AppendLine($"MISS d={d:F2} eye={eye:F3} direction={direction:F3}");
                        }
                    }
                }
                log.Insert(0, $"Coverage rays={rays}, misses={misses}\n");
                File.WriteAllText("Temp/RollerCoasterRideTuning/" + name + "-coverage.txt", log.ToString());
            }
            finally { Physics.queriesHitBackfaces = backfaces; UnityEngine.Object.DestroyImmediate(root); }
        }


        internal static void Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/YasudaHaruto/Scenes/RollerCoaster.unity")
                throw new InvalidOperationException("Dedicated scene required");
            EditorSceneManager.SaveScene(scene, "Assets/YasudaHaruto/Backups~/RollerCoaster-before-new-duct-scene-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
            var course = UnityEngine.Object.FindFirstObjectByType<RollerCoasterCourse>();
            float oldStraight = (course.FallLaunchParameter - RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline, course.SpiralEndKnot)) * course.Spline.CalculateLength();
            float target = oldStraight * 3f;
            float grade = course.SpiralDrop / (2f * Mathf.PI * course.EndRadius);
            var data = new SerializedObject(course);
            data.FindProperty("m_turns").intValue = 1;
            data.FindProperty("m_fallApproachLength").floatValue = (target - course.LaunchRailExtension) / Mathf.Sqrt(1 + grade * grade);
            data.ApplyModifiedPropertiesWithoutUndo();
            RollerCoasterTwoEncounterSetup.Build(course);
            var settings = course.GetComponentInChildren<RollerCoasterEnclosures>();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/map/duct/duct.fbx");
            var filter = asset.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "duct_straight");
            const string materialPath = "Assets/YasudaHaruto/Data/RollerCoaster/Enclosures/Source Duct.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/map/duct/duct.mat"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = Shader.Find("YasudaHaruto/RollerCoaster Source Duct");
            material.shaderKeywords = Array.Empty<string>();
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/map/duct/duct_ver02_BaseColor.png"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/map/duct/duct_ver02_Normal.png"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/map/duct/duct_ver02_Metalness.png"));
            material.SetTexture("_RoughnessMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/map/duct/duct_ver02_Roughness.png"));
            float uvWidth = filter.sharedMesh.uv.Max(p => p.x);
            material.SetVector("_AtlasRect", new Vector4(0, 0, uvWidth, 1));
            material.SetColor("_BaseColor", Color.white);
            float sourceLength = filter.transform.TransformVector(Vector3.forward * filter.sharedMesh.bounds.size.z).magnitude;
            float sourceWidth = filter.transform.TransformVector(Vector3.right * filter.sharedMesh.bounds.size.x).magnitude;
            material.SetFloat("_TileMeters", settings.Width * sourceLength / sourceWidth);
            material.SetFloat("_BumpScale", 1f);
            EditorUtility.SetDirty(material);
            settings.Configure(course, filter.sharedMesh, new[] { material });
            var enclosureData = new SerializedObject(settings);
            enclosureData.FindProperty("m_useSourceDuct").boolValue = true;
            enclosureData.ApplyModifiedPropertiesWithoutUndo();
            RollerCoasterEnclosureBuilder.Generate(settings);
            RollerCoasterBrakeSetup.Apply(true, false);
            RollerCoasterBrakeSetup.ApplyContinuousLean(false);
            float straight = (course.FallLaunchParameter - RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline, course.SpiralEndKnot)) * course.Spline.CalculateLength();
            if (course.Turns != 1 || Mathf.Abs(straight - target) > .03f)
                throw new InvalidOperationException($"Course length mismatch: {straight}, target {target}");
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            RollerCoasterRideTuning.StartValidation();
            File.AppendAllText("Temp/RollerCoasterRideTuning/ride-tuning-result.txt", $"New duct source profile and PBR maps applied; spiral=1; braking straight {oldStraight:F2}m -> {straight:F2}m (3x).\n");
        }


        internal static void Inspect()
        {
            const string folder = "Temp/RollerCoasterRideTuning";
            Directory.CreateDirectory(folder);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/map/duct/duct.fbx");
            var log = new StringBuilder();
            foreach (var filter in asset.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                log.AppendLine($"{filter.name}: {mesh.name}, vertices={mesh.vertexCount}, bounds={mesh.bounds}, local={filter.transform.localToWorldMatrix}");
                log.AppendLine("materials: " + string.Join(",", filter.GetComponent<Renderer>().sharedMaterials.Select(m => m == null ? "null" : AssetDatabase.GetAssetPath(m))));
                var vertices = mesh.vertices;
                var uv = mesh.uv;
                foreach (int axis in new[] { 0, 1, 2 })
                {
                    float min = vertices.Min(p => p[axis]), max = vertices.Max(p => p[axis]);
                    foreach (float end in new[] { min, max })
                    {
                        log.AppendLine($"axis {axis} end {end:F4}:");
                        foreach (int i in Enumerable.Range(0, vertices.Length).Where(i => Mathf.Abs(vertices[i][axis] - end) < .001f).Take(48))
                            log.AppendLine($"  {vertices[i]:F4}, uv={(uv.Length > i ? uv[i] : Vector2.zero):F4}");
                    }
                }
            }
            File.WriteAllText(folder + "/duct-inspect.txt", log.ToString());
            var preview = new PreviewRenderUtility();
            try
            {
                var model = UnityEngine.Object.Instantiate(asset);
                preview.AddSingleGO(model);
                var bounds = model.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                preview.camera.transform.position = bounds.center + new Vector3(1, .7f, -1).normalized * bounds.size.magnitude;
                preview.camera.transform.LookAt(bounds.center);
                preview.camera.nearClipPlane = .01f;
                preview.camera.farClipPlane = bounds.size.magnitude * 10;
                preview.camera.backgroundColor = new Color(.16f, .16f, .16f);
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.lights[0].intensity = 2;
                preview.lights[0].transform.rotation = Quaternion.Euler(35, 35, 0);
                preview.lights[1].intensity = 1;
                preview.BeginStaticPreview(new Rect(0, 0, 960, 540));
                preview.Render(true);
                var texture = preview.EndStaticPreview();
                File.WriteAllBytes(folder + "/duct-model.png", texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally { preview.Cleanup(); }
        }
    }
}
