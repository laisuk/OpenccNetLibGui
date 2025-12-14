using System;
using System.Threading;
using System.Threading.Tasks;
using OpenccNetLibGui.Models;
using OpenccNetLibGui.Services;

namespace OpenccNetLibGui.ViewModels
{
    public sealed class PdfViewModel : ViewModelBase
    {
        private CancellationTokenSource? _pdfCts;
        private int _pdfRequestId;

        public PdfEngine PdfEngine { get; set; }
        public bool IsAddPdfPageHeader { get; set; }
        public bool IsCompactPdfText { get; set; }
        public bool IsAutoReflow { get; set; }

        public int ShortHeadingMaxLen { get; set; }
        public ShortHeadingSettings? ShortHeading { get; set; }

        public void Cancel()
        {
            _pdfCts?.Cancel();
        }

        public int NewRequestId()
        {
            _pdfCts?.Cancel();
            _pdfCts = new CancellationTokenSource();
            return Interlocked.Increment(ref _pdfRequestId);
        }

        public int CurrentRequestId => _pdfRequestId;

        public CancellationToken CurrentToken => _pdfCts?.Token ?? CancellationToken.None;

        public Task<PdfLoadResult> LoadPdfAsync(
            string filePath,
            Action<string>? statusCallback,
            CancellationToken cancellationToken)
        {
            return LoadPdfTextCoreAsync(
                filePath,
                statusCallback,
                cancellationToken);
        }

        private async Task<PdfLoadResult> LoadPdfTextCoreAsync(
            string filePath,
            Action<string>? statusCallback,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1) Extract
            PdfExtractResult extract;

            switch (PdfEngine)
            {
                case PdfEngine.Pdfium:
                    // statusCallback?.Invoke("📄 Extracting PDF text (Pdfium)...");
                    // 你的原逻辑：PdfiumModel.LoadPdfTextAsync(...)
                    extract = await PdfiumModel.ExtractTextAsync(
                        filePath,
                        IsAddPdfPageHeader,
                        statusCallback,
                        cancellationToken);
                    // 如果 Pdfium 那边能拿页数就填；不能就留 0
                    break;

                case PdfEngine.PdfPig:
                default:
                    // statusCallback?.Invoke("📄 Extracting PDF text (PdfPig)...");
                    extract = await PdfHelper.LoadPdfTextAsync(
                        filePath,
                        IsAddPdfPageHeader,
                        statusCallback,
                        cancellationToken);
                    break;
            }

            var text = extract.Text;
            var pageCount = extract.PageCount;

            // 2) Auto reflow (保持原样)
            var reflowApplied = false;
            if (IsAutoReflow && !string.IsNullOrWhiteSpace(text))
            {
                statusCallback?.Invoke("🧹 Reflowing CJK paragraphs...");
                text = ReflowModel.ReflowCjkParagraphs(
                    text,
                    addPdfPageHeader: IsAddPdfPageHeader,
                    compact: IsCompactPdfText,
                    shortHeading: ShortHeading);

                reflowApplied = true;
            }

            // 3) Return record
            return new PdfLoadResult(
                Text: text ?? string.Empty,
                EngineUsed: PdfEngine,
                AutoReflowApplied: reflowApplied,
                PageCount: pageCount
            );
        }
    }
}