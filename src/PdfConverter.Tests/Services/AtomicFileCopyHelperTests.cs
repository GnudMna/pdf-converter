using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PdfConverter.Services;
using Xunit;

namespace PdfConverter.Tests.Services
{
    /// <summary>
    /// <see cref="AtomicFileCopyHelper"/> のコピーと失敗時の既存ファイル保護を検証する
    /// </summary>
    public class AtomicFileCopyHelperTests : IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>テストごとに作成する作業ディレクトリ</summary>
        private readonly string _directory;


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        public AtomicFileCopyHelperTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"pdf-converter-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
        }


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// コピー先が存在しない場合に内容がコピーされ、一時ファイルが残らないことを検証する
        /// </summary>
        [Fact]
        public async Task CopyAsync_NewDestination_CopiesContentWithoutLeftovers()
        {
            string source = CreateFile("source.pdf", "new-content");
            string destination = Path.Combine(_directory, "output.pdf");

            await AtomicFileCopyHelper.CopyAsync(source, destination, overwrite: false, TestContext.Current.CancellationToken);

            Assert.Equal("new-content", File.ReadAllText(destination));
            Assert.Equal(new[] { "output.pdf", "source.pdf" }, ListFileNames());
        }

        /// <summary>
        /// 上書きを許可した場合に既存ファイルが置き換えられることを検証する
        /// </summary>
        [Fact]
        public async Task CopyAsync_OverwriteAllowed_ReplacesExistingFile()
        {
            string source = CreateFile("source.pdf", "new-content");
            string destination = CreateFile("output.pdf", "existing-content");

            await AtomicFileCopyHelper.CopyAsync(source, destination, overwrite: true, TestContext.Current.CancellationToken);

            Assert.Equal("new-content", File.ReadAllText(destination));
            Assert.Equal(new[] { "output.pdf", "source.pdf" }, ListFileNames());
        }

        /// <summary>
        /// 上書きを許可しない場合に既存ファイルが残り、一時ファイルも残らないことを検証する
        /// </summary>
        [Fact]
        public async Task CopyAsync_OverwriteNotAllowed_KeepsExistingFileAndThrows()
        {
            string source = CreateFile("source.pdf", "new-content");
            string destination = CreateFile("output.pdf", "existing-content");

            await Assert.ThrowsAsync<IOException>(
                () => AtomicFileCopyHelper.CopyAsync(source, destination, overwrite: false, TestContext.Current.CancellationToken));

            Assert.Equal("existing-content", File.ReadAllText(destination));
            Assert.Equal(new[] { "output.pdf", "source.pdf" }, ListFileNames());
        }

        /// <summary>
        /// コピー元が存在しない場合に既存ファイルが壊れず、一時ファイルも残らないことを検証する
        /// </summary>
        [Fact]
        public async Task CopyAsync_SourceMissing_KeepsExistingFileAndThrows()
        {
            string destination = CreateFile("output.pdf", "existing-content");

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => AtomicFileCopyHelper.CopyAsync(
                    Path.Combine(_directory, "missing.pdf"), destination, overwrite: true, TestContext.Current.CancellationToken));

            Assert.Equal("existing-content", File.ReadAllText(destination));
            Assert.Equal(new[] { "output.pdf" }, ListFileNames());
        }

        /// <summary>
        /// キャンセル済みの場合に既存ファイルが壊れず、一時ファイルも残らないことを検証する
        /// </summary>
        [Fact]
        public async Task CopyAsync_Cancelled_KeepsExistingFileAndThrows()
        {
            string source = CreateFile("source.pdf", "new-content");
            string destination = CreateFile("output.pdf", "existing-content");
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => AtomicFileCopyHelper.CopyAsync(source, destination, overwrite: true, cts.Token));

            Assert.Equal("existing-content", File.ReadAllText(destination));
            Assert.Equal(new[] { "output.pdf", "source.pdf" }, ListFileNames());
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>
        /// 作業ディレクトリにファイルを作成する
        /// </summary>
        /// <param name="name">ファイル名</param>
        /// <param name="content">ファイルの内容</param>
        /// <returns>作成したファイルの絶対パス</returns>
        private string CreateFile(string name, string content)
        {
            string path = Path.Combine(_directory, name);
            File.WriteAllText(path, content);
            return path;
        }

        /// <summary>
        /// 作業ディレクトリ直下のファイル名 (隠しファイルを含む) を昇順で返す
        /// </summary>
        /// <returns>ファイル名の一覧</returns>
        private string[] ListFileNames()
        {
            return Directory.EnumerateFiles(_directory)
                .Select(path => Path.GetFileName(path)!)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
    }
}
