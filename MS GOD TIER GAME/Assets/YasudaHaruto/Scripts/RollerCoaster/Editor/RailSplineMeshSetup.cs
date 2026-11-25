using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace YasudaHaruto.RollerCoaster.Editor
{
    /// <summary>レール素材の生成設定を担当側のコースへ配置するEditor補助。</summary>
    public static class RailSplineMeshSetup
    {
        internal const string SourcePath = "Assets/YasudaHaruto/Data/RollerCoaster/RailStraight.asset";
        internal const string SamplePath = "Assets/YasudaHaruto/Prefabs/RollerCoaster/RailSplineSample.prefab";
        internal const string ModelPath = "Assets/map/bunki/duct_lane.fbx";


        [MenuItem("Tools/Roller Coaster/Create Rail Sample")]
        public static void CreateSample()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SamplePath);
            if (prefab == null)
                prefab = SaveSample();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Create rail sample");
            Selection.activeGameObject = instance;
            SceneView.lastActiveSceneView?.FrameSelected();
        }


        internal static GameObject SaveSample()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
                throw new InvalidOperationException("線路FBXが見つかりません: " + ModelPath);
            MeshFilter filter = model.GetComponentsInChildren<MeshFilter>(true).Single(x => x.name == "rail_straight");
            var renderer = filter.GetComponent<MeshRenderer>();
            Mesh sourceMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SourcePath);
            if (sourceMesh == null)
            {
                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                // FBX内の素材展示位置を除く。形状に必要な回転とScaleは維持する。
                matrix.m03 = 0f;
                matrix.m13 = 0f;
                matrix.m23 = 0f;
                var source = RailMeshBuilder.Read(filter.sharedMesh, matrix);
                var triangles = new System.Collections.Generic.List<RailMeshBuilder.Vertex>[source.Indices.Length];
                for (int sub = 0; sub < triangles.Length; sub++)
                    triangles[sub] = source.Indices[sub].Select(i => source.Vertices[i]).ToList();
                sourceMesh = RailMeshBuilder.MakeMesh(source, triangles, vertex => vertex);
                sourceMesh.name = "RailStraight";
                RailSplineMeshGenerator.EnsureFolder("Assets/YasudaHaruto/Data/RollerCoaster");
                AssetDatabase.CreateAsset(sourceMesh, SourcePath);
                Debug.Log($"Rail source: bounds={sourceMesh.bounds}, imported matrix={matrix}, original vertices={filter.sharedMesh.vertexCount}, readable={filter.sharedMesh.isReadable}");
            }
            var root = new GameObject("Rail Spline Sample");
            try
            {
                var container = root.AddComponent<SplineContainer>();
                var spline = new Spline();
                Vector3[] points = { Vector3.zero, new Vector3(0, 0, 9), new Vector3(4, 0, 18), new Vector3(10, 2, 27), new Vector3(12, 5, 39), new Vector3(8, 5, 51) };
                foreach (var point in points)
                    spline.Add(new BezierKnot(point), TangentMode.AutoSmooth);
                container.Spline = spline;
                var rail = root.AddComponent<RailSplineMesh>();
                rail.Configure(container, sourceMesh, renderer.sharedMaterials);
                var serialized = new SerializedObject(rail);
                // 初期値は素材上端を経路高さへ。車体との最終調整は利用Scene側で行う。
                serialized.FindProperty("m_crossSectionOffset").vector2Value = new Vector2(0f, -sourceMesh.bounds.extents.y);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                RailSplineMeshGenerator.Generate(rail, false);
                RailSplineMeshGenerator.EnsureFolder("Assets/YasudaHaruto/Prefabs/RollerCoaster");
                return PrefabUtility.SaveAsPrefabAsset(root, SamplePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
