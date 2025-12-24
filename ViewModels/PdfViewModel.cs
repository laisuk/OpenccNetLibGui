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
        public ShortHeadingSettings? ShortHeading { get; set; }
        public int SentenceBoundaryLevel { get; init; } = 2;

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
            Action<int>? progressCallback,
            CancellationToken cancellationToken)
        {
            return LoadPdfTextCoreAsync(
                filePath,
                progressCallback,
                cancellationToken);
        }

        private async Task<PdfLoadResult> LoadPdfTextCoreAsync(
            string filePath,
            Action<int>? progressCallback,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1) Extract
            PdfExtractResult extract;

            switch (PdfEngine)
            {
                case PdfEngine.Pdfium:
                    // statusCallback?.Invoke("📄 Extracting PDF text (Pdfium)...");
                    // Original logic：PdfiumModel.LoadPdfTextAsync(...)
                    extract = await PdfiumModel.ExtractTextAsync(
                        filePath,
                        IsAddPdfPageHeader,
                        progressCallback,
                        cancellationToken);
                    // If Pdfium can get total page: otherwise 0
                    break;

                case PdfEngine.PdfPig:
                default:
                    // statusCallback?.Invoke("📄 Extracting PDF text (PdfPig)...");
                    extract = await PdfHelper.LoadPdfTextAsync(
                        filePath,
                        IsAddPdfPageHeader,
                        progressCallback,
                        cancellationToken);
                    break;
            }

            var text = extract.Text;
            var pageCount = extract.PageCount;

            // 2) Auto reflow (keep)
            var reflowApplied = false;
            if (!IsAutoReflow || string.IsNullOrWhiteSpace(text))
                return new PdfLoadResult(
                    Text: text,
                    EngineUsed: PdfEngine,
                    AutoReflowApplied: reflowApplied,
                    PageCount: pageCount
                );
            text = ReflowModel.ReflowCjkParagraphs(
                text,
                addPdfPageHeader: IsAddPdfPageHeader,
                compact: IsCompactPdfText,
                shortHeading: ShortHeading,
                sentenceBoundaryLevel: SentenceBoundaryLevel
                );

            reflowApplied = true;

            // 3) Return record
            return new PdfLoadResult(
                Text: text,
                EngineUsed: PdfEngine,
                AutoReflowApplied: reflowApplied,
                PageCount: pageCount
            );
        }
    }
}