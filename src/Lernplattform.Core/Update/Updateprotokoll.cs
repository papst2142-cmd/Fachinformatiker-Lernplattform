using System.Globalization;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;

namespace Lernplattform.Core.Update;

/// <summary>
/// Phase 5 BE9: update.log im Datenordner wie write_update_log (fisi_update.py): jede Zeile
/// "JJJJ-MM-TT HH:MM:SS | Programm | Text", Benutzerpfade ersetzt (anonymize_paths). Ueber 64 KB wird
/// die Datei vor dem Schreiben auf die letzten 32 768 Zeichen ab der naechsten ganzen Zeile gekuerzt.
/// Gelesen wird ueber <see cref="Problembericht.UpdateLogLesen"/> (16 KB in den Bericht).
/// </summary>
public static class Updateprotokoll
{
    public const int Hoechstgroesse = 64 * 1024;

    private static readonly object Sperre = new();

    public static string Pfad(string? ordner = null) => Path.Combine(ordner ?? Ablage.Datenordner(), Problembericht.UpdateName);

    /// <summary>Haengt eine Zeile an. Darf nie scheitern. jetzt nur fuer Tests (Vorgabe: Ortszeit).</summary>
    public static void Schreiben(string text, string? ordner = null, DateTime? jetzt = null)
    {
        try
        {
            var pfad = Pfad(ordner);
            lock (Sperre)
            {
                if (File.Exists(pfad) && new FileInfo(pfad).Length > Hoechstgroesse)
                {
                    var rest = LetzteZeichen(Textmodus(File.ReadAllBytes(pfad)), Hoechstgroesse / 2);
                    rest = rest[(rest.IndexOf('\n', StringComparison.Ordinal) + 1)..];
                    File.WriteAllText(pfad, Zeilenenden(rest), new UTF8Encoding(false));
                }
                var zeit = (jetzt ?? DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                File.AppendAllText(pfad, Zeilenenden(zeit + " | Programm | " + Datenschutz.PfadeAnonymisieren(text) + "\n"), new UTF8Encoding(false));
            }
        }
        catch (Exception)
        {
            // wie Python: kein Schreibrecht, volles Laufwerk ... aendern am Update nichts
        }
    }

    /// <summary>Lesen wie open(encoding="utf-8", errors="replace") im Textmodus: \r\n und \r werden \n.</summary>
    internal static string Textmodus(byte[] roh) =>
        new UTF8Encoding(false, false).GetString(roh).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    /// <summary>Schreiben im Textmodus wie Python: unter Windows \r\n.</summary>
    private static string Zeilenenden(string text) => text.Replace("\n", Environment.NewLine, StringComparison.Ordinal);

    /// <summary>text[-anzahl:] wie Python (Zeichen = Unicode-Codepunkte, nicht UTF-16-Einheiten).</summary>
    internal static string LetzteZeichen(string text, int anzahl)
    {
        var punkte = 0;
        for (var i = text.Length; i > 0;)
        {
            i -= i >= 2 && char.IsSurrogatePair(text[i - 2], text[i - 1]) ? 2 : 1;
            if (++punkte == anzahl)
            {
                return text[i..];
            }
        }
        return text;
    }
}
