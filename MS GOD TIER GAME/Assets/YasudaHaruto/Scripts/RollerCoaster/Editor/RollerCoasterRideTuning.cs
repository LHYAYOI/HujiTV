using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace YasudaHaruto.RollerCoaster.Editor
{
    [InitializeOnLoad]
    internal static class RollerCoasterRideTuning
    {
        private const string Active="Yasuda.Coaster.TuningValidation";
        private const string Output="Temp/RollerCoasterRideTuning";
        private static double s_start;
        private static bool s_started,s_creep,s_floating,s_banked,s_capturedDark;
        private static float s_baseY,s_baseFov,s_maxFov,s_minCrestSpeed=float.PositiveInfinity;
        private static int s_errors;
        private static bool s_restarted;
        private static float s_lastLaunchSpeed;
        private static bool s_bright,s_dark,s_returnBank;
        private static float s_maxGapSpeed,s_minGapSpeed=float.PositiveInfinity;
        private static float s_minClearance=float.PositiveInfinity;
        private static float s_minBodyGap=float.PositiveInfinity;
        private static Vector3 s_previousPlayer,s_previousCart;
        private static Quaternion s_previousPlayerRotation,s_previousCartRotation;
        private static bool s_hasPrevious;
        private static float[] s_joinViews;
        private static float[] s_launchViews;
        private static int s_launchShot;
        private static bool s_dropCreep,s_dropTilt,s_dropFast;
        private static bool s_brakeCue,s_brakeInertia,s_brakeHeld;
        private static bool s_edgePause,s_tipHold,s_sparks;
        private static float s_previousTip,s_previousFallSpeed;
        private static float s_minBrakeSpeed;
        private static bool s_brakeRattle,s_smoothRelease;
        private static bool s_slipObserved;
        private static bool s_continuousPitch,s_receiverVisible;
        private static float s_previousDropPitch;
        private static int s_poseFrame;
        private static UnityEngine.Object s_activeCamera;
        private static int s_dropPauseStage;
        private static double s_dropPauseAt;
        private static float s_dropPauseWeight,s_dropPauseProgress;
        private static int s_joinShot;
        private static float s_lastOpponentDistance=float.PositiveInfinity;
        private static BoxCollider s_playerBox,s_opponentBox;
        private static bool[] s_close=new bool[1];
        private static bool[] s_encounters=new bool[1];
        static RollerCoasterRideTuning()
        {
            EditorApplication.update+=Tick;
            Application.logMessageReceived+=(message,stack,type)=>
            {
                if(SessionState.GetBool(Active,false)&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))
                {s_errors++;File.AppendAllText(Output+"/ride-tuning-result.txt",message+"\n");}
            };
        }
        private static void Float(UnityEngine.Object target,string name,float value)
        {var data=new SerializedObject(target);data.FindProperty(name).floatValue=value;data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(target);}
        public static void ApplyAndValidate()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit mode required.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/YasudaHaruto/Scenes/RollerCoaster.unity")
                throw new InvalidOperationException("Open the dedicated RollerCoaster scene first.");
            var course=UnityEngine.Object.FindFirstObjectByType<RollerCoasterCourse>();
            var ride=UnityEngine.Object.FindFirstObjectByType<RollerCoasterTrialRide>();
            bool rebuild=Mathf.Abs(course.SpiralBank-30f)>.01f||Mathf.Abs(course.ReturnBank-22f)>.01f;
            float ratio=12f/course.FirstHillHeight;
            void Height(SplineContainer container)
            {
                if(container==null)return;
                var spline=container.Spline;float ground=spline[course.FallEndKnot].Position.y;
                for(int i=course.FallEndKnot+1;i<spline.Count;i++)
                {
                    var k=spline[i];Quaternion old=k.Rotation;
                    Vector3 incoming=old*(Vector3)k.TangentIn,outgoing=old*(Vector3)k.TangentOut,forward=old*Vector3.forward;
                    k.Position.y=ground+(k.Position.y-ground)*ratio;
                    incoming.y*=ratio;outgoing.y*=ratio;forward.y*=ratio;
                    Quaternion rotation=Quaternion.LookRotation(forward,Vector3.up);
                    k.Rotation=rotation;k.TangentIn=Quaternion.Inverse(rotation)*incoming;k.TangentOut=Quaternion.Inverse(rotation)*outgoing;
                    spline.SetKnot(i,k);
                }
                EditorUtility.SetDirty(container);
            }
            if(Mathf.Abs(ratio-1f)>.0001f)Height(course.Spline);
            var sequence=course.GetComponentInChildren<RollerCoasterEncounterSequence>(true);
            if(sequence!=null&&Mathf.Abs(ratio-1f)>.0001f)Height(sequence.Baseline);
            Float(course,"m_firstHillHeight",12f);Float(course,"m_secondHillHeight",8.8f);
            Float(course,"m_exitDrop",course.ExitDrop*ratio);
            Float(course,"m_spiralBank",40f);Float(course,"m_returnBank",22f);
            RollerCoasterTwoEncounterSetup.Build(course);
            foreach(var encounter in course.GetComponentsInChildren<RollerCoasterEncounter>(true))
            {
                var data=new SerializedObject(encounter);var renderers=data.FindProperty("m_branchRenderers");
                renderers.arraySize=2;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue=encounter.OncomingRoute.GetComponent<RailSplineMesh>().GeneratedRoot.GetComponent<Renderer>();
                renderers.GetArrayElementAtIndex(1).objectReferenceValue=course.AfterFall.GeneratedRoot.GetComponent<Renderer>();
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            RollerCoasterEnclosureBuilder.Generate(course.GetComponentInChildren<RollerCoasterEnclosures>());
            var profile=ride.GetComponent<RollerCoasterSpeedProfile>();
            Float(profile,"m_crestStopSeconds",0);Float(profile,"m_crestApproachSpeed",.65f);Float(profile,"m_crestLaunchSeconds",3.2f);
            Float(profile,"m_descentSpeed",36);Float(profile,"m_spiralSpeed",24);Float(profile,"m_fallSpeed",16);
            Float(ride,"m_fallPitch",27f);
            foreach(var encounter in course.GetComponentsInChildren<RollerCoasterEncounter>(true))
            {
                Float(encounter,"m_triggerLeadMeters",encounter.Number==1?13:11);
                // 接近速度と開始位置は分岐生成時に通過時刻から計算する。
            }
            // 専用Scene内の既存照明を消し、プレイヤーの前方ライトへ置き換える。
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {light.enabled=false;EditorUtility.SetDirty(light);}
            var rideData=new SerializedObject(ride);
            var headlight=rideData.FindProperty("m_trialLight").objectReferenceValue as Light;
            headlight.name="Ride Headlight";headlight.transform.SetParent(ride.Cart.transform,false);
            headlight.transform.localPosition=new Vector3(0,.85f,.65f);headlight.transform.localRotation=Quaternion.Euler(5,0,0);
            headlight.type=LightType.Spot;headlight.range=28;headlight.spotAngle=105;headlight.innerSpotAngle=75;
            headlight.intensity=10;headlight.color=new Color(1f,.91f,.76f);headlight.shadows=LightShadows.Soft;headlight.enabled=false;
            EditorUtility.SetDirty(headlight);
            var lamps=AssetDatabase.LoadAssetAtPath<Material>("Assets/YasudaHaruto/Data/RollerCoaster/Encounter/Cart Lamps.mat");
            lamps.shader=Shader.Find("Universal Render Pipeline/Unlit");lamps.SetColor("_BaseColor",new Color(2f,1.8f,1.1f));EditorUtility.SetDirty(lamps);
            foreach(var encounter in course.GetComponentsInChildren<RollerCoasterEncounter>(true))
            {
                RollerCoasterEncounterSetup.EnsureCargoLight(encounter.OncomingCart);
                encounter.OncomingCart.Find("Cargo Light").GetComponent<Light>().enabled=true;
            }
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.32f,.34f,.38f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=0f;RenderSettings.fogColor=new Color(.002f,.003f,.005f);
            ride.RideCamera.clearFlags=CameraClearFlags.SolidColor;ride.RideCamera.backgroundColor=RenderSettings.fogColor;
            var ductLighting=ride.GetComponent<RollerCoasterDuctLighting>();
            if(ductLighting==null)ductLighting=ride.gameObject.AddComponent<RollerCoasterDuctLighting>();
            ductLighting.Configure(ride);EditorUtility.SetDirty(ductLighting);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(course.gameObject.scene);
            StartValidation();
        }
        internal static void StartValidation()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output+"/ride-tuning-result.txt","Saved tuning. Beginning full Play Mode ride.\n");
            s_started=s_creep=s_floating=s_banked=s_capturedDark=false;s_errors=0;
            s_restarted=false;s_lastLaunchSpeed=0;
            s_dropCreep=s_dropTilt=s_dropFast=false;s_dropPauseStage=0;
            s_brakeCue=s_brakeInertia=s_brakeHeld=false;
            s_edgePause=s_tipHold=s_sparks=false;s_previousTip=s_previousFallSpeed=0f;
            s_minBrakeSpeed=float.PositiveInfinity;
            s_brakeRattle=s_smoothRelease=false;
            s_slipObserved=false;
            s_continuousPitch=s_receiverVisible=false;s_previousDropPitch=0f;s_poseFrame=-1;s_activeCamera=null;
            s_bright=s_dark=s_returnBank=false;s_maxGapSpeed=0;s_minGapSpeed=float.PositiveInfinity;
            s_encounters=new bool[1];s_close=new bool[1];s_minClearance=float.PositiveInfinity;s_minBodyGap=float.PositiveInfinity;s_hasPrevious=false;s_lastOpponentDistance=float.PositiveInfinity;
            s_maxFov=0;s_minCrestSpeed=float.PositiveInfinity;
            SessionState.SetBool(Active,true);EditorApplication.isPlaying=true;
        }
        private static void Capture(Camera camera,string name)
        {
            var rt=new RenderTexture(960,540,24);var prior=camera.targetTexture;var active=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(960,540,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();
            File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            camera.targetTexture=prior;RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
            if((name.StartsWith("join-view-")||name=="ride-banked")&&!name.EndsWith("-geometry"))
            {
                var enclosure=UnityEngine.Object.FindFirstObjectByType<RollerCoasterEnclosures>();
                var renderer=enclosure.GeneratedRoot.GetComponent<MeshRenderer>();var originals=renderer.sharedMaterials;
                var gray=new Material(Shader.Find("Universal Render Pipeline/Lit"));gray.SetFloat("_Cull",0);gray.SetColor("_BaseColor",new Color(.5f,.5f,.5f));
                renderer.sharedMaterials=Enumerable.Repeat(gray,originals.Length).ToArray();
                try {Capture(camera,name+"-geometry");}finally {renderer.sharedMaterials=originals;UnityEngine.Object.DestroyImmediate(gray);}
            }
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && File.Exists(Output+"/ride-tuning-request.txt"))
            {
                var request=File.ReadAllText(Output+"/ride-tuning-request.txt").Trim();
                File.Delete(Output+"/ride-tuning-request.txt");
                try
                {
                    if(request=="visibility")RollerCoasterVisibilitySetup.Apply();
                    else if(request=="duct-inspect")RollerCoasterDuctUpgrade.Inspect();
                    else if(request=="duct-upgrade")RollerCoasterDuctUpgrade.Apply();
                    else if(request=="duct-refine")RollerCoasterDuctUpgrade.Refine();
                    else if(request=="brake")RollerCoasterBrakeSetup.Apply();
                    else if(request=="gravity")RollerCoasterBrakeSetup.Apply(true);
                    else if(request=="slip")RollerCoasterBrakeSetup.ApplySlip();
                    else if(request=="continuous")RollerCoasterBrakeSetup.ApplyContinuousLean();
                    else if(request=="verify")StartValidation();
                    else if(request.StartsWith("slip-restore|",StringComparison.Ordinal))
                    {
                        string backup=request.Substring("slip-restore|".Length);
                        if(!backup.StartsWith("Assets/YasudaHaruto/Backups~/",StringComparison.Ordinal)||!backup.EndsWith(".unity",StringComparison.Ordinal)||backup.Contains(".."))throw new InvalidOperationException("Dedicated backup required");
                        const string scenePath="Assets/YasudaHaruto/Scenes/RollerCoaster.unity";
                        File.Copy(backup,scenePath,true);AssetDatabase.ImportAsset(scenePath);EditorSceneManager.OpenScene(scenePath);
                        RollerCoasterBrakeSetup.ApplySlip();
                    }
                    else if(request=="drop")
                    {
                        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                        if(scene.path!="Assets/YasudaHaruto/Scenes/RollerCoaster.unity")throw new InvalidOperationException("Dedicated scene required");
                        Directory.CreateDirectory("Assets/YasudaHaruto/Backups~");
                        EditorSceneManager.SaveScene(scene,"Assets/YasudaHaruto/Backups~/RollerCoaster-before-drop-anticipation-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
                        var profile=UnityEngine.Object.FindFirstObjectByType<RollerCoasterSpeedProfile>();
                        Float(profile,"m_dropBrakeDistance",9f);Float(profile,"m_dropCreepSpeed",.18f);
                        Float(profile,"m_dropAnticipationSeconds",1.65f);Float(profile,"m_dropGravity",9.81f);Float(profile,"m_dropTipDegrees",38f);
                        EditorSceneManager.SaveScene(scene);StartValidation();
                    }
                    else ApplyAndValidate();
                }
                catch(Exception error){File.WriteAllText(Output+"/ride-tuning-result.txt","SETUP FAIL "+error);}
                return;
            }
            if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            try
            {
                var ride=UnityEngine.Object.FindFirstObjectByType<RollerCoasterTrialRide>();
                if(ride==null)return;
                var course=ride.Course;var motion=ride.GetComponent<RollerCoasterCameraMotion>();
                if(!s_started)
                {
                    var encounter=course.GetComponentsInChildren<RollerCoasterEncounter>(true).Single();
                    var distanceTable=new RailDistanceTable(course.Spline);
                    s_joinViews=new[]{distanceTable.Parameter(encounter.RoomEntry-2),distanceTable.Parameter(encounter.RoomEntry+2),distanceTable.Parameter(encounter.RoomExit-2),distanceTable.Parameter(encounter.RoomExit+2)};
                    s_joinShot=0;s_launchShot=0;
                    float wall=distanceTable.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline,course.FallStartKnot));
                    if(Mathf.Abs(course.BeforeFall.EndDistance-wall-course.LaunchRailExtension)>.01f)throw new InvalidOperationException("Launch rail extension mismatch");
                    if(Mathf.Abs(distanceTable.DistanceAtParameter(course.FallLaunchParameter)-course.BeforeFall.EndDistance)>.02f)throw new InvalidOperationException("Airborne state does not start at rail end");
                    s_launchViews=new[]{distanceTable.Parameter(wall-2f),distanceTable.Parameter(wall+.8f),distanceTable.Parameter(course.BeforeFall.EndDistance+.5f)};
                    s_started=true;s_start=EditorApplication.timeSinceStartup;Time.timeScale=1;
                    s_baseY=motion.VirtualCamera.transform.localPosition.y;s_baseFov=motion.VirtualCamera.Lens.FieldOfView;
                }
                float p=ride.Progress;
                if(!s_restarted&&motion.LandingImpactCount==1&&motion.LandingEffectElapsed>=.15f)
                {
                    if(!s_dropCreep||!s_dropTilt||!s_dropFast||s_dropPauseStage!=2)throw new InvalidOperationException("First drop anticipation/tilt/acceleration/pause not observed");
                    if(!s_brakeCue||!s_brakeInertia||!s_brakeHeld)throw new InvalidOperationException("Brake cue/inertia/held lean not observed");
                    if(!s_edgePause||!s_tipHold||!s_sparks||s_minBrakeSpeed>.25f)throw new InvalidOperationException("Near-zero braking / drop pauses / bilateral sparks not observed");
                    if(!s_brakeRattle||!s_smoothRelease)throw new InvalidOperationException("Brake rattle / smooth release not observed");
                    if(!s_slipObserved)throw new InvalidOperationException("Downward slip not observed");
                    if(!s_continuousPitch||!s_receiverVisible)throw new InvalidOperationException("Continuous pitch / receiving rails not observed");
                    if(!ride.GetComponent<RollerCoasterSpeedProfile>().HoldingAfterLanding||motion.VirtualCamera.transform.localPosition.y>=s_baseY-.02f)
                        throw new InvalidOperationException("Landing effect waited for cart movement");
                    File.AppendAllText(Output+"/ride-tuning-result.txt","PASS Landing effect advances immediately while the cart is stopped.\n");
                    s_restarted=true;ride.Restart();
                    if(motion.LandingImpactCount!=0||Vector3.Distance(motion.VirtualCamera.transform.localPosition,new Vector3(0,1.1f,-.2f))>.02f)
                        throw new InvalidOperationException("Restart did not reset landing/camera state");
                    s_creep=s_floating=s_banked=s_capturedDark=s_bright=s_dark=s_returnBank=false;
                    s_encounters=new bool[1];s_close=new bool[1];s_minClearance=float.PositiveInfinity;s_minBodyGap=float.PositiveInfinity;s_hasPrevious=false;s_lastOpponentDistance=float.PositiveInfinity;
                    s_maxGapSpeed=0;s_minGapSpeed=float.PositiveInfinity;s_lastLaunchSpeed=0;
                    s_dropCreep=s_dropTilt=s_dropFast=false;s_dropPauseStage=0;
                    s_brakeCue=s_brakeInertia=s_brakeHeld=false;
                    s_edgePause=s_tipHold=s_sparks=false;s_previousTip=s_previousFallSpeed=0f;
                    s_minBrakeSpeed=float.PositiveInfinity;
                    s_brakeRattle=s_smoothRelease=false;
                    s_slipObserved=false;
                    s_continuousPitch=s_receiverVisible=false;s_previousDropPitch=0f;s_poseFrame=-1;s_activeCamera=null;
                    var resetProfile=ride.GetComponent<RollerCoasterSpeedProfile>();
                    if(resetProfile.AnticipatingDrop||resetProfile.DropTipWeight!=0f||resetProfile.DropBrakeStarted||resetProfile.DropBrakeElapsed!=0f)throw new InvalidOperationException("Restart retained drop anticipation/brake inertia");
                    File.AppendAllText(Output+"/ride-tuning-result.txt","PASS Restart during landing resets camera and impact state. Replaying full route.\n");return;
                }
                if(s_launchShot<s_launchViews.Length&&p>=s_launchViews[s_launchShot]){Capture(ride.RideCamera,"launch-visibility-"+s_launchShot);s_launchShot++;}
                if(s_joinShot<s_joinViews.Length&&p>=s_joinViews[s_joinShot]){Capture(ride.RideCamera,"join-view-"+s_joinShot);s_joinShot++;}
                var lighting=ride.GetComponent<RollerCoasterDuctLighting>();
                if(lighting.Darkness<.01f){s_bright=true;if(!File.Exists(Output+"/ride-bright-start.png"))Capture(ride.RideCamera,"ride-bright-start");}
                if(lighting.Darkness>.99f)s_dark=true;
                if(p>RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline,course.HillStartKnot))
                {
                    course.Spline.Evaluate(0,p,out _,out var returnTangent,out var returnUp);
                    float angle=Quaternion.Angle(Quaternion.LookRotation(returnTangent,Vector3.up),Quaternion.LookRotation(returnTangent,returnUp));
                    if(angle>15)s_returnBank=true;
                }
                float crest=RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline,course.CrestKnot);
                float spiral=RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline,course.SpiralStartKnot);
                if(p>crest-.005f&&p<spiral)
                {
                    if(ride.Cart.Speed<=0||ride.GetComponent<RollerCoasterSpeedProfile>().HoldingAtCrest)throw new InvalidOperationException("Crest unexpectedly stopped");
                    if(ride.Cart.Speed>s_lastLaunchSpeed&&s_lastLaunchSpeed>0)
                    {
                        float step=ride.Cart.Speed-s_lastLaunchSpeed;
                        if(step>22f*Time.deltaTime+.3f)throw new InvalidOperationException("Abrupt descent acceleration");
                    }
                    s_lastLaunchSpeed=ride.Cart.Speed;
                    s_minCrestSpeed=Mathf.Min(s_minCrestSpeed,ride.Cart.Speed);if(ride.Cart.Speed<1f)s_creep=true;
                }
                float gapStart=course.FallLaunchParameter;
                float gapEnd=RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline,course.FallEndKnot);
                var dropProfile=ride.GetComponent<RollerCoasterSpeedProfile>();
                if(dropProfile.DropStage==RollerCoasterSpeedProfile.DROP_STAGE.APPROACH&&motion.BrakeRattleRotation.magnitude>.1f)s_brakeRattle=true;
                if(dropProfile.AnticipatingDrop&&motion.BrakeRattleRotation.sqrMagnitude>.0001f)throw new InvalidOperationException("Brake rattle continued after stopping");
                if(dropProfile.DropStage==RollerCoasterSpeedProfile.DROP_STAGE.FALLING&&dropProfile.DropReleaseElapsed>.05f&&dropProfile.DropReleaseElapsed<.2f)
                {
                    if(dropProfile.DropReleaseTiltWeight>.2f||motion.VirtualCamera.transform.localPosition.y<s_baseY-.075f)
                        throw new InvalidOperationException("Abrupt release tilt or downward camera jolt remains");
                    s_smoothRelease=true;
                }
                if(dropProfile.DropBrakeStarted&&dropProfile.DropStage==RollerCoasterSpeedProfile.DROP_STAGE.APPROACH&&ride.Cart.Speed>0)
                    s_minBrakeSpeed=Mathf.Min(s_minBrakeSpeed,ride.Cart.Speed);
                var brakeSparks=ride.GetComponent<RollerCoasterBrakeSparks>();
                if(brakeSparks!=null&&dropProfile.DropBrakeElapsed>.24f&&brakeSparks.LeftParticleCount>0&&brakeSparks.RightParticleCount>0&&!s_sparks)
                {
                    if(brakeSparks.LastRelativeForwardSpeed>=0)throw new InvalidOperationException("Brake sparks fly forward relative to cart");
                    s_sparks=true;Capture(ride.RideCamera,"brake-sparks");
                }
                if(dropProfile.DropStage==RollerCoasterSpeedProfile.DROP_STAGE.EDGE_PAUSE)
                {
                    float remaining=(gapStart-p)*course.Spline.CalculateLength();
                    if(ride.Cart.Speed!=0f||remaining<.3f||remaining>1f)throw new InvalidOperationException("Initial edge pause did not stop just before rail end");
                    if(!s_edgePause)File.AppendAllText(Output+"/ride-tuning-result.txt",$"Edge pause at {remaining:F2}m, braking min positive speed {s_minBrakeSpeed:F3}.\n");
                    s_edgePause=true;
                }
                if(dropProfile.DropStage==RollerCoasterSpeedProfile.DROP_STAGE.TIP_HOLD)
                {if(ride.Cart.Speed>.01f)throw new InvalidOperationException("Tipped hold did not stop");s_tipHold=true;}
                if(dropProfile.DropStage==RollerCoasterSpeedProfile.DROP_STAGE.CREEPING)
                {
                    if(dropProfile.DropTipWeight+.001f<s_previousTip)throw new InvalidOperationException("Tip reversed while creeping");
                    if(dropProfile.DropTipWeight-s_previousTip>2f*Time.deltaTime+.03f)throw new InvalidOperationException("Tip stepped abruptly");
                    s_previousTip=dropProfile.DropTipWeight;
                }
                if(dropProfile.DropStage==RollerCoasterSpeedProfile.DROP_STAGE.FALLING&&p<gapEnd&&!ride.Paused)
                {
                    if(ride.Cart.Speed+.01f<s_previousFallSpeed)throw new InvalidOperationException("Gravity fall slowed down");
                    if(ride.Cart.Speed-s_previousFallSpeed>10f*Time.deltaTime+1f)throw new InvalidOperationException("Fall accelerated discontinuously");
                    s_previousFallSpeed=ride.Cart.Speed;
                }
                if(dropProfile.DropBrakeStarted&&!s_brakeCue)
                {
                    float remaining=(gapStart-p)*course.Spline.CalculateLength();
                    if(Mathf.Abs(remaining-dropProfile.DropBrakeDistance)>1f)throw new InvalidOperationException("Brake cue timing drifted");
                    float straight=RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline,course.SpiralEndKnot);
                    if(Mathf.Abs((p-straight)*course.Spline.CalculateLength())>1f)throw new InvalidOperationException("Braking did not begin at the straight entry");
                    s_brakeCue=true;Capture(ride.RideCamera,"brake-reveal");
                    File.AppendAllText(Output+"/ride-tuning-result.txt",$"Brake began at {remaining:F2}m before rail end.\n");
                }
                if(dropProfile.DropBrakeStarted&&dropProfile.DropBrakeElapsed>.14f&&dropProfile.DropBrakeElapsed<.35f&&!s_brakeInertia)
                {
                    if(motion.VirtualCamera.transform.localPosition.z<.05f)throw new InvalidOperationException("Braking did not throw viewpoint forward");
                    s_brakeInertia=true;Capture(ride.RideCamera,"brake-inertia");
                }
                if(dropProfile.AnticipatingDrop&&dropProfile.DropBrakeElapsed>1.1f)
                {
                    if(motion.BrakeLeanWeight<.99f||motion.VirtualCamera.transform.localPosition.z<.05f)throw new InvalidOperationException("Brake lean returned upright before falling");
                    s_brakeHeld=true;
                }
                float dropFraction=Mathf.InverseLerp(gapStart,gapEnd,p);
                if(dropProfile.DropBrakeStarted&&p<gapEnd)
                {
                    var brain=ride.RideCamera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
                    var active=brain.ActiveVirtualCamera as UnityEngine.Object;
                    if(!ride.RideCamera.enabled||active!=motion.VirtualCamera)throw new InvalidOperationException("Ride camera switched during braking/fall");
                    if(s_activeCamera!=null&&s_activeCamera!=active)throw new InvalidOperationException("Active virtual camera changed");s_activeCamera=active;
                }
                if((dropProfile.AnticipatingDrop||p>=gapStart)&&p<gapEnd&&dropFraction<.6f&&s_poseFrame!=Time.frameCount)
                {
                    Vector3 forward=ride.RideCamera.transform.forward;
                    float pitch=Mathf.Atan2(-forward.y,Vector3.ProjectOnPlane(forward,Vector3.up).magnitude)*Mathf.Rad2Deg;
                    if(s_poseFrame>=0&&pitch<s_previousDropPitch-1.25f)throw new InvalidOperationException("View pitched upward during continuous fall: "+(pitch-s_previousDropPitch));
                    s_previousDropPitch=pitch;s_poseFrame=Time.frameCount;s_continuousPitch=true;
                }
                if(dropProfile.AnticipatingDrop)
                {
                    if(p>=gapStart||ride.Cart.Speed<-.001f||ride.Cart.Speed>.4f)throw new InvalidOperationException("Drop hesitation must remain before rail tip");
                    s_dropCreep=true;
                    if(dropProfile.DropTipWeight>.95f)
                    {
                        if(Vector3.Dot(ride.Cart.transform.forward,Vector3.down)<.5f||Vector3.Dot(ride.RideCamera.transform.forward,Vector3.down)<.45f)
                            throw new InvalidOperationException("Drop anticipation did not reveal the shaft below");
                        if(!s_dropTilt)Capture(ride.RideCamera,"drop-anticipation");
                        s_dropTilt=true;
                    }
                    if(s_dropPauseStage==0&&dropProfile.DropTipWeight>.4f)
                    {
                        s_dropPauseStage=1;s_dropPauseAt=EditorApplication.timeSinceStartup;
                        s_dropPauseWeight=dropProfile.DropTipWeight;s_dropPauseProgress=p;ride.TogglePause();
                    }
                    else if(s_dropPauseStage==1&&EditorApplication.timeSinceStartup-s_dropPauseAt>.35)
                    {
                        if(Mathf.Abs(dropProfile.DropTipWeight-s_dropPauseWeight)>.001f||Mathf.Abs(p-s_dropPauseProgress)>.00001f)
                            throw new InvalidOperationException("Paused drop anticipation advanced");
                        ride.TogglePause();s_dropPauseStage=2;
                    }
                }
                if(p>gapStart&&p<gapEnd&&ride.Cart.Speed>15.9f)s_dropFast=true;
                if(p>gapStart&&p<gapEnd)
                {
                    Vector3 fromTip=ride.Cart.transform.position-(Vector3)course.Spline.EvaluatePosition(0,gapStart);
                    if(fromTip.y< -1f&&fromTip.y> -6f)
                    {
                        if(Vector3.ProjectOnPlane(fromTip,Vector3.up).magnitude>.75f)throw new InvalidOperationException("Drop throws the cart forward before descending");
                        if(!s_slipObserved)Capture(ride.RideCamera,"downward-slip");s_slipObserved=true;
                    }
                    s_maxGapSpeed=Mathf.Max(s_maxGapSpeed,ride.Cart.Speed);s_minGapSpeed=Mathf.Min(s_minGapSpeed,ride.Cart.Speed);
                    s_maxFov=Mathf.Max(s_maxFov,motion.VirtualCamera.Lens.FieldOfView);
                    if(!s_floating&&motion.VirtualCamera.transform.localPosition.y>s_baseY-.02f)
                    {s_floating=true;Capture(ride.RideCamera,"ride-floating");}
                    if(dropFraction>.65f&&!s_receiverVisible)
                    {
                        var receiver=course.transform.Find("Landing Rail Extension").GetComponent<RailSplineMesh>();
                        var receiverTable=new RailDistanceTable(receiver.Spline);
                        Vector3 end=receiver.Spline.EvaluatePosition(0,1f);
                        if(receiverTable.Length<5.99f||Vector3.Distance(end,course.Spline.EvaluatePosition(0,gapEnd))>.01f)throw new InvalidOperationException("Receiving rail extension has a gap");
                        Vector3 seen=ride.RideCamera.WorldToViewportPoint(receiver.Spline.EvaluatePosition(0,.65f));
                        if(seen.z>0&&seen.x>0&&seen.x<1&&seen.y>0&&seen.y<1)
                        {s_receiverVisible=true;Capture(ride.RideCamera,"landing-receiver");}
                    }
                }
                if(p>spiral+.01f&&p<gapStart&&!s_banked)
                {
                    course.Spline.Evaluate(0,p,out _,out var tangent,out var up);
                    var upright=Quaternion.LookRotation(tangent,Vector3.up);var bank=Quaternion.LookRotation(tangent,up);
                    if(Quaternion.Angle(upright,bank)>10){s_banked=true;Capture(ride.RideCamera,"ride-banked");}
                }
                if(!s_capturedDark&&p>crest+.02f){s_capturedDark=true;Capture(ride.RideCamera,"ride-dark-descent");}
                foreach(var encounter in course.GetComponentsInChildren<RollerCoasterEncounter>(true))
                {
                    if(encounter.Begin<=gapEnd)throw new InvalidOperationException("Old pre-fall encounter remains");
                    if(encounter.OncomingCart.gameObject.activeSelf)
                    {
                        if(!s_encounters[encounter.Number-1]&&ride.Cart.Speed<6f)throw new InvalidOperationException("Encounter before sufficient post-landing speed");
                        if(encounter.AvoidRight)throw new InvalidOperationException("Avoidance must be left");
                        if(encounter.RouteDistance>=s_lastOpponentDistance)throw new InvalidOperationException("Opponent stopped or reversed");
                        s_lastOpponentDistance=encounter.RouteDistance;
                        if(s_encounters[encounter.Number-1]&&Mathf.Abs(encounter.CurrentSpeed-encounter.ApproachSpeed)>1.5f)
                            throw new InvalidOperationException("Opponent did not maintain moving speed: "+encounter.CurrentSpeed);
                        var player=ride.Cart.transform;var opponent=encounter.OncomingCart;
                        var playerBounds=RollerCoasterCartClearance.Measure(player);var opponentBounds=RollerCoasterCartClearance.Measure(opponent);
                        for(int sample=0;sample<=16;sample++)
                        {
                            float t=s_hasPrevious?sample/16f:1f;
                            float bodyGap=RollerCoasterCartClearance.Gap(playerBounds,Vector3.Lerp(s_previousPlayer,player.position,t),Quaternion.Slerp(s_previousPlayerRotation,player.rotation,t),
                                opponentBounds,Vector3.Lerp(s_previousCart,opponent.position,t),Quaternion.Slerp(s_previousCartRotation,opponent.rotation,t));
                            if(s_playerBox==null)
                            {
                                s_playerBox=new GameObject("Validation Player Envelope").AddComponent<BoxCollider>();s_playerBox.enabled=false;
                                s_opponentBox=new GameObject("Validation Opponent Envelope").AddComponent<BoxCollider>();s_opponentBox.enabled=false;
                            }
                            s_playerBox.center=playerBounds.center;s_playerBox.size=playerBounds.size;
                            s_opponentBox.center=opponentBounds.center;s_opponentBox.size=opponentBounds.size;
                            if(Physics.ComputePenetration(s_playerBox,Vector3.Lerp(s_previousPlayer,player.position,t),Quaternion.Slerp(s_previousPlayerRotation,player.rotation,t),
                                s_opponentBox,Vector3.Lerp(s_previousCart,opponent.position,t),Quaternion.Slerp(s_previousCartRotation,opponent.rotation,t),out _,out var penetration))
                                throw new InvalidOperationException("Physics reports vehicle penetration: "+penetration);
                            s_minBodyGap=Mathf.Min(s_minBodyGap,bodyGap);
                            if(bodyGap<0f)throw new InvalidOperationException("Vehicle boxes overlap during swept pass: "+bodyGap);
                        }
                        s_previousPlayer=player.position;s_previousPlayerRotation=player.rotation;s_previousCart=opponent.position;s_previousCartRotation=opponent.rotation;s_hasPrevious=true;
                        Vector3 eye=encounter.OncomingCart.InverseTransformPoint(ride.RideCamera.transform.position)-new Vector3(0,1.1f,0);
                        Vector3 outside=new Vector3(Mathf.Max(0,Mathf.Abs(eye.x)-1f),Mathf.Max(0,Mathf.Abs(eye.y)-1.2f),Mathf.Max(0,Mathf.Abs(eye.z)-1.2f));
                        s_minClearance=Mathf.Min(s_minClearance,outside.magnitude);
                        if(outside.magnitude<.2f)throw new InvalidOperationException("Opponent intersects viewpoint");
                        if(!s_close[encounter.Number-1]&&Vector3.Distance(ride.Cart.transform.position,encounter.OncomingCart.position)<5)
                        {s_close[encounter.Number-1]=true;Capture(ride.RideCamera,"ride-close-"+encounter.Number);}
                    }
                    if(encounter.OncomingCart.gameObject.activeSelf&&!s_encounters[encounter.Number-1])
                    {s_encounters[encounter.Number-1]=true;Capture(ride.RideCamera,"ride-opponent-"+encounter.Number);}
                }
                if(ride.Finished)
                {
                    if(!s_brakeCue||!s_brakeInertia||!s_brakeHeld)throw new InvalidOperationException("Replay brake cue/inertia/held lean missing");
                    File.AppendAllText(Output+"/ride-tuning-result.txt","PASS Visible rail-end cue synchronizes braking, camera holds its forward lean into the drop, restart clears inertia.\n");
                    if(!s_dropCreep||!s_dropTilt||!s_dropFast||s_dropPauseStage!=2)throw new InvalidOperationException("Replay drop anticipation/tilt/acceleration/pause not observed");
                    File.AppendAllText(Output+"/ride-tuning-result.txt","PASS Edge pause, smooth creep/tilt, tipped hold, gravity acceleration, bilateral sparks, pause/resume and restart.\n");
                    if(!s_edgePause||!s_tipHold||!s_sparks||s_minBrakeSpeed>.25f)throw new InvalidOperationException("Replay near-zero braking / pauses / sparks missing");
                    if(!s_brakeRattle||!s_smoothRelease)throw new InvalidOperationException("Replay brake rattle / smooth release missing");
                    if(!s_slipObserved)throw new InvalidOperationException("Replay downward slip missing");
                    if(!s_continuousPitch||!s_receiverVisible)throw new InvalidOperationException("Replay continuous pitch / receiving rails missing");
                    File.AppendAllText(Output+"/ride-tuning-result.txt","PASS Same camera throughout; brake lean remains held into falling, no upward pitch reset before landing approach, receiving rails visible and joined.\n");
                    File.AppendAllText(Output+"/ride-tuning-result.txt","PASS Straight-entry brake and downward drop: horizontal travel stays below .75m during first 1-6m descent.\n");
                    File.AppendAllText(Output+"/ride-tuning-result.txt","PASS Brake rattle occurs only during deceleration; release tilt starts smoothly with no downward jolt.\n");
                    if(!s_restarted||motion.LandingImpactCount!=1||!s_bright||!s_dark||!s_returnBank||s_maxGapSpeed>dropProfile.DropTerminalSpeed+.1f||!s_creep||!s_floating||!s_banked||s_encounters.Any(x=>!x)||s_close.Any(x=>!x)||s_errors>0||s_maxFov<s_baseFov+10)
                        throw new InvalidOperationException($"Incomplete: creep={s_creep},float={s_floating},bank={s_banked},encounters={string.Join(",",s_encounters)},errors={s_errors},fov={s_maxFov}");
                    File.AppendAllText(Output+"/ride-tuning-result.txt",$"PASS full ride: crest min={s_minCrestSpeed:F3}, floating and banking observed, FOV max={s_maxFov:F1}, restart replay and landing timing verified, bright-to-dark verified, return bank verified, gap speed={s_minGapSpeed:F1}-{s_maxGapSpeed:F1}, one encounter, swept body gap={s_minBodyGap:F2}m, viewpoint clearance={s_minClearance:F2}m, errors=0.\n");
                    Time.timeScale=1;SessionState.SetBool(Active,false);EditorApplication.isPlaying=false;if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
                }
                if(EditorApplication.timeSinceStartup-s_start>240)throw new TimeoutException("Full ride exceeded 240 seconds.");
            }
            catch(Exception e)
            {
                File.AppendAllText(Output+"/ride-tuning-result.txt","FAIL "+e+"\n");SessionState.SetBool(Active,false);
                EditorApplication.isPlaying=false;if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(1);
            }
        }
    }
}
