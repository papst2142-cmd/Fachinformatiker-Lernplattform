using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle K3 (A 0.45.0 / S 0.11.0, Weisungen P12-58/P12-59, Kurzplan Meldung P12-37 Abschnitt 5): die Regeln fuer
/// Kreisverkehre (<see cref="Kreisverkehr"/>), alle rot:
/// <list type="number">
/// <item>Feld <c>kreisverkehr</c> falsch (Radius nicht 2 oder 3) oder die Punkte bilden keinen Ring mit diesem Radius;</item>
/// <item>weniger als 3 oder mehr als 4 Zufahrten;</item>
/// <item>eine Zufahrt ist nicht gerade oder zielt nicht auf die Mitte;</item>
/// <item>zwei Zufahrten naeher als 60 Grad;</item>
/// <item>eine Strasse kreuzt den Ring oder fuehrt ueber die Insel, ohne dort zu enden;</item>
/// <item>Haus, Baum, Fluss, Teich, Bahn oder Katalog-Stueck auf Ring oder Insel;</item>
/// <item>Laterne auf der Insel, auf der Fahrbahn des Rings oder in einer Zufahrt;</item>
/// <item>der Kreisverkehr (samt Gehweg) ragt ueber die Karte.</item>
/// </list>
/// Fassung 0 hat keinen Kreisverkehr: hier kein Fund.
/// </summary>
public static partial class StadtbauPruefung
{
    private static void KreisverkehrRegeln(StadtSzene szene, StadtFassung fassung, List<StadtbauFund> funde)
    {
        var welt = Objekt(fassung.Karte["welt"]);
        var (breite, hoehe) = ((float)Py.F(welt["breite"]), (float)Py.F(welt["hoehe"]));
        var land = ListeOderLeer(welt, "landschaft").OfType<PyObjekt>().ToList();
        var strassenDaten = land.Where(l => Py.Gleich(l["typ"], "strasse")).ToList();
        var strassen = strassenDaten.Select(l => Linie(l["punkte"])).ToList();
        for (var i = 0; i < strassenDaten.Count; i++)
        {
            var daten = strassenDaten[i];
            if (!Kreisverkehr.IstRing(daten))
            {
                continue;
            }
            var nr = i + 1;
            var ring = strassen[i];
            // 1: Daten
            if (Kreisverkehr.Radius(daten) is not { } radius)
            {
                funde.Add(new StadtbauFund($"Der Kreisverkehr (Straße Nr. {nr}) hat einen Radius, den es nicht gibt. Erlaubt sind 2 und 3.",
                    "Kreisverkehr löschen und aus der Liste links neu setzen.", true, ring.FirstOrDefault().X, ring.FirstOrDefault().Y));
                continue;
            }
            if (!Kreisverkehr.Rund(ring, radius))
            {
                funde.Add(new StadtbauFund($"Der Kreisverkehr (Straße Nr. {nr}) ist falsch gespeichert: seine Punkte bilden keinen Ring mit Radius {Zf(radius)}.",
                    "Kreisverkehr löschen und aus der Liste links neu setzen.", true, ring.FirstOrDefault().X, ring.FirstOrDefault().Y));
                continue;
            }
            var m = Kreisverkehr.Mitte(ring);
            var aussen = (float)radius + (StadtSzene.StrassenBreite / 2) + StadtSzene.GehwegBreite(daten);
            var insel = Kreisverkehr.InselRadius(radius);
            var name = $"Der Kreisverkehr (Straße Nr. {nr})";

            // 8: ueber die Karte
            if (m.X - aussen < -1e-3f || m.Y - aussen < -1e-3f || m.X + aussen > breite + 1e-3f || m.Y + aussen > hoehe + 1e-3f)
            {
                funde.Add(new StadtbauFund($"{name} ragt über den Rand der Karte.", "Kreisverkehr weiter auf die Karte schieben.", true, m.X, m.Y));
            }

            // 2 bis 4: Zufahrten
            var zufahrten = Kreisverkehr.Zufahrten(ring, strassen, i);
            if (zufahrten.Count < Kreisverkehr.ZufahrtenMindestens || zufahrten.Count > Kreisverkehr.ZufahrtenHoechstens)
            {
                funde.Add(new StadtbauFund($"{name} hat {zufahrten.Count} Zufahrten. Erlaubt sind 3 oder 4.",
                    zufahrten.Count < Kreisverkehr.ZufahrtenMindestens ? "Eine gerade Straße bis an den Ring bauen." : "Eine Zufahrt löschen.", true, m.X, m.Y));
            }
            foreach (var (k, amAnfang, _) in zufahrten)
            {
                var z = strassen[k];
                var ende = amAnfang ? z[0] : z[^1];
                if (z.Count != 2 || Strassenkurve.IstKurve(strassenDaten[k]))
                {
                    funde.Add(new StadtbauFund($"Straße Nr. {k + 1} führt in einer Kurve auf den Kreisverkehr (Straße Nr. {nr}). Eine Zufahrt muss gerade sein.",
                        "Vor dem Ring ein gerades Stück bauen.", true, ende.X, ende.Y));
                }
                else if (!Kreisverkehr.ZieltAufMitte(z, amAnfang, m))
                {
                    funde.Add(new StadtbauFund($"Die Zufahrt Straße Nr. {k + 1} zielt nicht auf die Mitte des Kreisverkehrs (Straße Nr. {nr}).",
                        "Kreisverkehr neu setzen oder die Zufahrt gerade auf die Mitte ausrichten.", true, ende.X, ende.Y));
                }
            }
            var winkel = zufahrten.Select(z => z.Grad).Order().ToList();
            for (var a = 0; a < winkel.Count && winkel.Count >= 2; a++)
            {
                var abstand = a + 1 < winkel.Count ? winkel[a + 1] - winkel[a] : winkel[0] + 360 - winkel[a];
                if (abstand < Kreisverkehr.ZufahrtenAbstandGrad - 1e-6)
                {
                    var g = (winkel[a] + (abstand / 2)) * Math.PI / 180;
                    funde.Add(new StadtbauFund($"Zwei Zufahrten des Kreisverkehrs (Straße Nr. {nr}) liegen nur {Zf(abstand)}° auseinander, mindestens 60° sind nötig.",
                        "Eine der beiden Zufahrten löschen.", true, m.X + (radius * Math.Cos(g)), m.Y + (radius * Math.Sin(g))));
                }
            }

            // 5: Strasse kreuzt den Ring oder die Insel, ohne dort zu enden
            for (var k = 0; k < strassen.Count; k++)
            {
                var s = strassen[k];
                if (k == i || s.Count < 2)
                {
                    continue;
                }
                Vector2? treffer = Schnittpunkte(s, ring).Where(q => Vector2.Distance(q, s[0]) > Kreisverkehr.AmRing && Vector2.Distance(q, s[^1]) > Kreisverkehr.AmRing)
                    .Select(q => (Vector2?)q).FirstOrDefault() ?? (Kurve.Abstand(m, s) < insel ? m : null);
                if (treffer is { } tr)
                {
                    funde.Add(new StadtbauFund($"Straße Nr. {k + 1} kreuzt den Kreisverkehr (Straße Nr. {nr}). Straßen enden am Ring.",
                        "Die Straße am Ring enden lassen oder um den Kreisverkehr herum führen.", true, tr.X, tr.Y));
                }
            }

            // 6: Haus, Baum, Wasser, Bahn, Katalog-Stueck auf Ring oder Insel
            foreach (var h in szene.Haeuser)
            {
                var dx = MathF.Max(MathF.Max(h.Min.X - m.X, 0), m.X - h.Max.X);
                var dz = MathF.Max(MathF.Max(h.Min.Z - m.Y, 0), m.Y - h.Max.Z);
                if ((h.Fein.Aktiv ? h.Abstand(m) : MathF.Sqrt((dx * dx) + (dz * dz))) < aussen - 0.02f)
                {
                    funde.Add(new StadtbauFund($"„{szene.Ort(h.Id)?.Name ?? h.Id.Split('#')[0]}“ steht auf dem Kreisverkehr (Straße Nr. {nr}).",
                        "Haus vom Kreisverkehr weg schieben.", true, (h.Min.X + h.Max.X) / 2, (h.Min.Z + h.Max.Z) / 2));
                }
            }
            foreach (var baum in land.Where(l => Py.Gleich(l["typ"], "baum")))
            {
                foreach (var p in Linie(baum["punkte"]).Where(p => Vector2.Distance(p, m) < aussen).Take(1))
                {
                    funde.Add(new StadtbauFund($"Ein Baum steht auf dem Kreisverkehr (Straße Nr. {nr}). Die Insel hat schon ihren Baum.",
                        "Baum neben den Kreisverkehr setzen.", true, p.X, p.Y));
                }
            }
            foreach (var f in szene.Fluesse)
            {
                if (Kurve.Abstand(m, f) < aussen + ((StadtSzene.FlussBreite + 0.3f) / 2))
                {
                    funde.Add(new StadtbauFund($"Der Fluss führt über den Kreisverkehr (Straße Nr. {nr}).", "Kreisverkehr vom Fluss weg schieben.", true, m.X, m.Y));
                }
            }
            foreach (var t in szene.Teiche)
            {
                if (Wasser.Drin(t.MitUfer, m) || t.MitUfer.Any(q => Vector2.Distance(q, m) < aussen) || Kurve.Abstand(m, [.. t.MitUfer, t.MitUfer[0]]) < aussen)
                {
                    funde.Add(new StadtbauFund($"Ein Teich liegt auf dem Kreisverkehr (Straße Nr. {nr}).", "Teich oder Kreisverkehr verschieben.", true, m.X, m.Y));
                }
            }
            foreach (var g in szene.Gleise)
            {
                if (Kurve.Abstand(m, g) < aussen + (StadtSzene.GleisBreite / 2))
                {
                    funde.Add(new StadtbauFund($"Die Bahn führt über den Kreisverkehr (Straße Nr. {nr}).", "Kreisverkehr vom Gleis weg schieben.", true, m.X, m.Y));
                }
            }
            try
            {
                foreach (var k in Katalogstueck.AusStadtdaten(fassung.Stadtdaten).Where(k => !k.Effekt && Vector2.Distance(new Vector2(k.X, k.Z), m) < insel))
                {
                    funde.Add(new StadtbauFund($"„{k.Typ}“ steht auf der Insel des Kreisverkehrs (Straße Nr. {nr}).", "Stück neben den Kreisverkehr schieben.", true, k.X, k.Z));
                }
            }
            catch (InvalidDataException)
            {
                // meldet KatalogDraussen
            }

            // 7: Laternen
            var lnr = 0;
            foreach (var (x, z, _) in fassung.Laternen)
            {
                lnr++;
                var p = new Vector2(x, z);
                if (Vector2.Distance(p, m) < insel + 0.05f)
                {
                    funde.Add(new StadtbauFund($"Laterne Nr. {lnr} steht auf der Insel des Kreisverkehrs (Straße Nr. {nr}).", "Laterne an den Außenrand des Rings setzen.", true, x, z));
                }
                else if (Kurve.Abstand(p, ring) < StadtSzene.StrassenBreite / 2)
                {
                    funde.Add(new StadtbauFund($"Laterne Nr. {lnr} steht auf der Fahrbahn des Kreisverkehrs (Straße Nr. {nr}).", "Laterne an den Außenrand des Rings setzen.", true, x, z));
                }
                else if (zufahrten.FindIndex(zf => Kurve.Abstand(p, strassen[zf.Strasse]) < StadtSzene.StrassenBreite / 2) is var zi and >= 0)
                {
                    funde.Add(new StadtbauFund($"Laterne Nr. {lnr} steht in einer Zufahrt des Kreisverkehrs (Straße Nr. {zufahrten[zi].Strasse + 1}).", "Laterne an den Rand der Zufahrt setzen.", true, x, z));
                }
            }
        }
    }
}
