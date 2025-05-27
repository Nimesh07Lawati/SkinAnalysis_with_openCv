using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SkinCareAiIntegration.ForAndroid
{
    public static class FileAccessHelper
    {
        public static string GetFilePath(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            // IMPORTANT: Adjust this if your namespace is different!
            var resourceName = $"SkinCareAiIntegration.ForAndroid.{fileName}";

            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            if (!File.Exists(filePath))
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    throw new FileNotFoundException($"Resource '{resourceName}' not found in assembly.");

                using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                stream.CopyTo(fs);
            }
            return filePath;
        }
    }
}
