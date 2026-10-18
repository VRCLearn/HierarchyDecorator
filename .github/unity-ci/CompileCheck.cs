using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace HierarchyDecoratorCI
{
    public static class CompileCheck
    {
        [Serializable]
        private class Report
        {
            public string unityVersion;
            public string packageName;
            public string assemblyName;
            public int sourceFileCount;
            public bool upstreamCoexistenceChecked;
        }

        public static void Run()
        {
            const string assemblyName = "VRCLearn.HierarchyDecorator.Editor";
            var loadedAssembly = typeof(VRCLearn.HierarchyDecorator.Settings).Assembly;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(loadedAssembly);
            var compiled = CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .SingleOrDefault(assembly => assembly.name == assemblyName);
            if (loadedAssembly.GetName().Name != assemblyName || compiled == null ||
                compiled.sourceFiles.Length == 0 || !File.Exists(compiled.outputPath))
                throw new InvalidOperationException("HierarchyDecorator's editor assembly was not compiled.");
            if (package == null || package.name != "com.vrclearn.hierarchydecorator")
                throw new InvalidOperationException("HierarchyDecorator was not loaded as the expected UPM package.");

            var report = new Report
            {
                unityVersion = Application.unityVersion,
                packageName = package.name,
                assemblyName = assemblyName,
                sourceFileCount = compiled.sourceFiles.Length,
                upstreamCoexistenceChecked = CheckUpstreamCoexistence(loadedAssembly)
            };
            string logs = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "compile-result.json"), JsonUtility.ToJson(report, true));
            Debug.Log("HIERARCHY_DECORATOR_COMPILE_OK " + JsonUtility.ToJson(report));
            // GameCI requires this marker for successful custom build methods too.
            Debug.Log("Build succeeded!");
            EditorApplication.Exit(0);
        }

        private static bool CheckUpstreamCoexistence(System.Reflection.Assembly loadedAssembly)
        {
            const string upstreamAssemblyName = "Wooshii.HierarchyDecorator.Editor";
            var upstream = CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .SingleOrDefault(assembly => assembly.name == upstreamAssemblyName);
            bool upstreamInstalled = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                .Any(package => package.name == "com.wooshii.hierarchydecorator");
            if (upstream == null && !upstreamInstalled)
                return false;

            var upstreamType = Type.GetType("HierarchyDecorator.Settings, " + upstreamAssemblyName, true);
            var forkType = Type.GetType("VRCLearn.HierarchyDecorator.Settings, " + loadedAssembly.GetName().Name, true);
            if (upstream == null || upstream.sourceFiles.Length == 0 || !File.Exists(upstream.outputPath) ||
                upstreamType.Assembly.GetName().Name != upstreamAssemblyName ||
                forkType != typeof(VRCLearn.HierarchyDecorator.Settings) ||
                upstreamType == forkType || upstreamType.Assembly == forkType.Assembly)
                throw new InvalidOperationException("Upstream and VRCLearn settings did not compile as independent types.");
            return true;
        }
    }
}
