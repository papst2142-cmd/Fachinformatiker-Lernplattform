using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Lernplattform.Core.Pruefung.Echt;

/// <summary>
/// A 1.20.0 (W2, Weisung P12-446, Kurzplan P12-267 B und C): Anlage-Arten ab Format 6. "ausgabe" = Konsole, Protokoll oder
/// Mitschnitt mit Zeilennummern, Eingabezeilen (Feld "eingaben") gruen und fett. "daten" = Datenauszug mit "format" "csv"
/// (Feld "trennzeichen", Kopfzeile, jede Zeile gleich viele Felder) oder "json" (gueltiges JSON, ausser "fehlerhaft": true).
/// Ladepruefung mit klarer Meldung, nie ein Absturz.
/// </summary>
public static class EchtDaten
{
    public const string Ausgabe = "ausgabe";
    public const string Daten = "daten";
    public const string Csv = "csv";
    public const string Json = "json";

    /// <summary>Hoechstens so viele Zeilen (Blatt bleibt lesbar).</summary>
    public const int ZeilenHoechstens = 60;

    private static readonly Regex ZeileImText = new(@"\bZeilen?\s+([0-9]{1,3})(?:\s*(?:bis|und|–|-)\s*([0-9]{1,3}))?(?:\s+(?:der|in|von)\s+Anlage\s+([0-9A-Za-z]+))?",
        RegexOptions.CultureInvariant);

    internal static EchtAnlage Anlage(JsonObject a, string nr, string titel, string art, List<string> zeilen)
    {
        var wo = "Anlage " + nr;
        if (zeilen.Count is 0 or > ZeilenHoechstens)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: 1 bis {ZeilenHoechstens} Zeilen"));
        }
        if (art == Ausgabe)
        {
            var eingaben = new List<int>();
            if (a["eingaben"] is not null)
            {
                if (a["eingaben"] is not JsonArray liste)
                {
                    throw new EchtLadefehler(wo + ": \"eingaben\" ist keine Liste von Zeilennummern");
                }
                foreach (var e in liste)
                {
                    var z = EchtSaetze.Ganz(e) ?? throw new EchtLadefehler(wo + ": \"eingaben\" enthält etwas anderes als ganze Zahlen");
                    if (z < 1 || z > zeilen.Count || eingaben.Contains(z))
                    {
                        throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: Eingabezeile {z} gibt es nicht (oder doppelt)"));
                    }
                    eingaben.Add(z);
                }
            }
            return new EchtAnlage(nr, titel, art, zeilen, [], []) { Eingaben = eingaben };
        }
        var format = EchtSaetze.Pflicht(a, "format");
        if (format == Csv)
        {
            var trenner = EchtSaetze.Pflicht(a, "trennzeichen");
            if (trenner.Length != 1 || trenner == "\"")
            {
                throw new EchtLadefehler(wo + ": \"trennzeichen\" muss genau ein Zeichen sein (nicht \")");
            }
            if (zeilen.Count < 2)
            {
                throw new EchtLadefehler(wo + ": CSV braucht eine Kopfzeile und mindestens eine Datenzeile");
            }
            var felder = new List<IReadOnlyList<string>>();
            for (var i = 0; i < zeilen.Count; i++)
            {
                felder.Add(CsvZeile(zeilen[i], trenner[0]) ?? throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture,
                    $"{wo}: Zeile {i + 1} hat ein offenes Anführungszeichen")));
            }
            if (felder[0].Any(f => f.Trim().Length == 0))
            {
                throw new EchtLadefehler(wo + ": die Kopfzeile (Zeile 1) braucht in jeder Spalte einen Namen");
            }
            if (felder.Select((f, i) => (f, i)).FirstOrDefault(x => x.f.Count != felder[0].Count) is { f: not null } falsch)
            {
                throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture,
                    $"{wo}: Zeile {falsch.i + 1} hat {falsch.f.Count} Felder, die Kopfzeile {felder[0].Count}"));
            }
            if (a["fehlerhaft"] is not null)
            {
                throw new EchtLadefehler(wo + ": \"fehlerhaft\" gibt es nur bei JSON");
            }
            return new EchtAnlage(nr, titel, art, zeilen, [], []) { DatenFormat = Csv, Trennzeichen = trenner, Felder = felder };
        }
        if (format == Json)
        {
            var fehlerhaft = a["fehlerhaft"] is JsonValue f && f.TryGetValue<bool>(out var ja) && ja;
            var gueltig = true;
            try
            {
                using var _ = JsonDocument.Parse(string.Join("\n", zeilen));
            }
            catch (JsonException)
            {
                gueltig = false;
            }
            if (gueltig == fehlerhaft)
            {
                throw new EchtLadefehler(fehlerhaft
                    ? wo + ": als „absichtlich fehlerhaft“ markiert, ist aber gültiges JSON"
                    : wo + ": kein gültiges JSON (absichtlich fehlerhaft nur mit \"fehlerhaft\": true)");
            }
            return new EchtAnlage(nr, titel, art, zeilen, [], []) { DatenFormat = Json, Fehlerhaft = fehlerhaft };
        }
        throw new EchtLadefehler(wo + ": \"format\" muss \"csv\" oder \"json\" sein");
    }

    /// <summary>Felder einer CSV-Zeile (Anfuehrungszeichen wie RFC 4180, "" = ein Zeichen); null bei offenem Anfuehrungszeichen.</summary>
    public static List<string>? CsvZeile(string zeile, char trenner)
    {
        var felder = new List<string>();
        var feld = new StringBuilder();
        var inZitat = false;
        for (var i = 0; i < zeile.Length; i++)
        {
            var c = zeile[i];
            if (inZitat)
            {
                if (c == '"' && i + 1 < zeile.Length && zeile[i + 1] == '"')
                {
                    feld.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inZitat = false;
                }
                else
                {
                    feld.Append(c);
                }
            }
            else if (c == '"' && feld.Length == 0)
            {
                inZitat = true;
            }
            else if (c == trenner)
            {
                felder.Add(feld.ToString());
                feld.Clear();
            }
            else
            {
                feld.Append(c);
            }
        }
        if (inZitat)
        {
            return null;
        }
        felder.Add(feld.ToString());
        return felder;
    }

    /// <summary>
    /// Ladepruefung: jede im Text einer Teilaufgabe genannte Zeile einer Ausgabe oder eines Datenauszugs gibt es ("Zeile 9 der
    /// Anlage 2"; verweist der Teil nur auf eine solche Anlage, gilt auch "Zeile 9" allein).
    /// </summary>
    internal static void ZeilenPruefen(string wo, EchtTeil teil, IReadOnlyList<EchtAnlage> anlagen)
    {
        var eigene = teil.Anlagen.Select(v => anlagen.First(a => a.Nr == v)).Where(a => a.Art is Ausgabe or Daten).ToList();
        foreach (var zeile in teil.Text)
        {
            foreach (Match treffer in ZeileImText.Matches(zeile))
            {
                var ziel = treffer.Groups[3].Success
                    ? anlagen.FirstOrDefault(a => a.Nr == treffer.Groups[3].Value && a.Art is Ausgabe or Daten)
                    : teil.Anlagen.Count == 1 && eigene.Count == 1 ? eigene[0] : null;
                if (ziel is null)
                {
                    continue;
                }
                foreach (var g in new[] { treffer.Groups[1], treffer.Groups[2] }.Where(g => g.Success))
                {
                    var n = int.Parse(g.Value, CultureInfo.InvariantCulture);
                    if (n < 1 || n > ziel.Zeilen.Count)
                    {
                        throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture,
                            $"{wo}: Zeile {n} der Anlage {ziel.Nr} gibt es nicht (sie hat {ziel.Zeilen.Count} Zeilen)"));
                    }
                }
            }
        }
    }
}
