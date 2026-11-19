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
            var inwardCenters=new Dictionary<int,Vector3>();
            var triangles=Enumerable.Range(0,settings.Palette.Length).Select(_=>new List<int>()).ToArray();
            var random=new System.Random(settings.ColorSeed);int color=0,remaining=0,last=-1;var bag=new List<int>();
            int NextColor()
            {
                if(remaining-->0)return color;
                if(bag.Count==0){bag.AddRange(Enumerable.Range(0,settings.Palette.Length));for(int i=bag.Count-1;i>0;i--){int j=random.Next(i+1);(bag[i],bag[j])=(bag[j],bag[i]);}}
                int n=bag.FindIndex(x=>x!=last);if(n<0)n=0;color=bag[n];bag.RemoveAt(n);last=color;remaining=random.Next(1,4)-1;return color;
            }
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material,float v0=0,float v1=-1,int group=-1,bool subdivide=true)
            {
                if(Vector3.Cross(b-a,d-a).sqrMagnitude+Vector3.Cross(c-b,d-b).sqrMagnitude<1e-12f)return;
                if(subdivide&&group>=500&&settings.CornerRadius>0)
                {
                    int nx=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(Vector3.Distance(a,b),Vector3.Distance(c,d))/.35f));
                    int ny=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(Vector3.Distance(a,d),Vector3.Distance(b,c))/.35f));
                    if(nx>1||ny>1)
                    {
                        Vector3 P(float x,float y)=>Vector3.Lerp(Vector3.Lerp(a,b,x),Vector3.Lerp(d,c,x),y);
                        for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)
                            Quad(P((float)x/nx,(float)y/ny),P((float)(x+1)/nx,(float)y/ny),P((float)(x+1)/nx,(float)(y+1)/ny),P((float)x/nx,(float)(y+1)/ny),material,group:group,subdivide:false);
                        return;
                    }
                }
                int n=vertices.Count;foreach(var p in new[]{a,b,c,d})vertices.Add(settings.transform.InverseTransformPoint(p));
                float across=(Vector3.Distance(a,b)+Vector3.Distance(c,d))*.5f;
                if(v1<0)v1=(Vector3.Distance(a,d)+Vector3.Distance(b,c))*.5f;
                uv.AddRange(new[]{new Vector2(0,v0),new Vector2(across,v0),new Vector2(across,v1),new Vector2(0,v1)});
                Vector3 normal=settings.transform.InverseTransformDirection(-(Vector3.Cross(b-a,d-a)+Vector3.Cross(c-b,d-b)).normalized);
                for(int i=0;i<4;i++){normals.Add(normal);groups.Add(group);}
                triangles[material%triangles.Length].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            void Triangle(Vector3 a,Vector3 b,Vector3 c,int material,int group)
            {
                if(Vector3.Cross(b-a,c-a).sqrMagnitude<1e-12f)return;
                int n=vertices.Count;foreach(var p in new[]{a,b,c})vertices.Add(settings.transform.InverseTransformPoint(p));
                Vector3 u=(b-a).normalized;Vector3 normal=-Vector3.Cross(b-a,c-a).normalized;Vector3 v=Vector3.Cross(normal,u);
                uv.Add(Vector2.zero);uv.Add(new Vector2(Vector3.Distance(a,b),0));uv.Add(new Vector2(Vector3.Dot(c-a,u),Vector3.Dot(c-a,v)));
                for(int i=0;i<3;i++){normals.Add(settings.transform.InverseTransformDirection(normal));groups.Add(group);}
                triangles[material%triangles.Length].AddRange(new[]{n,n+1,n+2});
            }
            float MainWidth(float distance)=>width;
            Vector3[] Ring(Port port)=>new[]{port.Center+port.Rotation*new Vector3(-(port.Width>0?port.Width:width)*.5f,-height*.5f,0),port.Center+port.Rotation*new Vector3((port.Width>0?port.Width:width)*.5f,-height*.5f,0),
                port.Center+port.Rotation*new Vector3((port.Width>0?port.Width:width)*.5f,height*.5f,0),port.Center+port.Rotation*new Vector3(-(port.Width>0?port.Width:width)*.5f,height*.5f,0)};
            var profile=new List<(float V,Vector2 Position)>();
            if(settings.UseSourceDuct)
            {
                var points=settings.SourceMesh.vertices;var coordinates=settings.SourceMesh.uv;var bounds=settings.SourceMesh.bounds;
                for(int i=0;i<points.Length;i++)if(Mathf.Abs(points[i].z-bounds.max.z)<.000001f)
                    profile.Add((coordinates[i].y,new Vector2((points[i].x-bounds.center.x)/bounds.extents.x,(points[i].y-bounds.center.y)/bounds.extents.y)));
                profile=profile.GroupBy(p=>Mathf.RoundToInt(p.V*100000)).Select(g=>g.First()).OrderBy(p=>p.V).ToList();
                if(profile.Count<9)throw new InvalidOperationException("新ductの開口断面を取得できません。");
            }
            Vector2 SourcePoint(float v)
            {
                v=Mathf.Repeat(v,1f);int i=1;while(i<profile.Count-1&&profile[i].V<v)i++;
                return Vector2.Lerp(profile[i-1].Position,profile[i].Position,Mathf.InverseLerp(profile[i-1].V,profile[i].V,v));
            }
            Vector3[] RoundedRing(Port port,float blend)
            {
                var corners=new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)};var ring=new Vector3[16];
                for(int i=0;i<ring.Length;i++)
                {
                    int side=i/4;Vector2 rectangle=Vector2.Lerp(corners[side],corners[(side+1)%4],(i%4)/4f);
                    Vector2 point=Vector2.Lerp(rectangle,SourcePoint(.875f+i/16f),blend);
                    ring[i]=port.Center+port.Rotation*new Vector3(point.x*(port.Width>0?port.Width:width)*.5f,point.y*height*.5f,0);
                }
                return ring;
            }
            Port MainPort(float distance,Quaternion? fixedRotation=null)
            {table.Frame(distance,out var p,out var r);return new Port{Center=p+r*Vector3.up*settings.CenterHeight,Rotation=fixedRotation??r,Width=MainWidth(distance)};}
            table.Frame(shaftEntry,out _,out var shaftRotation);shaftRotation=Horizontal(shaftRotation);
            void Pipe(RailDistanceTable source,float start,float end,Port first,Port final,bool cap,int groupBase)
            {
                if(end-start<.001f)return;int count=Mathf.CeilToInt((end-start)/step);var previous=settings.UseSourceDuct?RoundedRing(first,0):Ring(first);var lengths=new float[previous.Length];int material=NextColor();
                float previousDistance=start;
                for(int i=1;i<=count;i++)
                {
                    float d=Mathf.Lerp(start,end,(float)i/count);source.Frame(d,out var p,out var rotation);
                    rotation=Quaternion.Slerp(first.Rotation,rotation,Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,start+2f,d)));
                    rotation=Quaternion.Slerp(rotation,final.Rotation,Mathf.SmoothStep(0,1,Mathf.InverseLerp(end-2f,end,d)));
                    var port=i==count?final:new Port{Center=p+rotation*Vector3.up*settings.CenterHeight,Rotation=rotation,Width=source==table?MainWidth(d):width};
                    float rounded=Mathf.SmoothStep(0,1,Mathf.Min((d-start)/3f,(end-d)/3f));
                    var next=settings.UseSourceDuct?RoundedRing(port,rounded):Ring(port);
                    if(i%Mathf.Max(1,Mathf.RoundToInt(3f/step))==0)material=NextColor();
                    for(int side=0;side<previous.Length;side++)
                    {
                        int other=(side+1)%previous.Length;float advance=(Vector3.Distance(previous[side],next[side])+Vector3.Distance(previous[other],next[other]))*.5f;
                        int n=vertices.Count;
                        Quad(previous[side],previous[other],next[other],next[side],material,lengths[side],lengths[side]+advance,settings.UseSourceDuct?groupBase:groupBase+side);lengths[side]+=advance;
                        if(settings.UseSourceDuct&&vertices.Count==n+4)
                        {
                            float meters=settings.Palette[0].GetFloat("_TileMeters");float v0=(.875f+side/16f)*meters,v1=(.875f+(side+1)/16f)*meters;
                            uv[n]=new Vector2(previousDistance,v0);uv[n+1]=new Vector2(previousDistance,v1);uv[n+2]=new Vector2(d,v1);uv[n+3]=new Vector2(d,v0);
                        }
                    }
                    previous=next;previousDistance=d;
                }
                if(cap)
                {
                    if(settings.UseSourceDuct)for(int i=1;i+1<previous.Length;i++)Triangle(previous[0],previous[i],previous[i+1],material,groupBase);
                    else Quad(previous[0],previous[1],previous[2],previous[3],material);
                }
            }
            int pipeGroup=0;
            float Rooms(float cursor,RollerCoasterEncounter[] rooms)
            {
            foreach(var encounter in rooms)
            {
                if(encounter.RoomEntry>cursor)
                {
                    var first=MainPort(cursor);if(cursor==0){table.Frame(0,out var p,out var r);first=new Port{Center=p+r*Vector3.up*settings.CenterHeight,Rotation=r};}
                    Pipe(table,cursor,encounter.RoomEntry,first,MainPort(encounter.RoomEntry),false,pipeGroup);pipeGroup+=10;
                }
                var wrongTable=new RailDistanceTable(encounter.OncomingRoute);float branchDistance=encounter.OncomingRoute.GetComponent<RailSplineMesh>().StartDistance+16f;
                wrongTable.Frame(branchDistance,out var wrongP,out var wrongR);
                Port[] ports={MainPort(encounter.RoomEntry),MainPort(encounter.RoomExit),new Port{Center=wrongP+wrongR*Vector3.up*settings.CenterHeight,Rotation=wrongR}};
                course.Spline.Evaluate(0,encounter.Begin,out var fork,out var tangent,out _);
                var basis=Quaternion.LookRotation(Vector3.ProjectOnPlane(tangent,Vector3.up).normalized,Vector3.up);Vector3 origin=fork;
                Vector3 Local(Vector3 p)=>Quaternion.Inverse(basis)*(p-origin);
                Vector3 World(Vector2 p,float y)=>origin+basis*new Vector3(p.x,y,p.y);
                // 接続面の4隅を3Dのまま共有する。水平面への投影で高さ/バンクを失わない。
                var floorPoints=new Dictionary<Vector2,Vector3>();var roofPoints=new Dictionary<Vector2,Vector3>();
                var directions=new Dictionary<Vector2,Vector3>();var floorDirections=new Dictionary<Vector2,Vector3>();var roofDirections=new Dictionary<Vector2,Vector3>();var edges=new List<(Vector2,Vector2)>();
                for(int pi=0;pi<ports.Length;pi++)
                {
                    var ring=Ring(ports[pi]);Vector3 outward=ports[pi].Rotation*Vector3.forward*(pi==0?-1f:1f);
                    Vector2 Key(Vector3 p){var l=Local(p);return new Vector2(l.x,l.z);}
                    var a=Key(ring[0]);var b=Key(ring[1]);edges.Add((a,b));
                    floorPoints[a]=ring[0];floorPoints[b]=ring[1];roofPoints[a]=ring[3];roofPoints[b]=ring[2];
                    directions[a]=outward;directions[b]=outward;
                    var source=pi==2?wrongTable:table;float at=pi==0?encounter.RoomEntry:pi==1?encounter.RoomExit:branchDistance;
                    Vector3[] Nearby(float distance)
                    {source.Frame(distance,out var p,out var r);return Ring(new Port{Center=p+r*Vector3.up*settings.CenterHeight,Rotation=r,Width=width});}
                    var before=Nearby(at-.01f);var after=Nearby(at+.01f);float sign=pi==0?-1f:1f;
                    floorDirections[a]=(after[0]-before[0]).normalized*sign;floorDirections[b]=(after[1]-before[1]).normalized*sign;
                    roofDirections[a]=(after[3]-before[3]).normalized*sign;roofDirections[b]=(after[2]-before[2]).normalized*sign;
                }
                float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
                var hull=new List<Vector2>();
                foreach(var edge in edges.OrderBy(e=>Mathf.Atan2((e.Item1+e.Item2).y,(e.Item1+e.Item2).x)))
                {
                    if(Cross(edge.Item1,edge.Item2)>0){hull.Add(edge.Item1);hull.Add(edge.Item2);}
                    else {hull.Add(edge.Item2);hull.Add(edge.Item1);}
                }
                Vector3 centerFloor=floorPoints.Values.Aggregate(Vector3.zero,(sum,p)=>sum+p)/floorPoints.Count;
                Vector3 centerRoof=roofPoints.Values.Aggregate(Vector3.zero,(sum,p)=>sum+p)/roofPoints.Count;
                var floorFrames=new List<(Vector3,Quaternion)>();
                void SampleFloor(RailDistanceTable source,float from,float to)
                {
                    int count=Mathf.Max(1,Mathf.CeilToInt((to-from)/.3f));
                    for(int i=0;i<=count;i++){source.Frame(Mathf.Lerp(from,to,(float)i/count),out var p,out var r);floorFrames.Add((p,r));}
                }
                SampleFloor(table,encounter.RoomEntry,encounter.RoomExit);
                SampleFloor(wrongTable,encounter.OncomingRoute.GetComponent<RailSplineMesh>().StartDistance,branchDistance);
                float SurfaceHeight(Vector3 point,bool roof)
                {
                    double total=0,value=0;
                    foreach(var frame in floorFrames)
                    {
                        var up=frame.Item2*Vector3.up;
                        var plane=frame.Item1+up*(settings.CenterHeight+(roof?height*.5f:-height*.5f));
                        float d=(new Vector2(point.x-frame.Item1.x,point.z-frame.Item1.z)).sqrMagnitude;
                        double weight=1.0/System.Math.Pow(d+.1f,3);
                        float y=plane.y-(up.x*(point.x-plane.x)+up.z*(point.z-plane.z))/Mathf.Max(.15f,up.y);
                        value+=weight*y;total+=weight;
                    }
                    return (float)(value/total);
                }
                centerFloor.y=SurfaceHeight(centerFloor,false);centerRoof.y=SurfaceHeight(centerRoof,true);
                inwardCenters[500+encounter.Number*10]=settings.transform.InverseTransformPoint((centerFloor+centerRoof)*.5f);
                Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
                {float u=1-t;return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;}
                int openings=0;
                for(int h=0;h<hull.Count;h++)
                {
                    var a=hull[h];var b=hull[(h+1)%hull.Count];
                    bool opening=edges.Any(e=>(e.Item1==a&&e.Item2==b)||(e.Item1==b&&e.Item2==a));
                    if(opening)openings++;
                    Vector3 fa=floorPoints[a],fb=floorPoints[b],ra=roofPoints[a],rb=roofPoints[b];
                    float length=Vector3.Distance(fa,fb);int segments=Mathf.Max(2,Mathf.CeilToInt(length/step));
                    Vector3 Edge(float t,bool roof)
                    {
                        var dirs=roof?roofDirections:floorDirections;
                        var p=opening?Vector3.Lerp(roof?ra:fa,roof?rb:fb,t):Bezier(roof?ra:fa,(roof?ra:fa)-dirs[a]*length/3f,(roof?rb:fb)-dirs[b]*length/3f,roof?rb:fb,t);
                        if(!opening)p.y=Mathf.Lerp(p.y,SurfaceHeight(p,roof),Mathf.Sin(t*Mathf.PI));
                        return p;
                    }
                    Vector3 Floor(float t)=>Edge(t,false);
                    Vector3 Roof(float t)=>Edge(t,true);
                    Vector3 Inner(float t,bool roof)
                    {
                        var dirs=roof?roofDirections:floorDirections;
                        var tangent=Vector3.Lerp(-dirs[a],-dirs[b],t).normalized;
                        if(opening)return tangent;
                        return Vector3.Lerp(tangent,(centerFloor-Floor(t)).normalized,Mathf.Sin(t*Mathf.PI)).normalized;
                    }
                    Vector3 Surface(float t,float v,bool roof)
                    {
                        var start=(roof?Roof(t):Floor(t))+Inner(t,roof)*Radius(t);var center=roof?centerRoof:centerFloor;
                        var p=Bezier(start,start+Inner(t,roof)*Vector3.Distance(start,center)/3f,center,center,v);
                        p.y=Mathf.Lerp(p.y,SurfaceHeight(p,roof),Mathf.SmoothStep(0,1,Mathf.Clamp01(v/.15f)));return p;
                    }
                    float Radius(float t)=>opening?0f:settings.CornerRadius*Mathf.Sin(t*Mathf.PI);
                    Vector3 Bevel(float t,float angle,bool roof)
                    {
                        float r=Radius(t);Vector3 up=(Roof(t)-Floor(t)).normalized;
                        return (roof?Roof(t):Floor(t))+(roof?-up:up)*r*(1-Mathf.Sin(angle))+Inner(t,roof)*r*(1-Mathf.Cos(angle));
                    }
                    for(int j=0;j<segments;j++)
                    {
                        float u=(float)j/segments,v=(float)(j+1)/segments;
                        if(!opening)
                        {
                            Quad(Bevel(u,0,false),Bevel(v,0,false),Bevel(v,0,true),Bevel(u,0,true),0,group:500+encounter.Number*10);
                            for(int bevel=0;bevel<6;bevel++)
                            {
                                float a0=bevel*Mathf.PI/12f,a1=(bevel+1)*Mathf.PI/12f;
                                Quad(Bevel(u,a0,false),Bevel(v,a0,false),Bevel(v,a1,false),Bevel(u,a1,false),0,group:501+encounter.Number*10);
                                Quad(Bevel(v,a0,true),Bevel(u,a0,true),Bevel(u,a1,true),Bevel(v,a1,true),2,group:502+encounter.Number*10);
                            }
                        }
                        const int radial=12;
                        for(int k=0;k<radial;k++)
                        {
                            float t0=(float)k/radial,t1=(float)(k+1)/radial;
                            if(k==radial-1)
                            {
                                Triangle(Surface(u,t0,false),Surface(v,t0,false),centerFloor,0,501+encounter.Number*10);
                                Triangle(Surface(v,t0,true),Surface(u,t0,true),centerRoof,2,502+encounter.Number*10);
                            }
                            else
                            {
                                Quad(Surface(u,t0,false),Surface(v,t0,false),Surface(v,t1,false),Surface(u,t1,false),0,group:501+encounter.Number*10);
                                Quad(Surface(v,t0,true),Surface(u,t0,true),Surface(u,t1,true),Surface(v,t1,true),2,group:502+encounter.Number*10);
                            }
                        }
                    }
                }
                if(openings!=3)throw new InvalidOperationException("分岐の開口が接続できません。");
                wrongTable.Frame(wrongTable.Length,out var endP,out var endR);
                Pipe(wrongTable,branchDistance,wrongTable.Length,ports[2],new Port{Center=endP+endR*Vector3.up*settings.CenterHeight,Rotation=endR},true,200+encounter.Number*10);
                cursor=encounter.RoomExit;
            }
            return cursor;
            }
            float mainCursor=Rooms(0,encounters.Where(x=>x.RoomEntry<shaftEntry).ToArray());
            // 落下は細い縦通路。下の通路の天井は正面壁の開口の向こうへ接続する。
            if(mainCursor<shaftEntry-.001f)Pipe(table,mainCursor,shaftEntry,MainPort(mainCursor),MainPort(shaftEntry,shaftRotation),false,300);
            Port shaftIn=MainPort(shaftEntry,shaftRotation),shaftOut=MainPort(shaftExit,shaftRotation);
            Vector3 shaftOrigin=shaftIn.Center;
            Vector3 ShaftLocal(Vector3 p)=>Quaternion.Inverse(shaftRotation)*(p-shaftOrigin);
            Vector3 ShaftWorld(float x,float y,float z)=>shaftOrigin+shaftRotation*new Vector3(x,y,z);
            Vector3 exitLocal=ShaftLocal(shaftOut.Center);float half=3.4f,yMin=exitLocal.y-height*.5f-.1f,yMax=height*.5f+1f;
            int shaftStartVertex=vertices.Count;
            float[] Grid(float min,float max,params float[] extra)
            {
                int count=Mathf.CeilToInt((max-min)/.25f);
                return Enumerable.Range(0,count+1).Select(i=>Mathf.Lerp(min,max,(float)i/count)).Concat(extra)
                    .Where(v=>v>=min&&v<=max).GroupBy(v=>Mathf.RoundToInt(v*100000)).Select(g=>g.First()).OrderBy(v=>v).ToArray();
            }
            var gridX=Grid(-half,half,-width*.5f,width*.5f,exitLocal.x-width*.5f,exitLocal.x+width*.5f);
            var gridY=Grid(yMin,yMax,-height*.5f,height*.5f,exitLocal.y-height*.5f,exitLocal.y+height*.5f);
            var gridZ=Grid(0,exitLocal.z);
            float[] Span(float[] grid,float min,float max)=>grid.Where(v=>v>=min-.00001f&&v<=max+.00001f).Select(v=>Mathf.Clamp(v,min,max)).Distinct().ToArray();
            void Face(float a,float b,float c,float d,float z)
            {
                var xs=Span(gridX,a,c);var ys=Span(gridY,b,d);
                for(int x=0;x+1<xs.Length;x++)for(int y=0;y+1<ys.Length;y++)
                {
                    Vector3 cornerA=ShaftWorld(xs[x],ys[y],z),cornerB=ShaftWorld(xs[x+1],ys[y],z),cornerC=ShaftWorld(xs[x+1],ys[y+1],z),cornerD=ShaftWorld(xs[x],ys[y+1],z);
                    if(z==0)Quad(cornerA,cornerD,cornerC,cornerB,0,group:600,subdivide:false);else Quad(cornerA,cornerB,cornerC,cornerD,0,group:600,subdivide:false);
                }
            }
            foreach(var port in new[]{Vector3.zero,exitLocal})
            {
                float left=port.x-width*.5f,right=port.x+width*.5f,bottom=port.y-height*.5f,top=port.y+height*.5f;
                Face(-half,yMin,left,yMax,port.z);Face(right,yMin,half,yMax,port.z);Face(left,yMin,right,bottom,port.z);Face(left,top,right,yMax,port.z);
            }
            for(int y=0;y+1<gridY.Length;y++)for(int z=0;z+1<gridZ.Length;z++)foreach(float x in new[]{-half,half})
            {
                Vector3 a=ShaftWorld(x,gridY[y],gridZ[z]),b=ShaftWorld(x,gridY[y+1],gridZ[z]),c=ShaftWorld(x,gridY[y+1],gridZ[z+1]),d=ShaftWorld(x,gridY[y],gridZ[z+1]);
                if(x<0)Quad(a,d,c,b,0,group:600,subdivide:false);else Quad(a,b,c,d,0,group:600,subdivide:false);
            }
            for(int x=0;x+1<gridX.Length;x++)for(int z=0;z+1<gridZ.Length;z++)foreach(float y in new[]{yMin,yMax})
            {
                Vector3 a=ShaftWorld(gridX[x],y,gridZ[z]),b=ShaftWorld(gridX[x+1],y,gridZ[z]),c=ShaftWorld(gridX[x+1],y,gridZ[z+1]),d=ShaftWorld(gridX[x],y,gridZ[z+1]);
                if(y==yMax)Quad(a,d,c,b,0,group:602,subdivide:false);else Quad(a,b,c,d,0,group:601,subdivide:false);
            }
            Vector3 boxCenter=new Vector3(0,(yMin+yMax)*.5f,exitLocal.z*.5f);
            inwardCenters[600]=settings.transform.InverseTransformPoint(ShaftWorld(boxCenter.x,boxCenter.y,boxCenter.z));
            Vector3 extents=new Vector3(half,(yMax-yMin)*.5f,exitLocal.z*.5f);
            for(int i=shaftStartVertex;i<vertices.Count;i++)
            {
                Vector3 p=ShaftLocal(settings.transform.TransformPoint(vertices[i]));float distance=float.PositiveInfinity;
                foreach(var portal in new[]{Vector3.zero,exitLocal})
                {
                    Vector3 closest=new Vector3(Mathf.Clamp(p.x,portal.x-width*.5f,portal.x+width*.5f),Mathf.Clamp(p.y,portal.y-height*.5f,portal.y+height*.5f),portal.z);
                    distance=Mathf.Min(distance,Vector3.Distance(p,closest));
                }
                float r=settings.CornerRadius;Vector3 local=p-boxCenter;
                Vector3 inner=extents-Vector3.one*r;Vector3 core=new Vector3(Mathf.Clamp(local.x,-inner.x,inner.x),Mathf.Clamp(local.y,-inner.y,inner.y),Mathf.Clamp(local.z,-inner.z,inner.z));
                Vector3 rounded=core+(local-core).normalized*r+boxCenter;
                p=Vector3.Lerp(p,rounded,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.05f,.7f,distance)));
                vertices[i]=settings.transform.InverseTransformPoint(ShaftWorld(p.x,p.y,p.z));
            }
            table.Frame(table.Length,out var finalP,out var finalR);
            mainCursor=Rooms(shaftExit,encounters.Where(x=>x.RoomEntry>=shaftEntry).ToArray());
            Pipe(table,mainCursor,table.Length,MainPort(mainCursor),new Port{Center=finalP+finalR*Vector3.up*settings.CenterHeight,Rotation=finalR},false,400);
            for(int i=0;i<vertices.Count;i++)if(groups[i]>=500)
            {var world=settings.transform.TransformPoint(vertices[i]);uv[i]=groups[i]%10==0?new Vector2(world.x+world.z,world.y):new Vector2(world.x,world.z);}
            var sums=new Dictionary<(int,int,int,int),Vector3>();
            (int,int,int,int) Key(int i)=>(Mathf.RoundToInt(vertices[i].x*10000),Mathf.RoundToInt(vertices[i].y*10000),Mathf.RoundToInt(vertices[i].z*10000),settings.CornerRadius>0&&groups[i]>=500?groups[i]/10*10:groups[i]);
            foreach(var indices in triangles)for(int i=0;i<indices.Count;i+=3)
            {
                int a=indices[i],b=indices[i+1],c=indices[i+2];Vector3 normal=-Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).normalized;
                if(inwardCenters.TryGetValue(groups[a]/10*10,out var center)&&Vector3.Dot(normal,center-(vertices[a]+vertices[b]+vertices[c])/3f)<0)normal=-normal;
                foreach(int vertex in new[]{a,b,c})if(groups[vertex]>=0){var key=Key(vertex);sums[key]=sums.TryGetValue(key,out var value)?value+normal:normal;}
            }
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
