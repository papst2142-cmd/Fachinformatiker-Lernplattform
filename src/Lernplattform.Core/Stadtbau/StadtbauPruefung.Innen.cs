using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Ein Fund der Pruefung fuer das Stadtbau-Werkzeug: Satz in einfachen Worten, Tipp, rot (sperrt den Export) oder gelb
/// (Hinweis), dazu die Stelle im Grundriss (x, y; null = keine Stelle), zu der die Ansicht beim Antippen springt.
/// </summary>
public sealed record StadtbauFund(string Text, string Tipp, bool Rot, double? X = null, double? Y = null);

/// <summary>
/// Phase 13 Welle 2 (Weisung P12-17, Plan Abschnitt 4): die Regeln fuer Innenraeume, die bis W1 nur die Tests des Spiels
/// prueften, als gemeinsame Pruefung fuer Werkzeug, CI und Einbau. Gleiche Rechnung wie die Tests (Erreichbarkeit wie
/// <c>SpielAuslageTests.JedeWareErreichbar</c>, Ueberschneidung der gezeichneten Grundflaechen wie
/// <c>SpielAuslageTests.AuslageUeberschneidetSichNicht</c>, Lage von Raeumen und Tueren wie <see cref="SpielPruefung.Gebaeude"/>).
/// In Fassung 0 ist genau der bekannte Fund 216 rot (Elektromarkt obere Reihe nicht erreichbar).
/// </summary>
public static partial class StadtbauPruefung
{
    /// <summary>Toleranz der Ueberschneidung wie in den Spieltests (0,02).</summary>
    public const float Toleranz = 0.02f;

    /// <summary>So nah kommt die Figur hoechstens an den Platz einer Person hinter dem Tresen (sonst ist hinten offen).</summary>
    public const double HinterTresenAbstand = 0.45;

    /// <summary>Gelb ab diesem Anteil der Grenzen, rot darueber (Plan Abschnitt 4).</summary>
    public const double GelbAb = 0.8;

    private static string Z(double wert) => wert.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));

    /// <summary>
    /// Alle Regeln fuer den Innenraum <paramref name="ort"/> mit dem (bearbeiteten) Grundriss <paramref name="gebaeude"/>:
    /// Lage von Raeumen, Tueren und Einrichtung, bekannte Teile, Ueberschneidung, Erreichbarkeit jeder Ware und jeder
    /// Person, hinter dem Tresen abgesperrt, Sortiment (nur Geschaefte, mit <paramref name="geschaefte"/>) und Grenzen.
    /// </summary>
    /// <remarks>
    /// Phase 13 Welle 5 (Weisung P12-73): <paramref name="etage"/> &gt; 0 prueft ein Obergeschoss: ohne Eingangstuer, Raeume
    /// nicht noetig, keine Personen; Wege, Sortiment und die Regeln zwischen den Etagen prueft <see cref="Etagen"/>.
    /// </remarks>
    public static List<StadtbauFund> Innen(string ort, PyObjekt gebaeude, IReadOnlyList<PyObjekt> personen, GrafikBibliothek? grafik,
        Geschaefte? geschaefte = null, PyObjekt? innenraeume = null, int etage = 0)
    {
        var funde = new List<StadtbauFund>();
        Grundriss(gebaeude, funde, grafik, etage);
        if (funde.Any(f => f.Rot))
        {
            // ohne gueltigen Grundriss lassen sich Wege und Grenzen nicht rechnen
            return funde;
        }
        Innenraum innen;
        try
        {
            innen = Innenraum.AusGrundrissMitPersonen(Ansicht(ort, etage), gebaeude, personen, grafik, Geschaefte.Laeden.Contains(ort) ? geschaefte?.Daten : null);
        }
        catch (Exception fehler) when (fehler is InvalidDataException or KeyNotFoundException or InvalidCastException or FormatException or NullReferenceException or ArgumentException)
        {
            funde.Add(new StadtbauFund("Der Innenraum lässt sich nicht bauen (" + fehler.GetType().Name + ").",
                "Letzten Schritt rückgängig machen.", true));
            return funde;
        }
        Ueberschneidung(innen, funde);
        if (etage > 0)
        {
            var (d, a) = StadtbauGrenzen.Innen(innen);
            Grenzen(d, a, funde);
            return funde;
        }
        Erreichbarkeit(innen, ort, geschaefte, funde);
        HinterDemTresen(innen, funde);
        if (geschaefte is not null && innenraeume is not null && Geschaefte.Laeden.Contains(ort))
        {
            Sortiment(ort, gebaeude, geschaefte, innenraeume, funde);
        }
        var (dreiecke, aufrufe) = StadtbauGrenzen.Innen(innen);
        Grenzen(dreiecke, aufrufe, funde);
        return funde;
    }

    /// <summary>Ansicht des Spiels zu einem Ort (Geschaeft oder Schalter).</summary>
    public static string Ansicht(string ort) => Schalter.Orte.Contains(ort) ? "schalter:" + ort : "geschaeft:" + ort;

    /// <summary>Ansicht des Spiels zu einer Etage (0 = Erdgeschoss wie <see cref="Ansicht(string)"/>).</summary>
    public static string Ansicht(string ort, int etage) => Ansicht(Stadtbau.Etagen.Schluessel(ort, etage));

    /// <summary>Grenzen eines Innenraums: rot darueber, gelb ab 80 % (Dreiecke 160 000, Aufrufe 40 wie die Gesamtansicht).</summary>
    public static void Grenzen(long dreiecke, int aufrufe, List<StadtbauFund> funde)
    {
        if (dreiecke > StadtbauGrenzen.Dreiecke || aufrufe > StadtbauGrenzen.Aufrufe)
        {
            funde.Add(new StadtbauFund($"Die Grenzen sind überschritten ({dreiecke:N0} von {StadtbauGrenzen.Dreiecke:N0} Dreiecken, {aufrufe} von {StadtbauGrenzen.Aufrufe} Zeichenaufrufen).",
                "Einrichtung herausnehmen, bis der Balken nicht mehr rot ist.", true));
        }
        else if (dreiecke >= GelbAb * StadtbauGrenzen.Dreiecke || aufrufe >= GelbAb * StadtbauGrenzen.Aufrufe)
        {
            funde.Add(new StadtbauFund($"Die Grenzen sind fast erreicht ({dreiecke:N0} Dreiecke, {aufrufe} Zeichenaufrufe).",
                "Das ist erlaubt; mehr Einrichtung macht das Bild am Tablet langsamer.", false));
        }
    }

    private static void Grundriss(PyObjekt g, List<StadtbauFund> funde, GrafikBibliothek? grafik = null, int etage = 0)
    {
        double breite = Py.F(g["breite"]), hoehe = Py.F(g["hoehe"]);
        if (breite < 4 || hoehe < 4)
        {
            funde.Add(new StadtbauFund("Das Haus ist kleiner als 4 × 4.", "Breite und Tiefe vergrößern.", true));
            return;
        }
        var flur = Objekt(g["flur"]);
        double fy = Py.F(flur["y"]), fh = Py.F(flur["h"]);
        if (fy < 0 || fh < 1 || fy + fh > hoehe + 1e-9)
        {
            funde.Add(new StadtbauFund("Der Flur liegt nicht im Haus.", "Flur verschieben oder kleiner machen.", true, 1, fy));
        }
        if (etage > 0)
        {
            // Phase 13 Welle 5: hinaus geht es nur im Erdgeschoss
            if (Py.Hat(flur, "eingang"))
            {
                funde.Add(new StadtbauFund("Auf einer Etage gibt es keine Eingangstür.", "Hinaus geht es nur im Erdgeschoss; die Tür hier entfernen.", true, 0.3, fy + (fh / 2)));
            }
        }
        else
        {
            var eingang = Objekt(flur["eingang"]);
            double ev = Py.F(eingang["von"]), eb = Py.F(eingang["bis"]);
            if (!(fy <= ev && ev < eb && eb <= fy + fh) || eb - ev < 1)
            {
                funde.Add(new StadtbauFund("Die Eingangstür liegt nicht an der Außenwand des Flurs (mindestens 1 breit).",
                    "Eingang im Flur nach oben oder unten schieben.", true, 0.3, (ev + eb) / 2));
            }
        }
        var raeume = Liste(g["raeume"]).OfType<PyObjekt>().ToList();
        if (raeume.Count == 0 && etage == 0)
        {
            funde.Add(new StadtbauFund("Das Haus hat keinen Raum.", "Einen Raum anlegen.", true));
        }
        foreach (var raum in raeume)
        {
            var name = Py.Str(Get(raum, "name", raum["id"]));
            double x = Py.F(raum["x"]), y = Py.F(raum["y"]), w = Py.F(raum["w"]), h = Py.F(raum["h"]);
            if (x < 0 || y < 0 || x + w > breite + 1e-9 || y + h > hoehe + 1e-9 || w < 1 || h < 1)
            {
                funde.Add(new StadtbauFund($"Raum „{name}“ liegt nicht ganz im Haus.", "Raum verschieben oder kleiner machen.", true, x + (w / 2), y + (h / 2)));
            }
            if (Math.Min(y + h, fy + fh) - Math.Max(y, fy) > 1e-9)
            {
                funde.Add(new StadtbauFund($"Raum „{name}“ überdeckt den Flur.", "Raum oder Flur verschieben.", true, x + (w / 2), y + (h / 2)));
            }
            foreach (var anderer in raeume.Where(r => !ReferenceEquals(r, raum) && raeume.IndexOf(r) > raeume.IndexOf(raum)))
            {
                double ax = Py.F(anderer["x"]), ay = Py.F(anderer["y"]);
                if (Math.Min(x + w, ax + Py.F(anderer["w"])) - Math.Max(x, ax) > 1e-9 && Math.Min(y + h, ay + Py.F(anderer["h"])) - Math.Max(y, ay) > 1e-9)
                {
                    funde.Add(new StadtbauFund($"Die Räume „{name}“ und „{Py.Str(Get(anderer, "name", anderer["id"]))}“ überdecken sich.",
                        "Einen der beiden Räume verschieben.", true, x + (w / 2), y + (h / 2)));
                }
            }
            if (Get(raum, "tuer") is not PyObjekt tuer || tuer.Count == 0)
            {
                funde.Add(new StadtbauFund($"Raum „{name}“ hat keine Tür.", "Eine Tür auf die Wand zum Flur setzen.", true, x + (w / 2), y + (h / 2)));
                continue;
            }
            double von = Py.F(tuer["von"]), bis = Py.F(tuer["bis"]);
            var seite = Wege.TuerSeite(raum, g);
            var richtig = seite is "w" or "o"
                ? Math.Max(y, fy) <= von && von < bis && bis <= Math.Min(y + h, fy + fh)
                : x <= von && von < bis && bis <= x + w && (seite == "s" ? Math.Abs(y + h - fy) < 1e-6 : Math.Abs(y - (fy + fh)) < 1e-6);
            if (!richtig || bis - von < 1)
            {
                var (tx, ty) = seite is "w" or "o" ? (seite == "w" ? x : x + w, (von + bis) / 2) : ((von + bis) / 2, seite == "s" ? y + h : y);
                funde.Add(new StadtbauFund($"Die Tür von „{name}“ liegt nicht auf der Wand zum Flur (mindestens 1 breit).",
                    "Tür auf die Wand zwischen Raum und Flur schieben.", true, tx, ty));
            }
        }
        foreach (var (bereich, d) in InnenGrundriss.Einrichtung(g))
        {
            var typ = Py.Str(d["typ"]);
            double x = Py.F(d["x"]), y = Py.F(d["y"]), w = Py.F(d["w"]), h = Py.F(d["h"]);
            // Phase 13 Welle 4a (Weisung P12-28): ein Stueck aus dem Katalog des Werkzeugs gilt, wenn sein Modell in der
            // Grafik ist (im Spiel: mitgeliefert in katalog_verbaut.bin); sonst bleibt es rot
            if (!InnenKatalog.Bekannt(typ) && !(Katalogstueck.IstTyp(typ) && grafik?.Modell(typ) is not null))
            {
                funde.Add(new StadtbauFund($"„{typ}“ ist kein Teil aus dem Katalog.", "Stück löschen und aus dem Katalog neu setzen.", true, x + (w / 2), y + (h / 2)));
            }
            if (w <= 0 || h <= 0)
            {
                funde.Add(new StadtbauFund($"„{InnenKatalog.Name(typ)}“ hat keine Größe.", "Breite und Tiefe größer als 0 eintragen.", true, x, y));
                continue;
            }
            var (bx, by, bw, bh) = bereich == "flur"
                ? (0.0, fy, breite, fh)
                : raeume.FirstOrDefault(r => Py.Str(r["id"]) == bereich) is { } r ? (Py.F(r["x"]), Py.F(r["y"]), Py.F(r["w"]), Py.F(r["h"])) : (0, 0, breite, hoehe);
            if (x < bx - 1e-9 || y < by - 1e-9 || x + w > bx + bw + 1e-9 || y + h > by + bh + 1e-9)
            {
                funde.Add(new StadtbauFund($"„{InnenKatalog.Name(typ)}“ ragt aus dem Raum.", "Stück ganz in den Raum schieben.", true, x + (w / 2), y + (h / 2)));
            }
        }
    }

    private static bool Ueberlappt((float X1, float Z1, float X2, float Z2) a, (float X1, float Z1, float X2, float Z2) b) =>
        Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1) > Toleranz && Math.Min(a.Z2, b.Z2) - Math.Max(a.Z1, b.Z1) > Toleranz;

    /// <summary>
    /// Kein Stueck der Auslage beruehrt mit seiner gezeichneten Grundflaeche ein anderes Stueck (wie die Spieltests); dazu
    /// keine zwei festen Stuecke (nicht begehbar) mit ihren Rechtecken aus dem Grundriss.
    /// </summary>
    private static void Ueberschneidung(Innenraum innen, List<StadtbauFund> funde)
    {
        var alle = innen.DekoUmrisse.ToList();
        var auslage = alle.Where(u => Py.Hat(u.Deko, "ware")).ToList();
        var gemeldet = new HashSet<(PyObjekt, PyObjekt)>();
        void Melden(PyObjekt a, PyObjekt b)
        {
            if (!gemeldet.Add((a, b)) || !gemeldet.Add((b, a)))
            {
                return;
            }
            funde.Add(new StadtbauFund($"„{InnenKatalog.Name(Py.Str(a["typ"]))}“ und „{InnenKatalog.Name(Py.Str(b["typ"]))}“ überschneiden sich.",
                "Eines der beiden Stücke verschieben.", true, Py.F(a["x"]) + (Py.F(a["w"]) / 2), Py.F(a["y"]) + (Py.F(a["h"]) / 2)));
        }
        foreach (var a in auslage)
        {
            foreach (var b in alle.Where(b => !ReferenceEquals(a, b) && b.Flaeche.Oben > 0.1f))
            {
                if (auslage.Contains(b) && auslage.IndexOf(b) < auslage.IndexOf(a))
                {
                    continue;
                }
                var (fa, fb) = (a.Flaeche, b.Flaeche);
                if (Ueberlappt((fa.X1, fa.Z1, fa.X2, fa.Z2), (fb.X1, fb.Z1, fb.X2, fb.Z2)))
                {
                    Melden(a.Deko, b.Deko);
                }
            }
        }
        var fest = InnenGrundriss.Einrichtung(innen.Gebaeude).Select(e => e.Deko)
            .Where(d => !Wege.Begehbar.Contains(Py.Str(d["typ"])) && !Katalogstueck.IstEffekt(Py.Str(d["typ"]))).ToList();
        for (var i = 0; i < fest.Count; i++)
        {
            for (var j = i + 1; j < fest.Count; j++)
            {
                if (Ueberlappt(Rechteck(fest[i]), Rechteck(fest[j])) && !InnenKatalog.DarfAufliegen(Py.Str(fest[i]["typ"]), Py.Str(fest[j]["typ"])))
                {
                    Melden(fest[i], fest[j]);
                }
            }
        }
    }

    private static (float, float, float, float) Rechteck(PyObjekt d) =>
        ((float)Py.F(d["x"]), (float)Py.F(d["y"]), (float)(Py.F(d["x"]) + Py.F(d["w"])), (float)(Py.F(d["y"]) + Py.F(d["h"])));

    /// <summary>Endpunkt des Wegs vom Eingang wie beim Antippen im Spiel (Ziel Mitte, Reichweite).</summary>
    private static (double X, double Z) WegEnde(Innenraum innen, double zx, double zz, double reichweite) =>
        WegEnde(innen, innen.StartX, innen.StartZ, zx, zz, reichweite);

    /// <summary>Endpunkt des Wegs von (sx, sz) zum Ziel (Phase 13 Welle 5: Start an der Ankunftsstelle einer Etage).</summary>
    private static (double X, double Z) WegEnde(Innenraum innen, double sx, double sz, double zx, double zz, double reichweite)
    {
        var weg = innen.Weg((float)sx, (float)sz, zx, zz, reichweite);
        return weg.Count == 0 ? (sx, sz) : weg[^1];
    }

    /// <summary>
    /// Jede Ware ist zu Fuss erreichbar (Rechnung wie <c>SpielAuslageTests.JedeWareErreichbar</c>: Weg vom Eingang bis
    /// Reichweite 0,9 + halbe kleinere Seite, Ende in 0,9 + 0,3 vom Rand); jede Person ebenso (bis zu ihr hin, Reichweite des Spiels).
    /// </summary>
    private static void Erreichbarkeit(Innenraum innen, string ort, Geschaefte? geschaefte, List<StadtbauFund> funde)
    {
        foreach (var d in Geschaefte.Auslage(new PyObjekt { { "gebaeude", innen.Gebaeude } }))
        {
            double x = Py.F(d["x"]), z = Py.F(d["y"]), w = Py.F(d["w"]), t = Py.F(d["h"]);
            var (ex, ez) = WegEnde(innen, x + (w / 2), z + (t / 2), Wohnung.MoebelReichweite + (Math.Min(w, t) / 2));
            var dx = Math.Max(Math.Max(x - ex, 0), ex - x - w);
            var dz = Math.Max(Math.Max(z - ez, 0), ez - z - t);
            if (Math.Sqrt((dx * dx) + (dz * dz)) > Wohnung.MoebelReichweite + 0.3)
            {
                var kennung = Py.Str(d["ware"]);
                var name = geschaefte is null ? kennung : Warennamen.Name(geschaefte, ort, kennung);
                funde.Add(new StadtbauFund($"Die Ware „{name}“ ({InnenKatalog.Name(Py.Str(d["typ"]))}) ist zu Fuß nicht erreichbar.",
                    "Einen Gang frei machen: davorstehende Stücke verschieben." + Warennamen.Kennung(name, kennung), true, x + (w / 2), z + (t / 2)));
            }
        }
        foreach (var p in innen.Personen)
        {
            var (ex, ez) = WegEnde(innen, p.X, p.Z, Wege.Reichweite);
            if (Math.Sqrt(((ex - p.X) * (ex - p.X)) + ((ez - p.Z) * (ez - p.Z))) > Wege.Reichweite + 0.3)
            {
                funde.Add(new StadtbauFund($"{(p.Name.Length > 0 ? p.Name : p.Id)} ist zu Fuß nicht erreichbar.",
                    "Den Weg zum Tresen frei machen.", true, p.X, p.Z));
            }
        }
    }

    /// <summary>Hinter dem Tresen ist abgesperrt: die Figur kommt nicht bis an den Platz einer Person heran.</summary>
    private static void HinterDemTresen(Innenraum innen, List<StadtbauFund> funde)
    {
        foreach (var p in innen.Personen)
        {
            var (ex, ez) = WegEnde(innen, p.X, p.Z, 0);
            if (Math.Sqrt(((ex - p.X) * (ex - p.X)) + ((ez - p.Z) * (ez - p.Z))) < HinterTresenAbstand)
            {
                funde.Add(new StadtbauFund($"Man kann hinter den Tresen zu {(p.Name.Length > 0 ? p.Name : p.Id)} gehen.",
                    "Den Platz hinter dem Tresen seitlich mit Regalen absperren.", true, p.X, p.Z));
            }
        }
    }

    /// <summary>Ware im Sortiment (Ladepruefung des Spiels <see cref="Geschaefte.Pruefen"/>), in einfachen Worten.</summary>
    private static void Sortiment(string ort, PyObjekt gebaeude, Geschaefte geschaefte, PyObjekt innenraeume, List<StadtbauFund> funde)
    {
        var mit = InnenGrundriss.MitGrundriss(innenraeume, ort, gebaeude);
        foreach (var fehler in geschaefte.Pruefen(mit).Where(f => f.StartsWith(ort + ": ", StringComparison.Ordinal)))
        {
            var rest = fehler[(ort.Length + 2)..];
            // Sammelliste 219 Punkt 2: der Name wie im Spiel, die Kennung nur klein im Tipp
            string Ware(int ab, string vorne)
            {
                var kennung = vorne + rest[ab..];
                return Warennamen.Name(geschaefte, ort, kennung);
            }
            string Kennung(int ab, string vorne) => Warennamen.Kennung(Ware(ab, vorne), vorne + rest[ab..]);
            var (text, tipp) = rest switch
            {
                _ when rest.StartsWith("Auslage ohne Ware im Sortiment: ", StringComparison.Ordinal) =>
                    ($"Die Ware „{Ware(32, "")}“ gibt es im Sortiment nicht.", "Eine Ware aus der Liste zuordnen." + Kennung(32, "")),
                _ when rest.StartsWith("Moebel nicht ausgestellt: ", StringComparison.Ordinal) =>
                    ($"Das Möbel „{Ware(26, "")}“ steht nirgends aus.", "Ein Stück mit dieser Ware setzen." + Kennung(26, "")),
                _ when rest.StartsWith("Teile-Typ ohne Regal: ", StringComparison.Ordinal) =>
                    ($"Für „{Ware(22, "teile:")}“ gibt es kein Regal.", "Ein Regal mit dieser Ware setzen." + Kennung(22, "teile:")),
                _ when rest.StartsWith("Kleidung ohne Auslage: ", StringComparison.Ordinal) =>
                    ($"Für „{Ware(23, Kleidung.Art + ":")}“ gibt es keine Auslage.", "Eine Auslage mit dieser Ware setzen." + Kennung(23, Kleidung.Art + ":")),
                _ => (rest, "Letzten Schritt rückgängig machen."),
            };
            funde.Add(new StadtbauFund(text, tipp, true));
        }
    }
}
