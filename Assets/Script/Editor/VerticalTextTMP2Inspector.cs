using System;
using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// TMP エディタクラスをリフレクションで取得して委譲する共通基底クラス
/// </summary>
public abstract class VerticalTextTMP2Inspector : Editor
{
    private Editor _tmpEditor;
    protected abstract string TmpEditorClassName { get; }

    private void OnEnable()
    {
        var type = FindTMPEditorType(TmpEditorClassName);
        if (type != null)
            _tmpEditor = CreateEditor(target, type);
    }

    private void OnDisable()
    {
        if (_tmpEditor != null) DestroyImmediate(_tmpEditor);
    }

    public override void OnInspectorGUI()
    {
        if (_tmpEditor != null)
            _tmpEditor.OnInspectorGUI();
        else
            DrawDefaultInspector();

        DrawVerticalToggle();
    }

    private void DrawVerticalToggle()
    {
        var tmp = (TMP_Text)target;
        bool has = tmp.GetComponent<VerticalTextTMP2>() != null;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("縦書き設定", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        bool val = EditorGUILayout.Toggle("縦書きレイアウト", has);
        if (EditorGUI.EndChangeCheck() && val != has)
        {
            if (val) Undo.AddComponent<VerticalTextTMP2>(tmp.gameObject);
            else Undo.DestroyObjectImmediate(tmp.GetComponent<VerticalTextTMP2>());
        }
    }

    private static Type FindTMPEditorType(string className)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = asm.GetType($"TMPro.EditorUtilities.{className}");
            if (type != null) return type;
        }
        return null;
    }
}

[CustomEditor(typeof(TextMeshProUGUI), true)]
[CanEditMultipleObjects]
public class VerticalTextUGUIInspector : VerticalTextTMP2Inspector
{
    protected override string TmpEditorClassName => "TMP_UiEditorPanel";
}

[CustomEditor(typeof(TextMeshPro), true)]
[CanEditMultipleObjects]
public class VerticalTextWorldInspector : VerticalTextTMP2Inspector
{
    protected override string TmpEditorClassName => "TMP_EditorPanel";
}
