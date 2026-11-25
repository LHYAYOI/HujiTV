using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace YasudaHaruto.BookValidation.Editor
{
    /// <summary>担当フォルダ整理のためUnityの依存関係を出力し、隔離後に参照の維持を検査する。</summary>
    [InitializeOnLoad]
    internal static class YasudaAssetUsageAudit
    {
        private const string Root = "Assets/YasudaHaruto/";
        private const string Output = "Temp/YasudaCleanup";
        [Serializable] internal sealed class AssetRecord { public string Path, Guid, Type; public string[] Dependencies; }
        [Serializable] internal sealed class Snapshot { public AssetRecord[] Assets; public string[] ProtectedPaths, MissingGuids; }
        [Serializable] internal sealed class KeepList { public AssetRecord[] Kept; }

        static YasudaAssetUsageAudit() => EditorApplication.update += Tick;

        [MenuItem("Tools/YasudaHaruto/Export Asset Usage Audit")]
        internal static void Export()
        {
            Directory.CreateDirectory(Output);
            var all = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && File.Exists(p)).ToArray();
            var own = all.Where(p => p.StartsWith(Root, StringComparison.Ordinal)).ToArray();
            // 他担当からの参照、全担当Scene、動的ロード用Resourcesは常に保護する。
            var roots = all.Where(p => !p.StartsWith(Root, StringComparison.Ordinal) || p.EndsWith(".unity", StringComparison.Ordinal) ||
                p.Contains("/Resources/") || p.Contains("/Editor Default Resources/")).ToArray();
            var protectedPaths = AssetDatabase.GetDependencies(roots, true).Where(p => p.StartsWith(Root, StringComparison.Ordinal)).Distinct().OrderBy(p => p).ToArray();
            var snapshot = new Snapshot
            {
                Assets = own.Select(p => new AssetRecord { Path = p, Guid = AssetDatabase.AssetPathToGUID(p), Type = AssetDatabase.GetMainAssetTypeAtPath(p)?.Name, Dependencies = AssetDatabase.GetDependencies(p, false) }).ToArray(),
                ProtectedPaths = protectedPaths,
                MissingGuids = MissingGuids(all)
            };
            File.WriteAllText(Output + "/native-usage.json", JsonUtility.ToJson(snapshot, true));
            File.WriteAllText(Output + "/audit-result.txt", $"EXPORTED {own.Length} assets; {protectedPaths.Length} native protected assets.\n");
        }

        private static string[] MissingGuids(IEnumerable<string> paths)
        {
            var result = new HashSet<string>();
            foreach (var path in paths.Where(p => new[] { ".unity", ".prefab", ".asset", ".mat", ".controller", ".playable" }.Contains(Path.GetExtension(p))))
            {
                string source = File.ReadAllText(path);
                foreach (Match match in Regex.Matches(source, @"guid:\s*([a-fA-F0-9]{32})"))
                {
                    string guid = match.Groups[1].Value;
                    if (guid.StartsWith("0000000000000000", StringComparison.Ordinal)) continue;
                    if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid))) result.Add(guid);
                }
            }
            return result.OrderBy(x => x).ToArray();
        }

        internal static void Verify()
        {
            var baseline = JsonUtility.FromJson<Snapshot>(File.ReadAllText(Output + "/native-usage.json"));
            var keep = JsonUtility.FromJson<KeepList>(File.ReadAllText(Output + "/keep.json"));
            foreach (var asset in keep.Kept)
                if (!File.Exists(asset.Path) || AssetDatabase.AssetPathToGUID(asset.Path) != asset.Guid)
                    throw new InvalidOperationException("Protected asset changed: " + asset.Path);
            foreach (string path in baseline.ProtectedPaths)
                if (!File.Exists(path)) throw new InvalidOperationException("Native dependency removed: " + path);
            var paths = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && File.Exists(p));
            var addedMissing = MissingGuids(paths).Except(baseline.MissingGuids).ToArray();
            if (addedMissing.Length > 0) throw new InvalidOperationException("New missing GUIDs: " + string.Join(",", addedMissing));
            File.WriteAllText(Output + "/verify-result.txt", $"PASS {keep.Kept.Length} retained assets and GUIDs unchanged.\nPASS All native dependencies preserved.\nPASS No new missing serialized GUIDs.\n");
        }

        private static void Tick()
        {
            string request = Output + "/request.txt";
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(request)) return;
            string command;
            try { command = File.ReadAllText(request).Trim(); File.Delete(request); }
            catch (IOException) { return; }
            try
            {
                if (command == "export") Export();
                else if (command == "verify") Verify();
            }
            catch (Exception error) { File.WriteAllText(Output + "/audit-error.txt", error.ToString()); Debug.LogException(error); }
        }
    }
}
