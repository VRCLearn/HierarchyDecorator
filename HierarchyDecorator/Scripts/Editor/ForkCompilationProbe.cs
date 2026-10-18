#if !UNITY_EDITOR || !(UNITY_EDITOR_WIN || UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX)
#error The fork compilation probe requires Unity Editor platform symbols.
#endif

namespace HierarchyDecorator
{
    // Temporary fork-only probe: verifies that CI compiles the PR's Unity API calls.
    internal static class ForkCompilationProbe
    {
        internal static string UnityVersion => UnityEngine.Application.unityVersion;
        internal static bool IsCompiling => UnityEditor.EditorApplication.isCompiling;
    }
}
