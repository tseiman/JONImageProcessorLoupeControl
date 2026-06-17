namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers
{
    using System;
    using System.IO;
    using System.Reflection;

    internal static class PluginResources
    {
        private static Assembly _assembly;

        public static void Init(Assembly assembly) => PluginResources._assembly = assembly;

        public static Stream GetStream(String resourceName) => PluginResources._assembly.GetStream(PluginResources.FindFile(resourceName));

        public static String FindFile(String fileName) => PluginResources._assembly.FindFileOrThrow(fileName);
    }
}
