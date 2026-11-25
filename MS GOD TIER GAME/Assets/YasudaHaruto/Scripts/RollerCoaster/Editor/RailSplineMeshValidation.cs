using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace YasudaHaruto.RollerCoaster.Editor
{
    // メニューには追加せず、バッチモードまたはコードから実行する。
    public static class RailSplineMeshValidation
    {
        private const string ReportPath = "Temp/RailSplineValidation/result.txt";

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            File.AppendAllText(ReportPath, "PASS " + message + "\n");
        }


        public static void RunBatch()
        {
            Directory.CreateDirectory("Temp/RailSplineValidation");
            File.WriteAllText(ReportPath, "Rail spline mesh validation\n");
            try
            {
                Verify();
                File.AppendAllText(ReportPath, "Rail Spline COMPLETE\n");
            }
            catch (Exception exception)
            {
                File.AppendAllText(ReportPath, "FAIL " + exception + "\n");
                Debug.LogException(exception);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                throw;
            }
        }


        public static void Verify()
        {
            string importerBefore = File.ReadAllText(RailSplineMeshSetup.ModelPath + ".meta");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RailSplineMeshSetup.SamplePath);
            if (prefab == null)
                prefab = RailSplineMeshSetup.SaveSample();
            Require(prefab != null, "Sample prefab is saved");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            string testMeshPath = null;
            try
            {
                var rail = instance.GetComponent<RailSplineMesh>();
                var savedMesh = rail.GeneratedRoot.GetComponent<MeshFilter>().sharedMesh;
                Require(AssetDatabase.Contains(savedMesh), "Generated mesh survives as a saved asset");
                Require(savedMesh.vertexCount > rail.SourceMesh.vertexCount, "Real FBX rail is subdivided and repeated on curve and hill");
                Require(savedMesh.subMeshCount == rail.SourceMesh.subMeshCount, "Material submeshes survive");
                Require(rail.GeneratedRoot.GetComponent<MeshRenderer>().sharedMaterials.Length == rail.Materials.Length, "Material slots survive");
                Require(savedMesh.vertices.All(Finite), "All curved and sloped positions are finite");
                Require(savedMesh.normals.All(n => Finite(n) && n.sqrMagnitude > 0.9f), "Normals remain finite and normalized");
                Require(savedMesh.uv.Length == savedMesh.vertexCount, "UV0 survives subdivision");
                Vector3[] sourceVertices = rail.SourceMesh.vertices;
                var startSection = sourceVertices.Where(v => Mathf.Abs(v.z - rail.SourceMesh.bounds.min.z) < 0.0001f).Select(v => new Vector2(v.x, v.y)).Distinct().ToArray();
                var endSection = sourceVertices.Where(v => Mathf.Abs(v.z - rail.SourceMesh.bounds.max.z) < 0.0001f).Select(v => new Vector2(v.x, v.y)).Distinct().ToArray();
                Require(startSection.Length > 0 && startSection.Length == endSection.Length && startSection.All(a => endSection.Any(b => Vector2.Distance(a, b) < 0.0001f)), "Real source endpoint cross-sections match for continuous repetitions");
                foreach (var point in savedMesh.vertices)
                    RequireBounds(savedMesh.bounds, point);
                File.AppendAllText(ReportPath, $"Sample bounds: {savedMesh.bounds}, vertices: {savedMesh.vertexCount}, source bounds: {rail.SourceMesh.bounds}\n");

                // 直線上で反復の継ぎ目、端の切り詰め、距離指定を検証。
                var container = rail.Spline;
                var line = new Spline();
                line.Add(new BezierKnot(Vector3.zero), TangentMode.AutoSmooth);
                line.Add(new BezierKnot(new Vector3(0f, 0f, 15f)), TangentMode.AutoSmooth);
                container.Spline = line;
                Set(rail, "m_startDistance", 1.25f);
                Set(rail, "m_endDistance", 11.37f);
                Set(rail, "m_crossSectionOffset", new Vector2(0.4f, -0.2f));
                Mesh straight = RailSplineMeshGenerator.Build(rail);
                try
                {
                    Require(Mathf.Abs(straight.bounds.min.z - 1.25f) < 0.003f && Mathf.Abs(straight.bounds.max.z - 11.37f) < 0.003f, "Start and trimmed end match world arc distances");
                    Require(Mathf.Abs(straight.bounds.center.x - 0.4f) < 0.001f && Mathf.Abs(straight.bounds.center.y + 0.2f) < 0.001f, "Cross-section offset uses the spline frame");
                    Require(Mathf.Abs(straight.bounds.size.x - rail.SourceMesh.bounds.size.x) < 0.001f, "Rail width is preserved");
                    float seam = 1.25f + rail.SourceMesh.bounds.size.z;
                    var vertices = straight.vertices;
                    Require(vertices.Count(v => Mathf.Abs(v.z - seam) < 0.001f) >= 4, "Adjacent repetitions share the same end plane");
                    Require(straight.uv.Any(uv => uv.x != 0f || uv.y != 0f), "UV values are retained rather than replaced");
                }
                finally { UnityEngine.Object.DestroyImmediate(straight); }

                // Containerと生成物に別々のTransformを与えても経路のワールド位置を共有。
                instance.transform.SetPositionAndRotation(new Vector3(7f, 3f, -4f), Quaternion.Euler(10f, 30f, 0f));
                instance.transform.localScale = new Vector3(1.2f, 0.8f, 1.3f);
                var table = new RailDistanceTable(container);
                Vector3 startWorld = container.EvaluatePosition(0, table.Parameter(1.25f));
                Vector3 sampled = container.EvaluatePosition(0, table.Parameter(11.37f));
                Require(Mathf.Abs(Vector3.Distance(startWorld, sampled) - 10.12f) < 0.01f, "Distance mapping accounts for container scale and rotation");
                Mesh transformed = RailSplineMeshGenerator.Build(rail);
                Require(transformed.vertices.All(Finite), "Transformed owner generates finite local vertices");
                UnityEngine.Object.DestroyImmediate(transformed);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;

                // 失敗しても以前の出力を保持。手置きオブジェクトも再生成対象にしない。
                var handPlaced = new GameObject("Hand placed");
                handPlaced.transform.SetParent(instance.transform);
                Set(rail, "m_endDistance", 500f);
                GameObject originalRoot = rail.GeneratedRoot;
                bool rejected = false;
                try
                {
                    RailSplineMeshGenerator.Generate(rail, false);
                }
                catch (ArgumentException) { rejected = true; }
                Require(rejected && rail.GeneratedRoot == originalRoot && handPlaced != null, "Invalid range preserves previous output and hand-placed objects");
                Set(rail, "m_endDistance", 11.37f);
                RailSplineMeshGenerator.Generate(rail);
                Undo.FlushUndoRecordObjects();
                GameObject replacement = rail.GeneratedRoot;
                testMeshPath = AssetDatabase.GetAssetPath(replacement.GetComponent<MeshFilter>().sharedMesh);
                Require(replacement != null && handPlaced != null, "Regeneration replaces only managed output");
                Undo.PerformUndo();
                Require(rail.GeneratedRoot != null && rail.GeneratedRoot.GetComponent<MeshFilter>().sharedMesh == savedMesh && handPlaced != null, "Undo restores prior mesh and managed reference");
                Undo.PerformRedo();
                Require(rail.GeneratedRoot != null && rail.GeneratedRoot.GetComponent<MeshFilter>().sharedMesh != savedMesh, "Redo restores rebuilt output");
                Undo.ClearUndo(rail);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                // この検証が生成したメッシュだけを除く。サンプルと利用者の出力は維持する。
                if (testMeshPath != null)
                    AssetDatabase.DeleteAsset(testMeshPath);
            }
            Require(File.ReadAllText(RailSplineMeshSetup.ModelPath + ".meta") == importerBefore, "FBX importer remains unchanged");
        }


        public static void RenderBatch()
        {
            RunBatch();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RailSplineMeshSetup.SamplePath);
            var preview = new PreviewRenderUtility();
            try
            {
                GameObject model = preview.InstantiatePrefabInScene(prefab);
                var bounds = model.GetComponentInChildren<MeshRenderer>().bounds;
                preview.camera.backgroundColor = new Color(0.07f, 0.09f, 0.12f);
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.nearClipPlane = 0.1f;
                preview.camera.farClipPlane = 250f;
                preview.lights[0].intensity = 2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(45f, 35f, 0f);
                preview.lights[1].intensity = 1.5f;
                preview.ambientColor = new Color(0.5f, 0.5f, 0.5f);
                Render("overview.png", bounds.center + new Vector3(32f, 38f, -36f), bounds.center, 40f);
                Render("curve-close.png", new Vector3(17f, 11f, 12f), new Vector3(6f, 0.5f, 21f), 42f);
                File.AppendAllText(ReportPath, "Rendered overview.png and curve-close.png using original materials\n");
                void Render(string file, Vector3 position, Vector3 lookAt, float fieldOfView)
                {
                    preview.camera.transform.position = position;
                    preview.camera.transform.LookAt(lookAt);
                    preview.camera.fieldOfView = fieldOfView;
                    preview.BeginPreview(new Rect(0, 0, 1280, 900), GUIStyle.none);
                    preview.Render(true);
                    Texture image = preview.EndPreview();
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = (RenderTexture)image;
                    var texture = new Texture2D(1280, 900, TextureFormat.RGB24, false);
                    try
                    {
                        texture.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0);
                        texture.Apply();
                        File.WriteAllBytes("Temp/RailSplineValidation/" + file, texture.EncodeToPNG());
                    }
                    finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
                }
            }
            finally { preview.Cleanup(); }
        }


        private static void Set(RailSplineMesh rail, string field, float value)
        {
            var serialized = new SerializedObject(rail);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(RailSplineMesh rail, string field, Vector2 value)
        {
            var serialized = new SerializedObject(rail);
            serialized.FindProperty(field).vector2Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static void RequireBounds(Bounds bounds, Vector3 point)
        {
            bounds.Expand(0.001f);
            if (!bounds.Contains(point))
                throw new InvalidOperationException("Generated bounds do not contain vertices");
        }
    }
}
