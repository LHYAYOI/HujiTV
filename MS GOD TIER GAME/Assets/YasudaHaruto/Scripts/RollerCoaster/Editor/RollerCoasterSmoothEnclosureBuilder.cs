using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace YasudaHaruto.RollerCoaster.Editor
{
    internal static class RollerCoasterSmoothEnclosureBuilder
    {
        private struct Port { public Vector3 Center; public Quaternion Rotation; public float Width; }
        internal static Quaternion Horizontal(Quaternion rotation)=>Quaternion.LookRotation(Vector3.ProjectOnPlane(rotation*Vector3.forward,Vector3.up).normalized,Vector3.up);
        internal static void Generate(RollerCoasterEnclosures settings)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorUtility.IsPersistent(settings))throw new InvalidOperationException("Sceneの編集モードで生成してください。");
            if(settings.Course==null||settings.Palette==null||settings.Palette.Length==0||settings.Palette.Any(x=>x==null))throw new ArgumentException("コースとPaletteを設定してください。");
            if(settings.GeneratedRoot!=null&&settings.GeneratedRoot.transform.parent!=settings.transform)throw new InvalidOperationException("生成rootを元の親へ戻してください。");
            RollerCoasterEnclosureBuilder.ApplyAtlasColors(settings);
            var course=settings.Course;var table=new RailDistanceTable(course.Spline);
            var encounters=course.GetComponentsInChildren<RollerCoasterEncounter>(true).OrderBy(x=>x.Number).ToArray();
            float shaftEntry=table.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline,course.FallStartKnot));
            float shaftExit=table.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline,course.FallEndKnot))+4f;
            float width=settings.Width,height=settings.Height,step=settings.MeshStep;
            if(width<3||height<3||step<.1f)throw new ArgumentException("囲いの寸法と分割間隔を確認してください。");
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var normals=new List<Vector3>();var groups=new List<int>();
            var triangles=Enumerable.Range(0,settings.Palette.Length).Select(_=>new List<int>()).ToArray();
            var random=new System.Random(settings.ColorSeed);int color=0,remaining=0,last=-1;var bag=new List<int>();
            int NextColor()
            {
                if(remaining-->0)return color;
                if(bag.Count==0){bag.AddRange(Enumerable.Range(0,settings.Palette.Length));for(int i=bag.Count-1;i>0;i--){int j=random.Next(i+1);(bag[i],bag[j])=(bag[j],bag[i]);}}
                int n=bag.FindIndex(x=>x!=last);if(n<0)n=0;color=bag[n];bag.RemoveAt(n);last=color;remaining=random.Next(1,4)-1;return color;
            }
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material,float v0=0,float v1=-1,int group=-1)
            {
                int n=vertices.Count;foreach(var p in new[]{a,b,c,d})vertices.Add(settings.transform.InverseTransformPoint(p));
                float across=(Vector3.Distance(a,b)+Vector3.Distance(c,d))*.5f;
                if(v1<0)v1=(Vector3.Distance(a,d)+Vector3.Distance(b,c))*.5f;
                uv.AddRange(new[]{new Vector2(0,v0),new Vector2(across,v0),new Vector2(across,v1),new Vector2(0,v1)});
                Vector3 normal=settings.transform.InverseTransformDirection(-Vector3.Cross(b-a,d-a).normalized);
                for(int i=0;i<4;i++){normals.Add(normal);groups.Add(group);}
                triangles[material%triangles.Length].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            void Triangle(Vector3 a,Vector3 b,Vector3 c,int material,int group)
            {
                int n=vertices.Count;foreach(var p in new[]{a,b,c})vertices.Add(settings.transform.InverseTransformPoint(p));
                Vector3 u=(b-a).normalized;Vector3 normal=Vector3.Cross(b-a,c-a).normalized;Vector3 v=Vector3.Cross(normal,u);
                uv.Add(Vector2.zero);uv.Add(new Vector2(Vector3.Distance(a,b),0));uv.Add(new Vector2(Vector3.Dot(c-a,u),Vector3.Dot(c-a,v)));
                for(int i=0;i<3;i++){normals.Add(settings.transform.InverseTransformDirection(normal));groups.Add(group);}
                triangles[material%triangles.Length].AddRange(new[]{n,n+1,n+2});
            }
            float MainWidth(float distance)
            {
                var first=encounters.FirstOrDefault(x=>x.Number==1);
                if(first==null)return width;
                float fork=table.DistanceAtParameter(first.Begin);
                float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(fork+2f,fork+5f,distance))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(fork+18f,fork+24f,distance)));
                return Mathf.Lerp(width,Mathf.Min(width,3.6f),blend);
            }
            Vector3[] Ring(Port port)=>new[]{port.Center+port.Rotation*new Vector3(-(port.Width>0?port.Width:width)*.5f,-height*.5f,0),port.Center+port.Rotation*new Vector3((port.Width>0?port.Width:width)*.5f,-height*.5f,0),
                port.Center+port.Rotation*new Vector3((port.Width>0?port.Width:width)*.5f,height*.5f,0),port.Center+port.Rotation*new Vector3(-(port.Width>0?port.Width:width)*.5f,height*.5f,0)};
            Port MainPort(float distance,Quaternion? fixedRotation=null)
            {table.Frame(distance,out var p,out var r);return new Port{Center=p+Vector3.up*settings.CenterHeight,Rotation=fixedRotation??Horizontal(r),Width=MainWidth(distance)};}
            table.Frame(shaftEntry,out _,out var shaftRotation);shaftRotation=Horizontal(shaftRotation);
            void Pipe(RailDistanceTable source,float start,float end,Port first,Port final,bool cap,int groupBase)
            {
                if(end-start<.001f)return;int count=Mathf.CeilToInt((end-start)/step);var previous=Ring(first);var lengths=new float[4];int material=NextColor();
                for(int i=1;i<=count;i++)
                {
                    float d=Mathf.Lerp(start,end,(float)i/count);source.Frame(d,out var p,out var rotation);
                    rotation=Quaternion.Slerp(first.Rotation,rotation,Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,start+2f,d)));
                    rotation=Quaternion.Slerp(rotation,final.Rotation,Mathf.SmoothStep(0,1,Mathf.InverseLerp(end-2f,end,d)));
                    var next=i==count?Ring(final):Ring(new Port{Center=p+rotation*Vector3.up*settings.CenterHeight,Rotation=rotation,Width=source==table?MainWidth(d):width});
                    if(i%Mathf.Max(1,Mathf.RoundToInt(3f/step))==0)material=NextColor();
                    for(int side=0;side<4;side++)
                    {
                        int other=(side+1)%4;float advance=(Vector3.Distance(previous[side],next[side])+Vector3.Distance(previous[other],next[other]))*.5f;
                        Quad(previous[side],previous[other],next[other],next[side],material,lengths[side],lengths[side]+advance,groupBase+side);lengths[side]+=advance;
                    }
                    previous=next;
                }
                if(cap)Quad(previous[0],previous[1],previous[2],previous[3],material);
            }
            float cursor=0;int pipeGroup=0;
            foreach(var encounter in encounters)
            {
                if(encounter.RoomEntry>cursor)
                {
                    var first=MainPort(cursor);if(cursor==0){table.Frame(0,out var p,out var r);first=new Port{Center=p+r*Vector3.up*settings.CenterHeight,Rotation=r};}
                    Pipe(table,cursor,encounter.RoomEntry,first,MainPort(encounter.RoomEntry),false,pipeGroup);pipeGroup+=10;
                }
                var wrongTable=new RailDistanceTable(encounter.OncomingRoute);float branchDistance=12f;
                wrongTable.Frame(branchDistance,out var wrongP,out var wrongR);
                Port[] ports={MainPort(encounter.RoomEntry),MainPort(encounter.RoomExit),new Port{Center=wrongP+Vector3.up*settings.CenterHeight,Rotation=Horizontal(wrongR)}};
                course.Spline.Evaluate(0,encounter.Begin,out var fork,out var tangent,out _);
                var basis=Quaternion.LookRotation(Vector3.ProjectOnPlane(tangent,Vector3.up).normalized,Vector3.up);Vector3 origin=fork;
                Vector3 Local(Vector3 p)=>Quaternion.Inverse(basis)*(p-origin);
                Vector3 World(Vector2 p,float y)=>origin+basis*new Vector3(p.x,y,p.y);
                var points=new List<Vector2>();var floorHeights=new Dictionary<Vector2,float>();var portEdges=new List<(Vector2,Vector2)>();
                foreach(var port in ports)
                {
                    float portalWidth=port.Width>0?port.Width:width;
                    Vector3 l=Local(port.Center-port.Rotation*Vector3.right*portalWidth*.5f),r=Local(port.Center+port.Rotation*Vector3.right*portalWidth*.5f);
                    var a=new Vector2(l.x,l.z);var b=new Vector2(r.x,r.z);points.Add(a);points.Add(b);
                    floorHeights[a]=l.y-height*.5f;floorHeights[b]=r.y-height*.5f;portEdges.Add((a,b));
                }
                float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
                // Preserve all three portal edges, including the concave nose between branches.
                // A convex hull can swallow one portal when the approach itself is curved.
                var hull=new List<Vector2>();
                foreach(var edge in portEdges.OrderBy(e=>Mathf.Atan2((e.Item1+e.Item2).y,(e.Item1+e.Item2).x)))
                {
                    if(Cross(Vector2.zero,edge.Item1,edge.Item2)>0){hull.Add(edge.Item1);hull.Add(edge.Item2);}
                    else {hull.Add(edge.Item2);hull.Add(edge.Item1);}
                }
                int openings=0;
                for(int i=0;i<hull.Count;i++)
                {
                    Vector2 a=hull[i],b=hull[(i+1)%hull.Count];float ay=floorHeights[a],by=floorHeights[b];
                    bool opening=portEdges.Any(e=>(Vector2.Distance(e.Item1,a)<.001f&&Vector2.Distance(e.Item2,b)<.001f)||(Vector2.Distance(e.Item2,a)<.001f&&Vector2.Distance(e.Item1,b)<.001f));
                    if(opening)openings++;else Quad(World(a,ay),World(b,by),World(b,by+height),World(a,ay+height),0);
                    var center=new Vector2(0,0);float centerFloor=floorHeights.Values.Min()-1f;float centerRoof=floorHeights.Values.Max()+height;
                    Triangle(World(center,centerFloor),World(b,by),World(a,ay),0,100+encounter.Number*10);
                    Triangle(World(center,centerRoof),World(a,ay+height),World(b,by+height),2,101+encounter.Number*10);
                }
                if(openings!=3)throw new InvalidOperationException($"分岐{encounter.Number}の3開口を接続できません ({openings})。points={string.Join(";",points)} hull={string.Join(";",hull)}");
                wrongTable.Frame(wrongTable.Length,out var endP,out var endR);
                Pipe(wrongTable,branchDistance,wrongTable.Length,ports[2],new Port{Center=endP+endR*Vector3.up*settings.CenterHeight,Rotation=endR},true,200+encounter.Number*10);
                cursor=encounter.RoomExit;
            }
            // 落下は細い縦通路。下の通路の天井は正面壁の開口の向こうへ接続する。
            if(cursor<shaftEntry-.001f)Pipe(table,cursor,shaftEntry,MainPort(cursor),MainPort(shaftEntry,shaftRotation),false,300);
            Port shaftIn=MainPort(shaftEntry,shaftRotation),shaftOut=MainPort(shaftExit,shaftRotation);
            Vector3 shaftOrigin=shaftIn.Center;
            Vector3 ShaftLocal(Vector3 p)=>Quaternion.Inverse(shaftRotation)*(p-shaftOrigin);
            Vector3 ShaftWorld(float x,float y,float z)=>shaftOrigin+shaftRotation*new Vector3(x,y,z);
            Vector3 exitLocal=ShaftLocal(shaftOut.Center);float half=3.4f,yMin=exitLocal.y-height*.5f-.1f,yMax=height*.5f+1f;
            void Face(float a,float b,float c,float d,float z)=>Quad(ShaftWorld(a,b,z),ShaftWorld(c,b,z),ShaftWorld(c,d,z),ShaftWorld(a,d,z),0);
            foreach(var port in new[]{Vector3.zero,exitLocal})
            {
                float left=port.x-width*.5f,right=port.x+width*.5f,bottom=port.y-height*.5f,top=port.y+height*.5f;
                Face(-half,yMin,left,yMax,port.z);Face(right,yMin,half,yMax,port.z);Face(left,yMin,right,bottom,port.z);Face(left,top,right,yMax,port.z);
            }
            Quad(ShaftWorld(-half,yMin,0),ShaftWorld(-half,yMax,0),ShaftWorld(-half,yMax,exitLocal.z),ShaftWorld(-half,yMin,exitLocal.z),0);
            Quad(ShaftWorld(half,yMin,0),ShaftWorld(half,yMax,0),ShaftWorld(half,yMax,exitLocal.z),ShaftWorld(half,yMin,exitLocal.z),1);
            Quad(ShaftWorld(-half,yMax,0),ShaftWorld(half,yMax,0),ShaftWorld(half,yMax,exitLocal.z),ShaftWorld(-half,yMax,exitLocal.z),2);
            Quad(ShaftWorld(-half,yMin,0),ShaftWorld(half,yMin,0),ShaftWorld(half,yMin,exitLocal.z),ShaftWorld(-half,yMin,exitLocal.z),0);
            table.Frame(table.Length,out var finalP,out var finalR);
            Pipe(table,shaftExit,table.Length,shaftOut,new Port{Center=finalP+finalR*Vector3.up*settings.CenterHeight,Rotation=finalR},false,400);
            var sums=new Dictionary<(int,int,int,int),Vector3>();
            (int,int,int,int) Key(int i)=>(Mathf.RoundToInt(vertices[i].x*10000),Mathf.RoundToInt(vertices[i].y*10000),Mathf.RoundToInt(vertices[i].z*10000),groups[i]);
            for(int i=0;i<vertices.Count;i++)if(groups[i]>=0){var k=Key(i);sums[k]=sums.TryGetValue(k,out var n)?n+normals[i]:normals[i];}
            for(int i=0;i<vertices.Count;i++)if(groups[i]>=0)normals[i]=sums[Key(i)].normalized;
            var mesh=new Mesh{name="Two permanent junctions and enclosed fall",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetNormals(normals);mesh.subMeshCount=triangles.Length;
            for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateTangents();mesh.RecalculateBounds();
            string folder="Assets/YasudaHaruto/Data/RollerCoaster/Enclosures/Generated";RailSplineMeshGenerator.EnsureFolder(folder);
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(folder+"/SmoothEnclosure.asset"));
            var root=new GameObject("Generated Enclosures");root.transform.SetParent(settings.transform,false);root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterials=settings.Palette;
            Undo.RegisterCreatedObjectUndo(root,"Generate two junction enclosures");Undo.RecordObject(settings,"Set enclosure mesh");if(settings.GeneratedRoot!=null)Undo.DestroyObjectImmediate(settings.GeneratedRoot);
            settings.SetGeneratedRoot(root);EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(mesh);
        }
    }
}
