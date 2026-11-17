using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Update;

namespace Lernplattform.Core.Pakete;

/// <summary>Kopf eines Inhaltspakets (paket.json, Format 1).</summary>
public sealed record PaketKopf(long Format, string Art, string Kennung, string Name, long Stand, string BrauchtApp, string Text,
    IReadOnlyDictionary<string, (string Sha256, long Groesse)> Dateien);

/// <summary>Ergebnis einer Pruefung: Kopf (gueltig) oder Grund (rot in der Liste, nie Absturz).</summary>
public sealed record PaketErgebnis(PaketKopf? Kopf, string? Grund)
{
    public bool Gut => Kopf is not null && Grund is null;
}

/// <summary>
/// Welle A 0.87.0 (Weisung P12-294, Plan P12-175 Abschnitte 1, 2, 6; Nico 1a 2a 3b 4a 5a): ein Inhaltspaket ist EINE Datei
/// <c>&lt;kennung&gt;_&lt;stand&gt;.fipaket</c> (ZIP, .NET kann es selbst) mit dem Kopf <c>paket.json</c> und den Dateien des
/// Inhalts (Art <c>stadt</c>: <c>stadt.fistadt</c>). Jede Datei steht mit SHA-256 und Groesse im Kopf; nichts Fremdes, nichts
/// fehlt. Das Feld <c>art</c> ist ab Format 1 Pflicht (Nico 3b: spaeter auch Lern-Pakete je Fachrichtung); eine unbekannte Art
/// ist ein klarer Grund (<see cref="PaketTexte.ArtUnbekannt"/>), nie ein Absturz. Neuere Formatnummer oder <c>braucht_app</c>
/// ueber der App = klarer Grund „Bitte die App aktualisieren“ (Formatregel wie bei den Stadt-Dateien).
/// </summary>
public static partial class Paket
{
    public const long Format = 1;
    public const string Endung = ".fipaket";
    public const string KopfDatei = "paket.json";
    public const string StadtDatei = "stadt.fistadt";
    public const string ArtStadt = "stadt";

    /// <summary>Arten, die diese App kennt (heute nur Staedte; Lern-Pakete erst nach eigenem Kurzplan, Nico 3b).</summary>
    public static readonly IReadOnlyList<string> Arten = [ArtStadt];

    /// <summary>Pflichtdateien je Art.</summary>
    public static IReadOnlyList<string> Pflicht(string art) => art == ArtStadt ? [StadtDatei] : [];

    /// <summary>Kennungen, die zur App gehoeren (Stadt 1 bleibt immer in der App, Nico 4a).</summary>
    public static readonly IReadOnlyList<string> Reserviert = [AktiveFassung.Stadt1];

    /// <summary>Hoechste Groesse eines Pakets (alle Dateien zusammen); schuetzt vor zu grossen oder gefaelschten Archiven.</summary>
    public const long HoechstGroesse = 512L * 1024 * 1024;

    [GeneratedRegex(@"^[a-z][a-z0-9_]{1,31}\z", RegexOptions.CultureInvariant)]
    private static partial Regex KennungMuster();

    [GeneratedRegex(@"^([a-z][a-z0-9_]{1,31})_([1-9][0-9]{0,8})\.fipaket\z", RegexOptions.CultureInvariant)]
    private static partial Regex DateiMuster();

    public static bool KennungGueltig(string? kennung) => kennung is not null && KennungMuster().IsMatch(kennung);

    /// <summary>Dateiname eines Pakets.</summary>
    public static string Dateiname(string kennung, long stand) => kennung + "_" + stand + Endung;

    /// <summary>Kennung und Stand aus einem Dateinamen <c>&lt;kennung&gt;_&lt;stand&gt;.fipaket</c>, sonst null.</summary>
    public static (string Kennung, long Stand)? AusDateiname(string name) =>
        DateiMuster().Match(name) is { Success: true } m ? (m.Groups[1].Value, long.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)) : null;

    /// <summary>
    /// Liest alle Dateien eines Archivs (hoechstens <see cref="HoechstGroesse"/> zusammen). Grund statt Ausnahme, wenn es
    /// kein lesbares Archiv ist, Namen mit Ordnern oder doppelt vorkommen.
    /// </summary>
    public static (Dictionary<string, byte[]>? Dateien, string? Grund) Entpacken(Stream archiv)
    {
        try
        {
            using var zip = new ZipArchive(archiv, ZipArchiveMode.Read, leaveOpen: true);
            var dateien = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            long summe = 0;
            foreach (var eintrag in zip.Entries)
            {
                var name = eintrag.FullName;
                if (name.Length == 0 || name.Contains('/') || name.Contains('\\') || name is "." or "..")
                {
                    return (null, PaketTexte.Format(PaketTexte.DateiFremd, name));
                }
                if (dateien.ContainsKey(name))
                {
                    return (null, PaketTexte.Format(PaketTexte.DateiFremd, name));
                }
                using var strom = eintrag.Open();
                using var puffer = new MemoryStream();
                var block = new byte[81920];
                int n;
                while ((n = strom.Read(block, 0, block.Length)) > 0)
                {
                    summe += n;
                    if (summe > HoechstGroesse)
                    {
                        return (null, PaketTexte.ZuGross);
                    }
                    puffer.Write(block, 0, n);
                }
                dateien[name] = puffer.ToArray();
            }
            return (dateien, null);
        }
        catch (Exception x) when (x is InvalidDataException or IOException or NotSupportedException or ArgumentException)
        {
            return (null, PaketTexte.KeinArchiv);
        }
    }

    /// <summary>Prueft ein Paket-Archiv vollstaendig (Kopf, Prüfsummen, Inhalt); <paramref name="app"/> = Anzeige der App.</summary>
    public static PaketErgebnis PruefenArchiv(Stream archiv, string app)
    {
        var (dateien, grund) = Entpacken(archiv);
        return dateien is null ? new(null, grund) : Pruefen(dateien, app);
    }

    /// <summary>
    /// Prueft die Dateien eines Pakets (aus dem Archiv oder aus dem Ordner im Datenordner): Kopf, Format, Art, Kennung,
    /// <c>braucht_app</c>, jede Datei gegen SHA-256 und Groesse, keine fremde und keine fehlende Datei, Ladepruefung des Inhalts
    /// (Stadt: <see cref="StadtFassung.Lesen"/>). Gibt den ersten Grund zurueck.
    /// </summary>
    public static PaketErgebnis Pruefen(IReadOnlyDictionary<string, byte[]> dateien, string app)
    {
        if (!dateien.TryGetValue(KopfDatei, out var kopfBytes))
        {
            return new(null, PaketTexte.KopfFehlt);
        }
        JsonObject kopf;
        try
        {
            kopf = JsonNode.Parse(kopfBytes) as JsonObject ?? throw new JsonException();
        }
        catch (Exception x) when (x is JsonException or ArgumentException or InvalidOperationException)
        {
            return new(null, PaketTexte.KopfKaputt);
        }
        string Fehlt(string feld) => PaketTexte.Format(PaketTexte.FeldFehlt, feld);
        if (Zahl(kopf["format"]) is not { } format || format < 1)
        {
            return new(null, Fehlt("format"));
        }
        if (format > Format)
        {
            return new(null, PaketTexte.Format(PaketTexte.FormatNeu, format, Format));
        }
        if (Text(kopf["art"]) is not { Length: > 0 } art)
        {
            return new(null, Fehlt("art"));
        }
        if (!Arten.Contains(art))
        {
            return new(null, PaketTexte.ArtUnbekannt);
        }
        if (Text(kopf["kennung"]) is not { } kennung || !KennungGueltig(kennung))
        {
            return new(null, Fehlt("kennung"));
        }
        if (Reserviert.Contains(kennung))
        {
            return new(null, PaketTexte.Format(PaketTexte.KennungReserviert, kennung));
        }
        if (Text(kopf["name"]) is not { Length: > 0 and <= 40 } name || name.Trim() != name)
        {
            return new(null, Fehlt("name"));
        }
        if (Zahl(kopf["stand"]) is not { } stand || stand < 1)
        {
            return new(null, Fehlt("stand"));
        }
        if (Text(kopf["braucht_app"]) is not { } braucht || UpdateVersion.Lesen(braucht) is not { } brauchtVersion)
        {
            return new(null, Fehlt("braucht_app"));
        }
        if (UpdateVersion.Lesen(app) is { } appVersion && brauchtVersion.CompareTo(appVersion) > 0)
        {
            return new(null, PaketTexte.Format(PaketTexte.AppAlt, braucht));
        }
        var text = kopf["text"] is null ? "" : Text(kopf["text"]);
        if (text is null)
        {
            return new(null, Fehlt("text"));
        }
        if (kopf["dateien"] is not JsonObject liste || liste.Count == 0)
        {
            return new(null, Fehlt("dateien"));
        }
        var angaben = new Dictionary<string, (string, long)>(StringComparer.Ordinal);
        foreach (var (datei, angabe) in liste)
        {
            if (datei == KopfDatei || angabe is not JsonObject a || Text(a["sha256"]) is not { Length: 64 } sha || Zahl(a["groesse"]) is not { } groesse || groesse < 0)
            {
                return new(null, Fehlt("dateien"));
            }
            angaben[datei] = (sha, groesse);
        }
        foreach (var pflicht in Pflicht(art).Where(p => !angaben.ContainsKey(p)))
        {
            return new(null, PaketTexte.Format(PaketTexte.DateiFehlt, pflicht));
        }
        foreach (var (datei, (sha, groesse)) in angaben)
        {
            if (!dateien.TryGetValue(datei, out var inhalt))
            {
                return new(null, PaketTexte.Format(PaketTexte.DateiFehlt, datei));
            }
            if (inhalt.LongLength != groesse || !string.Equals(Convert.ToHexStringLower(SHA256.HashData(inhalt)), sha, StringComparison.Ordinal))
            {
                return new(null, PaketTexte.Format(PaketTexte.Pruefsumme, datei));
            }
        }
        if (dateien.Keys.FirstOrDefault(d => d != KopfDatei && !angaben.ContainsKey(d)) is { } fremd)
        {
            return new(null, PaketTexte.Format(PaketTexte.DateiFremd, fremd));
        }
        if (art == ArtStadt)
        {
            try
            {
                _ = StadtFassung.Lesen(dateien[StadtDatei]);
            }
            catch (StadtbauFehler f)
            {
                return new(null, PaketTexte.Format(PaketTexte.StadtFehler, f.Funde.FirstOrDefault() ?? f.Message));
            }
            catch (Exception x) when (x is InvalidCastException or KeyNotFoundException or NullReferenceException or FormatException or OverflowException)
            {
                return new(null, PaketTexte.Format(PaketTexte.StadtFehler, x.Message));
            }
        }
        return new(new PaketKopf(format, art, kennung, name, stand, braucht, text, angaben), null);
    }

    private static string? Text(JsonNode? wert) => wert is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;

    private static long? Zahl(JsonNode? wert) =>
        wert is JsonValue v && v.GetValueKind() == JsonValueKind.Number && v.TryGetValue<long>(out var z) ? z : null;
}
