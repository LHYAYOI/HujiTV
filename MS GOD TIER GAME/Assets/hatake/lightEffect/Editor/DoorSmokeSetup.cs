using UnityEditor;
using UnityEngine;

namespace LightShaftLab.Editor
{
    /// <summary>
    /// 床を這う霧・扉の煙をシーンに追加するメニュー。
    /// 床霧（LocalFogVolume の GROUND）と煙パーティクル、それをまとめる DoorSmoke を一度に作る。
    /// </summary>
    public static class DoorSmokeSetup
    {
        const string k_materialPath = "Assets/hatake/lightEffect/materials/M_SmokePuff.mat";

        [MenuItem("Tools/LightShaft/Fogを追加/床を這う霧", false, 14)]
        [MenuItem("GameObject/Light Shaft/Ground Fog", false, 12)]
        public static void CreateGroundFog()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("床を這う霧を追加");
            var go = new GameObject("Fog（床を這う霧）");
            Undo.RegisterCreatedObjectUndo(go, "床を這う霧を追加");
            // 箱の底を置き場所の床に合わせる
            go.transform.position = PlacementPosition() + Vector3.up * 0.5f;
            go.transform.localScale = new Vector3(4, 1, 12);
            var fog = Undo.AddComponent<LocalFogVolume>(go);
            ApplyGroundDefaults(fog);
            Selection.activeGameObject = go;
            Undo.CollapseUndoOperations(group);
        }

        [MenuItem("Tools/LightShaft/Fogを追加/扉の煙（床霧＋パーティクル）", false, 15)]
        [MenuItem("GameObject/Light Shaft/Door Smoke", false, 13)]
        public static void CreateDoorSmoke()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("扉の煙を追加");

            // 根：扉の足元に置き、青い軸（Z）を煙が流れ出す向きに向ける
            var root = new GameObject("DoorSmoke");
            Undo.RegisterCreatedObjectUndo(root, "扉の煙を追加");
            var selected = Selection.activeTransform;
            root.transform.position = PlacementPosition();
            if (selected && selected.gameObject.scene.IsValid())
            {
                root.transform.rotation = Quaternion.Euler(0, selected.eulerAngles.y, 0);
            }

            // 床霧：扉の前に広がる箱。広がりの中心は根
            var fogObject = new GameObject("GroundFog");
            Undo.RegisterCreatedObjectUndo(fogObject, "扉の煙を追加");
            fogObject.transform.SetParent(root.transform, false);
            fogObject.transform.localPosition = new Vector3(0, 0.5f, 4.5f);
            fogObject.transform.localScale = new Vector3(6, 1, 10);
            var fog = Undo.AddComponent<LocalFogVolume>(fogObject);
            ApplyGroundDefaults(fog);
            fog.m_spreadOrigin = root.transform;
            fog.m_spreadRadius = 7;
            fog.m_groundFlow = root.transform.forward * 0.35f;

            // 煙パーティクル：扉の隙間（床際の横長の箱）から前へ押し出す
            var puffObject = new GameObject("Puffs");
            Undo.RegisterCreatedObjectUndo(puffObject, "扉の煙を追加");
            puffObject.transform.SetParent(root.transform, false);
            var puffs = Undo.AddComponent<ParticleSystem>(puffObject);
            ConfigurePuffs(puffs);

            var smoke = Undo.AddComponent<DoorSmoke>(root);
            smoke.m_groundFog = fog;
            smoke.m_puffs = puffs;

            Selection.activeGameObject = root;
            Undo.CollapseUndoOperations(group);
        }

        [MenuItem("Tools/LightShaft/Fogを追加/床を這う霧", true)]
        [MenuItem("Tools/LightShaft/Fogを追加/扉の煙（床霧＋パーティクル）", true)]
        static bool CanCreate() => !EditorApplication.isPlayingOrWillChangePlaymode &&
            UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() == null;

        // 選択中の物体の足元、なければシーンビューの注視点
        static Vector3 PlacementPosition()
        {
            var selected = Selection.activeTransform;
            if (selected && selected.gameObject.scene.IsValid())
            {
                return selected.position;
            }
            return SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        }

        static void ApplyGroundDefaults(LocalFogVolume fog)
        {
            fog.m_shape = LocalFogVolume.SHAPE.GROUND;
            fog.m_density = 1.5f;
            fog.m_tint = new Color(0.8f, 0.87f, 1f);
            fog.m_edgeFade = 0.25f;
            fog.m_priority = 2;
            fog.m_sunBoost = 0;
            fog.m_groundFill = 0.45f;
            fog.m_groundSoftness = 0.25f;
            fog.m_groundNoiseScale = 1.4f;
            fog.m_groundNoiseAmount = 0.75f;
            fog.m_spreadEdge = 1.2f;
        }

        static void ConfigurePuffs(ParticleSystem system)
        {
            // 作った直後に再生中だと一部の設定が書き込めないので止める
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            // 量は DoorSmoke が開き具合に合わせて書き換える。シーンでの確認用に少しだけ出しておく
            DoorSmokeParticles.Configure(system, GetOrCreateMaterial(), DoorSmokeParticles.Settings.Default, 12);
        }

        public static Material GetOrCreateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_materialPath);
            if (material)
            {
                return material;
            }
            var shader = Shader.Find(DoorSmokeParticles.k_shaderName);
            if (!shader)
            {
                Debug.LogError($"シェーダー {DoorSmokeParticles.k_shaderName} が見つかりません。");
                return null;
            }
            material = new Material(shader) { name = "M_SmokePuff" };
            AssetDatabase.CreateAsset(material, k_materialPath);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
