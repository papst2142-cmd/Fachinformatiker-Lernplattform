using System.Diagnostics;
using System.Globalization;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Weisung P8-51 (Teilplan W12, Sammelliste 144): nur Messung, kein Urteil. Welche Bilder im Spiel ueber 16,7 ms
/// liegen und bei welchem Anlass (Oeffnen, erstes Betreten je Ort, Zurueck in die Stadt, erstes Herauszoomen,
/// Fest- und Jahreszeitwechsel, Satz der Festgestalten), dazu die Kosten eines Vorladens (alle Innenraeume und
/// Netze auf der CPU, Ecken fuer die Grafikkarte, Speicher). Start mit LERNPLATTFORM_MESSUNG_TEIL=w12.
/// </summary>
public static partial class AutoMessung
{
    public const string TeilW12 = "w12";

    private const double W12Bild = 1000.0 / 60;

    private static async Task<int> W12LaufAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        void Zeile(string text) => Messwerte.Notiere("W12: " + text);
        var mitschnitt = new List<BildZeiten.Bild>();
        StadtFlaeche.Mitschnitt = mitschnitt;
        var gesamtUhr = Stopwatch.StartNew();
        var speicherStart = GC.GetTotalMemory(false);
        var seite = await SpielSeiteAsync(ansicht);
        Zeile(string.Create(inv, $"Spielseite gebaut und Inhalte geladen: {gesamtUhr.Elapsed.TotalMilliseconds:F0} ms"));
        if (seite?.Spiel is null || !await GlAbwartenAsync(seite))
        {
            Zeile("kein Spiel oder kein GL, nichts gemessen");
            return 0;
        }
        var flaeche = seite.Flaeche;
        Zeile("Grafik: " + flaeche.GlText);
        for (var i = 0; i < 60 && seite.OeffnenMs is null; i++)
        {
            await ansicht.BildFertigAsync();
        }
        Zeile(string.Create(inv, $"GL einrichten (2 Shader-Programme, Grundnetze): {flaeche.GlInitMs:F1} ms; Stadt oeffnen bis erstes Bild {seite.OeffnenMs ?? -1:F0} ms"));
        await Bilder(flaeche, 30);
        Zusammenfassen("Oeffnen: erste Bilder nach dem Anhaengen", mitschnitt, null);

        // Ruhe: 300 Bilder Schwenk in der Stadt (Grundlinie, Speicherbereinigung)
        await Schwenk(flaeche, mitschnitt, 300);
        Zusammenfassen("Grundlinie Stadt (300 Bilder Schwenk)", mitschnitt, null);
        var grundlinie = mitschnitt.Select(BildZeiten.Dauer).OrderBy(d => d).ElementAt((int)(mitschnitt.Count * 0.95));
        Zeile(string.Create(inv, $"Grundlinie 95 %: {grundlinie:F1} ms"));

        // erstes und zweites Herauszoomen (Fernform der Autos und des Paket-Netzes werden beim ersten Mal geladen)
        for (var mal = 1; mal <= 2; mal++)
        {
            var kamera = flaeche.AktuelleKamera!;
            var abstand = kamera.Abstand;
            mitschnitt.Clear();
            kamera.Zoomen(kamera.Abstand / Kamera.AbstandMax);
            kamera.Setzen(seite.Spiel.Szene.Breite / 2, seite.Spiel.Szene.Tiefe / 2);
            await Bilder(flaeche, 10);
            Zusammenfassen(mal + ". Herauszoomen (Gesamtansicht)", mitschnitt, grundlinie);
            kamera.Zoomen(kamera.Abstand / abstand);
            seite.Flaeche.KameraAufFigur();
            await Bilder(flaeche, 10);
        }

        // erstes und zweites Gehen (Schrittbilder der Figur werden beim ersten Schritt gebaut)
        var spiel = Frisch(seite);
        await Bilder(flaeche, 5);
        foreach (var (mal, ziel) in new[] { (1, "bitweiche"), (2, "zuhause") })
        {
            mitschnitt.Clear();
            spiel.GeheZu(ziel);
            for (var i = 0; i < 400 && spiel.Geht; i++)
            {
                await flaeche.NaechstesBildAsync();
            }
            Zusammenfassen(string.Create(inv, $"{mal}. Gehen nach {ziel}"), mitschnitt, grundlinie);
        }

        // jeder Ort mit Innenraum: erstes und zweites Betreten, Zurueck in die Stadt
        spiel = Frisch(seite);
        await Bilder(flaeche, 5);
        var orte = spiel.Szene.Orte.Where(o => Innenraum.OrtFuer(o.Ansicht) is not null).Select(o => o.Id).ToList();
        Zeile("Orte mit Innenraum: " + string.Join(", ", orte));
        foreach (var mal in new[] { 1, 2 })
        {
            foreach (var ort in orte)
            {
                spiel.GeheZu(ort);
                spiel.SofortAnkommen();
                if (!spiel.KannBetreten && spiel.KannWarten)
                {
                    spiel.Warten();
                }
                if (!spiel.KannBetreten)
                {
                    if (mal == 1)
                    {
                        Zeile(ort + ": nicht betretbar (" + spiel.Zeile + ")");
                    }
                    continue;
                }
                // wie im Spiel: die Figur geht hin, die Vorbereitung im Hintergrund hat Zeit
                await Task.Delay(1500);
                await Bilder(flaeche, 3);
                mitschnitt.Clear();
                var uhr = Stopwatch.StartNew();
                spiel.Betreten();
                var betretenMs = uhr.Elapsed.TotalMilliseconds;
                await Bilder(flaeche, 20);
                Zusammenfassen(string.Create(inv, $"{mal}. Betreten {ort} (Betreten() {betretenMs:F1} ms, vorbereitet genommen {spiel.VorbereitetGenommen})"), mitschnitt, grundlinie);
                mitschnitt.Clear();
                uhr.Restart();
                spiel.Verlassen();
                var verlassenMs = uhr.Elapsed.TotalMilliseconds;
                await Bilder(flaeche, 20);
                Zusammenfassen(string.Create(inv, $"{mal}. Zurueck in die Stadt von {ort} (Verlassen() {verlassenMs:F1} ms)"), mitschnitt, grundlinie);
                spiel = Frisch(seite);
                await Bilder(flaeche, 5);
            }
        }

        // Fest- und Jahreszeitwechsel (neue Szene, neuer Satz), je ohne und mit Gestalten
        spiel = Frisch(seite);
        await Bilder(flaeche, 5);
        spiel.Betreten();
        await Bilder(flaeche, 5);
        try
        {
            foreach (var (name, fest, jz, uhrzeit) in new (string, string?, Jahreszeit, long?)[]
            {
                ("Winter ohne Fest", null, Jahreszeit.Winter, null),
                ("Weihnachten abends (Gestalten)", Festzeit.Weihnachten, Jahreszeit.Winter, 1200),
                ("Halloween abends (Gestalten)", Festzeit.Halloween, Jahreszeit.Herbst, 1260),
                ("Ostern tags (Gestalten)", Festzeit.Ostern, Jahreszeit.Fruehling, 720),
                ("Sommer ohne Fest", null, Jahreszeit.Sommer, null),
            })
            {
                Jahreszeiten.Probe(jz);
                Festzeit.Probe(fest);
                Tageslicht.FesteUhr = uhrzeit;
                // wie im Spiel: der neue Tag (Fest, Jahreszeit) gilt beim Hinausgehen in die Stadt
                await Bilder(flaeche, 5);
                mitschnitt.Clear();
                var uhr = Stopwatch.StartNew();
                spiel.Verlassen();
                var neuMs = uhr.Elapsed.TotalMilliseconds;
                await Bilder(flaeche, 20);
                Zusammenfassen(string.Create(inv, $"Hinausgehen bei Wechsel {name} (Verlassen() mit neuer Szene {neuMs:F1} ms)"), mitschnitt, grundlinie);
                spiel = Frisch(seite);
                await Bilder(flaeche, 5);
                spiel.Betreten();
                await Bilder(flaeche, 5);
            }
        }
        finally
        {
            Tageslicht.FesteUhr = null;
            Jahreszeiten.ProbeEnde();
            Festzeit.ProbeEnde();
        }

        // Speicherbereinigung im Lauf: 600 Bilder Schwenk
        await Schwenk(flaeche, mitschnitt, 600);
        Zusammenfassen("Dauerlauf Stadt (600 Bilder Schwenk)", mitschnitt, grundlinie);
        StadtFlaeche.Mitschnitt = null;

        // Vorlade-Kosten auf der CPU (ohne Grafikkarte): Stadtnetze und alle Innenraeume einmal bauen
        spiel = Frisch(seite);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var speicherVor = GC.GetTotalMemory(true);
        var gesamt = Stopwatch.StartNew();
        long ecken = 0, dreiecke = 0;
        void Zaehle(Netz n)
        {
            ecken += n.AnzahlEcken;
            dreiecke += n.Indizes.Count / 3;
        }
        var t = Stopwatch.StartNew();
        var stadtNetze = new List<Netz> { spiel.Szene.NetzBauen() };
        stadtNetze.AddRange(spiel.Szene.Haeuser.Select(spiel.Szene.HausNetz));
        if (spiel.Szene.GruenNetzBauen() is { } gn)
        {
            stadtNetze.Add(gn);
        }
        if (spiel.Grafik is not null)
        {
            stadtNetze.Add(spiel.Szene.PaketNetzBauen(false));
            stadtNetze.Add(spiel.Szene.PaketNetzBauen(true));
        }
        stadtNetze.ForEach(Zaehle);
        Zeile(string.Create(inv, $"CPU Stadtnetze (Flaeche, {spiel.Szene.Haeuser.Count} Haeuser, Gruen, Paket nah/fern): {t.Elapsed.TotalMilliseconds:F1} ms"));
        var raeume = new List<Innenraum>();
        foreach (var ort in spiel.Szene.Orte.Where(o => Innenraum.OrtFuer(o.Ansicht) is not null))
        {
            t.Restart();
            var innen = Innenraum.Bauen(ort.Ansicht, spiel.Ablauf.Stand, spiel.Innenraeume, spiel.Grafik, spiel.GeschaefteDaten, null, spiel.Feste, spiel.Szene.Jahreszeit);
            if (innen is null)
            {
                Zeile(ort.Id + ": kein Innenraum zum Spielstand");
                continue;
            }
            innen.Vorbereiten();
            var vorher = ecken;
            Zaehle(innen.NetzOhneWaende);
            foreach (var w in innen.Waende)
            {
                Zaehle(innen.WandNetzFertig(w));
            }
            raeume.Add(innen);
            Zeile(string.Create(inv, $"CPU Innenraum {ort.Id}: {t.Elapsed.TotalMilliseconds:F1} ms, {ecken - vorher} Ecken"));
        }
        var cpuMs = gesamt.Elapsed.TotalMilliseconds;
        var speicherNach = GC.GetTotalMemory(true);
        Zeile(string.Create(inv, $"CPU alles vorladen: {cpuMs:F0} ms, {raeume.Count} Innenraeume, {ecken} Ecken, {dreiecke} Dreiecke, Grafikspeicher etwa {(ecken * Netz.Zahlen * 4 + dreiecke * 12) / 1024.0 / 1024:F1} MB, Speicher (verwaltet) +{(speicherNach - speicherVor) / 1024.0 / 1024:F1} MB"));
        GC.KeepAlive(raeume);
        GC.KeepAlive(stadtNetze);
        Zeile(string.Create(inv, $"Prozess: Arbeitsspeicher {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB, verwaltet {GC.GetTotalMemory(false) / 1024 / 1024} MB (Start {speicherStart / 1024 / 1024} MB), GC {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}"));
        Zeile(string.Create(inv, $"Toene geladen im Hintergrund: {TonDienst.GeladenMs:F0} ms (still: {TonDienst.Still})"));
        Messwerte.Notiere("URTEIL: gruen");
        return 0;
    }

    private static async Task Bilder(StadtFlaeche flaeche, int anzahl)
    {
        for (var i = 0; i < anzahl; i++)
        {
            await flaeche.NaechstesBildAsync();
        }
    }

    private static async Task Schwenk(StadtFlaeche flaeche, List<BildZeiten.Bild> mitschnitt, int anzahl)
    {
        mitschnitt.Clear();
        var schritt = 0;
        var fertig = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Bild()
        {
            if (mitschnitt.Count >= anzahl)
            {
                flaeche.Dauerlauf = false;
                fertig.TrySetResult();
            }
        }
        flaeche.VorBild = () =>
        {
            var richtung = (schritt++ / 30 % 2 == 0) ? 1f : -1f;
            flaeche.AktuelleKamera?.Verschieben(6f * richtung, 0f, (float)Math.Max(1, flaeche.Bounds.Height));
        };
        flaeche.BildGezeichnet += Bild;
        flaeche.Dauerlauf = true;
        flaeche.AnfordernBild();
        await fertig.Task;
        flaeche.BildGezeichnet -= Bild;
        flaeche.VorBild = null;
    }

    /// <summary>Eine Zeile je Anlass: Bilder, Mitte und hoechste Dauer, wie viele ueber 16,7/33 ms, groesster Teil.</summary>
    private static double Zusammenfassen(string anlass, List<BildZeiten.Bild> bilder, double? grundlinie)
    {
        var inv = CultureInfo.InvariantCulture;
        if (bilder.Count == 0)
        {
            Messwerte.Notiere("W12: " + anlass + ": keine Bilder");
            return 0;
        }
        var dauern = bilder.Select(BildZeiten.Dauer).ToList();
        var sortiert = dauern.OrderBy(d => d).ToList();
        var mitte = sortiert[sortiert.Count / 2];
        var hoechst = bilder[dauern.IndexOf(sortiert[^1])];
        var gc = bilder.Sum(b => b.Gc0 + b.Gc1 + b.Gc2);
        var gc12 = bilder.Sum(b => b.Gc1 + b.Gc2);
        Messwerte.Notiere(string.Create(inv,
            $"W12: {anlass}: {bilder.Count} Bilder, Mitte {mitte:F1} ms, hoechstes {sortiert[^1]:F1} ms (Vorbereitung {hoechst.VorbereitungMs:F1}, Logik {hoechst.LogikMs:F1}, Zeichnen {hoechst.ZeichnenMs:F1}, Danach {hoechst.DanachMs:F1}, Auftraege {hoechst.AuftraegeMs:F1}, Teil {BildZeiten.Teil(hoechst)}), " +
            $"ueber 16,7 ms {dauern.Count(d => d > W12Bild)}, ueber 33 ms {dauern.Count(d => d > 2 * W12Bild)}, GC {gc} (davon Stufe 1/2: {gc12})")
            + (grundlinie is { } g ? string.Create(inv, $", Mehrzeit des hoechsten gegen 95 % der Grundlinie {sortiert[^1] - g:F1} ms") : ""));
        var erstes = bilder[0].Zeit;
        Messwerte.Notiere("W12:   hoechste: " + string.Join("; ", bilder.Select((b, i) => (b, i)).OrderByDescending(x => BildZeiten.Dauer(x.b)).Take(3)
            .Select(x => string.Create(inv, $"Bild {x.i + 1} (+{(x.b.Zeit - erstes) * 1000:F0} ms) {BildZeiten.Dauer(x.b):F1} ms: Vorbereitung {x.b.VorbereitungMs:F1}, Zeichnen {x.b.ZeichnenMs:F1}, Luecke {x.b.LueckeMs:F1}, Layout {x.b.LayoutMs:F1}, Auftraege {x.b.AuftraegeMs:F1}, GC {x.b.Gc0}/{x.b.Gc1}/{x.b.Gc2}"))));
        return mitte;
    }
}
