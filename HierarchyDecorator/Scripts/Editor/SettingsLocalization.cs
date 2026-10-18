using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VRCLearn.HierarchyDecorator
{
    internal enum SettingsLanguage
    {
        Auto,
        English,
        Japanese,
        Korean,
        TraditionalChinese,
        SimplifiedChinese
    }

    internal static class SettingsLocalization
    {
        private const string PreferenceKey = "com.vrclearn.hierarchydecorator.language";
        private static readonly string[] LanguageCodes = { "auto", "en", "ja", "ko", "zh-Hant", "zh-Hans" };
        private static readonly string[] LanguageNames = { "Auto", "English", "日本語", "한국어", "繁體中文", "简体中文" };

        // English source text is the fallback. Columns: Japanese, Korean, traditional and simplified Chinese.
        private static readonly Dictionary<string, string[]> Translations = new Dictionary<string, string[]>
        {
            { "Language", new[] { "言語", "언어", "語言", "语言" } },
            { "Migrate from wooshii", new[] { "wooshii 版から移行", "wooshii 버전에서 마이그레이션", "從 wooshii 版遷移", "从 wooshii 版迁移" } },
            { "Choosing a preset keeps wooshii installed. Migration imports the selected settings, then removes the wooshii installation and the selected old settings asset.", new[] { "プリセットを選ぶと wooshii 版は残ります。移行すると選択した設定をインポートし、wooshii 版と選択した旧設定アセットを削除します。", "프리셋을 선택하면 wooshii 버전이 유지됩니다. 마이그레이션하면 선택한 설정을 가져온 다음 wooshii 설치와 선택한 이전 설정 에셋을 제거합니다.", "選擇預設會保留 wooshii 版。遷移會先匯入選定設定，再移除 wooshii 安裝及選定的舊設定資產。", "选择预设会保留 wooshii 版。迁移会先导入所选设置，再移除 wooshii 安装及所选的旧设置资产。" } },
            { "Settings source", new[] { "設定のコピー元", "설정 원본", "設定來源", "设置来源" } },
            { "Migrate settings and remove wooshii", new[] { "設定を移行して wooshii 版を削除", "설정 마이그레이션 및 wooshii 제거", "遷移設定並移除 wooshii", "一键迁移并移除 wooshii" } },
            { "Migrating...", new[] { "移行中…", "마이그레이션 중…", "遷移中…", "正在迁移…" } },
            { "Choose a preset style", new[] { "プリセットスタイルを選択", "프리셋 스타일 선택", "選擇預設樣式", "选择预设样式" } },
            { "Detailed", new[] { "詳細", "상세", "詳細", "详细" } },
            { "Minimal", new[] { "シンプル", "간결", "精簡", "精简" } },
            { "Hierarchy Settings", new[] { "Hierarchy 設定", "하이어라키 설정", "Hierarchy 設定", "Hierarchy 设置" } },
            { "GitHub Repository", new[] { "GitHub リポジトリ", "GitHub 저장소", "GitHub 儲存庫", "GitHub 仓库" } },
            { "Cannot find settings in project", new[] { "プロジェクトに設定が見つかりません", "프로젝트에서 설정을 찾을 수 없습니다", "在專案中找不到設定", "在项目中找不到设置" } },
            { "General", new[] { "一般", "일반", "一般", "常规" } },
            { "Visual", new[] { "外観", "모양", "外觀", "外观" } },
            { "Icons", new[] { "アイコン", "아이콘", "圖示", "图标" } },
            { "Toggles", new[] { "有効状態の切り替え", "활성 상태 전환", "啟用狀態切換", "启用状态切换" } },
            { "Tags & Layers", new[] { "タグとレイヤー", "태그 및 레이어", "標籤與圖層", "标签与图层" } },
            { "Tag Settings", new[] { "タグ設定", "태그 설정", "標籤設定", "标签设置" } },
            { "Layer Settings", new[] { "レイヤー設定", "레이어 설정", "圖層設定", "图层设置" } },
            { "Breadcrumbs", new[] { "階層ガイド", "계층 안내선", "階層引導線", "层级引导线" } },
            { "Instance", new[] { "オブジェクト", "오브젝트", "物件", "对象" } },
            { "Hierarchy", new[] { "階層全体", "전체 계층", "整體階層", "整体层级" } },
            { "Background", new[] { "背景", "배경", "背景", "背景" } },
            { "Light Mode", new[] { "ライトモード", "라이트 모드", "淺色模式", "浅色模式" } },
            { "Dark Mode", new[] { "ダークモード", "다크 모드", "深色模式", "深色模式" } },
            { "Highlight Settings", new[] { "ハイライト設定", "강조 설정", "醒目提示設定", "高亮设置" } },
            { "Styles", new[] { "スタイル", "스타일", "樣式", "样式" } },
            { "Settings", new[] { "設定", "설정", "設定", "设置" } },
            { "Icon Selection", new[] { "アイコンの選択", "아이콘 선택", "圖示選擇", "图标选择" } },
            { "Groups", new[] { "グループ", "그룹", "群組", "分组" } },
            { "Excluded", new[] { "除外", "제외", "排除", "排除" } },
            { "All", new[] { "すべて", "모두", "全部", "全部" } },
            { "Custom", new[] { "カスタム", "사용자 정의", "自訂", "自定义" } },
            { "Enable All", new[] { "すべて有効", "모두 활성화", "全部啟用", "全部启用" } },
            { "Disable All", new[] { "すべて無効", "모두 비활성화", "全部停用", "全部禁用" } },
            { "Add Group", new[] { "グループを追加", "그룹 추가", "新增群組", "添加分组" } },
            { "Move Up", new[] { "上へ移動", "위로 이동", "上移", "上移" } },
            { "Move Down", new[] { "下へ移動", "아래로 이동", "下移", "下移" } },
            { "Delete Group", new[] { "グループを削除", "그룹 삭제", "刪除群組", "删除分组" } },
            { "Group Options", new[] { "グループの操作", "그룹 옵션", "群組選項", "分组选项" } },
            { "Add Icon", new[] { "アイコンを追加", "아이콘 추가", "新增圖示", "添加图标" } },
            { "Delete", new[] { "削除", "삭제", "刪除", "删除" } },
            { "<No Component>", new[] { "<コンポーネントなし>", "<컴포넌트 없음>", "<無元件>", "<无组件>" } },
            { "Add New Style", new[] { "スタイルを追加", "스타일 추가", "新增樣式", "添加样式" } },
            { "No styles to display.", new[] { "表示するスタイルがありません。", "표시할 스타일이 없습니다.", "沒有可顯示的樣式。", "没有可显示的样式。" } },
            { "Animation", new[] { "アニメーション", "애니메이션", "動畫", "动画" } },
            { "Audio", new[] { "オーディオ", "오디오", "音訊", "音频" } },
            { "Mesh", new[] { "メッシュ", "메시", "網格", "网格" } },
            { "Physics", new[] { "物理", "물리", "物理", "物理" } },
            { "Network", new[] { "ネットワーク", "네트워크", "網路", "网络" } },
            { "Show Active Toggles", new[] { "有効状態の切り替えを表示", "활성 상태 토글 표시", "顯示啟用狀態切換", "显示启用状态开关" } },
            { "Active Toggle Type", new[] { "切り替えの表示形式", "활성 상태 토글 유형", "狀態切換類型", "状态开关类型" } },
            { "Active Swiping", new[] { "ドラッグで切り替え", "드래그로 전환", "拖曳切換", "拖动切换" } },
            { "Swipe Same State", new[] { "同じ状態のみ切り替え", "같은 상태만 전환", "僅切換相同狀態", "仅切换相同状态" } },
            { "Swipe Selection Only", new[] { "選択中のみ切り替え", "선택 항목만 전환", "僅切換選取項目", "仅切换选中对象" } },
            { "Depth Mode", new[] { "階層の範囲", "계층 범위", "階層範圍", "层级范围" } },
            { "Tag Layer Layout", new[] { "タグとレイヤーの配置", "태그 및 레이어 배치", "標籤與圖層配置", "标签与图层布局" } },
            { "Show", new[] { "表示", "표시", "顯示", "显示" } },
            { "Hide Untagged", new[] { "Untagged を非表示", "Untagged 숨기기", "隱藏 Untagged", "隐藏 Untagged" } },
            { "Use Solid Color", new[] { "単色を使用", "단색 사용", "使用單色", "使用单色" } },
            { "Solid Color", new[] { "単色", "단색", "單色", "单色" } },
            { "Use Random Color", new[] { "ランダムな色を使用", "임의 색상 사용", "使用隨機色彩", "使用随机颜色" } },
            { "Hue", new[] { "色相", "색조", "色相", "色相" } },
            { "Saturation", new[] { "彩度", "채도", "飽和度", "饱和度" } },
            { "Brightness", new[] { "明るさ", "밝기", "亮度", "亮度" } },
            { "Apply Child Layers", new[] { "子にもレイヤーを適用", "자식에도 레이어 적용", "套用至子物件圖層", "应用到子对象图层" } },
            { "Show Breadcrumbs", new[] { "階層ガイドを表示", "계층 안내선 표시", "顯示階層引導線", "显示层级引导线" } },
            { "Color", new[] { "色", "색상", "色彩", "颜色" } },
            { "Style", new[] { "スタイル", "스타일", "樣式", "样式" } },
            { "Display Horizontal", new[] { "横線を表示", "가로선 표시", "顯示水平線", "显示水平线" } },
            { "Enable Icons", new[] { "アイコンを有効化", "아이콘 활성화", "啟用圖示", "启用图标" } },
            { "Click To Toggle Component", new[] { "クリックでコンポーネントを切り替え", "클릭으로 컴포넌트 전환", "點擊切換元件", "点击切换组件" } },
            { "Stack Duplicate Icons", new[] { "重複アイコンをまとめる", "중복 아이콘 합치기", "合併重複圖示", "合并重复图标" } },
            { "Show Missing Script Warning", new[] { "スクリプト欠落の警告を表示", "누락된 스크립트 경고 표시", "顯示遺失腳本警告", "显示缺失脚本警告" } },
            { "Show All", new[] { "すべて表示", "모두 표시", "全部顯示", "全部显示" } },
            { "Two Tone Background", new[] { "交互の背景色", "교차 배경색", "交替背景色", "交替背景色" } },
            { "Show Scene Item Highlight", new[] { "シーンのハイライトを表示", "씬 항목 강조 표시", "顯示場景項目醒目提示", "显示场景项高亮" } },
            { "Line Thickness", new[] { "線の太さ", "선 두께", "線條粗細", "线条粗细" } },
            { "Display Tags", new[] { "タグを表示", "태그 표시", "顯示標籤", "显示标签" } },
            { "Display Layers", new[] { "レイヤーを表示", "레이어 표시", "顯示圖層", "显示图层" } },
            { "Display Icons", new[] { "アイコンを表示", "아이콘 표시", "顯示圖示", "显示图标" } },
            { "Prefix", new[] { "接頭辞", "접두사", "前綴", "前缀" } },
            { "No Space After Prefix", new[] { "接頭辞の後に空白を入れない", "접두사 뒤 공백 없음", "前綴後不加空格", "前缀后不加空格" } },
            { "Is Regex", new[] { "正規表現を使用", "정규식 사용", "使用正規表示式", "使用正则表达式" } },
            { "Name", new[] { "名前", "이름", "名稱", "名称" } },
            { "Font", new[] { "フォント", "글꼴", "字型", "字体" } },
            { "Font Size", new[] { "文字サイズ", "글꼴 크기", "字型大小", "字号" } },
            { "Font Style", new[] { "フォントスタイル", "글꼴 스타일", "字型樣式", "字体样式" } },
            { "Font Alignment", new[] { "文字の配置", "텍스트 정렬", "文字對齊", "文字对齐" } },
            { "Text Formatting", new[] { "文字の変換", "텍스트 변환", "文字轉換", "文字转换" } },
            { "Font Colour", new[] { "文字色", "글자 색상", "文字色彩", "文字颜色" } },
            { "Background Colour", new[] { "背景色", "배경색", "背景色彩", "背景颜色" } },
            { "Color One", new[] { "色 1", "색상 1", "色彩 1", "颜色 1" } },
            { "Color Two", new[] { "色 2", "색상 2", "色彩 2", "颜色 2" } },
            { "Checkbox", new[] { "チェックボックス", "체크박스", "核取方塊", "复选框" } },
            { "Dot", new[] { "点", "점", "圓點", "圆点" } },
            { "Same Depth", new[] { "同じ階層", "같은 깊이", "相同深度", "相同深度" } },
            { "Same Depth Or Lower", new[] { "同じ階層または下位", "같은 깊이 또는 하위", "相同深度或下層", "相同深度或下层" } },
            { "Same Depth Or Higher", new[] { "同じ階層または上位", "같은 깊이 또는 상위", "相同深度或上層", "相同深度或上层" } },
            { "Tag In Front", new[] { "タグを前に配置", "태그를 앞에 배치", "標籤在前", "标签在前" } },
            { "Layer In Front", new[] { "レイヤーを前に配置", "레이어를 앞에 배치", "圖層在前", "图层在前" } },
            { "Tag Above", new[] { "タグを上に配置", "태그를 위에 배치", "標籤在上", "标签在上" } },
            { "Layer Above", new[] { "レイヤーを上に配置", "레이어를 위에 배치", "圖層在上", "图层在上" } },
            { "Solid", new[] { "実線", "실선", "實線", "实线" } },
            { "Dash", new[] { "破線", "파선", "虛線", "虚线" } },
            { "Dotted", new[] { "点線", "점선", "點線", "点线" } },
            { "Normal", new[] { "標準", "보통", "一般", "常规" } },
            { "Bold", new[] { "太字", "굵게", "粗體", "粗体" } },
            { "Italic", new[] { "斜体", "기울임꼴", "斜體", "斜体" } },
            { "Bold And Italic", new[] { "太字と斜体", "굵게 및 기울임꼴", "粗斜體", "粗斜体" } },
            { "Upper Left", new[] { "左上", "왼쪽 위", "左上", "左上" } },
            { "Upper Center", new[] { "上中央", "가운데 위", "上方置中", "上方居中" } },
            { "Upper Right", new[] { "右上", "오른쪽 위", "右上", "右上" } },
            { "Middle Left", new[] { "左中央", "왼쪽 가운데", "左側置中", "左侧居中" } },
            { "Middle Center", new[] { "中央", "가운데", "置中", "居中" } },
            { "Middle Right", new[] { "右中央", "오른쪽 가운데", "右側置中", "右侧居中" } },
            { "Lower Left", new[] { "左下", "왼쪽 아래", "左下", "左下" } },
            { "Lower Center", new[] { "下中央", "가운데 아래", "下方置中", "下方居中" } },
            { "Lower Right", new[] { "右下", "오른쪽 아래", "右下", "右下" } },
            { "To Upper", new[] { "大文字に変換", "대문자로 변환", "轉為大寫", "转为大写" } },
            { "To Lower", new[] { "小文字に変換", "소문자로 변환", "轉為小寫", "转为小写" } },
            { "Dont Change", new[] { "変更しない", "변경 안 함", "不變更", "不更改" } },
            { "Clicking and dragging over check boxes to toggle them.", new[] { "チェックボックスをクリックしてドラッグすると切り替えます。", "체크박스를 클릭한 채 드래그하여 상태를 전환합니다.", "點擊並拖曳核取方塊以切換狀態。", "点击并拖动复选框以切换状态。" } },
            { "Only toggle the instances with the same state as the first selected.", new[] { "最初に選択したオブジェクトと同じ状態のものだけを切り替えます。", "처음 선택한 오브젝트와 상태가 같은 항목만 전환합니다.", "僅切換與第一個選取物件狀態相同的物件。", "仅切换与第一个选中对象状态相同的对象。" } },
            { "If a selection exists, only toggle the selected instances.", new[] { "選択がある場合は、選択中のオブジェクトだけを切り替えます。", "선택된 항목이 있으면 해당 항목만 전환합니다.", "若有選取項目，僅切換選取的物件。", "如果存在选中项，仅切换选中的对象。" } },
            { "The accepted criteria for selecting instances when swiping.", new[] { "ドラッグで切り替える対象の階層範囲です。", "드래그로 전환할 오브젝트의 계층 범위입니다.", "拖曳切換時允許選取的階層範圍。", "拖动切换时允许选取的层级范围。" } },
            { "Will clicking the icon toggle the component", new[] { "アイコンをクリックしてコンポーネントの有効状態を切り替えます。", "아이콘을 클릭하여 컴포넌트의 활성 상태를 전환합니다.", "點擊圖示以切換元件的啟用狀態。", "点击图标以切换组件的启用状态。" } }
        };

        private static readonly Dictionary<string, GUIContent> Contents = new Dictionary<string, GUIContent>();
        private static readonly Dictionary<string, GUIContent[]> EnumContents = new Dictionary<string, GUIContent[]>();
        private static SettingsLanguage language = LoadLanguage();

        internal static event Action LanguageChanged;

        private static SettingsLanguage LoadLanguage()
        {
            int index = Array.IndexOf(LanguageCodes, EditorPrefs.GetString(PreferenceKey, "auto"));
            return index < 0 ? SettingsLanguage.English : (SettingsLanguage)index;
        }

        private static SettingsLanguage AutomaticLanguage()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Japanese:
                    return SettingsLanguage.Japanese;
                case SystemLanguage.Korean:
                    return SettingsLanguage.Korean;
                case SystemLanguage.ChineseTraditional:
                    return SettingsLanguage.TraditionalChinese;
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                    return SettingsLanguage.SimplifiedChinese;
                default:
                    return SettingsLanguage.English;
            }
        }

        internal static SettingsLanguage Language
        {
            get => language;
            set
            {
                if ((int)value < 0 || (int)value >= LanguageCodes.Length || language == value)
                    return;

                language = value;
                EditorPrefs.SetString(PreferenceKey, LanguageCodes[(int)value]);

                // Update existing GUIContent references, including cached tabs and context menus.
                foreach (KeyValuePair<string, GUIContent> entry in Contents)
                {
                    int separator = entry.Key.IndexOf('\0');
                    entry.Value.text = Text(entry.Key.Substring(0, separator));
                    entry.Value.tooltip = Text(entry.Key.Substring(separator + 1));
                }

                LanguageChanged?.Invoke();
            }
        }

        internal static string Text(string source)
        {
            SettingsLanguage resolvedLanguage = language == SettingsLanguage.Auto ? AutomaticLanguage() : language;
            if (string.IsNullOrEmpty(source) || resolvedLanguage == SettingsLanguage.English)
                return source;

            return Translations.TryGetValue(source, out string[] translations) ? translations[(int)resolvedLanguage - (int)SettingsLanguage.Japanese] : source;
        }

        internal static GUIContent Content(string text, string tooltip = null)
        {
            string key = text + "\0" + tooltip;
            if (!Contents.TryGetValue(key, out GUIContent content))
            {
                content = new GUIContent(Text(text), Text(tooltip));
                Contents.Add(key, content);
            }
            return content;
        }

        internal static GUIContent Icon(string icon, string tooltip)
        {
            GUIContent content = Content(string.Empty, tooltip);
            content.image = EditorGUIUtility.IconContent(icon).image;
            return content;
        }

        internal static GUIContent[] EnumLabels(string[] names)
        {
            string key = string.Join("\0", names);
            if (!EnumContents.TryGetValue(key, out GUIContent[] labels))
            {
                labels = new GUIContent[names.Length];
                for (int i = 0; i < names.Length; i++)
                    labels[i] = Content(names[i]);
                EnumContents.Add(key, labels);
            }
            return labels;
        }

        internal static void DrawLanguageSelector()
        {
            int selected = EditorGUILayout.Popup(Content("Language"), (int)Language, LanguageNames);
            Language = (SettingsLanguage)selected;
        }

        internal static void PropertyField(SerializedProperty property)
        {
            GUIContent label = Content(property.displayName, property.tooltip);
            if (property.propertyType == SerializedPropertyType.Enum)
            {
                EditorGUI.BeginChangeCheck();
                int selected = EditorGUILayout.Popup(label, property.enumValueIndex, EnumLabels(property.enumDisplayNames));
                if (EditorGUI.EndChangeCheck())
                    property.enumValueIndex = selected;
            }
            else
            {
                EditorGUILayout.PropertyField(property, label);
            }
        }

        internal static void PropertyField(Rect rect, SerializedProperty property, GUIContent label, bool includeChildren)
        {
            if (property.propertyType == SerializedPropertyType.Enum)
            {
                EditorGUI.BeginProperty(rect, label, property);
                EditorGUI.BeginChangeCheck();
                int selected = EditorGUI.Popup(rect, label, property.enumValueIndex, EnumLabels(property.enumDisplayNames));
                if (EditorGUI.EndChangeCheck())
                    property.enumValueIndex = selected;
                EditorGUI.EndProperty();
            }
            else if (includeChildren && property.propertyType == SerializedPropertyType.Generic)
            {
                rect.height = EditorGUIUtility.singleLineHeight;
                if (!EditorGUI.PropertyField(rect, property, label, false))
                    return;

                SerializedProperty child = property.Copy();
                SerializedProperty end = child.GetEndProperty();
                bool enterChildren = true;
                using (new GUIHelper.IndentChangedScope(1))
                {
                    while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
                    {
                        rect.y += rect.height + EditorGUIUtility.standardVerticalSpacing;
                        rect.height = EditorGUI.GetPropertyHeight(child, true);
                        PropertyField(rect, child, Content(child.displayName, child.tooltip), true);
                        enterChildren = false;
                    }
                }
            }
            else
            {
                EditorGUI.PropertyField(rect, property, label, includeChildren);
            }
        }
    }
}
