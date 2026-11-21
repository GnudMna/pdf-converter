---
paths:
  - "src/PdfConverter.Tests/**/*.cs"
---

# 単体テスト規約

`src/PdfConverter.Tests/` の規約。xUnit v3 + Moq。セクション区切りは `cs-section-comments.md`、文言は `japanese-text-style.md` に従う。

## 配置と命名

- テストクラスは本体と同じフォルダ・名前空間に置き、`<対象クラス名>Tests` とする (例: `Services/BatchExportService.cs` → `Services/BatchExportServiceTests.cs`)
- メソッド名は `<対象メンバー>_<条件>_<期待結果>` の英語 PascalCase (例: `RunAsync_ShowHelp_ReturnsSuccess`, `CanExecute_WhileExecuting_ReturnsFalse`)
- 複数のテストクラスで使う補助処理は `Helpers/` に置く。既存の補助 (`PdfTestAssetFactory`・`StaTestHelper`・`MainViewModelTestFactory` など) があれば再実装しない

## ドキュメントコメント

- テストクラスには `<summary>` で `<see cref="対象"/> の〜を検証する` と書く
- 各テストメソッドには `<summary>` で検証内容を書き、**1 行目は `〜ことを検証する` で終える**

```csharp
/// <summary>
/// ヘルプ表示時は成功終了コードを返すことを検証する
/// </summary>
[Fact]
public async Task RunAsync_ShowHelp_ReturnsSuccess()
```

## 実行環境に依存するテスト

- WPF のオブジェクトを扱うテストは `StaTestHelper.Run` で STA スレッド上で実行する
- 静的な状態 (ログ出力先・一時ディレクトリのルートなど) を書き換えるテストクラスには `[Collection(GlobalStateTestCollection.Name)]` を付け、並列実行から外す。書き換えた状態はテストの終わりに戻す
- 一時ファイルは `PdfTestAssetFactory` などで作り、`finally` などでテストの終わりに削除する
- `CancellationToken` を受け取る API には `TestContext.Current.CancellationToken` を渡す

## してはいけないこと

- `Skip` や `[Fact(Skip = ...)]` でテストを黙らせる。必要だと判断したら、作業を止めてユーザーに確認する
- 原因を確かめないまま、期待値を実装に合わせて書き換える
- Microsoft Word・LibreOffice の起動を前提にする。どちらも未インストールの環境で通るようにし、外部プロセスは `FakeSofficeHelper` やモックで置き換える
