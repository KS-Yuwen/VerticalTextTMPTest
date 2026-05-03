#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
public static class TextMeshProAtlasClear
{
    [InitializeOnLoadMethod]
    private static void Test()
    {
        EditorApplication.playModeStateChanged -= PlayModeStateChanged;
        EditorApplication.playModeStateChanged += PlayModeStateChanged;
    }
    private static void PlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode)
            return;
        
        var assets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        // Dynamicなアセットのアトラスをクリア
        foreach (var asset in assets)
        {
            if (asset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
                continue;
            asset.ClearFontAssetData(setAtlasSizeToZero: true);
        }
    }
}
#endif
