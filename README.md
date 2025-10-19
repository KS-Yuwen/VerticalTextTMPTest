# VerticalTextTMP2

[](https://opensource.org/licenses/MIT)

Unityの **TextMeshPro (TMP)** 用のカスタム縦書きレイアウトコンポーネントです。

このスクリプトは、TMPの標準機能では実現できない日本語の縦書き表示を、メッシュの頂点情報を直接操作することで実現します。簡易的な**禁則処理**（句読点の行頭配置回避）にも対応しています。

## 📚 概要

| 項目 | 詳細 |
| :--- | :--- |
| **目的** | TextMeshPro/TextMeshProUGUIのテキストを縦書きレイアウトに変換する。 |
| **技術** | TMP\_TextInfoの頂点座標（`vertices`）を操作し、文字を縦に、列を右から左へ再配置する。 |
| **対応コンポーネント** | `TMP_Text` を継承する全てのコンポーネント（`TextMeshProUGUI`, `TextMeshPro`）。 |
| **自動更新** | テキスト内容が変更された際、`TMPro_EventManager` を利用して自動的にレイアウトを再計算する。 |

-----

## 🚀 導入と使い方

### 1\. 導入

1.  `VerticalTextTMP2.cs` ファイルをUnityプロジェクト内の任意のフォルダに配置します。

### 2\. 使用方法

1.  シーン内の **TextMeshPro** または **TextMeshProUGUI** コンポーネントがアタッチされているGameObjectを選択します。
2.  インスペクターで **`VerticalTextTMP2`** スクリプトをアタッチします。
3.  ゲームを実行すると、テキストが縦書きレイアウトに変換されます。

**💡 注意事項**:

  * 本コンポーネントは、**実行時 (Play Mode)** にメッシュの頂点情報を操作してレイアウトを適用します。エディタでの即時プレビュー（非実行時）には対応していません。

-----

## ✨ 主な機能

### 1\. 縦書きレイアウトと列送り

  * 文字を上から下へ、垂直方向に配置します。
  * 列は右から左へ送られます。
  * 改行コード（`\n`）を検出すると、自動的に次の列へ送ります。

### 2\. 簡易禁則処理

  * 以下の句読点リストに含まれる文字が**改行（列の先頭）** 直後に来ることを検出します。
      * `、。？！）】｝〕》.!?`
  * 検出した場合、その句読点を**前の列の末尾**に移動（ロールバック）させ、次の文字から新しい列を開始する処理を実行します。

### 3\. スペース処理

  * 半角スペース（`     `）と全角スペース（`　`）に対して、定義された適切な間隔（`HALF_SPACE_HEIGHT`, `FULL_SPACE_HEIGHT`）を空けます。

-----

## ⚙️ 詳細設定とカスタマイズ

レイアウトの微調整が必要な場合は、スクリプト内の以下の定数（`const float`）値を直接変更してください。

| 定数名 | 役割 | デフォルト値 |
| :--- | :--- | :--- |
| `EXTRA_VERTICAL_SPACE` | 文字間の追加スペース (負の値で密着度を高めることも可能) | `0.0f` |
| `FONT_SAFETY_MARGIN` | フォントの重なりを避けるための最小マージン | `0.5f` |
| `HALF_SPACE_HEIGHT` | 半角スペースを検出した際の間隔 | `10.0f` |
| `FULL_SPACE_HEIGHT` | 全角スペースを検出した際の間隔 | `20.0f` |
| `COLUMN_SPACING_RATIO` | 列間の間隔を決定する倍率（文字の高さに対する比率） | `1.0f` |
| `PUNCTUATION_MARKS` | 禁則処理の対象とする句読点リスト（`const string`） | `、。？！）】｝〕》.!?` |

-----

## 🛠️ 開発者向け技術詳細

本コンポーネントは、TMPの強力なメッシュ操作機能を利用してレイアウトを実現しています。

1.  **イベント登録**: `OnEnable`で\*\*`TMPro_EventManager.TEXT_CHANGED_EVENT`\*\*に`OnTextChanged`を登録します。これにより、テキストが変更された時に`UpdateVerticalText()`が自動で呼び出されます。
2.  **頂点情報の取得**: `UpdateVerticalText()`内で\*\*`_textComponent.ForceMeshUpdate()`\*\*を呼び出し、レンダリング前の最新の頂点情報を`TMP_TextInfo`に取得します。
3.  **レイアウト計算と適用**:
      * `ProcessCharacter`内で、改行、禁則処理、スペース処理を考慮しながら、各文字の新しいY座標（垂直位置）とX座標（列位置）を計算します。
      * 計算された`finalOffset`（`horizontalOffset`, `verticalOffset`）を`textInfo.meshInfo[materialIndex].vertices`に直接適用します。
4.  **メッシュの反映**: `FinalizeMeshUpdate()`内で、変更された`vertices`配列を`meshInfo[i].mesh.vertices`に再設定し、\*\*`_textComponent.UpdateGeometry()`\*\*を呼び出すことで、実際のメッシュに反映させます。

-----

## 📜 ライセンス

このプロジェクトはMITライセンスの下で公開されています。詳細については `LICENSE` ファイルを参照してください。