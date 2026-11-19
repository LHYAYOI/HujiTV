using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace YasudaHaruto.RollerCoaster.Editor
{
    // 対向車の荷物。原本の車体や走行には触れず、編集時にメッシュを保存する。
    internal static class RollerCoasterTrashCargo
    {
        private const string Folder = "Assets/YasudaHaruto/Data/RollerCoaster/Encounter";
        internal static void Build(Transform cart)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Trash Bags.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Trash Bags" };
                material.SetColor("_BaseColor", new Color(.055f, .065f, .06f));
                material.SetFloat("_Smoothness", .36f);
                AssetDatabase.CreateAsset(material, Folder + "/Trash Bags.mat");
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Trash Bag.asset");
            if (mesh == null)
            {
                mesh = CreateBag();
                AssetDatabase.CreateAsset(mesh, Folder + "/Trash Bag.asset");
            }
            var cargo = new GameObject("Trash Bag Cargo");
            cargo.transform.SetParent(cart, false);
            void Bag(string name, Vector3 position, Vector3 scale, Vector3 rotation)
            {
                var bag = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                bag.transform.SetParent(cargo.transform, false);
                bag.transform.localPosition = position;
                bag.transform.localScale = scale;
                bag.transform.localRotation = Quaternion.Euler(rotation);
                bag.GetComponent<MeshFilter>().sharedMesh = mesh;
                bag.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            Bag("Bag Front Left", new Vector3(-.37f,.58f,.42f), new Vector3(.78f,.87f,.84f), new Vector3(0,23,-8));
            Bag("Bag Front Right", new Vector3(.35f,.58f,.40f), new Vector3(.81f,.98f,.85f), new Vector3(0,-31,9));
            Bag("Bag Rear Left", new Vector3(-.36f,.58f,-.43f), new Vector3(.84f,1.02f,.86f), new Vector3(-6,62,5));
            Bag("Bag Rear Right", new Vector3(.36f,.58f,-.43f), new Vector3(.80f,.90f,.85f), new Vector3(5,118,-9));
            Bag("Bag Top", new Vector3(-.05f,1.18f,-.10f), new Vector3(.88f,.83f,.90f), new Vector3(10,-18,-16));
        }

        private static Mesh CreateBag()
        {
            const int sides = 32;
            // 膨らんだ袋→絞った口→結び目→余ったビニールの順。
            float[] heights = {0,.04f,.14f,.32f,.52f,.67f,.77f,.82f,.85f,.89f,.94f,1f};
            float[] radii = {.13f,.32f,.45f,.49f,.46f,.36f,.20f,.065f,.10f,.07f,.13f,.17f};
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int ring=0;ring<heights.Length;ring++)
                for (int side=0;side<=sides;side++)
                {
                    float a = 2*Mathf.PI*side/sides;
                    float folds = Mathf.Sin(a*9+heights[ring]*4)*.045f + Mathf.Cos(a*13-heights[ring]*3)*.025f;
                    float r = radii[ring]*(1+folds*(ring>=6?2f:1f));
                    float y = heights[ring]+(ring==heights.Length-1?Mathf.Sin(a*5)*.026f:0);
                    vertices.Add(new Vector3(Mathf.Cos(a)*r+.025f*Mathf.Sin(y*6),y,Mathf.Sin(a)*r));
                    uv.Add(new Vector2((float)side/sides,heights[ring]));
                    if(ring==0||side==sides)continue;
                    int p=(ring-1)*(sides+1)+side, q=ring*(sides+1)+side;
                    triangles.Add(p);triangles.Add(q);triangles.Add(p+1);
                    triangles.Add(p+1);triangles.Add(q);triangles.Add(q+1);
                }
            // 底と口の内側を塞ぎ、裏側から見ても穴を作らない。
            int bottom=vertices.Count;vertices.Add(new Vector3(0,0,0));uv.Add(Vector2.zero);
            int top=vertices.Count;vertices.Add(new Vector3(0,.94f,0));uv.Add(Vector2.one);
            for(int i=0;i<sides;i++)
            {
                triangles.Add(bottom);triangles.Add(i);triangles.Add(i+1);
                int p=(heights.Length-1)*(sides+1)+i;
                triangles.Add(top);triangles.Add(p+1);triangles.Add(p);
            }
            var mesh=new Mesh {name="Tied Trash Bag"};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }

        // バッチで既存2台の見た目だけ更新。経路更新は呼ばない。
        public static void ApplyToScene()
        {
            const string path="Assets/YasudaHaruto/Scenes/RollerCoaster.unity";
            var scene=EditorSceneManager.OpenScene(path);
            var encounters=UnityEngine.Object.FindObjectsByType<RollerCoasterEncounter>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            if(encounters.Length!=2)throw new InvalidOperationException("Expected two oncoming carts.");
            foreach(var encounter in encounters)
            {
                var cart=encounter.OncomingCart;
                if(cart==null)throw new InvalidOperationException("Missing cart.");
                for(int i=cart.childCount-1;i>=0;i--)
                {
                    var child=cart.GetChild(i);
                    if(child.name=="Passenger Body"||child.name=="Passenger Head"||child.name=="Raised Arm"||child.name=="Trash Bag Cargo")
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                Build(cart);
                if(cart.Find("Trash Bag Cargo").childCount!=5)throw new InvalidOperationException("Cargo incomplete.");
            }
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("Temp/RollerCoasterCargo");
            // 保存後、対向車1台を明るい確認用カメラで撮影。撮影用の変更は保存しない。
            var target=encounters[0].OncomingCart;
            target.gameObject.SetActive(true);target.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var cameraObject=new GameObject("Cargo Preview Camera");var camera=cameraObject.AddComponent<Camera>();
            camera.transform.position=new Vector3(3.6f,2.8f,4.8f);camera.transform.LookAt(new Vector3(0,1,0));
            camera.cullingMask=1<<31; foreach(var part in target.GetComponentsInChildren<Transform>(true))part.gameObject.layer=31; camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.22f,.25f,.28f);
            var lightObject=new GameObject("Cargo Preview Light");var light=lightObject.AddComponent<Light>();
            light.type=LightType.Directional;light.intensity=2.4f;light.transform.rotation=Quaternion.Euler(45,-35,0);
            var rt=new RenderTexture(960,720,24);camera.targetTexture=rt;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(960,720,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,960,720),0,0);image.Apply();File.WriteAllBytes("C:/Users/ths30546/.codex/visualizations/2026/11/12/01a1cf84-878c-7e61-a4da-1161ef851f2b/trash-cart.png",image.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;rt.Release();
            Debug.Log("TRASH CARGO PASS: 2 carts, 5 tied bags each; passenger removed; saved scene and preview.");
        }
    }
}
