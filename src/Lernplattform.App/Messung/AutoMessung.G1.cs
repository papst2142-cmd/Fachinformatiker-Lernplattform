using System.Globalization;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Programm;
using Lernplattform.Spiel;

namespace Lernplattform.Messung;

/// <summary>
/// Welle Grafik G1 (Weisung P12-182, Nico 1a 2c 3a 4b): Grafikstufen im Start-Test. Jede Stufe Niedrig, Mittel, Hoch wird
/// in der Gesamt- und der Nahansicht (Weihnachten, Winter, nachts, alle Orte) gegen IHRE Grenze gemessen (Niedrig 125 000 /
/// 32, Mittel 160 000 / 40, Hoch 200 000 / 48, nie lockern), die Zaehlung ohne Grafikkarte muss jedes Bild treffen. Dazu:
/// Niedrig zeichnet mit 75 % Aufloesung und hoechstens 30 Bildern je Sekunde, Niedrig ist guenstiger und Hoch teurer als
/// Mittel, die Belastung des letzten Aufenthalts ist da, und der Ablauf des ersten Starts (Wahl nach dem Geraet nur ohne
/// Merker, danach nie wieder; Gegenprobe mit Merker).
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Merker <c>grafik_stufe</c> vor dem Start-Test (null = fehlte, der Start-Test setzt Mittel).</summary>
    private static Grafikstufe? GrafikMerkerVorStart;

    private static async Task<int> GrafikStufenAsync(SpielSeite seite)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Grafik " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Grafik " + punkt + ": " + wert);
                funde++;
            }
        }
        var flaeche = seite.Flaeche;
        if (!flaeche.GlBereit || flaeche.AktuelleKamera is null || seite.Spiel is null)
        {
            Messwerte.Notiere("Spiel: Grafikstufen ohne OpenGL nicht gemessen");
            return 0;
        }

        // automatische Wahl dieses Rechners (nur Angabe; der Start-Test misst mit gesetztem Merker)
        var geraet = Grafikstufen.DiesesGeraet(flaeche.GlRenderer);
        var wahl = Grafikstufen.Automatisch(geraet);
        Messwerte.Notiere(string.Create(inv,
            $"Spiel: Grafik automatische Wahl dieses Rechners: {GrafikTexte.Name(wahl.Stufe)} (Regel: {wahl.Regel}; Renderer \"{flaeche.GlRenderer}\", RAM {(geraet.RamBytes is { } r ? (r / (1024.0 * 1024 * 1024)).ToString("F1", inv) + " GB" : "unbekannt")}, Kerne {geraet.Kerne}); Merker vor dem Start-Test: {(GrafikMerkerVorStart is { } m ? Grafikstufen.Name(m) : "fehlte")}"));
        Messwerte.Notiere(string.Create(inv,
            $"Spiel: Grafik Faehigkeiten: 75 % Aufloesung {(flaeche.AufloesungDa ? "ja" : "nein")}, GPU-Zeitabfrage {(flaeche.ZeitabfrageDa ? "ja" : "nein")}"));

        // Belastung des letzten Aufenthalts in der Stadt (die Stadt lief schon mehrere Sekunden)
        var belastung = Belastung.Letzte;
        Pruefe("Belastung gemessen", belastung is not null && belastung.BilderJeSekunde > 0 && belastung.RamBytes > 0,
            belastung is null ? "keine Werte" : string.Join("; ", GrafikTexte.Zeilen(belastung).Select(z => z.Name + " " + z.Wert)));

        var war = Grafikstufen.Lesen(Zustand.Werte) ?? Grafikstufe.Mittel;
        var groesseMittel = flaeche.LetzteBildgroesse;
        var gesamt = new Dictionary<Grafikstufe, (long Dreiecke, int Aufrufe)>();
        try
        {
            foreach (var stufe in (Grafikstufe[])[Grafikstufe.Mittel, Grafikstufe.Niedrig, Grafikstufe.Hoch])
            {
                Zustand.Aendere(w => Grafikstufen.Wert(w, stufe));
                seite.GrafikAnwenden();
                await flaeche.NaechstesBildAsync();
                await flaeche.NaechstesBildAsync();
                var name = GrafikTexte.Name(stufe);
                gesamt[stufe] = await GesamtStufeAsync(seite, stufe, Pruefe);
                if (stufe != Grafikstufe.Mittel)
                {
                    Messwerte.Notiere("Spiel: Nahansicht " + name + " mit allen Orten (Weihnachten, Winter, nachts)");
                    funde += await NahansichtAsync(seite, stufe);
                }
                if (stufe == Grafikstufe.Niedrig)
                {
                    var g = flaeche.LetzteBildgroesse;
                    if (flaeche.AufloesungDa)
                    {
                        var soll = ((int)Math.Round(groesseMittel.Breite * 0.75), (int)Math.Round(groesseMittel.Hoehe * 0.75));
                        Pruefe("Niedrig 75 % Aufloesung", Math.Abs(g.Breite - soll.Item1) <= 1 && Math.Abs(g.Hoehe - soll.Item2) <= 1,
                            string.Create(inv, $"Bild {g.Breite} x {g.Hoehe}, Mittel {groesseMittel.Breite} x {groesseMittel.Hoehe}, Soll {soll.Item1} x {soll.Item2}"));
                    }
                    else
                    {
                        Messwerte.Notiere("Spiel: Grafik Niedrig: 75 % Aufloesung nicht moeglich (Treiber ohne eigenen Bildspeicher), gezeichnet mit 100 %");
                    }
                    // Bildbremse: die Flaeche im Dauerlauf von selbst zeichnen lassen (NaechstesBildAsync fordert selbst Bilder
                    // an und ginge an der Bremse vorbei), eine Sekunde einschwingen, dann zwei Sekunden zaehlen. Ohne Dauerlauf
                    // ruht die Flaeche, wenn sich nichts bewegt, und die Zahl der letzten Sekunde stammte noch aus der
                    // Nahansicht (Windows, voller Lauf 828: 48)
                    flaeche.Dauerlauf = true;
                    flaeche.AnfordernBild();
                    await Task.Delay(1000);
                    var bremseUhr = System.Diagnostics.Stopwatch.StartNew();
                    var bremseVorher = flaeche.BilderGesamt;
                    await Task.Delay(2000);
                    var bremseBilder = flaeche.BilderGesamt - bremseVorher;
                    var bremseSek = bremseUhr.Elapsed.TotalSeconds;
                    flaeche.Dauerlauf = false;
                    Pruefe("Niedrig hoechstens 30 Bilder je Sekunde",
                        bremseBilder > 0 && bremseBilder <= (int)Math.Floor(bremseSek * Grafikstufen.BilderNiedrig) + 1,
                        string.Create(inv, $"{bremseBilder} Bilder in {bremseSek:0.00} s im Dauerlauf, {flaeche.BilderJeSekunde} in der letzten Sekunde"));
                }
                if (stufe == Grafikstufe.Mittel)
                {
                    Pruefe("Mittel 100 % Aufloesung", flaeche.LetzteBildgroesse == groesseMittel,
                        string.Create(inv, $"Bild {flaeche.LetzteBildgroesse.Breite} x {flaeche.LetzteBildgroesse.Hoehe}"));
                }
            }
            // Vergleich der Stufen: Niedrig guenstiger (Nah), Hoch teurer (Gesamt, volle Formen bis 40)
            if (NahTeuerstes.TryGetValue(Grafikstufe.Niedrig, out var nahN) && NahTeuerstes.TryGetValue(Grafikstufe.Mittel, out var nahM))
            {
                Pruefe("Niedrig guenstiger als Mittel (Nahansicht)", nahN.Dreiecke < nahM.Dreiecke,
                    string.Create(inv, $"Niedrig {nahN.Dreiecke}, Mittel {nahM.Dreiecke}"));
            }
            Pruefe("Hoch teurer als Mittel (Gesamtansicht)", gesamt[Grafikstufe.Hoch].Dreiecke > gesamt[Grafikstufe.Mittel].Dreiecke,
                string.Create(inv, $"Hoch {gesamt[Grafikstufe.Hoch].Dreiecke}, Mittel {gesamt[Grafikstufe.Mittel].Dreiecke}"));
            Pruefe("Niedrig guenstiger als Mittel (Gesamtansicht)", gesamt[Grafikstufe.Niedrig].Dreiecke < gesamt[Grafikstufe.Mittel].Dreiecke,
                string.Create(inv, $"Niedrig {gesamt[Grafikstufe.Niedrig].Dreiecke}, Mittel {gesamt[Grafikstufe.Mittel].Dreiecke}"));

            // erster Start: ohne Merker waehlt das Programm einmal nach dem Geraet und merkt es
            Zustand.Aendere(w => w.Remove(Grafikstufen.Schluessel));
            var erste = seite.GrafikErsterStart();
            var gemerkt = Grafikstufen.Lesen(Zustand.Werte);
            Pruefe("erster Start waehlt nach dem Geraet", erste is not null && erste.Stufe == wahl.Stufe && gemerkt == wahl.Stufe
                && flaeche.GrafikWerte == Grafikstufen.Werte(wahl.Stufe),
                $"Wahl {(erste is null ? "keine" : GrafikTexte.Name(erste.Stufe))}, gemerkt {(gemerkt is { } gm ? Grafikstufen.Name(gm) : "nichts")}");
            // Gegenprobe: mit Merker (auch einer anderen Stufe) waehlt das Programm nie wieder
            var andere = wahl.Stufe == Grafikstufe.Hoch ? Grafikstufe.Niedrig : Grafikstufe.Hoch;
            Zustand.Aendere(w => Grafikstufen.Wert(w, andere));
            var zweite = seite.GrafikErsterStart();
            Pruefe("Gegenprobe mit Merker keine neue Wahl", zweite is null && Grafikstufen.Lesen(Zustand.Werte) == andere,
                $"Wahl {(zweite is null ? "keine" : GrafikTexte.Name(zweite.Stufe))}, Merker {Grafikstufen.Name(Grafikstufen.Lesen(Zustand.Werte) ?? Grafikstufe.Mittel)}");
        }
        finally
        {
            Zustand.Aendere(w => Grafikstufen.Wert(w, war));
            seite.GrafikAnwenden();
            await flaeche.NaechstesBildAsync();
            await flaeche.NaechstesBildAsync();
        }
        return funde;
    }

    /// <summary>Gesamtansicht (ganz weit weg ueber der Mitte) in einer Stufe gegen ihre Grenze, Zaehlung ohne Grafikkarte gleich.</summary>
    private static async Task<(long Dreiecke, int Aufrufe)> GesamtStufeAsync(SpielSeite seite, Grafikstufe stufe, Action<string, bool, string> pruefe)
    {
        var inv = CultureInfo.InvariantCulture;
        var flaeche = seite.Flaeche;
        var kamera = flaeche.AktuelleKamera!;
        var spiel = seite.Spiel!;
        var (abstand, zx, zz, drehung) = (kamera.Abstand, kamera.ZielX, kamera.ZielZ, kamera.Drehung);
        try
        {
            kamera.Drehung = 0;
            kamera.Zoomen(kamera.Abstand / Kamera.AbstandMax);
            kamera.Setzen(spiel.Szene.Breite / 2, spiel.Szene.Tiefe / 2);
            await flaeche.NaechstesBildAsync();
            await flaeche.NaechstesBildAsync();
            var (d, a) = (flaeche.LetzteDreiecke, flaeche.LetzteZeichenaufrufe);
            var (gd, ga) = Grafikstufen.Grenze(stufe);
            var name = GrafikTexte.Name(stufe);
            pruefe("Gesamtansicht " + name, d > 0 && d <= gd && a > 0 && a <= ga,
                string.Create(inv, $"{d} Dreiecke, {a} Zeichenaufrufe (Grenzen {gd} Dreiecke, {ga} Aufrufe)"));
            if (flaeche.InstancingDa)
            {
                var w = Grafikstufen.Werte(stufe);
                var szene = spiel.Szene;
                var fest = new Core.Stadtbau.StadtbauGrenzen.Draussen(szene, szene.Vorbereiten()).Bild(flaeche.LetzteSichtProj, flaeche.LetzterAbstand, werte: w);
                var bewegt = Core.Stadtbau.StadtbauSpiel.Bewegt(spiel, w.Fern(flaeche.LetzterAbstand), StadtFlaeche.AutosHoechstens, halb: w.AutosHalb);
                var ganz = (fest.Dreiecke + bewegt.Dreiecke, fest.Aufrufe + bewegt.Aufrufe);
                pruefe("Gesamtansicht " + name + " Zaehlung ohne Grafikkarte", ganz == (d, a),
                    string.Create(inv, $"gezaehlt {ganz.Item1}/{ganz.Item2}, gezeichnet {d}/{a}"));
            }
            return (d, a);
        }
        finally
        {
            kamera.Drehung = drehung;
            kamera.Zoomen(kamera.Abstand / abstand);
            kamera.Setzen(zx, zz);
        }
    }
}
