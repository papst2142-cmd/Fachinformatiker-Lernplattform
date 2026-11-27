using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Engine festigen 3 (A 1.16.0, Weisung P12-395/P12-399): Teil von <see cref="StadtSzene"/> mit dem Laufraster der Figur.
/// Reiner Umzug aus StadtSzene.cs, nichts geaendert.
/// </summary>
public sealed partial class StadtSzene
{
    /// <summary>
    /// Laufraster des freien Gehens: frei ist eine Zelle, deren Mitte <see cref="LaufKoerper"/> von jedem
    /// Haus (Bloecke der Modelle) weg ist und nicht im Wasser oder auf dem Gleis liegt; Strassen (auch
    /// ueber Fluss und Gleis), Fusswege und die Stehplaetze vor den Tueren sind immer frei.
    /// </summary>
    private static Wegsuche LaufrasterBauen(float breite, float tiefe, List<(double, double, double, double)> hindernisse,
        List<List<Vector2>> wasser, List<List<Vector2>> gleise, List<List<Vector2>> strassen, List<List<Vector2>> wege, List<StadtOrt> orte,
        List<(List<Vector2> Linie, float Halb)>? gehwege = null, List<Teich>? teiche = null, List<Steg>? stege = null, List<List<Vector2>>? inseln = null,
        List<Zebra>? zebras = null, Gelaende? gelaende = null)
    {
        var spalten = (int)(breite / LaufSchritt);
        var zeilen = (int)(tiefe / LaufSchritt);
        var grenze = LaufKoerper * LaufKoerper;
        // Phase 13 Welle 3 (Weisung P12-21): gleiches Ergebnis wie die Pruefung jeder Zelle gegen jede Linie und jedes
        // Hindernis, aber jede Linie und jedes Hindernis prueft nur die Zellen in seinem Umkreis (die Bedingungen je Zelle
        // sind dieselben Ausdruecke; ausserhalb des Umkreises kann keine zutreffen)
        Vector2 Mitte(int s, int z) => new((float)((s + 0.5) * LaufSchritt), (float)((z + 0.5) * LaufSchritt));
        (int Von, int Bis) Bereich(double min, double max, int anzahl) =>
            (Math.Max(0, (int)Math.Floor((min / LaufSchritt) - 0.5) - 1), Math.Min(anzahl - 1, (int)Math.Ceiling((max / LaufSchritt) - 0.5) + 1));
        var immer = new bool[spalten * zeilen];
        var gesperrt = new bool[spalten * zeilen];
        void Linien(List<List<Vector2>> linien, float weite, bool[] ziel)
        {
            foreach (var l in linien)
            {
                if (l.Count == 0)
                {
                    continue;
                }
                // Welle Nachladen (grosse Karten): Strecke fuer Strecke, je nur die Zellen in ihrem Umkreis. Gleiches Ergebnis:
                // der Abstand zum Linienzug ist der kleinste Abstand zu einer Strecke (Kurve.AbstandStrecke, dieselbe Rechnung)
                for (var i = 0; i + 1 < l.Count || (l.Count == 1 && i == 0); i++)
                {
                    var (a, b) = l.Count == 1 ? (l[0], l[0]) : (l[i], l[i + 1]);
                    var (s0, s1) = Bereich(MathF.Min(a.X, b.X) - weite, MathF.Max(a.X, b.X) + weite, spalten);
                    var (r0, r1) = Bereich(MathF.Min(a.Y, b.Y) - weite, MathF.Max(a.Y, b.Y) + weite, zeilen);
                    for (var s = s0; s <= s1; s++)
                    {
                        for (var z = r0; z <= r1; z++)
                        {
                            if (!ziel[(s * zeilen) + z] && (l.Count == 1 ? Vector2.Distance(Mitte(s, z), a) : Kurve.AbstandStrecke(Mitte(s, z), a, b)) <= weite)
                            {
                                ziel[(s * zeilen) + z] = true;
                            }
                        }
                    }
                }
            }
        }
        Linien(strassen, StrassenBreite / 2, immer);
        foreach (var (linie, halb) in gehwege ?? [])
        {
            Linien([linie], halb, immer);
        }
        Linien(wege, 0.25f, immer);
        foreach (var o in orte)
        {
            var steh = new Vector2(o.StehX, o.StehZ);
            var (s0, s1) = Bereich(o.StehX - 0.25, o.StehX + 0.25, spalten);
            var (r0, r1) = Bereich(o.StehZ - 0.25, o.StehZ + 0.25, zeilen);
            for (var s = s0; s <= s1; s++)
            {
                for (var z = r0; z <= r1; z++)
                {
                    if (Vector2.Distance(Mitte(s, z), steh) <= 0.25f)
                    {
                        immer[(s * zeilen) + z] = true;
                    }
                }
            }
        }
        Linien(wasser, (FlussBreite / 2) + 0.1f, gesperrt);
        Linien(gleise, (GleisBreite / 2) + 0.1f, gesperrt);
        // S 0.8.0 (Weisung P12-47): Teich und Ufer gesperrt, Stege frei (laengs bis eine halbe Zelle ueber die Enden,
        // damit der Steg an das Land anschliesst)
        foreach (var teich in teiche ?? [])
        {
            var u = teich.MitUfer;
            var (s0, s1) = Bereich(u.Min(p => p.X), u.Max(p => p.X), spalten);
            var (r0, r1) = Bereich(u.Min(p => p.Y), u.Max(p => p.Y), zeilen);
            for (var s = s0; s <= s1; s++)
            {
                for (var z = r0; z <= r1; z++)
                {
                    if (Wasser.Drin(u, Mitte(s, z)))
                    {
                        gesperrt[(s * zeilen) + z] = true;
                    }
                }
            }
        }
        foreach (var steg in stege ?? [])
        {
            var (x1, z1, x2, z2) = steg.Rechteck;
            var zug = (float)(LaufSchritt / 2);
            // Welle A 1.0.0 (Sammelliste 403): nur ueber das Ende am Land verlaengern; am Ende im Wasser stand die Figur sonst
            // eine halbe Zelle hinter dem Steg auf dem Wasser
            var (zugA, zugB) = StegAltFuerTests ? (zug, zug) : (StegEndeAmLand(steg, teiche, true) ? zug : 0f, StegEndeAmLand(steg, teiche, false) ? zug : 0f);
            if (steg.Quer)
            {
                (z1, z2) = (z1 - zugA, z2 + zugB);
            }
            else
            {
                (x1, x2) = (x1 - zugA, x2 + zugB);
            }
            var (s0, s1) = Bereich(x1, x2, spalten);
            var (r0, r1) = Bereich(z1, z2, zeilen);
            for (var s = s0; s <= s1; s++)
            {
                for (var z = r0; z <= r1; z++)
                {
                    var c = Mitte(s, z);
                    if (c.X >= x1 && c.X <= x2 && c.Y >= z1 && c.Y <= z2)
                    {
                        immer[(s * zeilen) + z] = true;
                    }
                }
            }
        }
        foreach (var (x1, z1, x2, z2) in hindernisse)
        {
            var (s0, s1) = Bereich(Math.Min(x1, x2) - LaufKoerper, Math.Max(x1, x2) + LaufKoerper, spalten);
            var (r0, r1) = Bereich(Math.Min(z1, z2) - LaufKoerper, Math.Max(z1, z2) + LaufKoerper, zeilen);
            for (var s = s0; s <= s1; s++)
            {
                for (var z = r0; z <= r1; z++)
                {
                    var c = Mitte(s, z);
                    var dx = Math.Max(Math.Max(x1 - c.X, 0), c.X - x2);
                    var dz = Math.Max(Math.Max(z1 - c.Y, 0), c.Y - z2);
                    if ((dx * dx) + (dz * dz) < grenze)
                    {
                        gesperrt[(s * zeilen) + z] = true;
                    }
                }
            }
        }
        // Welle K3: die Mittelinsel eines Kreisverkehrs ist nie begehbar (auch nicht der innere Gehweg-Rand des Rings),
        // die Figur geht aussen herum
        foreach (var insel in inseln ?? [])
        {
            var (s0, s1) = Bereich(insel.Min(p => p.X), insel.Max(p => p.X), spalten);
            var (r0, r1) = Bereich(insel.Min(p => p.Y), insel.Max(p => p.Y), zeilen);
            for (var s = s0; s <= s1; s++)
            {
                for (var z = r0; z <= r1; z++)
                {
                    if (Wasser.Drin(insel, Mitte(s, z)))
                    {
                        immer[(s * zeilen) + z] = false;
                        gesperrt[(s * zeilen) + z] = true;
                    }
                }
            }
        }
        // Welle A 0.66.0 (Weisung P12-192, E1): ein Hang steiler als 45 Grad ist gesperrt (auch gegen "immer frei"); ohne
        // Gelaende nichts
        if (gelaende is not null)
        {
            foreach (var (gs, gz) in gelaende.Zellen((float)LaufSchritt, spalten, zeilen))
            {
                var m = Mitte(gs, gz);
                if (gelaende.ZuSteil(m.X, m.Y))
                {
                    immer[(gs * zeilen) + gz] = false;
                    gesperrt[(gs * zeilen) + gz] = true;
                }
            }
        }
        // Welle A 0.54.0 (Weisung P12-115, Nico P12-75 Fragen 3 = a, 4 = a): auf einer Strasse mit Gehweg geht die Figur
        // nur auf dem Gehweg; die Fahrbahn ist gesperrt (auch gegen "immer frei"), ausser auf einem Zebrastreifen (quer bis
        // an die Gehwege). Strassen ohne Gehweg (Fassung 0, alte Projekte) bleiben begehbar wie bisher.
        var fahrbahn = new bool[spalten * zeilen];
        if (FahrbahnFreiAltFuerTests is false)
        {
            Linien([.. (gehwege ?? []).Select(g => g.Linie)], StrassenBreite / 2, fahrbahn);
        }
        foreach (var zebra in zebras ?? [])
        {
            var weite = (StrassenBreite / 2) + 0.2f;
            var (s0, s1) = Bereich(zebra.Mitte.X - weite, zebra.Mitte.X + weite, spalten);
            var (r0, r1) = Bereich(zebra.Mitte.Y - weite, zebra.Mitte.Y + weite, zeilen);
            for (var s = s0; s <= s1; s++)
            {
                for (var z = r0; z <= r1; z++)
                {
                    if (zebra.Drauf(Mitte(s, z), 0f, 0.2f))
                    {
                        fahrbahn[(s * zeilen) + z] = false;
                        immer[(s * zeilen) + z] = true;
                    }
                }
            }
        }
        var frei = new List<(int, int)>();
        for (var s = 0; s < spalten; s++)
        {
            for (var z = 0; z < zeilen; z++)
            {
                if (fahrbahn[(s * zeilen) + z])
                {
                    continue;
                }
                if (immer[(s * zeilen) + z] || !gesperrt[(s * zeilen) + z])
                {
                    frei.Add((s, z));
                }
            }
        }
        return new Wegsuche(frei, LaufSchritt, MitWasserSperre(FeinSperre(breite, tiefe, gehwege, zebras, gleise, strassen, wege), teiche, stege), FeinProbe);
    }
}
