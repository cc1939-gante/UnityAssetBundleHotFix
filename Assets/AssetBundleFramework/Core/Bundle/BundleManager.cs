using System;
using Unity.VisualScripting;

namespace AssetBundleFramework.Core.Bundle
{
    public class BundleManager
    {
        public static readonly BundleManager Instance = new BundleManager();
        
        private Func<string, string> m_GetFileCallback { get; set; }
        private ulong offset { get; set; }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="platform"></param>
        /// <param name="getFileCallback"></param>
        /// <param name="offset"></param>
        internal void Init(string platform, Func<string, string> getFileCallback, ulong offset)
        {
            m_GetFileCallback = getFileCallback;
            this.offset = offset;
        }

    }
}