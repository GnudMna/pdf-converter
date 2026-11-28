# Claude Code の PostToolUse フック: Edit / Write で変更した src\ 配下の C# ファイルの書式を直す
#
# 改行コード (CRLF)・文字コード・インデント・末尾の空白など .editorconfig の書式を dotnet format whitespace で整える。
# build-and-test.ps1 の書式チェックで落ちるのを、編集の時点で防ぐためのもの。
# 標準入力にフックの入力 (JSON) を受け取る。書式の修正に失敗しても編集自体は止めない (常に終了コード 0)。

$ErrorActionPreference = 'Stop'

try {
    $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $filePath = $payload.tool_input.file_path
    if (-not $filePath -or [IO.Path]::GetExtension($filePath) -ne '.cs' -or -not (Test-Path -LiteralPath $filePath)) {
        exit 0
    }

    $repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $srcDir = Join-Path $repoRoot 'src'
    $fullPath = [IO.Path]::GetFullPath($filePath)
    if (-not $fullPath.StartsWith($srcDir + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        exit 0
    }

    & dotnet format whitespace $srcDir --folder --include $fullPath | Out-Null
}
catch {
    [Console]::Error.WriteLine("format-cs.ps1: $($_.Exception.Message)")
}

exit 0
