using System.Globalization;
using System.Text;
using Lernplattform.Core.Inhalte;

namespace Lernplattform.Core.Diagnose;

/// <summary>
/// Phase 11 W3 (Weisung P11-21, Plan Phase 11 Abschnitt 3 "W3 Pruefstand"): Messpunkte, die das Programm selbst
/// aufschreibt, damit Nico sie beim Geraetetest ueber "Problem melden" mitschicken kann. Ohne Vorlage in Python.
/// Je Programmstart hoechstens einmal je Messpunkt (kalt, wie die strengen Messpunkte des Start-Tests):
/// Inhalte laden, erste Karte (Karteikarten), Fachrichtung wechseln (Nachladen bis die Sicht steht) und Spiel
/// betreten (bis zum ersten 3D-Bild). Die Datei <see cref="DateiName"/> im Datenordner haelt nur feste Namen,
/// Version, Zeitpunkt (Minuten) und Millisekunden, nie Inhalte des Lernstands (Regel A3); die Fachrichtung steht nur
/// als Kennung aus <see cref="Fachrichtungen.Werte"/>. Ueber <see cref="DateiMax"/> fallen die aeltesten Zeilen weg.
/// Schreiben darf nie scheitern und nie das Programm aufhalten.
/// </summary>
public static class Messpunkte
{
    public const string DateiName = "messpunkte.log";
    public const int DateiMax = 8 * 1024;

    /// <summary>Die vier Messpunkte (feste Namen; nichts anderes kommt in die Datei).</summary>
    public enum Punkt
    {
        InhalteLaden,
        ErsteKarte,
        FachrichtungWechseln,
        SpielBetreten,
    }

    /// <summary>Anzeigename je Messpunkt (Zeile in der Datei und im Problembericht).</summary>
    public static string Name(Punkt punkt) => punkt switch
    {
        Punkt.InhalteLaden => "Inhalte laden",
        Punkt.ErsteKarte => "Erste Karte",
        Punkt.FachrichtungWechseln => "Fachrichtung wechseln",
        Punkt.SpielBetreten => "Spiel betreten",
        _ => throw new ArgumentOutOfRangeException(nameof(punkt)),
    };

    private static readonly object Sperre = new();
    private static readonly HashSet<string> Geschrieben = new(StringComparer.Ordinal);

    /// <summary>
    /// Eine Zeile, z. B. "03.11.2026 12:40 · A 0.34.0 · Fachrichtung wechseln ae: 312 ms". Eine unbekannte
    /// Fachrichtung steht als "?", ein ungueltiger Wert (negativ, NaN, unendlich) als "?" ms.
    /// </summary>
    public static string Zeile(DateTime jetzt, string version, Punkt punkt, double ms, string? fachrichtung = null)
    {
        var inv = CultureInfo.InvariantCulture;
        var name = Name(punkt);
        if (punkt == Punkt.FachrichtungWechseln)
        {
            name += " " + (fachrichtung is not null && Fachrichtungen.Werte.Contains(fachrichtung) ? fachrichtung : "?");
        }
        var wert = double.IsFinite(ms) && ms >= 0 ? ms.ToString("F0", inv) : "?";
        return jetzt.ToString("dd.MM.yyyy HH:mm", inv) + " · " + VersionText(version) + " · " + name + ": " + wert + " ms";
    }

    // nur Zeichen einer Versionsanzeige (A 0.34.0), damit nie etwas anderes in die Datei kommt
    private static string VersionText(string version)
    {
        var text = new string(version.Where(z => char.IsAsciiLetterOrDigit(z) || z is '.' or ' ' or '-').Take(32).ToArray()).Trim();
        return text.Length > 0 ? text : "?";
    }

    /// <summary>
    /// Haengt die Zeile an <see cref="DateiName"/> im Ordner an, je Programmstart (Prozess) nur einmal je Messpunkt
    /// (bei Fachrichtung wechseln einmal je Fachrichtung). Rueckgabe true = geschrieben. Fehler werden geschluckt.
    /// </summary>
    public static bool Notieren(string ordner, DateTime jetzt, string version, Punkt punkt, double ms, string? fachrichtung = null)
    {
        var schluessel = punkt == Punkt.FachrichtungWechseln ? Name(punkt) + " " + fachrichtung : Name(punkt);
        lock (Sperre)
        {
            if (!Geschrieben.Add(schluessel))
            {
                return false;
            }
            try
            {
                Anhaengen(Path.Combine(ordner, DateiName), Zeile(jetzt, version, punkt, ms, fachrichtung));
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return false;
            }
        }
    }

    /// <summary>Nur Tests: vergessen, welche Messpunkte in diesem Prozess schon geschrieben sind.</summary>
    public static void VergessenFuerTests()
    {
        lock (Sperre)
        {
            Geschrieben.Clear();
        }
    }

    /// <summary>Haengt eine Zeile an; ueber <see cref="DateiMax"/> Bytes fallen die aeltesten ganzen Zeilen weg.</summary>
    public static void Anhaengen(string pfad, string zeile)
    {
        var utf8 = new UTF8Encoding(false);
        string bisher;
        try
        {
            // Windows: lesen mit FileShare.ReadWrite | Delete (die Datei kann gerade ein Problembericht lesen)
            using var lesen = new FileStream(pfad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var leser = new StreamReader(lesen, utf8);
            bisher = leser.ReadToEnd();
        }
        catch (FileNotFoundException)
        {
            bisher = "";
        }
        catch (DirectoryNotFoundException)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pfad))!);
            bisher = "";
        }
        var zeilen = bisher.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(z => z.TrimEnd('\r')).ToList();
        zeilen.Add(zeile);
        while (zeilen.Count > 1 && utf8.GetByteCount(string.Join("\n", zeilen) + "\n") > DateiMax)
        {
            zeilen.RemoveAt(0);
        }
        File.WriteAllText(pfad, string.Join("\n", zeilen) + "\n", utf8);
    }
}
