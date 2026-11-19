using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
namespace YasudaHaruto.RollerCoaster.Editor
{
    internal static class RollerCoasterTwoEncounterSetup
    {
        internal static RollerCoasterEncounterSequence Build(RollerCoasterCourse course)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("編集モードで更新してください。");
            var previous=course.GetComponentInChildren<RollerCoasterEncounterSequence>(true);
            var oldRoots=course.GetComponentsInChildren<RollerCoasterEncounter>(true).Select(x=>x.transform.parent==course.transform?x.gameObject:null).Where(x=>x!=null).Distinct().ToArray();
            var layout=RollerCoasterCourseBuilder.MakeLayout(course);
            // 見かけの右側本線を保持し、実走行経路だけ直前に左へ分ける。
            var apparent=new Spline(layout.Spline);
            int junction=layout.ReturnSections[2]+1;
            Quaternion heading=apparent[junction].Rotation;
            Vector3 right=Vector3.Cross(Vector3.up,Vector3.ProjectOnPlane(heading*Vector3.forward,Vector3.up).normalized);
            float[] offsets={0f,3.5f,5.5f,5f,2.5f,0f};
            for(int n=0;n<offsets.Length;n++)
            {int i=junction+n;var k=layout.Spline[i];k.Position-=(Unity.Mathematics.float3)(right*offsets[n]);layout.Spline.SetKnot(i,k);}
            for(int i=junction;i<junction+offsets.Length;i++)
            {
                var k=layout.Spline[i];Vector3 before=(Vector3)(k.Position-layout.Spline[i-1].Position);
                Vector3 after=(Vector3)(layout.Spline[i+1].Position-k.Position);
                Quaternion rotation=Quaternion.LookRotation((before.normalized+after.normalized).normalized,Vector3.up);
                if(i==junction)rotation=apparent[junction].Rotation;
                k.Rotation=rotation;k.TangentIn=new Vector3(0,0,-before.magnitude/3f);k.TangentOut=new Vector3(0,0,after.magnitude/3f);layout.Spline.SetKnot(i,k);
            }
            RollerCoasterCourseBuilder.ApplyPreparedLayout(course,layout);
            var root=new GameObject("Single Post Landing Near Miss");root.transform.SetParent(course.transform,false);
            var baselineObject=new GameObject("Course Baseline");baselineObject.transform.SetParent(root.transform,false);
            baselineObject.transform.SetPositionAndRotation(course.Spline.transform.position,course.Spline.transform.rotation);
            var baseline=baselineObject.AddComponent<SplineContainer>();baseline.Spline=apparent;
            var table=new RailDistanceTable(course.Spline);var ride=RollerCoasterTrialSetup.Create(course);
            RollerCoasterEncounter Create(int number,int forkKnot,int endKnot)
            {
                float fork=RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline,forkKnot);
                baseline.Evaluate(0,RollerCoasterCourseBuilder.KnotParameter(apparent,forkKnot),out var start,out var tangent,out _);
                Vector3 ahead=Vector3.ProjectOnPlane(tangent,Vector3.up).normalized;
                Vector3 right=Vector3.Cross(Vector3.up,ahead);
                bool avoidRight=false;
                Vector3 side=right;
                var go=new GameObject("Post Landing Encounter "+number);go.transform.SetParent(root.transform,false);
                var branchObject=new GameObject("Oncoming Branch");branchObject.transform.SetParent(go.transform,false);
                var branch=branchObject.AddComponent<SplineContainer>();var spline=new Spline();
                float grade=tangent.y/Mathf.Max(.1f,Vector3.ProjectOnPlane(tangent,Vector3.up).magnitude);
                for(int i=forkKnot-3;i<=forkKnot;i++)
                {
                    var back=course.Spline.Spline[i];back.Position=branch.transform.InverseTransformPoint(course.Spline.transform.TransformPoint(back.Position));
                    back.Rotation=Quaternion.Inverse(branch.transform.rotation)*course.Spline.transform.rotation*(Quaternion)back.Rotation;
                    spline.Add(back,TangentMode.Broken);
                }
                foreach(Vector2 p in new[]{new Vector2(3,14),new Vector2(12,32),new Vector2(20,52)})
                    spline.Add(new BezierKnot(branch.transform.InverseTransformPoint((Vector3)start+side*p.x+ahead*p.y+Vector3.up*grade*p.y)),TangentMode.AutoSmooth);
                var split=spline[3];split.TangentOut=Quaternion.Inverse(split.Rotation)*(branch.transform.InverseTransformDirection(tangent).normalized*5f);spline.SetKnot(3,split);
                branch.Spline=spline;
                float mainT=RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline,forkKnot);
                course.Spline.Evaluate(0,mainT,out var mp,out var mt,out var mu);
                branch.Evaluate(0,RollerCoasterCourseBuilder.KnotParameter(spline,3),out var bp,out var bt,out var bu);
                if(Vector3.Distance(mp,bp)>.0001f||Vector3.Dot(((Vector3)mt).normalized,((Vector3)bt).normalized)<.9999f||Vector3.Dot(((Vector3)mu).normalized,((Vector3)bu).normalized)<.9999f)
                    throw new InvalidOperationException("分岐レールの位置・接線・バンクが一致しません。");
                Debug.Log("Rail junction verified: shared position, tangent and bank.");
                var branchTable=new RailDistanceTable(branch);
                float crossing=branchTable.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(spline,3));
                var rail=branchObject.AddComponent<RailSplineMesh>();rail.Configure(branch,course.BeforeFall.SourceMesh,course.BeforeFall.Materials);
                var data=new SerializedObject(rail);data.FindProperty("m_crossSectionOffset").vector2Value=course.BeforeFall.CrossSectionOffset;data.FindProperty("m_startDistance").floatValue=crossing;data.FindProperty("m_suppressSleeperMeters").floatValue=7f;data.ApplyModifiedPropertiesWithoutUndo();RailSplineMeshGenerator.Generate(rail);
                var cart=new GameObject("Oncoming Trash Cart");cart.transform.SetParent(go.transform,false);RollerCoasterEncounterSetup.BuildCart(cart.transform);cart.SetActive(false);
                var encounter=go.AddComponent<RollerCoasterEncounter>();
                float peak=RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline,forkKnot+1);
                float end=RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline,endKnot);
                encounter.Configure(ride,baseline,branch,cart.transform,number,avoidRight,fork,peak,end,
                    table.DistanceAtParameter(fork)-7f,table.DistanceAtParameter(fork)+30f,
                    new[]{rail.GeneratedRoot.GetComponent<Renderer>(),course.AfterFall.GeneratedRoot.GetComponent<Renderer>()});
                // 実走行のパラメータ速度で交差地点への到着時間を積分し、停止なしで同期。
                var profile=ride.GetComponent<RollerCoasterSpeedProfile>();profile.Initialize(course,ride.Cart,0);
                float target=Mathf.Lerp(fork,RollerCoasterCourseBuilder.KnotParameter(layout.Spline,forkKnot+1),.65f);
                float trigger=fork-13f/course.Spline.CalculateLength();
                double seconds=0;const int samples=512;
                for(int i=0;i<samples;i++)
                {float p=Mathf.Lerp(trigger,target,(i+.5f)/samples);seconds+=(target-trigger)*course.Spline.CalculateLength()/samples/profile.SpeedAt(p,10);}
                var config=new SerializedObject(encounter);
                config.FindProperty("m_startDistance").floatValue=Mathf.Min(branchTable.Length-1f,crossing+18f*(float)seconds);
                config.FindProperty("m_speed").floatValue=18f;config.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(encounter);return encounter;
            }
            var sections=course.ReturnSectionKnots;
            var first=Create(1,junction,junction+5);

            var result=root.AddComponent<RollerCoasterEncounterSequence>();result.Configure(baseline,new[]{first});
            foreach(var old in oldRoots)Undo.DestroyObjectImmediate(old);
            if(previous!=null)Undo.DestroyObjectImmediate(previous.gameObject);
            EditorUtility.SetDirty(result);return result;
        }
    }
}
