using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OpenccNetLibGui.Models;

namespace OpenccNetLibGui.ViewModels;

internal static class FileOpenViewModel
{
    public static async Task<FileOpenResult> OpenAsync(string path)
    {
        try
        {
            string text;

            if (OpenXmlHelper.IsDocx(path))
                text = await Task.Run(() => OpenXmlHelper.ExtractDocxAllText(path));
            else if (OpenXmlHelper.IsOdt(path))
                text = await Task.Run(() => OpenXmlHelper.ExtractOdtAllText(path));
            else if (EpubHelper.IsEpub(path))
                text = await Task.Run(() => EpubHelper.ExtractEpubAllText(path));
            else
            {
                using var reader = new StreamReader(path, Encoding.UTF8, true);
                text = await reader.ReadToEndAsync();
            }

            return new FileOpenResult(path) { Text = text };
        }
        catch (Exception ex)
        {
            return new FileOpenResult(path) { Error = ex.Message };
        }
    }
}

internal sealed class FileOpenResult
{
    public string Path { get; }
    public string? Text { get; init; }
    public string? Error { get; init; }

    public FileOpenResult(string path)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
    }
}