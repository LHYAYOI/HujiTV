using TeamToon;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FieldGhost.EditorTools
{
    /// <summary>ヒエラルキーの右クリック（GameObject メニュー）から、お化けの演出を置く。</summary>
    static class FieldGhostSetup
    {
        const string MenuRoot = "GameObject/お化けの演出/";
        const string MaterialFolder = "Assets/hatake/fieldGhost/materials";
        const string MaterialPath = MaterialFolder + "/M_FieldGhostCurtain.mat";
        const string CurtainShader = "custom/hatake/FieldGhostCurtain";

        [MenuItem(MenuRoot + "白のカーテン（カーテン越しの形）", false, 10)]
        static void CreateCurtain(MenuCommand command)
        {
            // 複数選んでいると選んだ数だけ呼ばれるので、1 回だけ作る。
            if (Selection.objects.Length > 1 && command.context != Selection.activeObject)
            {
                return;
            }
            var go = new GameObject("FieldGhost Curtain", typeof(MeshFilter), typeof(MeshRenderer));
            go.AddComponent<FieldGhostCurtain>();
            go.AddComponent<FieldGhostCurtainPress>();
            var parent = command.context as GameObject;
            if (parent)
            {
                GameObjectUtility.SetParentAndAlign(go, parent);
            }
            else
            {
                PlaceAtSceneView(go.transform);
            }
            var meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = CurtainMaterial();
            meshRenderer.shadowCastingMode = ShadowCastingMode.On;
            Undo.RegisterCreatedObjectUndo(go, "お化けの演出: カーテン");
            Selection.activeObject = go;
        }

        [MenuItem(MenuRoot + "床の冷気にとける（選んだお化けを使う）", false, 11)]
        static void CreateFloorMelt()
        {
            GameObject ghost = Selection.activeGameObject;
            var go = new GameObject("FieldGhost FloorMelt");
            go.transform.position = ghost.transform.position;
            Undo.RegisterCreatedObjectUndo(go, "お化けの演出: 床の冷気にとける");
            var melt = go.AddComponent<FieldGhostFloorMelt>();
            melt.ghost = ghost.transform;
            // 道筋の点を 2 つ置く（現れる所と溶ける所）。動かして調整する。
            Vector3 forward = Vector3.ProjectOnPlane(ghost.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = Vector3.forward;
            }
            melt.path = new[]
            {
                CreatePoint("現れる所", go.transform, ghost.transform.position),
                CreatePoint("溶ける所", go.transform, ghost.transform.position + forward.normalized * 2.5f),
            };
            EnsureDeformer(ghost);
            Selection.activeObject = go;
        }

        [MenuItem(MenuRoot + "壁をすり抜ける（選んだお化けを使う）", false, 12)]
        static void CreateWallPeek()
        {
            GameObject ghost = Selection.activeGameObject;
            var go = new GameObject("FieldGhost WallPeek");
            Vector3 forward = Vector3.ProjectOnPlane(ghost.transform.forward, Vector3.up);
            go.transform.SetPositionAndRotation(ghost.transform.position, Quaternion.LookRotation(forward.sqrMagnitude > 1e-4f ? forward : Vector3.forward));
            Undo.RegisterCreatedObjectUndo(go, "お化けの演出: 壁をすり抜ける");
            go.AddComponent<FieldGhostWallPeek>().ghost = ghost.transform;
            EnsureDeformer(ghost);
            Selection.activeObject = go;
            EditorUtility.DisplayDialog("壁をすり抜ける",
                "作ったオブジェクトを壁の表面へ動かし、青の矢印（+Z）を部屋側へ向けてください。\nお化けはその位置の壁の奥から出てきます。", "OK");
        }

        [MenuItem(MenuRoot + "床の冷気にとける（選んだお化けを使う）", true)]
        [MenuItem(MenuRoot + "壁をすり抜ける（選んだお化けを使う）", true)]
        static bool HasGhostSelected() => Selection.activeGameObject && Selection.activeGameObject.scene.IsValid();

        static Transform CreatePoint(string name, Transform parent, Vector3 position)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.position = position;
            return point;
        }

        // 変形が無ければ付ける（範囲は TeamToonGhostDeformer.Reset が自動で合わせる）。
        static void EnsureDeformer(GameObject ghost)
        {
            if (ghost.GetComponentInChildren<TeamToonGhostDeformer>(true))
            {
                return;
            }
            Undo.AddComponent<TeamToonGhostDeformer>(ghost);
            Debug.Log("お化けの演出: お化けに TeamToonGhostDeformer を付けました。各マテリアルの『変形（デフォーマ）を使用』を ON にすると伸び縮みします。", ghost);
        }

        static void PlaceAtSceneView(Transform target)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view)
            {
                target.position = view.pivot;
            }
        }

        static Material CurtainMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material)
            {
                return material;
            }
            Shader shader = Shader.Find(CurtainShader);
            if (!shader)
            {
                Debug.LogError($"お化けの演出: シェーダ {CurtainShader} が見つかりません。");
                return null;
            }
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/hatake/fieldGhost", "materials");
            }
            material = new Material(shader) { name = "M_FieldGhostCurtain" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
