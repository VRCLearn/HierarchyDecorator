using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_6000_3_OR_NEWER
using HierarchyObjectId = UnityEngine.EntityId;
#else
using HierarchyObjectId = System.Int32;
#endif

namespace HierarchyDecorator.Editor
{
    public class HierarchyItem
    {
        // --- Fields

        /// <summary>
        /// The ID for the hierarchy item.
        /// </summary>
        public readonly HierarchyObjectId ID;

        /// <summary>
        /// The GameObject this item represents.
        /// </summary>
        public readonly GameObject GameObject;
        
        /// <summary>
        /// The <see cref="GameObject"/> transform for this item. Cached for performance.
        /// </summary>
        public Transform Transform;

        // --- Properties

        /// <summary>
        /// Is this showing child items in hierarchy?
        /// </summary>
        public bool Foldout { get; private set; }

        /// <summary>
        /// Collection of components for this hierarchy item. 
        /// </summary>
        public ComponentList Components
        {
            get
            {
                ComponentList current = GetComponentsForDrawing();
                current.RefreshActiveStates();
                return current;
            }
        }

        private ComponentList components;
        private PrefabInfo prefabInfo;
        private bool hasPrefabInfo;
        private string rowName;
        private HierarchyStyle rowStyle;
        private bool hasResolvedRowStyle;
        
        /// <summary>
        /// Does this item have any children?
        /// </summary>
        public bool HasChildren => GameObject.transform.childCount > 0;

        /// <summary>
        /// Does this item have a parent or is it a root?
        /// </summary>
        public bool HasParent => Transform.parent != null;

        /// <summary>
        /// The type of prefab asset.
        /// </summary>
        public PrefabAssetType PrefabTypeInfo => PrefabInfo == PrefabInfo.None
            ? PrefabAssetType.NotAPrefab : PrefabUtility.GetPrefabAssetType(GameObject);

        /// <summary>
        /// The prefab state.
        /// </summary>
        public PrefabInfo PrefabInfo
        {
            get
            {
                if (!hasPrefabInfo)
                {
                    prefabInfo = GetPrefabInfo();
                    hasPrefabInfo = true;
                }
                return prefabInfo;
            }
        }

        /// <summary>
        /// Is this item part of a prefab?
        /// </summary>
        public bool IsPrefab => PrefabInfo != PrefabInfo.None; 

        /// <summary>
        /// The display string for the item.
        /// </summary>
        public string DisplayName => GameObject.name;

        internal string RowName => rowName ?? (rowName = DisplayName);

        internal HierarchyStyle GetRowStyle(HierarchyStyleData styles)
        {
            if (!hasResolvedRowStyle)
            {
                styles.TryGetStyleFromPrefix(RowName, out rowStyle);
                hasResolvedRowStyle = true;
            }
            return rowStyle;
        }

        /// <summary>
        /// Create a new HierachyItem instance.
        /// </summary>
        /// <param name="id">The Hierarchy object's ID.</param>
        /// <param name="instance">The gameobject this iunstance represents.</param>
        public HierarchyItem(HierarchyObjectId id, GameObject instance)
        {
            ID = id;

            GameObject = instance;
            Transform = instance.transform;
        }

        // --- Methods

        private PrefabInfo GetPrefabInfo()
        {
            if (!PrefabUtility.IsPartOfAnyPrefab(GameObject))
            {
                return PrefabInfo.None;
            }
             
            return PrefabUtility.GetNearestPrefabInstanceRoot(GameObject) == GameObject
                ? PrefabInfo.Root
                : PrefabInfo.Part;
        }

        /// <summary>
        /// Called before GUI.
        /// </summary>
        public void OnGUIBegin()
        {
            hasPrefabInfo = false;
            rowName = null;
            hasResolvedRowStyle = false;
        }

        internal ComponentList GetComponentsForDrawing()
        {
            if (components == null)
                components = new ComponentList(GameObject);
            else
                components.Refresh(GameObject);
            return components;
        }

        /// <summary>
        /// Called after GUI.
        /// </summary>
        /// <param name="nextItem">The next item to be called.</param>
        public void OnGUIEnd(HierarchyItem nextItem)
        {
            Foldout = nextItem.Transform.parent == Transform;
        }

        /// <summary>
        /// Calculate the depth of this item.
        /// </summary>
        /// <returns></returns>
        public int CalculateDepth()
        {
            Transform parent = Transform.parent;
            int index = 0;
            while (parent != null)
            {
                parent = parent.parent;
                index++;
            }

            return index;
        }

        /// <summary>
        /// Query to check if this instance is the last item between its siblings.
        /// </summary>
        /// <returns>Returns true if the last sibling, otherwise returns false.</returns>
        public bool IsLastSibling()
        {
            int index = Transform.GetSiblingIndex();
            if (!HasParent)
            {
                return GameObject.scene.rootCount - 1 == index;
            }

            return Transform.parent.childCount - 1 == index;
        }

        /// <summary>
        /// Is this item valid?
        /// </summary>
        /// <returns>Returns true if valid, otherwise returns false.</returns>
        public bool IsValid()
        {
            return GameObject != null;
        }
    }
}
