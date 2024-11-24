using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AssetBundleFramework.Editor
{
    public class BuildItem
    {
        [DisplayName("ab粒度类型")]
        [XmlAttribute("BundleType")]
        public EBundleType bundleType { get; set; } = EBundleType.File;

        [DisplayName("资源的路径")]
        [XmlAttribute("AssetPath")]
        public string assetPath { get; set; }

        [DisplayName("资源的类型")]
        [XmlAttribute("ResourceType")]
        public EResourceType resourceType { get; set; } = EResourceType.Direct;

        [DisplayName("资源后缀")]
        [XmlAttribute("Suffix")]
        public string suffix { get; set; } = ".prefab";

        [XmlIgnore]
        public List<string> suffixs { get; set; } = new List<string>();

        [XmlIgnore]
        public List<string> ignorePaths { get; set; } = new List<string>();

        // 匹配该打包的个数
        [XmlIgnore]
        public int count { get; set; }
    }
}