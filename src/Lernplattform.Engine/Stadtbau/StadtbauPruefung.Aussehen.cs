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

    /// <summary>Welle A 0.70.0: feine Drehung des Hauses; die <see cref="Rechtecke"/> liegen in ihrem Rahmen (nur viertelgedreht).</summary>
    public static Hausdrehung.Feinlage Fein(object? teile, double x, double y, int grad) =>
        new(new Vector2((float)x, (float)y) + Hausdrehung.Mitte(teile), grad);

    /// <summary>
    /// Welle A 0.70.0 (Weisung P12-220, Sammelliste 324): Feld eines Gebaeudes ohne Ort (nur Bahnsteig-Flaechen) in
    /// <c>karte.welt.landschaft</c>: Kennung des Orts (Bahnhof), zu dem der Bahnsteig gehoert. Ein Bahnsteig gehoert hoechstens
    /// zu einem Bahnhof (ein Feld), ein Bahnhof kann mehrere haben. Ohne Feld wie bisher (Fassung 0 hat keins).
    /// </summary>
    public const string BahnhofFeld = "bahnhof";

    /// <summary>Kennung des verknuepften Bahnhofs eines Gebaeudes ohne Ort, sonst null (auch bei falschem Wert).</summary>
    public static string? BahnhofVon(PyObjekt gebaeude) => Get(gebaeude, BahnhofFeld) as string;

    /// <summary>
    /// Orte, die in ihrer neuen Rolle kein Bahnhof sind (Weisung P12-214/P12-220: Talheim-Nord wird das Stellwerk ohne
    /// Bahnsteig). Gleich den Orten mit neuer Rolle im Werkzeug (<c>Spielort.Neu</c>, Test im Werkzeug). Dort gibt es keinen
    /// Hinweis „hat keinen Bahnsteig mehr“, und die Baustelle des alten Bahnhofs (Teile „offen“/„erledigt“: Geruest, Rohbau,
    /// fertiger Aufbau) zaehlt nicht als fehlender wechselnder Teil.
    /// </summary>
    public static readonly IReadOnlySet<string> OhneBahnsteig = new HashSet<string>(StringComparer.Ordinal) { "talheim_nord" };

    /// <summary>So weit darf die Kante eines verknuepften Bahnsteigs hoechstens von der Gleismitte liegen (halber Zug 0,29 plus Spalt).</summary>
    public const float AmGleis = 0.6f;

    /// <summary>Bis zu diesem Abstand vom Haus gilt ein Bahnsteig ohne Verknuepfung als „direkt daneben“ (Hinweis nennt den Knopf).</summary>
    public const float Nahe = 3f;

    /// <summary>Mitte eines Gebaeudes ohne Ort mit Bahnsteig-Flaechen in der Welt (Mitte aller Bahnsteig-Rechtecke, fein gedreht).</summary>
    public static Vector2? MitteVon(PyObjekt gebaeude, object? teile)
    {
        var r = Rechtecke(teile, Py.F(gebaeude["x"]), Py.F(gebaeude["y"]), Hausdrehung.Von(gebaeude));
        if (r.Count == 0)
        {
            return null;
        }
        var fein = Fein(teile, Py.F(gebaeude["x"]), Py.F(gebaeude["y"]), Hausdrehung.GradVon(gebaeude));
        var a = r.Aggregate(new Vector2(float.MaxValue), (m, q) => Vector2.Min(m, q.Min));
        var b = r.Aggregate(new Vector2(float.MinValue), (m, q) => Vector2.Max(m, q.Max));
        return fein.Punkt((a + b) / 2);
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
            // Weisung P12-220 Punkt 4: am Stellwerk Talheim-Nord ist die Baustelle des alten Bahnhofs (offen/erledigt) Nicos
            // Wunsch, kein fehlender Teil; Gegenprobe BahnsteigVerknuepfungAltFuerTests
            var ohneBahnsteig = !VerknuepfungAlt.Value && Bahnsteig.OhneBahnsteig.Contains(id);
            if (altTeile.OfType<PyObjekt>().Any(t => t.Any(p => p.Key is "serverfarm_fertig" or "wohnung" or "gegruendet" || (!ohneBahnsteig && p.Key is "erledigt" or "offen"))))
            {
                funde.Add(new StadtbauFund($"Das neue Aussehen von „{name}“ hat die wechselnden Teile des alten Hauses nicht (z. B. Gerüst oder Licht).",
                    "Das neue Haus sieht im Spiel immer gleich aus.", false, ox, oy));
            }
            var verknuepft = !VerknuepfungAlt.Value && landschaft.Any(g => Bahnsteig.BahnhofVon(g) == id);
            if (Bahnsteig.Hat(altTeile) && !Bahnsteig.Hat(teile) && !ohneBahnsteig && !verknuepft
                && !landschaft.Any(g => Py.Gleich(g["modell"], Bahnsteig.ModellName(id))))
            {
                // Weisung P12-220 Punkt 2: liegt ein Bahnsteig ohne Verknuepfung direkt daneben, nennt der Hinweis den Knopf
                var daneben = !VerknuepfungAlt.Value && szene.Ort(id) is { } so && landschaft.Any(g => Bahnsteig.BahnhofVon(g) is null
                    && Bahnsteig.Nur(Get(modelle, Py.Str(g["modell"]))) && Bahnsteig.MitteVon(g, Get(modelle, Py.Str(g["modell"]))) is { } m
                    && AbstandZumKasten(m, so.KastenMin, so.KastenMax) <= Bahnsteig.Nahe);
                funde.Add(new StadtbauFund($"„{name}“ hat keinen Bahnsteig mehr.",
                    daneben
                        ? "Den Bahnsteig daneben antippen und „Mit Bahnhof verknüpfen“ wählen."
                        : "Das ist erlaubt, der Zug hält weiter. Für einen Bahnsteig „Altes Aussehen zurück“ wählen.", false, ox, oy));
            }
        }
        if (!VerknuepfungAlt.Value)
        {
            BahnsteigVerknuepfungen(fassung, modelle, szene, landschaft, orte, funde);
        }
        if (szene.Gleise.Count == 0)
        {
            return;
        }
        // Sammelliste 295 (Weisung P12-196 Frage 2 = a): auch die Bahnsteige im Modell eines Gebaeudes oder Orts (Bahnhof), nicht nur
        // einzelne Bahnsteig-Stuecke; Gegenprobe BahnsteigNurAltFuerTests
        var pruefen = landschaft.Select(g => (Teile: Get(modelle, Py.Str(g["modell"])), X: Py.F(g["x"]), Y: Py.F(g["y"]), Dreh: Hausdrehung.Von(g), Grad: Hausdrehung.GradVon(g),
                Name: Bahnsteig.Nur(Get(modelle, Py.Str(g["modell"]))) ? $"Der Bahnsteig („{Py.Str(g["modell"])}“)" : $"Der Bahnsteig von „{Py.Str(g["modell"])}“"))
            .Concat(orte.Select(o => (Teile: Get(modelle, Py.Str(o["modell"])), X: Py.F(o["x"]), Y: Py.F(o["y"]), Dreh: Hausdrehung.VonOrt(fassung.Stadtdaten, Py.Str(o["id"])),
                Grad: Hausdrehung.GradVonOrt(fassung.Stadtdaten, Py.Str(o["id"])), Name: $"Der Bahnsteig von „{Get(o, "name") as string ?? Py.Str(o["id"])}“")));
        foreach (var (teile, x, y, dreh, grad, name) in pruefen)
        {
            if (BahnsteigNurAlt.Value ? !Bahnsteig.Nur(teile) : !Bahnsteig.Hat(teile))
            {
                continue;
            }
            foreach (var (min, max) in Bahnsteig.Rechtecke(teile, x, y, dreh))
            {
                if (BahnsteigAufGleis(min, max, szene, Bahnsteig.Fein(teile, x, y, grad)) is { } p)
                {
                    funde.Add(new StadtbauFund(name + " liegt auf dem Gleis.",
                        "Bahnsteig weiter weg vom Gleis schieben (mit „Einrasten“ rastet ein Bahnhof selbst ans Gleis).", true, p.X, p.Y));
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Weisung P12-220 Punkt 2: Regeln der Verknuepfung Bahnsteig → Bahnhof (Feld <see cref="Bahnsteig.BahnhofFeld"/>), alle rot:
    /// falscher Wert, Ort unbekannt, Gebaeude ist kein Bahnsteig (nicht nur Bahnsteig-Flaechen), Ort ohne Bahnhof-Rolle
    /// (<see cref="Bahnsteig.OhneBahnsteig"/>), Bahnsteig nicht am Gleis (Kante weiter als <see cref="Bahnsteig.AmGleis"/> von
    /// der Gleismitte), Bahnsteig vom Bahnhof zu Fuss nicht erreichbar (gleiche Wegsuche wie die Figur).
    /// </summary>
    private static void BahnsteigVerknuepfungen(StadtFassung fassung, PyObjekt modelle, StadtSzene szene, List<PyObjekt> landschaft,
        IEnumerable<PyObjekt> orte, List<StadtbauFund> funde)
    {
        var ortNamen = orte.ToDictionary(o => Py.Str(o["id"]), o => Get(o, "name") as string ?? Py.Str(o["id"]), StringComparer.Ordinal);
        foreach (var g in landschaft.Where(g => Py.Hat(g, Bahnsteig.BahnhofFeld)))
        {
            var teile = Get(modelle, Py.Str(g["modell"]));
            var (gx, gy) = (Py.F(g["x"]), Py.F(g["y"]));
            if (g[Bahnsteig.BahnhofFeld] is not string id || !ortNamen.TryGetValue(id, out var name))
            {
                funde.Add(new StadtbauFund("Ein Bahnsteig ist mit einem unbekannten Bahnhof verknüpft.", "Verknüpfung lösen und neu verknüpfen.", true, gx, gy));
                continue;
            }
            if (!Bahnsteig.Nur(teile) || Bahnsteig.MitteVon(g, teile) is not { } mitte)
            {
                funde.Add(new StadtbauFund($"„{Py.Str(g["modell"])}“ ist kein Bahnsteig und kann nicht mit „{name}“ verknüpft sein.", "Verknüpfung lösen.", true, gx, gy));
                continue;
            }
            if (Bahnsteig.OhneBahnsteig.Contains(id))
            {
                funde.Add(new StadtbauFund($"„{name}“ ist kein Bahnhof und hat keinen Bahnsteig.", "Verknüpfung lösen.", true, mitte.X, mitte.Y));
                continue;
            }
            var fein = Bahnsteig.Fein(teile, gx, gy, Hausdrehung.GradVon(g));
            var amGleis = Bahnsteig.Rechtecke(teile, gx, gy, Hausdrehung.Von(g)).Any(r => BahnsteigAbstandZumGleis(r.Min, r.Max, szene, fein) <= Bahnsteig.AmGleis);
            if (!amGleis)
            {
                funde.Add(new StadtbauFund($"Der Bahnsteig von „{name}“ liegt nicht am Gleis.",
                    "Bahnsteig direkt neben das Gleis schieben, dort hält der Zug.", true, mitte.X, mitte.Y));
            }
            if (szene.Ort(id) is { } o)
            {
                var start = (o.StehX, o.StehZ);
                var erreicht = szene.Laufraster.Weg(start, (mitte.X, mitte.Y)).Count > 0
                    || szene.Laufraster.NaechsteFreie(o.StehX, o.StehZ) == szene.Laufraster.NaechsteFreie(mitte.X, mitte.Y);
                if (!erreicht)
                {
                    funde.Add(new StadtbauFund($"Der Bahnsteig von „{name}“ ist vom Bahnhof zu Fuß nicht erreichbar.",
                        "Einen Weg frei machen (Gleis, Wasser oder Häuser versperren ihn) oder den Bahnsteig näher an den Bahnhof legen.", true, mitte.X, mitte.Y));
                }
            }
        }
    }

    /// <summary>Kleinster Abstand eines Bahnsteig-Rechtecks (abgetastet wie <see cref="BahnsteigAufGleis"/>) zur Gleismitte.</summary>
    private static float BahnsteigAbstandZumGleis(Vector2 min, Vector2 max, StadtSzene szene, Hausdrehung.Feinlage fein)
    {
        if (szene.Gleise.Count == 0)
        {
            return float.MaxValue;
        }
        var nx = Math.Max(1, (int)MathF.Ceiling((max.X - min.X) / BahnsteigRaster));
        var ny = Math.Max(1, (int)MathF.Ceiling((max.Y - min.Y) / BahnsteigRaster));
        var best = float.MaxValue;
        for (var i = 0; i <= nx; i++)
        {
            for (var j = 0; j <= ny; j++)
            {
                var p = fein.Punkt(new Vector2(min.X + ((max.X - min.X) * i / nx), min.Y + ((max.Y - min.Y) * j / ny)));
                best = Math.Min(best, szene.Gleise.Min(g => Kurve.Abstand(p, g)));
            }
        }
        return best;
    }

    /// <summary>Abstand eines Punkts zum Kasten eines Hauses (Grundriss), 0 im Kasten.</summary>
    private static float AbstandZumKasten(Vector2 p, Vector3 min, Vector3 max) =>
        Vector2.Distance(p, Vector2.Clamp(p, new Vector2(min.X, min.Z), new Vector2(max.X, max.Z)));

    /// <summary>Gegenprobe Weisung P12-220: Hinweise und Regeln wie vor der Verknuepfung (nur Tests, je Ablauf).</summary>
    public static bool BahnsteigVerknuepfungAltFuerTests { get => VerknuepfungAlt.Value; set => VerknuepfungAlt.Value = value; }
    private static readonly AsyncLocal<bool> VerknuepfungAlt = new();

    /// <summary>Gegenprobe Sammelliste 295: nur einzelne Bahnsteig-Stuecke pruefen (wie bis A 0.65.0, nur Tests, je Ablauf).</summary>
    public static bool BahnsteigNurAltFuerTests { get => BahnsteigNurAlt.Value; set => BahnsteigNurAlt.Value = value; }
    private static readonly AsyncLocal<bool> BahnsteigNurAlt = new();

    /// <summary>Erster Punkt des Rechtecks (abgetastet im Raster <see cref="BahnsteigRaster"/>, mit Rand) auf dem Gleis, sonst null.</summary>
    private static Vector2? BahnsteigAufGleis(Vector2 min, Vector2 max, StadtSzene szene, Hausdrehung.Feinlage fein = default)
    {
        var nx = Math.Max(1, (int)MathF.Ceiling((max.X - min.X) / BahnsteigRaster));
        var ny = Math.Max(1, (int)MathF.Ceiling((max.Y - min.Y) / BahnsteigRaster));
        for (var i = 0; i <= nx; i++)
        {
            for (var j = 0; j <= ny; j++)
            {
                var p = fein.Punkt(new Vector2(min.X + ((max.X - min.X) * i / nx), min.Y + ((max.Y - min.Y) * j / ny)));
                if (szene.Gleise.Any(g => Kurve.Abstand(p, g) < BahnsteigAbstand))
                {
                    return p;
                }
            }
        }
        return null;
    }
}
