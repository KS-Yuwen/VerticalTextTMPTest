using TMPro;
using UnityEngine;

/// <summary>
/// TextMeshProを縦書きにするコンポーネント
/// TMP_TextInfoの頂点情報を操作して縦書きを実現します。
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class VerticalTextTMP : MonoBehaviour
{
    private TMP_Text m_TextComponent = null;

    private void Awake()
    {
        m_TextComponent = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        addTextChangedEvent();
        m_TextComponent.ForceMeshUpdate();
        UpdateVerticalText();

    }

    private void OnDisable()
    {
        removeTextChangeEvent();
    }

    /// <summary>
    /// 縦書き処理の実行
    /// </summary>
    private void UpdateVerticalText()
    {
        // 無限ループ防止策：頂点操作中はイベントを一時的に解除
        removeTextChangeEvent();

        // メッシュ情報を取得
        m_TextComponent.ForceMeshUpdate();
        TMP_TextInfo textInfo = m_TextComponent.textInfo;
        if (textInfo.characterCount == 0)
        {
            addTextChangedEvent();
            return;
        }

        float scale = m_TextComponent.rectTransform.lossyScale.x;
        Vector3[] firstDestVertices = textInfo.meshInfo[0].vertices;
        // 1文字の高さ（縦書き時の幅に相当）
        float baseCharacterHeight = firstDestVertices[1].y - firstDestVertices[0].y;
        // 1文字目の左上X座標を列の基準とする
        float startX = textInfo.meshInfo[0].vertices[1].x;
        // 列間の間隔を設定（調整可能）
        float columnSpacing = baseCharacterHeight * 1.5f;

        int currentColumn = 0;  // 現在の列(0始まり)

        float yOffsetAccumulator = 0f;  // 累積的なY座標オフセット。この値が次の文字の配置基準になる。

        const float EXTRA_VERTICAL_SPACE = 0.0f;   // 文字間の追加スペース (調整可能)

        string originalText = m_TextComponent.text;
        string normalizedText = originalText.Replace("\\n", "\n"); // 改行コードを統一

        // 最後の文字のディセンダーを記憶し、次の文字のアセンダーとの間隔を計算する
        float previousDescender = 0f;

        // 各文字の頂点を回転・移動
        for (int index = 0; index < textInfo.characterCount; index++)
        {
            var charInfo = textInfo.characterInfo[index];

            // TextMeshProの文字数と正規化された文字列の文字数は一致しない可能性があるため
            // ここでは頂点情報に紐づく元のインデックスをチェック
            // ただし、charInfo.index と normalizedText[index] はほぼ対応している前提で進める
            if (index < normalizedText.Length && normalizedText[index] == '\n')
            {   // 改行文字の場合、列を進める
                currentColumn++;
                yOffsetAccumulator = 0f;    // Yオフセットをリセット
                previousDescender = 0f;     // ディセンダーもリセット
                continue;
            }

            if (!charInfo.isVisible)
            {   // 表示されていない文字はスキップ
                continue;
            }

            // 次の文字を配置するためのYオフセットを計算
            // 前の文字のディセンダーと、現在の文字のアセンダーを考慮
            float currentAscender = charInfo.ascender; // 現在の文字の上端


            // 最初の文字の場合、累積オフセットは0のまま（ただし、文字のアセンダー分だけ下にずらす必要がある）
            if (index > 0 && normalizedText[index - 1] != '\n')
            {
                // Yオフセットを更新: 
                // 既存のyOffsetAccumulatorから、前の文字のディセンダーと現在の文字のアセンダーを考慮した間隔分を引く
                float requiredSpace = (currentAscender - previousDescender) * scale;
                yOffsetAccumulator -= requiredSpace + EXTRA_VERTICAL_SPACE;
            }
            // 最初の文字または改行直後の文字の場合、アセンダー分だけ下に配置する
            else if (index == 0 || (index > 0 && normalizedText[index - 1] == '\n'))
            {
                yOffsetAccumulator -= currentAscender * scale;
            }

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;
            Vector3[] destVertices = textInfo.meshInfo[materialIndex].vertices;

            // 文字の中心座標を計算（回転のピボットに使用）
            Vector3 charCenter = (destVertices[vertexIndex] + destVertices[vertexIndex + 2]) / 2;


            // 一時的な頂点配列にコピー
            Vector3[] vertices = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                vertices[i] = destVertices[vertexIndex + i];
            }

            // --- 縦書き位置への移動オフセットを計算 ---

            // 縦書きの列全体を X 座標で右から左へ移動させるためのオフセット
            float columnMoveX = -currentColumn * columnSpacing;

            // Y座標オフセット: 現在の列内の文字数 * 1文字の高さ（下に移動）
            float verticalOffset = yOffsetAccumulator;

            // X座標補正: 
            // 1. 回転後の現在の文字の中心X座標（charCenter.x）を取得
            // 2. 基準X座標（startX）との差分を計算
            // 3. columnMoveX (列移動) を加算
            float horizontalOffset = columnMoveX + (startX - charCenter.x);

            // Y座標の移動と、X座標の列移動を適用
            Vector3 finalOffset = new Vector3(horizontalOffset, verticalOffset, 0);

            // --- 頂点にオフセットを適用 ---
            for (int i = 0; i < 4; i++)
            {
                // 既存の頂点情報（回転適用済み）に最終的なオフセットを加える
                destVertices[vertexIndex + i] = vertices[i] + finalOffset;
            }

            previousDescender = charInfo.descender;
        }

        // メッシュ情報を更新
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            m_TextComponent.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }


        addTextChangedEvent();
    }

    private void OnTextChanged(UnityEngine.Object obj)
    {
        // 変更があったコンポーネントが自身であるか確認
        if (obj == m_TextComponent)
        {
            UpdateVerticalText();
        }
    }

    private void addTextChangedEvent()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
    }

    private void removeTextChangeEvent()
    {
        // RectTransformが削除された場合のクリーンアップ
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
    }
}