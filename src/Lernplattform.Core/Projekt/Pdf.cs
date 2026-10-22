using System.Globalization;
using System.IO.Compression;
using System.Text;
using Lernplattform.Core.Rechner;

namespace Lernplattform.Core.Projekt;

/// <summary>
/// Phase 4, W3: einfacher PDF-Export wie fisi_pdf.build_pdf v0.63.2 (Entscheidung E3: eigener
/// Schreiber, keine Fremdbibliothek). A4, Helvetica und Helvetica-Bold mit WinAnsi-Breiten,
/// Ueberschriften, Absaetze, Listen, Fusszeile mit Seitenzahl. Gepackt wird mit ZLibStream von
/// .NET; die Bytes des Stroms weichen deshalb von zlib ab, der entpackte Inhalt und alle
/// anderen Objekte sind gleich (G34 Teil pdf). Zahlen wie "%.1f"/"%.2f" (exakter Binaerwert).
/// </summary>
public static class Pdf
{
    public const double SeiteBreite = 595.28;
    public const double SeiteHoehe = 841.89;
    public const int Rand = 56;
    public const int Fuss = 30;

    /// <summary>Zeichenbreiten (1/1000 em) fuer die Codes 32-255 (WinAnsi).</summary>
    private static readonly int[] Normal =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584, 761,
        556, 0, 222, 556, 333, 1000, 556, 556, 333, 1000, 667, 333, 1000, 0, 611, 0,
        0, 222, 222, 333, 333, 350, 556, 1000, 333, 1000, 500, 333, 944, 0, 500, 667,
        278, 333, 556, 556, 556, 556, 260, 556, 333, 737, 370, 556, 584, 333, 737, 333,
        400, 584, 333, 333, 333, 556, 537, 278, 333, 333, 365, 556, 834, 834, 834, 611,
        667, 667, 667, 667, 667, 667, 1000, 722, 667, 667, 667, 667, 278, 278, 278, 278,
        722, 722, 778, 778, 778, 778, 778, 584, 778, 722, 722, 722, 722, 667, 667, 611,
        556, 556, 556, 556, 556, 556, 889, 500, 556, 556, 556, 556, 278, 278, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 584, 611, 556, 556, 556, 556, 500, 556, 500,
    ];

    private static readonly int[] Fett =
    [
        278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333, 584, 584, 584, 611,
        975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556,
        333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611,
        611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584, 761,
        556, 0, 278, 556, 500, 1000, 556, 556, 333, 1000, 667, 333, 1000, 0, 611, 0,
        0, 278, 278, 500, 500, 350, 556, 1000, 333, 1000, 556, 333, 944, 0, 500, 667,
        278, 333, 556, 556, 556, 556, 280, 556, 333, 737, 370, 556, 584, 333, 737, 333,
        400, 584, 333, 333, 333, 611, 556, 278, 333, 333, 365, 556, 834, 834, 834, 611,
        722, 722, 722, 722, 722, 722, 1000, 722, 667, 667, 667, 667, 278, 278, 278, 278,
        722, 722, 778, 778, 778, 778, 778, 584, 778, 722, 722, 722, 722, 667, 667, 611,
        556, 556, 556, 556, 556, 556, 889, 556, 556, 556, 556, 556, 278, 278, 278, 278,
        611, 611, 611, 611, 611, 611, 611, 584, 611, 611, 611, 611, 611, 556, 611, 556,
    ];

    /// <summary>STYLES: Schrift fett?, Groesse, Abstand davor, danach, Einzug.</summary>
    private static (bool Fett, double Groesse, double Davor, double Danach, int Einzug) Stil(string art) => art switch
    {
        "h1" => (true, 18, 6, 10, 0),
        "h2" => (true, 13, 12, 6, 0),
        "h3" => (true, 11, 8, 3, 0),
        "p" => (false, 10.5, 0, 6, 0),
        "li" => (false, 10.5, 0, 3, 14),
        "small" => (false, 9, 0, 4, 0),
        _ => throw new ArgumentException("Unbekannte Art: " + art, nameof(art)),
    };

    /// <summary>text_width.</summary>
    public static double Breite(string text, bool fett, double groesse)
    {
        var breiten = fett ? Fett : Normal;
        long summe = 0;
        foreach (var code in PdfZeichen.Kodieren(text))
        {
            summe += code >= 32 && breiten[code - 32] != 0 ? breiten[code - 32] : 556;
        }
        return summe * groesse / 1000.0;
    }

    /// <summary>wrap: Absaetze an "\n", Woerter an " " (leere zaehlen mit), zu lange Woerter hart (in Codepunkten).</summary>
    public static List<string> Umbrechen(string text, bool fett, double groesse, double breite)
    {
        var zeilen = new List<string>();
        foreach (var absatz in text.Split('\n'))
        {
            var zeile = "";
            foreach (var w in absatz.Split(' '))
            {
                var wort = w;
                var kandidat = zeile.Length == 0 ? wort : zeile + " " + wort;
                if (Breite(kandidat, fett, groesse) <= breite)
                {
                    zeile = kandidat;
                    continue;
                }
                if (zeile.Length > 0)
                {
                    zeilen.Add(zeile);
                }
                while (Breite(wort, fett, groesse) > breite && PythonZeichen.Laenge(wort) > 1)
                {
                    var schnitt = PythonZeichen.Laenge(wort);
                    while (schnitt > 1 && Breite(PythonZeichen.Teil(wort, 0, schnitt), fett, groesse) > breite)
                    {
                        schnitt--;
                    }
                    zeilen.Add(PythonZeichen.Teil(wort, 0, schnitt));
                    wort = PythonZeichen.Teil(wort, schnitt);
                }
                zeile = wort;
            }
            zeilen.Add(zeile);
        }
        return zeilen;
    }

    private static byte[] Schuetzen(byte[] daten)
    {
        var ergebnis = new List<byte>(daten.Length + 8);
        foreach (var b in daten)
        {
            if (b is (byte)'\\' or (byte)'(' or (byte)')')
            {
                ergebnis.Add((byte)'\\');
            }
            ergebnis.Add(b);
        }
        return [.. ergebnis];
    }

    private static string F1(double x) => Abschlussprojekt.Fest(x, 1);

    private static string F2(double x) => Abschlussprojekt.Fest(x, 2);

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    private static byte[] Verbinden(params byte[][] teile) => [.. teile.SelectMany(t => t)];

    /// <summary>_info_string: "&lt;FEFF" + UTF-16-BE in Grossbuchstaben-Hex + "&gt;".</summary>
    private static string InfoText(string text) => "<FEFF" + Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text)) + ">";

    /// <summary>build_pdf(bloecke, titel, autor); tag = Datum im Fuss.</summary>
    public static byte[] Bauen(IReadOnlyList<ExportBlock> bloecke, string titel, string autor, DateTime tag)
    {
        var seiten = new List<List<byte[]>>();
        List<byte[]> befehle = [];
        double y = 0;
        var nutzbar = SeiteBreite - 2 * Rand;

        void NeueSeite()
        {
            befehle = [];
            seiten.Add(befehle);
            y = SeiteHoehe - Rand;
        }

        NeueSeite();
        foreach (var block in bloecke)
        {
            var (fett, groesse, davor, danach, einzug) = Stil(block.Art);
            var abstand = groesse * 1.35;
            var text = block.Text;
            var zeilen = text.Length > 0 ? Umbrechen(text, fett, groesse, nutzbar - einzug) : [""];
            y -= davor;
            // Ueberschrift nicht allein unten auf der Seite
            var bedarf = abstand * (block.Art.StartsWith('h') ? 2 : 1);
            for (var nummer = 0; nummer < zeilen.Count; nummer++)
            {
                if (y - bedarf < Rand + Fuss)
                {
                    NeueSeite();
                }
                bedarf = abstand;
                y -= abstand;
                var x = Rand + einzug;
                if (block.Art == "li" && nummer == 0)
                {
                    befehle.Add(Verbinden(Ascii("BT /F1 " + F1(groesse) + " Tf " + F2(Rand + 3) + " " + F2(y) + " Td ("),
                        Schuetzen(PdfZeichen.Kodieren("•")), Ascii(") Tj ET")));
                }
                befehle.Add(Verbinden(Ascii("BT /" + (fett ? "F2" : "F1") + " " + F1(groesse) + " Tf " + F2(x) + " " + F2(y) + " Td ("),
                    Schuetzen(PdfZeichen.Kodieren(zeilen[nummer])), Ascii(") Tj ET")));
            }
            y -= danach;
        }

        // Seitenzahlen und Fusszeile
        var anzahl = seiten.Count;
        var datum = tag.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        for (var n = 0; n < anzahl; n++)
        {
            var fuss = PythonZeichen.Strip(titel + "  ·  Seite " + (n + 1).ToString(CultureInfo.InvariantCulture) + " von "
                + anzahl.ToString(CultureInfo.InvariantCulture) + "  ·  " + datum, " ·");
            seiten[n].Add(Verbinden(Ascii("0.45 g BT /F1 8 Tf " + F2(Rand) + " " + F2(Rand - 20) + " Td ("),
                Schuetzen(PdfZeichen.Kodieren(fuss)), Ascii(") Tj ET 0 g")));
        }

        var objekte = new List<byte[]?> { null, null };
        int Neu(byte[] inhalt)
        {
            objekte.Add(inhalt);
            return objekte.Count;
        }

        const int katalog = 1, seitenbaum = 2;
        var schrift1 = Neu(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
        var schrift2 = Neu(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"));
        var kinder = new List<int>();
        foreach (var seite in seiten)
        {
            var strom = Packen(Verbinden([.. seite.SelectMany((b, i) => i == 0 ? new[] { b } : [Ascii("\n"), b])]));
            var inhalt = Neu(Verbinden(Ascii("<< /Length " + strom.Length.ToString(CultureInfo.InvariantCulture)
                + " /Filter /FlateDecode >>\nstream\n"), strom, Ascii("\nendstream")));
            kinder.Add(Neu(Ascii(string.Create(CultureInfo.InvariantCulture,
                $"<< /Type /Page /Parent {seitenbaum} 0 R /MediaBox [0 0 {F2(SeiteBreite)} {F2(SeiteHoehe)}] "
                + $"/Resources << /Font << /F1 {schrift1} 0 R /F2 {schrift2} 0 R >> >> /Contents {inhalt} 0 R >>"))));
        }
        objekte[katalog - 1] = Ascii(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {seitenbaum} 0 R >>"));
        objekte[seitenbaum - 1] = Ascii(string.Create(CultureInfo.InvariantCulture,
            $"<< /Type /Pages /Kids [{string.Join(" ", kinder.Select(k => k + " 0 R"))}] /Count {kinder.Count} >>"));
        var info = Neu(Ascii("<< /Title " + InfoText(titel) + " /Author " + InfoText(autor) + " /Producer (Fachinformatiker Lernplattform) >>"));

        using var aus = new MemoryStream();
        aus.Write(Verbinden(Ascii("%PDF-1.4\n%"), [0xE2, 0xE3, 0xCF, 0xD3], Ascii("\n")));
        var stellen = new List<long>();
        for (var n = 0; n < objekte.Count; n++)
        {
            stellen.Add(aus.Length);
            aus.Write(Ascii((n + 1).ToString(CultureInfo.InvariantCulture) + " 0 obj\n"));
            aus.Write(objekte[n]!);
            aus.Write(Ascii("\nendobj\n"));
        }
        var xref = aus.Length;
        var ende = new StringBuilder();
        ende.Append(CultureInfo.InvariantCulture, $"xref\n0 {objekte.Count + 1}\n0000000000 65535 f \n");
        foreach (var stelle in stellen)
        {
            ende.Append(stelle.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }
        ende.Append(CultureInfo.InvariantCulture,
            $"trailer\n<< /Size {objekte.Count + 1} /Root {katalog} 0 R /Info {info} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        aus.Write(Ascii(ende.ToString()));
        return aus.ToArray();
    }

    /// <summary>zlib-Format (wie zlib.compress), gepackt von .NET.</summary>
    private static byte[] Packen(byte[] daten)
    {
        using var ziel = new MemoryStream();
        using (var zlib = new ZLibStream(ziel, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(daten);
        }
        return ziel.ToArray();
    }
}
