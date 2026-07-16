using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace JRPG.Menu.Editor
{
    /// One-shot migration: every list-menu prefab's ScrollView Viewport used a stencil <see cref="Mask"/>
    /// whose alpha-0.001 mask graphic fails to write the stencil buffer in the nested-canvas
    /// configuration, silently discarding all masked rows. <see cref="RectMask2D"/> clips by rect (no
    /// stencil, alpha-independent) and is the correct choice for ScrollRect content. This sweep also
    /// fixes InventoryList.prefab — the template the combat/post-battle UI builders clone — so future
    /// rebuilds inherit the fix.
    public static class MenuMaskFixer
    {
        [MenuItem("JRPG/Setup/Fix Menu Viewport Masks")]
        public static void Fix()
        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/UI" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool dirty = false;
                    foreach (var mask in root.GetComponentsInChildren<Mask>(true))
                    {
                        var go = mask.gameObject;
                        Object.DestroyImmediate(mask, true);
                        if (go.GetComponent<RectMask2D>() == null) go.AddComponent<RectMask2D>();
                        dirty = true;
                    }
                    if (dirty)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        changed++;
                        Debug.Log($"[MenuMaskFixer] {path}: Mask → RectMask2D");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MenuMaskFixer] Updated {changed} prefab(s).");
        }
    }
}
