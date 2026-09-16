using System.Globalization;
using System.Text;

namespace Amaliyotchi.Infrastructure.Persistence.Seeding;

/// <summary>Demo stend uchun haqiqiy (brauzerda ochiladigan) fayl baytlari. Talaba kundalikni daftardan
/// rasmga olib PDF qilib yuboradi — shu holatni ko'rsatish uchun bir betli PDF generatsiya qilinadi.
/// Tashqi kutubxonasiz: minimal, lekin to'g'ri tuzilgan PDF 1.4 (xref jadvali bilan).</summary>
internal static class DemoFiles
{
    /// <summary>Bir betli A4 PDF: sarlavha + matn satrlari (ASCII'ga tushirilgan — standart Helvetica shrifti).</summary>
    public static byte[] OnePagePdf(string title, IReadOnlyList<string> lines)
    {
        var content = BuildContentStream(title, lines);
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] "
                + "/Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"
        };

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>(objects.Count);
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xrefOffset = pdf.Length;
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            pdf.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        pdf.Append(CultureInfo.InvariantCulture,
            $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string BuildContentStream(string title, IReadOnlyList<string> lines)
    {
        var text = new StringBuilder("BT\n/F1 16 Tf\n64 770 Td\n");
        text.Append(CultureInfo.InvariantCulture, $"({Escape(title)}) Tj\n");
        text.Append("/F1 11 Tf\n");
        foreach (var line in lines)
        {
            text.Append("0 -22 Td\n");
            text.Append(CultureInfo.InvariantCulture, $"({Escape(line)}) Tj\n");
        }

        text.Append("ET");
        return text.ToString();
    }

    /// <summary>PDF matn literalidagi maxsus belgilar + lotin bo'lmagan harflarni ASCII'ga tushirish.</summary>
    private static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '(' or ')' or '\\':
                    sb.Append('\\').Append(ch);
                    break;
                case '‘' or '’' or 'ʻ' or 'ʼ':
                    sb.Append('\'');
                    break;
                case '“' or '”':
                    sb.Append('"');
                    break;
                case '–' or '—' or '·':
                    sb.Append('-');
                    break;
                case '…':
                    sb.Append("...");
                    break;
                default:
                    sb.Append(ch <= 127 ? ch : '?');
                    break;
            }
        }

        return sb.ToString();
    }
}
