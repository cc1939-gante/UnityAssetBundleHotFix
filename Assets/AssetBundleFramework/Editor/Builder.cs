using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssetBundleFramework.Core;
using Codice.Utils;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssetBundleFramework.Editor
{
    public static class Builder
    {
        public static readonly Vector2 CollectRuleFileProgress = new Vector2(0, 0.2f);
        public static readonly Vector2 CollectDependencyProgress = new Vector2(0.2f, 0.4f);
        public static readonly Vector2 CollectBundleInfoProgress = new Vector2(0.4f, 0.5f);
        public static readonly Vector2 GenerateBuildInfoProgress = new Vector2(0.5f, 0.6f);
        public static readonly Vector2 BuildBundleProgress = new Vector2(0.6f, 0.7f);
        public static readonly Vector2 ClearBundleProgress = new Vector2(0.7f, 0.8f);
        public static readonly Vector2 BuildManifestProgress = new Vector2(0.9f, 1f);


        private static readonly Profiler MBuildProfiler = new Profiler(nameof(Builder));
        private static readonly Profiler MLoadBuildSettingProfiler = MBuildProfiler.CreateChild(nameof(LoadSetting));
        private static readonly Profiler MSwitchPlatformProfiler = MBuildProfiler.CreateChild((nameof(SwitchPlatform)));
        private static readonly Profiler MCollectionProfiler = MBuildProfiler.CreateChild(nameof(Collect));

        private static readonly Profiler MCollectBuildSettingFileProfiler =
            MBuildProfiler.CreateChild(("CollectBuildSettingFile"));

        private static readonly Profiler MCollectDependencyProfiler =
            MBuildProfiler.CreateChild((nameof(CollectDependency)));

        private static readonly Profiler MCollectBundleProfiler = MBuildProfiler.CreateChild(nameof(CollectBundle));

        private static readonly Profiler MGenerateManifestProfiler =
            MBuildProfiler.CreateChild(nameof(GenerateManifest));

        private static readonly Profiler MBuildBundleProfiler = MBuildProfiler.CreateChild(nameof(BuildBundle));
        private static readonly Profiler MClearBundleProfiler = MBuildProfiler.CreateChild(nameof(ClearAssetBundle));
        private static readonly Profiler MBuildManifestProfiler = MBuildProfiler.CreateChild(nameof(BuildManifest));

#if UNITY_IOS
        private const string PLATFORM = "iOS";
#elif UNITY_ANDROID
        private const string PLATFORM = "Android";
#else
        private const string PLATFORM = "Windows";
#endif

        // bundle后缀
        public const string BUNDLE_SUFFIX = ".ab";
        public const string BUNDLE_MANIFEST_SUFFIX = ".manifest";

        // bundle 描述文件名称
        public const string MANIFEST = "manifest";

        public static readonly ParallelOptions ParallelOption = new ParallelOptions()
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount * 2
        };

        // bundle 打包 Options
        private static readonly BuildAssetBundleOptions BuildAssetBundleOptions =
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DeterministicAssetBundle |
            BuildAssetBundleOptions.StrictMode | BuildAssetBundleOptions.DisableLoadAssetByFileName |
            BuildAssetBundleOptions.DisableLoadAssetByFileNameWithExtension;

        /// <summary>
        /// 打包设置
        /// </summary>
        public static BuildSetting buildSettings { get; private set; }

        /// <summary>
        /// 打包目录
        /// </summary>
        private static string buildPath { get; set; }

        /// <summary>
        /// 临时目录，临时生成的文件都统一放在该目录下
        /// </summary>
        private static readonly string TempPath =
            Path.GetFullPath(Path.Combine(Application.dataPath, "Temp")).Replace("\\", "/");

        private static readonly string TempBuildPath =
            Path.GetFullPath(Path.Combine(Application.dataPath, "../TempBuild")).Replace("\\", "/");

        /// <summary>
        /// 依赖描述__文本
        /// </summary>
        private static readonly string Dependency_Text = $"{TempPath}/Dependency.txt";

        /// <summary>
        /// 依赖描述__二进制
        /// </summary>
        private static readonly string Dependency_Binary = $"{TempPath}/Dependency.bytes";

        /// <summary>
        /// bundle描述__文本
        /// </summary>
        private static readonly string BundlePath_Text = $"{TempPath}/Bundle.txt";

        /// <summary>
        /// bundle描述__二进制
        /// </summary>
        private static readonly string BundlePath_Binary = $"{TempPath}/Bundle.bytes";

        /// <summary>
        /// 资源描述__文本
        /// </summary>
        private static readonly string ResourePath_Text = $"{TempPath}/ResourcePath.txt";

        /// <summary>
        /// 资源描述__二进制
        /// </summary>
        private static readonly string ResourePath_Binary = $"{TempPath}/ResourcePath.bytes";

        private static readonly string BuildSettingPath = Path.GetFullPath("BuildSetting.xml").Replace("\\", "/");

        #region Build MenuItem

        [MenuItem("Tools/ResBuild/Windows")]
        public static void BuildWindows()
        {
            Debug.Log("执行了BuildWindows");
            Build();
        }

        public static void SwitchPlatform()
        {
            switch (PLATFORM)
            {
                case "iOS":
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
                    break;
                case "Android":
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                    break;
                case "Windows":
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone,
                        BuildTarget.StandaloneWindows64);
                    break;
            }
        }

        private static BuildSetting LoadSetting(string settingPath)
        {
            buildSettings = XmlUtility.Read<BuildSetting>(settingPath);
            if (buildSettings == null)
            {
                throw new Exception($"Load BuildSetting failed, path: {settingPath}");
            }

            buildSettings.EndInit();

            buildPath = Path.GetFullPath(buildSettings.buildRoot).Replace("\\", "/");
            if (buildPath.Length > 0 && buildPath[^1] != '/')
            {
                buildPath += "/";
            }

            buildPath += $"{PLATFORM}/";

            return buildSettings;
        }

        private static void Build()
        {
            MBuildProfiler.Start();
            
            MSwitchPlatformProfiler.Start();
            SwitchPlatform();
            MSwitchPlatformProfiler.Stop();
            
            MLoadBuildSettingProfiler.Start();
            buildSettings = LoadSetting(BuildSettingPath);
            MLoadBuildSettingProfiler.Stop();
            
            // 搜集bundle信息
            MCollectionProfiler.Start();
            Dictionary<string, List<string>> bundleDic = Collect();
            MCollectionProfiler.Stop();
            
            // 打包 Assetbundle
            MBuildBundleProfiler.Start();
            BuildBundle(bundleDic);
            MBuildBundleProfiler.Stop();
            
            // // 清除多余文件
            // MClearBundleProfiler.Start();
            // ClearAssetBundle(buildPath, bundleDic);
            // MClearBundleProfiler.Stop();
            
            // // 把描述文件打包 bundle
            // MBuildManifestProfiler.Start();
            // BuildManifest();
            // MBuildManifestProfiler.Stop();
            
            EditorUtility.ClearProgressBar();
            MBuildProfiler.Stop();

            Debug.Log($"打包完成: {MBuildProfiler}");
        }

        private static Dictionary<string, List<string>> Collect()
        {
            // 获取所有在打包设置的文件列表
            MCollectBuildSettingFileProfiler.Start();
            HashSet<string> files = buildSettings.Collect();
            MCollectBuildSettingFileProfiler.Stop();

            // 获取所有文件的依赖关系
            MCollectDependencyProfiler.Start();
            var dependencyDict = CollectDependency(files);
            MCollectDependencyProfiler.Stop();

            // 标记所有资源的信息
            Dictionary<string, EResourceType> assetDic = files.ToDictionary(file => file, file => EResourceType.Direct);

            foreach (var url in dependencyDict.Keys)
            {
                assetDic.TryAdd(url, EResourceType.Dependency);
            }

            // 该字典保存的是 bundle 资源
            MCollectBundleProfiler.Start();
            Dictionary<string, List<string>> bundleDict = CollectBundle(buildSettings, assetDic, dependencyDict);
            MCollectBundleProfiler.Stop();
           
            // 生成 mainfest 文件
            MGenerateManifestProfiler.Start();
            GenerateManifest(assetDic, bundleDict, dependencyDict);
            MGenerateManifestProfiler.Stop();

            return bundleDict;
        }

        /// <summary>
        /// 收集指定文件集合的依赖信息
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        private static Dictionary<string, List<string>> CollectDependency(ICollection<string> files)
        {
            float min = CollectDependencyProgress.x;
            float max = CollectDependencyProgress.y;

            Dictionary<string, List<string>> dependencyDict = new Dictionary<string, List<string>>();
            List<string> fileList = new List<string>(files);

            for (int i = 0; i < fileList.Count; i++)
            {
                string assetURL = fileList[i];
                if (dependencyDict.ContainsKey(assetURL))
                    continue;

                // 大概模拟一下进度
                if (i % 10 == 0)
                {
                    EditorUtility.DisplayProgressBar($"{nameof(CollectDependency)}", "搜集依赖资源",
                        min + (max - min) * i / (fileList.Count * 3));
                }

                string[] dependencies = AssetDatabase.GetDependencies(assetURL, false);
                List<string> dependencyList = new List<string>(dependencies.Length);

                foreach (var tempAssetUrl in dependencies)
                {
                    string extension = Path.GetExtension(tempAssetUrl).ToLower();
                    if (string.IsNullOrEmpty(extension) || extension == ".cs" || extension == ".dll")
                        continue;
                    dependencyList.Add(tempAssetUrl);
                    if (!fileList.Contains(tempAssetUrl))
                    {
                        fileList.Add(tempAssetUrl);
                    }
                }

                dependencyDict.Add(assetURL, dependencyList);
            }

            return dependencyDict;
        }

        private static Dictionary<string, List<string>> CollectBundle(BuildSetting buildSetting,
            Dictionary<string, EResourceType> assetDic, Dictionary<string, List<string>> dependencyDict)
        {
            float min = CollectBundleInfoProgress.x;
            float max = CollectBundleInfoProgress.y;

            EditorUtility.DisplayProgressBar($"{nameof(CollectBundle)}", "搜集 Bundle 信息", min);
            Dictionary<string, List<string>> bundleDict = new Dictionary<string, List<string>>();
            // 外部资源
            List<string> notInRuleList = new List<string>();
            int index = 0;
            foreach (var kv in assetDic)
            {
                index++;
                string assetURL = kv.Key;
                string bundleName = buildSetting.GetBundleName(assetURL, kv.Value);

                if (bundleName == null)
                {
                    notInRuleList.Add(assetURL);
                    continue;
                }

                if (!bundleDict.TryGetValue(bundleName, out var list))
                {
                    list = new List<string>();
                    bundleDict.Add(bundleName, list);
                }

                list.Add(assetURL);
                EditorUtility.DisplayProgressBar($"{nameof(CollectBundle)}", "搜集 bundle 信息",
                    min + (max - min) * index / assetDic.Count);
            }

            if (notInRuleList.Count > 0)
            {
                string message = notInRuleList.Aggregate("", (current, url) => current + (url + "\n"));
                EditorUtility.ClearProgressBar();
                throw new Exception($"资源不在打包规则中，或者后缀不匹配!!!: \n{message}");
            }

            foreach (var list in bundleDict.Values)
            {
                list.Sort();
            }

            return bundleDict;
        }

        private static void GenerateManifest(Dictionary<string, EResourceType> assetDic,
            Dictionary<string, List<string>> bundleDic, Dictionary<string, List<string>> dependencyDict)
        {
            float min = GenerateBuildInfoProgress.x;
            float max = GenerateBuildInfoProgress.y;

            EditorUtility.DisplayProgressBar($"{nameof(GenerateManifest)}", "生成打包信息", min);

            if (!Directory.Exists(TempPath))
                Directory.CreateDirectory(TempPath);

            // 资源映射 id
            Dictionary<string, ushort> assetIdDict = new Dictionary<string, ushort>();

            #region 生成资源描述信息

            {
                // 删除旧的资源描述文件
                if (File.Exists(ResourePath_Text))
                    File.Delete(ResourePath_Text);

                // 删除资源描述二进制文件
                if (File.Exists(ResourePath_Binary))
                    File.Delete(ResourePath_Binary);

                if (assetDic.Count > ushort.MaxValue)
                {
                    EditorUtility.ClearProgressBar();
                    throw new Exception($"资源数量超过最大值: {assetDic.Count}");
                }

                // 写入资源列表
                StringBuilder resourceSb = new StringBuilder();
                MemoryStream resourceMs = new MemoryStream();
                BinaryWriter resourceBw = new BinaryWriter(resourceMs);

                resourceBw.Write((ushort)assetDic.Count);
                List<string> keys = new List<string>(assetDic.Keys);
                keys.Sort();

                for (ushort i = 0; i < keys.Count; i++)
                {
                    string assetURL = keys[i];
                    assetIdDict.Add(assetURL, i);
                    resourceSb.AppendLine($"{i} {assetURL}");
                    resourceBw.Write(assetURL);
                }

                resourceMs.Flush();
                byte[] buffer = resourceMs.GetBuffer();
                resourceBw.Close();

                // 写入资源描述文件
                File.WriteAllText(ResourePath_Text, resourceSb.ToString());
                File.WriteAllBytes(ResourePath_Binary, buffer);
            }

            #endregion

            EditorUtility.DisplayProgressBar($"{nameof(GenerateManifest)}", "生成打包信息", min + (max - min) * 0.3f);

            #region 生成bundle描述信息

            {
                // 删除旧的资源描述文件
                if (File.Exists(BundlePath_Text))
                    File.Delete(BundlePath_Text);

                // 删除资源描述二进制文件
                if (File.Exists(BundlePath_Binary))
                    File.Delete(BundlePath_Binary);

                if (assetDic.Count > ushort.MaxValue)
                {
                    EditorUtility.ClearProgressBar();
                    throw new Exception($"资源数量超过最大值: {assetDic.Count}");
                }

                // 写入资源列表
                StringBuilder bundleSb = new StringBuilder();
                MemoryStream bundleMs = new MemoryStream();
                BinaryWriter bundleBw = new BinaryWriter(bundleMs);

                bundleBw.Write((ushort)assetDic.Count);
                foreach (var kv in bundleDic)
                {
                    string bundleName = kv.Key;
                    List<string> assets = kv.Value;

                    // 写入 bundle
                    bundleSb.AppendLine(bundleName);
                    bundleBw.Write(bundleName);

                    // 写入资源个数
                    bundleBw.Write((ushort)assets.Count);

                    for (int i = 0; i < assets.Count; i++)
                    {
                        string assetUrl = assets[i];
                        ushort assetId = assetIdDict[assetUrl];
                        bundleSb.AppendLine($"\t{assetUrl}");
                        bundleBw.Write(assetId);
                    }
                }

                bundleMs.Flush();
                byte[] buffer = bundleMs.GetBuffer();
                bundleBw.Close();

                // 写入资源描述文件
                File.WriteAllText(ResourePath_Text, bundleSb.ToString());
                File.WriteAllBytes(ResourePath_Binary, buffer);
            }

            #endregion

            EditorUtility.DisplayProgressBar($"{nameof(GenerateManifest)}", "生成打包信息", min + (max - min) * 0.8f);

            #region 生成依赖信息

            {
                // 删除旧的资源描述文件
                if (File.Exists(Dependency_Text))
                    File.Delete(Dependency_Text);

                // 删除资源描述二进制文件
                if (File.Exists(Dependency_Binary))
                    File.Delete(Dependency_Binary);

                // 写入资源列表
                StringBuilder dependencySb = new StringBuilder();
                MemoryStream dependencyMs = new MemoryStream();
                BinaryWriter dependencyBw = new BinaryWriter(dependencyMs);

                // 用于保存资源依赖链
                List<List<ushort>> dependencyList = new List<List<ushort>>(dependencyDict.Count);
                foreach (var kv in dependencyDict)
                {
                    List<string> dependencyAssets = kv.Value;

                    if (dependencyAssets.Count == 0)
                        continue;

                    string assetURL = kv.Key;

                    List<ushort> ids = new List<ushort>();
                    ids.Add(assetIdDict[assetURL]);

                    string content = assetURL;
                    for (int i = 0; i < dependencyAssets.Count; i++)
                    {
                        string dependencyAsset = dependencyAssets[i];
                        content += $"\t{dependencyAsset}";
                        ids.Add(assetIdDict[dependencyAsset]);
                    }

                    dependencySb.AppendLine(content);

                    if (ids.Count > ushort.MaxValue)
                    {
                        EditorUtility.ClearProgressBar();
                        throw new Exception($"资源依赖链超过最大值: {ids.Count}");
                    }

                    dependencyList.Add(ids);
                }

                // 写入依赖链个数
                dependencyBw.Write((ushort)dependencyList.Count);
                for (int i = 0; i < dependencyList.Count; i++)
                {
                    // 写入资源数
                    List<ushort> ids = dependencyList[i];
                    dependencyBw.Write((ushort)ids.Count);
                    for (int j = 0; j < ids.Count; j++)
                    {
                        dependencyBw.Write(ids[j]);
                    }
                }

                dependencyMs.Flush();
                byte[] buffer = dependencyMs.GetBuffer();
                dependencyBw.Close();
                // 写入资源描述文件
                File.WriteAllText(Dependency_Text, dependencySb.ToString(), Encoding.UTF8);
                File.WriteAllBytes(Dependency_Binary, buffer);
            }

            #endregion

            AssetDatabase.Refresh();

            EditorUtility.DisplayProgressBar($"{nameof(GenerateManifest)}", "生成打包信息", max);
            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// 打包 AssetBundle
        /// </summary>
        /// <param name="bundleDic"></param>
        /// <returns></returns>
        private static AssetBundleManifest BuildBundle(Dictionary<string, List<string>> bundleDic)
        {
            float min = BuildBundleProgress.x;
            float max = BuildBundleProgress.y;

            EditorUtility.DisplayProgressBar($"{nameof(BuildBundle)}", "打包 AssetBundle", min);

            if (!Directory.Exists(buildPath))
            {
                Directory.CreateDirectory(buildPath);
            }

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(buildPath, GetBuilds(bundleDic),
                BuildAssetBundleOptions, EditorUserBuildSettings.activeBuildTarget);
            
            EditorUtility.DisplayProgressBar($"{nameof(BuildBundle)}", "打包 AssetBundle", max);
            return manifest;
        }

        /// <summary>
        /// 把 Resource.bytes bundle.bytes, Dependency.bytes 打包 AssetBundle
        /// </summary>
        private static void BuildManifest()
        {
            float min = BuildManifestProgress.x;
            float max = BuildManifestProgress.y;

            EditorUtility.DisplayProgressBar($"{nameof(BuildManifest)}", "将 manifest 文件打包 AssetBundle", min);
            if (!Directory.Exists(TempBuildPath))
            {
                Directory.CreateDirectory(TempBuildPath);
            }

            string prefix = Application.dataPath.Replace("/Assets", "/").Replace("\\", "/");

            AssetBundleBuild manifest = new AssetBundleBuild();
            manifest.assetBundleName = $"{MANIFEST}-{BUNDLE_SUFFIX}";
            manifest.assetNames = new[]
            {
                ResourePath_Binary.Replace(prefix, ""),
                BundlePath_Binary.Replace(prefix, ""),
                Dependency_Binary.Replace(prefix, ""),
            };

            EditorUtility.DisplayProgressBar($"{nameof(BuildManifest)}", "将 manifest 文件打包 AssetBundle",
                min + (max - min) * 0.5f);

            AssetBundleManifest assetBundleManifest = BuildPipeline.BuildAssetBundles(TempBuildPath,
                new AssetBundleBuild[] { manifest }, BuildAssetBundleOptions,
                EditorUserBuildSettings.activeBuildTarget);

            // 将文件 copy 到 build 目录
            if (assetBundleManifest)
            {
                string manifestFile = $"{TempBuildPath}/{MANIFEST}-{BUNDLE_SUFFIX}";
                string target = $"{buildPath}/{MANIFEST}-{BUNDLE_SUFFIX}";
                if (File.Exists(manifestFile))
                {
                    File.Copy(manifestFile, target);
                }
            }

            // 删除临时目录
            if (Directory.Exists(TempBuildPath))
                Directory.Delete(TempBuildPath, true);

            EditorUtility.DisplayProgressBar($"{nameof(BuildManifest)}", "将 manifest 文件打包 AssetBundle", max);
        }

        /// <summary>
        /// 获取所有需要打包的 AssetBundleBuild
        /// </summary>
        /// <param name="bundleTable"></param>
        /// <returns></returns>
        private static AssetBundleBuild[] GetBuilds(Dictionary<string, List<string>> bundleTable)
        {
            int index = 0;
            AssetBundleBuild[] builds = new AssetBundleBuild[bundleTable.Count];
            foreach (var pair in bundleTable)
            {
                builds[index++] = new AssetBundleBuild()
                {
                    assetBundleName = pair.Key,
                    assetNames = pair.Value.ToArray()
                };
            }

            return builds;
        }

        private static void ClearAssetBundle(string path, Dictionary<string, List<string>> bundleDic)
        {
            float min = ClearBundleProgress.x;
            float max = ClearBundleProgress.y;

            EditorUtility.DisplayProgressBar($"{nameof(ClearAssetBundle)}", "清除多余文件", min);

            List<string> fileList = GetFiles(path, null, null);
            HashSet<string> fileSet = new HashSet<string>(fileList);

            foreach (var bundle in bundleDic.Keys)
            {
                fileSet.Remove($"{path}{bundle}");
                fileSet.Remove($"{path}{bundle}{BUNDLE_MANIFEST_SUFFIX}");
            }

            fileSet.Remove($"{path}{PLATFORM}");
            fileSet.Remove($"{path}{PLATFORM}{BUNDLE_MANIFEST_SUFFIX}");

            Parallel.ForEach(fileSet, ParallelOption, File.Delete);

            EditorUtility.DisplayProgressBar($"{nameof(ClearAssetBundle)}", "清除多余文件", max);
        }

        /// <summary>
        /// 获取指定目录下的指定前后缀文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="prefix"></param>
        /// <param name="suffixes"></param>
        /// <returns></returns>
        public static List<string> GetFiles(string path, string prefix, params string[] suffixes)
        {
            string[] files = Directory.GetFiles(path, $"*.*", SearchOption.AllDirectories);
            List<string> result = new List<string>(files.Length);

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i].Replace('\\', '/');
                if (prefix != null && file.StartsWith(prefix, StringComparison.InvariantCulture))
                    continue;
                if (suffixes != null && suffixes.Length > 0)
                {
                    var isMatch = suffixes.Any(t => file.EndsWith(t, StringComparison.InvariantCulture));
                    if (!isMatch)
                        continue;
                }

                result.Add(file);
            }

            return result;
        }

        /// <summary>
        /// 文件是否再忽略列表中
        /// </summary>
        /// <param name="ignoreList"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public static bool IsIgnore(List<string> ignoreList, string file)
        {
            return ignoreList.Any(
                t => !string.IsNullOrEmpty(t) && file.StartsWith(t, StringComparison.InvariantCulture));
        }

        #endregion
    }
}