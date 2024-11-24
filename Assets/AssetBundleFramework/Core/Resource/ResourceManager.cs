using System;
using Unity.VisualScripting;

namespace AssetBundleFramework.Core.Resource
{
    public class ResourceManager
    {
        public static readonly ResourceManager Instance = new ResourceManager();

        private bool m_Editor;
        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="platform">平台</param>
        /// <param name="getFileCallback">获取资源真实路径回调</param>
        /// <param name="editor">是否使用 AssetDataBase 加载</param>
        /// <param name="offset"> 获取 bundle 的偏移</param>
        public void Init(string platform, Func<string, string> getFileCallback, bool editor, ulong offset)
        {
            m_Editor = editor;
            if (m_Editor)
                return;
            
            
        }
    }
}