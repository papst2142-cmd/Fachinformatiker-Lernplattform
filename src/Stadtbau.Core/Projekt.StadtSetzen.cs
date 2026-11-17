using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.56.2 (Fund Nico 373, Weisungen P12-295 und P12-295b): die heutige Stadt auf einen Platz setzen, mit Vorschau und
/// Bestaetigung. „Heutige Stadt“ ist alles, was auf der Karte steht (E1): die belegten Plaetze (gleiche Regel wie „Stadtteil
/// einsetzen“, <see cref="Belegt"/>) bilden ein Rechteck; es rueckt als Ganzes, sodass seine Ecke oben links auf dem gewaehlten
/// Platz liegt. Die Karte darf dabei groesser werden (Fall A, Karte 64 × 64) oder gleich bleiben (Fall B, Karte schon gross).
/// Vorschau (<see cref="StadtSetzenPruefen"/>) und Schritt (<see cref="StadtSetzen"/>) nutzen dieselbe Rechnung
/// (<see cref="StadtSetzenPlanen"/>). Verschoben wird nur ueber <see cref="KarteVerschieben.Alles"/> mit den festen Listen;
/// ein unbekannter Teil, ein Platz, den es nicht gibt, eine Stadt, die ueber den Rand ragen wuerde, oder ein Platz, auf dem sie
/// schon liegt, ist ein Grund (nichts geaendert, nie still). Ein Schritt (Rueckgaengig bitgleich).
/// </summary>
public sealed partial class Projekt
{
    /// <summary>Plan fuer „Stadt setzen“: Grund (null = geht), Versatz in Feldern und Lage der Stadt vorher und nachher (in Feldern).</summary>
    public sealed record StadtSetzenPlan(string? Grund, int Platz, int Breite, int Tiefe, (int X, int Y) Versatz,
        (int X1, int Y1, int X2, int Y2) Vorher, (int X1, int Y1, int X2, int Y2) Nachher)
    {
        public bool Geht => Grund is null;
    }

    /// <summary>
    /// Wo die heutige Stadt liegt: das Rechteck der belegten Plaetze (in Feldern). Eine leere Karte zaehlt als Platz 1.
    /// </summary>
    public static (int X1, int Y1, int X2, int Y2) StadtBereich(PyObjekt aussen)
    {
        var (b, h) = Aussen.Groesse(aussen);
        var belegt = Enumerable.Range(1, Plaetze.Anzahl(b, h)).Where(p => Belegt(aussen, Plaetze.Lage(p, b)) is not null).ToList();
        if (belegt.Count == 0)
        {
            return (0, 0, Plaetze.Seite, Plaetze.Seite);
        }
        var lagen = belegt.Select(p => Plaetze.Lage(p, b)).ToList();
        return (lagen.Min(l => l.X), lagen.Min(l => l.Y), lagen.Max(l => l.X) + Plaetze.Seite, lagen.Max(l => l.Y) + Plaetze.Seite);
    }

    /// <summary>Platz, auf dem die heutige Stadt beginnt (Ecke oben links ihres Bereichs).</summary>
    public int StadtPlatz()
    {
        var (b, h) = Aussen.Groesse(AussenStand);
        var bereich = StadtBereich(AussenStand);
        return Math.Max(1, Plaetze.Bei(bereich.X1 + 1, bereich.Y1 + 1, b, h));
    }

    /// <summary>Vorschau: was „Stadt setzen“ taete (aendert nichts).</summary>
    public StadtSetzenPlan StadtSetzenPruefen(int breite, int tiefe, int platz) => StadtSetzenPlanen(breite, tiefe, platz);

    /// <summary>Gleiche Rechnung fuer Vorschau und Schritt.</summary>
    internal StadtSetzenPlan StadtSetzenPlanen(int breite, int tiefe, int platz)
    {
        var (altB, altH) = Aussen.Groesse(AussenStand);
        var vorher = StadtBereich(AussenStand);
        StadtSetzenPlan Nein(string grund) => new(grund, platz, breite, tiefe, (0, 0), vorher, vorher);
        if (NurLesen)
        {
            return Nein(Texte.NurLesen);
        }
        if (!Plaetze.Seiten.Contains(breite) || !Plaetze.Seiten.Contains(tiefe))
        {
            return Nein(Texte.VergroessernGroesse);
        }
        if (breite < altB - 1e-9 || tiefe < altH - 1e-9)
        {
            return Nein(Texte.VergroessernKleiner);
        }
        if (!Plaetze.Gibt(platz, breite, tiefe))
        {
            return Nein(Texte.PlatzFehlt(platz, Plaetze.Anzahl(breite, tiefe)));
        }
        var (zx, zy) = Plaetze.Lage(platz, breite);
        var versatz = (X: zx - vorher.X1, Y: zy - vorher.Y1);
        var nachher = (vorher.X1 + versatz.X, vorher.Y1 + versatz.Y, vorher.X2 + versatz.X, vorher.Y2 + versatz.Y);
        if (nachher.Item3 > breite || nachher.Item4 > tiefe)
        {
            return Nein(Texte.StadtUeberRand(platz, (vorher.X2 - vorher.X1) / Plaetze.Seite, (vorher.Y2 - vorher.Y1) / Plaetze.Seite));
        }
        if (versatz == (0, 0) && breite <= altB + 1e-9 && tiefe <= altH + 1e-9)
        {
            return Nein(Texte.StadtSchonDort(platz));
        }
        if (KarteVerschieben.Unbekannt(AussenStand) is { Count: > 0 } u)
        {
            return Nein(Texte.TeilUnbekannt(u[0]));
        }
        return new(null, platz, breite, tiefe, versatz, vorher, nachher);
    }

    /// <summary>Stadt setzen (ein Schritt): Karte auf die Groesse bringen und alles um den Versatz verschieben; Plan zurueck.</summary>
    public StadtSetzenPlan StadtSetzen(int breite, int tiefe, int platz)
    {
        var plan = StadtSetzenPlanen(breite, tiefe, platz);
        if (!plan.Geht)
        {
            return plan;
        }
        var (altB, altH) = Aussen.Groesse(AussenStand);
        var groesser = breite > altB + 1e-9 || tiefe > altH + 1e-9;
        AussenAendern(StadtOrt, groesser ? Texte.KarteVergroessernSchritt(breite, tiefe, platz) : Texte.StadtVerschobenSchritt(platz), a =>
        {
            KarteVerschieben.Alles(a, plan.Versatz.X, plan.Versatz.Y);
            if (groesser)
            {
                var welt = Aussen.Welt(a);
                Py.Setzen(welt, "breite", Bearbeitung.Zahl(breite));
                Py.Setzen(welt, "hoehe", Bearbeitung.Zahl(tiefe));
            }
            return 0;
        }, ohneAutomatik: true);
        return plan;
    }
}
