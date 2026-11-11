using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle A 0.60.0 / S 0.35.0 (Weisung P12-166, Kurzplan P12-79 Wunsch 6): die Regeln fuer Ampeln
/// (<c>stadtdaten.ampeln</c>, <see cref="Ampeln"/>), alle rot:
/// <list type="number">
/// <item>falsch gespeichert (keine Liste, kein Objekt mit Zahlen x und y);</item>
/// <item>nicht auf der Karte;</item>
/// <item>am Ring eines Kreisverkehrs (dort gibt es keine Ampel);</item>
/// <item>nicht auf einer Kreuzung oder Einmuendung (mindestens drei Arme);</item>
/// <item>zwei Ampeln an derselben Kreuzung;</item>
/// <item>ein Arm ist kuerzer als <see cref="AmpelArmMindest"/> (kein Platz zum Halten vor der Haltelinie);</item>
/// <item>naeher als <see cref="AmpelBahnAbstand"/> an einem Bahnuebergang;</item>
/// <item>naeher als <see cref="AmpelZebraAbstand"/> an einem Zebrastreifen;</item>
/// <item>eine Laterne, ein Baum, ein Katalog-Stueck (ausser Effekten) oder ein Haus steht auf dem Platz eines Masts.</item>
/// </list>
/// Fassung 0 (ohne <c>ampeln</c>) hat hier keinen Fund.
/// </summary>
public static partial class StadtbauPruefung
{
    /// <summary>Kleinste Laenge jedes Arms von der Mitte der Kreuzung bis zum Ende der Strasse oder zur naechsten Kreuzung.</summary>
    public const float AmpelArmMindest = Ampeln.HalteBis + 0.25f;

    /// <summary>Kleinster Abstand der Mitte zu einem Bahnuebergang.</summary>
    public const float AmpelBahnAbstand = 2.0f;

    /// <summary>Kleinster Abstand der Mitte zu einem Zebrastreifen (die Furten liegen bis 1,01 von der Mitte).</summary>
    public const float AmpelZebraAbstand = 2.0f;

    /// <summary>Regeln 1 und 2 auf den Daten.</summary>
    private static void AmpelDaten(StadtFassung fassung, double breite, double hoehe, List<StadtbauFund> funde)
    {
        var punkte = Ampeln.Punkte(fassung.Stadtdaten);
        if (punkte is null)
        {
            return;
        }
        for (var i = 0; i < punkte.Count; i++)
        {
            if (punkte[i] is not { } p)
            {
                funde.Add(new StadtbauFund($"Ampel Nr. {i + 1} ist falsch gespeichert.", "Ampel löschen und neu setzen.", true));
                continue;
            }
            if (p.X < 0 || p.Y < 0 || p.X > breite || p.Y > hoehe)
            {
                funde.Add(new StadtbauFund($"Ampel Nr. {i + 1} liegt außerhalb der Karte.", "Ampel auf eine Kreuzung der Karte setzen.", true, p.X, p.Y));
            }
        }
    }

    /// <summary>Regeln 3 bis 9 an der gebauten Szene.</summary>
    private static void AmpelRegeln(StadtSzene szene, StadtFassung fassung, List<StadtbauFund> funde)
    {
        var punkte = Ampeln.Punkte(fassung.Stadtdaten);
        if (punkte is null || punkte.Any(p => p is null))
        {
            return;
        }
        var landschaft = ListeOderLeer(Objekt(fassung.Karte["welt"]), "landschaft").OfType<Daten.PyObjekt>().Where(t => Py.Gleich(t["typ"], "strasse")).ToList();
        var linien = landschaft.Select(t => (IReadOnlyList<Vector2>)Linie(t["punkte"])).ToList();
        var kreuzungen = Verkehr.KreuzungenAus(linien);
        // fuer die Laenge der Arme zaehlen nur echte Kreuzungen und Einmuendungen (nicht der Anschluss Kurve an Strasse)
        var echte = kreuzungen.Where(k => Ampeln.Arme(k, linien).Count >= 3).ToList();
        IReadOnlyList<Nachtlicht.Laterne> laternen;
        try
        {
            laternen = szene.LaternenSkizze.Count == 0 ? [] : szene.Laternen;
        }
        catch (InvalidOperationException)
        {
            laternen = [];   // schon ein Fund der Laternen-Regel
        }
        List<Katalogstueck.Draussen> stuecke;
        try
        {
            stuecke = Katalogstueck.AusStadtdaten(fassung.Stadtdaten);
        }
        catch (InvalidDataException)
        {
            stuecke = [];   // schon ein Fund der Katalog-Regel
        }
        var gelegt = new List<Ampel>();
        for (var i = 0; i < punkte.Count; i++)
        {
            var nr = i + 1;
            var p = punkte[i]!.Value;
            var a = Ampeln.Legen(nr, p, linien, Ampeln.AufMitte);
            if (a is null)
            {
                var amRing = kreuzungen.Any(k => Vector2.Distance(k, p) <= Ampeln.Fang && Ampeln.AmRing(k, linien)) || Ampeln.AmRing(p, linien);
                funde.Add(amRing
                    ? new StadtbauFund($"Ampel Nr. {nr} steht am Kreisverkehr.", "Am Kreisverkehr gibt es keine Ampel: die Ampel löschen.", true, p.X, p.Y)
                    : new StadtbauFund($"Ampel Nr. {nr} steht an keiner Kreuzung oder Einmündung.", "Ampel neu auf eine Kreuzung setzen.", true, p.X, p.Y));
                continue;
            }
            if (gelegt.FirstOrDefault(g => Vector2.Distance(g.Mitte, a.Mitte) < 0.5f) is { } doppelt)
            {
                funde.Add(new StadtbauFund($"Ampel Nr. {nr} steht an derselben Kreuzung wie Ampel Nr. {doppelt.Nr}.", "Eine der beiden löschen.", true, p.X, p.Y));
                continue;
            }
            gelegt.Add(a);
            if (a.Arme.Any(arm => ArmLaenge(a.Mitte, arm, linien, echte) < AmpelArmMindest))
            {
                funde.Add(new StadtbauFund($"An Ampel Nr. {nr} ist eine Straße kürzer als {Zf(AmpelArmMindest)} bis zum Ende oder zur nächsten Kreuzung (kein Platz zum Halten).",
                    "Die Straße verlängern oder die Ampel an eine andere Kreuzung setzen.", true, p.X, p.Y));
            }
            if (szene.Uebergaenge.Any(u => u.Strasse && Vector2.Distance(u.Mitte, a.Mitte) < AmpelBahnAbstand))
            {
                funde.Add(new StadtbauFund($"Ampel Nr. {nr} steht zu nah am Bahnübergang (mindestens {Zf(AmpelBahnAbstand)}).", "Ampel an eine andere Kreuzung setzen.", true, p.X, p.Y));
            }
            if (szene.Zebras.FirstOrDefault(z => Vector2.Distance(z.Mitte, a.Mitte) < AmpelZebraAbstand) is { } zebra)
            {
                funde.Add(new StadtbauFund($"Zebrastreifen Nr. {zebra.Nr} liegt zu nah an Ampel Nr. {nr} (mindestens {Zf(AmpelZebraAbstand)}).",
                    "Zebrastreifen weiter weg schieben: an der Ampel geht die Figur über die Furt.", true, zebra.Mitte.X, zebra.Mitte.Y));
            }
            foreach (var (fuss, _, _) in a.Masten)
            {
                var r = Ampeln.MastGrundriss;
                if (laternen.FirstOrDefault(l => Vector2.Distance(l.Ort, fuss) < r) is { } laterne)
                {
                    funde.Add(new StadtbauFund($"Laterne Nr. {laterne.Nr} steht auf dem Platz eines Masts von Ampel Nr. {nr}.", "Laterne ein Stück weiter schieben.", true, fuss.X, fuss.Y));
                }
                if (szene.Baeume.Any(b => Vector2.Distance(b.Mitte, fuss) < r + b.R))
                {
                    funde.Add(new StadtbauFund($"Ein Baum steht auf dem Platz eines Masts von Ampel Nr. {nr}.", "Baum ein Stück weiter schieben.", true, fuss.X, fuss.Y));
                }
                foreach (var k in stuecke.Where(k => !k.Effekt))
                {
                    var rk = k.Rechteck;
                    if (fuss.X >= rk.X1 - r && fuss.X <= rk.X2 + r && fuss.Y >= rk.Z1 - r && fuss.Y <= rk.Z2 + r)
                    {
                        funde.Add(new StadtbauFund($"„{k.Typ}“ steht auf dem Platz eines Masts von Ampel Nr. {nr}.", "Stück ein Stück weiter schieben.", true, fuss.X, fuss.Y));
                    }
                }
                if (szene.Haeuser.Any(h => fuss.X > h.Min.X + 0.05f && fuss.X < h.Max.X - 0.05f && fuss.Y > h.Min.Z + 0.05f && fuss.Y < h.Max.Z - 0.05f))
                {
                    funde.Add(new StadtbauFund($"Ein Mast von Ampel Nr. {nr} steht in einem Haus.", "Haus von der Kreuzung weg schieben.", true, fuss.X, fuss.Y));
                }
            }
        }
    }

    /// <summary>
    /// Laenge eines Arms: entlang der Strasse von der Mitte bis zu ihrem Ende oder bis zur naechsten Kreuzung. Geht die
    /// Strasse am Ende ohne Kreuzung in eine andere ueber (Kurve an gerade Strasse), zaehlt der Arm als lang genug.
    /// </summary>
    internal static float ArmLaenge(Vector2 mitte, Vector2 arm, IReadOnlyList<IReadOnlyList<Vector2>> strassen, IReadOnlyList<Vector2> kreuzungen)
    {
        foreach (var s in strassen)
        {
            for (var i = 1; i < s.Count; i++)
            {
                var (a, b) = (s[i - 1], s[i]);
                var d = b - a;
                var l = d.Length();
                if (l < 1e-4f)
                {
                    continue;
                }
                var t = Math.Clamp(Vector2.Dot(mitte - a, d / l), 0f, l);
                if (Vector2.Distance(a + (d / l * t), mitte) > 0.05f)
                {
                    continue;
                }
                // Weg in Richtung des Arms: vorwaerts (zu b) oder rueckwaerts (zu a)
                List<Vector2> weg;
                if (l - t > 0.05f && Vector2.Dot(Vector2.Normalize(b - mitte), arm) >= 0.985f)
                {
                    weg = [mitte, .. s.Skip(i)];
                }
                else if (t > 0.05f && Vector2.Dot(Vector2.Normalize(a - mitte), arm) >= 0.985f)
                {
                    weg = [mitte, .. s.Take(i).Reverse()];
                }
                else
                {
                    continue;
                }
                var laenge = 0f;
                for (var k = 1; k < weg.Count; k++)
                {
                    var stueck = Vector2.Distance(weg[k - 1], weg[k]);
                    for (var x = 0.05f; x <= stueck; x += 0.05f)
                    {
                        var q = Vector2.Lerp(weg[k - 1], weg[k], x / stueck);
                        if (kreuzungen.Any(kr => Vector2.Distance(kr, mitte) > 0.5f && Vector2.Distance(kr, q) < 0.05f))
                        {
                            return laenge + x;
                        }
                    }
                    laenge += stueck;
                }
                var ende = weg[^1];
                var weiter = strassen.Any(o => !ReferenceEquals(o, s) && (Vector2.Distance(o[0], ende) <= 0.02f || Vector2.Distance(o[^1], ende) <= 0.02f));
                return weiter ? float.MaxValue : laenge;
            }
        }
        return float.MaxValue;
    }
}
