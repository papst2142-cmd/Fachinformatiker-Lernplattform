using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using FW = Lernplattform.Core.Spiel.Probe.Fusswege;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle Z (A 0.48.0 / S 0.14.0, Weisung P12-71, Nico P12-67 Frage 4 = a): die Regeln fuer eigene Fusswege
/// (<c>stadtdaten.fusswege</c>, <see cref="Fusswege"/>), alle rot:
/// <list type="number">
/// <item>falsch gespeichert (kein Objekt, weniger als zwei Punkte, keine Zahlen, Feld <c>radien</c> nicht je Punkt eine
/// Zahl aus <see cref="Fusswege.Radien"/>); dann baut die Pruefung die Szene nicht;</item>
/// <item>ein Punkt liegt nicht auf der Karte;</item>
/// <item>der Fussweg fuehrt durch ein Haus (Bloecke der gebauten Szene; das Stueck vor einer Tuer bis an die Wand zaehlt nicht,
/// Weisung P12-98);</item>
/// <item>der Fussweg fuehrt durch Wasser (Fluss oder Teich samt Ufer; eine Bruecke gibt es nur fuer Strassen);</item>
/// <item>der Fussweg kreuzt das Gleis (ein Bahnuebergang gibt es nur fuer Strassen und die Wege zu den Tueren);</item>
/// <item>ein Baum, eine Laterne oder ein Katalog-Stueck (ausser Effekten) steht auf dem Fussweg (das Spiel liesse den Baum
/// sonst weg, und die Figur ginge durch das Stueck);</item>
/// <item>eine Ecke ist so eng, dass der Bogen kleiner als die halbe Wegbreite wird (der Weg ueberlappt sich).</item>
/// </list>
/// Fassung 0 (ohne <c>fusswege</c>) hat hier keinen Fund.
/// </summary>
public static partial class StadtbauPruefung
{
    /// <summary>Regel 1 und 2 auf den Daten (false = falsch gespeichert, die Szene nicht bauen).</summary>
    private static bool FusswegDaten(StadtFassung fassung, double breite, double hoehe, List<StadtbauFund> funde)
    {
        if (!Py.Hat(fassung.Stadtdaten, FW.Abschnitt))
        {
            return true;
        }
        if (fassung.Stadtdaten[FW.Abschnitt] is not List<object?> liste)
        {
            funde.Add(new StadtbauFund("Die Fußwege sind falsch gespeichert (keine Liste).", "Letzte Änderung rückgängig machen.", true));
            return false;
        }
        var heil = true;
        for (var i = 0; i < liste.Count; i++)
        {
            var f = FW.Lesen(liste[i]);
            if (f is null || (f.Radien is { } r && r.Any(w => !FW.Radien.Any(e => Math.Abs(e - w) < 1e-6))))
            {
                funde.Add(new StadtbauFund($"Fußweg Nr. {i + 1} ist falsch gespeichert (Punkte oder Radien).", "Fußweg löschen und neu ziehen.", true));
                heil = false;
                continue;
            }
            var draussen = f.Punkte.Where(p => p.X < -1e-3f || p.Y < -1e-3f || p.X > breite + 1e-3f || p.Y > hoehe + 1e-3f).ToList();
            if (draussen.Count > 0)
            {
                var q = draussen[0];
                funde.Add(new StadtbauFund($"Fußweg Nr. {i + 1} liegt nicht ganz auf der Karte.", "Fußweg auf die Karte schieben.", true, q.X, q.Y));
            }
        }
        return heil;
    }

    /// <summary>Regeln 3 bis 7 auf der gebauten Szene.</summary>
    private static void FusswegRegeln(StadtSzene szene, StadtFassung fassung, List<StadtbauFund> funde)
    {
        var fusswege = FW.Alle(fassung.Stadtdaten);
        if (fusswege.Count == 0)
        {
            return;
        }
        var halb = FW.Breite / 2;
        var welt = Objekt(fassung.Karte["welt"]);
        var baeume = ListeOderLeer(welt, "landschaft").OfType<PyObjekt>().Where(l => Py.Gleich(l["typ"], "baum"))
            .SelectMany(l => Linie(l["punkte"])).ToList();
        IReadOnlyList<Nachtlicht.Laterne> laternen;
        try
        {
            laternen = szene.LaternenSkizze.Count == 0 ? [] : szene.Laternen;
        }
        catch (InvalidOperationException)
        {
            laternen = [];   // schon ein Fund der Laternen-Regel
        }
        List<Katalogstueck.Draussen> katalog;
        try
        {
            katalog = Katalogstueck.AusStadtdaten(fassung.Stadtdaten);
        }
        catch (InvalidDataException)
        {
            katalog = [];   // schon ein Fund der Katalog-Regel
        }
        string HausName(StadtHaus h) => szene.Ort(h.Id)?.Name ?? h.Id.Split('#')[0];
        for (var i = 0; i < fusswege.Count; i++)
        {
            var nr = i + 1;
            var glatt = fusswege[i].Glatt;
            var proben = Abgetastet(glatt).ToList();

            // 3: durch ein Haus (P12-98: ausser auf dem Weg vor einer Tuer bis an die Wand)
            foreach (var h in szene.Haeuser)
            {
                if (proben.FirstOrDefault(p => (h.Fein.Aktiv ? h.Drin(p, -Toleranz) : p.X > h.Min.X + Toleranz && p.X < h.Max.X - Toleranz && p.Y > h.Min.Z + Toleranz && p.Y < h.Max.Z - Toleranz)
                        && !AnDerTuer(szene, p)) is var p
                    && p != default)
                {
                    funde.Add(new StadtbauFund($"Fußweg Nr. {nr} führt durch das Haus „{HausName(h)}“.", "Fußweg um das Haus herum ziehen.", true, p.X, p.Y));
                    break;
                }
            }

            // 4: durch Wasser (Fluss samt Ufer, Teich samt Ufer)
            var nass = proben.FirstOrDefault(p => szene.Fluesse.Any(f => Kurve.Abstand(p, f) < FlussUferHalb - Toleranz)
                || szene.Teiche.Any(t => Wasser.Drin(t.MitUfer, p)));
            if (nass != default)
            {
                funde.Add(new StadtbauFund($"Fußweg Nr. {nr} führt durch Wasser. Brücken gibt es nur für Straßen.", "Fußweg am Wasser entlang ziehen oder über eine Straße mit Brücke führen.", true, nass.X, nass.Y));
            }

            // 5: ueber das Gleis
            var gleis = proben.FirstOrDefault(p => szene.Gleise.Any(g => Kurve.Abstand(p, g) < (StadtSzene.GleisBreite / 2) + 0.1f));
            if (gleis != default)
            {
                funde.Add(new StadtbauFund($"Fußweg Nr. {nr} kreuzt das Gleis. Einen Bahnübergang gibt es nur für Straßen.", "Fußweg bis an eine Straße mit Bahnübergang ziehen.", true, gleis.X, gleis.Y));
            }

            // 6: Baum, Laterne, Katalog-Stueck auf dem Weg
            foreach (var b in baeume.Where(b => Kurve.Abstand(b, glatt) < halb + 0.1f).Take(1))
            {
                funde.Add(new StadtbauFund($"Ein Baum steht auf Fußweg Nr. {nr}.", "Baum neben den Fußweg setzen.", true, b.X, b.Y));
            }
            // Wunschplatz (die Laternen-Regel schoebe sie sonst still weg) und gebauter Platz
            foreach (var l in szene.LaternenSkizze.Select(l => new Vector2(l.X, l.Z)).Concat(laternen.Select(l => l.Ort)).Where(l => Kurve.Abstand(l, glatt) < halb).Take(1))
            {
                funde.Add(new StadtbauFund($"Eine Laterne steht auf Fußweg Nr. {nr}.", "Laterne neben den Fußweg setzen.", true, l.X, l.Y));
            }
            foreach (var k in katalog.Where(k => !k.Effekt))
            {
                var r = k.Rechteck;
                if (proben.Any(p => p.X > r.X1 - halb + Toleranz && p.X < r.X2 + halb - Toleranz && p.Y > r.Z1 - halb + Toleranz && p.Y < r.Z2 + halb - Toleranz
                        && AbstandRechteck(r, p) < halb - Toleranz))
                {
                    funde.Add(new StadtbauFund($"„{k.Typ}“ steht auf Fußweg Nr. {nr}.", "Stück neben den Fußweg schieben.", true, k.X, k.Z));
                }
            }

            // 7: zu enge Ecke
            foreach (var (ecke, r) in Kurve.Boegen(fusswege[i].Punkte, fusswege[i].Radien, FW.Radius))
            {
                if (r < halb - 1e-4f)
                {
                    funde.Add(new StadtbauFund($"Fußweg Nr. {nr} ist an der Ecke ({Zf(ecke.X)}; {Zf(ecke.Y)}) zu eng: der Weg überlappt sich.",
                        "Ecke flacher ziehen oder die Stücke daneben länger machen.", true, ecke.X, ecke.Y));
                }
            }
        }
    }

    /// <summary>
    /// Patch S 0.23.1 / A 0.52.1 (Weisung P12-98, Sammelliste 244): ein Probepunkt im Block eines Hauses zaehlt nicht als
    /// „durch das Haus“, wenn er auf dem Stueck vor einer Tuer liegt, das das Spiel selbst als Weg bis an die echte Wand
    /// zeichnet (<see cref="StadtTuer.WegAnfang"/> bis <see cref="StadtTuer.Mitte"/>, quer hoechstens <see cref="Toleranz"/>).
    /// Ein Weg durch die Wand neben der Tuer oder quer durchs Haus bleibt rot; die Toleranz wird nicht allgemein groesser.
    /// </summary>
    private static bool AnDerTuer(StadtSzene szene, Vector2 p) =>
        !FusswegTuerAltFuerTests && szene.Tueren.Any(t => Kurve.Abstand(p, (IReadOnlyList<Vector2>)[t.Mitte, t.WegAnfang]) <= Toleranz);

    /// <summary>Nur fuer Tests (Gegenprobe P12-98): Regel 3 ohne Ausnahme an der Tuer.</summary>
    internal static bool FusswegTuerAltFuerTests { get => FusswegTuerAlt.Value; set => FusswegTuerAlt.Value = value; }

    private static readonly AsyncLocal<bool> FusswegTuerAlt = new();

    private static float AbstandRechteck((float X1, float Z1, float X2, float Z2) r, Vector2 p)
    {
        var dx = MathF.Max(MathF.Max(r.X1 - p.X, 0), p.X - r.X2);
        var dz = MathF.Max(MathF.Max(r.Z1 - p.Y, 0), p.Y - r.Z2);
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}
