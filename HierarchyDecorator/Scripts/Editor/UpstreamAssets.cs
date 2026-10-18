using System.Collections.Generic;
using UnityEditor;

namespace VRCLearn.HierarchyDecorator
{
    internal static class UpstreamAssets
    {
        // Original distributed asset identities. Never remove files solely by folder name.
        private static readonly string[] Guids =
        {
            "f515db8d25e6d754b8d09e8b97e88454",
            "73e1a3fd64b18554391ad2f95d6060a3",
            "192aebadabb1aec469ee02046f2367a9",
            "31084919640aa4443bfef68726a55f65",
            "af7eb40c43d31a64e8974d27a427ab61",
            "48a479cbe55ff6a4e89fc4676a39b94d",
            "c2708b2dd0efd0e41b7100f9b3a84c2c",
            "fa507b92e013b2a4487185098b991169",
            "9917b346e29d4fc4bb68b38ebcd8162a",
            "7b19ee6f06cf2c848b65ff1fd053a7e4",
            "fdf4218929f3371419f6170aa975e9ec",
            "2274f94d4635da54b93542a4ef71e72a",
            "af5a5557b6503764b8bfe4bdc7c4b06e",
            "fbee4186caa409f4597b8b1914379c43",
            "6d7aa63d6cd07354bb49348b15839053",
            "0dee322f6d6313c47a8babc2adc14e1f",
            "2c154e63f1b989343b173d9de5e62d28",
            "7be28652b1b91d64facd8ed6d378d359",
            "82265ea1a6be8d84287eb5a2fe26af68",
            "22fde4c9ca90c41418fbc592b357ccb3",
            "03d0381539029194d928324e60c7ba96",
            "ba80f4e2aedd36045b183c6b693627e8",
            "7ace86b840da1ed40a57bbfa17e8ce2f",
            "5ec39671c1c150644ba3e43d70ae72cc",
            "3d3153fd97d96064b8e6898e62966a25",
            "78cc1eecce018a047b2649c872322057",
            "862f460517b87be49aa70fe6e76d90dd",
            "a93f12baa008f27449b145da87efb41e",
            "7b3400fe197cde8429ece80c072e05c7",
            "4e84f99de9c217d4fadfeaaa137b9799",
            "0f15848097b702f4392b6cd92d3d3c9f",
            "6ea2edb5d16c16a4c91bf62bd9f3a816",
            "5ffb1104eab18d14caf65c56dfe566af",
            "00668fd727de9bb4081a8a202ce24c3b",
            "908d11beccc74d94a9adb814bf8075b6",
            "2a97b9e4a4a3435785dcf1acb70f0d93",
            "249019516b184f040b3acc35ff0ee7d9",
            "ff5de384cdcfae9449729c0da9d34781",
            "33a90f8518809d549b4aa2e215119657",
            "d0e8cbbc0ad8ae94db18f7f2b4e03f19",
            "553facbe896b260419773848510d843a",
            "8fadab0468660454ea4ec1d05df261ac",
            "6bd8ec725ad527a498fb9219c908dbc3",
            "4577e4b7e135cb64d820397794e1f6c5",
            "1886e3b5518a5b74895360f2c4de57e6",
            "77fcc26283a18dd46900bc552d2531e3",
            "df7fa969e8c3e114887991f02a47cfa0",
            "e2bb8bdb348a99e4f98d1aa16a66ad1b",
            "0f41716b5554ff343b5bdc4c94215e40",
            "8cd8608ce60a6f94c9dcb7f5425280e3",
            "ca218642cb42ab6409e84fe7fdebd3ca",
        };

        internal static List<string> FindFiles()
        {
            var paths = new List<string>();
            foreach (string guid in Guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/", System.StringComparison.Ordinal) && !AssetDatabase.IsValidFolder(path))
                    paths.Add(path);
            }
            return paths;
        }
    }
}
