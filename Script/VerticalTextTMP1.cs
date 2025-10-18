using TMPro;
using UnityEngine;

public class VerticalTextTMP1 : MonoBehaviour
{
    private TMP_Text m_TextComponent = null;

    /// <summary>文字間の追加スペース(0で密着)</summary>
    private const float EXTRA_VERTICAL_SPACE = 0.0f;
    /// <summary>フォントのレンダリング上の重なりを避けるための最小マージン</summary>
    // 密着を強制するため、ここでは 0.0f に設定（必要に応じて負の値で微調整してください）
    private const float FONT_SAFETY_MARGIN = 0.5f;
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
        removeTextChangedEvent();
    }

    /// <summary>
    /// 縦書き処理の実行
    /// </summary>
    private void UpdateVerticalText()
    {
        // 無限ループ防止策：頂点操作中はイベントを一時的に解除
        removeTextChangedEvent();

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
        // 【変更】累積的なY座標オフセットではなく、列の開始Y座標のベースラインとして使用
        float currentYPosition = 0f;

        string originalText = m_TextComponent.text;
        string normalizedText = originalText.Replace("\\n", "\n");

        // 【新規】前の可視文字の最終的な下端Y座標（ワールド空間での最小Y座標）を記憶
        float previousBottomY = 0f;

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
        for (int index = 0; index < textInfo.characterCount; index++)
        {
            var charInfo = textInfo.characterInfo[index];
            float charScale = charInfo.scale;

            if (index < normalizedText.Length && normalizedText[index] == '\n')
            {   // 改行文字の場合、列を進める
                currentColumn++;
                currentYPosition = 0f;    // Y座標ベースをリセット
                previousBottomY = 0f;     // 前の下端座標もリセット
                continue;
            }


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
                {   // スペースの高さ分だけ YPosition を下に移動（マイナス方向）
                    currentYPosition -= spaceMove;
                }

                // 【重要】スペースは頂点操作をしないが、次の文字の配置基準（previousBottomY）をリセットする
                previousBottomY = 0f;
                continue;
            }

            // --- 可視文字の処理 ---

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;
            Vector3[] destVertices = textInfo.meshInfo[materialIndex].vertices;

            // 一時的な頂点配列にコピー
            Vector3[] vertices = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                vertices[i] = destVertices[vertexIndex + i];
            }

            // 文字の上端と下端を、初期位置 (0, 0) からの相対位置で計算
            float charTopY = charInfo.ascender * charScale;
            float charBottomY = charInfo.descender * charScale;
            float charHeight = charTopY - charBottomY; // 実際のレンダリング高さ

            // --- Y座標の計算と更新 ---
            float targetTopY; // 現在の文字の上端が目指すべき最終的なY座標

            bool isColumnStart = (index == 0) || (index > 0 && normalizedText[index - 1] == '\n');

            if (isColumnStart)
            {
                // 【ケース1】列の開始: 基準高さを使用し、YPositionを更新
                // 1行目の文字の上端Y座標が targetTopY となるように currentYPosition を設定
                targetTopY = currentYPosition - referenceAscenderScaled;
            }
            // 連続する文字、またはスペース直後の文字の場合
            else
            {
                // 【ケース2】前の文字の下端 + マージンを、現在の文字の上端が目指す
                // previousBottomY が 0 の場合（スペース後）は currentYPosition が使われる
                if (previousBottomY != 0f)
                {
                    // 頂点座標の最小値 (下端) を使用
                    targetTopY = previousBottomY - EXTRA_VERTICAL_SPACE - FONT_SAFETY_MARGIN;
                }
                else
                {
                    // スペース後、または非連続文字の場合
                    targetTopY = currentYPosition - EXTRA_VERTICAL_SPACE - FONT_SAFETY_MARGIN;
                }
            }

            // ----------------------------------------------------
            // 頂点移動に必要な Y オフセットを計算
            // Yオフセット = 目標の上端Y座標 - 現在の頂点の上端Y座標
            // 現在の頂点の上端Y座標 = vertices[1].y (オフセット適用前のY座標)
            float verticalOffset = targetTopY - vertices[1].y;

            // --- 頂点移動の計算と適用 ---

            Vector3 charCenter = (destVertices[vertexIndex] + destVertices[vertexIndex + 2]) / 2;
            float columnMoveX = -currentColumn * columnSpacing;
            float horizontalOffset = columnMoveX + (startX - charCenter.x);

            Vector3 finalOffset = new Vector3(horizontalOffset, verticalOffset, 0);

            // 頂点にオフセットを適用
            for (int i = 0; i < 4; i++)
            {
                destVertices[vertexIndex + i] = vertices[i] + finalOffset;
            }

            // 【重要】次の反復のために、現在の文字の最終的な下端Y座標を記憶する
            // 頂点[0]または[3]が下端。ここでは[0].yを使用。
            previousBottomY = destVertices[vertexIndex].y;

            // 【重要】次の文字が列の先頭ではない場合に備え、currentYPositionを更新
            // currentYPosition は次の文字のベースライン（または上端）の基準位置になる
            currentYPosition = previousBottomY;
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
        if (obj == m_TextComponent)
        {
            UpdateVerticalText();
        }
    }

    private void addTextChangedEvent()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
    }

    private void removeTextChangedEvent()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
    }
}
