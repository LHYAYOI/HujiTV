using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.Rendering;

namespace YasudaHaruto.RollerCoaster.Editor
{
    public static class RollerCoasterStoryboardBuilder
    {
        internal const string Output="Assets/YasudaHaruto/Data/RollerCoaster/Storyboard";
        private sealed class ROOM
        {
            public float Entry,Exit;
            public readonly List<(SplineContainer Route,float Distance)> Branches=new List<(SplineContainer,float)>();
            public int Ceiling;
        }
        internal static void FinalizeOutput()
        {
            bool Generated(string path)=>
                (path.StartsWith(RailSplineMeshGenerator.OutputFolder+"/RailMesh",StringComparison.Ordinal)||
                 path.StartsWith(Output+"/CourseEnclosure",StringComparison.Ordinal)||path.StartsWith(Output+"/Ceiling",StringComparison.Ordinal)||
                 path.StartsWith("Assets/YasudaHaruto/Data/RollerCoaster/Enclosures/Generated/SmoothEnclosure",StringComparison.Ordinal))&&path.EndsWith(".asset",StringComparison.Ordinal);
            var paths=AssetDatabase.GetAllAssetPaths();
            var roots=paths.Where(path=>path.StartsWith("Assets/",StringComparison.Ordinal)&&!Generated(path)&&
                new[]{".unity",".prefab",".asset",".mat",".controller",".overrideController",".playable"}.Contains(Path.GetExtension(path))).ToArray();
            var protectedPaths=new HashSet<string>(AssetDatabase.GetDependencies(roots,true));
            // 非アクティブな旧構成と他Scene／Prefabの参照も含め、生成Meshだけを整理する。
            int removed=0;
            foreach(var path in paths.Where(Generated))
            {
                if(protectedPaths.Contains(path))continue;
                if(AssetDatabase.DeleteAsset(path))removed++;
            }
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetSceneByPath(RollerCoasterCourseBuilder.ScenePath));
            AssetDatabase.SaveAssets();Directory.CreateDirectory("Temp/RollerCoasterStoryboard");
            File.WriteAllText("Temp/RollerCoasterStoryboard/finalize.txt",$"Removed {removed} unreferenced generated meshes; preserved all project scene and asset dependencies.\n");
        }
        public static void BuildAndValidate()
        {
            try {Build();RollerCoasterStoryboardValidation.Begin();}
            catch(Exception error){Directory.CreateDirectory("Temp/RollerCoasterStoryboard");File.WriteAllText("Temp/RollerCoasterStoryboard/result.txt",error.ToString());Debug.LogException(error);if(Application.isBatchMode)EditorApplication.Exit(1);}
        }
        [MenuItem("Tools/Roller Coaster/Build Image Course 1-11")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("編集モードで生成してください。");
            Directory.CreateDirectory("Temp/RollerCoasterStoryboard");
            var scene=EditorSceneManager.OpenScene(RollerCoasterCourseBuilder.ScenePath);
            var course=UnityEngine.Object.FindFirstObjectByType<RollerCoasterCourse>();
            var ride=UnityEngine.Object.FindFirstObjectByType<RollerCoasterTrialRide>();
            if(course==null||ride==null)throw new InvalidOperationException("保存済みコースとTrial Rideが必要です。");
            RailSplineMeshGenerator.EnsureFolder(Output);
            foreach(var item in scene.GetRootGameObjects().Where(x=>x.name=="Cube"))item.SetActive(false);
            // 参照中の共有資産は消さず、旧Scene内オブジェクトを非表示で保持する。
            var old=course.transform.Find("Image Course 1-11");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach(var encounter in course.GetComponentsInChildren<RollerCoasterEncounter>(true))encounter.gameObject.SetActive(false);
            var enclosure=course.GetComponentInChildren<RollerCoasterEnclosures>(true);enclosure.gameObject.SetActive(false);
            foreach(var sparks in ride.GetComponentsInChildren<RollerCoasterBrakeSparks>(true))sparks.enabled=true;
            var oldProfile=ride.GetComponent<RollerCoasterSpeedProfile>();if(oldProfile!=null)oldProfile.enabled=false;
            var oldStory=ride.GetComponent<RollerCoasterStoryboard>();if(oldStory!=null)UnityEngine.Object.DestroyImmediate(oldStory);
            var root=new GameObject("Image Course 1-11");root.transform.SetParent(course.transform,false);
            var points=new List<Vector3>();var knots=new int[11];
            Vector3 Pixel(float x,float y,float h)=>new Vector3((x-64f)*.4f,h,(597f-y)*.4f);
            void P(float x,float y,float h)=>points.Add(Pixel(x,y,h));
            void M(int n,float x,float y,float h){knots[n-1]=points.Count;P(x,y,h);}
            P(64,627,0);P(64,612,0);P(64,597,0);P(64,575,7);P(64,553,18);M(1,64,540,22);
            P(64,518,14);M(2,68,480,-14);P(90,435,-20);P(150,402,-25);P(174,365,-27);
            P(152,337,-28);P(91,317,-31);P(61,287,-34);M(3,61,241,-36);
            P(83,197,-38);P(133,176,-39);P(180,196,-42);P(196,238,-43);P(192,293,-44);P(210,340,-44.5f);
            M(4,250,350,-44);P(292,360,-46);P(345,388,-50);P(382,400,-51);M(5,425,400,-49);
            P(468,372,-44);P(510,349,-43);M(6,550,338,-45);P(600,334,-47);M(7,650,329,-44);
            P(710,324,-41);M(8,777,321,-40);P(807,320,-40);P(832,338,-41.5f);P(839,366,-44);P(847,415,-49);
            P(865,444,-51);P(899,455,-50);M(9,940,446,-48);P(986,428,-44);M(10,1058,399,-43);
            P(1100,379,-45);P(1134,341,-43);P(1155,293,-39);M(11,1172,240,-38);
            Vector3 edge=points.Last();Vector3 forward=(edge-points[points.Count-2]);forward.y=0;forward.Normalize();
            points.Add(edge+forward*.8f-Vector3.up*8f);points.Add(edge+forward*2.5f-Vector3.up*28f);
            int landing=points.Count;points.Add(edge+forward*5f-Vector3.up*36f);
            for(int i=1;i<=6;i++)points.Add(edge+forward*(5f+i*3.2f)+Vector3.up*(-36f+3f*Mathf.SmoothStep(0,1,i/6f)));
            var spline=new Spline();foreach(var point in points)spline.Add(new BezierKnot(course.Spline.transform.InverseTransformPoint(point)),TangentMode.AutoSmooth);
            // 頂点は水平、落下の離陸は水平、着地以後は前方。鉛直接線の方位反転は乗車側で避ける。
            void Direction(int index,Vector3 direction,float incoming,float outgoing)
            {
                spline.SetTangentMode(index,TangentMode.Broken);var k=spline[index];k.Rotation=Quaternion.LookRotation(direction,Vector3.up);
                k.TangentIn=new Vector3(0,0,-incoming);k.TangentOut=new Vector3(0,0,outgoing);spline.SetKnot(index,k);
            }
            Direction(0,Vector3.forward,1,2);Direction(knots[0],Vector3.forward,2,2);Direction(knots[10],forward,4,.2f);Direction(landing,forward,.3f,1.2f);
            course.Spline.Spline=spline;
            for(int i=1;i<spline.Count-1;i++)
            {
                var k=spline[i];spline.SetTangentMode(i,TangentMode.Broken);Quaternion oldRotation=k.Rotation;
                Vector3 before=points[i]-points[i-1],after=points[i+1]-points[i];
                float turn=Vector3.SignedAngle(Vector3.ProjectOnPlane(before,Vector3.up),Vector3.ProjectOnPlane(after,Vector3.up),Vector3.up);
                float bank=0;
                if(i>knots[1]&&i<knots[3])bank=-Mathf.Clamp(turn/25f,-1,1)*48f*Mathf.Clamp01((knots[3]-i)/2f);
                else if(i>knots[3]&&i<knots[10]-1)bank=-Mathf.Clamp(turn/25f,-1,1)*12f;
                if(new[]{knots[3],knots[4],knots[7],knots[8],knots[9]}.Any(x=>Mathf.Abs(i-x)<=1))bank=0;
                Quaternion rotation=oldRotation*Quaternion.Euler(0,0,bank);
                k.TangentIn=Quaternion.Inverse(rotation)*(oldRotation*(Vector3)k.TangentIn);k.TangentOut=Quaternion.Inverse(rotation)*(oldRotation*(Vector3)k.TangentOut);
                k.Rotation=rotation;spline.SetKnot(i,k);
            }
            course.SetGeneratedReferences(course.Spline,course.BeforeFall,course.AfterFall,knots[0],knots[1],knots[3],knots[10],landing,landing+1,Enumerable.Range(0,6).Select(i=>landing+i).ToArray());
            course.SetLaunchLayoutFlag(false);
            var table=new RailDistanceTable(course.Spline);float[] markers=knots.Select(x=>RollerCoasterCourseBuilder.KnotParameter(spline,x)).ToArray();
            float fallEnd=RollerCoasterCourseBuilder.KnotParameter(spline,landing);
            RollerCoasterCourseBuilder.SetRange(course.BeforeFall,0,table.DistanceAtParameter(markers[10]));
            RollerCoasterCourseBuilder.SetRange(course.AfterFall,table.DistanceAtParameter(fallEnd),0);
            RailSplineMeshGenerator.Generate(course.BeforeFall);RailSplineMeshGenerator.Generate(course.AfterFall);
            var actors=new List<RollerCoasterStoryboard.ACTOR>();var rooms=new List<ROOM>();
            SplineContainer Route(string name,IEnumerable<Vector3> positions)
            {
                var go=new GameObject(name);go.transform.SetParent(root.transform,false);var container=go.AddComponent<SplineContainer>();var path=new Spline();
                foreach(var p in positions)path.Add(new BezierKnot(go.transform.InverseTransformPoint(p)),TangentMode.AutoSmooth);container.Spline=path;
                var rail=go.AddComponent<RailSplineMesh>();rail.Configure(container,course.BeforeFall.SourceMesh,course.BeforeFall.Materials);
                var data=new SerializedObject(rail);data.FindProperty("m_crossSectionOffset").vector2Value=course.BeforeFall.CrossSectionOffset;data.ApplyModifiedPropertiesWithoutUndo();
                RailSplineMeshGenerator.Generate(rail);return container;
            }
            void Actor(int section,RollerCoasterStoryboard.ACTOR_KIND kind,SplineContainer route,float span)
            {
                var cart=new GameObject($"{section:00} {kind} Cart");cart.transform.SetParent(root.transform,false);RollerCoasterEncounterSetup.BuildCart(cart.transform);cart.SetActive(false);
                actors.Add(new RollerCoasterStoryboard.ACTOR{Section=section,Kind=kind,Route=route,Cart=cart.transform,Meeting=markers[section-1],Span=span});
            }
            foreach(int section in new[]{4,5,8})
            {
                float distance=table.DistanceAtParameter(markers[section-1]);table.Frame(distance,out var p,out var rotation);Vector3 heading=Vector3.ProjectOnPlane(rotation*Vector3.forward,Vector3.up).normalized;
                if(section==8)
                {
                    table.Frame(distance+8f,out p,out rotation);
                    heading=Vector3.ProjectOnPlane(rotation*Vector3.forward,Vector3.up).normalized;
                }
                Vector3 side=Vector3.Cross(Vector3.up,heading)*(section==4?-1f:1f);
                var route=Route($"{section:00} Apparent Forward Route",section==8?new[]{p,p+heading*12,p+heading*28,p+heading*40}:
                    new[]{p,p+heading*5+side*3,p+heading*12+side*10,p+heading*15+side*24});
                if(section!=8)
                {
                    var knot=route.Spline[0];route.Spline.SetTangentMode(0,TangentMode.Broken);knot.Rotation=rotation;
                    knot.TangentIn=new Vector3(0,0,-3);knot.TangentOut=new Vector3(0,0,3);route.Spline.SetKnot(0,knot);
                    RailSplineMeshGenerator.Generate(route.GetComponent<RailSplineMesh>());
                }
                // 全レールは実在する。⑧だけ正面の見せ線路を短くし、その奥に切れた続きの線路を置く。
                if(section==8)
                {
                    var rail=route.GetComponent<RailSplineMesh>();RollerCoasterCourseBuilder.SetRange(rail,3.5f,10);RailSplineMeshGenerator.Generate(rail);
                    Route("08 Broken Rail Far Side",new[]{p+heading*20,p+heading*33});
                    var reveal=new GameObject("08 Broken Rail Reveal Light");reveal.transform.SetParent(root.transform,false);
                    reveal.transform.position=p+heading*23f+Vector3.up*2.5f;
                    var revealLight=reveal.AddComponent<Light>();revealLight.type=LightType.Point;revealLight.range=25f;
                    revealLight.intensity=4f;revealLight.color=new Color(.85f,.9f,1f);revealLight.shadows=LightShadows.None;
                }
                else Actor(section,RollerCoasterStoryboard.ACTOR_KIND.ONCOMING,route,25);
                var room=new ROOM{Entry=distance-8,Exit=distance+(section==8?35:13),Ceiling=0};room.Branches.Add((route,section==8?32:12));rooms.Add(room);
            }
            {
                float d=table.DistanceAtParameter(markers[5]);var pp=new List<Vector3>();
                foreach(float offset in new[]{-28f,-15f,0f,15f,28f})
                {table.Frame(d+offset,out var p,out var r);pp.Add(p-r*Vector3.right*(offset>15?14:6));}
                var route=Route("06 Parallel Cart Separate Tunnel",pp);Actor(6,RollerCoasterStoryboard.ACTOR_KIND.PARALLEL,route,28);
                var room=new ROOM{Entry=d-23,Exit=d+18};var rt=new RailDistanceTable(route);room.Branches.Add((route,4));room.Branches.Add((route,rt.Length-4));rooms.Add(room);
            }
            foreach(int section in new[]{9,10})
            {
                float d=table.DistanceAtParameter(markers[section-1]);table.Frame(d,out var p,out var r);Vector3 right=Vector3.ProjectOnPlane(r*Vector3.right,Vector3.up).normalized;
                var route=Route($"{section:00} Crossing Right To Left",new[]{p+right*16,p+right*8,p,p-right*8,p-right*16});
                Actor(section,RollerCoasterStoryboard.ACTOR_KIND.CROSSING,route,20);
                var room=new ROOM{Entry=d-8,Exit=d+8};room.Branches.Add((route,3));room.Branches.Add((route,29));rooms.Add(room);
            }
            // 着地地点の手前まで受け側の線路を伸ばし、車体全体が線路へ乗る。
            Route("11 Landing Catch Rail",new[]{points[landing]-forward*7f,points[landing],points[landing+1]});
            RollerCoasterCourseBuilder.SetRange(course.AfterFall,table.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(spline,landing+1)),0);
            RailSplineMeshGenerator.Generate(course.AfterFall);
            var ceilings=new List<RollerCoasterStoryboard.CEILING>();
            GenerateEnclosure(root.transform,course,enclosure,table,rooms,markers,fallEnd,ceilings);
            var speedKeys=new List<Keyframe>{new Keyframe(0,3),new Keyframe(markers[0],3),new Keyframe(Mathf.Lerp(markers[0],markers[1],.55f),36)};
            float[] speeds={3,36,31,26,24,23,22,24,27,27,29};
            for(int i=1;i<markers.Length;i++)speedKeys.Add(new Keyframe(markers[i],speeds[i]));
            speedKeys.Add(new Keyframe(fallEnd-.0001f,29));speedKeys.Add(new Keyframe(fallEnd,2.4f));speedKeys.Add(new Keyframe(1,2.4f));
            var story=ride.gameObject.AddComponent<RollerCoasterStoryboard>();story.Configure(ride,markers,fallEnd,new AnimationCurve(speedKeys.OrderBy(x=>x.time).ToArray()),actors.ToArray(),ceilings.ToArray());
            EditorUtility.SetDirty(story);EditorUtility.SetDirty(course);EditorUtility.SetDirty(course.Spline);EditorUtility.SetDirty(oldProfile);
            // 旧生成ボタンは旧レイアウト用。新構成は専用メニューから再生成する。
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Temp/RollerCoasterStoryboard");
            File.WriteAllText("Temp/RollerCoasterStoryboard/build.txt",$"11 sections, {spline.Count} knots, {table.Length:F3}m; actors={actors.Count}, moving ceilings={ceilings.Count}\n"+
                string.Join("\n",markers.Select((x,i)=>$"{i+1}: t={x:F6} d={table.DistanceAtParameter(x):F3} p={course.Spline.EvaluatePosition(0,x)}")));
            Selection.activeGameObject=story.gameObject;
        }

        [MenuItem("Tools/Roller Coaster/Rebuild Image Course Duct Enclosure")]
        internal static void RebuildDuctEnclosure()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("編集モードで実行してください。");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!=RollerCoasterCourseBuilder.ScenePath)throw new InvalidOperationException("RollerCoaster Sceneを開いてください。");
            var course=UnityEngine.Object.FindFirstObjectByType<RollerCoasterCourse>();
            var story=UnityEngine.Object.FindFirstObjectByType<RollerCoasterStoryboard>();
            var root=course.transform.Find("Image Course 1-11");var main=new RailDistanceTable(course.Spline);
            var settings=course.GetComponentInChildren<RollerCoasterEnclosures>(true);var rooms=new List<ROOM>();
            foreach(int section in new[]{4,5,8})
            {
                float d=main.DistanceAtParameter(story.Markers[section-1]);
                var room=new ROOM{Entry=d-8,Exit=d+(section==8?35:13)};
                room.Branches.Add((root.Find($"{section:00} Apparent Forward Route").GetComponent<SplineContainer>(),section==8?32:12));rooms.Add(room);
            }
            {
                float d=main.DistanceAtParameter(story.Markers[5]);var route=root.Find("06 Parallel Cart Separate Tunnel").GetComponent<SplineContainer>();
                var room=new ROOM{Entry=d-23,Exit=d+18};var table=new RailDistanceTable(route);
                room.Branches.Add((route,4));room.Branches.Add((route,table.Length-4));rooms.Add(room);
            }
            foreach(int section in new[]{9,10})
            {
                float d=main.DistanceAtParameter(story.Markers[section-1]);var room=new ROOM{Entry=d-8,Exit=d+8};
                var route=root.Find($"{section:00} Crossing Right To Left").GetComponent<SplineContainer>();
                room.Branches.Add((route,3));room.Branches.Add((route,29));rooms.Add(room);
            }
            foreach(string name in new[]{"Continuous Course Enclosure","07 Rising Ceiling","08 Rising Ceiling Before Broken Track","08 Dark Gap Under Broken Track"})
            {var old=root.Find(name);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);}
            var ceilings=new List<RollerCoasterStoryboard.CEILING>();
            GenerateEnclosure(root,course,settings,main,rooms,story.Markers,story.FallEnd,ceilings);
            story.ConfigureCeilings(ceilings.ToArray());EditorUtility.SetDirty(story);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }

        private static void GenerateEnclosure(Transform parent,RollerCoasterCourse course,RollerCoasterEnclosures settings,RailDistanceTable main,List<ROOM> rooms,float[] markers,float fallEnd,List<RollerCoasterStoryboard.CEILING> ceilings)
        {
            var palette=settings.Palette;var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=palette.Select(_=>new List<int>()).ToArray();
            const float half=3f,floor=-1.6f,roof=4.4f;
            var sourceModel=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/map/duct/duct.fbx");
            var sourceMesh=sourceModel.GetComponentsInChildren<MeshFilter>(true).Single(x=>x.name=="duct_straight").sharedMesh;
            var sourceVertices=sourceMesh.vertices;var sourceUv=sourceMesh.uv;var bounds=sourceMesh.bounds;
            var profile=new List<(float V,Vector2 P)>();
            for(int i=0;i<sourceVertices.Length;i++)if(Mathf.Abs(sourceVertices[i].z-bounds.max.z)<.00001f)
                profile.Add((sourceUv[i].y,new Vector2((sourceVertices[i].x-bounds.center.x)/bounds.extents.x,(sourceVertices[i].y-bounds.center.y)/bounds.extents.y)));
            profile=profile.GroupBy(x=>Mathf.RoundToInt(x.V*100000)).Select(x=>x.First()).OrderBy(x=>x.V).ToList();
            if(profile.Count<9)throw new InvalidOperationException("duct_straightの円筒断面を取得できません。");
            Vector2 SourcePoint(float v)
            {
                v=Mathf.Repeat(v,1);int i=1;while(i<profile.Count-1&&profile[i].V<v)i++;
                return Vector2.Lerp(profile[i-1].P,profile[i].P,Mathf.InverseLerp(profile[i-1].V,profile[i].V,v));
            }
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int color)
            {
                int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});float w=Vector3.Distance(a,b),h=Vector3.Distance(a,d);
                uv.AddRange(new[]{Vector2.zero,new Vector2(w,0),new Vector2(w,h),new Vector2(0,h)});indices[Mathf.Abs(color)%indices.Length].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            Mesh SaveMesh(string name,List<Vector3> vv,List<Vector2> u,List<int>[] ii)
            {
                if(name.StartsWith("Ceiling",StringComparison.Ordinal))
                {
                    // 曲面の区間境界を共有し、内部に重なる小口面を作らない。
                    var shared=new Dictionary<Vector3,int>();var welded=new List<Vector3>();var weldedUv=new List<Vector2>();var remap=new int[vv.Count];
                    for(int i=0;i<vv.Count;i++)
                    {
                        Vector3 key=new Vector3(Mathf.Round(vv[i].x*10000),Mathf.Round(vv[i].y*10000),Mathf.Round(vv[i].z*10000));
                        if(!shared.TryGetValue(key,out int index)){index=welded.Count;shared.Add(key,index);welded.Add(vv[i]);weldedUv.Add(u[i]);}
                        remap[i]=index;
                    }
                    foreach(var submesh in ii)for(int i=0;i<submesh.Count;i++)submesh[i]=remap[submesh[i]];
                    vv.Clear();vv.AddRange(welded);u.Clear();u.AddRange(weldedUv);
                    int topCount=vv.Count;var topIndices=ii[0].ToArray();
                    vv.AddRange(vv.Take(topCount).Select(p=>p-Vector3.up*.12f).ToArray());u.AddRange(u.Take(topCount).ToArray());
                    var edges=new Dictionary<(int,int),int>();
                    for(int i=0;i<topIndices.Length;i+=3)
                    {
                        int a=topIndices[i],b=topIndices[i+1],c=topIndices[i+2];ii[0].AddRange(new[]{a+topCount,c+topCount,b+topCount});
                        foreach(var edge in new[]{(a,b),(b,c),(c,a)}){var key=(Mathf.Min(edge.Item1,edge.Item2),Mathf.Max(edge.Item1,edge.Item2));edges[key]=edges.TryGetValue(key,out int count)?count+1:1;}
                    }
                    foreach(var edge in edges.Where(x=>x.Value==1).Select(x=>x.Key))
                        ii[0].AddRange(new[]{edge.Item1,edge.Item2,edge.Item2+topCount,edge.Item1,edge.Item2+topCount,edge.Item1+topCount});
                }
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vv);mesh.SetUVs(0,u);mesh.subMeshCount=ii.Length;
                for(int i=0;i<ii.Length;i++)mesh.SetTriangles(ii[i],i);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Output+"/"+name+".asset"));return mesh;
            }
            void Object(string name,Mesh mesh,Transform root)
            {var go=new GameObject(name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=name=="Moving Roof"?new[]{palette[0]}:palette;}
            (Vector3 P,Quaternion R) Frame(RailDistanceTable source,float d){source.Frame(d,out var p,out var r);return(p,r);}
            Vector3[] Ring(RailDistanceTable source,float d)
            {
                var f=Frame(source,d);f.R=Quaternion.LookRotation(f.R*Vector3.forward,Vector3.up);return new[]{f.P+f.R*new Vector3(-half,floor,0),f.P+f.R*new Vector3(half,floor,0),f.P+f.R*new Vector3(half,roof,0),f.P+f.R*new Vector3(-half,roof,0)};
            }
            var roofVertices=new List<Vector3>();var roofUv=new List<Vector2>();var roofIndices=new List<int>();
            float ceiling7=main.DistanceAtParameter(markers[6]);
            float ceiling8=main.DistanceAtParameter(markers[7])-18f;
            Vector3[] TubeRing(RailDistanceTable source,float d,float start,float end)
            {
                var frame=Frame(source,d);var rotation=Quaternion.LookRotation(frame.R*Vector3.forward,Vector3.up);
                float round=Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Min(d-start,end-d)-1.25f)/4f));
                if(source==main)foreach(float ceiling in new[]{ceiling7,ceiling8})
                    round*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,10,Mathf.Abs(d-ceiling)));
                var corners=new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)};
                var result=new Vector3[32];
                for(int i=0;i<result.Length;i++)
                {
                    Vector2 box=Vector2.Lerp(corners[i/8],corners[(i/8+1)%4],(i%8)/8f);
                    Vector2 p=Vector2.Lerp(box,SourcePoint(.875f+i/32f),round);
                    result[i]=frame.P+rotation*new Vector3(p.x*half,(roof+floor)*.5f+p.y*(roof-floor)*.5f,0);
                }
                return result;
            }
            var roof8Vertices=new List<Vector3>();var roof8Uv=new List<Vector2>();var roof8Indices=new List<int>();
            void RoofQuad(List<Vector3> vv,List<Vector2> uu,List<int> ii,Vector3 a,Vector3 b,Vector3 c,Vector3 e,float d0,float d1,float center)
            {
                float Sag(float d){float x=Mathf.Clamp((d-center-.5f)/4.5f,-1,1);return 3.5f*Mathf.Sqrt(Mathf.Max(0,1-x*x));}
                int n=vv.Count;vv.AddRange(new[]{a-Vector3.up*Sag(d0),b-Vector3.up*Sag(d0),c-Vector3.up*Sag(d1),e-Vector3.up*Sag(d1)});
                float meters=palette[0].GetFloat("_TileMeters");
                uu.AddRange(new[]{new Vector2(d0,1.375f*meters),new Vector2(d0,1.625f*meters),new Vector2(d1,1.625f*meters),new Vector2(d1,1.375f*meters)});
                ii.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            void Pipe(RailDistanceTable source,float start,float end,bool cap=false)
            {
                if(end<=start)return;int steps=Mathf.CeilToInt((end-start)/.25f);var previous=TubeRing(source,start,start,end);
                var distances=new SortedSet<float>();
                for(int i=0;i<=steps;i++)distances.Add(Mathf.Lerp(start,end,i/(float)steps));
                foreach(float d in new[]{start+1,end-1,ceiling7-4,ceiling7+5,ceiling8-4,ceiling8+5})if(d>start&&d<end)distances.Add(d);
                float previousDistance=start;
                foreach(float d in distances)
                {
                    if(d<=start)continue;
                    var next=TubeRing(source,d,start,end);int color=(int)(d/5)%palette.Length;
                    if(source==main)
                    {
                        var a=Ring(source,previousDistance);var b=Ring(source,d);
                        if(d>ceiling7-4&&d<=ceiling7+5)RoofQuad(roofVertices,roofUv,roofIndices,a[2],a[3],b[3],b[2],previousDistance,d,ceiling7);
                        if(d>ceiling8-4&&d<=ceiling8+5)RoofQuad(roof8Vertices,roof8Uv,roof8Indices,a[2],a[3],b[3],b[2],previousDistance,d,ceiling8);
                    }
                    for(int side=0;side<previous.Length;side++)
                    {
                        int other=(side+1)%previous.Length;
                        {
                            int n=vertices.Count;Quad(previous[side],previous[other],next[other],next[side],color);
                            if(settings.UseSourceDuct)
                            {
                                float meters=palette[0].GetFloat("_TileMeters");float v0=(.875f+side/(float)previous.Length)*meters,v1=(.875f+(side+1)/(float)previous.Length)*meters;
                                float d0=previousDistance;
                                uv[n]=new Vector2(d0,v0);uv[n+1]=new Vector2(d0,v1);uv[n+2]=new Vector2(d,v1);uv[n+3]=new Vector2(d,v0);
                            }
                        }
                    }
                    previous=next;previousDistance=d;
                }
                if(cap){var capRing=Ring(source,end);Quad(capRing[0],capRing[1],capRing[2],capRing[3],0);}
            }
            float cursor=0;
            foreach(var room in rooms.OrderBy(x=>x.Entry))
            {
                if(room.Entry<cursor)throw new InvalidOperationException("演出用の囲いが重なっています。区間の間隔を広げてください。");
                Pipe(main,cursor,room.Entry+1f);
                var ports=new List<Vector3[]>{Ring(main,room.Entry),Ring(main,room.Exit)};
                foreach(var group in room.Branches.GroupBy(x=>x.Route))
                {
                    var table=new RailDistanceTable(group.Key);var distances=group.Select(x=>x.Distance).OrderBy(x=>x).ToArray();
                    foreach(float distance in distances)ports.Add(Ring(table,distance));
                    if(distances.Length==2)
                    {
                        Pipe(table,0,distances[0]+1f);Pipe(table,distances[1]-1f,table.Length,true);var cap=Ring(table,0);Quad(cap[0],cap[1],cap[2],cap[3],0);
                    }
                    else Pipe(table,Mathf.Max(0,distances[0]-1f),table.Length,true);
                }
                var all=ports.SelectMany(x=>new[]{x[0],x[1]}).ToArray();Vector3 center=all.Aggregate(Vector3.zero,(a,b)=>a+b)/all.Length;
                var hull=new List<Vector3>();
                foreach(var port in ports.OrderBy(p=>Mathf.Atan2((p[0].z+p[1].z)*.5f-center.z,(p[0].x+p[1].x)*.5f-center.x)))
                {
                    Vector3 a=port[0]-center,b=port[1]-center;
                    if(a.x*b.z-a.z*b.x>0){hull.Add(port[0]);hull.Add(port[1]);}
                    else {hull.Add(port[1]);hull.Add(port[0]);}
                }
                float bottom=all.Min(x=>x.y)-.4f,top=ports.Max(x=>x[2].y);int openings=0;
                var panelV=new List<Vector3>();var panelU=new List<Vector2>();var panelI=new List<int>();
                for(int i=0;i<hull.Count;i++)
                {
                    var a=hull[i];var b=hull[(i+1)%hull.Count];
                    bool opening=ports.Any(p=>(Vector3.Distance(a,p[0])<.01f&&Vector3.Distance(b,p[1])<.01f)||(Vector3.Distance(a,p[1])<.01f&&Vector3.Distance(b,p[0])<.01f));
                    Vector3 aa=a+Vector3.up*(roof-floor),bb=b+Vector3.up*(roof-floor);
                    if(opening)openings++;else Quad(a,b,bb,aa,0);
                    Vector3 floorCenter=new Vector3(center.x,bottom,center.z),roofCenter=new Vector3(center.x,top,center.z);
                    Quad(floorCenter,a,b,floorCenter,0);
                    if(room.Ceiling==0)Quad(roofCenter,bb,aa,roofCenter,2);
                    else {int n=panelV.Count;panelV.AddRange(new[]{roofCenter,bb,aa});panelU.AddRange(new[]{Vector2.zero,Vector2.right*6,Vector2.up*6});panelI.AddRange(new[]{n,n+1,n+2});}
                }
                if(openings!=ports.Count)throw new InvalidOperationException($"分岐の開口が不足しています: {openings}/{ports.Count}, {room.Entry:F1}m");
                if(room.Ceiling>0)
                {
                    var go=new GameObject("08 Rising Ceiling and Broken Track Reveal");go.transform.SetParent(parent,false);
                    Object("Moving Roof",SaveMesh("Ceiling08",panelV,panelU,new[]{panelI}),go.transform);
                    ceilings.Add(new RollerCoasterStoryboard.CEILING{Section=8,Panel=go.transform,Entry=main.Parameter(room.Entry),Exit=main.Parameter(room.Exit),Lift=2.6f,Lead=20});
                }
                cursor=room.Exit-1f;
            }
            float edge=main.DistanceAtParameter(markers[10]),landing=main.DistanceAtParameter(fallEnd);Pipe(main,cursor,edge);
            // 箱状の縦通路を上下の開口へ接続。下の通路の天井上面を露出させない。
            var first=Ring(main,edge);var last=Ring(main,landing+3);
            Vector3 heading=Vector3.ProjectOnPlane(Frame(main,edge).R*Vector3.forward,Vector3.up).normalized;
            Vector3 right=Vector3.Cross(Vector3.up,heading);Vector3 origin=Frame(main,edge).P;
            float depth=Vector3.Dot(Frame(main,landing+3).P-origin,heading);
            float low=Frame(main,landing+3).P.y+floor-.2f,high=origin.y+roof;
            Vector3 V(float x,float y,float z)=>origin+right*x+heading*z+Vector3.up*(y-origin.y);
            Quad(V(-half,low,0),V(-half,high,0),V(-half,high,depth),V(-half,low,depth),0);
            Quad(V(half,low,0),V(half,high,0),V(half,high,depth),V(half,low,depth),1);
            Quad(V(-half,high,0),V(half,high,0),V(half,high,depth),V(-half,high,depth),2);
            Quad(V(-half,low,0),V(half,low,0),V(half,low,depth),V(-half,low,depth),0);
            Quad(V(-half,low,0),V(half,low,0),first[1],first[0],0);
            Quad(V(-half-.03f,low,0),first[0],first[3],V(-half-.03f,high,0),0);
            Quad(first[1],V(half+.03f,low,0),V(half+.03f,high,0),first[2],0);
            Quad(first[3],first[2],V(half,high,0),V(-half,high,0),2);
            Quad(last[3],last[2],V(half,high,depth),V(-half,high,depth),0);
            Quad(V(-half-.03f,low,depth),last[0],last[3],V(-half-.03f,high,depth),0);
            Quad(last[1],V(half+.03f,low,depth),V(half+.03f,high,depth),last[2],0);
            Quad(V(-half,low,depth),V(half,low,depth),last[1],last[0],0);
            Pipe(main,landing+3,main.Length);
            Object("Continuous Course Enclosure",SaveMesh("CourseEnclosure",vertices,uv,indices),parent);
            var roof7=new GameObject("07 Rising Ceiling");roof7.transform.SetParent(parent,false);
            Object("Moving Roof",SaveMesh("Ceiling07",roofVertices,roofUv,new[]{roofIndices}),roof7.transform);
            ceilings.Add(new RollerCoasterStoryboard.CEILING{Section=7,Panel=roof7.transform,Entry=main.Parameter(ceiling7-1),Exit=main.Parameter(ceiling7+5),Lift=3.9f,Lead=7});
            var roof8=new GameObject("08 Rising Ceiling Before Broken Track");roof8.transform.SetParent(parent,false);
            Object("Moving Roof",SaveMesh("Ceiling08",roof8Vertices,roof8Uv,new[]{roof8Indices}),roof8.transform);
            ceilings.Add(new RollerCoasterStoryboard.CEILING{Section=8,Panel=roof8.transform,Entry=main.Parameter(ceiling8-1),Exit=main.Parameter(ceiling8+5),Lift=3.9f,Lead=7});
        }
    }
}
