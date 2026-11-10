using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 0.61.0 / S 0.36.0 (Weisung P12-171, Nico 1a 2a): die Buslinie entsteht von selbst aus Strassen und Haltestellen.
/// Ein Rundweg auf dem Strassennetz der automatischen Strecken (<see cref="Autostrecken.NetzAus"/>), der jede Haltestelle so
/// anfaehrt, dass sie rechts liegt: ab der ersten Haltestelle immer zur naechsten noch nicht angefahrenen nach Fahrweg
/// (kuerzester erlaubter Weg, Wenden in Sackgassen wie die Autos), am Ende zurueck zur ersten. Ein Bus je Stadt, ab
/// <see cref="ZweiBusseAb"/> Haltestellen zwei mit halbem Abstand. Der Bus haelt an jeder Haltestelle, an der er auf seiner
/// Spur vorbeikommt, <see cref="HaltSekunden"/> Sekunden Fahrzeit mit der Front am Schild. Nur Darstellung wie
/// <see cref="Verkehr"/>: kein Zufall, keine Ereignisart, kein Spielstand.
/// </summary>
public static class Buslinie
{
    /// <summary>Modell des Busses in modelle.bin (werkzeuge/grafik/bus_modell.py).</summary>
    public const string Modell = "bus";

    /// <summary>Halbe Laenge des Busses im Spiel (Modell 5,72 lang mal Meter × 1,1; Test gegen das Modell).</summary>
    public const float Halb = 0.63f;

    /// <summary>Haltezeit an jeder Haltestelle (Nico P12-153: rund 4 Sekunden), in Fahrzeit wie der Takt der Ampeln.</summary>
    public const double HaltSekunden = 4;

    /// <summary>Ab so vielen Haltestellen fahren zwei Busse (Nico 2a).</summary>
    public const int ZweiBusseAb = 7;

    /// <summary>Nummer des ersten Busses im Verkehr (die Autos haben 0 bis 5; Gestalten der Festzeiten nutzen keine Busse).</summary>
    public const int ErsteNummer = 100;

    /// <summary>So nah muss die Spur an der Haltestelle vorbeifuehren, damit der Bus dort haelt.</summary>
    public const float HaltNah = 0.08f;

    /// <summary>Ergebnis: Spur (geschlossen) und Haltestellen (Weg der Front auf der Spur, Nummer der Haltestelle).</summary>
    public sealed record Linie(List<Vector2> Spur, List<(float Front, int Haltestelle)> Halte, List<int> NichtErreicht);

    /// <summary>
    /// Die Linie zu den Strassen und Haltestellen; null, wenn es keine Haltestelle gibt oder keine erreichbar ist.
    /// <see cref="Linie.NichtErreicht"/> nennt die Haltestellen, die kein Weg anfaehrt (die Pruefung meldet sie rot).
    /// </summary>
    public static Linie? Anlegen(IReadOnlyList<IReadOnlyList<Vector2>> strassen, IReadOnlyList<Haltestelle> haltestellen, float bogen = 0.6f)
    {
        if (haltestellen.Count == 0)
        {
            return null;
        }
        var netz = Autostrecken.NetzAus(strassen);
        var orte = haltestellen.Select(h => AufKante(netz, h)).ToList();
        var nicht = Enumerable.Range(0, haltestellen.Count).Where(i => orte[i] is null).Select(i => haltestellen[i].Nr).ToList();
        var offen = Enumerable.Range(0, haltestellen.Count).Where(i => orte[i] is not null).ToList();
        if (offen.Count == 0)
        {
            return new Linie([], [], nicht);
        }
        var start = orte[offen[0]]!.Value;
        offen.RemoveAt(0);
        var weg = new List<int> { start.Kante };
        var jetzt = start;
        while (offen.Count > 0)
        {
            // die naechste nach Fahrweg (gleich weit: die fruehere in den Daten)
            (int I, float Weite, List<int>? Pfad)? beste = null;
            foreach (var i in offen)
            {
                var ziel = orte[i]!.Value;
                if (ziel.Kante == jetzt.Kante && ziel.U > jetzt.U)
                {
                    var d = ziel.U - jetzt.U;
                    if (beste is null || d < beste.Value.Weite)
                    {
                        beste = (i, d, []);
                    }
                    continue;
                }
                if (Pfad(netz, jetzt.Kante, ziel.Kante) is { } pfad)
                {
                    var d = netz.Kanten[jetzt.Kante].Laenge - jetzt.U + pfad.Take(pfad.Count - 1).Sum(k => netz.Kanten[k].Laenge) + ziel.U;
                    if (beste is null || d < beste.Value.Weite)
                    {
                        beste = (i, d, pfad);
                    }
                }
            }
            if (beste is not { } b)
            {
                nicht.AddRange(offen.Select(i => haltestellen[i].Nr));
                break;
            }
            weg.AddRange(b.Pfad!);
            jetzt = orte[b.I]!.Value;
            offen.Remove(b.I);
        }
        // zurueck zur ersten: der Weg bis vor die erste Kante (sie steht schon vorn)
        if (Pfad(netz, jetzt.Kante, start.Kante) is not { } zurueck)
        {
            return new Linie([], [], [.. haltestellen.Select(h => h.Nr)]);
        }
        weg.AddRange(zurueck.Take(zurueck.Count - 1));
        var mitWende = weg.Select((k, i) => (k, Autostrecken.Folgen(netz, k).Wende && netz.Kanten[weg[(i + 1) % weg.Count]].Teil == netz.Kanten[k].Teil)).ToList();
        if (Autostrecken.Spur(netz, mitWende, bogen, []) is not { } spur || spur.Count < 3)
        {
            return new Linie([], [], [.. haltestellen.Select(h => h.Nr)]);
        }
        spur = Autostrecken.OhneDoppelte(spur);
        return new Linie(spur, Halte(spur, haltestellen), nicht);
    }

    /// <summary>Weg der Front auf der Spur, wo der Bus an einer Haltestelle haelt (je Vorbeifahrt einmal, sortiert).</summary>
    public static List<(float Front, int Haltestelle)> Halte(IReadOnlyList<Vector2> spur, IReadOnlyList<Haltestelle> haltestellen)
    {
        var halte = new List<(float Front, int Haltestelle, float Abstand)>();
        var bis = 0f;
        for (var i = 1; i < spur.Count; i++)
        {
            var (a, b) = (spur[i - 1], spur[i]);
            var d = b - a;
            var l = d.Length();
            if (l < 1e-5f)
            {
                continue;
            }
            d /= l;
            foreach (var h in haltestellen)
            {
                var t = Vector2.Dot(h.Halt - a, d);
                if (t < 0 || t > l || Vector2.Dot(d, h.Fahrt) < 0.9f)
                {
                    continue;
                }
                var abstand = Vector2.Distance(a + (d * t), h.Halt);
                if (abstand > HaltNah)
                {
                    continue;
                }
                var front = bis + t;
                var gleich = halte.FindIndex(x => x.Haltestelle == h.Nr && MathF.Abs(x.Front - front) < 0.5f);
                if (gleich < 0)
                {
                    halte.Add((front, h.Nr, abstand));
                }
                else if (abstand < halte[gleich].Abstand)
                {
                    halte[gleich] = (front, h.Nr, abstand);
                }
            }
            bis += l;
        }
        return [.. halte.OrderBy(x => x.Front).Select(x => (x.Front, x.Haltestelle))];
    }

    /// <summary>Kante des Netzes, auf der die Haltestelle rechts liegt, und Weg der Haltestelle (Schild) auf ihr; null = keine.</summary>
    private static (int Kante, float U)? AufKante(Autostrecken.Netz netz, Haltestelle h)
    {
        (int, float)? beste = null;
        var abstand = float.MaxValue;
        var ziel = h.Mitte + (h.Fahrt * Haltestellen.SchildVor);
        for (var k = 0; k < netz.Kanten.Count; k++)
        {
            var linie = netz.Kanten[k].Linie;
            var bis = 0f;
            for (var i = 1; i < linie.Count; i++)
            {
                var d = linie[i] - linie[i - 1];
                var l = d.Length();
                if (l < 1e-5f)
                {
                    continue;
                }
                d /= l;
                var t = Math.Clamp(Vector2.Dot(ziel - linie[i - 1], d), 0, l);
                var a = Vector2.Distance(linie[i - 1] + (d * t), ziel);
                if (Vector2.Dot(d, h.Fahrt) > 0.9f && a < 0.05f && a < abstand)
                {
                    (beste, abstand) = ((k, bis + t), a);
                }
                bis += l;
            }
        }
        return beste;
    }

    /// <summary>Kuerzester erlaubter Weg nach Laenge ab dem Ende von <paramref name="von"/>: die Kanten danach bis einschliesslich <paramref name="nach"/>.</summary>
    private static List<int>? Pfad(Autostrecken.Netz netz, int von, int nach)
    {
        var abstand = new Dictionary<int, float>();
        var vorher = new Dictionary<int, int>();
        var offen = new PriorityQueue<int, (float, int)>();
        foreach (var n in Autostrecken.Folgen(netz, von).Wege)
        {
            abstand[n] = netz.Kanten[n].Laenge;
            offen.Enqueue(n, (abstand[n], n));
        }
        while (offen.TryDequeue(out var k, out var prio))
        {
            if (prio.Item1 > abstand[k])
            {
                continue;
            }
            if (k == nach)
            {
                var kette = new List<int> { k };
                while (vorher.TryGetValue(kette[^1], out var v))
                {
                    kette.Add(v);
                }
                kette.Reverse();
                return kette;
            }
            foreach (var n in Autostrecken.Folgen(netz, k).Wege)
            {
                var d = abstand[k] + netz.Kanten[n].Laenge;
                if (!abstand.TryGetValue(n, out var alt) || d < alt)
                {
                    abstand[n] = d;
                    vorher[n] = k;
                    offen.Enqueue(n, (d, n));
                }
            }
        }
        return null;
    }

    /// <summary>Die Busse zur Linie: einer, ab <see cref="ZweiBusseAb"/> Haltestellen zwei mit halbem Abstand.</summary>
    public static List<Verkehr.Auto> Busse(Linie linie, int haltestellen)
    {
        if (linie.Spur.Count < 3)
        {
            return [];
        }
        var zahl = haltestellen >= ZweiBusseAb ? 2 : 1;
        var busse = new List<Verkehr.Auto>();
        for (var i = 0; i < zahl; i++)
        {
            var bus = new Verkehr.Auto(Modell, linie.Spur, 0) { Nummer = ErsteNummer + i, Schnell = true, Halb = Halb, Halte = linie.Halte };
            // Start: halber Weg zwischen den Haltestellen (der erste nach der ersten, der zweite einen halben Umlauf weiter)
            var erst = linie.Halte.Count > 0 ? linie.Halte[0].Front + 0.6f : 0f;
            bus.Weg = (erst + (i * bus.Laenge / zahl)) % bus.Laenge;
            busse.Add(bus);
        }
        return busse;
    }
}
