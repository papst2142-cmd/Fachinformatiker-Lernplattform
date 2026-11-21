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
    public static ((double X, double Y) Springen, (double X, double Y) Haltung)? Lage(Sticks.Bereich b)
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
        var (g, k) = Groesse(b.Breite, b.Hoehe);
        var x1 = Math.Min(l.Springen.X - (g / 2), l.Haltung.X - (k / 2)) - Rand;
        var y1 = Math.Min(l.Springen.Y - (g / 2), l.Haltung.Y - (k / 2)) - Rand;
        var x2 = Math.Max(l.Springen.X + (g / 2), l.Haltung.X + (k / 2)) + Rand;
        var y2 = Math.Max(l.Springen.Y + (g / 2), l.Haltung.Y + (k / 2)) + Rand;
        return (x1, y1, x2, y2);
    }

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
