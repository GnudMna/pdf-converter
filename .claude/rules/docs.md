---
paths:
  - "docs/**/*.md"
  - "README.md"
  - "CHANGELOG.md"
  - "CLAUDE.md"
  - ".claude/rules/*.md"
  - ".claude/skills/**/*.md"
---

# ドキュメント規約

同じ説明は 1 か所にだけ書き、ほかの文書からはリンクで参照する。括弧・英数字との境界などの表記は `japanese-text-style.md` に従う。

## 文体

- 常体 (だ・である調)。です・ます調にしない
- 括弧・コロンは半角を使う
- 表の区切りは `| :--- | :--- |` の形式にそろえる

## 文書ごとの規約

- `docs/user/` (利用者向け・mdBook 原稿): インストーラーに同梱してオフラインで読むため、`docs/user/` の外 (`src/`・`installer/` など) へ相対リンクしない。ページを足したら `SUMMARY.md` にも追加する
- `docs/user/changelog.md`: `CHANGELOG.md` を取り込むだけのため編集しない
- `CHANGELOG.md`: 書き方は冒頭の規約に従う。`installer/build-user-docs.ps1` が最初の `## [` 見出しより前を除いて利用者向けページに変換するため、見出し構造 (`## [バージョン]` / `### 節`) を崩さない。リンク先は外部 URL・`docs/user/*.md`・`CHANGELOG.md` 内のアンカーのいずれかにする (それ以外のリンクは変換時に外れる)
- `CLAUDE.md` / `.claude/`: Claude Code が毎回読み込むため、作業時に間違えやすい点だけを短く書く。手順の詳細はスキル (`.claude/skills/`) に置く
