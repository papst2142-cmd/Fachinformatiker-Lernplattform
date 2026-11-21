using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 1.2.0 (Weisung P12-334 Punkt 2, Nico: Weg B „Neue Grundrisse als Daten“): gespeicherte Moebelplaetze eines
/// Spielstands auf die breiten Grundrisse umrechnen. Gespeichert sind nur die Einrichtungen der Wohnungen (Ereignis
/// <see cref="Ereignisse.Einrichtung"/>, je Moebelstueck x, y, Drehung); alles andere (Startplaetze, Kartons, Personen, Stellen
/// der Auftraege) kommt aus den Inhalten und ist dort schon breit (<see cref="GrundrisseBreit"/>).
/// <para>
/// Regeln: nie etwas aendern, nur anhaengen. Je Wohnung mit gespeicherter Einrichtung, die noch nie umgerechnet wurde, kommt
/// ein neues Ereignis „Einrichtung“ dazu: dieselben Moebel und Boeden, jedes Stueck mit der Verschiebung seiner Wohnung
/// (<see cref="GrundrisseBreit.MoebelUmrechnen"/>), dazu das Feld <see cref="Feld"/> = <see cref="Breit"/>. Ein Stueck, das
/// sich nicht rechnen laesst, bleibt unveraendert in der Liste (nie still weg). Gibt es fuer die Wohnung schon ein Ereignis mit
/// diesem Feld, rechnet nichts mehr um (zweiter Start, spaetere Umbauten im breiten Grundriss).
/// </para>
/// <para>
/// Vorher entsteht immer eine automatische Kopie wie beim Update (<c>sicherungen/vor_update_*</c>); ohne Kopie keine Umrechnung.
/// Geschrieben wird ueber den Spielablauf (<see cref="Spielablauf.Ausfuehren"/>, Lernstand-Regeln: angehaengte Zeilen).
/// </para>
/// </summary>
public static class GrundrissUmrechnung
{
    /// <summary>Feld im Ereignis der Umrechnung.</summary>
    public const string Feld = "grundriss";

    /// <summary>Wert des Felds: die Moebel stehen im breiten Grundriss.</summary>
    public const string Breit = "breit";

    /// <summary>Ausgang: Zahl der umgerechneten Wohnungen, die Kopie (null: nichts zu tun oder keine Kopie) und ein Satz fuers Protokoll.</summary>
    public sealed record Ergebnis(int Wohnungen, string? Kopie, string Protokoll);

    /// <summary>Wohnungen, die schon umgerechnet sind (ein Ereignis mit <see cref="Feld"/> = <see cref="Breit"/>).</summary>
    public static HashSet<string> Umgerechnet(Spielstand stand) =>
        [.. stand.Verlauf.Where(e => e.Art == Spiel.Ereignisse.Einrichtung && Py.Gleich(SpielDaten.Get(e.Daten, Feld), Breit) && e.Daten["wohnung"] is not null)
            .Select(e => Py.Str(e.Daten["wohnung"]))];

    /// <summary>Die Ereignisse der Umrechnung (leer: nichts zu tun). Nur lesen.</summary>
    public static List<(string Art, PyObjekt Daten)> Ereignisse(Spielstand stand, GrundrisseBreit.Daten breit)
    {
        var ergebnis = new List<(string, PyObjekt)>();
        if (stand.Einrichtungen.Count == 0 || breit.Wohnungen.Count == 0)
        {
            return ergebnis;
        }
        var fertig = Umgerechnet(stand);
        var wohnDaten = SpielDaten.Objekt(stand.Inhalt["wohnungen"]);
        foreach (var (wohnung, wert) in stand.Einrichtungen.OrderBy(p => Py.Str(p.Key), StringComparer.Ordinal))
        {
            if (wohnung is not string id || wert is not PyObjekt alt || fertig.Contains(id)
                || !breit.Wohnungen.TryGetValue(id, out var u) || u.Zuwachs <= 0)
            {
                continue;
            }
            var daten = (PyObjekt)Py.Kopie(alt)!;
            if (SpielDaten.Get(alt, "moebel") is PyObjekt moebel)
            {
                var neu = GrundrisseBreit.MoebelUmrechnen(moebel, u, wohnDaten, s => stand.Moebel.Get(s));
                FreiRuecken(stand, id, neu, daten);
                Py.Setzen(daten, "moebel", neu);
            }
            Py.Setzen(daten, "wohnung", id);
            Py.Setzen(daten, Feld, Breit);
            ergebnis.Add((Spiel.Ereignisse.Einrichtung, daten));
        }
        return ergebnis;
    }

    /// <summary>Groesster Weg, um den ein Stueck von einer breiteren Tuer wegrueckt (Einheiten).</summary>
    public const double RueckenHoechstens = 2.0;

    /// <summary>
    /// Die Tueren sind breiter (0,90 m): ein Stueck, das nach der Verschiebung eine Tuer versperrt oder nicht mehr frei steht, rueckt
    /// in kleinen Schritten (0,05) an seiner Wand entlang, sonst quer dazu, bis es frei steht (hoechstens <see cref="RueckenHoechstens"/>).
    /// Findet sich kein freier Platz, bleibt es an der umgerechneten Stelle (nie weg).
    /// </summary>
    private static void FreiRuecken(Spielstand stand, string wohnung, PyObjekt moebel, PyObjekt daten)
    {
        var probe = stand.Kopie();
        probe.Wohnung = wohnung;
        var einrichtung = new PyObjekt { { "moebel", moebel }, { "boeden", SpielDaten.Get(daten, "boeden") as PyObjekt ?? [] } };
        var grundriss = SpielDaten.Objekt(Wohnung.Bleibe(wohnung, stand.Inhalt)!["gebaeude"]);
        foreach (var stueck in moebel.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToList())
        {
            if (moebel[stueck] is not List<object?> { Count: 3 } p || stand.Moebel.Get(stueck) is not { } kennung
                || Wohnung.PlatzProblem(probe, stueck, kennung, p[0], p[1], p[2], einrichtung) == "")
            {
                continue;
            }
            double x = Py.F(p[0]), y = Py.F(p[1]);
            var m = Wohnung.MoebelStueck(kennung, stand.Inhalt);
            var (w, h) = m is null ? (0.0, 0.0) : (Py.F(Wohnung.Groesse(m, p[2]).W), Py.F(Wohnung.Groesse(m, p[2]).H));
            var raum = Wege.RaumBei(x + w / 2, y + h / 2, grundriss)?["id"];
            var gefunden = false;
            for (var schritt = 1; schritt * 0.05 <= RueckenHoechstens + 1e-9 && !gefunden; schritt++)
            {
                var d = Math.Round(schritt * 0.05, 2);
                foreach (var (dx, dy) in new[] { (d, 0.0), (-d, 0.0), (0.0, d), (0.0, -d) })
                {
                    object nx = Flurbreite.Zahl(x + dx, p[0]), ny = Flurbreite.Zahl(y + dy, p[1]);
                    if (Py.Gleich(Wege.RaumBei(Py.F(nx) + w / 2, Py.F(ny) + h / 2, grundriss)?["id"], raum)
                        && Wohnung.PlatzProblem(probe, stueck, kennung, nx, ny, p[2], einrichtung) == "")
                    {
                        p[0] = nx;
                        p[1] = ny;
                        gefunden = true;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Rechnet den Spielstand des Ablaufs um, wenn es etwas zu tun gibt: erst die Kopie (<paramref name="kopie"/>, null oder Fehler =
    /// keine Umrechnung), dann die Ereignisse. <see cref="SpielBelegtAusnahme"/> (anderer Vorgang): nichts geschrieben, der
    /// naechste Start versucht es wieder.
    /// </summary>
    public static Ergebnis Ausfuehren(Spielablauf ablauf, GrundrisseBreit.Daten breit, Func<string?> kopie)
    {
        var ereignisse = Ereignisse(ablauf.Stand, breit);
        if (ereignisse.Count == 0)
        {
            return new Ergebnis(0, null, "");
        }
        string? datei;
        try
        {
            datei = kopie();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            datei = null;
        }
        if (datei is null)
        {
            return new Ergebnis(0, null, $"Breitere Flure: {ereignisse.Count} Wohnung(en) nicht umgerechnet, die Kopie des Lernstands ließ sich nicht anlegen.");
        }
        ablauf.Ausfuehren(ereignisse);
        return new Ergebnis(ereignisse.Count, datei,
            $"Breitere Flure: Möbel in {ereignisse.Count} Wohnung(en) umgerechnet, Kopie vorher: {Uebernahme.SicherungsOrdner}/{Path.GetFileName(datei)}.");
    }
}
