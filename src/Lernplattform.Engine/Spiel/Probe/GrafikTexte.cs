using System.Globalization;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle Grafik G1 (Weisung P12-182): alle Texte der Grafik-Einstellungen (Optionen-Karte „Grafik“, Belastungsanzeige,
/// Info-Zeile im Werkzeug). PC und Handy gleich.
/// </summary>
public static class GrafikTexte
{
    public const string Titel = "Grafik";

    public const string Kurz = "Darstellung der Stadt und Belastung des Geräts";

    public static readonly IReadOnlyList<string> Stichwoerter =
        ["Grafik", "Stufe", "Niedrig", "Mittel", "Hoch", "Belastung", "Bilder je Sekunde", "RAM", "CPU", "GPU", "Leistungsanzeige"];

    public const string StufeTitel = "Stufe";

    public const string Satz = "Die Stufe ändert nur, wie die Stadt gezeichnet wird. Spielstand, Häuser, Türen, Wege und "
        + "deine Figur bleiben in jeder Stufe gleich.";

    public const string Niedrig = "Niedrig";
    public const string Mittel = "Mittel";
    public const string Hoch = "Hoch";
    public const string Eigene = "Eigene";

    /// <summary>Kurze Erklaerung je Stufe (Nico 2 = c: bei Hoch „für starke Geräte“).</summary>
    public static string Erklaerung(Grafikstufe stufe) => stufe switch
    {
        Grafikstufe.Niedrig => "Spart am meisten: einfache Formen schon aus mittlerer Entfernung, kein Kleingrün, halb so viele Autos, 75 % Auflösung, höchstens 30 Bilder je Sekunde.",
        Grafikstufe.Hoch => "Für starke Geräte: volle Formen bis in die Ferne, Kleingrün weiter sichtbar.",
        Grafikstufe.Eigene => "Jeden Wert selbst wählen.",
        _ => "Die Vorgabe: ausgewogen für die meisten Geräte.",
    };

    public static string Name(Grafikstufe stufe) => stufe switch
    {
        Grafikstufe.Niedrig => Niedrig,
        Grafikstufe.Hoch => Hoch,
        Grafikstufe.Eigene => Eigene,
        _ => Mittel,
    };

    // Einzelwerte der Stufe „Eigene“
    public const string FernAbTitel = "Einfache Formen ab Entfernung";
    public const string GruenTitel = "Kleingrün (Gras, Büsche)";
    public const string AutosTitel = "Autos und Gestalten";
    public const string AufloesungTitel = "Auflösung der Stadt";
    public const string BilderTitel = "Bilder je Sekunde höchstens";

    public const string Nah = "nah";
    public const string Mittelweit = "mittel";
    public const string Nie = "nie";
    public const string Aus = "aus";
    public const string Weit = "weit";
    public const string AutosHalb = "halb so viele";
    public const string AutosAlle = "alle";
    public const string Bildschirm = "wie der Bildschirm";

    /// <summary>Beschriftung eines waehlbaren Abstands der Fernform.</summary>
    public static string FernAbName(float wert) =>
        wert == Grafikstufen.FernAbNiedrig ? Nah : wert >= Kamera.AbstandMax ? Nie : Mittelweit;

    /// <summary>Beschriftung eines waehlbaren Abstands des Kleingruens.</summary>
    public static string GruenName(float wert) =>
        wert <= 0 ? Aus : wert >= Grafikstufen.GruenBisHoch ? Weit : Mittelweit;

    public static string AufloesungName(int prozent) => prozent.ToString(CultureInfo.InvariantCulture) + " %";

    public static string BilderName(int bilder) => bilder <= 0 ? Bildschirm : bilder.ToString(CultureInfo.InvariantCulture);

    // Belastung
    public const string BelastungTitel = "Belastung des Geräts";

    public const string BelastungSatz = "Werte aus der Stadt, gemittelt über 5 Sekunden. Was ein Gerät nicht meldet, steht "
        + "als „nicht verfügbar“ da.";

    public const string BelastungNochNicht = "Noch keine Werte: betritt einmal die Stadt im Spiel.";

    public const string NichtVerfuegbar = "nicht verfügbar";

    public const string Bilder = "Bilder je Sekunde";
    public const string ZeitJeBild = "Rechenzeit je Bild";
    public const string Ram = "Arbeitsspeicher (RAM) des Programms";
    public const string Cpu = "Prozessor (CPU) des Programms";
    public const string GpuZeit = "Grafikkarte (GPU): Zeit je Bild";
    public const string GpuProzent = "Grafikkarte (GPU): Auslastung";

    public const string AnzeigeHilfe = "Zeigt im Spiel oben links Bilder je Sekunde, Dreiecke, Aufrufe und die Belastung "
        + "des Geräts. Vorgabe aus. Es wird nichts aufgezeichnet.";

    /// <summary>Hinweis unter Diagnose > Leistungsmessung: der Schalter steht jetzt in der Karte „Grafik“ (ein Schalter).</summary>
    public const string AnzeigeVerweis = "Die Leistungsanzeige im Spiel schaltest du in der Karte „Grafik“ (Gruppe Aussehen) an und aus.";

    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Die Werte als Paare (Name, Wert) fuer die Optionen-Karte.</summary>
    public static IReadOnlyList<(string Name, string Wert)> Zeilen(Belastung.Werte w) =>
    [
        (Bilder, w.BilderJeSekunde.ToString("0", De)),
        (ZeitJeBild, w.ZeitJeBildMs.ToString("0.0", De) + " ms"),
        (Ram, Mb(w.RamBytes)),
        (Cpu, w.CpuProzent is { } c ? c.ToString("0", De) + " %" : NichtVerfuegbar),
        (GpuZeit, w.GpuMs is { } g ? g.ToString("0.0", De) + " ms" : NichtVerfuegbar),
        (GpuProzent, w.GpuProzent is { } p ? p.ToString("0", De) + " %" : NichtVerfuegbar),
    ];

    /// <summary>Kurze Zeile fuer die Leistungsanzeige im Spiel, z. B. „RAM 512 MB · CPU 12 % · GPU 3,1 ms · GPU nicht verfügbar“.</summary>
    public static string Kurzzeile(Belastung.Werte w) =>
        "RAM " + Mb(w.RamBytes)
        + " · CPU " + (w.CpuProzent is { } c ? c.ToString("0", De) + " %" : NichtVerfuegbar)
        + " · GPU " + (w.GpuMs is { } g ? g.ToString("0.0", De) + " ms" : NichtVerfuegbar)
        + (w.GpuProzent is { } p ? " · GPU " + p.ToString("0", De) + " %" : "");

    private static string Mb(long bytes) => (bytes / (1024.0 * 1024)).ToString("#,0", De) + " MB";

    // Werkzeug (Nico 2 = c, Vorschlag 5): Grenzen live gegen Mittel, Info-Zeile mit Niedrig und Hoch
    /// <summary>Info-Zeile im Werkzeug, z. B. „Niedrig 81 % · Hoch 70 % (geprüft wird gegen Mittel)“.</summary>
    public static string WerkzeugInfo(double anteilNiedrig, double anteilHoch) =>
        "Niedrig " + Prozent(anteilNiedrig) + " · Hoch " + Prozent(anteilHoch) + " (geprüft wird gegen Mittel)";

    private static string Prozent(double anteil) => Math.Round(anteil * 100).ToString("0", CultureInfo.InvariantCulture) + " %";
}
