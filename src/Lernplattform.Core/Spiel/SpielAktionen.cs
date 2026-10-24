using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Rechner;
using static Lernplattform.Core.Spiel.PyFormat;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>Fehler einer Spielaktion mit dem Text fuer die Anzeige (ValueError in Python).</summary>
public sealed class SpielFehler(string text) : Exception(text);

/// <summary>
/// Phase 7, W2: die Aktionen der Klasse <c>Game</c> aus <c>fisi_game.py</c> (Python 0.63.2) ohne Firma,
/// als reine Funktionen: Jede Aktion prueft den Spielstand und liefert die Ereignisse, die Python
/// mit <c>_log</c> schreibt (Art, Daten), in derselben Reihenfolge, oder wirft <see cref="SpielFehler"/>.
/// Schreiben, Erfolge (check_achievements) und die Firma kommen in W3.
/// </summary>
public static class SpielAktionen
{
    public const int NameMax = 30;   // [:30] in set_profile

    private static List<(string Art, PyObjekt Daten)> Eins(string art, PyObjekt daten) => [(art, daten)];

    private static long PyMod(long a, long b) => ((a % b) + b) % b;

    private static string Codepunkte(string text, int max)
    {
        var punkte = text.EnumerateRunes().Take(max).ToList();
        return string.Concat(punkte.Select(r => r.ToString()));
    }

    /// <summary>set_profile: Name und Aussehen, beim allerersten Profil vorher der Schwierigkeitsgrad.</summary>
    public static List<(string Art, PyObjekt Daten)> Profil(Spielstand stand, string? name, object? aussehen, object? schwierigkeit)
    {
        var kurz = Codepunkte(PythonZeichen.Strip(name ?? ""), NameMax);
        if (kurz.Length == 0)
        {
            throw new SpielFehler("Bitte gib deiner Spielfigur einen Namen.");
        }
        var ergebnis = new List<(string, PyObjekt)>();
        if (stand.Profil is null && stand.Schwierigkeitsgrad is null)
        {
            var stufe = Py.Gleich(schwierigkeit, Ereignisse.Einfach) || Py.Gleich(schwierigkeit, Ereignisse.Normal)
                ? schwierigkeit
                : Get(SchwierigkeitsRegeln(stand.Inhalt), "voreinstellung", Ereignisse.Normal);
            ergebnis.Add((Ereignisse.Schwierigkeit, new PyObjekt { { "stufe", stufe } }));
        }
        ergebnis.Add((Ereignisse.Profil, new PyObjekt { { "name", kurz }, { "aussehen", Aussehen.Normalisieren(aussehen) } }));
        return ergebnis;
    }

    private static PyObjekt SchwierigkeitsRegeln(PyObjekt inhalt) =>
        Get(Objekt(inhalt["balancing"]), "schwierigkeit") is PyObjekt r && Py.Wahr(r) ? r : [];

    private static PyObjekt OffenerAuftrag(Spielstand stand, object? aufgabe)
    {
        var auftrag = stand.Vorbereitet(Auftrag(aufgabe, stand.Inhalt));
        if (auftrag is null || !stand.IstOffen(aufgabe))
        {
            throw new SpielFehler("Dieses Ticket ist heute nicht (mehr) offen.");
        }
        return auftrag;
    }

    /// <summary>solve: wertet ein Ticket aus (wissen = topic_knowledge aus dem Lernstand).</summary>
    public static List<(string Art, PyObjekt Daten)> Loesen(Spielstand stand, object? aufgabe, object? antwort, bool hilfe, PyObjekt wissen)
    {
        var auftrag = OffenerAuftrag(stand, aufgabe);
        var daten = Bewertung.Auswerten(auftrag, antwort, hilfe, wissen, stand.Tag, Bewertung.EinfachBalancing(stand),
            stand.VerfuegbareTeile(auftrag), stand.Inhalt, stand.Lager());
        return Eins(Ereignisse.Erledigt, daten);
    }

    /// <summary>order_spare: das passende Ersatzteil fuer einen Austausch nachbestellen.</summary>
    public static List<(string Art, PyObjekt Daten)> Ersatzteil(Spielstand stand, object? aufgabe, object? teil)
    {
        var auftrag = OffenerAuftrag(stand, aufgabe);
        if (!Py.Wahr(Get(auftrag, "austausch")) || Py.Wahr(Get(auftrag, "zwischenfall")))
        {
            throw new SpielFehler("Für dieses Ticket kann nichts nachbestellt werden.");
        }
        var ware = Teil(teil, stand.Inhalt);
        if (ware is null || !Py.Gleich(ware["typ"], Objekt(auftrag["austausch"])["typ"]))
        {
            throw new SpielFehler("Dieses Teil passt nicht zum Austausch.");
        }
        var nach = Get(Objekt(stand.Inhalt["hardware"]), "nachbestellung") as PyObjekt ?? [];
        return Eins(Ereignisse.Ersatzteil, new PyObjekt
        {
            { "aufgabe", aufgabe }, { "tag", stand.Tag }, { "teil", teil }, { "geld", -Py.Int(ware["preis"]) },
            { "haendler", Get(nach, "haendler", "") }, { "ankunft", stand.Tag + Py.Int(Get(nach, "lieferzeit", 1L)) },
        });
    }

    /// <summary>defer: Ticket verschieben (kostet Zuverlaessigkeit).</summary>
    public static List<(string Art, PyObjekt Daten)> Verschieben(Spielstand stand, object? aufgabe)
    {
        var auftrag = Auftrag(aufgabe, stand.Inhalt);
        if (auftrag is null || !stand.IstOffen(aufgabe))
        {
            throw new SpielFehler("Dieses Ticket ist heute nicht (mehr) offen.");
        }
        return Eins(Ereignisse.Verschoben, Bewertung.Verschieben(auftrag, stand.Tag, Objekt(stand.Inhalt["balancing"])));
    }

    /// <summary>end_day ohne Firma: Gehalt, Rang und Miete des Tages.</summary>
    public static List<(string Art, PyObjekt Daten)> Feierabend(Spielstand stand)
    {
        if (Py.Wahr(stand.Firma))
        {
            throw new NotSupportedException("Feierabend der Firma kommt in Phase 7 W3");
        }
        if (!stand.FeierabendMoeglich())
        {
            throw new SpielFehler("Es sind noch Tickets offen.");
        }
        var daten = new PyObjekt { { "tag", stand.Tag }, { "gehalt", stand.Gehalt }, { "rang", stand.Rang } };
        if (stand.Miete != 0)
        {
            daten.Add("miete", stand.Miete);
        }
        return Eins(Ereignisse.Feierabend, daten);
    }

    // -- Wohnung ----------------------------------------------------------

    private static List<(string Art, PyObjekt Daten)> EinrichtungEreignis(Spielstand stand, PyObjekt einrichtung)
    {
        var daten = Py.Kopie(einrichtung) as PyObjekt ?? [];
        Py.Setzen(daten, "wohnung", stand.Wohnung);
        return Eins(Ereignisse.Einrichtung, daten);
    }

    private static bool Entfernen(PyObjekt d, string schluessel)
    {
        var index = d.FindIndex(e => e.Key == schluessel);
        if (index < 0)
        {
            return false;
        }
        d.RemoveAt(index);
        return true;
    }

    /// <summary>buy_furniture: kauft ein Moebelstueck (stueck = "m" + uuid4().hex[:10], vom Aufrufer).</summary>
    public static List<(string Art, PyObjekt Daten)> MoebelKaufen(Spielstand stand, object? moebel, string stueck)
    {
        var ware = Wohnung.MoebelStueck(moebel, stand.Inhalt);
        if (ware is null || !Py.Wahr(Get(ware, "laden", true)))
        {
            throw new SpielFehler("Dieses Möbelstück gibt es im Möbelhaus nicht.");
        }
        if (Py.Kleiner(stand.Geld, ware["preis"]))
        {
            throw new SpielFehler(Prozent("Dafür reicht dein Geld noch nicht (%d € fehlen).", Py.Minus(ware["preis"], Math.Max(0, stand.Geld))));
        }
        return Eins(Ereignisse.MoebelGekauft, new PyObjekt { { "stueck", stueck }, { "moebel", moebel }, { "geld", Py.Minus(0L, ware["preis"]) } });
    }

    /// <summary>sell_furniture: verkauft zum Rueckkaufpreis; steht es, wird es vorher aus der Einrichtung genommen.</summary>
    public static List<(string Art, PyObjekt Daten)> MoebelVerkaufen(Spielstand stand, string stueck)
    {
        var ware = Wohnung.MoebelStueck(stand.Moebel.Get(stueck), stand.Inhalt) ?? throw new SpielFehler("Dieses Möbelstück gehört dir nicht.");
        var preis = Py.Int(Py.Mal(ware["preis"], Get(Objekt(stand.Inhalt["wohnungen"]), "rueckkauf", 0.5)));
        var ergebnis = new List<(string, PyObjekt)>();
        var einrichtung = Wohnung.Einrichtung(stand);
        if (Entfernen(Objekt(einrichtung["moebel"]), stueck))
        {
            ergebnis.AddRange(EinrichtungEreignis(stand, einrichtung));
        }
        ergebnis.Add((Ereignisse.MoebelVerkauft, new PyObjekt { { "stueck", stueck }, { "moebel", ware["id"] }, { "geld", preis } }));
        return ergebnis;
    }

    private static List<(string Art, PyObjekt Daten)> Setzen(Spielstand stand, string stueck, object? x, object? y, object? dreh)
    {
        var problem = Wohnung.PlatzProblem(stand, stueck, stand.Moebel.Get(stueck), x, y, dreh);
        if (problem.Length > 0)
        {
            throw new SpielFehler(problem);
        }
        var einrichtung = Wohnung.Einrichtung(stand);
        Py.Setzen(Objekt(einrichtung["moebel"]), stueck, new List<object?> { x, y, PyMod(Py.Int(dreh), 4) });
        return EinrichtungEreignis(stand, einrichtung);
    }

    /// <summary>place_furniture: stellt ein Moebelstueck auf (oder um), Ecke oben links eingerastet.</summary>
    public static List<(string Art, PyObjekt Daten)> Aufstellen(Spielstand stand, string stueck, object? x, object? y, object? dreh)
    {
        if (stand.Moebel.Get(stueck) is null)
        {
            throw new SpielFehler("Dieses Möbelstück gehört dir nicht.");
        }
        return Setzen(stand, stueck, Wohnung.Einrasten(x), Wohnung.Einrasten(y), dreh);
    }

    /// <summary>place_furniture_at: mit der Mitte bei (cx, cy), an die Wand gerueckt.</summary>
    public static List<(string Art, PyObjekt Daten)> AufstellenBei(Spielstand stand, string stueck, object? cx, object? cy, object? dreh)
    {
        var moebel = stand.Moebel.Get(stueck) ?? throw new SpielFehler("Dieses Möbelstück gehört dir nicht.");
        var (x, y) = Wohnung.Andocken(stand, moebel, Py.F(cx), Py.F(cy), dreh);
        return Setzen(stand, stueck, x, y, dreh);
    }

    private static List<object?>? Platz(Spielstand stand, string stueck) =>
        Get(Objekt(Wohnung.Einrichtung(stand)["moebel"]), stueck) as List<object?>;

    /// <summary>rotate_furniture: Vierteldrehung, an der Wand bleibt es an der Wand.</summary>
    public static List<(string Art, PyObjekt Daten)> Drehen(Spielstand stand, string stueck)
    {
        var moebel = stand.Moebel.Get(stueck);
        var platz = Platz(stand, stueck);
        if (moebel is null || platz is null)
        {
            throw new SpielFehler("Dieses Möbelstück steht nicht in der Wohnung.");
        }
        var dreh = PyMod(Py.Int(platz[2]) + 1, 4);
        var (x, y) = Wohnung.Gedreht(stand, moebel, platz, dreh);
        return Setzen(stand, stueck, x, y, dreh);
    }

    /// <summary>nudge_furniture: um ein Rasterfeld schieben (Handy).</summary>
    public static List<(string Art, PyObjekt Daten)> Schieben(Spielstand stand, string stueck, long dx, long dy)
    {
        var platz = Platz(stand, stueck) ?? throw new SpielFehler("Dieses Möbelstück steht nicht in der Wohnung.");
        return Setzen(stand, stueck, PythonRundenStellen.Runde(Wohnung.Einrasten(platz[0]) + dx * Wohnung.Raster, 2),
            PythonRundenStellen.Runde(Wohnung.Einrasten(platz[1]) + dy * Wohnung.Raster, 2), platz[2]);
    }

    /// <summary>restore_layout: eine fruehere Einrichtung als neues Ereignis (nur, wenn sie sich unterscheidet).</summary>
    public static List<(string Art, PyObjekt Daten)> EinrichtungZurueck(Spielstand stand, PyObjekt einrichtung)
    {
        var jetzt = Wohnung.Einrichtung(stand);
        var moebel = new PyObjekt();
        foreach (var (k, v) in Get(einrichtung, "moebel") as PyObjekt is { Count: > 0 } m ? m : [])
        {
            if (stand.Moebel.ContainsKey(k))
            {
                moebel.Add(k, Liste(v).ToList());
            }
        }
        var boeden = new PyObjekt();
        boeden.AddRange(Get(einrichtung, "boeden") as PyObjekt is { Count: > 0 } b ? b : []);
        var neu = new PyObjekt { { "moebel", moebel }, { "boeden", boeden } };
        return Py.Gleich(neu, jetzt) ? [] : EinrichtungEreignis(stand, neu);
    }

    /// <summary>box_furniture: zurueck in den Karton.</summary>
    public static List<(string Art, PyObjekt Daten)> Einpacken(Spielstand stand, string stueck)
    {
        var einrichtung = Wohnung.Einrichtung(stand);
        return Entfernen(Objekt(einrichtung["moebel"]), stueck) ? EinrichtungEreignis(stand, einrichtung) : [];
    }

    /// <summary>set_floor: Boden eines Raums (Art, Farbe).</summary>
    public static List<(string Art, PyObjekt Daten)> Boden(Spielstand stand, string raum, object? art, object? farbe)
    {
        var einrichtung = Wohnung.Einrichtung(stand);
        Py.Setzen(Objekt(einrichtung["boeden"]), raum, new List<object?> { art, farbe });
        return EinrichtungEreignis(stand, einrichtung);
    }

    /// <summary>move_home: Umzug (miete = Schalter "Miete" dieses Geraets), alle Moebel in Kartons.</summary>
    public static List<(string Art, PyObjekt Daten)> Umziehen(Spielstand stand, object? wohnung, bool miete)
    {
        var ziel = Wohnung.Bleibe(wohnung, stand.Inhalt);
        if (ziel is null || !Wohnung.Umzuege(stand).Any(w => Py.Gleich(w, ziel)))
        {
            throw new SpielFehler("In diese Wohnung kannst du nicht umziehen.");
        }
        var angebot = Wohnung.UmzugAngebot(stand, ziel, miete);
        if (Py.Wahr(angebot["fehlt"]))
        {
            throw new SpielFehler(Prozent("Dafür reicht dein Geld noch nicht (%d € fehlen).", angebot["fehlt"]));
        }
        var daten = new PyObjekt { { "wohnung", wohnung }, { "geld", Py.Minus(angebot["zurueck"], angebot["kosten"]) } };
        if (miete)
        {
            daten.Add("miete", angebot["miete"]);
            daten.Add("kaution", angebot["kosten"]);
        }
        if (Py.Wahr(angebot["zurueck"]))
        {
            daten.Add("kaution_zurueck", angebot["zurueck"]);
        }
        return Eins(Ereignisse.Umzug, daten);
    }
}
