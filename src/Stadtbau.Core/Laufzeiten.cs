using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.45.0 (Weisung P12-212 Punkt 5, Nico a): Laufzeiten zwischen den Orten der Stadt, genau wie das Spiel sie rechnet
/// (<see cref="Stadt.Weg"/>: Breitensuche von Tuer zu Tuer auf dem Stadt-Raster, je Schritt <see cref="Stadt.MinutenJeSchritt"/>
/// Spielminuten, heute 2). Die Spielregel bleibt; das Werkzeug zeigt die Wege und warnt gelb ab <see cref="GelbAb"/> Minuten.
/// Nur Anzeige und gelber Hinweis, nie rot.
/// </summary>
public static class Laufzeiten
{
    /// <summary>Ab so vielen Spielminuten ist ein Weg gelb (nach der kuerzeren Zeit: zu Fuss oder mit dem Bus).</summary>
    public const long GelbAb = 60;

    /// <summary>
    /// Ein Weg zwischen zwei Orten (Kennungen, Namen, Minuten zu Fuss; null = kein Weg). Zu Fuss ist der Weg in beide Richtungen
    /// gleich lang. Mit dem Bus (Welle A 0.70.0, Weisung P12-221) nicht: die Linie faehrt im Kreis. <paramref name="BusHin"/>
    /// (von <paramref name="Von"/> nach <paramref name="Nach"/>) und <paramref name="BusZurueck"/> nur, wenn der Bus schneller
    /// ist, sonst null.
    /// </summary>
    public sealed record Weg(string Von, string Nach, string VonName, string NachName, long? Minuten, long? BusHin = null, long? BusZurueck = null)
    {
        /// <summary>
        /// Die Zeit, die zaehlt: je Richtung die kuerzere (Bus, wenn er schneller ist, sonst zu Fuss), davon die laengere
        /// Richtung (E1: ein Weg ist lang, wenn er in einer Richtung auch mit dem Bus lang ist). null = kein Weg.
        /// </summary>
        public long? Kuerzer => Minuten is { } m ? Math.Max(BusHin ?? m, BusZurueck ?? m) : null;

        /// <summary>Die Zeit mit dem Bus, wenn er in beiden Richtungen schneller ist (= <see cref="Kuerzer"/>), sonst null.</summary>
        public long? Bus => Kuerzer < Minuten ? Kuerzer : null;
    }

    /// <summary>Gegenprobe P12-221: ohne Bus (Stand S 0.45.0).</summary>
    public static bool OhneBusAltFuerTests { get => OhneBusAlt.Value; set => OhneBusAlt.Value = value; }
    private static readonly AsyncLocal<bool> OhneBusAlt = new();

    /// <summary>
    /// Alle Wege zwischen je zwei Orten (jedes Paar einmal), laengste zuerst nach der kuerzeren Zeit, ohne Weg ganz vorn. Leer bei
    /// weniger als zwei Orten. Zu Fuss rechnet <see cref="StadtSpiel.MinutenVon"/> (<see cref="Stadt.Weg"/>), mit dem Bus
    /// <see cref="StadtSpiel.BusLohntVon"/> wie die Ortsliste im Spiel (Abfahrt zu Tagesbeginn <see cref="Stadt.Beginn"/>; die
    /// Wartezeit auf den Takt haengt im Spiel von der Uhrzeit ab, hoechstens <see cref="Busfahrt.TaktHoechstens"/> Minuten).
    /// Die Szene fuer den Bus wird nur gebaut, wenn die Stadt Haltestellen hat (blockiert: im Hintergrund rufen).
    /// </summary>
    public static List<Weg> Rechnen(StadtFassung stand, PyObjekt inhalt, GrafikBibliothek? grafik = null)
    {
        Stadt stadt;
        try
        {
            if (SpielKern.Regeln.Stadt(stand, stand.MitKarte(inhalt), nachsichtig: true) is not Stadt s)
            {
                return [];
            }
            stadt = s;
        }
        catch (Exception fehler) when (fehler is InvalidDataException or IOException or KeyNotFoundException or InvalidCastException or FormatException or OverflowException or SpielFehler)
        {
            // laedt die Stadt nicht, meldet das schon die Pruefung
            return [];
        }
        var spiel = OhneBusAlt.Value ? null : BusSpiel(stand, stadt, grafik);
        var orte = stadt.Regeln.Select(r => (Id: Py.Str(r["id"]), Name: Py.Str(r["name"]))).ToList();
        var wege = new List<Weg>();
        for (var i = 0; i < orte.Count; i++)
        {
            for (var j = i + 1; j < orte.Count; j++)
            {
                var w = stadt.Weg(orte[i].Id, orte[j].Id);
                var fuss = w is null ? (long?)null : checked(w.Schritte * stadt.MinutenJeSchritt);
                var hin = fuss is null ? null : spiel?.BusLohntVon(orte[i].Id, orte[j].Id, stadt.Beginn)?.Bus;
                var zurueck = fuss is null ? null : spiel?.BusLohntVon(orte[j].Id, orte[i].Id, stadt.Beginn)?.Bus;
                wege.Add(new Weg(orte[i].Id, orte[j].Id, orte[i].Name, orte[j].Name, fuss, hin, zurueck));
            }
        }
        return [.. wege.OrderByDescending(w => w.Kuerzer ?? long.MaxValue).ThenBy(w => w.VonName, StringComparer.Ordinal).ThenBy(w => w.NachName, StringComparer.Ordinal)];
    }

    /// <summary>Spiel fuer die Busrechnung (wie die 3D-Ansicht, ohne Netze); null ohne Haltestellen oder wenn die Szene nicht baut.</summary>
    private static StadtSpiel? BusSpiel(StadtFassung stand, Stadt stadt, GrafikBibliothek? grafik)
    {
        if (SpielDaten.Get(stand.Stadtdaten, Haltestellen.Abschnitt) is not List<object?> { Count: > 0 })
        {
            return null;
        }
        try
        {
            using (Nachsicht.Werkzeugansicht())
            {
                var spiel = new StadtSpiel(stadt, StadtbauSpiel.Probestand(stadt.Inhalt), Ansicht3D.MitGebaeuden(grafik, stand), stand.Innenraeume);
                return spiel.Linie is null ? null : spiel;
            }
        }
        catch (Exception fehler) when (fehler is InvalidDataException or KeyNotFoundException or InvalidCastException or FormatException or OverflowException or SpielFehler or StadtbauFehler or ArgumentException)
        {
            // baut die Szene nicht, meldet das die Pruefung; ohne Bus nur zu Fuss
            return null;
        }
    }

    /// <summary>
    /// Gelber Hinweis: EINER fuer alle Wege, die auch mit dem Bus <see cref="GelbAb"/> Minuten oder laenger dauern (Zahl und
    /// laengster Weg mit beiden Zeiten, an der Tuer seines ersten Orts); die einzelnen Wege zeigt die Karte „Laufzeiten“. Stadt 1
    /// hat Wege bis ueber 250 Minuten, ein Hinweis je Weg waere eine lange gelbe Liste (E1).
    /// </summary>
    public static List<StadtbauFund> Pruefen(StadtFassung stand, PyObjekt inhalt, PyObjekt aussen, GrafikBibliothek? grafik = null) =>
        Pruefen(Rechnen(stand, inhalt, grafik), aussen);

    /// <summary>Wie oben zu schon gerechneten Wegen.</summary>
    public static List<StadtbauFund> Pruefen(List<Weg> wege, PyObjekt aussen)
    {
        var lang = wege.Where(w => w.Kuerzer >= GelbAb).ToList();
        if (lang.Count == 0)
        {
            return [];
        }
        var w = lang[0];
        var (x, y) = TuerVon(aussen, w.Von);
        return [new StadtbauFund(Texte.LaufzeitLang(lang.Count, w.VonName, w.NachName, w.Minuten!.Value, w.BusHin, w.BusZurueck), Texte.LaufzeitLangTipp, false, x, y)];
    }

    private static (double?, double?) TuerVon(PyObjekt aussen, string ort)
    {
        var sd = Aussen.Stadtdaten(aussen);
        foreach (var abschnitt in new[] { "zugaenge_neu", "zugaenge" })
        {
            if (SpielDaten.Get(SpielDaten.Get(sd, abschnitt) as PyObjekt ?? [], ort) is PyObjekt z && z["tuer"] is List<object?> t)
            {
                return (Py.F(t[0]), Py.F(t[1]));
            }
        }
        return (null, null);
    }
}
