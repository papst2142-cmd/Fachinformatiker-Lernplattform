using System.Diagnostics;
using System.Globalization;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Spiel;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle Nachladen A 0.82.0 (Weisung P12-280, Nico 2b 4a): eigener Teil <see cref="TeilNachladen"/> des Start-Tests mit der grossen
/// Testkarte (256 × 256, Pfad in <see cref="Nachladeprobe.Umgebung"/>). Gemessen:
/// <list type="bullet">
/// <item>„Stadt bauen Testkarte (geladen)“: Bauen, Grundlage und die noetigen Kacheln am Start, einmalig, Grenze
/// <see cref="StadtLast.BauenGrenzeMs"/>; Netzdaten des gleichzeitig Geladenen hoechstens <see cref="StadtLast.NetzdatenGrenzeMb"/> (4a).</item>
/// <item>Gang ueber die ganze Karte: laengstes Bild hoechstens 400 ms, nie eine noetige Kachel im Bild ohne Nahform, Netzdaten
/// zu jeder Zeit unter der Grenze; Zeiten „Kachel nachladen“ nur als Angabe (Vorschlag einer Grenze in der Meldung).</item>
/// <item>Ankunft (Bus/Zug): das Ziel ist vor dem Sprung vorbereitet, danach kein Bild ueber 400 ms und keine fehlende Nahform.</item>
/// <item>Nahansicht an jeder Kachel in jeder Grafikstufe (Abstand 9, Neigung 35, vier Blickrichtungen) unter den Grenzen;
/// Gegenprobe am teuersten Bild: ohne Nachladen (ganze Karte wie bis A 0.81.2) ueber der Grenze.</item>
/// </list>
/// Ohne 3D (Mac in der CI) nur ein Hinweis; ohne Testkarte ein Fehler.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Wert von LERNPLATTFORM_MESSUNG_TEIL fuer diesen Teil (eigener Schritt in der CI).</summary>
    public const string TeilNachladen = "nachladen";

    /// <summary>Grenze fuer das laengste Bild beim Gehen (Nico 4a, wie jeder Messpunkt).</summary>
    public const double GehenGrenzeMs = 400;

    private static async Task<int> NachladenTeilAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Fehler(string text)
        {
            Messwerte.Notiere("  FEHLER: Spiel Testkarte: " + text);
            funde++;
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            Fehler("Spielseite nicht gebaut");
            return funde;
        }
        if (Nachladeprobe.Laden() is not { } datei)
        {
            Fehler("Testkarte nicht gefunden (" + Nachladeprobe.Umgebung + " = " + (Environment.GetEnvironmentVariable(Nachladeprobe.Umgebung) ?? "nicht gesetzt") + ")");
            return funde;
        }
        var vorher = seite.Spiel;
        var gl = await GlAbwartenAsync(seite);
        var stufeVorher = Grafikstufen.Lesen(Zustand.Werte);
        try
        {
            Zustand.Aendere(w => Grafikstufen.Wert(w, Grafikstufe.Mittel));
            seite.GrafikAnwenden();
            var uhr = Stopwatch.StartNew();
            var spiel = Nachladeprobe.Spiel(datei, vorher);
            Messwerte.Notiere(string.Create(inv, $"Spiel: Testkarte geladen ({spiel.Szene.Breite:F0} x {spiel.Szene.Tiefe:F0}, {spiel.Szene.Haeuser.Count} Haeuser) in {uhr.ElapsedMilliseconds} ms"));
            funde += TestkarteBauenMessen(spiel);
            if (!gl)
            {
                Messwerte.Notiere("Spiel: Testkarte Gang, Ankunft und Nahansicht nicht gemessen (ohne 3D kein Nachladen)");
                return funde;
            }
            seite.SpielSetzen(spiel);
            var flaeche = seite.Flaeche;
            for (var i = 0; i < 20 && !flaeche.NachladenAktiv; i++)
            {
                await flaeche.NaechstesBildAsync();
            }
            if (!flaeche.NachladenAktiv)
            {
                Fehler("Nachladen nicht aktiv (feste Kamera draussen erwartet)");
                return funde;
            }
            funde += await TestkarteGangAsync(flaeche, spiel);
            funde += await TestkarteNahAsync(seite, spiel);
        }
        finally
        {
            StadtFlaeche.NachladenAusFuerTests = false;
            Netz.OhneZellenFuerTests = false;
            if (stufeVorher is { } s)
            {
                Zustand.Aendere(w => Grafikstufen.Wert(w, s));
            }
            seite.SpielSetzen(vorher);
            seite.GrafikAnwenden();
            await ansicht.BildFertigAsync();
        }
        return funde;
    }

    /// <summary>„Stadt bauen Testkarte (geladen)“: frisch gebaut wie „Stadt bauen“ (Winter, Weihnachten), dazu Grundlage und Kacheln am Start.</summary>
    private static int TestkarteBauenMessen(StadtSpiel spiel)
    {
        var inv = CultureInfo.InvariantCulture;
        var (x, z) = (spiel.Szene.Breite / 2, spiel.Szene.Tiefe / 2);
        var uhr = Stopwatch.StartNew();
        var szene = StadtSzene.Bauen(spiel.Stadt, spiel.Ablauf.Stand, spiel.Grafik, Festzeit.Weihnachten, spiel.Feste, Jahreszeit.Winter);
        var bauen = uhr.Elapsed.TotalMilliseconds;
        uhr.Restart();
        var lader = new Nachlader(szene, mitGruen: true, hintergrund: false);
        var grundlage = uhr.Elapsed.TotalMilliseconds;
        uhr.Restart();
        // wie der Start im Spiel (Vorladen): die noetigen Kacheln nebeneinander bauen, dann der Reihe nach uebernehmen
        var noetig = Ladeplan.Noetig(x, z, szene.Breite, szene.Tiefe);
        lader.StartBauen(noetig);
        foreach (var k in noetig)
        {
            lader.Hochgeladen(lader.JetztBauen(k));
        }
        var kacheln = uhr.Elapsed.TotalMilliseconds;
        var gesamt = bauen + grundlage + kacheln;
        var mb = lader.Bytes / 1048576.0;
        Messwerte.Notiere(string.Create(inv,
            $"Spiel: Stadt bauen Testkarte (geladen, Winter, Weihnachten): Bauen {bauen:F0} ms, Grundlage {grundlage:F0} ms, {lader.Geladen.Count} Kacheln am Start {kacheln:F0} ms, zusammen {gesamt:F0} ms; gleichzeitig geladen {mb:F1} MB (Grundlage {lader.Grundlage.Bytes / 1048576.0:F1} MB)"));
        var funde = Ergebnis("Spiel: Stadt bauen Testkarte (geladen, einmalig)", Messwiederholung.Einmal(gesamt, StadtLast.BauenGrenzeMs));
        Messwerte.Notiere(string.Create(inv, $"Spiel: Netzdaten Testkarte (gleichzeitig geladen): {mb:F1} MB (Grenze {StadtLast.NetzdatenGrenzeMb:F0} MB)"));
        if (mb > StadtLast.NetzdatenGrenzeMb)
        {
            Messwerte.Notiere(string.Create(inv, $"  FEHLER: Spiel: Netzdaten Testkarte {mb:F1} MB ueber Grenze {StadtLast.NetzdatenGrenzeMb:F0} MB"));
            funde++;
        }
        return funde;
    }

    /// <summary>Gang ueber die ganze Karte und eine Ankunft.</summary>
    private static async Task<int> TestkarteGangAsync(StadtFlaeche flaeche, StadtSpiel spiel)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        var kamera = flaeche.AktuelleKamera!;
        kamera.Neigung = SpielKamera.NeigungMin;
        kamera.Zoomen(kamera.Abstand / SpielKamera.AbstandMax);
        // Schlangenlinie durch alle 16 Viertel (je 64 × 64) mit dreifachem Gehtempo
        var weg = Nachladeprobe.Schlange(spiel.Szene.Breite, spiel.Szene.Tiefe, 64f, 32f);
        var gang = await Nachladeprobe.GangAsync(flaeche, spiel, weg, 3 * StadtSpiel.Tempo);
        Messwerte.Notiere("Spiel: " + Nachladeprobe.Zeile("Gang (dreifaches Gehtempo)", gang));
        funde += Ergebnis("Spiel: laengstes Bild beim Gehen (Testkarte)", Messwiederholung.Einmal(gang.LaengstesMs, GehenGrenzeMs));
        if (gang.BilderMitLoch > 0)
        {
            Messwerte.Notiere(string.Create(inv, $"  FEHLER: Spiel Testkarte Gang: {gang.BilderMitLoch} Bilder mit einer noetigen Kachel ohne Nahform (hoechstens {gang.LoecherHoechstens})"));
            funde++;
        }
        if (gang.HoechstMb > StadtLast.NetzdatenGrenzeMb)
        {
            Messwerte.Notiere(string.Create(inv, $"  FEHLER: Spiel Testkarte Gang: Netzdaten {gang.HoechstMb:F1} MB ueber Grenze {StadtLast.NetzdatenGrenzeMb:F0} MB"));
            funde++;
        }
        if (gang.Bilder < 100 || gang.Weg < 900)
        {
            Messwerte.Notiere(string.Create(inv, $"  FEHLER: Spiel Testkarte Gang zu kurz ({gang.Bilder} Bilder, {gang.Weg:F0} Felder)"));
            funde++;
        }
        // Ankunft: vom Ende des Gangs zur gegenueberliegenden Ecke (wie ein Bus oder Zug, dessen Ziel vorher feststeht)
        var (ankunft, vorbereitet) = await Nachladeprobe.AnkunftAsync(flaeche, spiel, 32f, 32f);
        Messwerte.Notiere("Spiel: " + Nachladeprobe.Zeile("Ankunft (Ziel vorbereitet: " + (vorbereitet ? "ja" : "nein") + ")", ankunft));
        if (!vorbereitet || ankunft.BilderMitLoch > 0 || ankunft.Sofort > 0)
        {
            Messwerte.Notiere(string.Create(inv, $"  FEHLER: Spiel Testkarte Ankunft: vorbereitet {vorbereitet}, Bilder ohne Nahform {ankunft.BilderMitLoch}, sofort gebaut {ankunft.Sofort}"));
            funde++;
        }
        funde += Ergebnis("Spiel: laengstes Bild bei der Ankunft (Testkarte)", Messwiederholung.Einmal(ankunft.LaengstesMs, GehenGrenzeMs));
        return funde;
    }

    /// <summary>Nahansicht an jeder Kachel in jeder Grafikstufe; Gegenprobe ohne Nachladen.</summary>
    private static async Task<int> TestkarteNahAsync(SpielSeite seite, StadtSpiel spiel)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        var flaeche = seite.Flaeche;
        var kacheln = Stadtteile.Alle(spiel.Szene.Breite, spiel.Szene.Tiefe).ToList();
        (long Dreiecke, int Aufrufe, Stadtteile.Kachel Kachel, float Dreh) teuerstMittel = default;
        foreach (var stufe in new[] { Grafikstufe.Niedrig, Grafikstufe.Mittel, Grafikstufe.Hoch })
        {
            Zustand.Aendere(w => Grafikstufen.Wert(w, stufe));
            seite.GrafikAnwenden();
            for (var i = 0; i < 20; i++)
            {
                await flaeche.NaechstesBildAsync();
            }
            var (grenzeD, grenzeA) = Grafikstufen.Grenze(stufe);
            (long Dreiecke, int Aufrufe, Stadtteile.Kachel Kachel, float Dreh) teuerst = (-1, 0, default, 0);
            var aufrufeMax = 0;
            var gemessen = 0;
            var ohneNachladen = 0;
            foreach (var k in kacheln)
            {
                var m = k.Mitte;
                spiel.HinstellenFuerTests(m.X, m.Y);
                var kamera = flaeche.AktuelleKamera!;
                kamera.Neigung = SpielKamera.NeigungMin;
                kamera.Zoomen(kamera.Abstand / SpielKamera.AbstandMax);
                kamera.Setzen(m.X, m.Y);
                await flaeche.NaechstesBildAsync();
                foreach (var dreh in new[] { 0f, 90f, 180f, 270f })
                {
                    kamera.Drehung = dreh;
                    kamera.Setzen(m.X, m.Y);
                    await flaeche.NaechstesBildAsync();
                    gemessen++;
                    if (!flaeche.NachladenAktiv)
                    {
                        ohneNachladen++;
                    }
                    var (d, a) = (flaeche.LetzteDreiecke, flaeche.LetzteZeichenaufrufe);
                    aufrufeMax = Math.Max(aufrufeMax, a);
                    if (d > teuerst.Dreiecke)
                    {
                        teuerst = (d, a, k, dreh);
                    }
                }
            }
            var name = "Spiel: Nahansicht Testkarte " + GrafikTexte.Name(stufe);
            Messwerte.Notiere(string.Create(inv,
                $"{name}: {kacheln.Count} Kacheln, {gemessen} Bilder (Abstand {SpielKamera.AbstandMax:F0}, Neigung {SpielKamera.NeigungMin:F0}, vier Blickrichtungen), teuerstes Bild bei {teuerst.Kachel.Name} Drehung {teuerst.Dreh:F0}: {teuerst.Dreiecke} Dreiecke, hoechstens {aufrufeMax} Zeichenaufrufe (Grenzen {grenzeD}, {grenzeA})"));
            if (teuerst.Dreiecke > grenzeD || aufrufeMax > grenzeA || teuerst.Dreiecke <= 0 || ohneNachladen > 0)
            {
                Messwerte.Notiere(string.Create(inv, $"  FEHLER: {name}: {teuerst.Dreiecke} Dreiecke, {aufrufeMax} Aufrufe (Grenzen {grenzeD}, {grenzeA}), Bilder ohne Nachladen {ohneNachladen}"));
                funde++;
            }
            if (stufe == Grafikstufe.Mittel)
            {
                teuerstMittel = teuerst;
            }
        }
        // Gegenprobe (Mittel, teuerstes Bild): ganze Karte geladen wie bis A 0.81.2 liegt ueber der Grenze
        Zustand.Aendere(w => Grafikstufen.Wert(w, Grafikstufe.Mittel));
        seite.GrafikAnwenden();
        var mitte = teuerstMittel.Kachel.Mitte;
        StadtFlaeche.NachladenAusFuerTests = true;
        spiel.HinstellenFuerTests(mitte.X, mitte.Y);
        flaeche.ProbeNeuLaden();
        for (var i = 0; i < 30 && flaeche.NachladenAktiv; i++)
        {
            await flaeche.NaechstesBildAsync();
        }
        var k2 = flaeche.AktuelleKamera!;
        k2.Neigung = SpielKamera.NeigungMin;
        k2.Zoomen(k2.Abstand / SpielKamera.AbstandMax);
        k2.Drehung = teuerstMittel.Dreh;
        k2.Setzen(mitte.X, mitte.Y);
        await flaeche.NaechstesBildAsync();
        await flaeche.NaechstesBildAsync();
        var ohne = (flaeche.LetzteDreiecke, flaeche.NachladenAktiv);
        StadtFlaeche.NachladenAusFuerTests = false;
        flaeche.ProbeNeuLaden();
        var (grenze, _) = Grafikstufen.Grenze(Grafikstufe.Mittel);
        var gegen = string.Create(inv,
            $"Spiel: Nahansicht Testkarte Gegenprobe ohne Nachladen (ganze Karte wie bis A 0.81.2) bei {teuerstMittel.Kachel.Name}: {ohne.LetzteDreiecke} statt {teuerstMittel.Dreiecke} Dreiecke ({(ohne.LetzteDreiecke > grenze ? "ueber" : "NICHT ueber")} der Grenze {grenze})");
        Messwerte.Notiere(gegen);
        if (ohne.LetzteDreiecke <= grenze || ohne.NachladenAktiv)
        {
            Messwerte.Notiere("  FEHLER: " + gegen);
            funde++;
        }
        for (var i = 0; i < 10; i++)
        {
            await flaeche.NaechstesBildAsync();
        }
        return funde;
    }
}
