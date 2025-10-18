using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// TextMeshProを縦書きにするコンポーネント
/// TMP_TextInfoの頂点情報を操作して縦書きを実現します。
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class VerticalTextTMP : MonoBehaviour
{
    private TMP_Text m_TextComponent = null;

    /// <summary>文字間の追加スペース(0で密着)</summary>
    private const float EXTRA_VERTICAL_SPACE = 2.0f;
    /// <summary>半角スペースの間隔</summary>
    private const float HALF_SPACE_HEIGHT = 10.0f;
    /// <summary>全角スペースの間隔</summary>
    private const float FULL_SPACE_HEIGHT = 10.0f;

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

        // --- 定数の定義と初期化 ---
        Vector3[] firstDestVertices = textInfo.meshInfo[0].vertices;
        // 1文字の高さ（縦書き時の幅に相当）
        float baseCharacterHeight = firstDestVertices[1].y - firstDestVertices[0].y;
        // 1文字目の左上X座標を列の基準とする
        float startX = textInfo.meshInfo[0].vertices[1].x;
        // 列間の間隔を設定（調整可能）
        float columnSpacing = baseCharacterHeight * 1.5f;

        int currentColumn = 0;  // 現在の列(0始まり)
        float yOffsetAccumulator = 0f;  // 累積的なY座標オフセット。この値が次の文字の配置基準になる。

        string originalText = m_TextComponent.text;
        string normalizedText = originalText.Replace("\\n", "\n"); // 改行コードを統一

        // 最後の文字のディセンダーを記憶し、次の文字のアセンダーとの間隔を計算する
        float previousDescender = 0f;

        // 全ての列の開始高さを統一するための基準値（最初の可視文字のアセンダー）
        float referenceAscenderScaled = 0f;
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            var cInfo = textInfo.characterInfo[i];
            if (cInfo.isVisible)
            {
                referenceAscenderScaled = cInfo.ascender * cInfo.scale;
                break;
            }
        }

        // --- メインループ ---
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

            float charScale = charInfo.scale;

            if (!charInfo.isVisible)
            {
                char c = normalizedText[index];
                float spaceMove = 0f;

                if (c == ' ')
                {   // 半角スペース
                    spaceMove = HALF_SPACE_HEIGHT * charScale;
                }
                else if (c == '　')
                {   // 全角スペース
                    spaceMove = FULL_SPACE_HEIGHT * charScale;
                }

                if (spaceMove > 0f)
                {   // スペースの高さ分だけオフセットを進める
                    yOffsetAccumulator -= spaceMove;
                }
                // スペース直後の文字は「改行後」と同じ基準高さから始まるようにリセットする
                previousDescender = 0f;
                continue;
            }

            // 可視文字の処理
            // 次の文字を配置するためのYオフセットを計算
            // 前の文字のディセンダーと、現在の文字のアセンダーを考慮
            float currentAscender = charInfo.ascender; // 現在の文字の上端

            // --- Yオフセットの計算と更新 ---
            bool isColumnStart = (index == 0) || (index > 0 && normalizedText[index - 1] == '\n');
            bool isAfterReset = previousDescender == 0f;


            // 最初の文字または改行直後の文字の場合
            if (isColumnStart)
            {   // 固定の基準高さを使用: 1行目の文字と同じ高さから開始
                yOffsetAccumulator -= referenceAscenderScaled;
            }
            // スペース直後
            else if (isAfterReset)
            {
                yOffsetAccumulator -= (currentAscender * charScale) + EXTRA_VERTICAL_SPACE;
            }
            // 連続する文字の場合
            else
            {   // 密着調整: 前の文字の下端と現在の文字の上端の差分を計算
                // requiredMove は次の文字のアセンダー(scaled)から前の文字のディセンダー(scaled)までの距離
                float requiredMove = (currentAscender * charScale) - previousDescender;
                yOffsetAccumulator -= requiredMove + EXTRA_VERTICAL_SPACE;
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

            previousDescender = charInfo.descender * charScale;
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