using System;
using System.Linq;
using UnityEditor;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace YasudaHaruto.RollerCoaster.Editor
{
    internal static class RollerCoasterTrialSetup
    {
        internal static void StrengthenVerticalShake(RollerCoasterTrialRide trial,float vertical=1.8f,float pitch=1.25f,float airborne=.025f)
        {
            var motion=AddCameraMotion(trial);
            if(motion.VirtualCamera.GetComponent<RollerCoasterVerticalNoiseExtension>()==null)
                Undo.AddComponent<RollerCoasterVerticalNoiseExtension>(motion.VirtualCamera.gameObject);
            string path="Assets/YasudaHaruto/Data/RollerCoaster/RideVerticalNoise.asset";
            var profile=AssetDatabase.LoadAssetAtPath<NoiseSettings>(path);
            var source=AssetDatabase.LoadAssetAtPath<NoiseSettings>(AssetDatabase.GUIDToAssetPath("e9ce97b7a206dc64d90931ea044e0c72"));
            bool created=profile==null;
            if(created)
            {
                profile=UnityEngine.Object.Instantiate(source);profile.name="Ride Vertical Noise";
            }
            else Undo.RecordObject(profile,"Tune vertical noise");
            profile.PositionNoise=source.PositionNoise.ToArray();profile.OrientationNoise=source.OrientationNoise.ToArray();
            for(int i=0;i<profile.PositionNoise.Length;i++)profile.PositionNoise[i].Y.Amplitude*=vertical;
            for(int i=0;i<profile.OrientationNoise.Length;i++)profile.OrientationNoise[i].X.Amplitude*=pitch;
            if(created)AssetDatabase.CreateAsset(profile,path);else EditorUtility.SetDirty(profile);
            var noise=motion.VirtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
            Undo.RecordObject(noise,"Increase vertical ride shake");noise.NoiseProfile=profile;EditorUtility.SetDirty(noise);
            Undo.RecordObject(motion,"Increase vertical landing shake");
            var data=new SerializedObject(motion);data.FindProperty("m_landingPitch").floatValue=3.5f;data.ApplyModifiedPropertiesWithoutUndo();
            data.FindProperty("m_airborneStrength").floatValue=airborne;data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(motion);
        }
        internal static RollerCoasterCameraMotion AddCameraMotion(RollerCoasterTrialRide trial)
        {
            var existing = trial.GetComponent<RollerCoasterCameraMotion>();
            if (existing != null && existing.VirtualCamera != null) return existing;
            string noisePath = AssetDatabase.GUIDToAssetPath("e9ce97b7a206dc64d90931ea044e0c72");
            var noiseSettings = AssetDatabase.LoadAssetAtPath<NoiseSettings>(noisePath);
            if (noiseSettings == null) throw new InvalidOperationException("既存トロッコと同じNoise Profileが見つかりません。");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Connect Hamada camera shake");
            var camera = trial.RideCamera;
            var rig = new GameObject("Ride Cinemachine Camera");
            rig.transform.SetParent(trial.Cart.transform, false);
            rig.transform.localPosition = camera.transform.localPosition;
            rig.transform.localRotation = camera.transform.localRotation;
            var virtualCamera = rig.AddComponent<CinemachineCamera>();
            virtualCamera.enabled = false;
            virtualCamera.Priority = 1000;
            virtualCamera.OutputChannel = OutputChannels.Channel02;
            var lens = virtualCamera.Lens;
            lens.FieldOfView = camera.fieldOfView; lens.NearClipPlane = camera.nearClipPlane; lens.FarClipPlane = camera.farClipPlane;
            virtualCamera.Lens = lens;
            var noise = rig.AddComponent<CinemachineBasicMultiChannelPerlin>();
            noise.NoiseProfile = noiseSettings;
            noise.AmplitudeGain = 0f;
            noise.FrequencyGain = 0f;
            var extension = rig.AddComponent<CinemachineRotationExtension>();
            var shake = rig.AddComponent<CameraShake>();
            var shakeData = new SerializedObject(shake);
            shakeData.FindProperty("m_multiChannelPerlin").objectReferenceValue = noise;
            shakeData.FindProperty("m_cinemachineRotationExtension").objectReferenceValue = extension;
            shakeData.FindProperty("m_amplitudeGain").floatValue = 0.18f;
            shakeData.FindProperty("m_frequencyGain").floatValue = 1.5f;
            shakeData.ApplyModifiedPropertiesWithoutUndo();
            var brain = camera.GetComponent<CinemachineBrain>();
            if (brain == null) brain = Undo.AddComponent<CinemachineBrain>(camera.gameObject);
            Undo.RecordObject(brain, "Configure ride brain");
            brain.enabled = false;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            brain.ChannelMask = OutputChannels.Channel02;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            var motion = existing != null ? existing : Undo.AddComponent<RollerCoasterCameraMotion>(trial.gameObject);
            motion.Configure(trial.Course, trial.Cart, shake, virtualCamera, brain);
            Undo.RecordObject(trial, "Connect ride camera motion");
            trial.SetCameraMotion(motion);
            Undo.RegisterCreatedObjectUndo(rig, "Create ride virtual camera");
            EditorUtility.SetDirty(trial); EditorUtility.SetDirty(motion); EditorUtility.SetDirty(brain);
            Undo.CollapseUndoOperations(group);
            return motion;
        }
        internal static RollerCoasterSpeedProfile AddSectionSpeeds(RollerCoasterTrialRide trial)
        {
            var profile = trial.GetComponent<RollerCoasterSpeedProfile>();
            if (profile == null) profile = Undo.AddComponent<RollerCoasterSpeedProfile>(trial.gameObject);
            Undo.RecordObject(trial, "Connect section speeds");
            trial.SetSpeedProfile(profile);
            EditorUtility.SetDirty(trial);
            return profile;
        }
        internal static RollerCoasterTrialRide Create(RollerCoasterCourse course)
        {
            var existing = course.gameObject.scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<RollerCoasterTrialRide>(true)).SingleOrDefault();
            if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Arai/Prefabs/Stage/TrokkoController.prefab");
            if (source == null) throw new InvalidOperationException("既存トロッコPrefabが見つかりません。");
            var root = new GameObject("Trial Ride");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, course.gameObject.scene);
            try
            {
                var vehicle = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                vehicle.name = "Trial Cart";
                vehicle.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                var cart = vehicle.GetComponent<CartController>();
                var data = new SerializedObject(cart);
                data.FindProperty("m_distance").floatValue = 0f;
                data.FindProperty("m_speed").floatValue = 12f;
                data.ApplyModifiedPropertiesWithoutUndo();
                vehicle.GetComponent<Rigidbody>().isKinematic = true;
                // 既存PrefabのCamera演出は、試走用の簡単なカメラへ影響させない。
                vehicle.transform.Find("FPSCamera").gameObject.SetActive(false);
                var body = vehicle.transform.Find("Trokko");
                body.localPosition = new Vector3(0f, 0.15f, 0.443f);
                var cameraObject = new GameObject("Trial Ride Camera");
                cameraObject.transform.SetParent(vehicle.transform, false);
                cameraObject.transform.localPosition = new Vector3(0f, 1.1f, -0.2f);
                cameraObject.transform.localRotation = Quaternion.Euler(2f, 0f, 0f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 70f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 1000f;
                camera.enabled = false;
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
                cameraObject.AddComponent<AudioListener>();
                var lightObject = new GameObject("Trial Directional Light");
                lightObject.transform.SetParent(root.transform, false);
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 2f;
                light.enabled = false;
                var trial = root.AddComponent<RollerCoasterTrialRide>();
                trial.Configure(course, cart, camera, light);
                // Sceneでも車両を入口へ表示する。Play時にStartから走行を開始する。
                course.Spline.Evaluate(0, 0f, out var position, out var tangent, out var up);
                vehicle.transform.SetPositionAndRotation(position, Quaternion.LookRotation(tangent, up));
                PrefabUtility.RecordPrefabInstancePropertyModifications(cart);
                PrefabUtility.RecordPrefabInstancePropertyModifications(vehicle.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(vehicle.GetComponent<Rigidbody>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(body);
                PrefabUtility.RecordPrefabInstancePropertyModifications(vehicle.transform.Find("FPSCamera").gameObject);
                Undo.RegisterCreatedObjectUndo(root, "Create trial ride");
                return trial;
            }
            catch { UnityEngine.Object.DestroyImmediate(root); throw; }
        }
    }
}
