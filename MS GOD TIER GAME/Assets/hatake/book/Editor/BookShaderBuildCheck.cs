using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace hatake.EditorTools
{
    /// <summary>
    /// 本のシェーダー（合成・後処理）を、ビルドせずに Android 向け（Vulkan / OpenGL ES 3）へコンパイルして確かめる。
    /// メニュー「Tools/hatake/Book Shader Build Check」で実行し、結果を Console と Logs/BookShaderBuildCheck.txt に書く。
    /// 紙の仕上げのバリアント（マット／グロス）は両方試す。
    /// </summary>
    public static class BookShaderBuildCheck
    {
        private static readonly string[] s_shaderPaths =
        {
            "Assets/hatake/book/shader/BookFlipComposite.shader",
            "Assets/hatake/book/shader/BookFlipPost.shader",
        };

        private static readonly ShaderCompilerPlatform[] s_platforms =
        {
            ShaderCompilerPlatform.Vulkan,
            ShaderCompilerPlatform.GLES3x,
        };

        [MenuItem("Tools/hatake/Book Shader Build Check")]
        public static void Run()
        {
            var report = new StringBuilder();
            int failures = 0;
            foreach (string path in s_shaderPaths)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null)
                {
                    report.AppendLine("MISSING " + path);
                    failures++;
                    continue;
                }
                ShaderData data = ShaderUtil.GetShaderData(shader);
                for (int s = 0; s < data.SubshaderCount; s++)
                {
                    ShaderData.Subshader subshader = data.GetSubshader(s);
                    for (int p = 0; p < subshader.PassCount; p++)
                    {
                        ShaderData.Pass pass = subshader.GetPass(p);
                        // 合成は紙の仕上げの2つのバリアント、後処理はキーワード無し
                        string[][] variants = path.Contains("Composite")
                            ? new[] { new[] { "_PAPERFINISH_MATTE" }, new[] { "_PAPERFINISH_GLOSS" } }
                            : new[] { new string[0] };
                        foreach (string[] keywords in variants)
                        {
                            foreach (ShaderCompilerPlatform platform in s_platforms)
                            {
                                foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                                {
                                    ShaderData.VariantCompileInfo info = pass.CompileVariant(stage, keywords, platform, BuildTarget.Android);
                                    string name = shader.name + " pass " + p + " " + stage + " " + platform + " [" + string.Join(" ", keywords) + "]";
                                    // 大きさ（変換後のコードのバイト数）。大きすぎるとモバイルの GPU で命令キャッシュに収まらず極端に遅くなる
                                    int size = info.ShaderData != null ? info.ShaderData.Length : 0;
                                    report.AppendLine((info.Success ? "OK   " : "FAIL ") + name + " size=" + size);
                                    if (!info.Success) failures++;
                                    foreach (ShaderMessage message in info.Messages)
                                    {
                                        if (message.severity == ShaderCompilerMessageSeverity.Error)
                                        {
                                            report.AppendLine("     error: " + message.message + " (" + message.file + ":" + message.line + ")");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            report.Insert(0, (failures == 0 ? "RESULT OK" : "RESULT FAIL " + failures) + "\n");
            string text = report.ToString();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/BookShaderBuildCheck.txt", text);
            if (failures == 0) Debug.Log("[BookShaderBuildCheck]\n" + text);
            else Debug.LogError("[BookShaderBuildCheck]\n" + text);
        }
    }
}
