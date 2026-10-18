using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace VRCLearn.HierarchyDecorator
{
    internal static class UpstreamAssets
    {
        private const string SourceFolder = "HierarchyDecorator/";
        // Track distributed paths as well as GUIDs: copied sources can have regenerated metadata.
        private static readonly Dictionary<string, string> Files = new Dictionary<string, string>
        {
            { "CHANGELOG.md", "f515db8d25e6d754b8d09e8b97e88454" },
            { "HierarchyDecorator/Scripts/Editor/Attributes/RegisterTabAttribute.cs", "73e1a3fd64b18554391ad2f95d6060a3" },
            { "HierarchyDecorator/Scripts/Editor/ComponentItem.cs", "192aebadabb1aec469ee02046f2367a9" },
            { "HierarchyDecorator/Scripts/Editor/Constants.cs", "31084919640aa4443bfef68726a55f65" },
            { "HierarchyDecorator/Scripts/Editor/Data/ComponentData.cs", "af7eb40c43d31a64e8974d27a427ab61" },
            { "HierarchyDecorator/Scripts/Editor/Data/GlobalData.cs", "48a479cbe55ff6a4e89fc4676a39b94d" },
            { "HierarchyDecorator/Scripts/Editor/Data/HierarchyStyleData.cs", "c2708b2dd0efd0e41b7100f9b3a84c2c" },
            { "HierarchyDecorator/Scripts/Editor/Data/Types/ComponentGroup.cs", "fa507b92e013b2a4487185098b991169" },
            { "HierarchyDecorator/Scripts/Editor/Data/Types/ComponentType.cs", "9917b346e29d4fc4bb68b38ebcd8162a" },
            { "HierarchyDecorator/Scripts/Editor/Data/Types/HierarchyStyle.cs", "7b19ee6f06cf2c848b65ff1fd053a7e4" },
            { "HierarchyDecorator/Scripts/Editor/GUI/DrawerGroup.cs", "fdf4218929f3371419f6170aa975e9ec" },
            { "HierarchyDecorator/Scripts/Editor/GUI/Drawers/EnumFlagToggleDrawer.cs", "2274f94d4635da54b93542a4ef71e72a" },
            { "HierarchyDecorator/Scripts/Editor/GUI/Drawers/ReorderableDrawer.cs", "af5a5557b6503764b8bfe4bdc7c4b06e" },
            { "HierarchyDecorator/Scripts/Editor/GUI/Drawers/SerializedGroupDrawer.cs", "fbee4186caa409f4597b8b1914379c43" },
            { "HierarchyDecorator/Scripts/Editor/GUI/Drawers/SerializedPropertyDrawer.cs", "6d7aa63d6cd07354bb49348b15839053" },
            { "HierarchyDecorator/Scripts/Editor/GUI/Drawers/SpaceDrawer.cs", "0dee322f6d6313c47a8babc2adc14e1f" },
            { "HierarchyDecorator/Scripts/Editor/GUI/GUIDrawer.cs", "2c154e63f1b989343b173d9de5e62d28" },
            { "HierarchyDecorator/Scripts/Editor/Hierarchy/Drawers/BreadcrumbsDrawer.cs", "7be28652b1b91d64facd8ed6d378d359" },
            { "HierarchyDecorator/Scripts/Editor/Hierarchy/Drawers/StateDrawer.cs", "82265ea1a6be8d84287eb5a2fe26af68" },
            { "HierarchyDecorator/Scripts/Editor/Hierarchy/Drawers/StyleDrawer.cs", "22fde4c9ca90c41418fbc592b357ccb3" },
            { "HierarchyDecorator/Scripts/Editor/Hierarchy/Drawers/ToggleDrawer.cs", "03d0381539029194d928324e60c7ba96" },
            { "HierarchyDecorator/Scripts/Editor/Hierarchy/HierarchyDrawer.cs", "ba80f4e2aedd36045b183c6b693627e8" },
            { "HierarchyDecorator/Scripts/Editor/Hierarchy/Info/ComponentIconInfo.cs", "7ace86b840da1ed40a57bbfa17e8ce2f" },
            { "HierarchyDecorator/Scripts/Editor/Hierarchy/Info/HierarchyInfo.cs", "5ec39671c1c150644ba3e43d70ae72cc" },
            { "HierarchyDecorator/Scripts/Editor/Hierarchy/Info/TagLayerInfo.cs", "3d3153fd97d96064b8e6898e62966a25" },
            { "HierarchyDecorator/Scripts/Editor/HierarchyDecorator.cs", "78cc1eecce018a047b2649c872322057" },
            { "HierarchyDecorator/Scripts/Editor/HierarchyGUI.cs", "862f460517b87be49aa70fe6e76d90dd" },
            { "HierarchyDecorator/Scripts/Editor/HierarchyItem.cs", "a93f12baa008f27449b145da87efb41e" },
            { "HierarchyDecorator/Scripts/Editor/HierarchyManager.cs", "7b3400fe197cde8429ece80c072e05c7" },
            { "HierarchyDecorator/Scripts/Editor/Interfaces/IDrawable.cs", "4e84f99de9c217d4fadfeaaa137b9799" },
            { "HierarchyDecorator/Scripts/Editor/Interfaces/IGroup.cs", "0f15848097b702f4392b6cd92d3d3c9f" },
            { "HierarchyDecorator/Scripts/Editor/Preferences/HierarchyDecoratorProvider.cs", "6ea2edb5d16c16a4c91bf62bd9f3a816" },
            { "HierarchyDecorator/Scripts/Editor/Preferences/SettingsPreferences.cs", "5ffb1104eab18d14caf65c56dfe566af" },
            { "HierarchyDecorator/Scripts/Editor/Settings.cs", "00668fd727de9bb4081a8a202ce24c3b" },
            { "HierarchyDecorator/Scripts/Editor/SettingsEditor.cs", "908d11beccc74d94a9adb814bf8075b6" },
            { "HierarchyDecorator/Scripts/Editor/SettingsLocalization.cs", "2a97b9e4a4a3435785dcf1acb70f0d93" },
            { "HierarchyDecorator/Scripts/Editor/Style.cs", "249019516b184f040b3acc35ff0ee7d9" },
            { "HierarchyDecorator/Scripts/Editor/Tabs/CategoryFilter.cs", "ff5de384cdcfae9449729c0da9d34781" },
            { "HierarchyDecorator/Scripts/Editor/Tabs/GeneralTab.cs", "33a90f8518809d549b4aa2e215119657" },
            { "HierarchyDecorator/Scripts/Editor/Tabs/IconInfo.cs", "d0e8cbbc0ad8ae94db18f7f2b4e03f19" },
            { "HierarchyDecorator/Scripts/Editor/Tabs/IconTab.cs", "553facbe896b260419773848510d843a" },
            { "HierarchyDecorator/Scripts/Editor/Tabs/SettingsTab.cs", "8fadab0468660454ea4ec1d05df261ac" },
            { "HierarchyDecorator/Scripts/Editor/Tabs/StyleTab.cs", "6bd8ec725ad527a498fb9219c908dbc3" },
            { "HierarchyDecorator/Scripts/Editor/Util/AssetUtility.cs", "4577e4b7e135cb64d820397794e1f6c5" },
            { "HierarchyDecorator/Scripts/Editor/Util/GUIHelper.cs", "1886e3b5518a5b74895360f2c4de57e6" },
            { "HierarchyDecorator/Scripts/Editor/Util/ReflectionUtility.cs", "77fcc26283a18dd46900bc552d2531e3" },
            { "HierarchyDecorator/Scripts/Editor/Util/SerializedPropertyUtility.cs", "df7fa969e8c3e114887991f02a47cfa0" },
            { "HierarchyDecorator/Scripts/Editor/Wooshii.HierarchyDecorator.Editor.asmdef", "e2bb8bdb348a99e4f98d1aa16a66ad1b" },
            { "LICENSE.md", "0f41716b5554ff343b5bdc4c94215e40" },
            { "README.md", "8cd8608ce60a6f94c9dcb7f5425280e3" },
            { "package.json", "ca218642cb42ab6409e84fe7fdebd3ca" },
        };

        internal static List<string> FindFiles()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in new[] { "Assets/HierarchyDecorator", "Assets/HierarchyDecorator/HierarchyDecorator" })
            {
                if (File.Exists(root + "/Scripts/Editor/Wooshii.HierarchyDecorator.Editor.asmdef") &&
                    File.Exists(root + "/Scripts/Editor/Settings.cs"))
                    roots.Add(root);
            }
            foreach (string guid in new[] { "812738d80ce9f7d45b52d937d04bc30a", "47c3d226141cd1447a88e7df4ace0329", "28321ee5d44647e4a89c3e801e5afbb3" })
            {
                string root = AssetDatabase.GUIDToAssetPath(guid);
                if (root.StartsWith("Assets/", StringComparison.Ordinal) && AssetDatabase.IsValidFolder(root))
                    roots.Add(root);
            }
            foreach (var file in Files)
            {
                string path = AssetDatabase.GUIDToAssetPath(file.Value);
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || AssetDatabase.IsValidFolder(path))
                    continue;
                paths.Add(path);
                if (file.Key.StartsWith(SourceFolder, StringComparison.Ordinal))
                {
                    string suffix = "/" + file.Key.Substring(SourceFolder.Length);
                    if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        roots.Add(path.Substring(0, path.Length - suffix.Length));
                }
            }
            // UnityPackage's outer folder can contain the source folder one level below.
            foreach (string root in new List<string>(roots))
            {
                string nested = root + "/HierarchyDecorator";
                if (File.Exists(nested + "/Scripts/Editor/Wooshii.HierarchyDecorator.Editor.asmdef") &&
                    File.Exists(nested + "/Scripts/Editor/Settings.cs"))
                    roots.Add(nested);
            }
            foreach (string root in roots)
            {
                foreach (string file in Files.Keys)
                {
                    if (!file.StartsWith(SourceFolder, StringComparison.Ordinal))
                        continue;
                    string path = root + "/" + file.Substring(SourceFolder.Length);
                    if (File.Exists(path))
                        paths.Add(path);
                }
            }
            return new List<string>(paths);
        }
    }
}
