using TMPro;
using UnityEngine;

/// <summary>
/// TextMeshPro 用縦書きレイアウトコンポーネント
/// </summary>
public class VerticalTextTMP2 : MonoBehaviour
{
    /// <summary>テキストコンポーネント</summary>
    private TMP_Text _textComponent = null;

    /// <summary>文字間の追加スペース(0で密着)</summary>
    private const float EXTRA_VERTICAL_SPACE = 0.0f;
    /// <summary>フォントのレンダリング上の重なりを避けるための最小マージン</summary>
    private const float FONT_SAFETY_MARGIN = 0.5f;
    /// <summary>半角スペースの間隔</summary>
    private const float HALF_SPACE_HEIGHT = 10.0f;
    /// <summary>全角スペースの間隔</summary>
    private const float FULL_SPACE_HEIGHT = 20.0f;
    /// <summary>列間の倍率（文字高さに対する）</summary>
    private const float COLUMN_SPACING_RATIO = 1.0f;

    /// <summary>句読点リスト</summary>
    private const string PUNCTUATION_MARKS = "、。？！）】｝〕》.!?";

    /// <summary>
    /// Awake
    /// </summary>
    private void Awake()
    {
        _textComponent = GetComponent<TMP_Text>();
    }

    /// <summary>
    /// OnEnable
    /// </summary>
    private void OnEnable()
    {
        AddTextChangedEvent();
        _textComponent.ForceMeshUpdate();
        UpdateVerticalText();
    }

    /// <summary>
    /// OnDisable
    /// </summary>
    private void OnDisable()
    {
        RemoveTextChangedEvent();
    }

    /// <summary>
    /// 縦書き処理の実行
    /// メインとなる処理の流れを記述し、詳細な処理は補助関数に切り出します。
    /// </summary>
    private void UpdateVerticalText()
    {
        RemoveTextChangedEvent();

        _textComponent.ForceMeshUpdate();
        TMP_TextInfo textInfo = _textComponent.textInfo;
        if (textInfo.characterCount == 0)
        {
            AddTextChangedEvent();
            return;
        }

        // --- 初期化 ---
        InitializeTextLayout(
            textInfo,
            out float startX,
            out float columnSpacing,
            out float referenceAscenderScaled);

        // --- 状態変数 ---
        int currentColumn = 0;
        float currentYPosition = 0f;
        float previousBottomY = 0f;
        bool wasRolledBack = false;
        string normalizedText = _textComponent.text.Replace("\\n", "\n");

        // --- メインループ ---
        for (int index = 0; index < textInfo.characterCount; index++)
        {
            var charInfo = textInfo.characterInfo[index];

            // 文字処理と状態更新
            ProcessCharacter(
                index,
                charInfo,
                textInfo,
                normalizedText,
                startX,
                columnSpacing,
                referenceAscenderScaled,
                ref currentColumn,
                ref currentYPosition,
                ref previousBottomY,
                ref wasRolledBack);
        }

        // --- 頂点更新 ---
        FinalizeMeshUpdate(textInfo);

        AddTextChangedEvent();
    }

    /// <summary>
    /// レイアウトの基本定数と基準値を計算し、初期化します。
    /// </summary>
    private void InitializeTextLayout(
        TMP_TextInfo textInfo,
        out float startX,
        out float columnSpacing,
        out float referenceAscenderScaled)
    {
        Vector3[] firstDestVertices = textInfo.meshInfo[0].vertices;
        float baseCharacterHeight = firstDestVertices[1].y - firstDestVertices[0].y;

        // 1文字目の左上X座標を列の基準とする
        startX = firstDestVertices[1].x;
        // 列間の間隔を設定
        columnSpacing = baseCharacterHeight * COLUMN_SPACING_RATIO;

        // 全ての列の開始高さを統一するための基準値
        referenceAscenderScaled = 0f;
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            var cInfo = textInfo.characterInfo[i];
            if (cInfo.isVisible)
            {
                referenceAscenderScaled = cInfo.ascender * cInfo.scale;
                break;
            }
        }
    }
    // --------------------------------------------------------------------------------------
    /// <summary>
    /// 個々の文字の配置処理、改行/スペース処理、禁則処理、状態更新を行います。
    /// </summary>
    private void ProcessCharacter(
        int index,
        TMP_CharacterInfo charInfo,
        TMP_TextInfo textInfo,
        string normalizedText,
        float startX,
        float columnSpacing,
        float referenceAscenderScaled,
        ref int currentColumn,
        ref float currentYPosition,
        ref float previousBottomY,
        ref bool wasRolledBack)
    {
        float charScale = charInfo.scale;

        bool isPunctuation = PUNCTUATION_MARKS.Contains(normalizedText[index]);
        bool isAfterNewline = index > 0 && normalizedText[index - 1] == '\n';

        // 1. 改行コードの処理
        if (index < normalizedText.Length && normalizedText[index] == '\n')
        {
            currentColumn++;
            currentYPosition = 0f;
            previousBottomY = 0f;
            return;
        }

        // 2. 禁則処理（改行直後の句読点を前の行末へ）
        if (isPunctuation && isAfterNewline)
        {
            currentColumn--;
            if (index > 1)
            {
                var prevCharInfo = textInfo.characterInfo[index - 2];
                int prevVertexIndex = prevCharInfo.vertexIndex;
                int prevMaterialIndex = prevCharInfo.materialReferenceIndex;
                Vector3[] prevDestVertices = textInfo.meshInfo[prevMaterialIndex].vertices;

                // 前の文字の最終的な下端Y座標を取得
                float lastCharBottomY = prevDestVertices[prevVertexIndex].y;
                currentYPosition = lastCharBottomY;
                wasRolledBack = true;
            }
            else
            {
                currentColumn++; // 変更を取り消す
            }
        }

        // 3. 非可視文字（スペース）の処理
        if (!charInfo.isVisible)
        {
            char c = normalizedText[index];
            float spaceMove = (c == ' ') ? HALF_SPACE_HEIGHT * charScale :
                             ((c == '　') ? FULL_SPACE_HEIGHT * charScale : 0f);

            if (spaceMove > 0f)
            {
                currentYPosition -= spaceMove;
            }
            previousBottomY = 0f; // スペース後の文字はベースラインリセット
            return;
        }

        // 4. 可視文字の配置計算と頂点適用
        ApplyCharacterLayout(
            index,
            charInfo,
            textInfo,
            normalizedText,
            startX,
            columnSpacing,
            referenceAscenderScaled,
            isPunctuation,
            ref currentColumn,
            ref currentYPosition,
            ref previousBottomY);

        // 5. ロールバック後の状態リセット
        if (wasRolledBack)
        {
            currentColumn++;
            currentYPosition = 0f;
            previousBottomY = 0f;
            wasRolledBack = false;
        }
    }

    /// <summary>
    /// Y座標の計算、オフセットの適用、状態変数の更新を行います。
    /// </summary>
    private void ApplyCharacterLayout(
        int index,
        TMP_CharacterInfo charInfo,
        TMP_TextInfo textInfo,
        string normalizedText,
        float startX,
        float columnSpacing,
        float referenceAscenderScaled,
        bool isPunctuation,
        ref int currentColumn,
        ref float currentYPosition,
        ref float previousBottomY)
    {
        int materialIndex = charInfo.materialReferenceIndex;
        int vertexIndex = charInfo.vertexIndex;
        Vector3[] destVertices = textInfo.meshInfo[materialIndex].vertices;

        Vector3[] vertices = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            vertices[i] = destVertices[vertexIndex + i];
        }

        // --- Y座標の計算 ---
        float targetTopY;

        // 句読点によるロールバックが発生した場合、Column Startと見なさない
        bool isColumnStart = (index == 0) || (index > 0 && normalizedText[index - 1] == '\n') && !isPunctuation;

        if (isColumnStart)
        {
            targetTopY = currentYPosition - referenceAscenderScaled;
        }
        else
        {
            if (previousBottomY != 0f)
            {
                targetTopY = previousBottomY - EXTRA_VERTICAL_SPACE - FONT_SAFETY_MARGIN;
            }
            else
            {
                targetTopY = currentYPosition - EXTRA_VERTICAL_SPACE - FONT_SAFETY_MARGIN;
            }
        }

        // 頂点移動に必要な Y オフセットを計算
        float verticalOffset = targetTopY - vertices[1].y;

        // --- X座標の計算と頂点適用 ---
        Vector3 charCenter = (destVertices[vertexIndex] + destVertices[vertexIndex + 2]) / 2;
        float columnMoveX = -currentColumn * columnSpacing;
        float horizontalOffset = columnMoveX + (startX - charCenter.x);
        Vector3 finalOffset = new Vector3(horizontalOffset, verticalOffset, 0);

        // 頂点にオフセットを適用
        for (int i = 0; i < 4; i++)
        {
            destVertices[vertexIndex + i] = vertices[i] + finalOffset;
        }

        // --- 状態変数の更新 ---
        // 次の反復のために、現在の文字の最終的な下端Y座標を記憶する
        previousBottomY = destVertices[vertexIndex].y;
        currentYPosition = previousBottomY;
    }
    // --------------------------------------------------------------------------------------
    /// <summary>
    /// 変更された頂点情報に基づいてメッシュを更新します。
    /// </summary>
    private void FinalizeMeshUpdate(TMP_TextInfo textInfo)
    {
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            _textComponent.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }

    // --- イベントハンドラ ---

    /// <summary>
    /// テキスト変更イベントハンドラ
    /// </summary>
    /// <param name="obj"></param>
    private void OnTextChanged(Object obj)
    {
        if (obj == _textComponent)
        {
            UpdateVerticalText();
        }
    }

    /// <summary>
    /// テキスト変更イベントの登録
    /// </summary>
    private void AddTextChangedEvent()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
    }

    /// <summary>
    /// テキスト変更イベントの解除
    /// </summary>
    private void RemoveTextChangedEvent()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
    }
}