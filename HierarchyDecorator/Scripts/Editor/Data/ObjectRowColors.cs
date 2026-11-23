using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HierarchyDecorator.Editor
{
    [Serializable]
    internal sealed class ObjectRowColors : ISerializationCallbackReceiver
    {
        private const string SessionKey = "com.vrclearn.hierarchydecorator.row-color-session";
        private static readonly Dictionary<GameObject, ObjectKeys> KeyCache = new Dictionary<GameObject, ObjectKeys>();
        private static string sessionPrefix;

        [Serializable]
        private struct Entry
        {
            public string key;
            public Color color;
        }

        private struct ObjectKeys
        {
            public string persistent;
            public string temporary;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [NonSerialized] private Dictionary<string, Color> colors;

        [InitializeOnLoadMethod]
        private static void TrackObjectIdentity()
        {
            EditorApplication.hierarchyChanged += KeyCache.Clear;
            EditorSceneManager.sceneSaved += SaveSceneColors;
            EditorSceneManager.sceneClosed += _ => KeyCache.Clear();
#if UNITY_2021_2_OR_NEWER
            PrefabStage.prefabSaved += root => SaveSceneColors(root.scene);
#endif
        }

        internal bool TryGetColor(GameObject target, out Color color)
        {
            color = default;
            if (entries.Count == 0)
            {
                return false;
            }

            if (colors == null)
            {
                colors = new Dictionary<string, Color>(entries.Count);
                foreach (Entry entry in entries)
                {
                    colors[entry.key] = entry.color;
                }
            }

            if (!KeyCache.TryGetValue(target, out ObjectKeys keys))
            {
                keys = GetKeys(target);
                KeyCache.Add(target, keys);
            }

            return (keys.persistent != null && colors.TryGetValue(keys.persistent, out color)) ||
                colors.TryGetValue(keys.temporary, out color);
        }

        internal void SetColor(GameObject target, Color? color)
        {
            ObjectKeys keys = GetKeys(target);
            KeyCache[target] = keys;
            entries.RemoveAll(entry => entry.key == keys.persistent || entry.key == keys.temporary);
            if (color.HasValue)
            {
                entries.Add(new Entry { key = keys.persistent ?? keys.temporary, color = color.Value });
            }
            colors = null;
        }

        private static string SessionPrefix
        {
            get
            {
                if (sessionPrefix == null)
                {
                    string session = SessionState.GetString(SessionKey, string.Empty);
                    if (string.IsNullOrEmpty(session))
                    {
                        session = Guid.NewGuid().ToString("N");
                        SessionState.SetString(SessionKey, session);
                    }
                    sessionPrefix = "session/" + session + "/";
                }
                return sessionPrefix;
            }
        }

        private static ObjectKeys GetKeys(GameObject target)
        {
            return new ObjectKeys
            {
                persistent = PersistentKey(target),
                temporary = SessionPrefix + target.GetInstanceID()
            };
        }

        private static string PersistentKey(GameObject target)
        {
#if UNITY_2019_2_OR_NEWER
            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(target);
            if (id.targetObjectId != 0 && id.assetGUID != default(GUID))
            {
                return id.ToString();
            }

#if UNITY_2021_2_OR_NEWER
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(target);
            if (id.targetObjectId != 0 && stage != null && stage.IsPartOfPrefabContents(target))
            {
                string guid = AssetDatabase.AssetPathToGUID(stage.assetPath);
                if (!string.IsNullOrEmpty(guid))
                {
                    return $"prefab/{guid}/{id.targetObjectId}/{id.targetPrefabId}";
                }
            }
#endif
#endif
            return null;
        }

        private static void SaveSceneColors(Scene scene)
        {
            KeyCache.Clear();
            Settings settings = HierarchyDecorator.Settings;
            ObjectRowColors data = settings.RowColors;
            if (data.entries.Count == 0)
            {
                return;
            }

            string prefix = SessionPrefix;
            bool changed = false;
            for (int index = 0; index < data.entries.Count; index++)
            {
                Entry entry = data.entries[index];
                if (!entry.key.StartsWith(prefix, StringComparison.Ordinal) ||
                    !int.TryParse(entry.key.Substring(prefix.Length), out int instanceId))
                {
                    continue;
                }

                var target = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                if (target == null || target.scene != scene)
                {
                    continue;
                }

                string key = PersistentKey(target);
                if (key != null)
                {
                    entry.key = key;
                    data.entries[index] = entry;
                    changed = true;
                }
            }

            if (changed)
            {
                data.colors = null;
                EditorUtility.SetDirty(settings);
            }
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            colors = null;
        }
    }
}
