using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using System.Linq;
namespace YasudaHaruto.RollerCoaster.Editor
{
    /// <summary>走行中の見通しを調整するライトと暗転設定を配置する。</summary>
    internal static class RollerCoasterVisibilitySetup
    {

        internal static void Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/YasudaHaruto/Scenes/RollerCoaster.unity")
                throw new InvalidOperationException("RollerCoaster Sceneを開いてください。");
            Directory.CreateDirectory("Assets/YasudaHaruto/Backups~");
            string backup = "Assets/YasudaHaruto/Backups~/RollerCoaster-before-visibility-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
            if (!EditorSceneManager.SaveScene(scene, backup, true))
                throw new IOException("Scene backup failed");
            var course = UnityEngine.Object.FindFirstObjectByType<RollerCoasterCourse>();
            var ride = UnityEngine.Object.FindFirstObjectByType<RollerCoasterTrialRide>();
            var lighting = ride.GetComponent<RollerCoasterDuctLighting>();
            var data = new SerializedObject(lighting);
            data.FindProperty("m_darkFog").floatValue = .028f;
            data.FindProperty("m_darkAmbient").colorValue = new Color(.065f, .075f, .09f);
            data.ApplyModifiedProperties();
            var rideData = new SerializedObject(ride);
            var lamp = rideData.FindProperty("m_trialLight").objectReferenceValue as Light;
            Undo.RecordObject(lamp, "Extend visible distance");
            lamp.range = 28;
            lamp.intensity = 10;
            EditorUtility.SetDirty(lamp);
            var courseData = new SerializedObject(course);
            courseData.FindProperty("m_launchRailExtension").floatValue = 2.5f;
            courseData.ApplyModifiedProperties();
            if (!course.HasExtendedLaunch)
            {
                int split = course.FallStartKnot;
                var actors = course.GetComponentsInChildren<RollerCoasterEncounter>(true);
                var anchors = actors.Select(actor => new Vector3(
                    SplineUtility.ConvertIndexUnit(course.Spline.Spline, actor.Begin, PathIndexUnit.Normalized, PathIndexUnit.Knot),
                    SplineUtility.ConvertIndexUnit(course.Spline.Spline, actor.Peak, PathIndexUnit.Normalized, PathIndexUnit.Knot),
                    SplineUtility.ConvertIndexUnit(course.Spline.Spline, actor.End, PathIndexUnit.Normalized, PathIndexUnit.Knot))).ToArray();
                Vector3 shiftWorld = course.Spline.transform.TransformVector((Quaternion)course.Spline.Spline[split].Rotation * Vector3.forward * course.LaunchRailExtension);
                void Extend(SplineContainer container)
                {
                    var spline = container.Spline;
                    var original = spline[split];
                    Vector3 delta = (Quaternion)original.Rotation * Vector3.forward * course.LaunchRailExtension;
                    for (int i = split + 1; i < spline.Count; i++)
                    {
                        var knot = spline[i];
                        knot.Position += (Unity.Mathematics.float3)delta;
                        spline.SetKnot(i, knot);
                    }
                    var launch = original;
                    launch.Position += (Unity.Mathematics.float3)delta;
                    launch.TangentIn = new Vector3(0, 0, -course.LaunchRailExtension / 3f);
                    original.TangentOut = new Vector3(0, 0, course.LaunchRailExtension / 3f);
                    spline.SetKnot(split, original);
                    spline.Insert(split + 1, launch, TangentMode.Broken);
                    EditorUtility.SetDirty(container);
                }
                Extend(course.Spline);
                foreach (var baseline in actors.Select(x => x.OriginalRoute).Where(x => x != null && x != course.Spline).Distinct())
                    Extend(baseline);
                course.SetGeneratedReferences(course.Spline, course.BeforeFall, course.AfterFall, course.CrestKnot, course.SpiralStartKnot, course.SpiralEndKnot,
                    split, course.FallEndKnot + 1, course.HillStartKnot + 1, course.ReturnSectionKnots.Select(x => x + 1).ToArray());
                course.SetLaunchLayoutFlag(true);
                for (int i = 0; i < actors.Length; i++)
                {
                    var actor = actors[i];
                    actor.OncomingRoute.transform.position += shiftWorld;
                    actor.OncomingCart.position += shiftWorld;
                    var config = new SerializedObject(actor);
                    float Parameter(float knot) => SplineUtility.ConvertIndexUnit(course.Spline.Spline, knot + 1, PathIndexUnit.Knot, PathIndexUnit.Normalized);
                    config.FindProperty("m_begin").floatValue = Parameter(anchors[i].x);
                    config.FindProperty("m_peak").floatValue = Parameter(anchors[i].y);
                    config.FindProperty("m_end").floatValue = Parameter(anchors[i].z);
                    config.FindProperty("m_roomEntry").floatValue = actor.RoomEntry + course.LaunchRailExtension;
                    config.FindProperty("m_roomExit").floatValue = actor.RoomExit + course.LaunchRailExtension;
                    config.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(course);
            }
            var table = new RailDistanceTable(course.Spline);
            float wall = table.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline, course.FallStartKnot));
            float railEnd = Mathf.Min(wall + course.LaunchRailExtension, course.AfterFall.StartDistance - 2f);
            RollerCoasterCourseBuilder.SetRange(course.BeforeFall, 0, railEnd);
            RailSplineMeshGenerator.Generate(course.BeforeFall);
            float landing = table.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline, course.FallEndKnot));
            RollerCoasterCourseBuilder.SetRange(course.AfterFall, landing, 0);
            RailSplineMeshGenerator.Generate(course.AfterFall);
            foreach (var actor in course.GetComponentsInChildren<RollerCoasterEncounter>(true))
            {
                var config = new SerializedObject(actor);
                config.FindProperty("m_branchRenderers").GetArrayElementAtIndex(1).objectReferenceValue = course.AfterFall.GeneratedRoot.GetComponent<Renderer>();
                config.ApplyModifiedPropertiesWithoutUndo();
            }
            RollerCoasterEnclosureBuilder.Generate(course.GetComponentInChildren<RollerCoasterEnclosures>());
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Visibility updated: light={lamp.range}m, fog=.028, rail extends {railEnd - wall:F2}m beyond wall. Scene copy: {backup}");
            RollerCoasterRideTuning.StartValidation();
        }
    }
}
