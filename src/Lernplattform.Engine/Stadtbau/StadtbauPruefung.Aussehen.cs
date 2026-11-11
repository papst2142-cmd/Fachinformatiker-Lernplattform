using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle A 0.52.0 / S 0.21.0 (Weisungen P12-87/P12-88, Kurzplan Meldung P12-56, Nico 1a-5a): Bahnsteige der Orte. In Stadt 1
/// gehoert der Bahnsteig zum Modell des Orts (Teil <c>flaeche</c> mit der Farbe <see cref="Farbe"/>: Bahnhof Talheim,
/// Talheim-Nord, Birkenhain). Tauscht Nico das Aussehen eines solchen Orts, legt das Werkzeug den Bahnsteig als eigenes
/// Gebaeude ohne Ort an (Modell <see cref="ModellName"/>, nur Bahnsteig-Flaechen), das an der alten Stelle stehen bleibt.
/// Das Spiel braucht dafuer nichts Neues: es zeichnet Gebaeude ohne Ort und Flaechen seit Phase 7.
/// </summary>
public static class Bahnsteig
{
    /// <summary>Anfang des Modellnamens eines Bahnsteig-Stuecks (danach die Kennung des Orts).</summary>
    public const string ModellPraefix = "bahnsteig_";

    /// <summary>Farbe einer Bahnsteig-Flaeche in den Modellen.</summary>
    public const string Farbe = "bahnsteig";

    /// <summary>Modellname des Bahnsteig-Stuecks eines Orts.</summary>
    public static string ModellName(string ort) => ModellPraefix + ort;

    /// <summary>Ist das Teil eines Modells eine Bahnsteig-Flaeche?</summary>
    public static bool IstFlaeche(object? teil) => teil is PyObjekt t && Py.Gleich(Get(t, "art"), "flaeche") && Py.Gleich(Get(t, "farbe"), Farbe);

    /// <summary>Hat das Modell (Liste der Teile) wenigstens eine Bahnsteig-Flaeche?</summary>
    public static bool Hat(object? teile) => (teile as List<object?> ?? []).Any(IstFlaeche);

    /// <summary>Besteht das Modell nur aus Bahnsteig-Flaechen (ein Bahnsteig-Stueck)?</summary>
    public static bool Nur(object? teile) => teile is List<object?> { Count: > 0 } l && l.All(IstFlaeche);

    /// <summary>
    /// Bahnsteig-Flaechen eines Hauses als Rechtecke in der Welt: Ecke (x, y), gedreht um die Mitte der Bloecke wie im Spiel
    /// (<see cref="Hausdrehung"/>).
    /// </summary>
    public static List<(Vector2 Min, Vector2 Max)> Rechtecke(object? teile, double x, double y, int dreh)
    {
        var mitte = new Vector2((float)x, (float)y) + Hausdrehung.Mitte(teile);
        var liste = new List<(Vector2, Vector2)>();
        foreach (var t in (teile as List<object?> ?? []).Where(IstFlaeche).OfType<PyObjekt>())
        {
            var a = new Vector2((float)(x + Py.F(t["x"])), (float)(y + Py.F(t["y"])));
            var b = a + new Vector2((float)Py.F(t["w"]), (float)Py.F(t["h"]));
            a = Hausdrehung.Drehen(a, mitte, dreh);
            b = Hausdrehung.Drehen(b, mitte, dreh);
            liste.Add((Vector2.Min(a, b), Vector2.Max(a, b)));
        }
        return liste;
    }
}

public static partial class StadtbauPruefung
{
    /// <summary>Schrittweite, in der ein Bahnsteig auf das Gleis abgetastet wird.</summary>
    private const float BahnsteigRaster = 0.05f;

    /// <summary>So weit bleibt ein Bahnsteig mindestens von der Gleismitte weg: halbes Gleisbett (<see cref="StadtSzene.GleisBreite"/>).</summary>
    public const float BahnsteigAbstand = StadtSzene.GleisBreite / 2;

    /// <summary>
    /// Welle A 0.52.0 (Weisungen P12-87/P12-88): Regeln fuer Orte mit einem anderen Aussehen.
    /// <list type="bullet">
    /// <item>rot: das Modell eines Orts hat keinen Block (dann gibt es kein Haus mit Tuer);</item>
    /// <item>rot: das Schild eines Orts liegt nicht auf der Karte;</item>
    /// <item>rot: ein Bahnsteig-Stueck liegt auf dem Gleis (naeher als <see cref="BahnsteigAbstand"/> an der Gleismitte). Nicht
    /// der ganze Lichtraum (<see cref="Bahnuebergang.LichtraumHalb"/> 0,493): die Bahnsteige der Stadt 1 liegen flach (0,05 hoch)
    /// unter dem Zug, 0,26 bis 0,40 von der Gleismitte (Bahnhof Talheim 0,26, Birkenhain 0,30, Talheim-Nord 0,40); mit dem
    /// Lichtraum waere schon das unveraenderte Original rot (E1, in Meldung P12-58 genannt).</item>
    /// <item>gelb: das alte Aussehen hatte Teile, die im Spiel wechseln (Geruest, Licht, Wohnung), das neue nicht (Nico 2a);</item>
    /// <item>gelb: ein Ort hatte einen Bahnsteig und hat keinen mehr (P12-88: erlaubt, sperrt den Export nicht).</item>
    /// </list>
    /// Fassung 0 hat hier keinen Fund (alte Modelle, keine Bahnsteig-Stuecke).
    /// </summary>
    private static void AussehenRegeln(StadtFassung fassung, PyObjekt inhalt, StadtSzene szene, List<StadtbauFund> funde)
    {
        var welt = Objekt(fassung.Karte["welt"]);
        var (breite, hoehe) = (Py.F(welt["breite"]), Py.F(welt["hoehe"]));
        var modelle = new PyObjekt();
        foreach (var quelle in new[] { Get(fassung.Karte, "modelle"), Get(fassung.Stadtdaten, "neue_modelle") })
        {
            if (quelle is PyObjekt m)
            {
                foreach (var (k, v) in m)
                {
                    Py.Setzen(modelle, k, v);
                }
            }
        }
        var original = Objekt(inhalt["orte"]);
        var originalModelle = Get(original, "modelle") as PyObjekt ?? [];
        var originalOrte = ListeOderLeer(original, "orte").OfType<PyObjekt>().ToDictionary(o => Py.Str(o["id"]), StringComparer.Ordinal);
        var landschaft = ListeOderLeer(welt, "landschaft").OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "gebaeude")).ToList();
        var orte = ListeOderLeer(fassung.Karte, "orte").OfType<PyObjekt>().Concat(ListeOderLeer(fassung.Stadtdaten, "neue_orte").OfType<PyObjekt>());
        foreach (var o in orte)
        {
            var id = Py.Str(o["id"]);
            var name = Get(o, "name") as string ?? id;
            var modell = Py.Str(o["modell"]);
            var teile = Get(modelle, modell) as List<object?>;
            var (ox, oy) = (Py.F(o["x"]), Py.F(o["y"]));
            if (teile is not null && !teile.OfType<PyObjekt>().Any(t => Py.Gleich(t["art"], "block")))
            {
                funde.Add(new StadtbauFund($"Das Haus von „{name}“ hat kein Gebäude (keinen Block), nur Flächen.",
                    "Ein anderes Aussehen wählen oder „Altes Aussehen zurück“.", true, ox, oy));
            }
            if (Get(o, "schild") is List<object?> { Count: 2 } schild && (Py.F(schild[0]) < 0 || Py.F(schild[1]) < 0 || Py.F(schild[0]) > breite || Py.F(schild[1]) > hoehe))
            {
                funde.Add(new StadtbauFund($"Das Schild von „{name}“ liegt außerhalb der Karte.", "Haus weiter in die Karte schieben.", true, ox, oy));
            }
            if (!originalOrte.TryGetValue(id, out var alt) || Py.Gleich(alt["modell"], modell) || Get(originalModelle, Py.Str(alt["modell"])) is not List<object?> altTeile)
            {
                continue;
            }
            if (altTeile.OfType<PyObjekt>().Any(t => t.Any(p => p.Key is "erledigt" or "offen" or "serverfarm_fertig" or "wohnung" or "gegruendet")))
            {
                funde.Add(new StadtbauFund($"Das neue Aussehen von „{name}“ hat die wechselnden Teile des alten Hauses nicht (z. B. Gerüst oder Licht).",
                    "Das neue Haus sieht im Spiel immer gleich aus.", false, ox, oy));
            }
            if (Bahnsteig.Hat(altTeile) && !Bahnsteig.Hat(teile) && !landschaft.Any(g => Py.Gleich(g["modell"], Bahnsteig.ModellName(id))))
            {
                funde.Add(new StadtbauFund($"„{name}“ hat keinen Bahnsteig mehr.",
                    "Das ist erlaubt, der Zug hält weiter. Für einen Bahnsteig „Altes Aussehen zurück“ wählen.", false, ox, oy));
            }
        }
        if (szene.Gleise.Count == 0)
        {
            return;
        }
        // Sammelliste 295 (Weisung P12-196 Frage 2 = a): auch die Bahnsteige im Modell eines Gebaeudes oder Orts (Bahnhof), nicht nur
        // einzelne Bahnsteig-Stuecke; Gegenprobe BahnsteigNurAltFuerTests
        var pruefen = landschaft.Select(g => (Teile: Get(modelle, Py.Str(g["modell"])), X: Py.F(g["x"]), Y: Py.F(g["y"]), Dreh: Hausdrehung.Von(g),
                Name: Bahnsteig.Nur(Get(modelle, Py.Str(g["modell"]))) ? $"Der Bahnsteig („{Py.Str(g["modell"])}“)" : $"Der Bahnsteig von „{Py.Str(g["modell"])}“"))
            .Concat(orte.Select(o => (Teile: Get(modelle, Py.Str(o["modell"])), X: Py.F(o["x"]), Y: Py.F(o["y"]), Dreh: Hausdrehung.VonOrt(fassung.Stadtdaten, Py.Str(o["id"])),
                Name: $"Der Bahnsteig von „{Get(o, "name") as string ?? Py.Str(o["id"])}“")));
        foreach (var (teile, x, y, dreh, name) in pruefen)
        {
            if (BahnsteigNurAlt.Value ? !Bahnsteig.Nur(teile) : !Bahnsteig.Hat(teile))
            {
                continue;
            }
            foreach (var (min, max) in Bahnsteig.Rechtecke(teile, x, y, dreh))
            {
                if (BahnsteigAufGleis(min, max, szene) is { } p)
                {
                    funde.Add(new StadtbauFund(name + " liegt auf dem Gleis.",
                        "Bahnsteig weiter weg vom Gleis schieben (mit „Einrasten“ rastet ein Bahnhof selbst ans Gleis).", true, p.X, p.Y));
                    break;
                }
            }
        }
    }

    /// <summary>Gegenprobe Sammelliste 295: nur einzelne Bahnsteig-Stuecke pruefen (wie bis A 0.65.0, nur Tests, je Ablauf).</summary>
    public static bool BahnsteigNurAltFuerTests { get => BahnsteigNurAlt.Value; set => BahnsteigNurAlt.Value = value; }
    private static readonly AsyncLocal<bool> BahnsteigNurAlt = new();

    /// <summary>Erster Punkt des Rechtecks (abgetastet im Raster <see cref="BahnsteigRaster"/>, mit Rand) auf dem Gleis, sonst null.</summary>
    private static Vector2? BahnsteigAufGleis(Vector2 min, Vector2 max, StadtSzene szene)
    {
        var nx = Math.Max(1, (int)MathF.Ceiling((max.X - min.X) / BahnsteigRaster));
        var ny = Math.Max(1, (int)MathF.Ceiling((max.Y - min.Y) / BahnsteigRaster));
        for (var i = 0; i <= nx; i++)
        {
            for (var j = 0; j <= ny; j++)
            {
                var p = new Vector2(min.X + ((max.X - min.X) * i / nx), min.Y + ((max.Y - min.Y) * j / ny));
                if (szene.Gleise.Any(g => Kurve.Abstand(p, g) < BahnsteigAbstand))
                {
                    return p;
                }
            }
        }
        return null;
    }
}
