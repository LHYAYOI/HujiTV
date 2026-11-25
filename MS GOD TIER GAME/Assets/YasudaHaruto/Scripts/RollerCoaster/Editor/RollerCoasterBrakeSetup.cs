using System;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using System.Linq;

namespace YasudaHaruto.RollerCoaster.Editor
{
    /// <summary>レール終端とその先の空間が視界に入る位置を、現在の囲いと視点から求める。</summary>
    internal static class RollerCoasterBrakeSetup
    {

        internal static void Apply(bool staged = false, bool validate = true)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/YasudaHaruto/Scenes/RollerCoaster.unity")
                throw new InvalidOperationException("Dedicated scene required");
            Directory.CreateDirectory("Assets/YasudaHaruto/Backups~");
            EditorSceneManager.SaveScene(scene, "Assets/YasudaHaruto/Backups~/RollerCoaster-before-brake-inertia-scene-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
            var ride = UnityEngine.Object.FindFirstObjectByType<RollerCoasterTrialRide>();
            var course = ride.Course;
            var camera = ride.GetComponent<RollerCoasterCameraMotion>().VirtualCamera;
            float distance = (course.FallLaunchParameter - RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline, course.SpiralEndKnot)) * course.Spline.CalculateLength();
            var data = new SerializedObject(ride.GetComponent<RollerCoasterSpeedProfile>());
            data.FindProperty("m_dropBrakeDistance").floatValue = distance;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (staged)
            {
                data.Update();
                data.FindProperty("m_dropCreepSpeed").floatValue = .35f;
                data.FindProperty("m_dropAnticipationSeconds").floatValue = 1.65f;
                data.FindProperty("m_dropEdgePauseSeconds").floatValue = .08f;
                data.FindProperty("m_dropTipHoldSeconds").floatValue = .35f;
                data.FindProperty("m_dropGravity").floatValue = 9.81f;
                data.FindProperty("m_dropTerminalSpeed").floatValue = 28f;
                data.ApplyModifiedPropertiesWithoutUndo();
                var rideData = new SerializedObject(ride);
                rideData.FindProperty("m_fallPitch").floatValue = 35f;
                rideData.ApplyModifiedPropertiesWithoutUndo();
                const string materialPath = "Assets/YasudaHaruto/Data/RollerCoaster/Brake Sparks.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("YasudaHaruto/RollerCoaster/BrakeSparks"));
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                var sparks = ride.GetComponent<RollerCoasterBrakeSparks>();
                if (sparks == null)
                    sparks = ride.gameObject.AddComponent<RollerCoasterBrakeSparks>();
                sparks.Configure(material);
                EditorUtility.SetDirty(sparks);
                AssetDatabase.SaveAssets();
            }
            EditorUtility.SetDirty(data.targetObject);
            EditorUtility.SetDirty(ride.GetComponent<RollerCoasterCameraMotion>());
            EditorSceneManager.SaveScene(scene);
            if (validate)
            {
                RollerCoasterRideTuning.StartValidation();
                File.AppendAllText("Temp/RollerCoasterRideTuning/ride-tuning-result.txt", $"Brake cue baked at {distance:F2}m before rail end.\n");
            }
        }


        internal static void ApplySlip()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/YasudaHaruto/Scenes/RollerCoaster.unity")
                throw new InvalidOperationException("Dedicated scene required");
            EditorSceneManager.SaveScene(scene, "Assets/YasudaHaruto/Backups~/RollerCoaster-before-slip-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
            var course = UnityEngine.Object.FindFirstObjectByType<RollerCoasterCourse>();
            var actors = course.GetComponentsInChildren<RollerCoasterEncounter>(true);
            var oldTable = new RailDistanceTable(course.Spline);
            float Knot(float parameter) => SplineUtility.ConvertIndexUnit(course.Spline.Spline, parameter, PathIndexUnit.Normalized, PathIndexUnit.Knot);
            var anchors = actors.Select(a => new[] { Knot(a.Begin), Knot(a.Peak), Knot(a.End), Knot(oldTable.Parameter(a.RoomEntry)), Knot(oldTable.Parameter(a.RoomExit)) }).ToArray();
            foreach (var container in actors.Select(a => a.OriginalRoute).Append(course.Spline).Where(s => s != null).Distinct())
            {
                var spline = container.Spline;
                int launch = course.FallStartKnot + 1;
                var start = spline[launch];
                Vector3 origin = start.Position;
                Vector3 horizontal = Vector3.ProjectOnPlane((Vector3)spline[course.FallEndKnot].Position - origin, Vector3.up);
                start.TangentOut = ((Vector3)start.TangentOut).normalized * .12f;
                spline.SetKnot(launch, start);
                for (int i = launch + 1; i < course.FallEndKnot; i++)
                {
                    var knot = spline[i];
                    Vector3 position = knot.Position;
                    Vector3 flat = origin + horizontal * (i == launch + 1 ? .0875f : .2f);
                    position.x = flat.x;
                    position.z = flat.z;
                    knot.Position = position;
                    Vector3 down = (Vector3.down + horizontal.normalized * .03f).normalized;
                    knot.TangentIn = Quaternion.Inverse(knot.Rotation) * (-down * (i == launch + 1 ? 2f : 4f));
                    knot.TangentOut = Quaternion.Inverse(knot.Rotation) * (down * (i == launch + 1 ? 4f : 2f));
                    spline.SetKnot(i, knot);
                }
                EditorUtility.SetDirty(container);
            }
            var table = new RailDistanceTable(course.Spline);
            float Parameter(float knot) => SplineUtility.ConvertIndexUnit(course.Spline.Spline, knot, PathIndexUnit.Knot, PathIndexUnit.Normalized);
            RollerCoasterCourseBuilder.SetRange(course.BeforeFall, 0, table.DistanceAtParameter(Parameter(course.FallStartKnot + 1)));
            RollerCoasterCourseBuilder.SetRange(course.AfterFall, table.DistanceAtParameter(Parameter(course.FallEndKnot)), 0);
            RailSplineMeshGenerator.Generate(course.BeforeFall);
            RailSplineMeshGenerator.Generate(course.AfterFall);
            for (int i = 0; i < actors.Length; i++)
            {
                var data = new SerializedObject(actors[i]);
                data.FindProperty("m_begin").floatValue = Parameter(anchors[i][0]);
                data.FindProperty("m_peak").floatValue = Parameter(anchors[i][1]);
                data.FindProperty("m_end").floatValue = Parameter(anchors[i][2]);
                data.FindProperty("m_roomEntry").floatValue = table.DistanceAtParameter(Parameter(anchors[i][3]));
                data.FindProperty("m_roomExit").floatValue = table.DistanceAtParameter(Parameter(anchors[i][4]));
                data.FindProperty("m_branchRenderers").GetArrayElementAtIndex(1).objectReferenceValue = course.AfterFall.GeneratedRoot.GetComponent<Renderer>();
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            RollerCoasterEnclosureBuilder.Generate(course.GetComponentInChildren<RollerCoasterEnclosures>());
            var motion = UnityEngine.Object.FindFirstObjectByType<RollerCoasterCameraMotion>();
            var cameraData = new SerializedObject(motion);
            cameraData.FindProperty("m_floatHeight").floatValue = .10f;
            cameraData.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Apply(true);
        }


        internal static void ApplyContinuousLean(bool validate = true)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/YasudaHaruto/Scenes/RollerCoaster.unity")
                throw new InvalidOperationException("Dedicated scene required");
            EditorSceneManager.SaveScene(scene, "Assets/YasudaHaruto/Backups~/RollerCoaster-before-landing-rails-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
            var course = UnityEngine.Object.FindFirstObjectByType<RollerCoasterCourse>();
            var table = new RailDistanceTable(course.Spline);
            float landing = table.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline, course.FallEndKnot));
            table.Frame(landing, out var position, out var rotation);
            var holder = course.transform.Find("Landing Rail Extension");
            if (holder == null)
            {
                var go = new GameObject("Landing Rail Extension");
                holder = go.transform;
                holder.SetParent(course.transform, false);
            }
            var container = holder.GetComponent<SplineContainer>();
            if (container == null)
                container = holder.gameObject.AddComponent<SplineContainer>();
            var spline = new Spline();
            Quaternion localRotation = Quaternion.Inverse(holder.rotation) * rotation;
            for (int i = 0; i < 2; i++)
            {
                Vector3 point = holder.InverseTransformPoint(position + rotation * Vector3.forward * (i == 0 ? -6f : 0f));
                spline.Add(new BezierKnot(point, new Vector3(0, 0, -2), new Vector3(0, 0, 2), localRotation), TangentMode.Broken);
            }
            container.Spline = spline;
            EditorUtility.SetDirty(container);
            var rail = holder.GetComponent<RailSplineMesh>();
            if (rail == null)
                rail = holder.gameObject.AddComponent<RailSplineMesh>();
            rail.Configure(container, course.AfterFall.SourceMesh, course.AfterFall.Materials);
            var data = new SerializedObject(rail);
            data.FindProperty("m_sourceRotation").vector3Value = course.AfterFall.SourceRotation;
            data.FindProperty("m_sourceScale").vector3Value = course.AfterFall.SourceScale;
            data.FindProperty("m_crossSectionOffset").vector2Value = course.AfterFall.CrossSectionOffset;
            data.FindProperty("m_maxSegmentLength").floatValue = course.AfterFall.MaxSegmentLength;
            data.ApplyModifiedPropertiesWithoutUndo();
            RailSplineMeshGenerator.Generate(rail);
            EditorUtility.SetDirty(rail);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            if (validate)
            {
                RollerCoasterRideTuning.StartValidation();
                File.AppendAllText("Temp/RollerCoasterRideTuning/ride-tuning-result.txt", "Held brake lean into fall; landing receiver rails extended 6m backward.\n");
            }
        }


        internal static float FindRevealDistance(RollerCoasterCourse course, Vector3 eyeOffset, float fov)
        {
            var enclosure = course.GetComponentInChildren<RollerCoasterEnclosures>();
            var filter = enclosure.GeneratedRoot.GetComponent<MeshFilter>();
            // 検出用Colliderは編集時だけ作成し、Sceneには保存しない。
            var query = new GameObject("Temporary drop visibility query") { hideFlags = HideFlags.HideAndDontSave };
            bool backfaces = Physics.queriesHitBackfaces;
            try
            {
                query.transform.SetParent(filter.transform, false);
                var collider = query.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                Physics.queriesHitBackfaces = true;
                Physics.SyncTransforms();
                var table = new RailDistanceTable(course.Spline);
                float end = course.BeforeFall.EndDistance;
                table.Frame(end, out var tip, out var tipRotation);
                // 両レール先端だけでなく、その向こうに床がないことも見えて初めて合図にする。
                var targets = new[]{tip+tipRotation*new Vector3(-.7f,.08f,0),tip+tipRotation*new Vector3(.7f,.08f,0),
                    tip+tipRotation*new Vector3(0,-.65f,.6f)};
                float tangent = Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
                for (float distance = 20f; distance >= 2f; distance -= .1f)
                {
                    table.Frame(end - distance, out var position, out var rotation);
                    Vector3 eye = position + rotation * eyeOffset;
                    bool visible = true;
                    foreach (var target in targets)
                    {
                        Vector3 view = Quaternion.Inverse(rotation) * (target - eye);
                        if (view.z <= 0 || Mathf.Abs(view.y) > view.z * tangent * .65f || Mathf.Abs(view.x) > view.z * tangent * (16f / 9f) * .65f)
                        {
                            visible = false;
                            break;
                        }
                        Vector3 direction = target - eye;
                        if (collider.Raycast(new Ray(eye, direction.normalized), out _, direction.magnitude - .04f))
                        {
                            visible = false;
                            break;
                        }
                    }
                    if (visible)
                    {
                        Vector3 left = Quaternion.Inverse(rotation) * (targets[0] - eye);
                        Vector3 right = Quaternion.Inverse(rotation) * (targets[1] - eye);
                        float screenWidth = Mathf.Abs(right.x / right.z - left.x / left.z) / (2f * tangent * (16f / 9f));
                        // 960px幅なら両レールの間隔が約62px以上。小さな点の可視化と認識可能な断線を区別する。
                        if (screenWidth >= .065f)
                            return distance;
                    }
                }
                throw new InvalidOperationException("No safe visible braking cue found before the rail end");
            }
            finally { Physics.queriesHitBackfaces = backfaces; UnityEngine.Object.DestroyImmediate(query); }
        }
    }
}
