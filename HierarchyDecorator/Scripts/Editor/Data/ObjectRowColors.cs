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
            public bool colorRegion;
        }

        private struct ObjectKeys
        {
            public string persistent;
            public string temporary;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [NonSerialized] private Dictionary<string, Entry> colors;
        [NonSerialized] private bool hasRegions;

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

        internal bool HasRegions
        {
            get
            {
                EnsureColorIndex();
                return hasRegions;
            }
        }

        internal bool TryGetColor(GameObject target, out Color color, out bool colorRegion)
        {
            color = default;
            colorRegion = false;
            if (entries.Count == 0)
            {
                return false;
            }

            EnsureColorIndex();
            if (!KeyCache.TryGetValue(target, out ObjectKeys keys))
            {
                keys = GetKeys(target);
                KeyCache.Add(target, keys);
            }

            if ((keys.persistent != null && colors.TryGetValue(keys.persistent, out Entry entry)) ||
                colors.TryGetValue(keys.temporary, out entry))
            {
                color = entry.color;
                colorRegion = entry.colorRegion;
                return true;
            }

            return false;
        }

        private void EnsureColorIndex()
        {
            if (colors != null)
            {
                return;
            }

            colors = new Dictionary<string, Entry>(entries.Count);
            hasRegions = false;
            foreach (Entry entry in entries)
            {
                colors[entry.key] = entry;
                hasRegions |= entry.colorRegion;
            }
        }

        internal void SetColor(GameObject target, Color? color, bool colorRegion)
        {
            ObjectKeys keys = GetKeys(target);
            KeyCache[target] = keys;
            entries.RemoveAll(entry => entry.key == keys.persistent || entry.key == keys.temporary);
            if (color.HasValue)
            {
                entries.Add(new Entry
                {
                    key = keys.persistent ?? keys.temporary,
                    color = color.Value,
                    colorRegion = colorRegion
                });
            }
            colors = null;
        }

        internal void RemoveColor(Color color)
        {
            if (entries.RemoveAll(entry => entry.color.Equals(color)) > 0)
            {
                colors = null;
            }
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
#if UNITY_6000_5_OR_NEWER
                temporary = SessionPrefix + EntityId.ToULong(target.GetEntityId())
#else
                temporary = SessionPrefix + target.GetInstanceID()
#endif
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
                if (!entry.key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string objectId = entry.key.Substring(prefix.Length);
#if UNITY_6000_5_OR_NEWER
                if (!ulong.TryParse(objectId, out ulong rawId))
                {
                    continue;
                }
                var target = EditorUtility.EntityIdToObject(EntityId.FromULong(rawId)) as GameObject;
#else
                if (!int.TryParse(objectId, out int instanceId))
                {
                    continue;
                }
                var target = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
#endif
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
