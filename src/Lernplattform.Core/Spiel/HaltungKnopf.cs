using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 0.99.0 (Sammelliste 400, Weisungen P12-319/P12-320, Nico 1a 2a 3b): zwei runde Knoepfe am rechten Daumen (Handy und
/// Tablet). Hier steht nur die Logik ohne Oberflaeche: was der Knopf „Haltung“ bei kurzem Tippen und langem Druecken tut, Groesse
/// und Lage ueber dem Bereich des rechten Sticks (die lesbare Symbolfarbe steht in <c>Core/Design/Symbolfarbe.cs</c>). Liegt im Core (Namensraum ohne Probe), weil es den
/// Spiel-Kern (<c>Tastenbelegung</c>) kennt; die Engine bleibt ohne ihn.
/// </summary>
public static class HaltungKnopf
{
    /// <summary>So lange (Sekunden) halten, bis der Druck als „lang“ gilt (Kriechen).</summary>
    public const double LangAb = 0.5;

    /// <summary>
    /// Aufgabe (<see cref="Tastenbelegung.Ducken"/> oder <see cref="Tastenbelegung.Kriechen"/>, an <see cref="StadtSpiel.Ducken"/> bzw.
    /// <see cref="StadtSpiel.Kriechen"/>) fuer den Knopf „Haltung“; null = nichts tun. Stehen: tippen = ducken, lang = kriechen.
    /// Geduckt: tippen = aufstehen (Ducken schaltet zurueck), lang = kriechen. Kriechend: tippen = aufstehen, lang = bleibt.
    /// </summary>
    public static string? Aufgabe(Koerperhaltung jetzt, bool lang) => (jetzt, lang) switch
    {
        (Koerperhaltung.Kriechen, true) => null,
        (Koerperhaltung.Kriechen, false) => Tastenbelegung.Kriechen,
        (_, true) => Tastenbelegung.Kriechen,
        _ => Tastenbelegung.Ducken,
    };

    /// <summary>
    /// Durchmesser (Punkte) von Springen und Haltung: Handy 56/40, Tablet (kuerzere Seite ab 600) 64/46. Welle A 1.0.0 (Sammelliste
    /// 405, Nico: „etwas kleiner“); bis A 0.99.0 64/46 und 72/52 (<see cref="AltFuerTests"/>).
    /// </summary>
    public static (double Springen, double Haltung) Groesse(double breite, double hoehe) => Alt.Value
        ? (Math.Min(breite, hoehe) >= 600 ? (72, 52) : (64, 46))
        : (Math.Min(breite, hoehe) >= 600 ? (64, 46) : (56, 40));

    /// <summary>Abstand der Knoepfe zum Rand des Bilds und zur Leiste (Punkte).</summary>
    public const double Rand = 16;

    /// <summary>
    /// Mittelpunkte (in Punkten der Spielflaeche) von Springen und Haltung. Welle A 1.2.0 (Sammelliste 412, Weisung P12-332, Nico:
    /// „Der Joystick sollte da sitzen und die Knoepfe darueber“): der rechte Stick liegt unten rechts in der Ecke, Springen steht mit
    /// <see cref="Rand"/> Abstand ueber seinem Kreis am rechten Rand, Haltung links daneben etwas hoeher; kein Knopf liegt im
    /// Kreis des Sticks; ist ueber dem Kreis kein Platz (sehr flaches Bild), stehen beide links neben ihm. Bis A 1.1.0 (Gegenprobe <see cref="EckeFuerTests"/>, Welle A 1.0.0, Sammelliste 405, Nico: „weiter nach unten
    /// in die Ecken“): Springen unten rechts in der Ecke (<see cref="Rand"/> zum Rand), Haltung links daneben etwas hoeher auf dem
    /// Daumenbogen. Ragt Haltung dort bis an die Leiste (schmales Bild oder aufgeklappte Leiste), stehen beide ueber der Leiste.
    /// Den Platz der Knoepfe haelt der rechte Stick frei (<see cref="Sperre"/>). Kein Platz (sehr flaches Bild, Haltung in der
    /// linken Haelfte): null. Bis A 0.99.0 (Gegenprobe <see cref="AltFuerTests"/>): ueber der Zone des rechten Sticks.
    /// </summary>
    public static Lagen? Lage(Sticks.Bereich b)
    {
        if (!Zwei.Value && !Alt.Value && !Ecke.Value)
        {
            return LageBogen(b);
        }
        return LageZwei(b) is { } l ? new Lagen(l.Springen, l.Haltung, null) : null;
    }

    /// <summary>Lage der zwei Knoepfe bis A 1.6.0 (Gegenproben <see cref="ZweiKnoepfeFuerTests"/>, <see cref="EckeFuerTests"/>, <see cref="AltFuerTests"/>).</summary>
    private static ((double X, double Y) Springen, (double X, double Y) Haltung)? LageZwei(Sticks.Bereich b)
    {
        var (g, k) = Groesse(b.Breite, b.Hoehe);
        if (Alt.Value)
        {
            var zone = Sticks.ZoneOben(b);
            var sAlt = (X: b.Breite - 20 - (g / 2), Y: zone - 14 - (g / 2));
            var hochAlt = NurSchraeg.Value ? 22 : Math.Clamp(sAlt.Y - (k / 2) - (b.Oben + 4), 0, 22);
            var hAlt = (X: sAlt.X - (g / 2) - 18 - (k / 2), Y: sAlt.Y - hochAlt);
            return Math.Min(sAlt.Y - (g / 2), hAlt.Y - (k / 2)) < b.Oben + 4 || hAlt.X - (k / 2) < b.Breite / 2 ? null : (sAlt, hAlt);
        }
        const double neben = 14;
        const double hoch = 18;
        if (!Ecke.Value)
        {
            // Welle A 1.2.0 (Sammelliste 412, Nico: „Der Joystick sollte da sitzen und die Knoepfe darueber“): der rechte Stick
            // hat die Ecke unten rechts (so tief wie seit A 1.0.0), die Knoepfe stehen ueber seinem Kreis, Springen am rechten Rand
            var (_, sy, sr) = Sticks.Lage(b.Breite, b.Hoehe, Sticks.Seite.Rechts, b with { Sperre = default });
            var sp = (X: b.Breite - Rand - (g / 2), Y: sy - sr - Rand - (g / 2));
            var hochNeu = Math.Clamp(sp.Y - (k / 2) - (b.Oben + 4), 0, hoch);
            var ha = (X: sp.X - (g / 2) - neben - (k / 2), Y: sp.Y - hochNeu);
            if (Math.Min(sp.Y - (g / 2), ha.Y - (k / 2)) >= b.Oben + 4 && ha.X - (k / 2) >= b.Breite / 2)
            {
                return (sp, ha);
            }
            // sehr flaches Bild (ueber dem Stick kein Platz): beide links neben seinem Kreis, Springen auf seiner Hoehe
            var (sx, _, _) = Sticks.Lage(b.Breite, b.Hoehe, Sticks.Seite.Rechts, b with { Sperre = default });
            var sn = (X: sx - sr - Rand - (g / 2), Y: Math.Min(sy, b.Hoehe - b.Unten - Rand - (g / 2)));
            var hn = (X: sn.X - (g / 2) - neben - (k / 2), Y: sn.Y - Math.Clamp(sn.Y - (k / 2) - (b.Oben + 4), 0, hoch));
            return Math.Min(sn.Y - (g / 2), hn.Y - (k / 2)) < b.Oben + 4 || hn.X - (k / 2) < b.Breite / 2 ? null : (sn, hn);
        }
        var springen = (X: b.Breite - Rand - (g / 2), Y: b.Hoehe - Rand - (g / 2));
        var haltung = (X: springen.X - (g / 2) - neben - (k / 2), Y: springen.Y - hoch);
        // steht die Leiste unter Haltung oder reicht die Leiste ueber die ganze Breite: beide ueber die Leiste
        var leisteDa = b.LeisteBis <= b.LeisteVon || haltung.X - (k / 2) < b.LeisteBis + Rand;
        if (leisteDa && b.Unten > 0)
        {
            springen.Y = b.Hoehe - b.Unten - Rand - (g / 2);
            haltung.Y = springen.Y - hoch;
        }
        if (Math.Min(springen.Y - (g / 2), haltung.Y - (k / 2)) < b.Oben + 4 || haltung.X - (k / 2) < b.Breite / 2)
        {
            return null;
        }
        return (springen, haltung);
    }

    /// <summary>
    /// Flaeche der zwei Knoepfe mit <see cref="Rand"/> rundherum (Punkte der Spielflaeche: x1, y1, x2, y2), die der rechte Stick frei
    /// haelt; default = keine Knoepfe.
    /// </summary>
    public static (double X1, double Y1, double X2, double Y2) Sperre(Sticks.Bereich b)
    {
        if (Lage(b) is not { } l)
        {
            return default;
        }
        var (g, k) = Groessen(b.Breite, b.Hoehe);
        var kreise = new List<((double X, double Y) M, double D)> { (l.Springen, g), (l.Knien, k) };
        if (l.Liegen is { } liegen)
        {
            kreise.Add((liegen, k));
        }
        var x1 = kreise.Min(c => c.M.X - (c.D / 2)) - Rand;
        var y1 = kreise.Min(c => c.M.Y - (c.D / 2)) - Rand;
        var x2 = kreise.Max(c => c.M.X + (c.D / 2)) + Rand;
        var y2 = kreise.Max(c => c.M.Y + (c.D / 2)) + Rand;
        return (x1, y1, x2, y2);
    }

    /// <summary>
    /// Welle A 1.7.0 (Sammelliste 447, Weisung P12-367, Nico 1a: Anordnung B): Mittelpunkte der drei Knoepfe am rechten Daumen.
    /// Springen (gross) unten rechts in der Ecke (<see cref="Rand"/> zu beiden Raendern, Daumenruhe), Hinknien und Hinlegen (klein)
    /// im Bogen darueber (Winkel <see cref="KnienWinkel"/> und <see cref="LiegenWinkel"/> um die Mitte von Springen, Abstand
    /// <see cref="Rand"/> zwischen den Kreisen). Reicht Hinknien bis an die Leiste (schmales Bild oder aufgeklappte Leiste), stehen alle
    /// drei ueber der Leiste. Den Platz haelt der rechte Stick frei (<see cref="Sperre"/>). Kein Platz (Hinknien in der linken Haelfte
    /// oder ein Knopf unter der Kopfzeile): null.
    /// </summary>
    private static Lagen? LageBogen(Sticks.Bereich b)
    {
        var (g, k) = Groessen(b.Breite, b.Hoehe);
        var sp = (X: b.Breite - Rand - (g / 2), Y: b.Hoehe - Rand - (g / 2));
        var r = (g / 2) + Rand + (k / 2);
        (double X, double Y) Bogen((double X, double Y) m, double grad) =>
            (m.X + (r * Math.Cos(grad * Math.PI / 180)), m.Y - (r * Math.Sin(grad * Math.PI / 180)));
        var knien = Bogen(sp, KnienWinkel);
        var leisteDa = b.LeisteBis <= b.LeisteVon || knien.X - (k / 2) < b.LeisteBis + Rand;
        if (leisteDa && b.Unten > 0)
        {
            sp.Y = b.Hoehe - b.Unten - Rand - (g / 2);
            knien = Bogen(sp, KnienWinkel);
        }
        var liegen = Bogen(sp, LiegenWinkel);
        if (Math.Min(sp.Y - (g / 2), Math.Min(knien.Y, liegen.Y) - (k / 2)) < b.Oben + 4 || knien.X - (k / 2) < b.Breite / 2)
        {
            return null;
        }
        return new Lagen(sp, knien, liegen);
    }

    /// <summary>Welle A 1.7.0: Winkel (Grad, 0 = rechts, gegen den Uhrzeiger) von Hinknien um die Mitte von Springen.</summary>
    public const double KnienWinkel = 165;

    /// <summary>Welle A 1.7.0: Winkel von Hinlegen um die Mitte von Springen (fast senkrecht darueber).</summary>
    public const double LiegenWinkel = 100;

    /// <summary>
    /// Welle A 1.7.0 (Sammelliste 447, Entwurf P12-231): Durchmesser (Punkte) von Springen und den zwei kleinen Knoepfen Hinknien
    /// und Hinlegen: Handy 64/48, Tablet (kuerzere Seite ab 600) 76/56. Bis A 1.6.0 <see cref="Groesse"/> (bleibt fuer die Knoepfe
    /// am linken Daumen, <see cref="HandlungKnopf.Groesse"/>).
    /// </summary>
    public static (double Springen, double Klein) Groessen(double breite, double hoehe) =>
        Zwei.Value || Alt.Value || Ecke.Value ? Groesse(breite, hoehe) : Math.Min(breite, hoehe) >= 600 ? (76, 56) : (64, 48);

    /// <summary>Welle A 1.7.0: Lage der Knoepfe am rechten Daumen (Hinlegen null = zwei Knoepfe wie bis A 1.6.0, Knien = „Haltung“).</summary>
    public readonly record struct Lagen((double X, double Y) Springen, (double X, double Y) Knien, (double X, double Y)? Liegen);

    /// <summary>Welle A 1.7.0: die drei Knoepfe am rechten Daumen (am PC die drei Symbole).</summary>
    public enum Knopf
    {
        Springen,
        Knien,
        Liegen,
    }

    /// <summary>
    /// Welle A 1.7.0 (Sammelliste 447, Nico 2a): Aufgabe (<see cref="Tastenbelegung.Springen"/>, <see cref="Tastenbelegung.Ducken"/>
    /// oder <see cref="Tastenbelegung.Kriechen"/>) eines Knopfs; null = nichts tun. Hinknien: tippen = hinknien (kniend: aufstehen,
    /// liegend: hinknien), gedrueckt halten oder nach unten wischen (<paramref name="lang"/>) = hinlegen (liegend: bleibt). Hinlegen:
    /// tippen = hinlegen (liegend: aufstehen), lang wie tippen ausser liegend (bleibt).
    /// </summary>
    public static string? Aufgabe(Knopf knopf, Koerperhaltung jetzt, bool lang) => knopf switch
    {
        Knopf.Springen => lang ? null : Tastenbelegung.Springen,
        Knopf.Knien when lang => jetzt == Koerperhaltung.Kriechen ? null : Tastenbelegung.Kriechen,
        Knopf.Knien => Tastenbelegung.Ducken,
        _ when lang => jetzt == Koerperhaltung.Kriechen ? null : Tastenbelegung.Kriechen,
        _ => Tastenbelegung.Kriechen,
    };

    /// <summary>
    /// Welle A 1.7.0 (Konzept Punkt 4): das Symbol zeigt die Haltung, in die man als Naechstes wechselt; die aktuelle Haltung
    /// leuchtet. Hinknien zeigt kniend „Aufstehen“, Hinlegen liegend „Aufstehen“.
    /// </summary>
    public static (bool Aufstehen, bool Leuchtet) Zustand(Knopf knopf, Koerperhaltung jetzt) => knopf switch
    {
        Knopf.Knien => (jetzt == Koerperhaltung.Ducken, jetzt == Koerperhaltung.Ducken),
        Knopf.Liegen => (jetzt == Koerperhaltung.Kriechen, jetzt == Koerperhaltung.Kriechen),
        _ => (false, false),
    };

    /// <summary>Welle A 1.7.0 (Nico 2a): so weit (Punkte) nach unten gewischt gilt auf Hinknien als „hinlegen“ (mehr nach unten als zur Seite).</summary>
    public const double WischAb = 24;

    /// <summary>
    /// Welle A 1.7.0 (Sammelliste 447, Nico 4a): am PC drei kleine Symbole unten rechts mit ihrer Taste darunter (von rechts:
    /// Springen, Hinknien, Hinlegen), Durchmesser 52/44, unten Platz fuer die Taste (<see cref="TasteHoehe"/>); reicht Hinlegen bis
    /// an die Leiste, stehen sie ueber ihr. null = kein Platz.
    /// </summary>
    public static Lagen? LageTasten(Sticks.Bereich b)
    {
        var (g, k) = GroessenTasten;
        var sp = (X: b.Breite - Rand - (g / 2), Y: b.Hoehe - Rand - TasteHoehe - (g / 2));
        (double X, double Y) Klein(int nr) => (sp.X - (g / 2) - (nr * AbstandTasten) - (((2 * nr) - 1) * k / 2), sp.Y + ((g - k) / 2));
        var liegen = Klein(2);
        if (b.Unten > 0 && (b.LeisteBis <= b.LeisteVon || liegen.X - (k / 2) < b.LeisteBis + Rand))
        {
            sp.Y = b.Hoehe - b.Unten - Rand - TasteHoehe - (g / 2);
            liegen = Klein(2);
        }
        var knien = Klein(1);
        return sp.Y - (g / 2) < b.Oben + 4 || liegen.X - (k / 2) < b.Breite / 2 ? null : new Lagen(sp, knien, liegen);
    }

    /// <summary>Welle A 1.7.0: Durchmesser (Punkte) der Symbole am PC (Springen, Hinknien und Hinlegen).</summary>
    public static readonly (double Springen, double Klein) GroessenTasten = (52, 44);

    /// <summary>Welle A 1.7.0: Hoehe (Punkte) des Platzes fuer die Taste unter einem Symbol am PC.</summary>
    public const double TasteHoehe = 22;

    /// <summary>Welle A 1.7.0: Abstand (Punkte) zwischen den Symbolen am PC.</summary>
    public const double AbstandTasten = 18;

    private static readonly AsyncLocal<bool> Zwei = new();

    /// <summary>Gegenprobe (nur Tests, AsyncLocal): zwei Knoepfe (Springen, Haltung) ueber dem rechten Stick wie A 1.2.0 bis A 1.6.0.</summary>
    public static bool ZweiKnoepfeFuerTests { get => Zwei.Value; set => Zwei.Value = value; }

    private static readonly AsyncLocal<bool> Alt = new();

    /// <summary>Gegenprobe (nur Tests, AsyncLocal): Groesse und Lage wie bis A 0.99.0.</summary>
    public static bool AltFuerTests { get => Alt.Value; set => Alt.Value = value; }

    private static readonly AsyncLocal<bool> Ecke = new();

    /// <summary>Gegenprobe (nur Tests, AsyncLocal): Knoepfe unten rechts in der Ecke wie A 1.0.0 bis A 1.1.0 (Sammelliste 412).</summary>
    public static bool EckeFuerTests { get => Ecke.Value; set => Ecke.Value = value; }

    private static readonly AsyncLocal<bool> NurSchraeg = new();

    /// <summary>Gegenprobe (nur Tests, AsyncLocal): Haltung immer 22 Punkte schraeg ueber Springen (Stand vor Lauf 1118).</summary>
    public static bool NurSchraegFuerTests { get => NurSchraeg.Value; set => NurSchraeg.Value = value; }
}
