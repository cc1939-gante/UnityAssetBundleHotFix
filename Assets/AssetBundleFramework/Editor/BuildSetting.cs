using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Xml.Serialization;
using System.IO;
using AssetBundleFramework.Editor;
using Codice.CM.WorkspaceServer.Lock;
using NUnit.Framework.Interfaces;
using OpenCover.Framework.Model;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace AssetBundleFramework.Editor
{
    public class BuildSetting : ISupportInitialize
    {
        [DisplayName("项目名称")]
        [XmlAttribute("ProjectName")]
        public string projectName { get; set; }

        [DisplayName("后缀列表")]
        [XmlAttribute("SuffixList")]
        public List<string> suffixList { get; set; } = new List<string>();

        [DisplayName("打包文件的目录文件夹")]
        [XmlAttribute("BuildRoot")]
        public string buildRoot { get; set; }

        [DisplayName("打包选项")]
        [XmlElement("BuildItem")]
        public List<BuildItem> items { get; set; } = new List<BuildItem>();

        [XmlIgnore]
        private Dictionary<string, BuildItem> itemDict = new Dictionary<string, BuildItem>();
        public void BeginInit()
        {
            // throw new NotImplementedException();
        }

        public void EndInit()
        {
            buildRoot = Path.GetFullPath(buildRoot).Replace("\\", "/");
            itemDict.Clear();
            
            for (int i = 0; i < items.Count; i++)
            {
                BuildItem buildItem = items[i];
                if (buildItem.bundleType == EBundleType.All || buildItem.bundleType == EBundleType.Directory)
                {
                    if (!Directory.Exists(buildItem.assetPath))
                    {
                        throw new Exception($"Directory not exist, path: {buildItem.assetPath}");
                    }
                }

                // 处理后缀
                string[] suffixs = buildItem.suffix.Split('|');
                for (int ii = 0; ii < suffixs.Length; ii++)
                {
                    string suffix = suffixs[ii].Trim();
                    if (!string.IsNullOrEmpty(suffix))
                    {
                        buildItem.suffixs.Add(suffix);
                    }
                }

                if(itemDict.ContainsKey(buildItem.assetPath))
                {
                    throw new Exception($"重复的资源路径: {buildItem.assetPath}");
                }

                itemDict.Add(buildItem.assetPath, buildItem);
            }
        }
        
        /// <summary>
        /// 获取所有在打包设置的文件列表
        /// </summary>
        /// <returns></returns>
        public HashSet<string> Collect()
        {
            float min = Builder.CollectRuleFileProgress.x;
            float max = Builder.CollectRuleFileProgress.y;
            
            EditorUtility.DisplayProgressBar($"{nameof(Collect)}", "搜集打包规则资源", min);
            
            foreach (var buildItem_i in items)
            {
                if(buildItem_i.resourceType != EResourceType.Direct)
                    continue;
                
                buildItem_i.ignorePaths.Clear();
                foreach (var buildItem_j in items)
                {
                    if (buildItem_i != buildItem_j && buildItem_j.resourceType == EResourceType.Direct)
                    {
                        if (buildItem_j.assetPath.StartsWith(buildItem_i.assetPath, StringComparison.InvariantCulture))
                        {
                            buildItem_i.ignorePaths.Add(buildItem_j.assetPath);
                        }
                    }
                }
            }
        
            // 存储被规则分析到的所有文件
            HashSet<string> files = new HashSet<string>();
            
            for (int i = 0; i < items.Count; i++)
            {
                var buildItem = items[i];
                EditorUtility.DisplayProgressBar($"{nameof(Collect)}", "搜集打包规则资源", min + (max - min) * ((float)i / (items.Count - 1)));
                
                if(buildItem.resourceType != EResourceType.Direct)
                {
                    continue;
                }
                
                var tempFiles = Builder.GetFiles(buildItem.assetPath, null, buildItem.suffixs.ToArray());
                foreach (var file in tempFiles)
                {
                    if (Builder.IsIgnore(buildItem.ignorePaths, file))
                    {
                        continue;
                    }
                    
                    files.Add(file);
                }
                EditorUtility.DisplayProgressBar($"{nameof(Collect)}", "搜集打包设置资源", (float)(i + 1) / items.Count);
            }

            return files;
        }

        /// <summary>
        /// 通过资源获取打包选项
        /// </summary>
        /// <param name="assetURL"></param>
        /// <returns></returns>
        public BuildItem GetBuildItem(string assetURL)
        {
            BuildItem item = null;
            for (int i = 0; i <items.Count; i++)
            {
                BuildItem tempItem = items[i];
                // 前面是否匹配
                if (assetURL.StartsWith(tempItem.assetPath, StringComparison.InvariantCulture))
                {
                    if (item == null || item.assetPath.Length < tempItem.assetPath.Length)
                    {
                        item = tempItem;
                    }
                }
            }

            return item;
        }

        /// <summary>
        /// 获取 bundleName
        /// </summary>
        /// <param name="assetPath">资源路径</param>
        /// <param name="resourceType">资源类型</param>
        /// <returns></returns>
        public string GetBundleName(string assetPath, EResourceType resourceType)
        {
            BuildItem buildItem = GetBuildItem(assetPath);

            if (null == buildItem)
                return null;

            string name = "";
            
            // 依赖资源一定要匹配后缀
            if (buildItem.resourceType == EResourceType.Dependency)
            {
                string extension = Path.GetExtension(assetPath).ToLower();
                bool exist = false;
                for (int i = 0; i < buildItem.suffixs.Count; i++)
                {
                    if (buildItem.suffixs[i] == extension)
                    {
                        exist = true;
                        break;
                    }
                }

                if (!exist)
                {
                    return null;
                }
            }

            switch (buildItem.bundleType)
            {
                case EBundleType.All:
                    name = buildItem.assetPath;
                    if (buildItem.assetPath[^1] == '/')
                    {
                        name = buildItem.assetPath.Substring(0, buildItem.assetPath.Length - 1);
                    }

                    name = $"{name}{Builder.BUNDLE_SUFFIX}".ToLowerInvariant();
                    break;
                case EBundleType.Directory:
                    name = $"{assetPath.Substring(0, assetPath.LastIndexOf('/'))}{Builder.BUNDLE_SUFFIX}"
                        .ToLowerInvariant();
                    break;
                case EBundleType.File:
                    name = $"{assetPath}{Builder.BUNDLE_SUFFIX}".ToLowerInvariant();
                    break;
                default:
                    throw new Exception($"无法获取{assetPath}的BundleName");
            }

            buildItem.count += 1;
            return name;
        }
    }
}