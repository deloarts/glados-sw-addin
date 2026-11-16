using System;
using System.IO;
using System.Reflection;

namespace GladosSwAddin.Helpers
{
    public static class IconManager
    {
        private static readonly string _iconDir = Path.Combine(
            Path.GetTempPath(),
            "GladosSwAddin",
            "Icons"
        );

        public static string Extract(string resourceName, string fileName)
        {
            Directory.CreateDirectory(_iconDir);

            string path = Path.Combine(_iconDir, fileName);
            Assembly assembly = Assembly.GetExecutingAssembly();

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException($"Could not find embedded icon: {resourceName}");
                }

                using (FileStream file = File.Create(path))
                {
                    stream.CopyTo(file);
                }
            }

            return path;
        }
    }
}
