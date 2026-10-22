using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Lernplattform.Core.Daten;

/// <summary>
/// Phase 2, L5: Teile des Dialogs "Lernstand übernehmen" ohne Oberflaeche: Quelle am Inhalt
/// erkennen, gewaehlte Datei lesen (auch ohne Dateipfad, z. B. Android) und die Zeilen der
/// Vorschau einer Sicherung (fisi_sicherung.backup_summary).
/// </summary>
public static class UebernahmeAblauf
{
    private static readonly byte[] SqliteKopf = "SQLite format 3\0"u8.ToArray();

    /// <summary>Name der Arbeitskopie einer alten Datenbank ohne Dateipfad (Datenordner, wird sofort geloescht).</summary>
    public const string ArbeitsKopie = "uebernahme_quelle.tmp";

    /// <summary>
    /// Quelle am Inhalt erkennen, nicht an der Endung: SQLite-Kopf = alte Datenbank, gzip mit
    /// JSON-Objekt und Feld "art" = Sicherung, gzip mit JSON-Objekt ohne "art" = Abgleich-Datei.
    /// Alles andere (fremd, nicht entpackbar, kein Objekt): null (KeineSicherung).
    /// </summary>
    public static UebernahmeQuelle? Erkennen(byte[] inhalt)
    {
        if (inhalt.AsSpan().StartsWith(SqliteKopf))
        {
            return UebernahmeQuelle.AlteDatenbank;
        }
        if (inhalt.Length < 2 || inhalt[0] != 0x1F || inhalt[1] != 0x8B)
        {
            return null;
        }
        try
        {
            using var dokument = JsonDocument.Parse(Entpacken(inhalt));
            if (dokument.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            return dokument.RootElement.TryGetProperty("art", out _) ? UebernahmeQuelle.Sicherung : UebernahmeQuelle.AbgleichDatei;
        }
        catch (Exception e) when (e is InvalidDataException or JsonException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Gewaehlte Datei lesen und pruefen. lokalerPfad: Pfad, wenn das System einen liefert
    /// (dann prueft die Uebernahme auch "eigene Datei gewaehlt"). Eine alte Datenbank ohne
    /// Pfad wird kurz als Datei mit Rechten nur fuer den Benutzer in den Datenordner kopiert,
    /// gelesen und sofort wieder geloescht. Schreibt nichts in den Lernstand.
    /// </summary>
    public static Austausch Lesen(byte[] inhalt, string? lokalerPfad, string arbeitsordner)
    {
        var quelle = Erkennen(inhalt) ?? throw new UebernahmeAusnahme(UebernahmeFehler.KeineSicherung);
        if (quelle == UebernahmeQuelle.AlteDatenbank)
        {
            if (lokalerPfad is not null && File.Exists(lokalerPfad))
            {
                return Uebernahme.LeseAlteDatenbank(lokalerPfad).MitPfad(lokalerPfad);
            }
            var kopie = Path.Combine(arbeitsordner, ArbeitsKopie);
            try
            {
                Directory.CreateDirectory(arbeitsordner);
                var optionen = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
                if (!OperatingSystem.IsWindows())
                {
                    optionen.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                }
                using (var datei = new FileStream(kopie, optionen))
                {
                    datei.Write(inhalt);
                }
                return Uebernahme.LeseAlteDatenbank(kopie);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new UebernahmeAusnahme(UebernahmeFehler.DatenbankNichtLesbar);
            }
            finally
            {
                foreach (var rest in new[] { kopie, kopie + "-journal", kopie + "-wal", kopie + "-shm" })
                {
                    try
                    {
                        File.Delete(rest);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                    }
                }
            }
        }
        var daten = quelle == UebernahmeQuelle.Sicherung ? Uebernahme.LeseSicherung(inhalt) : Uebernahme.LeseAbgleichDatei(inhalt);
        return lokalerPfad is null ? daten : daten.MitPfad(lokalerPfad);
    }

    /// <summary>Reste einer abgebrochenen Arbeitskopie entfernen (beim Oeffnen des Dialogs).</summary>
    public static void Aufraeumen(string arbeitsordner)
    {
        var kopie = Path.Combine(arbeitsordner, ArbeitsKopie);
        foreach (var rest in new[] { kopie, kopie + "-journal", kopie + "-wal", kopie + "-shm" })
        {
            try
            {
                File.Delete(rest);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static byte[] Entpacken(byte[] roh)
    {
        using var eingabe = new MemoryStream(roh);
        using var gz = new GZipStream(eingabe, CompressionMode.Decompress);
        using var ausgabe = new MemoryStream();
        gz.CopyTo(ausgabe);
        return ausgabe.ToArray();
    }

    /// <summary>
    /// backup_summary (fisi_sicherung 387-403): Zeilen der Vorschau einer gueltigen Sicherung:
    /// Erstellt, App-Version, Geraet (nur wenn gesetzt), Lern-Eintraege, Spielstaende, Abzeichen, Abschlussprojekte.
    /// </summary>
    public static IReadOnlyList<string> SicherungsZeilen(byte[] roh)
    {
        using var dokument = JsonDocument.Parse(Entpacken(roh));
        var wurzel = dokument.RootElement;
        var daten = Feld(wurzel, "daten") is { ValueKind: JsonValueKind.Object } d ? d : default;
        var zeilen = new List<string>
        {
            "Erstellt: " + Erstellt(Feld(wurzel, "erstellt")),
            "App-Version: " + (Wahr(Feld(wurzel, "app_version")) ? PyStr(Feld(wurzel, "app_version")!.Value) : "?"),
        };
        if (Wahr(Feld(wurzel, "geraet")))
        {
            zeilen.Add("Gerät: " + PyStr(Feld(wurzel, "geraet")!.Value));
        }
        var (eintraege, spielstaende, abzeichen, projekte) = Zaehlen(daten);
        zeilen.Add("Lern-Einträge: " + eintraege.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.'));
        zeilen.Add("Spielstände: " + spielstaende.ToString(CultureInfo.InvariantCulture));
        zeilen.Add("Abzeichen: " + abzeichen.ToString(CultureInfo.InvariantCulture));
        zeilen.Add("Abschlussprojekte: " + projekte.ToString(CultureInfo.InvariantCulture));
        return zeilen;
    }

    private static JsonElement? Feld(JsonElement objekt, string name) =>
        objekt.ValueKind == JsonValueKind.Object && objekt.TryGetProperty(name, out var wert) ? wert : null;

    // bool(wert) in Python fuer JSON-Werte
    private static bool Wahr(JsonElement? wert) => wert is { } w && w.ValueKind switch
    {
        JsonValueKind.String => w.GetString()!.Length > 0,
        JsonValueKind.Number => w.GetDouble() != 0,
        JsonValueKind.True => true,
        JsonValueKind.Array => w.GetArrayLength() > 0,
        JsonValueKind.Object => w.EnumerateObject().Any(),
        _ => false,
    };

    // str(wert) fuer die Werte, die in einer gueltigen Sicherung vorkommen
    private static string PyStr(JsonElement wert) => wert.ValueKind switch
    {
        JsonValueKind.String => wert.GetString()!,
        JsonValueKind.True => "True",
        JsonValueKind.False => "False",
        JsonValueKind.Null => "None",
        _ => wert.GetRawText(),
    };

    // _german_created: datetime.fromisoformat, sonst str(text or "?")
    private static string Erstellt(JsonElement? wert)
    {
        var text = Wahr(wert) ? PyStr(wert!.Value) : (wert is { } w && w.ValueKind == JsonValueKind.String ? "" : "None");
        string[] formen =
        [
            "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss.FFFFFFF", "yyyy-MM-dd HH:mm:ss.FFFFFFF",
            "yyyy-MM-ddTHH:mm", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
            "yyyy-MM-ddTHH:mm:sszzz", "yyyy-MM-dd HH:mm:sszzz", "yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz", "yyyy-MM-dd HH:mm:ss.FFFFFFFzzz",
        ];
        if (DateTimeOffset.TryParseExact(text, formen, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var zeit))
        {
            // Ortszeit der Datei wie Python (kein Umrechnen)
            return zeit.ToString("dd.MM.yyyy, HH:mm", CultureInfo.InvariantCulture) + " Uhr";
        }
        return Wahr(wert) ? text : "?";
    }

    private static readonly string[] Ereignisse =
        ["test_results", "card_events", "quiz_answers", "scenario_events", "project_events", "ap1_events", "trainer_aufgaben", "pruefungen"];

    // _summary_counts (fisi_sicherung 261-283)
    private static (long Eintraege, int Spielstaende, int Abzeichen, int Projekte) Zaehlen(JsonElement daten)
    {
        var tabellen = Feld(daten, "tables") is { ValueKind: JsonValueKind.Object } t ? t : default;
        long eintraege = 0;
        foreach (var name in Ereignisse)
        {
            if (Feld(tabellen, name) is { ValueKind: JsonValueKind.Array } liste)
            {
                eintraege += liste.GetArrayLength();
            }
        }
        var plaetze = Zeilen(daten, "spiel_plaetze");
        var geloescht = plaetze.Where(r => Text(r, "aktion") == "geloescht").Select(r => Schluessel(r, "lauf")).ToHashSet();
        var laeufe = plaetze.Where(r => Text(r, "aktion") == "angelegt" && Wahr(r.GetValueOrDefault("lauf")))
            .Select(r => Schluessel(r, "lauf")).ToHashSet();
        foreach (var r in Zeilen(daten, "spiel_ereignisse"))
        {
            laeufe.Add(Wahr(r.GetValueOrDefault("lauf")) ? Schluessel(r, "lauf") : "s:alt");
        }
        var abzeichen = Zeilen(daten, "spiel_bestenliste").Count(r => Text(r, "art") == "erfolg");
        var marken = new Dictionary<string, ((string, string) Schluessel, string Wert)>(StringComparer.Ordinal);
        var projekte = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in Zeilen(daten, "abschlussprojekt"))
        {
            if (!Wahr(r.GetValueOrDefault("projekt")))
            {
                continue;
            }
            var projekt = Schluessel(r, "projekt");
            projekte.Add(projekt);
            if (Text(r, "feld") == "_geloescht")
            {
                var schluessel = (PyStr(r["timestamp"]!.Value), PyStr(r["uid"]!.Value));
                if (!marken.TryGetValue(projekt, out var bisher)
                    || string.CompareOrdinal(schluessel.Item1, bisher.Schluessel.Item1) is var v && (v > 0 || (v == 0 && string.CompareOrdinal(schluessel.Item2, bisher.Schluessel.Item2) > 0)))
                {
                    marken[projekt] = (schluessel, r.GetValueOrDefault("wert") is { } wert ? PyStr(wert) : "None");
                }
            }
        }
        projekte.ExceptWith(marken.Where(m => m.Value.Wert == "1").Select(m => m.Key));
        projekte.Remove("s:" + Pruefung.Ausbildung.Zeile);   // Fachrichtung ist kein Projekt (P4-5, Python zaehlt sie mit)
        laeufe.ExceptWith(geloescht);
        return (eintraege, laeufe.Count, abzeichen, projekte.Count);
    }

    private static string? Text(Dictionary<string, JsonElement?> zeile, string spalte) =>
        zeile.GetValueOrDefault(spalte) is { ValueKind: JsonValueKind.String } w ? w.GetString() : null;

    // Schluessel fuer Mengen: Typ und Wert (Python vergleicht "1" und 1 verschieden)
    private static string Schluessel(Dictionary<string, JsonElement?> zeile, string spalte) =>
        zeile.GetValueOrDefault(spalte) is { } w ? (w.ValueKind == JsonValueKind.String ? "s:" + w.GetString() : "j:" + w.GetRawText()) : "j:null";

    // _records: Zeilen als Woerterbuch (mit uid), nur vollstaendige Zeilen
    private static List<Dictionary<string, JsonElement?>> Zeilen(JsonElement daten, string tabelle)
    {
        var spalten = Uebernahme.Tabellen.First(t => t.Tabelle == tabelle).Spalten;
        var optional = Uebernahme.Optional.GetValueOrDefault(tabelle, []);
        var namen = new List<string> { "uid" };
        namen.AddRange(Feld(Feld(daten, "columns") ?? default, tabelle) is { ValueKind: JsonValueKind.Array } eigene && eigene.GetArrayLength() > 0
            ? eigene.EnumerateArray().Select(PyStr)
            : spalten);
        var ergebnis = new List<Dictionary<string, JsonElement?>>();
        if (Feld(Feld(daten, "tables") ?? default, tabelle) is not { ValueKind: JsonValueKind.Array } zeilen)
        {
            return ergebnis;
        }
        foreach (var zeile in zeilen.EnumerateArray())
        {
            var werte = new Dictionary<string, JsonElement?>(StringComparer.Ordinal);
            if (zeile.ValueKind == JsonValueKind.Array)
            {
                foreach (var (name, wert) in namen.Zip(zeile.EnumerateArray()))
                {
                    werte[name] = wert;
                }
            }
            foreach (var spalte in optional)
            {
                werte.TryAdd(spalte, null);
            }
            if (Wahr(werte.GetValueOrDefault("uid")) && spalten.All(werte.ContainsKey))
            {
                ergebnis.Add(werte);
            }
        }
        return ergebnis;
    }
}
