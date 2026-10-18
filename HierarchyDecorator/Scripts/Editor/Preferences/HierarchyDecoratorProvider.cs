using System.IO;
using UnityEditor;

namespace HierarchyDecorator
{
    // Register the Settings for the HiearchyDecorator
    public static class HierarchyDecoratorProvider
    {
        private const string LanguageMenu = "Tools/Hierarchy Decorator/Language/";
        private const string EnglishMenu = LanguageMenu + "English";
        private const string JapaneseMenu = LanguageMenu + "日本語";
        private const string KoreanMenu = LanguageMenu + "한국어";
        private const string TraditionalChineseMenu = LanguageMenu + "繁體中文";
        private const string SimplifiedChineseMenu = LanguageMenu + "简体中文";

        private static string Path => Constants.Paths.SETTINGS_PATH;

        public static SettingsProvider[] tabs =
        {
            new SettingsPreferences (Path, SettingsScope.User),
        };

        [MenuItem ("Tools/Hierarchy Decorator/Settings")]
        private static void OpenSettings()
        {
            SettingsService.OpenUserPreferences (Path);
        }

        [MenuItem (EnglishMenu)]
        private static void SelectEnglish() => SelectLanguage (SettingsLanguage.English);

        [MenuItem (JapaneseMenu)]
        private static void SelectJapanese() => SelectLanguage (SettingsLanguage.Japanese);

        [MenuItem (KoreanMenu)]
        private static void SelectKorean() => SelectLanguage (SettingsLanguage.Korean);

        [MenuItem (TraditionalChineseMenu)]
        private static void SelectTraditionalChinese() => SelectLanguage (SettingsLanguage.TraditionalChinese);

        [MenuItem (SimplifiedChineseMenu)]
        private static void SelectSimplifiedChinese() => SelectLanguage (SettingsLanguage.SimplifiedChinese);

        [MenuItem (EnglishMenu, true)]
        private static bool ValidateEnglish() => ValidateLanguage (EnglishMenu, SettingsLanguage.English);

        [MenuItem (JapaneseMenu, true)]
        private static bool ValidateJapanese() => ValidateLanguage (JapaneseMenu, SettingsLanguage.Japanese);

        [MenuItem (KoreanMenu, true)]
        private static bool ValidateKorean() => ValidateLanguage (KoreanMenu, SettingsLanguage.Korean);

        [MenuItem (TraditionalChineseMenu, true)]
        private static bool ValidateTraditionalChinese() => ValidateLanguage (TraditionalChineseMenu, SettingsLanguage.TraditionalChinese);

        [MenuItem (SimplifiedChineseMenu, true)]
        private static bool ValidateSimplifiedChinese() => ValidateLanguage (SimplifiedChineseMenu, SettingsLanguage.SimplifiedChinese);

        private static void SelectLanguage(SettingsLanguage language)
        {
            SettingsLocalization.Language = language;
            SettingsService.RepaintAllSettingsWindow ();
        }

        private static bool ValidateLanguage(string menuPath, SettingsLanguage language)
        {
            Menu.SetChecked (menuPath, SettingsLocalization.Language == language);
            return true;
        }

        [SettingsProviderGroup]
        public static SettingsProvider[] Load()
        {
            return tabs;
        }
    }

}
