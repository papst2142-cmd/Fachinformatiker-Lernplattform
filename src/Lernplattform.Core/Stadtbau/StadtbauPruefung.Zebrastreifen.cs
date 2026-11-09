using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle A 0.54.0 / S 0.26.0 (Weisung P12-115, Meldung P12-75 Abschnitt 4, Sammelliste 255): die Regeln fuer Zebrastreifen
/// (<c>stadtdaten.zebrastreifen</c>, <see cref="Zebrastreifen"/>), alle rot:
/// <list type="number">
/// <item>falsch gespeichert (keine Liste, kein Objekt mit Zahlen x und y);</item>
/// <item>nicht auf der Karte;</item>
/// <item>nicht quer auf einer geraden Strasse (in einer Kurve, im Kreisverkehr oder neben jeder Strasse);</item>
/// <item>auf einer Strasse ohne Gehweg (dort ist die Fahrbahn ohnehin begehbar);</item>
/// <item>auf einer Zufahrt naeher als <see cref="ZebraRingAbstand"/> am Ring eines Kreisverkehrs;</item>
/// <item>naeher als <see cref="ZebraKreuzungAbstand"/> an einer Kreuzung oder Einmuendung;</item>
/// <item>naeher als <see cref="ZebraBahnAbstand"/> an einem Bahnuebergang;</item>
/// <item>naeher als <see cref="ZebraZwischen"/> an einem anderen Zebrastreifen;</item>
/// <item>eine Laterne oder ein Katalog-Stueck (ausser Effekten) steht auf dem Zebrastreifen oder an seinen Enden auf dem Gehweg.</item>
/// </list>
/// Fassung 0 (ohne <c>zebrastreifen</c>) hat hier keinen Fund.
/// </summary>
public static partial class StadtbauPruefung
{
    /// <summary>Kleinster Abstand zum Ring eines Kreisverkehrs (vom Fahrbahnrand des Rings bis zur Mitte des Zebrastreifens).</summary>
    public const float ZebraRingAbstand = 1.0f;

    /// <summary>Kleinster Abstand der Mitte zu einer Kreuzung oder Einmuendung.</summary>
    public const float ZebraKreuzungAbstand = 1.0f;

    /// <summary>Kleinster Abstand der Mitte zu einem Bahnuebergang.</summary>
    public const float ZebraBahnAbstand = 2.0f;

    /// <summary>Kleinster Abstand zweier Zebrastreifen.</summary>
    public const float ZebraZwischen = 2.0f;

    /// <summary>So weit neben der Fahrbahn (auf dem Gehweg) und laengs ueber den Zebrastreifen hinaus muss es frei sein.</summary>
    public const float ZebraFreiQuer = 0.35f;

    /// <inheritdoc cref="ZebraFreiQuer"/>
    public const float ZebraFreiLaengs = 0.15f;

    /// <summary>So nah an der Strassenmitte muss ein gespeicherter Zebrastreifen liegen (das Werkzeug legt ihn genau darauf).</summary>
    public const float ZebraAufMitte = 0.05f;

    /// <summary>Regeln 1 und 2 auf den Daten (false = falsch gespeichert).</summary>
    private static bool ZebraDaten(StadtFassung fassung, double breite, double hoehe, List<StadtbauFund> funde)
    {
        var punkte = Zebrastreifen.Punkte(fassung.Stadtdaten);
        if (punkte is null)
        {
            return true;
        }
        var heil = true;
        for (var i = 0; i < punkte.Count; i++)
        {
            if (punkte[i] is not { } p)
            {
                funde.Add(new StadtbauFund($"Zebrastreifen Nr. {i + 1} ist falsch gespeichert.", "Zebrastreifen löschen und neu setzen.", true));
                heil = false;
                continue;
            }
            if (p.X < 0 || p.Y < 0 || p.X > breite || p.Y > hoehe)
            {
                funde.Add(new StadtbauFund($"Zebrastreifen Nr. {i + 1} liegt außerhalb der Karte.", "Zebrastreifen auf eine Straße der Karte setzen.", true, p.X, p.Y));
            }
        }
        return heil;
    }

    /// <summary>Regeln 3 bis 9 an der gebauten Szene.</summary>
    private static void ZebraRegeln(StadtSzene szene, StadtFassung fassung, List<StadtbauFund> funde)
    {
        var punkte = Zebrastreifen.Punkte(fassung.Stadtdaten);
        if (punkte is null || punkte.Any(p => p is null))
        {
            return;
        }
        var landschaft = ListeOderLeer(Objekt(fassung.Karte["welt"]), "landschaft").OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "strasse")).ToList();
        var linien = landschaft.Select(t => (IReadOnlyList<Vector2>)Linie(t["punkte"])).ToList();
        var ringe = Kreisverkehr.Alle(landschaft.Cast<object?>());
        var kreuzungen = Verkehr.KreuzungenAus(linien);
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
        var gelegt = new List<Zebra>();
        for (var i = 0; i < punkte.Count; i++)
        {
            var nr = i + 1;
            var p = punkte[i]!.Value;
            var z = Zebrastreifen.Legen(nr, p, linien, ZebraAufMitte);
            if (z is null)
            {
                var inKurve = landschaft.Any(t => Linie(t["punkte"]).Count > 2 && Kurve.Abstand(p, Linie(t["punkte"])) < StadtSzene.StrassenBreite / 2);
                funde.Add(inKurve
                    ? new StadtbauFund($"Zebrastreifen Nr. {nr} liegt in einer Kurve oder im Kreisverkehr.", "Zebrastreifen auf ein gerades Stück Straße setzen.", true, p.X, p.Y)
                    : new StadtbauFund($"Zebrastreifen Nr. {nr} liegt nicht quer auf einer geraden Straße.", "Zebrastreifen neu auf eine gerade Straße setzen.", true, p.X, p.Y));
                continue;
            }
            var strasse = landschaft.First(t => Linie(t["punkte"]) is { Count: 2 } l && Kurve.Abstand(z.Mitte, l) < 1e-3f + ZebraAufMitte);
            if (StadtSzene.GehwegBreite(strasse) <= 0)
            {
                funde.Add(new StadtbauFund($"Zebrastreifen Nr. {nr} liegt auf einer Straße ohne Gehweg.", "Zebrastreifen auf eine Straße mit Gehweg setzen.", true, p.X, p.Y));
                continue;
            }
            if (ringe.Any(r => MathF.Abs(Vector2.Distance(z.Mitte, Kreisverkehr.Mitte(r.Ring)) - (float)r.Radius) < (StadtSzene.StrassenBreite / 2) + ZebraRingAbstand))
            {
                funde.Add(new StadtbauFund($"Zebrastreifen Nr. {nr} liegt zu nah am Kreisverkehr (mindestens {Zf(ZebraRingAbstand)} vom Ring).",
                    "Zebrastreifen weiter vom Ring weg schieben.", true, p.X, p.Y));
                continue;
            }
            if (kreuzungen.Any(k => Vector2.Distance(k, z.Mitte) < ZebraKreuzungAbstand))
            {
                funde.Add(new StadtbauFund($"Zebrastreifen Nr. {nr} liegt zu nah an einer Kreuzung oder Einmündung (mindestens {Zf(ZebraKreuzungAbstand)}).",
                    "Zebrastreifen weiter von der Kreuzung weg schieben.", true, p.X, p.Y));
                continue;
            }
            if (szene.Uebergaenge.Any(u => u.Strasse && Vector2.Distance(u.Mitte, z.Mitte) < ZebraBahnAbstand))
            {
                funde.Add(new StadtbauFund($"Zebrastreifen Nr. {nr} liegt zu nah am Bahnübergang (mindestens {Zf(ZebraBahnAbstand)}).",
                    "Zebrastreifen weiter vom Gleis weg schieben.", true, p.X, p.Y));
                continue;
            }
            if (gelegt.FirstOrDefault(g => Vector2.Distance(g.Mitte, z.Mitte) < ZebraZwischen) is { } anderer)
            {
                funde.Add(new StadtbauFund($"Zebrastreifen Nr. {nr} liegt zu nah an Zebrastreifen Nr. {anderer.Nr} (mindestens {Zf(ZebraZwischen)}).",
                    "Einen der beiden weiter weg schieben oder löschen.", true, p.X, p.Y));
                continue;
            }
            gelegt.Add(z);
            if (laternen.FirstOrDefault(l => z.Drauf(l.Ort, ZebraFreiLaengs, ZebraFreiQuer)) is { } laterne)
            {
                funde.Add(new StadtbauFund($"Auf Zebrastreifen Nr. {nr} steht Laterne Nr. {laterne.Nr}.", "Laterne oder Zebrastreifen ein Stück weiter schieben.", true, p.X, p.Y));
            }
            foreach (var k in stuecke.Where(k => !k.Effekt))
            {
                var r = k.Rechteck;
                Vector2[] ecken = [new(k.X, k.Z), new(r.X1, r.Z1), new(r.X2, r.Z1), new(r.X1, r.Z2), new(r.X2, r.Z2)];
                var mitteDrin = z.Mitte.X >= r.X1 && z.Mitte.X <= r.X2 && z.Mitte.Y >= r.Z1 && z.Mitte.Y <= r.Z2;
                if (mitteDrin || ecken.Any(e => z.Drauf(e, ZebraFreiLaengs, ZebraFreiQuer)))
                {
                    funde.Add(new StadtbauFund($"Auf Zebrastreifen Nr. {nr} steht „{k.Typ}“.", "Stück oder Zebrastreifen ein Stück weiter schieben.", true, p.X, p.Y));
                }
            }
        }
    }
}
