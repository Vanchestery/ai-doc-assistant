using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AiDocAssistant.Infrastructure.Parsing;

/// <summary>
/// OCR via CLI processes: tesseract (recognition) and pdftoppm (PDF rasterization).
/// CLI instead of .NET wrappers — see DECISIONS.md (no native dependencies,
/// works the same on Windows and in a Linux container).
/// </summary>
public class OcrCli
{
    private readonly ILogger<OcrCli> _logger;

    public OcrCli(ILogger<OcrCli> logger) => _logger = logger;

    /// <summary>Recognize text from an image. Languages: Russian + English.</summary>
    public async Task<string> RecognizeImageAsync(string imagePath, CancellationToken ct = default)
    {
        // "stdout" instead of an output file name -> text goes straight to the stream
        var output = await RunAsync("tesseract", $"\"{imagePath}\" stdout -l rus+eng", ct);
        return output.Trim();
    }

    /// <summary>Scanned PDF: rasterize pages to PNG and run OCR.</summary>
    public async Task<string> RecognizePdfAsync(string pdfPath, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var prefix = Path.Combine(tempDir, "page");
            // -r 200: 200 DPI — balance of recognition quality and speed
            await RunAsync("pdftoppm", $"-png -r 200 \"{pdfPath}\" \"{prefix}\"", ct);

            var sb = new StringBuilder();
            foreach (var page in Directory.GetFiles(tempDir, "page*.png").OrderBy(f => f))
            {
                sb.AppendLine(await RecognizeImageAsync(page, ct));
                sb.AppendLine();
            }
            return sb.ToString().Trim();
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); }
            catch (Exception e) { _logger.LogWarning(e, "Failed to delete OCR temp folder {Dir}", tempDir); }
        }
    }

    private static async Task<string> RunAsync(string command, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process {command}");

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"{command} exited with code {process.ExitCode}: {stderr}. " +
                $"Make sure {command} is installed and available on PATH.");

        return stdout;
    }
}
