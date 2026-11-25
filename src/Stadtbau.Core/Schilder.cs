namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.64.0 (Weisung P12-380 Punkt 415, P12-381 Antwort 5a und Zusatz Moebel-Beschriftungen): Lage der Schilder im
/// Grundriss. Jede Beschriftung steht ganz (nie gekuerzt) auf einem Schild; gewaehlt wird die erste Stelle, die ganz im
/// sichtbaren Bereich liegt und kein Sperr-Rechteck (Raumnamen, Punkte der Personen) und kein schon gesetztes Schild
/// ueberdeckt. Reihenfolge der Stellen: im eigenen Stueck (nur wenn <see cref="Schild.Innen"/> und es ganz hineinpasst),
/// rechts, links, darueber, darunter, dann in Stufen weiter weg. Passt keine Stelle, nimmt sie die mit der kleinsten
/// Ueberdeckung und meldet das (<see cref="Gesetzt.Frei"/> = false); der Test fordert dann Frei in allen Raeumen.
/// Alle Werte in Bildpunkten; reine Rechnung (Start-Test und Unit-Test rechnen dasselbe).
/// </summary>
public static class Schilder
{
    /// <summary>Nur fuer die Gegenprobe in Tests und im Start-Test: Schilder wie vor S 0.64.0 (gleich rechts am Punkt bzw.
    /// oben links im Stueck, ohne Platzregel). AsyncLocal (Sammelliste 307): gilt nur im Ablauf, der ihn setzt.</summary>
    public static readonly AsyncLocal<bool> AltFuerTests = new();

    /// <summary>Rechteck in Bildpunkten.</summary>
    public readonly record struct Kasten(double X, double Y, double Breite, double Hoehe)
    {
        public double Rechts => X + Breite;

        public double Unten => Y + Hoehe;

        /// <summary>Flaeche der Ueberdeckung (Grenze 0,5 Bildpunkte fuer die Rundung: Beruehren zaehlt nicht).</summary>
        public double Ueberdeckung(Kasten o)
        {
            var b = Math.Min(Rechts, o.Rechts) - Math.Max(X, o.X);
            var h = Math.Min(Unten, o.Unten) - Math.Max(Y, o.Y);
            return b > 0.5 && h > 0.5 ? b * h : 0;
        }

        /// <summary>Liegt das Rechteck ganz in <paramref name="sicht"/> (Grenze 0,5 Bildpunkte)?</summary>
        public bool InnerhalbVon(Kasten sicht) =>
            X >= sicht.X - 0.5 && Y >= sicht.Y - 0.5 && Rechts <= sicht.Rechts + 0.5 && Unten <= sicht.Unten + 0.5;
    }

    /// <summary>Ein Schild: Anker (Punkt einer Person oder Stueck), Groesse des Schilds, darf es im Anker stehen.</summary>
    public readonly record struct Schild(string Text, Kasten Anker, double Breite, double Hoehe, bool Innen);

    /// <summary>Ergebnis je Schild in der Reihenfolge der Eingabe; Frei = ganz sichtbar und ohne Ueberdeckung;
    /// ImAnker = steht im eigenen Stueck (dann ohne Linie zum Stueck gezeichnet).</summary>
    public readonly record struct Gesetzt(string Text, Kasten Lage, bool Frei, bool ImAnker);

    /// <summary>Abstand zwischen Anker und Schild ausserhalb.</summary>
    public const double Abstand = 3;

    /// <summary>Stufen weiter weg (je eine Schildhoehe bzw. halbe Schildbreite).</summary>
    public const int Stufen = 6;

    /// <summary>Schilder der Reihe nach setzen (frueher = Vorrang).</summary>
    public static List<Gesetzt> Setzen(IReadOnlyList<Schild> schilder, IReadOnlyList<Kasten> sperren, Kasten sicht)
    {
        var belegt = new List<Kasten>(sperren);
        var ergebnis = new Gesetzt?[schilder.Count];
        if (!AltFuerTests.Value)
        {
            // zuerst jede Beschriftung, die frei in ihr eigenes Stueck passt (sie steht dann wie bisher im Stueck)
            for (var i = 0; i < schilder.Count; i++)
            {
                var s = schilder[i];
                foreach (var (k, imAnker) in Stellen(s))
                {
                    if (!imAnker)
                    {
                        break;
                    }
                    if (k.InnerhalbVon(sicht) && belegt.All(b => b.Ueberdeckung(k) == 0))
                    {
                        ergebnis[i] = new Gesetzt(s.Text, k, true, true);
                        belegt.Add(k);
                        break;
                    }
                }
            }
        }
        for (var i = 0; i < schilder.Count; i++)
        {
            var s = schilder[i];
            if (ergebnis[i] is not null)
            {
                continue;
            }
            Gesetzt g;
            if (AltFuerTests.Value)
            {
                var alt = s.Innen
                    ? new Kasten(s.Anker.X + 3, s.Anker.Y + 1, Math.Min(s.Breite, Math.Max(1, s.Anker.Breite - 6)), s.Hoehe)
                    : new Kasten(s.Anker.Rechts + Abstand, s.Anker.Y + (s.Anker.Hoehe / 2) - 8, s.Breite, s.Hoehe);
                var frei = alt.Breite >= s.Breite - 0.5 && alt.InnerhalbVon(sicht) && belegt.All(b => b.Ueberdeckung(alt) == 0);
                g = new Gesetzt(s.Text, alt, frei, s.Innen);
            }
            else
            {
                g = Waehlen(s, belegt, sicht);
            }
            belegt.Add(g.Lage);
            ergebnis[i] = g;
        }
        return [.. ergebnis.Select(g => g!.Value)];
    }

    /// <summary>Moegliche Stellen eines Schilds in der Reihenfolge der Wahl.</summary>
    public static IEnumerable<(Kasten Lage, bool ImAnker)> Stellen(Schild s)
    {
        var a = s.Anker;
        double w = s.Breite, h = s.Hoehe;
        if (s.Innen && w <= a.Breite && h <= a.Hoehe)
        {
            yield return (new Kasten(a.X + ((a.Breite - w) / 2), a.Y + ((a.Hoehe - h) / 2), w, h), true);
            yield return (new Kasten(a.X, a.Y, w, h), true);
            yield return (new Kasten(a.Rechts - w, a.Unten - h, w, h), true);
        }
        var mitteY = a.Y + (a.Hoehe / 2) - (h / 2);
        var mitteX = a.X + (a.Breite / 2) - (w / 2);
        for (var stufe = 0; stufe <= Stufen; stufe++)
        {
            var dy = stufe * (h + 2);
            var dx = stufe * (w / 2);
            yield return (new Kasten(a.Rechts + Abstand, mitteY - dy, w, h), false);
            yield return (new Kasten(a.X - Abstand - w, mitteY - dy, w, h), false);
            yield return (new Kasten(mitteX - dx, a.Y - Abstand - h - (stufe * (h + 2)), w, h), false);
            yield return (new Kasten(mitteX - dx, a.Unten + Abstand + (stufe * (h + 2)), w, h), false);
            if (stufe > 0)
            {
                yield return (new Kasten(a.Rechts + Abstand, mitteY + dy, w, h), false);
                yield return (new Kasten(a.X - Abstand - w, mitteY + dy, w, h), false);
                yield return (new Kasten(mitteX + dx, a.Y - Abstand - h - (stufe * (h + 2)), w, h), false);
                yield return (new Kasten(mitteX + dx, a.Unten + Abstand + (stufe * (h + 2)), w, h), false);
            }
        }
    }

    private static Gesetzt Waehlen(Schild s, List<Kasten> belegt, Kasten sicht)
    {
        Kasten? beste = null;
        var besteImAnker = false;
        var kosten = double.MaxValue;
        foreach (var (k, imAnker) in Stellen(s))
        {
            var ueber = belegt.Sum(b => b.Ueberdeckung(k));
            if (ueber == 0 && k.InnerhalbVon(sicht))
            {
                return new Gesetzt(s.Text, k, true, imAnker);
            }
            // Ersatz: in die Sicht geschoben, kleinste Ueberdeckung
            var innen = new Kasten(Math.Clamp(k.X, sicht.X, Math.Max(sicht.X, sicht.Rechts - k.Breite)),
                Math.Clamp(k.Y, sicht.Y, Math.Max(sicht.Y, sicht.Unten - k.Hoehe)), k.Breite, k.Hoehe);
            var u = belegt.Sum(b => b.Ueberdeckung(innen));
            if (u < kosten)
            {
                kosten = u;
                beste = innen;
                besteImAnker = imAnker;
            }
        }
        var lage = beste ?? new Kasten(s.Anker.X, s.Anker.Y, s.Breite, s.Hoehe);
        return new Gesetzt(s.Text, lage, kosten == 0 && lage.InnerhalbVon(sicht), besteImAnker);
    }
}
