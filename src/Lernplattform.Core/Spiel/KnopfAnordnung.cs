using System.Globalization;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 1.9.0 (Sammelliste 450, Weisungen P12-375 bis P12-377, Nico 1a 2b 3a 4a 5a+b, Frage 1 a): die sechs runden Knoepfe am
/// Handy und Tablet (rechts Springen, Hinknien, Hinlegen; links Handlung, Ich-Sicht, Spiel-Handy) lassen sich selbst anordnen, je
/// Knopf mit eigener Groesse von <see cref="GroesseMin"/> bis <see cref="GroesseMax"/> Prozent (100 = heute, Mitte des Reglers).
/// X oben rechts und die Sticks bleiben fest. Hier steht nur die Logik ohne Oberflaeche: Standard (genau die Lagen von
/// <see cref="HaltungKnopf"/> und <see cref="HandlungKnopf"/>), Schutzregeln (<see cref="Pruefen"/>), Einpassen auf einen anderen
/// Bildschirm (<see cref="Wirksam"/>), Raster und Hilfslinien, Speicherung je Ausrichtung als Abstand zur naechsten Ecke.
/// Gespeichert wird nur ueber <see cref="Einstellungen.Aendern"/> (Schluessel <see cref="Schluessel"/>, nur eine abweichende
/// Anordnung; <see cref="SchluesselRaster"/> nur „aus“); ein kaputter Wert gilt als Standard (Einstellungen.cs unveraendert).
/// Am PC gibt es das nicht (feste Symbole mit Tasten).
/// </summary>
public static class KnopfAnordnung
{
    /// <summary>Die sechs verschiebbaren Knoepfe.</summary>
    public enum Knopf
    {
        Springen,
        Knien,
        Liegen,
        Handlung,
        Ich,
        Telefon,
    }

    /// <summary>Alle sechs in fester Reihenfolge (Einpassen, Speicherung, Tests).</summary>
    public static IReadOnlyList<Knopf> Alle { get; } = [Knopf.Springen, Knopf.Knien, Knopf.Liegen, Knopf.Handlung, Knopf.Ich, Knopf.Telefon];

    public const string Schluessel = "knoepfe_anordnung";

    public const string SchluesselRaster = "knoepfe_raster";

    public const int GroesseMin = 50;

    public const int GroesseMax = 150;

    public const int GroesseSchritt = 5;

    public const int GroesseVorgabe = 100;

    /// <summary>Kleinste Trefferflaeche (Durchmesser in Punkten, Nico 1a P12-241): bei kleinerem Bild bleibt die Flaeche zum Tippen so gross.</summary>
    public const double TrefferMin = 40;

    /// <summary>Luft (Punkte) zwischen den Trefferflaechen zweier Knoepfe.</summary>
    public const double Luft = 8;

    /// <summary>Raster (Punkte), auf das ein gezogener Knopf einrastet, wenn das Raster an ist.</summary>
    public const double Raster = 8;

    /// <summary>Abstand (Punkte) zum Bildrand, zum sicheren Bereich und unter der Kopfzeile.</summary>
    public const double RandBild = 4;

    /// <summary>Ausrichtung des Bilds: quer (breiter als hoch) oder hoch; jede hat ihre eigene Anordnung (Nico 3a).</summary>
    public static string Ausrichtung(Sticks.Bereich b) => b.Breite >= b.Hoehe ? "quer" : "hoch";

    /// <summary>Lage (Mitte in Punkten der Spielflaeche) und Groesse in Prozent eines Knopfs.</summary>
    public readonly record struct Platz(double X, double Y, int Prozent = GroesseVorgabe);

    /// <summary>Gespeicherte Lage: Ecke der erlaubten Flaeche („lo“, „ro“, „lu“, „ru“), Abstand der Mitte zu ihr, Groesse.</summary>
    public readonly record struct Gemerkt(string Ecke, double Dx, double Dy, int Prozent);

    /// <summary>Was an einer Stelle nicht erlaubt ist.</summary>
    public enum Fehler
    {
        Keiner,
        Aussen,
        Leiste,
        Ueberlappt,
        StickPlatz,
    }

    // ---------------------------------------------------------------- Groessen

    /// <summary>Durchmesser bei 100 % (heute): rechts wie <see cref="HaltungKnopf.Groessen"/>, links wie <see cref="HandlungKnopf.Groesse"/>.</summary>
    public static double Standardgroesse(Knopf k, double breite, double hoehe)
    {
        var (g, kl) = HaltungKnopf.Groessen(breite, hoehe);
        var (hg, hk) = HandlungKnopf.Groesse(breite, hoehe);
        return k switch
        {
            Knopf.Springen => g,
            Knopf.Knien or Knopf.Liegen => kl,
            Knopf.Handlung => hg,
            _ => hk,
        };
    }

    /// <summary>Durchmesser des sichtbaren Knopfs bei <paramref name="prozent"/>.</summary>
    public static double Durchmesser(Knopf k, Sticks.Bereich b, int prozent) => Standardgroesse(k, b.Breite, b.Hoehe) * GroesseBegrenzen(prozent) / 100.0;

    /// <summary>Durchmesser der Trefferflaeche: nie unter <see cref="TrefferMin"/>.</summary>
    public static double Treffer(double durchmesser) => Math.Max(durchmesser, TrefferMin);

    /// <summary>Prozent in den Bereich legen und auf den Schritt runden.</summary>
    public static int GroesseBegrenzen(int prozent) =>
        Math.Clamp((int)Math.Round(prozent / (double)GroesseSchritt, MidpointRounding.AwayFromZero) * GroesseSchritt, GroesseMin, GroesseMax);

    // ---------------------------------------------------------------- Flaechen

    /// <summary>
    /// Erlaubte Flaeche (x1, y1, x2, y2) fuer die Trefferflaechen: das Bild ohne sicheren Bereich des Geraets (Kerbe, Leisten;
    /// <paramref name="sicher"/> = links, oben, rechts, unten) und ohne Kopfzeile, je <see cref="RandBild"/> Abstand.
    /// </summary>
    public static (double X1, double Y1, double X2, double Y2) Erlaubt(Sticks.Bereich b, (double L, double O, double R, double U) sicher) =>
        (sicher.L + RandBild, Math.Max(b.Oben, sicher.O) + RandBild, b.Breite - sicher.R - RandBild, b.Hoehe - sicher.U - RandBild);

    /// <summary>Flaeche der Leiste unten mit <see cref="HaltungKnopf.Rand"/> rundherum (default = keine Leiste).</summary>
    public static (double X1, double Y1, double X2, double Y2) Leiste(Sticks.Bereich b)
    {
        if (b.Unten <= 0)
        {
            return default;
        }
        var rand = HaltungKnopf.Rand;
        return b.LeisteBis > b.LeisteVon
            ? (b.LeisteVon - rand, b.Hoehe - b.Unten - rand, b.LeisteBis + rand, b.Hoehe)
            : (0, b.Hoehe - b.Unten - rand, b.Breite, b.Hoehe);
    }

    // ---------------------------------------------------------------- Standard

    /// <summary>
    /// Standard-Anordnung (A 1.8.0): die Lagen von <see cref="HaltungKnopf.Lage"/>, <see cref="HandlungKnopf.Lage"/> und
    /// <see cref="HandlungKnopf.Telefon"/>, alle 100 %. Knoepfe ohne Platz fehlen (sehr flaches Bild).
    /// </summary>
    public static Dictionary<Knopf, Platz> Standard(Sticks.Bereich b)
    {
        var frei = b with { Sperre = default, SperreLinks = default, SperreTelefon = default, Flaechen = default };
        var aus = new Dictionary<Knopf, Platz>();
        if (HaltungKnopf.Lage(frei) is { } r)
        {
            aus[Knopf.Springen] = new(r.Springen.X, r.Springen.Y);
            aus[Knopf.Knien] = new(r.Knien.X, r.Knien.Y);
            if (r.Liegen is { } l)
            {
                aus[Knopf.Liegen] = new(l.X, l.Y);
            }
        }
        if (HandlungKnopf.Lage(frei) is { } h)
        {
            aus[Knopf.Handlung] = new(h.Handlung.X, h.Handlung.Y);
            aus[Knopf.Ich] = new(h.Ich.X, h.Ich.Y);
        }
        if (HandlungKnopf.Telefon(frei) is { } t)
        {
            aus[Knopf.Telefon] = new(t.X, t.Y);
        }
        return aus;
    }

    // ---------------------------------------------------------------- Schutzregeln

    /// <summary>
    /// Prueft eine Anordnung (alle Knoepfe, die sie enthaelt): jede Trefferflaeche ganz in der erlaubten Flaeche, nicht auf der
    /// Leiste, zwei Trefferflaechen mit mindestens <see cref="Luft"/> Abstand, und in jeder Haelfte unten Platz fuer einen ganzen
    /// Stick. Rueckgabe: der erste Fehler und der Knopf (bei Stickplatz null).
    /// </summary>
    public static (Fehler Fehler, Knopf? Knopf) Pruefen(IReadOnlyDictionary<Knopf, Platz> a, Sticks.Bereich b, (double L, double O, double R, double U) sicher = default)
    {
        foreach (var k in Alle)
        {
            if (a.TryGetValue(k, out var p) && PruefenEiner(a, k, p, b, sicher, ohneStick: true) is var f and not Fehler.Keiner)
            {
                return (f, k);
            }
        }
        return StickPlatz(a, b, Sticks.Seite.Links) && StickPlatz(a, b, Sticks.Seite.Rechts) ? (Fehler.Keiner, null) : (Fehler.StickPlatz, null);
    }

    /// <summary>Waere Knopf <paramref name="k"/> an Platz <paramref name="p"/> erlaubt (die anderen wie in <paramref name="a"/>)?</summary>
    public static Fehler PruefenEiner(IReadOnlyDictionary<Knopf, Platz> a, Knopf k, Platz p, Sticks.Bereich b, (double L, double O, double R, double U) sicher = default, bool ohneStick = false)
    {
        var t = Treffer(Durchmesser(k, b, p.Prozent)) / 2;
        var (x1, y1, x2, y2) = Erlaubt(b, sicher);
        if (p.X - t < x1 - 1e-6 || p.X + t > x2 + 1e-6 || p.Y - t < y1 - 1e-6 || p.Y + t > y2 + 1e-6)
        {
            return Fehler.Aussen;
        }
        var leiste = Leiste(b);
        if (leiste.X2 > leiste.X1 && Sticks.KreisInRechteck(p.X, p.Y, t, leiste))
        {
            return Fehler.Leiste;
        }
        foreach (var (andere, q) in a)
        {
            if (andere == k)
            {
                continue;
            }
            var ta = Treffer(Durchmesser(andere, b, q.Prozent)) / 2;
            var (dx, dy) = (p.X - q.X, p.Y - q.Y);
            if (Math.Sqrt((dx * dx) + (dy * dy)) < t + ta + Luft - 1e-6)
            {
                return Fehler.Ueberlappt;
            }
        }
        if (!ohneStick)
        {
            var neu = new Dictionary<Knopf, Platz>(a) { [k] = p };
            if (!StickPlatz(neu, b, Sticks.Seite.Links) || !StickPlatz(neu, b, Sticks.Seite.Rechts))
            {
                return Fehler.StickPlatz;
            }
        }
        return Fehler.Keiner;
    }

    /// <summary>
    /// Flaechen (je Knopf ein Quadrat um die Trefferflaeche mit <see cref="HaltungKnopf.Rand"/>), die die Sticks frei halten
    /// (<see cref="Sticks.Bereich.Flaechen"/>).
    /// </summary>
    public static Sticks.Sperrflaechen Sperren(IReadOnlyDictionary<Knopf, Platz> a, Sticks.Bereich b) =>
        new(Alle.Where(a.ContainsKey).Select(k =>
        {
            var p = a[k];
            var r = (Treffer(Durchmesser(k, b, p.Prozent)) / 2) + HaltungKnopf.Rand;
            return (p.X - r, p.Y - r, p.X + r, p.Y + r);
        }));

    /// <summary>
    /// Hat ein ganzer Stick (Radius wie <see cref="Sticks.Lage"/>) in seiner Haelfte Platz, ohne einen Knopf zu beruehren, und kann
    /// ein Daumen ihn starten? Wie im Spiel: der Daumen setzt im unteren Teil auf (ab <see cref="Sticks.ZoneOben"/>, nicht auf einem
    /// Knopf), der Stick rueckt von den Knoepfen weg bis hoechstens an die Kopfzeile (Lauf 1225: auf niedrigen Bildern verlangte
    /// die alte Regel die Mitte unter ZoneOben, dann war schon der Standard ungueltig).
    /// </summary>
    public static bool StickPlatz(IReadOnlyDictionary<Knopf, Platz> a, Sticks.Bereich b, Sticks.Seite seite) => StickStelle(a, b, seite) is not null;

    /// <summary>Eine Stelle (Mitte und Radius), an der ein ganzer Stick in seiner Haelfte Platz hat (null = keine; Anzeige im Bearbeiten-Modus).</summary>
    public static (double X, double Y, double R)? StickStelle(IReadOnlyDictionary<Knopf, Platz> a, Sticks.Bereich b, Sticks.Seite seite)
    {
        var halb = b.Breite / 2;
        var r = Math.Min(Sticks.Radius, Math.Min(b.FreiHoehe / 2, halb / 2));
        var (links, rechts) = seite == Sticks.Seite.Links ? (r, halb - r) : (halb + r, b.Breite - r);
        var mit = b with { Sperre = default, SperreLinks = default, SperreTelefon = default, Flaechen = Sperren(a, b) };
        var oben = b.Oben + r;
        var unten = b.Hoehe - b.Unten - r;
        if (rechts < links || !DaumenStart(mit, seite))
        {
            return null;
        }
        return Sticks.NaechsteFreie((links + rechts) / 2, unten, r, seite, mit, links, rechts, oben, unten) is { } m ? (m.X, m.Y, r) : null;
    }

    /// <summary>Gibt es in der Haelfte eine Stelle (Raster 2 Punkte), an der ein Daumen einen Stick dieser Seite startet?</summary>
    private static bool DaumenStart(Sticks.Bereich mit, Sticks.Seite seite)
    {
        var halb = mit.Breite / 2;
        var (x1, x2) = seite == Sticks.Seite.Links ? (0.0, halb - 1) : (halb, mit.Breite);
        for (var y = Math.Ceiling(Sticks.ZoneOben(mit)); y <= mit.Hoehe; y += 2)
        {
            for (var x = x1; x <= x2; x += 2)
            {
                if (Sticks.SeiteFuer(x, y, mit) == seite)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Obere Kante des Hinweises im Bearbeiten-Modus (Weisung P12-378): die Wunschhoehe, ausser der Hinweis (x, Breite, Hoehe)
    /// verdeckt dort einen der <paramref name="texte"/> („Platz fuer den Stick“, je x, y, Breite, Hoehe); dann die naechste freie
    /// Hoehe (Schritt 4) zwischen <paramref name="oben"/> und <paramref name="unten"/> (Unterkante). Gibt es keine, bleibt die Wunschhoehe.
    /// </summary>
    public static double HinweisOben(double wunsch, double oben, double unten, double x, double breite, double hoehe,
        IReadOnlyList<(double X, double Y, double W, double H)> texte)
    {
        bool Frei(double y) => !texte.Any(t => t.X < x + breite && x < t.X + t.W && t.Y < y + hoehe && y < t.Y + t.H);
        if (Frei(wunsch))
        {
            return wunsch;
        }
        double? beste = null;
        for (var y = oben; y + hoehe <= unten; y += 4)
        {
            if (Frei(y) && (beste is not { } b || Math.Abs(y - wunsch) < Math.Abs(b - wunsch)))
            {
                beste = y;
            }
        }
        return beste ?? wunsch;
    }

    // ---------------------------------------------------------------- Raster und Hilfslinien

    /// <summary>Auf das Raster einrasten (Mitte auf ein Vielfaches von <see cref="Raster"/>).</summary>
    public static Platz Einrasten(Platz p) =>
        p with { X = Math.Round(p.X / Raster, MidpointRounding.AwayFromZero) * Raster, Y = Math.Round(p.Y / Raster, MidpointRounding.AwayFromZero) * Raster };

    /// <summary>Hilfslinien: senkrecht (gleiches X) und waagrecht (gleiches Y) wie ein anderer Knopf (auf 0,5 Punkte genau).</summary>
    public static (double? X, double? Y) Hilfslinien(IReadOnlyDictionary<Knopf, Platz> a, Knopf k)
    {
        if (!a.TryGetValue(k, out var p))
        {
            return (null, null);
        }
        double? x = null;
        double? y = null;
        foreach (var (andere, q) in a)
        {
            if (andere == k)
            {
                continue;
            }
            if (Math.Abs(q.X - p.X) < 0.5)
            {
                x = p.X;
            }
            if (Math.Abs(q.Y - p.Y) < 0.5)
            {
                y = p.Y;
            }
        }
        return (x, y);
    }

    /// <summary>
    /// Naechste erlaubte Stelle fuer Knopf <paramref name="k"/> nahe (x, y) (Raster 2 Punkte in der erlaubten Flaeche, kleinster
    /// Weg); null = keine.
    /// </summary>
    public static Platz? NaechsteErlaubte(IReadOnlyDictionary<Knopf, Platz> a, Knopf k, Platz wunsch, Sticks.Bereich b, (double L, double O, double R, double U) sicher = default, bool ohneStick = true)
    {
        if (PruefenEiner(a, k, wunsch, b, sicher, ohneStick) == Fehler.Keiner)
        {
            return wunsch;
        }
        var t = Treffer(Durchmesser(k, b, wunsch.Prozent)) / 2;
        var (x1, y1, x2, y2) = Erlaubt(b, sicher);
        var frei = new List<(double Weg, Platz P)>();
        for (var x = x1 + t; x <= x2 - t + 1e-9; x += 2)
        {
            for (var y = y1 + t; y <= y2 - t + 1e-9; y += 2)
            {
                var p = wunsch with { X = x, Y = y };
                if (PruefenEiner(a, k, p, b, sicher, ohneStick: true) == Fehler.Keiner)
                {
                    frei.Add((((x - wunsch.X) * (x - wunsch.X)) + ((y - wunsch.Y) * (y - wunsch.Y)), p));
                }
            }
        }
        frei.Sort((u, v) => u.Weg.CompareTo(v.Weg));
        if (ohneStick)
        {
            return frei.Count > 0 ? frei[0].P : null;
        }
        // mit Platz fuer die Sticks: die naechste freie Stelle, die auch einen ganzen Stick je Haelfte laesst (hoechstens die 2000
        // naechsten, jede Probe sucht den Platz eines Sticks)
        foreach (var (_, p) in frei.Take(2000))
        {
            if (PruefenEiner(a, k, p, b, sicher) == Fehler.Keiner)
            {
                return p;
            }
        }
        return null;
    }

    // ---------------------------------------------------------------- Speicherung

    /// <summary>Lage als Abstand der Mitte zur naechsten Ecke der erlaubten Flaeche merken.</summary>
    public static Gemerkt Merken(Platz p, Sticks.Bereich b, (double L, double O, double R, double U) sicher = default)
    {
        var (x1, y1, x2, y2) = Erlaubt(b, sicher);
        var links = p.X - x1 <= x2 - p.X;
        var oben = p.Y - y1 <= y2 - p.Y;
        return new Gemerkt((links ? "l" : "r") + (oben ? "o" : "u"), Math.Round(links ? p.X - x1 : x2 - p.X, 1),
            Math.Round(oben ? p.Y - y1 : y2 - p.Y, 1), GroesseBegrenzen(p.Prozent));
    }

    /// <summary>Gemerkte Lage auf diesem Bildschirm (gleicher Abstand zur selben Ecke).</summary>
    public static Platz Ort(Gemerkt g, Sticks.Bereich b, (double L, double O, double R, double U) sicher = default)
    {
        var (x1, y1, x2, y2) = Erlaubt(b, sicher);
        return new Platz(g.Ecke[0] == 'l' ? x1 + g.Dx : x2 - g.Dx, g.Ecke[1] == 'o' ? y1 + g.Dy : y2 - g.Dy, GroesseBegrenzen(g.Prozent));
    }

    /// <summary>
    /// Gespeicherte Anordnungen aus einstellungen.json: je Ausrichtung („quer“, „hoch“) alle sechs Knoepfe mit Ecke, Abstand und
    /// Groesse. Eine Ausrichtung mit fehlendem oder kaputtem Eintrag gilt als Standard (fehlt in der Rueckgabe).
    /// </summary>
    public static Dictionary<string, Dictionary<Knopf, Gemerkt>> Lesen(JsonObject werte)
    {
        var aus = new Dictionary<string, Dictionary<Knopf, Gemerkt>>();
        if (werte[Schluessel] is not JsonObject alle)
        {
            return aus;
        }
        foreach (var ausrichtung in (string[])["quer", "hoch"])
        {
            if (alle[ausrichtung] is not JsonObject ja)
            {
                continue;
            }
            var eine = new Dictionary<Knopf, Gemerkt>();
            foreach (var k in Alle)
            {
                if (ja[Name(k)] is JsonObject o && o["ecke"] is JsonValue e && e.TryGetValue<string>(out var ecke)
                    && ecke is "lo" or "ro" or "lu" or "ru" && Zahl(o["dx"]) is { } dx && Zahl(o["dy"]) is { } dy && dx >= 0 && dy >= 0
                    && dx < 100_000 && dy < 100_000 && o["groesse"] is JsonValue gv && gv.TryGetValue<int>(out var g) && g >= GroesseMin && g <= GroesseMax)
                {
                    eine[k] = new Gemerkt(ecke, dx, dy, GroesseBegrenzen(g));
                }
            }
            if (eine.Count == Alle.Count)
            {
                aus[ausrichtung] = eine;
            }
        }
        return aus;
    }

    private static double? Zahl(JsonNode? n)
    {
        if (n is not JsonValue v)
        {
            return null;
        }
        if (v.TryGetValue<double>(out var d))
        {
            return double.IsFinite(d) ? d : null;
        }
        return v.TryGetValue<int>(out var i) ? i : null;
    }

    /// <summary>Name eines Knopfs im Schluessel (klein, ASCII).</summary>
    public static string Name(Knopf k) => k switch
    {
        Knopf.Springen => "springen",
        Knopf.Knien => "knien",
        Knopf.Liegen => "liegen",
        Knopf.Handlung => "handlung",
        Knopf.Ich => "ich",
        _ => "handy",
    };

    /// <summary>
    /// In einer Aenderung setzen: <paramref name="neu"/> = Anordnung dieser Ausrichtung (null = Standard, Eintrag entfernen;
    /// ist keine Ausrichtung mehr da, verschwindet der ganze Schluessel).
    /// </summary>
    public static void Wert(JsonObject werte, string ausrichtung, IReadOnlyDictionary<Knopf, Gemerkt>? neu)
    {
        var alle = werte[Schluessel] as JsonObject;
        if (neu is null)
        {
            alle?.Remove(ausrichtung);
            if (alle is not null && alle.Count == 0)
            {
                werte.Remove(Schluessel);
            }
            return;
        }
        if (alle is null)
        {
            alle = [];
            werte[Schluessel] = alle;
        }
        var ja = new JsonObject();
        foreach (var k in Alle)
        {
            var g = neu[k];
            ja[Name(k)] = new JsonObject
            {
                ["ecke"] = g.Ecke,
                ["dx"] = Math.Round(g.Dx, 1),
                ["dy"] = Math.Round(g.Dy, 1),
                ["groesse"] = GroesseBegrenzen(g.Prozent),
            };
        }
        alle[ausrichtung] = ja;
    }

    /// <summary>Raster an (Vorgabe) oder aus (nur „aus“ wird gespeichert).</summary>
    public static bool RasterLesen(JsonObject werte) => !(werte[SchluesselRaster] is JsonValue v && v.TryGetValue<bool>(out var an) && !an);

    public static void RasterWert(JsonObject werte, bool an)
    {
        if (an)
        {
            werte.Remove(SchluesselRaster);
        }
        else
        {
            werte[SchluesselRaster] = false;
        }
    }

    // ---------------------------------------------------------------- Aktueller Stand

    private static Dictionary<string, Dictionary<Knopf, Gemerkt>> _gespeichert = [];

    private static bool _raster = true;

    private static readonly AsyncLocal<Dictionary<string, Dictionary<Knopf, Gemerkt>>?> TestWert = new();

    private static readonly AsyncLocal<bool?> TestRaster = new();

    /// <summary>Gespeicherte Anordnungen (setzt die Oberflaeche beim Start und nach jedem Speichern).</summary>
    public static IReadOnlyDictionary<string, Dictionary<Knopf, Gemerkt>> Gespeichert => TestWert.Value ?? _gespeichert;

    /// <summary>Raster an?</summary>
    public static bool RasterAn
    {
        get => TestRaster.Value ?? _raster;
        set => _raster = value;
    }

    /// <summary>Aus einstellungen.json uebernehmen (Programmstart und nach dem Speichern).</summary>
    public static void Uebernehmen(JsonObject werte)
    {
        _gespeichert = Lesen(werte);
        _raster = RasterLesen(werte);
    }

    /// <summary>Nur fuer Tests und den Start-Test (Sammelliste 307: AsyncLocal, wirkt nur im eigenen Ablauf).</summary>
    public static IDisposable FuerTests(Dictionary<string, Dictionary<Knopf, Gemerkt>>? gespeichert, bool? raster = null)
    {
        var (vorher, vorherRaster) = (TestWert.Value, TestRaster.Value);
        TestWert.Value = gespeichert;
        TestRaster.Value = raster;
        return new Zurueck(() =>
        {
            TestWert.Value = vorher;
            TestRaster.Value = vorherRaster;
        });
    }

    /// <summary>
    /// Wirksame eigene Anordnung fuer diesen Bildschirm: null = Standard (keine eigene gespeichert oder sie passt hier nicht).
    /// Jeder Knopf kommt an seine gemerkte Stelle; passt er dort nicht (kleineres Bild, groessere Kopfzeile, aufgeklappte Leiste),
    /// rueckt er nur in der Anzeige an die naechste erlaubte Stelle. Geht das fuer einen Knopf nicht oder bleibt kein Platz fuer die
    /// Sticks, gilt der Standard und <paramref name="passtNicht"/> ist true (einmal Hinweis).
    /// </summary>
    public static Dictionary<Knopf, Platz>? Wirksam(Sticks.Bereich b, (double L, double O, double R, double U) sicher, out bool passtNicht)
    {
        passtNicht = false;
        if (!Gespeichert.TryGetValue(Ausrichtung(b), out var gemerkt))
        {
            return null;
        }
        return Einpassen(gemerkt, b, sicher, out passtNicht);
    }

    /// <summary>Gemerkte Anordnung auf diesen Bildschirm legen (siehe <see cref="Wirksam"/>).</summary>
    public static Dictionary<Knopf, Platz>? Einpassen(IReadOnlyDictionary<Knopf, Gemerkt> gemerkt, Sticks.Bereich b, (double L, double O, double R, double U) sicher, out bool passtNicht)
    {
        passtNicht = false;
        var a = new Dictionary<Knopf, Platz>();
        foreach (var k in Alle)
        {
            if (!gemerkt.TryGetValue(k, out var g))
            {
                passtNicht = true;
                return null;
            }
            if (NaechsteErlaubte(a, k, Ort(g, b, sicher), b, sicher) is not { } p)
            {
                passtNicht = true;
                return null;
            }
            a[k] = p;
        }
        if (Pruefen(a, b, sicher).Fehler != Fehler.Keiner)
        {
            passtNicht = true;
            return null;
        }
        return a;
    }

    /// <summary>Ist diese Anordnung gleich dem Standard (alle Stellen auf 0,5 Punkte genau, alle 100 %)?</summary>
    public static bool IstStandard(IReadOnlyDictionary<Knopf, Platz> a, Sticks.Bereich b)
    {
        var s = Standard(b);
        return s.Count == a.Count && s.All(e => a.TryGetValue(e.Key, out var p) && Math.Abs(p.X - e.Value.X) < 0.5 && Math.Abs(p.Y - e.Value.Y) < 0.5
            && p.Prozent == GroesseVorgabe);
    }

    /// <summary>Text fuer Prozent (Anzeige im Regler).</summary>
    public static string ProzentText(int prozent) => prozent.ToString(CultureInfo.InvariantCulture) + " %";

    private sealed class Zurueck(Action aktion) : IDisposable
    {
        public void Dispose() => aktion();
    }
}
