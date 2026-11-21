using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PDFiumCore;

namespace PdfConverter.Services
{
    /// <summary>
    /// PDFium (PDFiumCore) で開いた PDF 文書を表す薄いラッパー
    /// </summary>
    /// <remarks>
    /// PDFium はスレッドセーフではないため、すべてのネイティブ呼び出しをプロセス共通のロックで直列化する<br/>
    /// PDFium は文書を閉じるまで入力バッファーを参照するため、バイト配列を固定 (ピン留め) して保持する<br/>
    /// <see cref="OpenFile"/> で開いた文書はファイル全体を読み込まず、PDFium が必要とした範囲だけをファイルから読む<br/>
    /// ハイライト・スタンプなどの注釈と、フォームフィールドの入力内容も描画する<br/>
    /// メモリ不足や int のオーバーフローを防ぐため、1 ページあたりの描画画素数を <see cref="MaxRenderPixels"/> までに制限する
    /// </remarks>
    internal sealed class PdfiumDocument : IDisposable
    {
        /********************************************************************************/
        /*                                 ローカル変数                                 */
        /********************************************************************************/
        /// <summary>PDFium のエラーコード: パスワードが必要または不正</summary>
        private const ulong PdfiumErrorPassword = 4;

        /// <summary>PDFium のエラーコード: 未対応のセキュリティハンドラー</summary>
        private const ulong PdfiumErrorSecurity = 5;

        /// <summary>PDFium のビットマップ形式: BGRA 32 ビット (<c>FPDFBitmap_BGRA</c>)</summary>
        private const int PdfiumBitmapFormatBgra = 4;

        /// <summary>PDFium が scale 1.0 で描画するネイティブ DPI</summary>
        internal const double NativeDpi = 72.0;

        /// <summary>64 ビットプロセスで描画できる 1 ページあたりの最大画素数 (BGRA32 で約 1.2 GB)</summary>
        internal const long MaxRenderPixelsX64 = 300_000_000;

        /// <summary>32 ビットプロセスで描画できる 1 ページあたりの最大画素数 (BGRA32 で約 400 MB)</summary>
        /// <remarks>アドレス空間が狭く、連続した大きな領域を確保しにくいため 64 ビットより低く抑える</remarks>
        internal const long MaxRenderPixelsX86 = 100_000_000;

        /// <summary>PDFium の描画フラグ: 注釈を描画する (<c>FPDF_ANNOT</c>)</summary>
        private const int RenderFlagAnnotations = 0x01;

        /// <summary><see cref="OpenFile"/> で開いたファイルの読み込みバッファーのサイズ (バイト)</summary>
        /// <remarks>PDFium は構文解析中に小さな範囲を近い位置から繰り返し読むため、システムコールの回数を抑える</remarks>
        private const int FileReadBufferSize = 16 * 1024;

        /// <summary>読み込みコールバックが 1 回に読み込む最大バイト数</summary>
        private const int MaxReadChunkSize = 1024 * 1024;

        /// <summary>フォーム環境に渡す <c>FPDF_FORMFILLINFO</c> の構造体バージョン</summary>
        private const int FormFillInfoVersion = 2;

        /// <summary>PDFium 呼び出しを直列化するロックオブジェクト</summary>
        private static readonly object PdfiumLock = new object();

        /// <summary>PDFium ライブラリを初期化済みかどうか</summary>
        private static bool _libraryInitialized;

        /// <summary>PDFium に渡した入力バッファーの固定ハンドル</summary>
        private GCHandle _bufferHandle;

        /// <summary>PDFium の文書ハンドル</summary>
        private FpdfDocumentT? _document;

        /// <summary>フォーム環境の初期化情報 (PDFium がフォーム環境の終了まで参照するため保持する)</summary>
        private FPDF_FORMFILLINFO? _formFillInfo;

        /// <summary>フォーム環境のハンドル (フォームを描画できない場合は <c>null</c>)</summary>
        private FpdfFormHandleT? _formHandle;

        /// <summary>ファイルから開いた場合の入力ストリーム (バイト配列から開いた場合は <c>null</c>)</summary>
        private FileStream? _fileStream;

        /// <summary>ファイルから開いた場合のファイルアクセス情報 (PDFium が文書を閉じるまで参照するため保持する)</summary>
        private FPDF_FILEACCESS? _fileAccess;

        /// <summary>PDFium に渡した読み込みコールバック (ネイティブ側が参照する間 GC に回収されないよう保持する)</summary>
        private GetBlockCallback? _getBlockCallback;

        /// <summary>読み込みコールバックで使い回す読み込みバッファー</summary>
        private byte[] _readBuffer = Array.Empty<byte>();


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// PDF のバイト列から文書を開く
        /// </summary>
        /// <param name="fileBytes">PDF ファイルのバイト配列</param>
        /// <param name="password">PDF を開くパスワード (パスワードなしの場合は <c>null</c>)</param>
        /// <exception cref="ArgumentException"><paramref name="fileBytes"/> が空の場合</exception>
        /// <exception cref="PdfPasswordRequiredException">パスワードが必要、または <paramref name="password"/> が正しくない場合</exception>
        /// <exception cref="InvalidDataException">PDF を開けない場合 (破損など)</exception>
        public PdfiumDocument(byte[] fileBytes, string? password = null)
        {
            if (fileBytes == null || fileBytes.Length == 0)
            {
                throw new ArgumentException("PDF のデータが空です。", nameof(fileBytes));
            }

            _bufferHandle = GCHandle.Alloc(fileBytes, GCHandleType.Pinned);
            try
            {
                PageCount = LoadDocument(() => fpdfview.FPDF_LoadMemDocument(_bufferHandle.AddrOfPinnedObject(), fileBytes.Length, password), password);
            }
            catch
            {
                _bufferHandle.Free();
                throw;
            }
        }

        /// <summary>
        /// 開いたファイルストリームから、PDFium が必要とした範囲だけを読み込む文書を開く
        /// </summary>
        /// <param name="fileStream">読み取り可能かつシーク可能なファイルストリーム (文書の破棄時に閉じる)</param>
        /// <param name="password">PDF を開くパスワード (パスワードなしの場合は <c>null</c>)</param>
        /// <exception cref="PdfPasswordRequiredException">パスワードが必要、または <paramref name="password"/> が正しくない場合</exception>
        /// <exception cref="InvalidDataException">PDF を開けない場合 (空・4 GB 以上・破損など)</exception>
        private PdfiumDocument(FileStream fileStream, string? password)
        {
            long length = fileStream.Length;
            if (length == 0)
            {
                throw new InvalidDataException("PDF のデータが空です。");
            }

            // FPDF_FILEACCESS のファイル長と読み込み位置は Windows では 32 ビットの unsigned long
            if (length > uint.MaxValue)
            {
                throw new InvalidDataException("4 GB 以上の PDF ファイルには対応していません。");
            }

            _fileStream = fileStream;
            _getBlockCallback = ReadBlock;
            _fileAccess = CreateFileAccess((uint)length, _getBlockCallback);
            try
            {
                PageCount = LoadDocument(() => fpdfview.FPDF_LoadCustomDocument(_fileAccess, password), password);
            }
            catch
            {
                _fileAccess.Dispose();
                _fileAccess = null;
                _fileStream = null;
                throw;
            }
        }


        /********************************************************************************/
        /*                                  プロパティ                                  */
        /********************************************************************************/
        /// <summary>PDF のページ数</summary>
        public int PageCount { get; }

        /// <summary>実行中のプロセスで描画できる 1 ページあたりの最大画素数</summary>
        internal static long MaxRenderPixels => Environment.Is64BitProcess ? MaxRenderPixelsX64 : MaxRenderPixelsX86;


        /********************************************************************************/
        /*                              パブリックメソッド                              */
        /********************************************************************************/
        /// <summary>
        /// PDF ファイルを、ファイル全体を読み込まずに開く
        /// </summary>
        /// <remarks>
        /// 文書を破棄するまでファイルを開いたままにする。ほかのプロセスによる書き込み・削除は妨げない<br/>
        /// 開いている間にファイルが書き換えられた場合の描画結果は保証しない
        /// </remarks>
        /// <param name="filePath">PDF ファイルのパス</param>
        /// <param name="password">PDF を開くパスワード (パスワードなしの場合は <c>null</c>)</param>
        /// <returns>開いた文書</returns>
        /// <exception cref="PdfPasswordRequiredException">パスワードが必要、または <paramref name="password"/> が正しくない場合</exception>
        /// <exception cref="InvalidDataException">PDF を開けない場合 (空・4 GB 以上・破損など)</exception>
        /// <exception cref="IOException">ファイルを開けない場合</exception>
        public static PdfiumDocument OpenFile(string filePath, string? password = null)
        {
            var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                FileReadBufferSize,
                FileOptions.RandomAccess);
            try
            {
                return new PdfiumDocument(fileStream, password);
            }
            catch
            {
                fileStream.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 指定した倍率で描画したときのページのピクセルサイズを返す
        /// </summary>
        /// <param name="pageIndex">ページインデックス (0 始まり)</param>
        /// <param name="scale">描画倍率 (1.0 = 72 DPI)</param>
        /// <returns>幅と高さ (ピクセル、小数点以下切り捨て)</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> が範囲外の場合</exception>
        /// <exception cref="InvalidDataException">ページサイズを取得できない場合</exception>
        public (int Width, int Height) GetPagePixelSize(int pageIndex, double scale)
        {
            (double width, double height) = GetPageSize(pageIndex);
            return ((int)(width * scale), (int)(height * scale));
        }

        /// <summary>
        /// ページの寸法を返す (1.0 = 72 DPI のピクセルに相当するポイント数、小数点以下を保持)
        /// </summary>
        /// <param name="pageIndex">ページインデックス (0 始まり)</param>
        /// <returns>幅と高さ (ポイント)</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> が範囲外の場合</exception>
        /// <exception cref="InvalidDataException">ページサイズを取得できない場合</exception>
        public (double Width, double Height) GetPageSize(int pageIndex)
        {
            ValidatePageIndex(pageIndex);

            lock (PdfiumLock)
            {
                FpdfDocumentT document = GetDocument();
                double width = 0;
                double height = 0;
                if (fpdfview.FPDF_GetPageSizeByIndex(document, pageIndex, ref width, ref height) == 0)
                {
                    throw new InvalidDataException($"ページサイズを取得できませんでした (ページ {pageIndex + 1})。");
                }

                return (width, height);
            }
        }

        /// <summary>
        /// 指定した倍率で描画するときのピクセルサイズを、描画できる上限内であることを検証したうえで返す
        /// </summary>
        /// <param name="pageIndex">ページインデックス (0 始まり)</param>
        /// <param name="scale">描画倍率 (1.0 = 72 DPI)</param>
        /// <returns>幅と高さ (ピクセル、小数点以下切り捨て)</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> が範囲外の場合</exception>
        /// <exception cref="InvalidDataException">ページサイズを取得できない、または描画サイズが 0 以下の場合</exception>
        /// <exception cref="InvalidOperationException">描画サイズが <see cref="MaxRenderPixels"/> を超える場合</exception>
        public (int Width, int Height) GetRenderSize(int pageIndex, double scale)
        {
            ValidatePageIndex(pageIndex);

            lock (PdfiumLock)
            {
                FpdfDocumentT document = GetDocument();
                double width = 0;
                double height = 0;
                if (fpdfview.FPDF_GetPageSizeByIndex(document, pageIndex, ref width, ref height) == 0)
                {
                    throw new InvalidDataException($"ページサイズを取得できませんでした (ページ {pageIndex + 1})。");
                }

                return ValidateRenderSize(pageIndex, width * scale, height * scale, MaxRenderPixels);
            }
        }

        /// <summary>
        /// 指定ページを透過背景の BGRA32 ビットマップとして描画する
        /// </summary>
        /// <param name="pageIndex">ページインデックス (0 始まり)</param>
        /// <param name="scale">描画倍率 (1.0 = 72 DPI)</param>
        /// <returns>凍結済みのビットマップ</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> が範囲外の場合</exception>
        /// <exception cref="InvalidDataException">ページを読み込めない、または描画サイズが 0 以下の場合</exception>
        /// <exception cref="InvalidOperationException">描画サイズが <see cref="MaxRenderPixels"/> を超える場合</exception>
        /// <exception cref="InsufficientMemoryException">描画用ビットマップを確保できない場合</exception>
        public BitmapSource RenderPage(int pageIndex, double scale)
        {
            ValidatePageIndex(pageIndex);

            // 1 ポイント = 1 ピクセル (scale 1.0) が 72 DPI なので、倍率を掛けた値を DPI メタデータとして設定する
            double dpi = NativeDpi * scale;
            WriteableBitmap result;
            lock (PdfiumLock)
            {
                FpdfDocumentT document = GetDocument();
                FpdfPageT? page = fpdfview.FPDF_LoadPage(document, pageIndex);
                if (page == null || page.__Instance == IntPtr.Zero)
                {
                    throw new InvalidDataException($"ページを読み込めませんでした (ページ {pageIndex + 1})。");
                }

                if (_formHandle != null)
                {
                    fpdf_formfill.FORM_OnAfterLoadPage(page, _formHandle);
                }

                try
                {
                    (int width, int height) = ValidateRenderSize(
                        pageIndex,
                        fpdfview.FPDF_GetPageWidth(page) * scale,
                        fpdfview.FPDF_GetPageHeight(page) * scale,
                        MaxRenderPixels);
                    result = CreateBitmap(width, height, dpi);
                    RenderPageToBitmap(page, _formHandle, result, scale);
                }
                finally
                {
                    if (_formHandle != null)
                    {
                        fpdf_formfill.FORM_OnBeforeClosePage(page, _formHandle);
                    }

                    fpdfview.FPDF_ClosePage(page);
                }
            }

            result.Freeze();
            return result;
        }

        /// <summary>
        /// 文書を閉じ、入力バッファーの固定を解除する
        /// </summary>
        public void Dispose()
        {
            lock (PdfiumLock)
            {
                if (_formHandle != null)
                {
                    fpdf_formfill.FPDFDOC_ExitFormFillEnvironment(_formHandle);
                    _formHandle = null;
                }

                if (_document != null)
                {
                    fpdfview.FPDF_CloseDocument(_document);
                    _document = null;
                }
            }

            _formFillInfo?.Dispose();
            _formFillInfo = null;

            _fileAccess?.Dispose();
            _fileAccess = null;
            _fileStream?.Dispose();
            _fileStream = null;
            _getBlockCallback = null;

            if (_bufferHandle.IsAllocated)
            {
                _bufferHandle.Free();
            }
        }


        /********************************************************************************/
        /*                             プライベートメソッド                             */
        /********************************************************************************/
        /// <summary>PDFium ライブラリを初回のみ初期化する (呼び出し側で <see cref="PdfiumLock"/> を保持すること)</summary>
        private static void EnsureLibraryInitialized()
        {
            if (_libraryInitialized)
            {
                return;
            }

            fpdfview.FPDF_InitLibrary();
            _libraryInitialized = true;
        }

        /// <summary>
        /// 文書を読み込み、フォーム環境を初期化する
        /// </summary>
        /// <param name="load">ロック内で PDFium の読み込み関数を呼び出し、文書ハンドルを返す関数</param>
        /// <param name="password">読み込みに使ったパスワード (パスワードなしの場合は <c>null</c>)</param>
        /// <returns>ページ数</returns>
        /// <exception cref="PdfPasswordRequiredException">パスワードが必要、または <paramref name="password"/> が正しくない場合</exception>
        /// <exception cref="InvalidDataException">PDF を開けない場合</exception>
        private int LoadDocument(Func<FpdfDocumentT?> load, string? password)
        {
            lock (PdfiumLock)
            {
                EnsureLibraryInitialized();

                FpdfDocumentT? document = load();
                if (document == null || document.__Instance == IntPtr.Zero)
                {
                    throw CreateLoadException(fpdfview.FPDF_GetLastError(), password != null);
                }

                _document = document;
                int pageCount = fpdfview.FPDF_GetPageCount(document);
                InitializeFormFillEnvironment(document);
                return pageCount;
            }
        }

        /// <summary>
        /// PDFium に渡すファイルアクセス情報を生成する
        /// </summary>
        /// <remarks>
        /// PDFiumCore の <see cref="FPDF_FILEACCESS"/> はファイル長を 64 ビット、各フィールドを 64 ビット環境の位置で定義しており、
        /// 32 ビット環境の C の構造体と配置が一致しない<br/>
        /// そのため確保された領域へ、実行環境の C の配置に合わせた <see cref="NativeFileAccess"/> を書き込む
        /// </remarks>
        /// <param name="fileLength">ファイル長 (バイト)</param>
        /// <param name="getBlock">読み込みコールバック</param>
        /// <returns>ファイルアクセス情報</returns>
        private static FPDF_FILEACCESS CreateFileAccess(uint fileLength, GetBlockCallback getBlock)
        {
            var fileAccess = new FPDF_FILEACCESS();
            if (Marshal.SizeOf<NativeFileAccess>() > Marshal.SizeOf<FPDF_FILEACCESS.__Internal>())
            {
                fileAccess.Dispose();
                throw new PlatformNotSupportedException("PDFium のファイルアクセス情報を確保できません。");
            }

            var native = new NativeFileAccess
            {
                FileLength = fileLength,
                GetBlock = Marshal.GetFunctionPointerForDelegate(getBlock),
                Param = IntPtr.Zero,
            };
            Marshal.StructureToPtr(native, fileAccess.__Instance, false);
            return fileAccess;
        }

        /// <summary>
        /// PDFium の要求に応じて、ファイルの指定範囲を読み込む (PDFium の呼び出し中にのみ呼ばれるため <see cref="PdfiumLock"/> を保持している)
        /// </summary>
        /// <remarks>例外をネイティブ側へ送出するとプロセスが終了するため、失敗はすべて戻り値で返す</remarks>
        /// <param name="param">未使用</param>
        /// <param name="position">読み込み開始位置 (バイト)</param>
        /// <param name="buffer">読み込み先のバッファー</param>
        /// <param name="size">読み込むバイト数</param>
        /// <returns>成功した場合は 0 以外、失敗した場合は 0</returns>
        private int ReadBlock(IntPtr param, uint position, IntPtr buffer, uint size)
        {
            try
            {
                FileStream? stream = _fileStream;
                if (stream == null || size > int.MaxValue || (long)position + size > stream.Length)
                {
                    return 0;
                }

                // 大きな画像ストリームなどの一括要求でもバッファーが肥大化しないよう、上限サイズずつ読んで書き込む
                int remaining = (int)size;
                if (_readBuffer.Length < Math.Min(remaining, MaxReadChunkSize))
                {
                    _readBuffer = new byte[Math.Min(remaining, MaxReadChunkSize)];
                }

                stream.Position = position;
                IntPtr destination = buffer;
                while (remaining > 0)
                {
                    int chunk = Math.Min(remaining, _readBuffer.Length);
                    stream.ReadExactly(_readBuffer, 0, chunk);
                    Marshal.Copy(_readBuffer, 0, destination, chunk);
                    destination = IntPtr.Add(destination, chunk);
                    remaining -= chunk;
                }

                return 1;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// フォームフィールドを描画するためのフォーム環境を初期化する (呼び出し側で <see cref="PdfiumLock"/> を保持すること)
        /// </summary>
        /// <remarks>
        /// 初期化できない場合はフォームフィールドを描画せず、注釈とページ本体のみ描画する
        /// </remarks>
        /// <param name="document">文書ハンドル</param>
        private void InitializeFormFillEnvironment(FpdfDocumentT document)
        {
            var formFillInfo = new FPDF_FORMFILLINFO();

            // コールバックはすべて未設定 (null) とするため、構造体全体を 0 で初期化してからバージョンを設定する
            int size = Marshal.SizeOf<FPDF_FORMFILLINFO.__Internal>();
            Marshal.Copy(new byte[size], 0, formFillInfo.__Instance, size);
            formFillInfo.Version = FormFillInfoVersion;

            FpdfFormHandleT? formHandle = fpdf_formfill.FPDFDOC_InitFormFillEnvironment(document, formFillInfo);
            if (formHandle == null || formHandle.__Instance == IntPtr.Zero)
            {
                formFillInfo.Dispose();
                return;
            }

            // 画像出力にフォームフィールドの入力欄ハイライトを含めない
            fpdf_formfill.FPDF_SetFormFieldHighlightAlpha(formHandle, 0);

            _formFillInfo = formFillInfo;
            _formHandle = formHandle;
        }

        /// <summary>
        /// 文書の読み込み失敗を表す例外を生成する
        /// </summary>
        /// <param name="errorCode"><c>FPDF_GetLastError</c> のエラーコード</param>
        /// <param name="wasPasswordProvided">パスワードを指定して開こうとしたかどうか</param>
        /// <returns>例外</returns>
        private static Exception CreateLoadException(ulong errorCode, bool wasPasswordProvided)
        {
            switch (errorCode)
            {
                case PdfiumErrorPassword:
                    return new PdfPasswordRequiredException(wasPasswordProvided);
                case PdfiumErrorSecurity:
                    return new InvalidDataException("この PDF のセキュリティ方式には対応していません。");
                default:
                    return new InvalidDataException($"PDF ファイルを開けませんでした。ファイルが破損している可能性があります (エラーコード: {errorCode})。");
            }
        }

        /// <summary>
        /// 描画サイズ (小数) をピクセル数に切り捨て、0 以下でないことと画素数が上限以内であることを検証する
        /// </summary>
        /// <remarks>
        /// int へ変換する前に検証し、巨大なページや高倍率でのオーバーフローを防ぐ<br/>
        /// 上限以内であれば幅・高さ・バッファーのバイト数 (幅 x 高さ x 4) はいずれも int に収まる
        /// </remarks>
        /// <param name="pageIndex">ページインデックス (エラーメッセージ用)</param>
        /// <param name="width">描画幅 (ピクセル、小数)</param>
        /// <param name="height">描画高さ (ピクセル、小数)</param>
        /// <param name="maxPixels">許容する最大画素数</param>
        /// <returns>幅と高さ (ピクセル)</returns>
        /// <exception cref="InvalidDataException">描画サイズが 1 ピクセル未満または数値でない場合</exception>
        /// <exception cref="InvalidOperationException">画素数が <paramref name="maxPixels"/> を超える場合</exception>
        internal static (int Width, int Height) ValidateRenderSize(int pageIndex, double width, double height, long maxPixels)
        {
            double flooredWidth = Math.Floor(width);
            double flooredHeight = Math.Floor(height);

            // NaN も不正として扱うため、否定形で判定する
            if (!(flooredWidth >= 1) || !(flooredHeight >= 1))
            {
                throw new InvalidDataException($"描画サイズが不正です (ページ {pageIndex + 1}: {flooredWidth}x{flooredHeight})。");
            }

            if (flooredWidth * flooredHeight > maxPixels)
            {
                throw new InvalidOperationException(
                    $"ページ {pageIndex + 1} の描画サイズ ({flooredWidth:0}x{flooredHeight:0} px) が上限 ({maxPixels / 100_000_000.0:0.##} 億画素) を超えています。解像度を下げてください。");
            }

            return ((int)flooredWidth, (int)flooredHeight);
        }

        /// <summary>
        /// 描画先の BGRA32 ビットマップを確保する
        /// </summary>
        /// <param name="width">幅 (ピクセル)</param>
        /// <param name="height">高さ (ピクセル)</param>
        /// <param name="dpi">DPI メタデータ</param>
        /// <returns>未凍結のビットマップ</returns>
        /// <exception cref="InsufficientMemoryException">描画用ビットマップを確保できない場合</exception>
        private static WriteableBitmap CreateBitmap(int width, int height, double dpi)
        {
            try
            {
                return new WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, null);
            }
            catch (OutOfMemoryException ex)
            {
                throw new InsufficientMemoryException($"描画用ビットマップを確保できませんでした ({width}x{height})。", ex);
            }
        }

        /// <summary>
        /// 読み込み済みページを <paramref name="target"/> のバックバッファーへ直接描画する (呼び出し側で <see cref="PdfiumLock"/> を保持すること)
        /// </summary>
        /// <remarks>
        /// PDFium の描画先にバックバッファーを渡すため、ピクセルデータの複製は発生しない<br/>
        /// 描画後に全体を更新領域として通知する
        /// </remarks>
        /// <param name="page">ページハンドル</param>
        /// <param name="formHandle">フォーム環境のハンドル (<c>null</c> の場合はフォームフィールドを描画しない)</param>
        /// <param name="target">描画先 (BGRA32、未凍結で、呼び出しスレッドが作成したもの)</param>
        /// <param name="scale">描画倍率</param>
        /// <exception cref="InsufficientMemoryException">PDFium の描画用ビットマップを作成できない場合</exception>
        private static void RenderPageToBitmap(FpdfPageT page, FpdfFormHandleT? formHandle, WriteableBitmap target, double scale)
        {
            int width = target.PixelWidth;
            int height = target.PixelHeight;

            target.Lock();
            try
            {
                // 外部バッファーを包むだけなので、バックバッファーの寿命は PDFium のビットマップより長い
                FpdfBitmapT? bitmap = fpdfview.FPDFBitmapCreateEx(width, height, PdfiumBitmapFormatBgra, target.BackBuffer, target.BackBufferStride);
                if (bitmap == null || bitmap.__Instance == IntPtr.Zero)
                {
                    throw new InsufficientMemoryException($"描画用ビットマップを確保できませんでした ({width}x{height})。");
                }

                try
                {
                    // 外部バッファーは初期化されないため、透明 (全 0) で塗りつぶす
                    fpdfview.FPDFBitmapFillRect(bitmap, 0, 0, width, height, 0);

                    using (var matrix = new FS_MATRIX_())
                    using (var clipping = new FS_RECTF_())
                    {
                        matrix.A = (float)scale;
                        matrix.B = 0;
                        matrix.C = 0;
                        matrix.D = (float)scale;
                        matrix.E = 0;
                        matrix.F = 0;

                        clipping.Left = 0;
                        clipping.Top = 0;
                        clipping.Right = width;
                        clipping.Bottom = height;

                        fpdfview.FPDF_RenderPageBitmapWithMatrix(bitmap, page, matrix, clipping, RenderFlagAnnotations);
                    }

                    // フォームフィールド (ウィジェット注釈) は通常の描画対象外のため、ページ描画後に重ねて描画する
                    if (formHandle != null)
                    {
                        fpdf_formfill.FPDF_FFLDraw(formHandle, bitmap, page, 0, 0, width, height, 0, RenderFlagAnnotations);
                    }
                }
                finally
                {
                    fpdfview.FPDFBitmapDestroy(bitmap);
                }

                target.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                target.Unlock();
            }
        }

        /// <summary>
        /// ページインデックスが範囲内であることを検証する
        /// </summary>
        /// <param name="pageIndex">ページインデックス</param>
        /// <exception cref="ArgumentOutOfRangeException">範囲外の場合</exception>
        private void ValidatePageIndex(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= PageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pageIndex), "ページインデックスが範囲外です。");
            }
        }

        /// <summary>
        /// 文書ハンドルを返す (呼び出し側で <see cref="PdfiumLock"/> を保持すること)
        /// </summary>
        /// <returns>文書ハンドル</returns>
        /// <exception cref="ObjectDisposedException">破棄済みの場合</exception>
        private FpdfDocumentT GetDocument()
        {
            return _document ?? throw new ObjectDisposedException(nameof(PdfiumDocument));
        }


        /********************************************************************************/
        /*                                    ネスト型                                  */
        /********************************************************************************/
        /// <summary>
        /// <c>FPDF_FILEACCESS.m_GetBlock</c> の関数ポインター型
        /// </summary>
        /// <param name="param"><c>m_Param</c> の値</param>
        /// <param name="position">読み込み開始位置 (バイト)</param>
        /// <param name="buffer">読み込み先のバッファー</param>
        /// <param name="size">読み込むバイト数</param>
        /// <returns>成功した場合は 0 以外、失敗した場合は 0</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GetBlockCallback(IntPtr param, uint position, IntPtr buffer, uint size);

        /// <summary>
        /// 実行環境の C の配置に合わせた <c>FPDF_FILEACCESS</c> 構造体
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeFileAccess
        {
            /// <summary>ファイル長 (<c>m_FileLen</c>、Windows の unsigned long は 32 ビット)</summary>
            public uint FileLength;

            /// <summary>読み込みコールバック (<c>m_GetBlock</c>)</summary>
            public IntPtr GetBlock;

            /// <summary>コールバックへ渡す値 (<c>m_Param</c>)</summary>
            public IntPtr Param;
        }
    }
}
