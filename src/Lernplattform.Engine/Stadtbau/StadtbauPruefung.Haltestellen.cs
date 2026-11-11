using System.Numerics;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle A 0.61.0 / S 0.36.0 (Weisung P12-171, Kurzplan P12-102 Punkt f): die Regeln fuer Bushaltestellen
/// (<c>stadtdaten.haltestellen</c>, <see cref="Haltestellen"/>) und Parkplaetze (<c>stadtdaten.parkplaetze</c>,
/// <see cref="Parkplaetze"/>), alle rot:
/// <list type="number">
/// <item>falsch gespeichert; Parkplatz mit weniger als 2 oder mehr als 12 Plaetzen oder nicht 1 oder 2 Reihen;</item>
/// <item>nicht auf der Karte;</item>
/// <item>nicht an einer geraden Strasse mit Gehweg;</item>
/// <item>Haltestelle: weniger als <see cref="HaltKreuzungAbstand"/> von Kreuzung, Einmuendung oder Kreisverkehr, weniger als
/// <see cref="HaltZebraAbstand"/> von Zebrastreifen, Ampel oder Bahnuebergang, zwei auf derselben Seite naeher als
/// <see cref="HaltGleicheSeite"/>, mehr als <see cref="Haltestellen.Hoechstens"/>, eine Laterne am Schild;</item>
/// <item>Haeuschen bzw. Flaeche des Parkplatzes nicht frei (Haus, Strasse, Gleis, Wasser, Baum, Laterne, Katalog-Stueck,
/// andere Haltestelle oder anderer Parkplatz);</item>
/// <item>Einfahrt des Parkplatzes naeher als <see cref="ParkplatzEinfahrtAbstand"/> an Kreuzung, Zebrastreifen, Ampel oder Haltestelle;</item>
/// <item>der Bus erreicht eine Haltestelle nicht oder bleibt stecken (wartet laenger als 20 Sekunden).</item>
/// </list>
/// Fassung 0 (ohne beide Abschnitte) hat hier keinen Fund.
/// </summary>
public static partial class StadtbauPruefung
{
    /// <summary>Kleinster Abstand des Haltebereichs (Zickzack-Linie) zu einer Kreuzung, Einmuendung oder einem Kreisverkehr.</summary>
    public const float HaltKreuzungAbstand = 1.5f;

    /// <summary>Kleinster Abstand des Haltebereichs zu Zebrastreifen, Ampel oder Bahnuebergang.</summary>
    public const float HaltZebraAbstand = 2.0f;

    /// <summary>Kleinster Abstand zweier Haltestellen auf derselben Seite derselben Strasse.</summary>
    public const float HaltGleicheSeite = 3.0f;

    /// <summary>Kleinster Abstand einer Laterne zum Mast der Haltestelle.</summary>
    public const float HaltLaterneAbstand = 0.3f;

    /// <summary>Kleinster Abstand der Einfahrt eines Parkplatzes zu Kreuzung, Zebrastreifen, Ampel oder Haltestelle.</summary>
    public const float ParkplatzEinfahrtAbstand = 1.0f;

    /// <summary>Regeln 1 und 2 auf den Daten (falsch gespeicherte Eintraege laesst die Szene weg, Rot sperrt den Export).</summary>
    private static void HaltDaten(StadtFassung fassung, double breite, double hoehe, List<StadtbauFund> funde)
    {
        var punkte = Haltestellen.Punkte(fassung.Stadtdaten);
        for (var i = 0; i < (punkte?.Count ?? 0); i++)
        {
            if (punkte![i] is not { } p)
            {
                funde.Add(new StadtbauFund($"Haltestelle Nr. {i + 1} ist falsch gespeichert.", "Haltestelle löschen und neu setzen.", true));
            }
            else if (p.X < 0 || p.Y < 0 || p.X > breite || p.Y > hoehe)
            {
                funde.Add(new StadtbauFund($"Haltestelle Nr. {i + 1} liegt außerhalb der Karte.", "Haltestelle an eine Straße der Karte setzen.", true, p.X, p.Y));
            }
        }
        var parks = Parkplaetze.Eintraege(fassung.Stadtdaten);
        for (var i = 0; i < (parks?.Count ?? 0); i++)
        {
            if (parks![i] is not var (p, n, r))
            {
                funde.Add(new StadtbauFund($"Parkplatz Nr. {i + 1} ist falsch gespeichert.", "Parkplatz löschen und neu setzen.", true));
                continue;
            }
            if (!Parkplaetze.Gueltig(n, r))
            {
                funde.Add(new StadtbauFund($"Parkplatz Nr. {i + 1} hat {n} Plätze in {r} Reihen (erlaubt {Parkplaetze.Mindestens} bis {Parkplaetze.Hoechstens} Plätze in 1 oder 2 Reihen).",
                    "Plätze und Reihen rechts unter „Ausgewähltes Stück“ einstellen.", true, p.X, p.Y));
            }
            if (p.X < 0 || p.Y < 0 || p.X > breite || p.Y > hoehe)
            {
                funde.Add(new StadtbauFund($"Parkplatz Nr. {i + 1} liegt außerhalb der Karte.", "Parkplatz an eine Straße der Karte setzen.", true, p.X, p.Y));
            }
        }
    }

    /// <summary>Regeln 3 bis 7 an der gebauten Szene.</summary>
    private static void HaltRegeln(StadtSzene szene, StadtFassung fassung, List<StadtbauFund> funde)
    {
        var punkte = Haltestellen.Punkte(fassung.Stadtdaten);
        var parks = Parkplaetze.Eintraege(fassung.Stadtdaten);
        if (punkte is null && parks is null)
        {
            return;
        }
        var welt = Objekt(fassung.Karte["welt"]);
        var landschaft = ListeOderLeer(welt, "landschaft").OfType<Daten.PyObjekt>().ToList();
        var strassenDaten = landschaft.Where(t => Py.Gleich(t["typ"], "strasse")).ToList();
        var linien = strassenDaten.Select(t => (IReadOnlyList<Vector2>)Linie(t["punkte"])).ToList();
        var echte = Verkehr.KreuzungenAus(linien).Where(k => Ampeln.Arme(k, linien).Count >= 3).ToList();
        var ringe = strassenDaten.Where(Kreisverkehr.IstRing).Select(t => (IReadOnlyList<Vector2>)Linie(t["punkte"])).ToList();
        var gehwege = Haltestellen.Gehwege(landschaft);
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

        // die Flaechen aller gelegten Haltestellen (Haeuschen) und Parkplaetze, damit sie sich nicht ueberlappen
        var flaechen = new List<(string Name, Vector2[] Ecken)>();
        flaechen.AddRange(szene.Haltestellen.Select(h => ($"Haltestelle Nr. {h.Nr}", h.HaeuschenEcken())));
        flaechen.AddRange(szene.Parkplaetze.Select(pp => ($"Parkplatz Nr. {pp.Nr}", pp.Ecken())));

        // --- Haltestellen ---
        if (punkte is { Count: > Haltestellen.Hoechstens })
        {
            funde.Add(new StadtbauFund($"Es gibt {punkte.Count} Haltestellen, höchstens {Haltestellen.Hoechstens} sind erlaubt.", "Haltestellen löschen.", true));
        }
        for (var i = 0; i < (punkte?.Count ?? 0); i++)
        {
            if (punkte![i] is { } p && Haltestellen.Legen(i + 1, p, gehwege) is null)
            {
                funde.Add(new StadtbauFund($"Haltestelle Nr. {i + 1} steht an keiner geraden Straße mit Gehweg.", "Haltestelle neben ein gerades Stück Straße mit Gehweg setzen.", true, p.X, p.Y));
            }
        }
        foreach (var h in szene.Haltestellen)
        {
            var name = $"Haltestelle Nr. {h.Nr}";
            var (a, b) = (h.Mitte + (h.Fahrt * (Haltestellen.SchildVor - Haltestellen.ZickzackLang)), h.Mitte + (h.Fahrt * Haltestellen.SchildVor));
            IReadOnlyList<Vector2> bereich = [a, b];
            var m = h.Mitte;
            if (echte.Any(k => Kurve.Abstand(k, bereich) < HaltKreuzungAbstand) || ringe.Any(r => Kurve.Abstand(a, r) < HaltKreuzungAbstand || Kurve.Abstand(b, r) < HaltKreuzungAbstand))
            {
                funde.Add(new StadtbauFund($"{name} liegt zu nah an einer Kreuzung, Einmündung oder einem Kreisverkehr (mindestens {Zf(HaltKreuzungAbstand)}).",
                    "Haltestelle längs der Straße weiter weg schieben.", true, m.X, m.Y));
            }
            if (szene.Zebras.Any(z => Kurve.Abstand(z.Mitte, bereich) < HaltZebraAbstand) || szene.Ampeln.Any(am => Kurve.Abstand(am.Mitte, bereich) < HaltZebraAbstand)
                || szene.Uebergaenge.Any(u => u.Strasse && Kurve.Abstand(u.Mitte, bereich) < HaltZebraAbstand))
            {
                funde.Add(new StadtbauFund($"{name} liegt zu nah an einem Zebrastreifen, einer Ampel oder einem Bahnübergang (mindestens {Zf(HaltZebraAbstand)}).",
                    "Haltestelle längs der Straße weiter weg schieben.", true, m.X, m.Y));
            }
            if (szene.Haltestellen.FirstOrDefault(o => o.Nr < h.Nr && Vector2.Dot(o.Fahrt, h.Fahrt) > 0.9f && Vector2.Dot(o.Aussen, h.Aussen) > 0.9f
                && MathF.Abs(Vector2.Dot(o.Mitte - h.Mitte, h.Aussen)) < 0.05f && Vector2.Distance(o.Mitte, h.Mitte) < HaltGleicheSeite) is { } nah)
            {
                funde.Add(new StadtbauFund($"{name} liegt zu nah an Haltestelle Nr. {nah.Nr} auf derselben Seite (mindestens {Zf(HaltGleicheSeite)}).",
                    "Eine der beiden weiter weg schieben oder löschen.", true, m.X, m.Y));
            }
            if (laternen.FirstOrDefault(l => Vector2.Distance(l.Ort, h.Schild) < HaltLaterneAbstand) is { } laterne)
            {
                funde.Add(new StadtbauFund($"Laterne Nr. {laterne.Nr} steht am Schild von {name}.", "Laterne ein Stück weiter schieben.", true, h.Schild.X, h.Schild.Y));
            }
            FlaecheFrei(name, "Wartehäuschens", h.HaeuschenEcken(), szene, linien, laternen, stuecke, flaechen, funde);
        }

        // --- Parkplaetze ---
        for (var i = 0; i < (parks?.Count ?? 0); i++)
        {
            if (parks![i] is var (p, n, r) && Parkplaetze.Gueltig(n, r) && Parkplaetze.Legen(i + 1, p, n, r, gehwege) is null)
            {
                funde.Add(new StadtbauFund($"Parkplatz Nr. {i + 1} liegt an keiner geraden Straße mit Gehweg.", "Parkplatz neben ein gerades Stück Straße mit Gehweg setzen.", true, p.X, p.Y));
            }
        }
        foreach (var pp in szene.Parkplaetze)
        {
            var name = $"Parkplatz Nr. {pp.Nr}";
            var (e1, e2) = pp.Einfahrt;
            var mitte = pp.Vorn - (pp.Aussen * pp.Halb);
            if (echte.Any(k => Vector2.Distance(k, mitte) < ParkplatzEinfahrtAbstand) || ringe.Any(rg => Kurve.Abstand(mitte, rg) < ParkplatzEinfahrtAbstand)
                || szene.Zebras.Any(z => Vector2.Distance(z.Mitte, mitte) < ParkplatzEinfahrtAbstand)
                || szene.Ampeln.Any(am => Vector2.Distance(am.Mitte, mitte) < ParkplatzEinfahrtAbstand + Ampeln.FurtMitte)
                || szene.Haltestellen.Any(h => Kurve.Abstand(mitte, [h.Mitte + (h.Fahrt * (Haltestellen.SchildVor - Haltestellen.ZickzackLang)), h.Mitte + (h.Fahrt * Haltestellen.SchildVor)]) < ParkplatzEinfahrtAbstand))
            {
                funde.Add(new StadtbauFund($"Die Einfahrt von {name} liegt zu nah an einer Kreuzung, einem Zebrastreifen, einer Ampel oder einer Haltestelle (mindestens {Zf(ParkplatzEinfahrtAbstand)}).",
                    "Parkplatz längs der Straße weiter weg schieben.", true, e1.X, e1.Y));
            }
            if (laternen.FirstOrDefault(l => Kurve.Abstand(l.Ort, [e1, e2]) < (Parkplaetze.Gasse / 2) + 0.05f) is { } laterne)
            {
                funde.Add(new StadtbauFund($"Laterne Nr. {laterne.Nr} steht in der Einfahrt von {name}.", "Laterne ein Stück weiter schieben.", true, laterne.Ort.X, laterne.Ort.Y));
            }
            FlaecheFrei(name, "Parkplatzes", pp.Ecken(), szene, linien, laternen, stuecke, flaechen, funde);
        }

        // --- Bus (nur mit Haltestellen) ---
        if (szene.Haltestellen.Count == 0)
        {
            return;
        }
        var linie = Buslinie.Anlegen(szene.Strassen, szene.Haltestellen);
        foreach (var nr in linie?.NichtErreicht ?? [])
        {
            var h = szene.Haltestellen.First(x => x.Nr == nr);
            funde.Add(new StadtbauFund($"Der Bus erreicht Haltestelle Nr. {nr} nicht so, dass sie rechts liegt.",
                "Die Straße an das Netz anschließen oder die Haltestelle auf die andere Straßenseite setzen.", true, h.Mitte.X, h.Mitte.Y));
        }
        if (linie is not null && linie.NichtErreicht.Count == 0)
        {
            foreach (var p in BusSteckt(Verkehr.Fuer(szene)))
            {
                funde.Add(new StadtbauFund("Hier bleibt der Bus stecken: er wartet länger als 20 Sekunden.",
                    "Kreuzungen weiter auseinander bauen oder Haltestellen anders setzen.", true, p.X, p.Y));
            }
        }
    }

    /// <summary>Stellen, an denen ein Bus laenger als 20 Sekunden steht (90 Sekunden Fahrt in Schritten von 1/20 s).</summary>
    internal static List<Vector2> BusSteckt(Verkehr v, double sekunden = 90, double grenze = 20)
    {
        var busse = v.Autos.Where(a => a.Nummer >= Buslinie.ErsteNummer).ToList();
        var steht = new double[busse.Count];
        var gemeldet = new bool[busse.Count];
        var stellen = new List<Vector2>();
        const double schritt = 1.0 / 20;
        for (var t = 0; t < (int)Math.Round(sekunden / schritt); t++)
        {
            v.Weiter(schritt, -1000, -1000);
            for (var i = 0; i < busse.Count; i++)
            {
                steht[i] = busse[i].Haelt ? steht[i] + schritt : 0;
                if (steht[i] > grenze && !gemeldet[i])
                {
                    gemeldet[i] = true;
                    stellen.Add(busse[i].Lage().Ort);
                }
            }
        }
        return stellen;
    }

    /// <summary>Ist die Flaeche frei? Probe an 7 × 7 Punkten im Rechteck gegen Haeuser, Strassen, Gleise, Wasser, Baeume, Laternen, Katalog-Stuecke und andere Flaechen.</summary>
    private static void FlaecheFrei(string name, string was, Vector2[] ecken, StadtSzene szene, List<IReadOnlyList<Vector2>> linien,
        IReadOnlyList<Nachtlicht.Laterne> laternen, List<Katalogstueck.Draussen> stuecke, List<(string Name, Vector2[] Ecken)> flaechen, List<StadtbauFund> funde)
    {
        var proben = new List<Vector2>();
        for (var i = 0; i <= 6; i++)
        {
            for (var j = 0; j <= 6; j++)
            {
                var u = Vector2.Lerp(ecken[0], ecken[1], (0.02f + (i * 0.16f)));
                var v = ecken[3] - ecken[0];
                proben.Add(u + (v * (0.02f + (j * 0.16f))));
            }
        }
        var mitte = (ecken[0] + ecken[2]) / 2;
        string? grund = null;
        if (proben.Any(q => szene.Haeuser.Any(h => q.X > h.Min.X && q.X < h.Max.X && q.Y > h.Min.Z && q.Y < h.Max.Z)))
        {
            grund = "ein Haus";
        }
        else if (proben.Any(q => linien.Any(l => Kurve.Abstand(q, l) < StadtSzene.StrassenBreite / 2)))
        {
            grund = "eine Straße";
        }
        else if (proben.Any(q => szene.Gleise.Any(g => Kurve.Abstand(q, g) < Bahnuebergang.LichtraumHalb)))
        {
            grund = "ein Gleis";
        }
        else if (proben.Any(q => szene.Teiche.Any(w => Wasser.Drin(w.MitUfer, q)) || szene.Fluesse.Any(f => Kurve.Abstand(q, f) < FlussUferHalb)))
        {
            grund = "Wasser";
        }
        else if (szene.Baeume.Any(b => proben.Any(q => Vector2.Distance(b.Mitte, q) < b.R)))
        {
            grund = "ein Baum";
        }
        else if (laternen.Any(l => Wasser.Drin(ecken, l.Ort)))
        {
            grund = "eine Laterne";
        }
        else if (stuecke.Where(k => !k.Effekt).Any(k => proben.Any(q => q.X > k.Rechteck.X1 && q.X < k.Rechteck.X2 && q.Y > k.Rechteck.Z1 && q.Y < k.Rechteck.Z2)))
        {
            grund = "ein Stück aus dem Katalog";
        }
        else if (flaechen.FirstOrDefault(f => f.Name != name && proben.Any(q => Wasser.Drin(f.Ecken, q))) is { Name: not null } andere)
        {
            grund = andere.Name;
        }
        if (grund is not null)
        {
            funde.Add(new StadtbauFund($"Der Platz des {was} von {name} ist nicht frei: dort ist {grund}.", "Längs der Straße an eine freie Stelle schieben.", true, mitte.X, mitte.Y));
        }
    }
}
