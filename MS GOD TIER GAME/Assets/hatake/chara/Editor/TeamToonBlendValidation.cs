using System;
using UnityEditor;
using UnityEngine;

namespace TeamToon.Editor
{
    // Also callable from the editor menu without modifying scenes or source assets.
    public static class TeamToonBlendValidation
    {
        [MenuItem("Tools/Team Toon/Validate Model Blend")]
        public static void Run()
        {
            var a = AssetDatabase.LoadAssetAtPath<Material>("Assets/WatanabeKaito/Materials/Enemy/ghost_mtl_blue.mat");
            var b = AssetDatabase.LoadAssetAtPath<Material>("Assets/hatake/chara/sample/charactorSample_001.mat");
            if (!a || !TeamToonMaterial.IsSupported(b)) throw new Exception("Blend source materials are missing or unsupported.");
            Debug.Log("Model blend source shader: " + a.shader.name);
            var material = new Material(b);
            try
            {
                material.SetFloat("_MaterialBlendEnabled", 1);
                material.SetFloat("_SurfaceType", 1);
                TeamToonMaterial.Synchronize(material);
                for (int i = 0; i < material.passCount; i++) ShaderUtil.CompilePass(material, i, true);
                foreach (var message in ShaderUtil.GetShaderMessages(material.shader))
                    if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                        throw new Exception(message.message + " at " + message.file + ":" + message.line);
                Debug.Log("Model blend shader validation passed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }
    }
}
