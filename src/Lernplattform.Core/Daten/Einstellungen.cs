using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Design;

namespace Lernplattform.Core.Daten;

/// <summary>
/// Einstellungen dieses Geraets in "einstellungen.json" im Datenordner, wie in
/// der Python-Linie (fisi_update.load_settings/save_settings):
/// <list type="bullet">
/// <item>Gleiche Schluessel und Vorgabewerte wie Python (Vergleichswerte G11).</item>
/// <item>Unbekannte Schluessel (z. B. von einem spaeteren Programmstand) bleiben beim Speichern erhalten.</item>
/// <item>Atomar speichern: erst Hilfsdatei ".tmp", dann ersetzen; unter Linux/macOS nur fuer das eigene Konto lesbar (0600).</item>
/// <item>Eine beschaedigte Datei wird vor dem naechsten Speichern als "einstellungen.defekt.json" aufgehoben und einmal gemeldet.</item>
/// <item>Abweichung von Python (Weisung P1-2, 3.1): fehlt die Datei, gelten die Vorgaben und Speichern legt sie an.
/// Kann eine vorhandene Datei nicht gelesen werden (gesperrt, kein Zugriff), gelten die Vorgaben nur fuer die
/// Anzeige; Speichern und Setze schreiben dann nicht (Rueckgabe false), die Datei bleibt unveraendert.</item>
/// </list>
/// </summary>
public sealed class Einstellungen
{
    public const string DateiName = "einstellungen.json";
    public const string DefektName = "einstellungen.defekt.json";

    public const string DefektTitel = "Einstellungen ersetzt";
    public const string DefektText = "Die Einstellungsdatei war beschädigt und wurde durch die "
        + "Grundeinstellungen ersetzt. Die alte Datei ist als "
        + "einstellungen.defekt.json im Datenordner aufgehoben. Falls du "
        + "den Abgleich eingerichtet hattest, richte ihn bitte neu ein.";

    // Schluessel wie in Python (fisi_theme, fisi_lernen, fisi_update)
    public const string DarstellungSchluessel = "darstellung";
    public const string GrundfarbeSchluessel = "grundfarbe";
    public const string HintergrundSchluessel = "hintergrund";
    public const string SchriftSchluessel = "schriftgroesse";
    public const string AutoCheckSchluessel = "auto_check";

    public static readonly IReadOnlyList<string> Darstellungen = [Farbwelt.Dunkel, Farbwelt.Hell];
    public static readonly IReadOnlyList<string> Schriftgroessen = ["normal", "gross", "sehr_gross", "extra_gross", "riesig"];
    public const string SchriftNormal = "normal";

    // Tagesziel (fisi_lernen.GOAL_SETTINGS)
    public const int ZielVorgabe = 20;
    public const int ZielMin = 5;
    public const int ZielMax = 100;
    public const string ErinnerungVorgabe = "18:00";

    private readonly string _pfad;
    private bool _defektGesehen;

    // Phase 5 W2 (Risiko 5, Sammelliste 55): Lesen-Aendern-Schreiben nie gleichzeitig (Oberflaeche,
    // Abgleich im Hintergrund, Schalter der Leistungsmessung), sonst ginge eine Aenderung verloren.
    private static readonly object Schreibsperre = new();

    public Einstellungen(string? ordner = null)
    {
        _pfad = Path.Combine(ordner ?? Ablage.Datenordner(), DateiName);
    }

    public string Pfad => _pfad;

    /// <summary>
    /// true, wenn das letzte Laden die vorhandene Datei nicht lesen konnte (gesperrt, kein Zugriff).
    /// Die Werte gelten dann nur fuer die Anzeige, gespeichert wird nicht.
    /// </summary>
    public bool NurAnzeige { get; private set; }

    /// <summary>Die gespeicherten Einstellungen mit Vorgabewerten (wie load_settings).</summary>
    public JsonObject Laden()
    {
        var werte = new JsonObject { [AutoCheckSchluessel] = true };
        var (roh, gesperrt) = Lesen();
        NurAnzeige = gesperrt;
        if (roh is null)
        {
            return werte;
        }
        try
        {
            if (JsonNode.Parse(new UTF8Encoding(false, true).GetString(roh)) is not JsonObject gelesen)
            {
                throw new JsonException("einstellungen.json enthaelt kein Objekt");
            }
            foreach (var (schluessel, wert) in gelesen.ToList())
            {
                gelesen.Remove(schluessel);
                werte[schluessel] = wert;
            }
        }
        catch (Exception e) when (e is JsonException or DecoderFallbackException or ArgumentException)
        {
            try
            {
                File.Copy(_pfad, Path.Combine(Path.GetDirectoryName(_pfad)!, DefektName), true);
                _defektGesehen = true;
            }
            catch (Exception f) when (f is IOException or UnauthorizedAccessException)
            {
            }
        }
        return werte;
    }

    /// <summary>
    /// Atomar speichern (wie save_settings). Rueckgabe false, wenn es nicht ging;
    /// die bisherige Datei bleibt dann unveraendert.
    /// </summary>
    public bool Speichern(JsonObject werte)
    {
        if (Lesen().Gesperrt)
        {
            // vorhandene Datei nicht lesbar: nicht ueberschreiben (Weisung P1-2, 3.1)
            NurAnzeige = true;
            return false;
        }
        var temp = _pfad + ".tmp";
        try
        {
            Schreiben(temp, werte);
            File.Move(temp, _pfad, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception f) when (f is IOException or UnauthorizedAccessException)
            {
            }
            return false;
        }
    }

    /// <summary>
    /// Werte nach &lt;datei&gt; schreiben (eingerueckt, UTF-8 ohne BOM), unter Linux/macOS nur fuer das
    /// eigene Konto lesbar (0600, auch wenn die Datei schon da war), auf die Platte bringen.
    /// Wirft IOException bzw. UnauthorizedAccessException.
    /// </summary>
    private void Schreiben(string datei, JsonObject werte)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_pfad)!);
        var optionen = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };
        if (!OperatingSystem.IsWindows())
        {
            optionen.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }
        using var strom = new FileStream(datei, optionen);
        if (!OperatingSystem.IsWindows())
        {
            // falls die Datei schon da war (dann gilt UnixCreateMode nicht)
            File.SetUnixFileMode(strom.SafeFileHandle, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        var text = werte.ToJsonString(new JsonSerializerOptions { WriteIndented = true, IndentSize = 2 });
        strom.Write(new UTF8Encoding(false).GetBytes(text));
        strom.Flush(true);
    }

    // -- Phase 5 BE2: Hilfsdatei fuer "Alles ersetzen" (fisi_sicherung _settings_temp, remove_stale_temp)

    /// <summary>SETTINGS_TEMP_SUFFIX: Hilfsdatei neben einstellungen.json.</summary>
    public const string HilfsdateiEndung = ".sicherung.tmp";

    public string Hilfsdatei => _pfad + HilfsdateiEndung;

    /// <summary>
    /// remove_stale_temp: Hilfsdatei eines abgebrochenen "Alles ersetzen" loeschen (beim Start und vor
    /// dem naechsten Schreiben). Sie enthaelt den Zugangsschluessel; nachgeholt wird sie nie.
    /// </summary>
    public void HilfsdateiEntfernen()
    {
        try
        {
            File.Delete(Hilfsdatei);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// _settings_temp: alte Hilfsdatei loeschen, Werte in die Hilfsdatei schreiben (0600, auf der Platte).
    /// Ist die vorhandene einstellungen.json nicht lesbar, wird nichts geschrieben (Rueckgabe false,
    /// wie <see cref="Speichern"/>). Scheitert das Schreiben: Hilfsdatei entfernt, Ausnahme weiter.
    /// </summary>
    public bool HilfsdateiSchreiben(JsonObject werte)
    {
        HilfsdateiEntfernen();
        if (Lesen().Gesperrt)
        {
            NurAnzeige = true;
            return false;
        }
        try
        {
            Schreiben(Hilfsdatei, werte);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            HilfsdateiEntfernen();
            throw;
        }
    }

    /// <summary>os.replace(Hilfsdatei, einstellungen.json); scheitert es, Hilfsdatei entfernt, Rueckgabe false.</summary>
    public bool HilfsdateiEinsetzen()
    {
        try
        {
            File.Move(Hilfsdatei, _pfad, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            HilfsdateiEntfernen();
            return false;
        }
    }

    /// <summary>
    /// Inhalt der Datei. Fehlt sie (Datei oder Ordner), ist das kein Fehler: (null, false).
    /// Jeder andere Lesefehler ergibt (null, true).
    /// </summary>
    private (byte[]? Roh, bool Gesperrt) Lesen()
    {
        try
        {
            return (File.ReadAllBytes(_pfad), false);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return (null, false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (null, true);
        }
    }

    /// <summary>
    /// Beim Start: (Titel, Text), wenn einstellungen.json kaputt war, sonst null.
    /// Die Datei wird dann durch die Grundeinstellungen ersetzt (das Original
    /// liegt schon als einstellungen.defekt.json daneben); die Meldung kommt genau einmal.
    /// </summary>
    public (string Titel, string Text)? DefektHinweis()
    {
        Laden();
        if (!_defektGesehen)
        {
            return null;
        }
        _defektGesehen = false;
        lock (Schreibsperre)
        {
            Speichern(Laden());
        }
        _defektGesehen = false;
        return (DefektTitel, DefektText);
    }

    /// <summary>Einen Wert setzen und sofort speichern (alle anderen Schluessel bleiben).</summary>
    public bool Setze(string schluessel, JsonNode? wert)
    {
        lock (Schreibsperre)
        {
            var werte = Laden();
            werte[schluessel] = wert;
            return Speichern(werte);
        }
    }

    /// <summary>
    /// Mehrere Werte in einem Schritt aendern (Laden, Aendern, Speichern unter derselben Sperre wie
    /// <see cref="Setze"/>). false: nicht lesbar (NurAnzeige) oder nicht gespeichert, Datei unveraendert.
    /// </summary>
    public bool Aendern(Action<JsonObject> aenderung)
    {
        lock (Schreibsperre)
        {
            var werte = Laden();
            if (NurAnzeige)
            {
                return false;
            }
            aenderung(werte);
            return Speichern(werte);
        }
    }

    // -- Gelesene Werte mit Vorgaben und Pruefung wie Python ---------------

    public static string Darstellung(JsonObject werte) => Auswahl(werte, DarstellungSchluessel, Darstellungen, Farbwelt.Dunkel);

    public static string Grundfarbe(JsonObject werte) =>
        Auswahl(werte, GrundfarbeSchluessel, Farbwelt.Grundfarben.Select(g => g.Id).ToList(), Farbwelt.StandardGrundfarbe);

    public static string Hintergrund(JsonObject werte) =>
        Auswahl(werte, HintergrundSchluessel, Farbwelt.Hintergruende.Select(h => h.Id).ToList(), Farbwelt.StandardHintergrund);

    public static string Schriftgroesse(JsonObject werte) => Auswahl(werte, SchriftSchluessel, Schriftgroessen, SchriftNormal);

    private static string Auswahl(JsonObject werte, string schluessel, IReadOnlyList<string> erlaubt, string vorgabe) =>
        werte[schluessel] is JsonValue v && v.TryGetValue<string>(out var text) && erlaubt.Contains(text) ? text : vorgabe;

    public sealed record Tagesziel(bool ZielAn, int ZielAnzahl, bool SerieAn, bool ErinnerungAn, string ErinnerungZeit);

    /// <summary>Tagesziel mit Vorgaben und gueltigen Grenzen (fisi_lernen.goal_settings).</summary>
    public static Tagesziel Ziel(JsonObject werte)
    {
        JsonNode? Wert(string schluessel, JsonNode vorgabe) => werte.ContainsKey(schluessel) ? werte[schluessel] : vorgabe;
        var anzahl = (int)Math.Max(ZielMin, Math.Min(ZielMax, PythonInt(Wert("ziel_anzahl", ZielVorgabe)) ?? ZielVorgabe));
        var zeit = Uhrzeit(PythonStr(Wert("erinnerung_zeit", ErinnerungVorgabe))) ?? ErinnerungVorgabe;
        return new Tagesziel(PythonBool(Wert("ziel_an", true)), anzahl, PythonBool(Wert("serie_an", true)),
            PythonBool(Wert("erinnerung_an", true)), zeit);
    }

    /// <summary>"HH:MM" oder null bei ungueltiger Eingabe (fisi_lernen.parse_time).</summary>
    public static string? Uhrzeit(string text)
    {
        var teile = text.Trim().Split(':');
        if (teile.Length != 2 || PythonIntText(teile[0]) is not { } stunden || PythonIntText(teile[1]) is not { } minuten)
        {
            return null;
        }
        if (stunden is < 0 or > 23 || minuten is < 0 or > 59)
        {
            return null;
        }
        return string.Create(CultureInfo.InvariantCulture, $"{stunden:00}:{minuten:00}");
    }

    // -- Nachbildung der Python-Umwandlungen (int(), bool(), str()) --------

    /// <summary>
    /// Wie Pythons int(x): Zahl, Wahrheitswert oder Ganzzahl-Text; sonst null.
    /// Sehr grosse Werte werden auf den long-Bereich begrenzt; fuer die
    /// nachfolgende Begrenzung (Tagesziel 5 bis 100) ergibt das dasselbe wie Python.
    /// </summary>
    internal static long? PythonInt(JsonNode? wert)
    {
        if (wert is not JsonValue v)
        {
            return null;
        }
        switch (v.GetValueKind())
        {
            case JsonValueKind.True:
                return 1;
            case JsonValueKind.False:
                return 0;
            case JsonValueKind.Number:
                var zahl = Math.Truncate(Zahl(v));
                return double.IsNaN(zahl) ? null : zahl >= long.MaxValue ? long.MaxValue : zahl <= long.MinValue ? long.MinValue : (long)zahl;
            case JsonValueKind.String:
                return PythonIntText(v.GetValue<string>());
            default:
                return null;
        }
    }

    /// <summary>
    /// Wie Pythons int(text): Leerraum aussen, Vorzeichen, Dezimalziffern (auch
    /// Unicode-Ziffern wie "２０"), einzelne _ zwischen Ziffern erlaubt.
    /// Sehr grosse Werte werden auf den long-Bereich begrenzt.
    /// </summary>
    internal static long? PythonIntText(string text)
    {
        text = text.Trim();
        var vorzeichen = 1;
        if (text.StartsWith('+') || text.StartsWith('-'))
        {
            vorzeichen = text[0] == '-' ? -1 : 1;
            text = text[1..];
        }
        if (text.Length == 0 || text[0] == '_' || text[^1] == '_' || text.Contains("__", StringComparison.Ordinal))
        {
            return null;
        }
        long wert = 0;
        var gross = false;
        foreach (var zeichen in text.EnumerateRunes())
        {
            if (zeichen.Value == '_')
            {
                continue;
            }
            var ziffer = Rune.GetUnicodeCategory(zeichen) == UnicodeCategory.DecimalDigitNumber ? (int)Rune.GetNumericValue(zeichen) : -1;
            if (ziffer < 0)
            {
                return null;
            }
            if (!gross && wert > (long.MaxValue - ziffer) / 10)
            {
                gross = true;
            }
            wert = gross ? long.MaxValue : wert * 10 + ziffer;
        }
        return vorzeichen * wert;
    }

    /// <summary>Wahrheitswert wie Pythons bool(x).</summary>
    internal static bool PythonBool(JsonNode? wert) => wert switch
    {
        null => false,
        JsonArray a => a.Count > 0,
        JsonObject o => o.Count > 0,
        JsonValue v => v.GetValueKind() switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => Zahl(v) != 0,
            JsonValueKind.String => v.GetValue<string>().Length > 0,
            _ => false,
        },
        _ => false,
    };

    /// <summary>Zahlwert unabhaengig davon, ob er gelesen oder im Programm gesetzt wurde.</summary>
    private static double Zahl(JsonValue v) => double.Parse(v.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>Text wie Pythons str(x) fuer die Uhrzeit-Pruefung (nur Texte koennen gueltig sein).</summary>
    private static string PythonStr(JsonNode? wert) =>
        wert is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : wert?.ToJsonString() ?? "None";
}
