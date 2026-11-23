using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 1.6.0 (Sammelliste 444, Weisungen P12-359/P12-360, Nico: „ich sicht als kleines symbol ebenfalls links übern
/// Joystik“): am Handy und Tablet der runde Knopf der Handlung ueber dem linken Stick (gross, Symbol Tuer, Bus oder Hand) und
/// rechts daneben etwas hoeher das kleine Symbol der Ich-Sicht, gespiegelt zu Springen und Haltung ueber dem rechten Stick
/// (<see cref="HaltungKnopf"/>, gleiche Groessen). Hier steht nur die Logik ohne Oberflaeche.
/// </summary>
public static class HandlungKnopf
{
    /// <summary>So lange (Sekunden) halten, bis die Auswahl aller Handlungen erscheint (wie beim Knopf „Haltung“).</summary>
    public const double LangAb = HaltungKnopf.LangAb;

    /// <summary>Durchmesser (Punkte) von Handlung und Ich-Sicht: wie Springen und Haltung (Handy 56/40, Tablet 64/46).</summary>
    public static (double Handlung, double Ich) Groesse(double breite, double hoehe) => HaltungKnopf.Groesse(breite, hoehe);

    /// <summary>
    /// Mittelpunkte (Punkte der Spielflaeche) von Handlung und Ich-Sicht: der linke Stick liegt unten links in der Ecke, die
    /// Handlung steht mit <see cref="HaltungKnopf.Rand"/> Abstand ueber seinem Kreis am linken Rand, die Ich-Sicht rechts daneben
    /// etwas hoeher; keiner liegt im Kreis des Sticks. Ist ueber dem Kreis kein Platz (sehr flaches Bild), stehen beide rechts
    /// neben ihm. Kein Platz: null.
    /// </summary>
    public static ((double X, double Y) Handlung, (double X, double Y) Ich)? Lage(Sticks.Bereich b)
    {
        const double neben = 14;
        const double hoch = 18;
        var rand = HaltungKnopf.Rand;
        var (g, k) = Groesse(b.Breite, b.Hoehe);
        var (sx, sy, sr) = Sticks.Lage(0, b.Hoehe, Sticks.Seite.Links, b with { Sperre = default, SperreLinks = default });
        var ha = (X: rand + (g / 2), Y: sy - sr - rand - (g / 2));
        var ich = (X: ha.X + (g / 2) + neben + (k / 2), Y: ha.Y - Math.Clamp(ha.Y - (k / 2) - (b.Oben + 4), 0, hoch));
        if (Math.Min(ha.Y - (g / 2), ich.Y - (k / 2)) >= b.Oben + 4 && ich.X + (k / 2) <= b.Breite / 2)
        {
            return (ha, ich);
        }
        // sehr flaches Bild: beide rechts neben dem Kreis, die Handlung auf seiner Hoehe
        var hn = (X: sx + sr + rand + (g / 2), Y: Math.Min(sy, b.Hoehe - b.Unten - rand - (g / 2)));
        var inn = (X: hn.X + (g / 2) + neben + (k / 2), Y: hn.Y - Math.Clamp(hn.Y - (k / 2) - (b.Oben + 4), 0, hoch));
        return Math.Min(hn.Y - (g / 2), inn.Y - (k / 2)) < b.Oben + 4 || inn.X + (k / 2) > b.Breite / 2 ? null : (hn, inn);
    }

    /// <summary>
    /// Welle A 1.6.0 (Weisung P12-361, Nico: „wesentlich schöner, wenn das sich alles um die Finger abspielt“): Mitte des runden
    /// Handy-Symbols (Groesse wie die Ich-Sicht) am Handy und Tablet rechts neben dem linken Stick (Nico 1a, Weisung P12-362), auf
    /// der Hoehe seiner Mitte (der Stick in der Ecke); laege es dort auf der Leiste unten, steht es knapp ueber ihr. Kein Platz in
    /// der linken Haelfte: null.
    /// </summary>
    public static (double X, double Y)? Telefon(Sticks.Bereich b)
    {
        var rand = HaltungKnopf.Rand;
        var (_, k) = Groesse(b.Breite, b.Hoehe);
        var frei = b with { Sperre = default, SperreLinks = default, SperreTelefon = default };
        var (sx, sy, sr) = Sticks.Lage(0, b.Hoehe, Sticks.Seite.Links, frei);
        // die anderen runden Knoepfe (Handlung und Ich-Sicht; Springen und Haltung rechts nur bei sehr schmalem Bild), je schon mit Rand
        var andere = new[] { HaltungKnopf.Sperre(frei), Sperre(frei) }.Where(r => r != default).ToArray();
        var x0 = sx + sr + rand + (k / 2);
        // erst gleich neben dem Stick; liegt dort ein anderer Knopf, gleich rechts neben ihm oder ueber ihm
        var wahl = new List<(double X, double Y)> { (x0, sy) };
        foreach (var r in andere)
        {
            wahl.Add((Math.Max(x0, r.X2 + (k / 2)), sy));
            wahl.Add((x0, r.Y1 - (k / 2)));
        }
        foreach (var (x, y1) in wahl)
        {
            var y = y1;
            if (b.LeisteBis > b.LeisteVon && x + (k / 2) + rand > b.LeisteVon && x - (k / 2) - rand < b.LeisteBis)
            {
                // auf der Leiste unten: knapp ueber ihr
                y = Math.Min(y, b.Hoehe - b.Unten - rand - (k / 2));
            }
            var inHaelfte = x + (k / 2) <= b.Breite / 2;
            var dx = x - sx;
            var dy = y - sy;
            var ausserKreis = Math.Sqrt((dx * dx) + (dy * dy)) >= sr + (k / 2) + rand - 1e-9;
            var frei2 = !andere.Any(r => x + (k / 2) > r.X1 && x - (k / 2) < r.X2 && y + (k / 2) > r.Y1 && y - (k / 2) < r.Y2);
            if (inHaelfte && ausserKreis && frei2 && y - (k / 2) >= b.Oben + 4 && y + (k / 2) <= b.Hoehe)
            {
                return (x, y);
            }
        }
        return null;
    }

    /// <summary>Flaeche des Handy-Symbols mit Rand (x1, y1, x2, y2), die die Sticks frei halten; default = kein Symbol.</summary>
    public static (double X1, double Y1, double X2, double Y2) TelefonSperre(Sticks.Bereich b)
    {
        if (Telefon(b) is not { } m)
        {
            return default;
        }
        var (_, k) = Groesse(b.Breite, b.Hoehe);
        var r = (k / 2) + HaltungKnopf.Rand;
        return (m.X - r, m.Y - r, m.X + r, m.Y + r);
    }

    /// <summary>Flaeche beider Knoepfe mit Rand (x1, y1, x2, y2), die der linke Stick frei haelt; default = keine Knoepfe.</summary>
    public static (double X1, double Y1, double X2, double Y2) Sperre(Sticks.Bereich b)
    {
        if (Lage(b) is not { } l)
        {
            return default;
        }
        var (g, k) = Groesse(b.Breite, b.Hoehe);
        var rand = HaltungKnopf.Rand;
        return (Math.Min(l.Handlung.X - (g / 2), l.Ich.X - (k / 2)) - rand, Math.Min(l.Handlung.Y - (g / 2), l.Ich.Y - (k / 2)) - rand,
            Math.Max(l.Handlung.X + (g / 2), l.Ich.X + (k / 2)) + rand, Math.Max(l.Handlung.Y + (g / 2), l.Ich.Y + (k / 2)) + rand);
    }
}
