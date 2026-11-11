using System.Globalization;
using System.Text.Json.Nodes;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Welle Grafik G1 (Weisung P12-182, Nico 1a): die Stufen der Grafik-Einstellungen.</summary>
public enum Grafikstufe
{
    Niedrig,
    Mittel,
    Hoch,
    Eigene,
}

/// <summary>
/// Welle Grafik G1 (Weisung P12-182, Kurzplan Meldung P12-109 Tabelle 2): die Werte einer Stufe, nur Darstellung.
/// <paramref name="FernAb"/> = ab diesem Kameraabstand die einfachen Fernformen (Haeuser, Fest-Deko, Autos, Paket-Netz);
/// <paramref name="GruenBis"/> = Kleingruen bis zu diesem Abstand (0 = aus); <paramref name="AutosHalb"/> = nur jedes
/// zweite Auto bzw. jede zweite Festgestalt wird gezeichnet (der Verkehr rechnet gleich, Busse immer);
/// <paramref name="Aufloesung"/> = Bildaufloesung der Stadt in Prozent (75 oder 100); <paramref name="BilderHoechstens"/> =
/// Bilder je Sekunde hoechstens (0 = so viele wie der Bildschirm). Spielstand, Fingerabdruecke, Stadt-Datei, Tueren, Wege,
/// Figur und Zielmarke sind in jeder Stufe gleich.
/// </summary>
public sealed record Grafikwerte(float FernAb, float GruenBis, bool AutosHalb, int Aufloesung, int BilderHoechstens)
{
    /// <summary>Ab diesem Abstand die Fernformen (gleiche Regel wie seit D1: groesser als <see cref="FernAb"/>).</summary>
    public bool Fern(float abstand) => abstand > FernAb;

    /// <summary>Kleingruen bei diesem Abstand (gleiche Regel wie seit Phase 8: bis einschliesslich <see cref="GruenBis"/>).</summary>
    public bool Gruen(float abstand) => GruenBis > 0 && abstand <= GruenBis;
}

/// <summary>
/// Welle Grafik G1 (Weisung P12-182): Stufen, ihre Werte und Grenzen, Merker und die automatische Wahl beim ersten Start.
/// Die Werte von „Mittel“ sind die Konstanten der Engine (<see cref="StadtSzene.FernAb"/>, <see cref="StadtSzene.GruenBis"/>,
/// <see cref="StadtbauGrenzen.Dreiecke"/>), damit Mittel genau dem Stand vor G1 entspricht (Spiel-Fotos pixelgleich).
/// </summary>
public static class Grafikstufen
{
    /// <summary>Merker in einstellungen.json: "niedrig", "mittel", "hoch" oder "eigene" (fehlt = automatische Wahl).</summary>
    public const string Schluessel = "grafik_stufe";

    /// <summary>Merker in einstellungen.json: die Einzelwerte der Stufe „Eigene“.</summary>
    public const string SchluesselEigene = "grafik_eigene";

    /// <summary>Fernform schon ab Abstand 12 (Plan Tabelle 2).</summary>
    public const float FernAbNiedrig = 12f;

    /// <summary>Kleingruen bis Abstand 30 bei „Hoch“.</summary>
    public const float GruenBisHoch = 30f;

    /// <summary>Bildaufloesung der Stadt bei „Niedrig“ in Prozent.</summary>
    public const int AufloesungNiedrig = 75;

    /// <summary>Bilder je Sekunde hoechstens bei „Niedrig“.</summary>
    public const int BilderNiedrig = 30;

    public static readonly Grafikwerte Niedrig = new(FernAbNiedrig, 0f, true, AufloesungNiedrig, BilderNiedrig);

    /// <summary>
    /// = Stand vor G1 (A 0.63.0): Fernform ab 25, Kleingruen bis 18, alle Autos, volle Aufloesung, keine Bildbremse
    /// (E1: die Tabelle nannte 60; eine Bremse auf 60 wirkt unter Windows an 120/144-Hz-Bildschirmen ungleichmaessig,
    /// darum zeichnet Mittel wie heute so oft wie der Bildschirm; 60 ist unter „Eigene“ waehlbar).
    /// </summary>
    public static readonly Grafikwerte Mittel = new(StadtSzene.FernAb, StadtSzene.GruenBis, false, 100, 0);

    /// <summary>Fernform nie (der groesste Kameraabstand ist <see cref="Kamera.AbstandMax"/>), Kleingruen bis 30.</summary>
    public static readonly Grafikwerte Hoch = new(Kamera.AbstandMax, GruenBisHoch, false, 100, 0);

    /// <summary>Waehlbare Werte der Stufe „Eigene“ (je Wert genau diese, sonst gilt der Wert von Mittel).</summary>
    public static readonly IReadOnlyList<float> FernAbWahl = [FernAbNiedrig, StadtSzene.FernAb, Kamera.AbstandMax];

    /// <inheritdoc cref="FernAbWahl"/>
    public static readonly IReadOnlyList<float> GruenBisWahl = [0f, StadtSzene.GruenBis, GruenBisHoch];

    /// <inheritdoc cref="FernAbWahl"/>
    public static readonly IReadOnlyList<int> AufloesungWahl = [AufloesungNiedrig, 100];

    /// <inheritdoc cref="FernAbWahl"/>
    public static readonly IReadOnlyList<int> BilderWahl = [BilderNiedrig, 60, 0];

    /// <summary>Werte einer festen Stufe; „Eigene“ nimmt <paramref name="eigene"/> (ohne: Mittel).</summary>
    public static Grafikwerte Werte(Grafikstufe stufe, Grafikwerte? eigene = null) => stufe switch
    {
        Grafikstufe.Niedrig => Niedrig,
        Grafikstufe.Hoch => Hoch,
        Grafikstufe.Eigene => eigene ?? Mittel,
        _ => Mittel,
    };

    /// <summary>
    /// Grenzen je Stufe (Nico 2 = c): Niedrig 125 000 / 32, Mittel 160 000 / 40 (wie bisher), Hoch 200 000 / 48 (fuer
    /// starke Geraete); gilt fuer Gesamt- und Nahansicht. „Eigene“ darf bis zur Grenze von Hoch (E1). Das Werkzeug prueft
    /// jede Stadt gegen Mittel.
    /// </summary>
    public static (long Dreiecke, int Aufrufe) Grenze(Grafikstufe stufe) => stufe switch
    {
        Grafikstufe.Niedrig => (125_000, 32),
        Grafikstufe.Mittel => (StadtbauGrenzen.Dreiecke, StadtbauGrenzen.Aufrufe),
        _ => (200_000, 48),
    };

    /// <summary>Name im Merker.</summary>
    public static string Name(Grafikstufe stufe) => stufe switch
    {
        Grafikstufe.Niedrig => "niedrig",
        Grafikstufe.Hoch => "hoch",
        Grafikstufe.Eigene => "eigene",
        _ => "mittel",
    };

    /// <summary>Gemerkte Stufe; null, wenn der Merker fehlt oder keinen bekannten Wert hat (dann waehlt der erste Start).</summary>
    public static Grafikstufe? Lesen(JsonObject werte) =>
        werte[Schluessel] is JsonValue v && v.TryGetValue<string>(out var s)
            ? s switch
            {
                "niedrig" => Grafikstufe.Niedrig,
                "mittel" => Grafikstufe.Mittel,
                "hoch" => Grafikstufe.Hoch,
                "eigene" => Grafikstufe.Eigene,
                _ => null,
            }
            : null;

    /// <summary>Einzelwerte der Stufe „Eigene“; jeder unbekannte oder fehlende Wert = der Wert von Mittel.</summary>
    public static Grafikwerte EigeneLesen(JsonObject werte)
    {
        var o = werte[SchluesselEigene] as JsonObject;
        // Zahl aus der Datei (JsonElement) oder frisch gesetzt (int/double im Speicher)
        static double? Wert(JsonNode? n) =>
            n is not JsonValue v ? null
            : v.TryGetValue<double>(out var d) ? d
            : v.TryGetValue<int>(out var i) ? i
            : v.TryGetValue<long>(out var l) ? l
            : v.TryGetValue<float>(out var f) ? f
            : null;
        float Zahl(string name, IReadOnlyList<float> wahl, float vorgabe) =>
            Wert(o?[name]) is { } d && wahl.Contains((float)d) ? (float)d : vorgabe;
        int Ganz(string name, IReadOnlyList<int> wahl, int vorgabe) =>
            Wert(o?[name]) is { } d && d == Math.Floor(d) && wahl.Contains((int)d) ? (int)d : vorgabe;
        var halb = o?["autos_halb"] is JsonValue h && h.TryGetValue<bool>(out var b) ? b : Mittel.AutosHalb;
        return new Grafikwerte(
            Zahl("fern_ab", FernAbWahl, Mittel.FernAb),
            Zahl("gruen_bis", GruenBisWahl, Mittel.GruenBis),
            halb,
            Ganz("aufloesung", AufloesungWahl, Mittel.Aufloesung),
            Ganz("bilder", BilderWahl, Mittel.BilderHoechstens));
    }

    /// <summary>Werte der gemerkten Stufe (ohne Merker: Mittel; der erste Start setzt ihn vorher).</summary>
    public static Grafikwerte Aktuell(JsonObject werte) => Werte(Lesen(werte) ?? Grafikstufe.Mittel, EigeneLesen(werte));

    /// <summary>Merker setzen (nur ueber <c>Einstellungen.Aendern</c> aufrufen).</summary>
    public static void Wert(JsonObject werte, Grafikstufe stufe) => werte[Schluessel] = Name(stufe);

    /// <summary>Einzelwerte von „Eigene“ setzen (nur ueber <c>Einstellungen.Aendern</c> aufrufen).</summary>
    public static void EigeneWert(JsonObject werte, Grafikwerte w) => werte[SchluesselEigene] = new JsonObject
    {
        ["fern_ab"] = (double)w.FernAb,
        ["gruen_bis"] = (double)w.GruenBis,
        ["autos_halb"] = w.AutosHalb,
        ["aufloesung"] = w.Aufloesung,
        ["bilder"] = w.BilderHoechstens,
    };

    /// <summary>Messbare Werte des Geraets fuer die automatische Wahl (null = das System liefert den Wert nicht).</summary>
    public sealed record Geraet(bool Pc, long? RamBytes, int Kerne, string? Renderer);

    /// <summary>Ergebnis der automatischen Wahl mit der Zeile der festen Tabelle.</summary>
    public sealed record Wahl(Grafikstufe Stufe, string Regel);

    private const long Gb = 1024L * 1024 * 1024;

    /// <summary>
    /// Nico 4 = b: automatische Wahl nach Geraet, nur einmal beim ersten Start (Merker fehlt), danach nie von allein.
    /// Feste Tabelle (erste passende Zeile gilt):
    /// <list type="number">
    /// <item>Zeichnung ohne Grafikkarte (llvmpipe, softpipe, SwiftShader, Microsoft Basic Render, „Software“) → Niedrig.</item>
    /// <item>Weniger als 6 GB Arbeitsspeicher oder weniger als 4 Kerne → Niedrig.</item>
    /// <item>Handy/Tablet: Adreno ab 700, Mali G7xx/G9xx, Immortalis oder Xclipse → Mittel; sonst Niedrig (Handys nie Hoch).</item>
    /// <item>PC mit eigener Grafikkarte (NVIDIA GeForce/RTX/Quadro, AMD Radeon RX/Pro, Intel Arc) und mindestens 16 GB → Hoch.</item>
    /// <item>Apple M Pro/Max/Ultra mit mindestens 16 GB → Hoch.</item>
    /// <item>Jeder andere PC (eingebaute Grafik, unbekannt) → Mittel.</item>
    /// </list>
    /// </summary>
    public static Wahl Automatisch(Geraet g)
    {
        var r = (g.Renderer ?? "").ToUpperInvariant();
        if (r.Contains("LLVMPIPE", StringComparison.Ordinal) || r.Contains("SOFTPIPE", StringComparison.Ordinal)
            || r.Contains("SWIFTSHADER", StringComparison.Ordinal) || r.Contains("BASIC RENDER", StringComparison.Ordinal)
            || r.Contains("SOFTWARE", StringComparison.Ordinal))
        {
            return new(Grafikstufe.Niedrig, "1 Zeichnung ohne Grafikkarte");
        }
        if ((g.RamBytes is { } ram && ram < 6 * Gb) || g.Kerne < 4)
        {
            return new(Grafikstufe.Niedrig, "2 wenig Arbeitsspeicher oder Kerne");
        }
        if (!g.Pc)
        {
            return StarkesHandy(r)
                ? new(Grafikstufe.Mittel, "3 Handy/Tablet mit starker Grafik")
                : new(Grafikstufe.Niedrig, "3 Handy/Tablet ohne starke Grafik");
        }
        var genugRam = g.RamBytes is { } r16 && r16 >= 16 * Gb;
        if (genugRam && (r.Contains("GEFORCE", StringComparison.Ordinal) || r.Contains("RTX", StringComparison.Ordinal)
            || r.Contains("QUADRO", StringComparison.Ordinal) || r.Contains("RADEON RX", StringComparison.Ordinal)
            || r.Contains("RADEON PRO", StringComparison.Ordinal) || r.Contains("INTEL(R) ARC", StringComparison.Ordinal)
            || r.Contains("INTEL ARC", StringComparison.Ordinal)))
        {
            return new(Grafikstufe.Hoch, "4 PC mit eigener Grafikkarte");
        }
        if (genugRam && r.Contains("APPLE M", StringComparison.Ordinal)
            && (r.Contains(" PRO", StringComparison.Ordinal) || r.Contains(" MAX", StringComparison.Ordinal) || r.Contains(" ULTRA", StringComparison.Ordinal)))
        {
            return new(Grafikstufe.Hoch, "5 Apple M Pro/Max/Ultra");
        }
        return new(Grafikstufe.Mittel, "6 PC mit eingebauter oder unbekannter Grafik");
    }

    /// <summary>Starke Grafik am Handy: Adreno ab 700, Mali G7xx/G9xx, Immortalis, Xclipse.</summary>
    private static bool StarkesHandy(string r)
    {
        var a = r.IndexOf("ADRENO", StringComparison.Ordinal);
        if (a >= 0)
        {
            var ziffern = new string([.. r[a..].SkipWhile(c => !char.IsAsciiDigit(c)).TakeWhile(char.IsAsciiDigit)]);
            return int.TryParse(ziffern, NumberStyles.None, CultureInfo.InvariantCulture, out var nr) && nr >= 700;
        }
        var m = r.IndexOf("MALI-G", StringComparison.Ordinal);
        if (m >= 0)
        {
            var ziffern = new string([.. r[(m + 6)..].TakeWhile(char.IsAsciiDigit)]);
            return ziffern.Length >= 2 && (ziffern[0] == '7' || ziffern[0] == '9');
        }
        return r.Contains("IMMORTALIS", StringComparison.Ordinal) || r.Contains("XCLIPSE", StringComparison.Ordinal);
    }

    /// <summary>Geraetewerte dieses Rechners (Arbeitsspeicher laut .NET, Kerne); den Renderer kennt erst die Grafik.</summary>
    public static Geraet DiesesGeraet(bool pc, string? renderer)
    {
        long? ram = null;
        try
        {
            var gesamt = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            ram = gesamt > 0 ? gesamt : null;
        }
        catch (Exception e) when (e is PlatformNotSupportedException or InvalidOperationException)
        {
            ram = null;
        }
        return new Geraet(pc, ram, Environment.ProcessorCount, renderer);
    }
}
