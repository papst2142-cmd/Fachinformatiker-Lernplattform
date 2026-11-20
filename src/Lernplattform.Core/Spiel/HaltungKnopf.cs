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

    /// <summary>Durchmesser (Punkte) von Springen und Haltung: Handy 64/46, Tablet (kuerzere Seite ab 600) 72/52 (Nico 2a).</summary>
    public static (double Springen, double Haltung) Groesse(double breite, double hoehe) =>
        Math.Min(breite, hoehe) >= 600 ? (72, 52) : (64, 46);

    /// <summary>
    /// Mittelpunkte (in Punkten der Spielflaeche) von Springen und Haltung (Nico 1a): Springen aussen rechts knapp ueber der Zone
    /// des rechten Sticks (<see cref="Sticks.ZoneOben"/>), Haltung schraeg links darueber auf dem Daumenbogen. Beide liegen ganz
    /// ueber der Zone und unter der Kopfzeile. In einem sehr flachen Bild (E1, Lauf 1118: Emulator 640 x 320 Punkte) steigt
    /// Haltung nur so weit schraeg nach oben, wie Platz ist (bis gleich hoch wie Springen); reicht auch das nicht, null (dann
    /// gelten die Menue-Eintraege). Alte Lage nur schraeg: <see cref="NurSchraegFuerTests"/>.
    /// </summary>
    public static ((double X, double Y) Springen, (double X, double Y) Haltung)? Lage(Sticks.Bereich b)
    {
        var (g, k) = Groesse(b.Breite, b.Hoehe);
        var zone = Sticks.ZoneOben(b);
        var springen = (X: b.Breite - 20 - (g / 2), Y: zone - 14 - (g / 2));
        var hoch = NurSchraeg.Value ? 22 : Math.Clamp(springen.Y - (k / 2) - (b.Oben + 4), 0, 22);
        var haltung = (X: springen.X - (g / 2) - 18 - (k / 2), Y: springen.Y - hoch);
        if (Math.Min(springen.Y - (g / 2), haltung.Y - (k / 2)) < b.Oben + 4 || haltung.X - (k / 2) < b.Breite / 2)
        {
            return null;
        }
        return (springen, haltung);
    }

    private static readonly AsyncLocal<bool> NurSchraeg = new();

    /// <summary>Gegenprobe (nur Tests, AsyncLocal): Haltung immer 22 Punkte schraeg ueber Springen (Stand vor Lauf 1118).</summary>
    public static bool NurSchraegFuerTests { get => NurSchraeg.Value; set => NurSchraeg.Value = value; }
}
