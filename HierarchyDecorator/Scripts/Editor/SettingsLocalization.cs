using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator
{
    public enum SettingsLanguage
    {
        English = 0,
        Japanese = 1,
        TraditionalChinese = 2,
        SimplifiedChinese = 3,
    }

    /// <summary>
    /// Localized display text used by the Hierarchy Decorator settings UI.
    /// Serialized field names and internal category keys remain unchanged.
    /// </summary>
    internal static class SettingsLocalization
    {
        private static SettingsLanguage currentLanguage = SettingsLanguage.SimplifiedChinese;

        // Language names are proper names and must remain unchanged when the UI language changes.
        private static readonly string[] LanguageLabels =
        {
            "English",
            "日本語",
            "繁體中文",
            "简体中文",
        };

        private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
        {
            { "Settings.Title", new[] { "Hierarchy Settings", "階層設定", "階層設定", "层级设置" } },
            { "Settings.Repository", new[] { "GitHub Repository", "GitHub リポジトリ", "GitHub 儲存庫", "GitHub 仓库" } },
            { "Settings.Language", new[] { "Language", "言語", "語言", "语言" } },
            { "Settings.NotFound", new[] { "Cannot find settings in project", "プロジェクトに設定が見つかりません", "在專案中找不到設定", "在项目中找不到设置" } },
            { "Language.English", new[] { "English", "English", "English", "English" } },
            { "Language.Japanese", new[] { "日本語", "日本語", "日本語", "日本語" } },
            { "Language.TraditionalChinese", new[] { "繁體中文", "繁體中文", "繁體中文", "繁體中文" } },
            { "Language.SimplifiedChinese", new[] { "简体中文", "简体中文", "简体中文", "简体中文" } },

            { "Tab.General", new[] { "General", "一般", "一般", "通用" } },
            { "Tab.Visual", new[] { "Visual", "ビジュアル", "視覺", "视觉" } },
            { "Tab.Icons", new[] { "Icons", "アイコン", "圖示", "图标" } },

            { "Group.Toggles", new[] { "Toggles", "切り替え", "切換", "切换设置" } },
            { "Group.TagsLayers", new[] { "Tags & Layers", "タグとレイヤー", "標籤與圖層", "标签和层级" } },
            { "Group.TagSettings", new[] { "Tag Settings", "タグ設定", "標籤設定", "标签设置" } },
            { "Group.LayerSettings", new[] { "Layer Settings", "レイヤー設定", "圖層設定", "层级设置" } },
            { "Group.Breadcrumbs", new[] { "Breadcrumbs", "パンくずリスト", "階層路徑", "层级路径" } },
            { "Group.Instance", new[] { "Instance", "オブジェクト", "目前物件", "当前对象" } },
            { "Group.Hierarchy", new[] { "Hierarchy", "階層", "階層結構", "层级结构" } },
            { "Group.Background", new[] { "Background", "背景", "背景", "背景" } },
            { "Group.Highlight", new[] { "Highlight Settings", "ハイライト設定", "反白設定", "高亮设置" } },
            { "Group.Styles", new[] { "Styles", "スタイル", "樣式", "样式" } },
            { "Group.Settings", new[] { "Settings", "設定", "設定", "设置" } },

            { "Mode.Light", new[] { "Light Mode", "ライトモード", "淺色模式", "浅色模式" } },
            { "Mode.Dark", new[] { "Dark Mode", "ダークモード", "深色模式", "深色模式" } },
            { "Style.Add", new[] { "Add New Style", "新しいスタイルを追加", "新增樣式", "添加新样式" } },
            { "Style.None", new[] { "No styles to display.", "表示するスタイルがありません。", "沒有可顯示的樣式。", "暂无可显示的样式。" } },
            { "Style.New", new[] { "New Style", "新しいスタイル", "新樣式", "新样式" } },

            { "Icon.Title", new[] { "Icon Selection", "アイコン選択", "圖示選取", "图标选择" } },
            { "Icon.Groups", new[] { "Groups", "グループ", "群組", "分组" } },
            { "Icon.ShowAll", new[] { "Show All", "すべて表示", "顯示全部", "显示全部" } },
            { "Icon.EnableAll", new[] { "Enable All", "すべて有効化", "全部啟用", "全部启用" } },
            { "Icon.DisableAll", new[] { "Disable All", "すべて無効化", "全部停用", "全部禁用" } },
            { "Icon.AddGroup", new[] { "Add Group", "グループを追加", "新增群組", "添加分组" } },
            { "Icon.Delete", new[] { "Delete", "削除", "刪除", "删除" } },
            { "Icon.UnnamedGroup", new[] { "Unnamed Group", "名前なしグループ", "未命名群組", "未命名分组" } },
            { "Icon.AddIcon", new[] { "Add Icon", "アイコンを追加", "新增圖示", "添加图标" } },
            { "Icon.MoveUp", new[] { "Move Up", "上へ移動", "上移", "上移" } },
            { "Icon.MoveDown", new[] { "Move Down", "下へ移動", "下移", "下移" } },
            { "Icon.DeleteGroup", new[] { "Delete Group", "グループを削除", "刪除群組", "删除分组" } },
            { "Icon.NoComponent", new[] { "<No Component>", "<コンポーネントなし>", "<無元件>", "<无组件>" } },

            { "Property.showActiveToggles", new[] { "Show Active Toggles", "アクティブ切り替えを表示", "顯示狀態切換", "显示状态切换框" } },
            { "Property.activeToggleType", new[] { "Active Toggle Type", "アクティブ切り替えの種類", "狀態切換類型", "切换框样式" } },
            { "Property.activeSwiping", new[] { "Active Swiping", "ドラッグ切り替え", "拖曳切換", "拖动切换" } },
            { "Property.swipeSameState", new[] { "Swipe Same State", "同じ状態のみ切り替え", "僅切換相同狀態", "仅切换相同状态的对象" } },
            { "Property.swipeSelectionOnly", new[] { "Swipe Selection Only", "選択中のみ切り替え", "僅切換選取的物件", "仅切换选中的对象" } },
            { "Property.depthMode", new[] { "Depth Mode", "階層モード", "階層模式", "拖动选择范围" } },
            { "Property.tagLayerLayout", new[] { "Tag Layer Layout", "タグとレイヤーの配置", "標籤與圖層佈局", "标签和层级布局" } },
            { "Property.show", new[] { "Show", "表示", "顯示", "显示" } },
            { "Property.hideUntagged", new[] { "Hide Untagged", "未タグを非表示", "隱藏未標記", "隐藏未标记标签" } },
            { "Property.applyChildLayers", new[] { "Apply Child Layers", "子オブジェクトにも適用", "套用至子物件", "应用到子对象" } },
            { "Property.useSolidColor", new[] { "Use Solid Color", "単色を使用", "使用純色", "使用固定颜色" } },
            { "Property.useRandomColor", new[] { "Use Random Color", "ランダムカラーを使用", "使用隨機色彩", "使用随机颜色" } },
            { "Property.solidColor", new[] { "Solid Color", "単色", "純色", "固定颜色" } },
            { "Property.hue", new[] { "Hue", "色相", "色相", "色相" } },
            { "Property.saturation", new[] { "Saturation", "彩度", "飽和度", "饱和度" } },
            { "Property.brightness", new[] { "Brightness", "明度", "亮度", "亮度" } },
            { "Property.showBreadcrumbs", new[] { "Show Breadcrumbs", "パンくずリストを表示", "顯示階層路徑", "显示层级路径" } },
            { "Property.color", new[] { "Color", "色", "顏色", "颜色" } },
            { "Property.style", new[] { "Style", "スタイル", "樣式", "线条样式" } },
            { "Property.displayHorizontal", new[] { "Display Horizontal", "横方向に表示", "橫向顯示", "横向显示" } },
            { "Property.enableIcons", new[] { "Enable Icons", "アイコンを有効化", "啟用圖示", "启用组件图标" } },
            { "Property.clickToToggleComponent", new[] { "Click To Toggle Component", "クリックでコンポーネントを切り替え", "點擊圖示切換元件", "点击图标切换组件" } },
            { "Property.stackDuplicateIcons", new[] { "Stack Duplicate Icons", "重複アイコンを重ねる", "堆疊重複圖示", "堆叠重复图标" } },
            { "Property.showMissingScriptWarning", new[] { "Show Missing Script Warning", "欠落スクリプト警告を表示", "顯示遺失腳本警告", "显示缺失脚本警告" } },
            { "Property.twoToneBackground", new[] { "Two Tone Background", "2色の背景", "使用雙色背景", "使用双色背景" } },
            { "Property.showSceneItemHighlight", new[] { "Show Scene Item Highlight", "現在のオブジェクトを強調表示", "反白目前物件", "高亮当前对象" } },
            { "Property.lineThickness", new[] { "Line Thickness", "線の太さ", "線條粗細", "线条粗细" } },
            { "Property.displayTags", new[] { "Display Tags", "タグを表示", "顯示標籤", "显示标签" } },
            { "Property.displayLayers", new[] { "Display Layers", "レイヤーを表示", "顯示圖層", "显示层级" } },
            { "Property.displayIcons", new[] { "Display Icons", "アイコンを表示", "顯示圖示", "显示图标" } },
            { "Property.prefix", new[] { "Prefix", "プレフィックス", "前綴", "前缀" } },
            { "Property.noSpaceAfterPrefix", new[] { "No Space After Prefix", "プレフィックス後に空白なし", "前綴後不加空格", "前缀后不需要空格" } },
            { "Property.isRegex", new[] { "Is Regex", "正規表現を使用", "使用正規表示式", "使用正则表达式" } },
            { "Property.name", new[] { "Name", "名前", "名稱", "样式名称" } },
            { "Property.font", new[] { "Font", "フォント", "字型", "字体" } },
            { "Property.fontSize", new[] { "Font Size", "フォントサイズ", "字型大小", "字号" } },
            { "Property.fontStyle", new[] { "Font Style", "フォントスタイル", "字型樣式", "字体样式" } },
            { "Property.fontAlignment", new[] { "Font Alignment", "文字の配置", "文字對齊", "文字对齐" } },
            { "Property.textFormatting", new[] { "Text Formatting", "文字フォーマット", "文字格式", "文字格式" } },
            { "Property.fontColour", new[] { "Font Color", "文字色", "文字顏色", "文字颜色" } },
            { "Property.backgroundColour", new[] { "Background Color", "背景色", "背景顏色", "背景颜色" } },
            { "Property.colorOne", new[] { "Color 1", "色 1", "顏色 1", "颜色 1" } },
            { "Property.colorTwo", new[] { "Color 2", "色 2", "顏色 2", "颜色 2" } },

            { "Tooltip.activeSwiping", new[] { "Clicking and dragging over check boxes to toggle them.", "チェックボックスをクリックしてドラッグすると切り替えます。", "點擊並拖曳核取方塊即可切換。", "在层级窗口中拖动复选框以切换对象状态。" } },
            { "Tooltip.swipeSameState", new[] { "Only toggle the instances with the same state as the first selected.", "最初に選択したオブジェクトと同じ状態のみ切り替えます。", "僅切換與第一個選取物件狀態相同的物件。", "仅切换与第一个选中对象状态相同的对象。" } },
            { "Tooltip.swipeSelectionOnly", new[] { "If a selection exists, only toggle the selected instances.", "選択中のオブジェクトのみ切り替えます。", "有選取項目時，僅切換選取的物件。", "存在选择时，仅切换选中的对象。" } },
            { "Tooltip.depthMode", new[] { "The accepted criteria for selecting instances when swiping.", "ドラッグ切り替え時に選択できる階層範囲です。", "拖曳切換時允許選取的階層範圍。", "拖动切换时允许选择的对象层级范围。" } },
            { "Tooltip.clickToToggleComponent", new[] { "Will clicking the icon toggle the component", "アイコンをクリックするとコンポーネントを切り替えます。", "點擊圖示時切換元件。", "点击组件图标时切换该组件。" } },

            { "Enum.ToggleType.Checkbox", new[] { "Checkbox", "チェックボックス", "核取方塊", "复选框" } },
            { "Enum.ToggleType.Dot", new[] { "Dot", "ドット", "圓點", "圆点" } },
            { "Enum.DepthMode.All", new[] { "All", "すべての階層", "全部階層", "全部层级" } },
            { "Enum.DepthMode.SameDepth", new[] { "Same Depth", "同じ階層", "相同階層", "同级" } },
            { "Enum.DepthMode.SameDepthOrLower", new[] { "Same Depth Or Lower", "同じ階層以下", "相同階層或更低", "同级或更低" } },
            { "Enum.DepthMode.SameDepthOrHigher", new[] { "Same Depth Or Higher", "同じ階層以上", "相同階層或更高", "同级或更高" } },
            { "Enum.TagLayerLayout.TagInFront", new[] { "Tag In Front", "タグを前面に表示", "標籤在前", "标签在前" } },
            { "Enum.TagLayerLayout.LayerInFront", new[] { "Layer In Front", "レイヤーを前面に表示", "圖層在前", "层级在前" } },
            { "Enum.TagLayerLayout.TagAbove", new[] { "Tag Above", "タグを上に表示", "標籤在上", "标签在上" } },
            { "Enum.TagLayerLayout.LayerAbove", new[] { "Layer Above", "レイヤーを上に表示", "圖層在上", "层级在上" } },
            { "Enum.BreadcrumbStyle.Solid", new[] { "Solid", "実線", "實線", "实线" } },
            { "Enum.BreadcrumbStyle.Dash", new[] { "Dash", "破線", "虛線", "虚线" } },
            { "Enum.BreadcrumbStyle.Dotted", new[] { "Dotted", "点線", "點線", "点线" } },
            { "Enum.DisplayMode.Unity", new[] { "Unity", "Unity コンポーネント", "Unity 元件", "Unity 组件" } },
            { "Enum.DisplayMode.Custom", new[] { "Custom", "カスタムコンポーネント", "自訂元件", "自定义组件" } },
            { "Enum.TextFormatting.ToUpper", new[] { "To Upper", "大文字に変換", "轉為大寫", "转为大写" } },
            { "Enum.TextFormatting.ToLower", new[] { "To Lower", "小文字に変換", "轉為小寫", "转为小写" } },
            { "Enum.TextFormatting.DontChange", new[] { "Don't Change", "変更しない", "保持不變", "保持不变" } },
            { "Enum.FontStyle.Normal", new[] { "Normal", "標準", "一般", "常规" } },
            { "Enum.FontStyle.Bold", new[] { "Bold", "太字", "粗體", "粗体" } },
            { "Enum.FontStyle.Italic", new[] { "Italic", "斜体", "斜體", "斜体" } },
            { "Enum.FontStyle.BoldAndItalic", new[] { "Bold And Italic", "太字斜体", "粗體斜體", "粗体斜体" } },
            { "Enum.TextAnchor.UpperLeft", new[] { "Upper Left", "左上", "左上", "左上" } },
            { "Enum.TextAnchor.UpperCenter", new[] { "Upper Center", "上中央", "上方居中", "上方居中" } },
            { "Enum.TextAnchor.UpperRight", new[] { "Upper Right", "右上", "右上", "右上" } },
            { "Enum.TextAnchor.MiddleLeft", new[] { "Middle Left", "中央左", "左側置中", "左侧居中" } },
            { "Enum.TextAnchor.MiddleCenter", new[] { "Middle Center", "中央", "正中", "正中" } },
            { "Enum.TextAnchor.MiddleRight", new[] { "Middle Right", "中央右", "右側置中", "右侧居中" } },
            { "Enum.TextAnchor.LowerLeft", new[] { "Lower Left", "左下", "左下", "左下" } },
            { "Enum.TextAnchor.LowerCenter", new[] { "Lower Center", "下中央", "下方居中", "下方居中" } },
            { "Enum.TextAnchor.LowerRight", new[] { "Lower Right", "右下", "右下", "右下" } },

            { "Category.Excluded", new[] { "Excluded", "除外", "已排除", "已排除" } },
            { "Category.All", new[] { "All", "すべて", "全部", "全部" } },
            { "Category.Custom", new[] { "Custom", "カスタム", "自訂", "自定义" } },
            { "Category.General", new[] { "General", "一般", "一般", "通用" } },
            { "Category.2D", new[] { "2D", "2D", "2D", "2D" } },
            { "Category.Animation", new[] { "Animation", "アニメーション", "動畫", "动画" } },
            { "Category.Audio", new[] { "Audio", "オーディオ", "音訊", "音频" } },
            { "Category.Mesh", new[] { "Mesh", "メッシュ", "網格", "网格" } },
            { "Category.Physics", new[] { "Physics", "物理", "物理", "物理" } },
            { "Category.Network", new[] { "Network", "ネットワーク", "網路", "网络" } },
            { "Category.UI", new[] { "UI", "UI", "UI", "界面" } },
            { "Category.Other", new[] { "Other", "その他", "其他", "其他" } },
        };

        private static readonly Dictionary<string, string> PropertyLabelKeys = new Dictionary<string, string>
        {
            { "showActiveToggles", "Property.showActiveToggles" },
            { "activeToggleType", "Property.activeToggleType" },
            { "activeSwiping", "Property.activeSwiping" },
            { "swipeSameState", "Property.swipeSameState" },
            { "swipeSelectionOnly", "Property.swipeSelectionOnly" },
            { "depthMode", "Property.depthMode" },
            { "tagLayerLayout", "Property.tagLayerLayout" },
            { "show", "Property.show" },
            { "hideUntagged", "Property.hideUntagged" },
            { "applyChildLayers", "Property.applyChildLayers" },
            { "useSolidColor", "Property.useSolidColor" },
            { "useRandomColor", "Property.useRandomColor" },
            { "solidColor", "Property.solidColor" },
            { "hue", "Property.hue" },
            { "saturation", "Property.saturation" },
            { "brightness", "Property.brightness" },
            { "showBreadcrumbs", "Property.showBreadcrumbs" },
            { "color", "Property.color" },
            { "style", "Property.style" },
            { "displayHorizontal", "Property.displayHorizontal" },
            { "enableIcons", "Property.enableIcons" },
            { "clickToToggleComponent", "Property.clickToToggleComponent" },
            { "stackDuplicateIcons", "Property.stackDuplicateIcons" },
            { "showMissingScriptWarning", "Property.showMissingScriptWarning" },
            { "twoToneBackground", "Property.twoToneBackground" },
            { "showSceneItemHighlight", "Property.showSceneItemHighlight" },
            { "lineThickness", "Property.lineThickness" },
            { "displayTags", "Property.displayTags" },
            { "displayLayers", "Property.displayLayers" },
            { "displayIcons", "Property.displayIcons" },
            { "prefix", "Property.prefix" },
            { "noSpaceAfterPrefix", "Property.noSpaceAfterPrefix" },
            { "isRegex", "Property.isRegex" },
            { "name", "Property.name" },
            { "font", "Property.font" },
            { "fontSize", "Property.fontSize" },
            { "fontStyle", "Property.fontStyle" },
            { "fontAlignment", "Property.fontAlignment" },
            { "textFormatting", "Property.textFormatting" },
            { "fontColour", "Property.fontColour" },
            { "backgroundColour", "Property.backgroundColour" },
            { "colorOne", "Property.colorOne" },
            { "colorTwo", "Property.colorTwo" },
        };

        private static readonly Dictionary<string, string> PropertyTooltipKeys = new Dictionary<string, string>
        {
            { "activeSwiping", "Tooltip.activeSwiping" },
            { "swipeSameState", "Tooltip.swipeSameState" },
            { "swipeSelectionOnly", "Tooltip.swipeSelectionOnly" },
            { "depthMode", "Tooltip.depthMode" },
            { "clickToToggleComponent", "Tooltip.clickToToggleComponent" },
        };

        public static SettingsLanguage CurrentLanguage => currentLanguage;

        public static void SetLanguage(SettingsLanguage language)
        {
            if ((int)language < 0 || (int)language > (int)SettingsLanguage.SimplifiedChinese)
            {
                language = SettingsLanguage.SimplifiedChinese;
            }

            currentLanguage = language;
        }

        public static SettingsLanguage GetSystemLanguage()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Japanese:
                    return SettingsLanguage.Japanese;
                case SystemLanguage.ChineseTraditional:
                    return SettingsLanguage.TraditionalChinese;
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                    return SettingsLanguage.SimplifiedChinese;
                case SystemLanguage.English:
                default:
                    return SettingsLanguage.English;
            }
        }

        public static string Text(string key)
        {
            string[] values;
            if (!Texts.TryGetValue(key, out values))
            {
                return key;
            }

            int index = Mathf.Clamp((int)currentLanguage, 0, values.Length - 1);
            return values[index];
        }

        public static string GetLanguageLabel(SettingsLanguage language)
        {
            int index = Mathf.Clamp((int)language, 0, LanguageLabels.Length - 1);
            return LanguageLabels[index];
        }

        public static SettingsLanguage DrawLanguagePopup(SettingsLanguage language)
        {
            int selected = Mathf.Clamp((int)language, 0, LanguageLabels.Length - 1);
            selected = EditorGUILayout.Popup(Text("Settings.Language"), selected, LanguageLabels);
            return (SettingsLanguage)Mathf.Clamp(selected, 0, LanguageLabels.Length - 1);
        }

        public static GUIContent GetPropertyContent(SerializedProperty property)
        {
            if (property == null)
            {
                return GUIContent.none;
            }

            string tooltipKey;
            string tooltip = property.tooltip;
            if (PropertyTooltipKeys.TryGetValue(property.name, out tooltipKey))
            {
                tooltip = Text(tooltipKey);
            }

            return new GUIContent(GetPropertyLabel(property), tooltip);
        }

        public static string GetPropertyLabel(SerializedProperty property)
        {
            if (property == null)
            {
                return string.Empty;
            }

            string labelKey;
            if (PropertyLabelKeys.TryGetValue(property.name, out labelKey))
            {
                return Text(labelKey);
            }

            return property.displayName;
        }

        public static string[] GetEnumLabels(SerializedProperty property)
        {
            string[] enumNames = property.enumNames;
            string[] labels = new string[enumNames.Length];

            for (int i = 0; i < enumNames.Length; i++)
            {
                labels[i] = GetEnumLabel(property, enumNames[i]);
            }

            return labels;
        }

        public static string GetEnumLabel(SerializedProperty property, string enumName)
        {
            string key = "Enum." + property.type + "." + enumName;
            if (Texts.ContainsKey(key))
            {
                return Text(key);
            }

            return enumName;
        }

        public static string GetCategoryLabel(string category)
        {
            string key = "Category." + category;
            if (category == "Visual")
            {
                key = "Tab.Visual";
            }
            else if (category == "Icons")
            {
                key = "Tab.Icons";
            }
            else if (category == "General")
            {
                key = "Tab.General";
            }

            return Texts.ContainsKey(key) ? Text(key) : category;
        }

        public static void PropertyField(SerializedProperty property, bool includeChildren = true)
        {
            if (property == null)
            {
                return;
            }

            if (property.propertyType == SerializedPropertyType.Enum && !property.hasMultipleDifferentValues)
            {
                int value = EditorGUILayout.Popup(GetPropertyLabel(property), property.enumValueIndex, GetEnumLabels(property));
                if (value >= 0 && value < property.enumNames.Length)
                {
                    property.enumValueIndex = value;
                }
                return;
            }

            EditorGUILayout.PropertyField(property, GetPropertyContent(property), includeChildren);
        }

        public static void PropertyField(Rect rect, SerializedProperty property, bool includeChildren = true)
        {
            if (property == null)
            {
                return;
            }

            if (property.propertyType == SerializedPropertyType.Enum && !property.hasMultipleDifferentValues)
            {
                int value = EditorGUI.Popup(rect, GetPropertyLabel(property), property.enumValueIndex, GetEnumLabels(property));
                if (value >= 0 && value < property.enumNames.Length)
                {
                    property.enumValueIndex = value;
                }
                return;
            }

            EditorGUI.PropertyField(rect, property, GetPropertyContent(property), includeChildren);
        }
    }
}
