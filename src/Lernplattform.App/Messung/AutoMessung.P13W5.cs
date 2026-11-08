using System.Diagnostics;
using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Spiel;
using Lernplattform.Views;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 13 Welle 5 „Etagen“ (Weisung P12-73): das Moebelhaus bekommt im Start-Test ein 1. Obergeschoss mit einer Treppe
/// (Fassung 0 hat keine Etagen; nur im Speicher). Messpunkte „Spiel: Etage wechseln (erstes Mal, ohne Vorrat, bis zum
/// Bild)“ (einmalig, streng 400 ms) und „Spiel: Etage wechseln (bis zum Bild)“ (wiederholbar, 400 ms, mit vorbereiteter
/// Nachbar-Etage). Prueft: Etage, Figur an der Ankunft, Zeile „aufenthalt“ im Spielstand, oben kein Weg hinaus.
/// Layout: Knopf vor der Treppe und das 1. Obergeschoss.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>innenraeume.json mit einem 1. Obergeschoss im Moebelhaus und einer Treppe im Flur (vorn nach +x).</summary>
    internal static PyObjekt MitEtageImMoebelhaus(PyObjekt innenraeume)
    {
        var innen = (PyObjekt)Py.Kopie(innenraeume)!;
        var eg = Objekt(Objekt(innen["moebelhaus"])["gebaeude"]);
        Py.Setzen(eg, Etagen.FeldEtagen, new List<object?> { new PyObjekt { { "gebaeude", Etagen.Vorschlag(eg) } } });
        var halte = new List<object?>();
        foreach (var k in new[] { 0, 1 })
        {
            var d = new PyObjekt
            {
                { "typ", Etagen.Treppe }, { "x", 8.0 }, { "y", 12.2 }, { "w", 3.0 }, { "h", 1.6 }, { "dreh", 3L }, { Etagen.FeldLink, "u1" },
            };
            Liste(Objekt(Etagen.Gebaeude(eg, k)["flur"])["deko"]).Add(d);
            var (ax, ay, blick) = Etagen.Vor(d);
            halte.Add(new PyObjekt { { "etage", (long)k }, { "ankunft", new List<object?> { ax, ay, blick } } });
        }
        Py.Setzen(eg, Etagen.FeldUebergaenge, new List<object?> { new PyObjekt { { "id", "u1" }, { "art", Etagen.Treppe }, { "halte", halte } } });
        return innen;
    }

    /// <summary>Frisches Spiel mit dem Moebelhaus mit Etage, im Erdgeschoss vor der Treppe. null = nicht hineingekommen.</summary>
    private static StadtSpiel? VorDerTreppe(SpielSeite seite, bool vorrat)
    {
        var alt = seite.Spiel!;
        var spiel = SpielStart.Neu(alt.Stadt, alt.Grafik, MitEtageImMoebelhaus(alt.Innenraeume!), alt.GeschaefteDaten, alt.Feste,
            Festzeit.Heute, Jahreszeiten.Heute);
        spiel.EtagenVorrat = vorrat;
        seite.SpielSetzen(spiel);
        spiel.GeheZu("moebelhaus");
        spiel.SofortAnkommen();
        if (!spiel.KannBetreten && spiel.KannWarten)
        {
            spiel.Warten();
        }
        if (!spiel.KannBetreten || !spiel.Betreten())
        {
            return null;
        }
        var eg = Objekt(Objekt(spiel.Innenraeume!["moebelhaus"])["gebaeude"]);
        var halt = Etagen.Von(eg, "moebelhaus", "u1")!.Auf(0)!;
        spiel.GeheZu(halt.X, halt.Y);
        spiel.SofortAnkommen();
        return spiel;
    }

    private static async Task<int> P13W5EtagenAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel?.Innenraeume is null)
        {
            Messwerte.Notiere("  FEHLER: Spiel Etagen: Spielseite nicht gebaut");
            return 1;
        }
        // ohne GL (macOS Metal/Software: Ortsliste statt 3D) zeichnet die Flaeche kein Bild: dann nur bis zum Bild der Oberflaeche
        var gl = await GlAbwartenAsync(seite);
        async Task Bild()
        {
            if (gl)
            {
                await seite.Flaeche.NaechstesBildAsync();
            }
            await ansicht.BildFertigAsync();
        }
        Messwerte.Notiere("Spiel: Etagen " + (gl ? "mit 3D" : "ohne 3D (Ortsliste)"));
        var vorher = seite.Spiel;
        try
        {
            // 1. erstes Mal ohne Vorrat (die Etage wird beim Wechsel gebaut): einmalig, streng
            var spiel = VorDerTreppe(seite, vorrat: false);
            if (spiel is null)
            {
                Pruefe("Etagen: Moebelhaus betreten", false, "nicht hineingekommen");
                return funde;
            }
            await Bild();
            var knopf = spiel.Knoepfe.FirstOrDefault(k => k.Aktion.StartsWith(Etagen.AktionEtage, StringComparison.Ordinal));
            Pruefe("Etagen: Knopf vor der Treppe", knopf.Name == "Treppe hoch zum 1. Obergeschoss", knopf.Name ?? "kein Knopf");
            var zahl = spiel.Ablauf.Stand.Verlauf.Count;
            var uhr = Stopwatch.StartNew();
            spiel.EtageWechseln("u1", 1);
            await Bild();
            funde += Ergebnis("Spiel: Etage wechseln (erstes Mal, ohne Vorrat, bis zum Bild)",
                Messwiederholung.Einmal(uhr.Elapsed.TotalMilliseconds, Geraetegrenze));
            Pruefe("Etagen: oben angekommen", spiel.Etage == 1 && spiel.FlaecheId == "geschaeft:moebelhaus@1" && spiel.EtageVorbereitetGenommen == 0,
                string.Create(inv, $"Etage {spiel.Etage}, {spiel.FlaecheId}, vorbereitet genommen {spiel.EtageVorbereitetGenommen}"));
            Pruefe("Etagen: Figur an der Ankunft", (spiel.FigurX, spiel.FigurZ, spiel.FigurRichtung) == (11.6f, 13.0f, 90f),
                string.Create(inv, $"{spiel.FigurX:F2} {spiel.FigurZ:F2} Blick {spiel.FigurRichtung:F0}"));
            var e = spiel.Ablauf.Stand.Verlauf[^1];
            Pruefe("Etagen: Zeile aufenthalt", spiel.Ablauf.Stand.Verlauf.Count == zahl + 1 && e.Art == Etagen.Aufenthalt && Py.Int(e.Daten["etage"]) == 1,
                e.Art + " " + PythonJson.Standard(e.Daten));
            spiel.Verlassen();
            Pruefe("Etagen: oben kein Weg hinaus", spiel.ImInnenraum && spiel.Zeile == Etagen.NurUnten, spiel.Zeile.Replace('\n', ' '));

            // 2. mit vorbereiteter Nachbar-Etage: wiederholbar (hin und zurueck)
            spiel = VorDerTreppe(seite, vorrat: true)!;
            var bereit = Stopwatch.StartNew();
            while (!spiel.Vorrat.Any(i => i.Ansicht == "geschaeft:moebelhaus@1") && bereit.Elapsed < TimeSpan.FromSeconds(20))
            {
                await Task.Delay(50);
            }
            await Bild();
            async Task<double?> Wechseln()
            {
                var ziel = spiel.Etage == 0 ? 1 : 0;
                var u = Stopwatch.StartNew();
                spiel.EtageWechseln("u1", ziel);
                await Bild();
                return u.Elapsed.TotalMilliseconds;
            }
            async Task Warten()
            {
                // die Gegen-Etage ist nach dem Wechsel wieder im Hintergrund vorbereitet
                var w = Stopwatch.StartNew();
                var ziel = "geschaeft:moebelhaus" + (spiel.Etage == 0 ? "@1" : "");
                while (!spiel.Vorrat.Any(i => i.Ansicht == ziel) && w.Elapsed < TimeSpan.FromSeconds(20))
                {
                    await Task.Delay(50);
                }
            }
            var genommen = spiel.EtageVorbereitetGenommen;
            funde += Ergebnis("Spiel: Etage wechseln (bis zum Bild)",
                await Messwiederholung.MessenAsync(await Wechseln(), Geraetegrenze, Wechseln, Warten, Task.Delay));
            Pruefe("Etagen: vorbereitete Etage genommen", spiel.EtageVorbereitetGenommen > genommen,
                string.Create(inv, $"genommen {spiel.EtageVorbereitetGenommen - genommen}"));
        }
        finally
        {
            seite.SpielSetzen(vorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }

    /// <summary>Layout: Knopf vor der Treppe im Erdgeschoss und das 1. Obergeschoss mit dem Knopf zurueck.</summary>
    private static async IAsyncEnumerable<string> P13W5Ansichten(MainView ansicht)
    {
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel?.Innenraeume is null)
        {
            P7W1AnsichtFehler.Add("spiel/etage: Spielseite nicht gebaut");
            yield break;
        }
        var vorher = seite.Spiel;
        var spiel = VorDerTreppe(seite, vorrat: true);
        if (spiel is null)
        {
            P7W1AnsichtFehler.Add("spiel/etage: Moebelhaus nicht betretbar");
            seite.SpielSetzen(vorher);
            yield break;
        }
        await ansicht.BildFertigAsync();
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/etage-treppe"));
        yield return "spiel/etage-treppe";
        if (!spiel.EtageWechseln("u1", 1))
        {
            P7W1AnsichtFehler.Add("spiel/etage-oben: Wechsel ging nicht (" + spiel.Zeile.Replace('\n', ' ') + ")");
        }
        await ansicht.BildFertigAsync();
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/etage-oben"));
        yield return "spiel/etage-oben";
        seite.SpielSetzen(vorher);
        await ansicht.BildFertigAsync();
    }
}
