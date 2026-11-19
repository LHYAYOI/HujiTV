using System.Linq;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace YasudaHaruto.RollerCoaster.Editor
{
    internal sealed class RailDistanceTable
    {
        private readonly SplineContainer m_container;
        private readonly float[] m_distances;
        public float Length => m_distances[m_distances.Length - 1];

        public RailDistanceTable(SplineContainer container)
        {
            if (container == null || container.Splines.Count == 0 || container.Spline.Count < 2)
                throw new ArgumentException("2点以上のSplineを持つContainerを指定してください。");
            m_container = container;
            int samples = Mathf.Clamp(Mathf.Max(container.Spline.Count * 64, Mathf.CeilToInt(container.CalculateLength() / 0.02f)), 256, 65536);
            m_distances = new float[samples + 1];
            Vector3 previous = Position(0f);
            for (int i = 1; i <= samples; i++)
            {
                Vector3 next = Position((float)i / samples);
                m_distances[i] = m_distances[i - 1] + Vector3.Distance(previous, next);
                previous = next;
            }
            if (!float.IsFinite(Length) || Length < 0.001f) throw new ArgumentException("Splineの長さが不足しています。");
        }

        private Vector3 Position(float t) => m_container.EvaluatePosition(0, t);

        public float Parameter(float distance)
        {
            distance = Mathf.Clamp(distance, 0f, Length);
            int found = Array.BinarySearch(m_distances, distance);
            if (found >= 0) return (float)found / (m_distances.Length - 1);
            int high = Mathf.Clamp(~found, 1, m_distances.Length - 1);
            float fraction = Mathf.InverseLerp(m_distances[high - 1], m_distances[high], distance);
            return (high - 1 + fraction) / (m_distances.Length - 1);
        }

        public float DistanceAtParameter(float parameter)
        {
            float index = Mathf.Clamp01(parameter) * (m_distances.Length - 1);
            int first = Mathf.Min(Mathf.FloorToInt(index), m_distances.Length - 2);
            return Mathf.Lerp(m_distances[first], m_distances[first + 1], index - first);
        }

        public void Frame(float distance, out Vector3 position, out Quaternion rotation)
        {
            m_container.Evaluate(0, Parameter(distance), out var p, out var tangent, out var up);
            position = p;
            Vector3 forward = tangent;
            Vector3 vertical = up;
            if (forward.sqrMagnitude < 1e-10f) throw new ArgumentException("接線が0の地点があります。Splineのノットとハンドルを調整してください。");
            forward.Normalize();
            vertical -= forward * Vector3.Dot(vertical, forward);
            if (vertical.sqrMagnitude < 1e-8f) throw new ArgumentException("Splineの上方向と接線が平行です。ノットの回転を調整してください。");
            rotation = Quaternion.LookRotation(forward, vertical.normalized);
        }
    }

    internal static class RailSplineMeshGenerator
    {
        internal const string OutputFolder = "Assets/YasudaHaruto/Data/RollerCoaster/Generated";

        internal static Mesh Build(RailSplineMesh settings)
        {
            if (settings.MaxSegmentLength < 0.02f || !float.IsFinite(settings.MaxSegmentLength)) throw new ArgumentException("分割間隔は0.02m以上にしてください。");
            Vector3 scale = settings.SourceScale;
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f) throw new ArgumentException("素材Scaleは各軸とも正の値にしてください。");
            var source = RailMeshBuilder.Read(settings.SourceMesh, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(settings.SourceRotation), scale));
            var pieces = RailMeshBuilder.Subdivide(source, settings.MaxSegmentLength);
            var table = new RailDistanceTable(settings.Spline);
            float start = settings.StartDistance;
            float end = settings.EndDistance == 0f ? table.Length : settings.EndDistance;
            if (!float.IsFinite(start) || !float.IsFinite(end) || start < 0f || end > table.Length + 0.001f || end - start < 0.001f)
                throw new ArgumentException($"生成区間は0〜{table.Length:F3}mの範囲で、開始 < 終了にしてください。");
            end = Mathf.Min(end, table.Length);
            float length = source.Bounds.size.z;
            int tiles = Mathf.CeilToInt((end - start) / length);
            if (tiles > 8192) throw new ArgumentException("素材の反復数が多すぎます。素材Scaleまたは生成区間を見直してください。");
            var output = new List<RailMeshBuilder.Vertex>[pieces.Length];
            for (int sub = 0; sub < output.Length; sub++) output[sub] = new List<RailMeshBuilder.Vertex>();
            int count = 0;
            for (int tile = 0; tile < tiles; tile++)
            {
                float tileStart = start + tile * length;
                float available = Mathf.Min(length, end - tileStart);
                for (int sub = 0; sub < pieces.Length; sub++)
                {
                    for (int i = 0; i < pieces[sub].Count; i += 3)
                    {
                        var triangle = new List<RailMeshBuilder.Vertex> { pieces[sub][i], pieces[sub][i + 1], pieces[sub][i + 2] };
                        var polygon = available < length - 1e-6f ? RailMeshBuilder.Clip(triangle, source.Bounds.min.z + available, false) : triangle;
                        if(settings.SuppressSleeperMeters>0 && tileStart<start+settings.SuppressSleeperMeters &&
                            polygon.All(v=>v.Position.y<source.Bounds.min.y+source.Bounds.size.y*.32f))continue;
                        for (int p = 1; p + 1 < polygon.Count; p++)
                        {
                            if (Vector3.Cross(polygon[p].Position - polygon[0].Position, polygon[p + 1].Position - polygon[0].Position).sqrMagnitude < 1e-16f) continue;
                            Add(polygon[0]); Add(polygon[p]); Add(polygon[p + 1]);
                        }
                    }
                    void Add(RailMeshBuilder.Vertex vertex)
                    {
                        if (++count > RailMeshBuilder.VertexLimit) throw new ArgumentException("生成頂点数が60万を超えます。区間を分けるか分割間隔を広げてください。");
                        vertex.Position.z = Mathf.Clamp(tileStart + vertex.Position.z - source.Bounds.min.z, start, end);
                        output[sub].Add(vertex);
                    }
                }
            }
            var cache = new Dictionary<float, FrameData>();
            Matrix4x4 toLocal = settings.transform.worldToLocalMatrix;
            Vector3 center = source.Bounds.center;
            Vector2 offset = settings.CrossSectionOffset;
            return RailMeshBuilder.MakeMesh(source, output, vertex =>
            {
                float distance = vertex.Position.z;
                if (!cache.TryGetValue(distance, out var frame))
                {
                    table.Frame(distance, out frame.Position, out frame.Rotation);
                    // レール外の空中軌道を終端の法線へ混ぜない。
                    float before = Mathf.Max(start, distance - 0.002f);
                    float after = Mathf.Min(end, distance + 0.002f);
                    table.Frame(before, out frame.BeforePosition, out frame.BeforeRotation);
                    table.Frame(after, out frame.AfterPosition, out frame.AfterRotation);
                    frame.Delta = after - before;
                    cache.Add(distance, frame);
                }
                var cross = new Vector3(vertex.Position.x - center.x + offset.x, vertex.Position.y - center.y + offset.y, 0f);
                Vector3 along = (frame.AfterPosition + frame.AfterRotation * cross - frame.BeforePosition - frame.BeforeRotation * cross) / frame.Delta;
                var jacobian = Matrix4x4.identity;
                jacobian.SetColumn(0, frame.Rotation * Vector3.right);
                jacobian.SetColumn(1, frame.Rotation * Vector3.up);
                jacobian.SetColumn(2, along);
                if (jacobian.determinant < 0.001f) throw new ArgumentException($"カーブが線路幅に対して急すぎて内側が潰れます。distance={distance:F5}, range={start:F5}..{end:F5}, determinant={jacobian.determinant:F5}, position={frame.Position:F4}");
                var localJacobian = toLocal * jacobian;
                vertex.Position = toLocal.MultiplyPoint3x4(frame.Position + frame.Rotation * cross);
                vertex.Normal = localJacobian.inverse.transpose.MultiplyVector(vertex.Normal).normalized;
                Vector3 tangent = localJacobian.MultiplyVector((Vector3)vertex.Tangent);
                tangent = (tangent - vertex.Normal * Vector3.Dot(tangent, vertex.Normal)).normalized;
                vertex.Tangent = new Vector4(tangent.x, tangent.y, tangent.z, vertex.Tangent.w * Mathf.Sign(localJacobian.determinant));
                return vertex;
            });
        }

        private struct FrameData
        {
            public Vector3 Position, BeforePosition, AfterPosition;
            public Quaternion Rotation, BeforeRotation, AfterRotation;
            public float Delta;
        }

        internal static void Generate(RailSplineMesh settings, bool withUndo = true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("編集モードで生成してください。");
            if (EditorUtility.IsPersistent(settings)) throw new InvalidOperationException("PrefabをシーンまたはPrefab Modeで開いてから生成してください。");
            GameObject oldRoot = settings.GeneratedRoot;
            if (oldRoot != null && oldRoot.transform.parent != settings.transform) throw new InvalidOperationException("生成物が別の場所へ移動しています。元の親へ戻してから再生成してください。");
            Mesh mesh = Build(settings); // 成功するまで既存生成物へ触れない。
            SaveBuiltMesh(settings, mesh, withUndo);
        }

        internal static void SaveBuiltMesh(RailSplineMesh settings, Mesh mesh, bool withUndo = true)
        {
            GameObject oldRoot = settings.GeneratedRoot;
            if (oldRoot != null && oldRoot.transform.parent != settings.transform)
                throw new InvalidOperationException("生成物が別の親にあります。元の親に戻してください。");
            string path = null;
            GameObject root = null;
            try
            {
                EnsureFolder(OutputFolder);
                path = AssetDatabase.GenerateUniqueAssetPath(OutputFolder + "/RailMesh.asset");
                AssetDatabase.CreateAsset(mesh, path);
                root = new GameObject("Generated Rail");
                root.transform.SetParent(settings.transform, false);
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterials = settings.Materials;
                if (withUndo)
                {
                    Undo.IncrementCurrentGroup();
                    int group = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Generate spline rail");
                    Undo.RegisterCreatedObjectUndo(root, "Generate spline rail");
                    // 旧rootの破棄で参照がnullになる前にスナップショットを確定する。
                    Undo.RegisterCompleteObjectUndo(settings, "Generate spline rail");
                    if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot);
                    settings.SetGeneratedRoot(root);
                    Undo.CollapseUndoOperations(group);
                }
                else
                {
                    if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot);
                    settings.SetGeneratedRoot(root);
                }
                EditorUtility.SetDirty(settings);
                PrefabUtility.RecordPrefabInstancePropertyModifications(settings);
                AssetDatabase.SaveAssetIfDirty(mesh);
            }
            catch
            {
                if (root != null && settings.GeneratedRoot != root) UnityEngine.Object.DestroyImmediate(root);
                if (settings.GeneratedRoot != root)
                {
                    if (path != null) AssetDatabase.DeleteAsset(path);
                    else UnityEngine.Object.DestroyImmediate(mesh);
                }
                throw;
            }
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }

    [CustomEditor(typeof(RailSplineMesh))]
    public sealed class RailSplineMeshEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("同じ走行用Splineの先頭1本から線路を生成します。変更後は再生成してください。開始・終了は実距離(m)。囲い・Colliderは生成しません。", MessageType.Info);
            EditorGUILayout.HelpBox("生成メッシュは担当フォルダへ保存します。Undo用に以前のメッシュ資産も残ります。", MessageType.None);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("線路を再生成・保存"))
                {
                    try { RailSplineMeshGenerator.Generate((RailSplineMesh)target); }
                    catch (Exception exception) { Debug.LogException(exception, target); EditorUtility.DisplayDialog("線路を生成できません", exception.Message, "OK"); }
                }
                if (GUILayout.Button("生成した線路を選択") && ((RailSplineMesh)target).GeneratedRoot != null)
                    Selection.activeGameObject = ((RailSplineMesh)target).GeneratedRoot;
            }
        }
    }
}
