using System;
using UnityEditor;
using UnityEngine;
namespace YasudaHaruto.RollerCoaster.Editor
{
    internal static class RollerCoasterEncounterSetup
    {
        internal static RollerCoasterEncounter Create(RollerCoasterCourse course)=>RollerCoasterTwoEncounterSetup.Build(course).Encounters[0];
        internal static void BuildCart(Transform parent)
        {
            string folder="Assets/YasudaHaruto/Data/RollerCoaster/Encounter";
            RailSplineMeshGenerator.EnsureFolder(folder);
            Material Material(string name,Color color,bool emission=false)
            {
                string path=folder+"/"+name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material!=null)return material;
                material=new Material(Shader.Find(emission?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")){name=name};
                material.SetColor("_BaseColor",color); material.SetFloat("_Smoothness",.3f);
                if(emission){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*3f);}
                AssetDatabase.CreateAsset(material,path);return material;
            }
            var body=Material("Cart Steel",new Color(.16f,.25f,.32f));
            var rubber=Material("Cart Wheels",new Color(.045f,.045f,.055f));
            var lamp=Material("Cart Lamps",new Color(1f,.9f,.55f),true);

            void Part(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material,Quaternion? rotation=null)
            {
                var piece=GameObject.CreatePrimitive(type);piece.name=name;piece.transform.SetParent(parent,false);
                piece.transform.localPosition=position;piece.transform.localScale=scale;piece.transform.localRotation=rotation??Quaternion.identity;
                UnityEngine.Object.DestroyImmediate(piece.GetComponent<Collider>());piece.GetComponent<MeshRenderer>().sharedMaterial=material;
            }
            Part("Base",PrimitiveType.Cube,new Vector3(0,.42f,0),new Vector3(1.65f,.32f,2.1f),body);
            Part("Front",PrimitiveType.Cube,new Vector3(0,.95f,.95f),new Vector3(1.7f,.8f,.16f),body);
            Part("Back",PrimitiveType.Cube,new Vector3(0,.95f,-.95f),new Vector3(1.7f,.8f,.16f),body);
            foreach(float x in new[]{-.8f,.8f})Part("Side",PrimitiveType.Cube,new Vector3(x,.95f,0),new Vector3(.13f,.8f,1.9f),body);
            foreach(float x in new[]{-.88f,.88f})foreach(float z in new[]{-.65f,.65f})
                Part("Wheel",PrimitiveType.Cylinder,new Vector3(x,.25f,z),new Vector3(.5f,.12f,.5f),rubber,Quaternion.Euler(0,0,90));
            foreach(float x in new[]{-.53f,.53f})Part("Headlamp",PrimitiveType.Sphere,new Vector3(x,1.03f,1.07f),Vector3.one*.23f,lamp);
            RollerCoasterTrashCargo.Build(parent);
            EnsureCargoLight(parent);
        }
        internal static void EnsureCargoLight(Transform parent)
        {
            if(parent.Find("Cargo Light")!=null)return;
            var go=new GameObject("Cargo Light");go.transform.SetParent(parent,false);
            go.transform.localPosition=new Vector3(0,1.9f,.35f);
            var light=go.AddComponent<Light>();light.type=LightType.Point;
            light.range=3.5f;light.intensity=1.4f;light.color=new Color(1f,.85f,.65f);
        }
    }
    [CustomEditor(typeof(RollerCoasterEncounter))]
    internal sealed class RollerCoasterEncounterEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("対向車は着地後の下り2か所で接近します。Speedは対向車の実距離速度です。経路更新は帰路のうねりと分岐を再生成します。",MessageType.Info);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if(GUILayout.Button("2か所の経路と囲いを更新"))
                    try {var course=((RollerCoasterEncounter)target).GetComponentInParent<RollerCoasterCourse>();RollerCoasterTwoEncounterSetup.Build(course);RollerCoasterEnclosureBuilder.Generate(course.GetComponentInChildren<RollerCoasterEnclosures>());}
                    catch(Exception exception){Debug.LogException(exception);}
        }
    }
}
