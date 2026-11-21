using System;
using System.IO;
using System.Runtime.InteropServices;
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

        /// <summary>BGRA32 の 1 ピクセルあたりのバイト数</summary>
        private const int BytesPerPixel = 4;

        /// <summary>64 ビットプロセスで描画できる 1 ページあたりの最大画素数 (BGRA32 で約 1.2 GB)</summary>
        internal const long MaxRenderPixelsX64 = 300_000_000;

        /// <summary>32 ビットプロセスで描画できる 1 ページあたりの最大画素数 (BGRA32 で約 400 MB)</summary>
        /// <remarks>アドレス空間が狭く、描画中はビットマップの複製が複数同時に存在するため 64 ビットより低く抑える</remarks>
        internal const long MaxRenderPixelsX86 = 100_000_000;

        /// <summary>PDFium の描画フラグ: 注釈を描画する (<c>FPDF_ANNOT</c>)</summary>
        private const int RenderFlagAnnotations = 0x01;

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


        /********************************************************************************/
        /*                                コンストラクタ                                */
        /********************************************************************************/
        /// <summary>
        /// PDF のバイト列から文書を開く
        /// </summary>
        /// <param name="fileBytes">PDF ファイルのバイト配列</param>
        /// <exception cref="ArgumentException"><paramref name="fileBytes"/> が空の場合</exception>
        /// <exception cref="InvalidDataException">PDF を開けない場合 (破損・パスワード保護など)</exception>
        public PdfiumDocument(byte[] fileBytes)
        {
            if (fileBytes == null || fileBytes.Length == 0)
            {
                throw new ArgumentException("PDF のデータが空です。", nameof(fileBytes));
            }

            _bufferHandle = GCHandle.Alloc(fileBytes, GCHandleType.Pinned);
            try
            {
                lock (PdfiumLock)
                {
                    EnsureLibraryInitialized();

                    FpdfDocumentT? document = fpdfview.FPDF_LoadMemDocument(_bufferHandle.AddrOfPinnedObject(), fileBytes.Length, null);
                    if (document == null || document.__Instance == IntPtr.Zero)
                    {
                        throw CreateLoadException(fpdfview.FPDF_GetLastError());
                    }

                    _document = document;
                    PageCount = fpdfview.FPDF_GetPageCount(document);
                    InitializeFormFillEnvironment(document);
                }
            }
            catch
            {
                _bufferHandle.Free();
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
        /// 指定した倍率で描画したときのページのピクセルサイズを返す
        /// </summary>
        /// <param name="pageIndex">ページインデックス (0 始まり)</param>
        /// <param name="scale">描画倍率 (1.0 = 72 DPI)</param>
        /// <returns>幅と高さ (ピクセル、小数点以下切り捨て)</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> が範囲外の場合</exception>
        /// <exception cref="InvalidDataException">ページサイズを取得できない場合</exception>
        public (int Width, int Height) GetPagePixelSize(int pageIndex, double scale)
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

                return ((int)(width * scale), (int)(height * scale));
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

            int width;
            int height;
            byte[] pixels;
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
                    (width, height) = ValidateRenderSize(
                        pageIndex,
                        fpdfview.FPDF_GetPageWidth(page) * scale,
                        fpdfview.FPDF_GetPageHeight(page) * scale,
                        MaxRenderPixels);
                    pixels = RenderPageToBuffer(page, _formHandle, width, height, scale);
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

            var source = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixels,
                width * BytesPerPixel);
            source.Freeze();
            return source;
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
        /// <returns>例外</returns>
        private static InvalidDataException CreateLoadException(ulong errorCode)
        {
            switch (errorCode)
            {
                case PdfiumErrorPassword:
                    return new InvalidDataException("パスワードで保護された PDF は開けません。");
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
        /// 読み込み済みページを BGRA32 のバイト配列へ描画する (呼び出し側で <see cref="PdfiumLock"/> を保持すること)
        /// </summary>
        /// <param name="page">ページハンドル</param>
        /// <param name="formHandle">フォーム環境のハンドル (<c>null</c> の場合はフォームフィールドを描画しない)</param>
        /// <param name="width">描画幅 (ピクセル)</param>
        /// <param name="height">描画高さ (ピクセル)</param>
        /// <param name="scale">描画倍率</param>
        /// <returns>stride = width * 4 のピクセルデータ</returns>
        /// <exception cref="InsufficientMemoryException">描画用ビットマップを確保できない場合</exception>
        private static byte[] RenderPageToBuffer(FpdfPageT page, FpdfFormHandleT? formHandle, int width, int height, double scale)
        {
            // アルファ付きで作成したビットマップは透明 (全 0) で初期化される
            FpdfBitmapT? bitmap = fpdfview.FPDFBitmapCreate(width, height, 1);
            if (bitmap == null || bitmap.__Instance == IntPtr.Zero)
            {
                throw new InsufficientMemoryException($"描画用ビットマップを確保できませんでした ({width}x{height})。");
            }

            try
            {
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

                int stride = fpdfview.FPDFBitmapGetStride(bitmap);
                IntPtr buffer = fpdfview.FPDFBitmapGetBuffer(bitmap);
                int rowLength = width * BytesPerPixel;
                var pixels = new byte[rowLength * height];
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(IntPtr.Add(buffer, y * stride), pixels, y * rowLength, rowLength);
                }

                return pixels;
            }
            finally
            {
                fpdfview.FPDFBitmapDestroy(bitmap);
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
    }
}
