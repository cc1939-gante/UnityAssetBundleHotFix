using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace AssetBundleFramework.Editor
{
    public static class XmlUtility
    {
        public static T Read<T>(string fileName) where T : class
        {
            FileStream fileStream = null;
            if(!File.Exists(fileName)) 
            {
                return default(T);
            }

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                fileStream = File.OpenRead(fileName);
                XmlReader reader = XmlReader.Create(fileStream);
                T instance = serializer.Deserialize(reader) as T;
                reader.Close();
                fileStream.Close();
                return instance;
            }
            catch
            {
                if(fileStream != null) 
                {
                    fileStream.Close();
                }
                return default(T);
            }
        }
    }
}