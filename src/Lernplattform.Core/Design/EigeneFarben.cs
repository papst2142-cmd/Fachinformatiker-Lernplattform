using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Mathe;

namespace Lernplattform.Core.Design;

/// <summary>Ein Regler-Satz: Farbton (0-359 Grad), Saettigung und Helligkeit (0-100 %).</summary>
public sealed record Regler(int H, int S, int L);

/// <summary>Die drei Regler-Gruppen der eigenen Farben (Python: {"akzent1": (h, s, l), ...}).</summary>
public sealed record EigeneWerte(Regler Akzent1, Regler Akzent2, Regler Hintergrund);

/// <summary>Ein Regler-Kanal (Python: CUSTOM_CHANNELS).</summary>
public sealed record ReglerKanal(string Id, string Name, int Min, int Max, string EinheitKurz, string EinheitGesprochen);

/// <summary>Eine Kontrastpruefung (Python: (Element, Messwert, Ziel)).</summary>
public sealed record KontrastPruefung(string Element, double Wert, double Ziel);

/// <summary>Kartenfarben fuer die Kontrastpruefung (Python: map_colors, nur diese vier werden gebraucht).</summary>
public sealed record KartenFarben(string Boden, string Schrift, string Strasse, string Wasser);

/// <summary>
/// Eigene Farben (Python fisi_theme ab 0.57): drei Regler-Gruppen (Akzent 1,
/// Akzent 2, Hintergrund) mit Farbton, Saettigung und Helligkeit, daraus alle
/// Farben abgeleitet. Gespeichert lokal je Geraet und je Darstellung unter
/// "eigene_farben" in einstellungen.json. Stehen die Regler genau auf dem
/// Startwert der Farbwelt, gilt die Farbwelt exakt ("sanft", Nico 05.10.2026).
/// Alle Ergebnisse muessen exakt den Python-Werten entsprechen (Vergleichswerte G6b).
/// </summary>
public static class EigeneFarben
{
    public const string Schluessel = "eigene_farben";

    public const string TeilAkzent1 = "akzent1";
    public const string TeilAkzent2 = "akzent2";
    public const string TeilHintergrund = "hintergrund";

    /// <summary>Python: CUSTOM_PARTS (Kennung, Beschriftung).</summary>
    public static readonly IReadOnlyList<(string Id, string Name)> Teile =
        [(TeilAkzent1, "Akzent 1"), (TeilAkzent2, "Akzent 2"), (TeilHintergrund, "Hintergrund")];

    public static readonly ReglerKanal Farbton = new("h", "Farbton", 0, 359, "°", "Grad");
    public static readonly ReglerKanal Saettigung = new("s", "Sättigung", 0, 100, " %", "Prozent");
    public static readonly ReglerKanal Helligkeit = new("l", "Helligkeit", 0, 100, " %", "Prozent");

    /// <summary>Python: CUSTOM_CHANNELS (Reihenfolge h, s, l).</summary>
    public static readonly IReadOnlyList<ReglerKanal> Kanaele = [Farbton, Saettigung, Helligkeit];

    // Texte (PC und Handy gleich). Platzhalter {0} usw. stehen fuer Pythons %s/%d.
    public const string Titel = "EIGENE FARBEN";
    public const string Hinweis = "Mit den Reglern stellst du die zwei Farben der Farbverläufe (Akzent 1 und 2) "
        + "und den Hintergrund selbst ein. Alle anderen Farben werden daraus abgeleitet. "
        + "Gilt nur für dieses Gerät und nur für die gewählte Darstellung.";
    public const string ZustandAnText = "Eigene Farben aktiv ({0})";
    public const string ZustandAusText = "Startwerte aus der Farbwelt ({0})";
    public const string ZustandVeraendertText = "Regler verändert, noch nicht gespeichert";
    public const string KachelHinweis = "Ein Klick auf eine Grundfarbe oder einen Hintergrund setzt nur die "
        + "Regler auf diese Farbwelt. Gespeichert wird erst mit „Speichern“.";
    public const string Vorschau = "Vorschau";
    public const string VorschauUeberschrift = "Überschrift";
    public const string VorschauText = "Fließtext einer Frage";
    public const string VorschauNebentext = "Nebentext";
    public const string VorschauLink = "Link";
    public const string VorschauKnopf = "Knopf";
    public const string VorschauKarte = "Karte";
    public const string SpeichernText = "Speichern";
    public const string ZuruecksetzenText = "Auf Farbwelt zurücksetzen";
    public const string AllesGut = "Alle Kontraste reichen aus.";
    public const string WarnTitel = "Zu wenig Kontrast (Speichern ist trotzdem möglich):";
    public const string WarnWeitere = "… und {0} weitere";
    public const string FachbereichNahText = "Fachbereichsfarben schwer unterscheidbar: {0} und {1} (Farbabstand {2}, nötig {3})";

    // Zustaende der Regler (Python: custom_state)
    public const string ZustandAktiv = "aktiv";
    public const string ZustandStart = "start";
    public const string ZustandVeraendert = "veraendert";

    // Kontrastziele (Python: TEXT_TARGET, AREA_TARGET, CATEGORY_DISTANCE)
    public const double SchriftZiel = 4.5;
    public const double FlaechenZiel = 3.0;
    public const double FachbereichAbstand = 20.0;

    /// <summary>Python: MODES (Kennung -> Name).</summary>
    public static string DarstellungName(string darstellung) => darstellung switch
    {
        Farbwelt.Dunkel => "Dunkel",
        Farbwelt.Hell => "Hell",
        _ => throw new ArgumentException("Unbekannte Darstellung: " + darstellung, nameof(darstellung)),
    };

    // Helligkeitsstufen der Flaechen relativ zum Hintergrund: (Abstand, Saettigungsfaktor), Reihenfolge wie Python
    private static readonly (string, int, double)[] StufenDunkel =
    [
        ("sidebar", -3, 1.05), ("card", 4, 0.9), ("card_alt", 8, 0.9), ("card_hi", 14, 0.9),
        ("border", 13, 0.85), ("border_hi", 28, 0.83), ("ring_bg", 11, 0.88),
    ];

    private static readonly (string, int, double)[] StufenHell =
    [
        ("sidebar", -3, 0.97), ("card_alt", 0, 1.0), ("card_hi", -4, 1.0),
        ("border", -8, 0.83), ("border_hi", -21, 0.8), ("ring_bg", -6, 0.9),
    ];

    // Nebentexte: (Helligkeit, Saettigungsfaktor), danach bis 4,6:1 nachgeregelt
    private static readonly (string, int, double)[] NebenDunkel = [("text_dim", 68, 0.53), ("muted", 62, 0.46)];
    private static readonly (string, int, double)[] NebenHell = [("text_dim", 38, 0.43), ("muted", 44, 0.38)];

    // Python: _LIGHT_SHADED
    private static readonly string[] Schriftfarben = ["cyan", "pink", "purple", "violet", "green", "yellow", "orange", "blue", "red"];

    private static readonly double[] Spreizungen = [1.0, 0.9, 0.8, 0.7, 0.6, 0.5];

    // ------------------------------------------------------------ HSL

    /// <summary>Python: hsl_to_hex - Farbton in Grad, Saettigung und Helligkeit in % -> "#RRGGBB".</summary>
    public static string HslZuHex(double farbton, double saettigung, double helligkeit)
    {
        var (r, g, b) = HlsZuRgb(PyMod(farbton, 360) / 360.0,
            Math.Max(0.0, Math.Min(100.0, helligkeit)) / 100.0,
            Math.Max(0.0, Math.Min(100.0, saettigung)) / 100.0);
        return Farbwelt.RgbZuHex(r * 255, g * 255, b * 255);
    }

    /// <summary>Python: hex_to_hsl - "#RRGGBB" -> ganze Zahlen (Farbton, Saettigung, Helligkeit).</summary>
    public static Regler HexZuHsl(string farbe)
    {
        var (ri, gi, bi) = Farbwelt.HexZuRgb(farbe);
        var (h, l, s) = RgbZuHls(ri / 255.0, gi / 255.0, bi / 255.0);
        return new Regler(PyMod(Runden(h * 360), 360), Runden(s * 100), Runden(l * 100));
    }

    // Python: round() = auf gerade Zahl runden
    private static int Runden(double v) => (int)Math.Round(v, MidpointRounding.ToEven);

    private static int PyMod(int a, int b)
    {
        var r = a % b;
        return r != 0 && (r < 0) != (b < 0) ? r + b : r;
    }

    // Python float % (Ergebnis hat das Vorzeichen des Teilers)
    private static double PyMod(double a, double b)
    {
        var r = a % b;
        if (r != 0)
        {
            if ((r < 0) != (b < 0))
            {
                r += b;
            }
        }
        else
        {
            r = b < 0 ? -0.0 : 0.0;
        }
        return r;
    }

    private const double EinDrittel = 1.0 / 3.0;
    private const double EinSechstel = 1.0 / 6.0;
    private const double ZweiDrittel = 2.0 / 3.0;

    // Python colorsys.hls_to_rgb (Rechenreihenfolge unveraendert)
    private static (double, double, double) HlsZuRgb(double h, double l, double s)
    {
        if (s == 0.0)
        {
            return (l, l, l);
        }
        double m2 = l <= 0.5 ? l * (1.0 + s) : l + s - (l * s);
        double m1 = 2.0 * l - m2;
        return (Wert(m1, m2, h + EinDrittel), Wert(m1, m2, h), Wert(m1, m2, h - EinDrittel));
    }

    private static double Wert(double m1, double m2, double ton)
    {
        ton = PyMod(ton, 1.0);
        if (ton < EinSechstel)
        {
            return m1 + (m2 - m1) * ton * 6.0;
        }
        if (ton < 0.5)
        {
            return m2;
        }
        if (ton < ZweiDrittel)
        {
            return m1 + (m2 - m1) * (ZweiDrittel - ton) * 6.0;
        }
        return m1;
    }

    // Python colorsys.rgb_to_hls (Stand Python 3.12+, gh-106498)
    private static (double, double, double) RgbZuHls(double r, double g, double b)
    {
        double maxc = Math.Max(r, Math.Max(g, b));
        double minc = Math.Min(r, Math.Min(g, b));
        double sumc = maxc + minc;
        double rangec = maxc - minc;
        double l = sumc / 2.0;
        if (minc == maxc)
        {
            return (0.0, l, 0.0);
        }
        double s = l <= 0.5 ? rangec / sumc : rangec / (2.0 - maxc - minc);
        double rc = (maxc - r) / rangec;
        double gc = (maxc - g) / rangec;
        double bc = (maxc - b) / rangec;
        double h;
        if (r == maxc)
        {
            h = bc - gc;
        }
        else if (g == maxc)
        {
            h = 2.0 + rc - bc;
        }
        else
        {
            h = 4.0 + gc - rc;
        }
        h = PyMod(h / 6.0, 1.0);
        return (h, l, s);
    }

    // ------------------------------------------------------------ Pruefen und Speichern

    /// <summary>True, wenn alle neun Werte innerhalb der Grenzen liegen.</summary>
    public static bool IstGueltig(EigeneWerte werte) =>
        Teil(werte).All(r => r.H >= Farbton.Min && r.H <= Farbton.Max && r.S >= Saettigung.Min
            && r.S <= Saettigung.Max && r.L >= Helligkeit.Min && r.L <= Helligkeit.Max);

    private static Regler[] Teil(EigeneWerte w) => [w.Akzent1, w.Akzent2, w.Hintergrund];

    /// <summary>
    /// Python: valid_custom - prueft einen gespeicherten Satz eigener Farben. Null, wenn
    /// irgendetwas fehlt, falsch getippt (auch true/false, Text, NaN) oder ausserhalb der
    /// Grenzen ist. Kommazahlen werden wie in Python gerundet (auf gerade Zahl).
    /// </summary>
    public static EigeneWerte? Pruefen(JsonNode? wert)
    {
        if (wert is not JsonObject objekt)
        {
            return null;
        }
        var ergebnis = new Regler[3];
        for (int i = 0; i < Teile.Count; i++)
        {
            if (!objekt.TryGetPropertyValue(Teile[i].Id, out var dreier) || dreier is not JsonArray liste || liste.Count != 3)
            {
                return null;
            }
            var zahlen = new int[3];
            for (int k = 0; k < 3; k++)
            {
                if (!Zahl(liste[k], out var z) || double.IsNaN(z) || z < Kanaele[k].Min || z > Kanaele[k].Max)
                {
                    return null;
                }
                zahlen[k] = Runden(z);
            }
            ergebnis[i] = new Regler(zahlen[0], zahlen[1], zahlen[2]);
        }
        return new EigeneWerte(ergebnis[0], ergebnis[1], ergebnis[2]);
    }

    // Nur echte Zahlen (kein true/false, kein Text); sehr grosse Zahlen werden unendlich
    private static bool Zahl(JsonNode? knoten, out double zahl)
    {
        zahl = 0;
        if (knoten is not JsonValue wert || wert.GetValueKind() != JsonValueKind.Number)
        {
            return false;
        }
        if (wert.TryGetValue(out double d))
        {
            zahl = d;
            return true;
        }
        zahl = double.Parse(wert.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Python: saved_custom - gueltige eigene Farben je Darstellung aus dem Wert "eigene_farben".</summary>
    public static IReadOnlyDictionary<string, EigeneWerte> Gespeichert(JsonNode? eintrag)
    {
        var ergebnis = new Dictionary<string, EigeneWerte>(StringComparer.Ordinal);
        if (eintrag is JsonObject objekt)
        {
            foreach (var darstellung in (string[])[Farbwelt.Dunkel, Farbwelt.Hell])
            {
                if (Pruefen(objekt[darstellung]) is { } werte)
                {
                    ergebnis[darstellung] = werte;
                }
            }
        }
        return ergebnis;
    }

    /// <summary>Speicherform eines Werte-Satzes: {"akzent1": [h, s, l], ...}.</summary>
    public static JsonObject AlsJson(EigeneWerte werte)
    {
        var objekt = new JsonObject();
        var teile = Teil(werte);
        for (int i = 0; i < Teile.Count; i++)
        {
            objekt[Teile[i].Id] = new JsonArray(teile[i].H, teile[i].S, teile[i].L);
        }
        return objekt;
    }

    /// <summary>
    /// Python: save_custom (nur der neue Wert) - der Wert fuer "eigene_farben" nach dem Speichern
    /// von <paramref name="werte"/> (null oder ungueltig = Darstellung zuruecksetzen). Alle anderen
    /// Schluessel des bisherigen Werts bleiben unveraendert erhalten. Liefert null, wenn danach
    /// nichts mehr gespeichert ist (dann den Schluessel "eigene_farben" entfernen).
    /// </summary>
    public static JsonObject? SpeicherWert(JsonNode? bisher, string darstellung, EigeneWerte? werte)
    {
        var neu = bisher is JsonObject alt ? (JsonObject)alt.DeepClone() : new JsonObject();
        if (werte is not null && IstGueltig(werte))
        {
            neu[darstellung] = AlsJson(werte);
        }
        else
        {
            neu.Remove(darstellung);
        }
        return neu.Count > 0 ? neu : null;
    }

    // ------------------------------------------------------------ Startwerte und Zustand

    /// <summary>
    /// Python: custom_start - Startwerte der Regler: Knopfverlauf der Grundfarbe und
    /// Hauptflaeche des Hintergrunds der Farbwelt (unbekannte Kennungen = Standard).
    /// </summary>
    public static EigeneWerte Start(string darstellung, string grundfarbe, string hintergrund)
    {
        var g = Farbwelt.Grundfarben.FirstOrDefault(x => x.Id == grundfarbe) ?? Farbwelt.Grundfarben[0];
        var h = Farbwelt.Hintergruende.FirstOrDefault(x => x.Id == hintergrund) ?? Farbwelt.Hintergruende[0];
        var bg = darstellung == Farbwelt.Hell ? h.Hell[0] : h.Dunkel[0];
        return new EigeneWerte(HexZuHsl(g.Primaer.Item1), HexZuHsl(g.Primaer.Item2), HexZuHsl(bg));
    }

    /// <summary>Python: is_start - stehen die Regler genau auf dem Startwert der Farbwelt?</summary>
    public static bool IstStart(EigeneWerte werte, string darstellung, string grundfarbe, string hintergrund) =>
        werte == Start(darstellung, grundfarbe, hintergrund);

    /// <summary>
    /// Python: values_palette - Palette fuer Reglerwerte: am Startwert exakt die Farbwelt,
    /// sonst die Ableitungsregel. Fuer Vorschau und Warnhinweis.
    /// </summary>
    public static Palette WertePalette(EigeneWerte werte, string darstellung, string grundfarbe, string hintergrund) =>
        IstStart(werte, darstellung, grundfarbe, hintergrund)
            ? Farbwelt.Berechne(darstellung, grundfarbe, hintergrund)
            : Palette(werte);

    /// <summary>
    /// Python: custom_state - "aktiv" (eigene Farben gespeichert, Regler darauf), "start"
    /// (nichts gespeichert, Regler auf den Startwerten der gespeicherten Farbwelt) oder
    /// "veraendert". <paramref name="gespeichert"/>: gespeicherte eigene Farben dieser
    /// Darstellung (null = keine); Grundfarbe/Hintergrund = die gespeicherte Farbwelt.
    /// </summary>
    public static string Zustand(EigeneWerte regler, EigeneWerte? gespeichert, string darstellung,
        string grundfarbe, string hintergrund)
    {
        var vergleich = gespeichert ?? Start(darstellung, grundfarbe, hintergrund);
        if (regler != vergleich)
        {
            return ZustandVeraendert;
        }
        return gespeichert is not null ? ZustandAktiv : ZustandStart;
    }

    /// <summary>Python: custom_state_text - Zustandszeile ueber den Reglern.</summary>
    public static string ZustandText(EigeneWerte regler, EigeneWerte? gespeichert, string darstellung,
        string grundfarbe, string hintergrund)
    {
        var zustand = Zustand(regler, gespeichert, darstellung, grundfarbe, hintergrund);
        if (zustand == ZustandVeraendert)
        {
            return ZustandVeraendertText;
        }
        return string.Format(CultureInfo.InvariantCulture, zustand == ZustandAktiv ? ZustandAnText : ZustandAusText,
            DarstellungName(darstellung));
    }

    /// <summary>
    /// Python: custom_to_save - was "Speichern" ablegt: null (nur die Farbwelt, keine eigenen
    /// Farben), wenn die Regler genau auf dem Startwert der (angeklickten oder gespeicherten)
    /// Farbwelt stehen, sonst die Reglerwerte (null, wenn ungueltig).
    /// </summary>
    public static EigeneWerte? ZuSpeichern(EigeneWerte? werte, string darstellung, string grundfarbe, string hintergrund)
    {
        if (werte is null || IstStart(werte, darstellung, grundfarbe, hintergrund))
        {
            return null;
        }
        return IstGueltig(werte) ? werte : null;
    }

    /// <summary>
    /// Python: tile_sets_sliders - soll ein Klick auf eine Farbwelt-Kachel nur die Regler setzen?
    /// Ja, wenn eigene Farben gespeichert sind, schon eine Kachel angeklickt wurde oder die Regler
    /// nicht auf dem Startwert der gespeicherten Farbwelt stehen.
    /// </summary>
    public static bool KachelSetztRegler(string darstellung, EigeneWerte regler, bool eigeneGespeichert,
        bool weltAngeklickt, string grundfarbe, string hintergrund) =>
        eigeneGespeichert || weltAngeklickt || !IstStart(regler, darstellung, grundfarbe, hintergrund);

    // ------------------------------------------------------------ Palette

    private static string Stufe(int ton, int saettigung, int helligkeit, double abstand, double faktor) =>
        HslZuHex(ton, saettigung * faktor, Math.Max(0, Math.Min(100, helligkeit + abstand)));

    /// <summary>
    /// Python: custom_palette - alle Farben aus den drei Reglern (Ableitungsregel). Die
    /// Verlaeufe "danger" und "success" sind fest wie in jeder Farbwelt (in Python bleiben sie
    /// in GRADIENTS stehen).
    /// </summary>
    public static Palette Palette(EigeneWerte werte)
    {
        var (ton, satt, hell) = (werte.Hintergrund.H, werte.Hintergrund.S, werte.Hintergrund.L);
        var grund = HslZuHex(ton, satt, hell);
        var textDunkel = Farbwelt.DunkelFest[0].Item2;
        var textHell = Farbwelt.HellText[0].Item2;
        bool istHell = Farbwelt.Kontrast(textHell, grund) > Farbwelt.Kontrast(textDunkel, grund);
        var c = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in Farbwelt.DunkelFest)
        {
            c[k] = v;
        }
        c["on_accent"] = "#FFFFFF";
        c["bg"] = grund;
        if (istHell)
        {
            foreach (var (k, v) in Farbwelt.HellText)
            {
                c[k] = v;
            }
        }
        string[] fest = istHell ? ["text", "text_soft"] : ["text", "text_soft", .. Schriftfarben];
        string[] flaechen = [];
        foreach (var spreizung in Spreizungen)
        {
            if (istHell)
            {
                foreach (var (k, abstand, faktor) in StufenHell)
                {
                    c[k] = Stufe(ton, satt, hell, abstand * spreizung, faktor);
                }
                c["card"] = HslZuHex(ton, satt, hell + (100 - hell) * 0.9);
            }
            else
            {
                foreach (var (k, abstand, faktor) in StufenDunkel)
                {
                    c[k] = Stufe(ton, satt, hell, abstand * spreizung, faktor);
                }
                c["sidebar"] = HslZuHex(ton, Math.Min(100, satt * 1.05), hell * 0.66);
            }
            flaechen = Farbwelt.Flaechen.Select(k => c[k]).ToArray();
            if (fest.All(k => flaechen.All(f => Farbwelt.Kontrast(c[k], f) >= Farbwelt.MindestKontrast)))
            {
                break;
            }
        }
        var ziel = istHell ? "#000000" : "#FFFFFF";
        foreach (var (k, stufe, faktor) in istHell ? NebenHell : NebenDunkel)
        {
            c[k] = Farbwelt.AbtoenenBis(HslZuHex(ton, satt * faktor, stufe), ziel, flaechen, Farbwelt.MindestKontrast);
        }
        if (istHell)
        {
            foreach (var k in Schriftfarben)
            {
                c[k] = Farbwelt.LesbarAuf(c[k], flaechen, Farbwelt.MindestKontrast);
            }
        }
        var erste = HslZuHex(werte.Akzent1.H, werte.Akzent1.S, werte.Akzent1.L);
        var zweite = HslZuHex(werte.Akzent2.H, werte.Akzent2.S, werte.Akzent2.L);
        foreach (var (k, r) in (ReadOnlySpan<(string, Regler)>)[("accent", werte.Akzent1), ("accent2", werte.Akzent2)])
        {
            var start = HslZuHex(r.H, r.S, istHell ? Math.Min(r.L, 45) : Math.Max(r.L, 60));
            c[k] = Farbwelt.AbtoenenBis(start, ziel, flaechen, Farbwelt.MindestKontrast);
        }
        // Python: _apply_control_colors
        var bedien = Farbwelt.Flaechen.Select(k => c[k]).ToArray();
        c["field_border"] = Farbwelt.AbtoenenBis(c["border_hi"], ziel, bedien, Farbwelt.BedienKontrast);
        c["focus"] = Farbwelt.AbtoenenBis(c["accent"], ziel, bedien, Farbwelt.BedienKontrast);
        var verlaeufe = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["primary"] = (erste, zweite),
            ["accent"] = (Farbwelt.Mischen(erste, "#000000", 0.2), Farbwelt.Mischen(zweite, "#000000", 0.2)),
            ["hero"] = (Farbwelt.Mischen(erste, "#000000", 0.4), Farbwelt.Mischen(zweite, "#000000", 0.4)),
            ["danger"] = ("#DC2626", "#BF5811"),
            ["success"] = ("#05875F", "#158293"),
        };
        return new Palette(istHell, c, verlaeufe);
    }

    // ------------------------------------------------------------ Kontrollen und Warnung

    // Python: _CATEGORY_NAMES
    private static readonly (string, string)[] FachbereichNamen =
    [
        ("cyan", "Fachbereich Netzwerk (Cyan)"), ("pink", "Fachbereich Sicherheit (Pink)"),
        ("purple", "Fachbereich Systeme (Lila)"), ("green", "Fachbereich Wirtschaft (Grün)"),
        ("orange", "Fachbereich Datenbanken (Orange)"), ("blue", "Themenfarbe Blau"),
        ("red", "Fehler (Rot)"), ("yellow", "Warnung (Gelb)"),
    ];

    private static readonly string[] FachbereichReihenfolge = ["cyan", "pink", "purple", "green", "orange"];

    /// <summary>
    /// Python: custom_checks - alle Kontrastpruefungen einer Palette, gemessen gegen die
    /// schwaechste Flaeche. <paramref name="karte"/>: Kartenfarben, sonst ohne Karte.
    /// </summary>
    public static IReadOnlyList<KontrastPruefung> Pruefungen(Palette palette, KartenFarben? karte = null)
    {
        var c = palette.Farben;
        var v = palette.Verlaeufe;
        var flaechen = Farbwelt.Flaechen.Select(k => c[k]).ToArray();
        double Schlechteste(string farbe) => flaechen.Min(f => Farbwelt.Kontrast(farbe, f));
        var liste = new List<KontrastPruefung>
        {
            new("Überschriften", Schlechteste(c["text"]), SchriftZiel),
            new("Fließtext", Schlechteste(c["text_soft"]), SchriftZiel),
            new("Nebentext", Math.Min(Schlechteste(c["text_dim"]), Schlechteste(c["muted"])), SchriftZiel),
            new("Knopfschrift auf Akzent 1", Farbwelt.Kontrast(c["on_accent"], v["primary"].Von), SchriftZiel),
            new("Knopfschrift auf Akzent 2", Farbwelt.Kontrast(c["on_accent"], v["primary"].Bis), SchriftZiel),
            new("Bannerschrift", Math.Min(Farbwelt.Kontrast("#FFFFFF", v["hero"].Von), Farbwelt.Kontrast("#FFFFFF", v["hero"].Bis)), SchriftZiel),
            new("Akzent 1 als Schrift (Menü, Links)", Schlechteste(c["accent"]), SchriftZiel),
            new("Akzent 2 als Schrift", Schlechteste(c["accent2"]), SchriftZiel),
            new("Rahmen von Eingabefeldern", Schlechteste(c["field_border"]), FlaechenZiel),
            new("Fokusrahmen", Schlechteste(c["focus"]), FlaechenZiel),
        };
        foreach (var (k, name) in FachbereichNamen)
        {
            liste.Add(new(name + " als Schrift", Schlechteste(c[k]), SchriftZiel));
        }
        if (karte is not null)
        {
            liste.Add(new("Karte: Beschriftung", Farbwelt.Kontrast(karte.Schrift, karte.Boden), SchriftZiel));
            liste.Add(new("Karte: Straßen", Farbwelt.Kontrast(karte.Strasse, karte.Boden), FlaechenZiel));
            liste.Add(new("Karte: Fluss", Farbwelt.Kontrast(karte.Wasser, karte.Boden), FlaechenZiel));
        }
        return liste;
    }

    /// <summary>Python: custom_problems - nur die Pruefungen unter dem Ziel, schlechteste zuerst (stabil).</summary>
    public static IReadOnlyList<KontrastPruefung> Probleme(Palette palette, KartenFarben? karte = null) =>
        Pruefungen(palette, karte).Where(p => p.Wert < p.Ziel).OrderBy(p => p.Wert / p.Ziel).ToList();

    /// <summary>Python: format_ratio - 4.5 -> "4,5:1" (eine Nachkommastelle, abgeschnitten).</summary>
    public static string Verhaeltnis(double wert) => Eine(wert) + ":1";

    private static string Eine(double wert) =>
        (Math.Truncate(wert * 10) / 10.0).ToString("F1", CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>Python: warning_lines - Warnzeilen mit Messwert (hoechstens <paramref name="grenze"/>, dann "… und n weitere").</summary>
    public static IReadOnlyList<string> WarnZeilen(IReadOnlyList<KontrastPruefung> probleme, int grenze = 4)
    {
        var zeilen = probleme.Take(grenze)
            .Select(p => $"{p.Element}: {Verhaeltnis(p.Wert)} (nötig {Verhaeltnis(p.Ziel)})").ToList();
        if (probleme.Count > grenze)
        {
            zeilen.Add(string.Format(CultureInfo.InvariantCulture, WarnWeitere, probleme.Count - grenze));
        }
        return zeilen;
    }

    // Python: _lab - "#RRGGBB" -> CIE-Lab (D65)
    private static (double, double, double) Lab(string farbe)
    {
        static double Linear(double k)
        {
            k /= 255.0;
            return k <= 0.04045 ? k / 12.92 : PythonPow.Pow((k + 0.055) / 1.055, 2.4);
        }
        static double F(double v) => v > 0.008856 ? PythonPow.Pow(v, 1.0 / 3.0) : 7.787 * v + 16.0 / 116.0;
        var (ri, gi, bi) = Farbwelt.HexZuRgb(farbe);
        double r = Linear(ri), g = Linear(gi), b = Linear(bi);
        double x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047;
        double y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        double z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883;
        return (116.0 * F(y) - 16.0, 500.0 * (F(x) - F(y)), 200.0 * (F(y) - F(z)));
    }

    /// <summary>Python: color_distance - Farbabstand Delta E (CIE 1976).</summary>
    public static double Farbabstand(string a, string b)
    {
        var (l1, a1, b1) = Lab(a);
        var (l2, a2, b2) = Lab(b);
        return PythonPow.Pow(PySumme(PythonPow.Pow(l1 - l2, 2), PythonPow.Pow(a1 - a2, 2), PythonPow.Pow(b1 - b2, 2)), 0.5);
    }

    // Python sum() ueber Gleitkommazahlen ab 3.12: Neumaier-Summe (Startwert 0)
    private static double PySumme(params double[] werte)
    {
        double summe = 0.0, korrektur = 0.0;
        foreach (var x in werte)
        {
            double t = summe + x;
            korrektur += Math.Abs(summe) >= Math.Abs(x) ? (summe - t) + x : (x - t) + summe;
            summe = t;
        }
        if (korrektur != 0.0 && double.IsFinite(korrektur))
        {
            summe += korrektur;
        }
        return summe;
    }

    /// <summary>
    /// Python: category_clash - das am schwersten unterscheidbare Paar der fuenf
    /// Fachbereichsfarben, wenn es unter dem Mindestabstand liegt, sonst null.
    /// </summary>
    public static (string Erste, string Zweite, double Abstand)? FachbereichNah(Palette palette)
    {
        var namen = FachbereichNamen.ToDictionary(x => x.Item1, x => x.Item2.Replace("Fachbereich ", "", StringComparison.Ordinal));
        (string, string, double)? schlechtestes = null;
        for (int i = 0; i < FachbereichReihenfolge.Length; i++)
        {
            for (int k = i + 1; k < FachbereichReihenfolge.Length; k++)
            {
                var abstand = Farbabstand(palette.Farben[FachbereichReihenfolge[i]], palette.Farben[FachbereichReihenfolge[k]]);
                if (schlechtestes is null || abstand < schlechtestes.Value.Item3)
                {
                    schlechtestes = (namen[FachbereichReihenfolge[i]], namen[FachbereichReihenfolge[k]], abstand);
                }
            }
        }
        return schlechtestes!.Value.Item3 < FachbereichAbstand ? schlechtestes : null;
    }

    /// <summary>
    /// Python: custom_warning - der ganze Warnhinweis als Zeilen (leer, wenn alles reicht):
    /// erst die Kontrastwerte, dann die Zeile fuer schwer unterscheidbare Fachbereichsfarben.
    /// </summary>
    public static IReadOnlyList<string> Warnung(Palette palette, KartenFarben? karte = null)
    {
        var zeilen = new List<string>();
        var probleme = Probleme(palette, karte);
        if (probleme.Count > 0)
        {
            zeilen.Add(WarnTitel);
            zeilen.AddRange(WarnZeilen(probleme).Select(z => "• " + z));
        }
        if (FachbereichNah(palette) is { } nah)
        {
            zeilen.Add(string.Format(CultureInfo.InvariantCulture, FachbereichNahText, nah.Erste, nah.Zweite,
                Eine(nah.Abstand), Eine(FachbereichAbstand)));
        }
        return zeilen;
    }

    // ------------------------------------------------------------ Regler-Texte

    /// <summary>Python: slider_label - "Akzent 1, Farbton, 210 Grad" (Screenreader/TalkBack).</summary>
    public static string ReglerBeschriftung(string teilName, ReglerKanal kanal, int wert) =>
        string.Create(CultureInfo.InvariantCulture, $"{teilName}, {kanal.Name}, {wert} {kanal.EinheitGesprochen}");

    /// <summary>Python: slider_text - sichtbarer Wert neben dem Regler: "210°", "45 %".</summary>
    public static string ReglerText(ReglerKanal kanal, int wert) =>
        string.Create(CultureInfo.InvariantCulture, $"{wert}{kanal.EinheitKurz}");
}
