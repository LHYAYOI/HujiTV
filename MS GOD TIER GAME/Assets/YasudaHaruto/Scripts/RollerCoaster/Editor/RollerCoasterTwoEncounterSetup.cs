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
            var sequence=course.GetComponentInChildren<RollerCoasterEncounterSequence>(true);
            Spline baseline;
            if(sequence!=null)baseline=new Spline(sequence.Baseline.Spline);
            else
            {
                baseline=new Spline(course.Spline.Spline);
                var legacy=course.GetComponentInChildren<RollerCoasterEncounter>(true);
                if(legacy!=null&&legacy.OriginalRoute!=null)
                    for(int i=course.FallEndKnot+1;i<=course.ReturnSectionKnots[1];i++)baseline.SetKnot(i,legacy.OriginalRoute.Spline[i]);
            }
            var oldRoots=course.GetComponentsInChildren<RollerCoasterEncounter>(true).Select(x=>x.transform.parent==course.transform?x.gameObject:null).Where(x=>x!=null).Distinct().ToArray();
            var root=new GameObject("Two Oncoming Encounters");root.transform.SetParent(course.transform,false);
            Undo.RegisterCreatedObjectUndo(root,"Create two oncoming encounters");
            var baselineObject=new GameObject("Course Baseline");baselineObject.transform.SetParent(root.transform,false);
            baselineObject.transform.SetPositionAndRotation(course.Spline.transform.position,course.Spline.transform.rotation);
            baselineObject.transform.localScale=course.Spline.transform.lossyScale;
            var reference=baselineObject.AddComponent<SplineContainer>();reference.Spline=baseline;
            var planned=new Spline(baseline);
            reference.Evaluate(0,RollerCoasterCourseBuilder.KnotParameter(baseline,course.SpiralEndKnot),out _,out var forward,out _);
            Vector3 right=Vector3.Cross(Vector3.up,Vector3.ProjectOnPlane(forward,Vector3.up).normalized).normalized;
            Vector3 localShift=course.Spline.transform.InverseTransformVector(-right*4f);
            for(int i=course.FallStartKnot;i<planned.Count;i++){var knot=planned[i];knot.Position+=(Unity.Mathematics.float3)localShift;planned.SetKnot(i,knot);}
            RollerCoasterCourseBuilder.ApplyPreparedLayout(course,new RollerCoasterCourseBuilder.Layout
            {
                Spline=planned,Crest=course.CrestKnot,SpiralStart=course.SpiralStartKnot,SpiralEnd=course.SpiralEndKnot,
                FallStart=course.FallStartKnot,FallEnd=course.FallEndKnot,HillStart=course.HillStartKnot,ReturnSections=course.ReturnSectionKnots
            });
            var table=new RailDistanceTable(course.Spline);
            var ride=RollerCoasterTrialSetup.Create(course);
            RollerCoasterEncounter Create(int number,int forkKnot,int peakKnot,int endKnot,bool avoidRight)
            {
                var go=new GameObject(number==1?"Encounter 1 - Left threat, Right spiral":"Encounter 2 - Right threat, Left drop");go.transform.SetParent(root.transform,false);
                float fork=RollerCoasterCourseBuilder.KnotParameter(planned,forkKnot);
                course.Spline.Evaluate(0,fork,out var start,out var tangent,out _);
                Vector3 ahead=Vector3.ProjectOnPlane(tangent,Vector3.up).normalized;
                Vector3 side=Vector3.Cross(Vector3.up,ahead).normalized*(avoidRight?-1f:1f);
                var wrongObject=new GameObject(avoidRight?"Left Branch - Always Visible":"Right Branch - Always Visible");wrongObject.transform.SetParent(go.transform,false);
                var wrong=wrongObject.AddComponent<SplineContainer>();var spline=new Spline();
                Vector3 tangent3=tangent;
                float grade=tangent3.y/Mathf.Max(.1f,Vector3.ProjectOnPlane(tangent3,Vector3.up).magnitude);
                foreach(Vector2 p in new[]{Vector2.zero,new Vector2(3,7),new Vector2(10,10),new Vector2(24,10)})
                {
                    Vector3 world=(Vector3)start+side*p.x+ahead*p.y+Vector3.up*(grade*p.y);
                    spline.Add(new BezierKnot(wrong.transform.InverseTransformPoint(world)),TangentMode.AutoSmooth);
                }
                var k=spline[0];spline.SetTangentMode(0,TangentMode.Broken);
                k.Rotation=Unity.Mathematics.quaternion.identity;
                k.TangentOut=wrong.transform.InverseTransformDirection(tangent3).normalized*4f;k.TangentIn=-k.TangentOut;spline.SetKnot(0,k);wrong.Spline=spline;
                var rail=wrongObject.AddComponent<RailSplineMesh>();rail.Configure(wrong,course.BeforeFall.SourceMesh,course.BeforeFall.Materials);
                var data=new SerializedObject(rail);data.FindProperty("m_crossSectionOffset").vector2Value=course.BeforeFall.CrossSectionOffset;data.ApplyModifiedPropertiesWithoutUndo();
                RailSplineMeshGenerator.Generate(rail);
                var cart=new GameObject("Oncoming Cart (Preview Model)");cart.transform.SetParent(go.transform,false);RollerCoasterEncounterSetup.BuildCart(cart.transform);cart.SetActive(false);
                var encounter=go.AddComponent<RollerCoasterEncounter>();
                float entry=table.DistanceAtParameter(fork)-(number==1?7f:6f);
                float exit=number==1?table.DistanceAtParameter(fork)+6f:table.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(planned,course.FallStartKnot));
                encounter.Configure(ride,reference,wrong,cart.transform,number,avoidRight,fork,
                    RollerCoasterCourseBuilder.KnotParameter(planned,peakKnot),RollerCoasterCourseBuilder.KnotParameter(planned,endKnot),entry,exit,
                    new[]{rail.GeneratedRoot.GetComponent<Renderer>(),course.BeforeFall.GeneratedRoot.GetComponent<Renderer>()});
                EditorUtility.SetDirty(encounter);return encounter;
            }
            var first=Create(1,course.SpiralStartKnot,course.SpiralStartKnot+1,course.SpiralStartKnot+2,true);
            var second=Create(2,course.SpiralEndKnot,course.FallStartKnot,course.FallStartKnot,false);
            var result=root.AddComponent<RollerCoasterEncounterSequence>();result.Configure(reference,new[]{first,second});EditorUtility.SetDirty(result);
            foreach(var old in oldRoots)Undo.DestroyObjectImmediate(old);
            if(sequence!=null)Undo.DestroyObjectImmediate(sequence.gameObject);
            return result;
        }
    }
}
