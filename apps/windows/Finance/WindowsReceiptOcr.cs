using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Andivum_Windows.Finance;

public static class WindowsReceiptOcr
{
    public static async Task<string> ExtractAsync(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("Document is empty.");
        }

        using var stream = new InMemoryRandomAccessStream();
        using (var output = stream.GetOutputStreamAt(0))
        {
            await output.WriteAsync(bytes.AsBuffer());
            await output.FlushAsync();
        }

        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var bitmap = await decoder.GetSoftwareBitmapAsync();
        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? throw new InvalidOperationException("Windows OCR is unavailable for the current language profile.");
        var result = await engine.RecognizeAsync(bitmap);
        return result.Text;
    }
}
