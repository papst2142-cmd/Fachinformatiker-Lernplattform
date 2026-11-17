using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Pakete;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.57.0 (Inhaltspakete W3, Weisung P12-297 W3-1, Plan P12-175 Abschnitt 4): „Neue Stadt (Paket)“. Abnahmesatz:
/// eine Stadt laesst sich als Paket anlegen (Name und Kennung) und exportieren; Stadt 1 bleibt geschuetzt; das Werkzeug zeigt
/// die geschaetzte Paketgroesse. Ohne Paket bleibt jede Datei bitgleich (Projekt 3, Stadt 1); mit Paket Projekt 6, Stadt 4.
/// </summary>
public class PaketStadtTests
{
    private static readonly DateTimeOffset Zeit = new(2026, 11, 17, 18, 0, 0, TimeSpan.FromHours(1));

    private static long Nummer(string text)
    {
        var i = text.IndexOf("\"formatversion\": ", StringComparison.Ordinal) + "\"formatversion\": ".Length;
        return long.Parse(text[i..text.IndexOf(',', i)], System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public void NeueStadtAlsPaket_Anlegen_Speichern_Oeffnen_Exportieren()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Lindenau", Daten.Spiel, ablage, leer: (32, 24), paket: ("lindenau", "Lindenau am See"));
        Assert.Equal(("lindenau", "Lindenau am See"), p.Paket);
        // kein Schritt: Rueckgaengig nimmt das Paket nicht weg
        Assert.Equal(0, p.SchritteZurueck);
        Assert.Equal(6, Nummer(File.ReadAllText(p.Datei, Encoding.UTF8)));
        var wieder = Projekt.Oeffnen(p.Datei, Daten.Spiel, ablage);
        Assert.False(wieder.NurLesen);
        Assert.Equal(p.Paket, wieder.Paket);
        // Export: eine Stadt-Datei Format 4 mit dem Abschnitt; sie liest sich und schreibt sich bitgleich zurueck
        var stadt = wieder.Exportieren(Zeit);
        Assert.Equal(4, Nummer(Encoding.UTF8.GetString(stadt)));
        var f = StadtFassung.Lesen(stadt);
        Assert.Equal(("lindenau", "Lindenau am See"), StadtPaket.Von(f.Stadtdaten));
        Assert.StartsWith("neu_", f.StadtId, StringComparison.Ordinal);
        Assert.Equal(stadt, f.Schreiben());
        // als neues Projekt geoeffnet, bleibt es eine Paket-Stadt
        Assert.Equal(p.Paket, Projekt.AusStadtDatei(stadt, Daten.Spiel, Daten.NeueAblage(), "Lindenau Kopie").Paket);
        // Gegenprobe ohne Paket: Projekt 3, Stadt 1 (bitgleich wie bisher), kein Abschnitt
        var ohne = Projekt.Neu("Ohne Paket", Daten.Spiel, Daten.NeueAblage(), leer: (32, 24));
        Assert.Null(ohne.Paket);
        Assert.Equal(3, Nummer(File.ReadAllText(ohne.Datei, Encoding.UTF8)));
        ohne.AussenAendern("Baum", a => AussenBearbeitung.Setzen(a, AussenArt.Baum, 20, 20));
        Assert.Equal(1, Nummer(Encoding.UTF8.GetString(ohne.Exportieren(Zeit))));
    }

    [Fact]
    public void Stadt1Geschuetzt_UngueltigeKennungOderName_Abgelehnt()
    {
        var ablage = Daten.NeueAblage();
        Assert.Throws<ArgumentException>(() => Projekt.Neu("Stadt eins", Daten.Spiel, ablage, paket: ("lindenau", "Lindenau")));
        foreach (var k in new[] { "Lindenau", "1stadt", "a", "stadt1", "falschlieferung", "mit-strich", new string('a', 33) })
        {
            Assert.NotNull(StadtPaket.KennungFehler(k));
            Assert.Throws<ArgumentException>(() => Projekt.Neu("Falsch " + k.Length, Daten.Spiel, ablage, leer: (32, 24), paket: (k, "Name")));
        }
        foreach (var n in new[] { "", " Lindenau", new string('x', 41) })
        {
            Assert.NotNull(StadtPaket.NameFehler(n));
        }
        // Regeln der Kennung gleich dem Paket (App) und gleiche reservierte Kennungen
        foreach (var k in new[] { "lindenau", "a1", "probe_quests", "x_" + new string('y', 30), "Lindenau", "1x", "a", "a-b", new string('a', 33) })
        {
            Assert.Equal(Paket.KennungGueltig(k), StadtPaket.KennungFehler(k) is null || StadtPaket.Reserviert.Contains(k));
        }
        Assert.Equal(Paket.Reserviert, StadtPaket.Reserviert);
        // Stadt 1 mit Paket-Abschnitt ist ein Fund der Pruefung (geschuetzt), ebenso ein falscher Abschnitt
        Assert.Contains(StadtPaket.Fehler(new PyObjekt { { "paket", StadtPaket.Neu("lindenau", "Lindenau") } }, AktiveFassung.Stadt1), t => t.Contains("Stadt 1", StringComparison.Ordinal));
        Assert.NotEmpty(StadtPaket.Fehler(new PyObjekt { { "paket", new PyObjekt { { "kennung", "lindenau" } } } }, "neu_x"));
        Assert.Empty(StadtPaket.Fehler(new PyObjekt { { "paket", StadtPaket.Neu("lindenau", "Lindenau") } }, "neu_x"));
        Assert.Empty(StadtPaket.Fehler(new PyObjekt(), AktiveFassung.Stadt1));
    }

    [Fact]
    public void Paketgroesse_Geschaetzt_WieDasGebauteProbePaket()
    {
        var p = Projekt.Neu("Groesse", Daten.Spiel, Daten.NeueAblage(), leer: (32, 24), paket: ("groesse", "Größe"));
        var g = p.PaketGroesse();
        Assert.NotNull(g);
        Assert.InRange(g!.Value, 200, p.Stand().Schreiben().Length);
        Assert.Null(Projekt.Neu("Ohne Groesse", Daten.Spiel, Daten.NeueAblage(), leer: (32, 24)).PaketGroesse());
        // Schaetzung gegen das echte, mit paket_bauen.py gebaute Probe-Paket: hoechstens 5 % daneben
        var stadt = File.ReadAllBytes(Path.Combine(Daten.Wurzel, "pakete", "probe_quests", Paket.StadtDatei));
        var echt = new FileInfo(Path.Combine(Daten.Wurzel, "tests", "Lernplattform.Tests", "Testdaten", "pakete_echt", "probe_quests_1.fipaket")).Length;
        var schaetzung = Projekt.PaketGroesseVon(stadt, "probe_quests", "Probe: Stadt mit Aufträgen");
        Assert.True(Math.Abs(schaetzung - echt) <= echt * 0.05, $"geschätzt {schaetzung}, gebaut {echt}");
    }

    [Fact]
    public void PaketStadt_VergroessernUndStadtteilEinsetzen_BehaltenDenAbschnitt()
    {
        var p = Projekt.Neu("Gross Paket", Daten.Spiel, Daten.NeueAblage(), leer: (64, 64), paket: ("gross_paket", "Groß"));
        Assert.Empty(KarteVerschieben.Unbekannt(p.AussenStand));
        Assert.Null(p.KarteVergroessern(128, 64, 1));
        Assert.Equal(("gross_paket", "Groß"), p.Paket);
        Assert.True(p.Rueckgaengig());
        Assert.Equal(("gross_paket", "Groß"), p.Paket);
    }
}

/// <summary>
/// Welle S 0.57.0 (Weisung P12-297 W3-2): jedes Paket unter <c>pakete/</c> prueft das Werkzeug wie Stadt 1: als Projekt
/// geoeffnet zeigen beide Fundlisten 0 rot, die Grenzen (Gesamt- und Nahansicht, bei grossen Karten je Stadtteil) liegen
/// unter der Grenze, und jeder Ort ist zu Fuss erreichbar. Gegenprobe: eine Laterne auf der Fahrbahn ist rot.
/// </summary>
public class PaketeImRepoTests
{
    private static IEnumerable<string> Kennungen =>
        Directory.GetDirectories(Path.Combine(Daten.Wurzel, "pakete")).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal);

    [Fact]
    public void JedesPaket_BeideFundlistenNullRot_GrenzenUnterDerGrenze_GegenprobeRot()
    {
        Assert.NotEmpty(Kennungen);
        foreach (var kennung in Kennungen)
        {
            var datei = File.ReadAllBytes(Path.Combine(Daten.Wurzel, "pakete", kennung, Paket.StadtDatei));
            var p = Projekt.AusStadtDatei(datei, Daten.Spiel, Daten.NeueAblage(), "Paket " + kennung);
            Assert.Equal(kennung, p.Paket?.Kennung);
            var innen = InnenFunde.Rechnen(InnenFunde.Vorbereiten(p));
            var innenRot = innen.SelectMany(o => o.Funde.Where(f => f.Rot).Select(f => o.Schluessel + ": " + f.Text)).ToList();
            Assert.True(innenRot.Count == 0, kennung + " Innenräume rot: " + string.Join(" | ", innenRot));
            var aussenRot = p.PruefenAussen().Where(f => f.Rot).Select(f => f.Text).ToList();
            Assert.True(aussenRot.Count == 0, kennung + " Stadt rot: " + string.Join(" | ", aussenRot));
            var g = GrenzenLive.Fuer(StadtFassung.Lesen(datei), Daten.Spiel);
            Assert.False(g.Ueber, $"{kennung}: Grenzen {g.Gesamt} / {g.Teuerstes}");
            Assert.True(g.Kacheln?.All(k => !k.Ueber) ?? true, kennung + ": ein Stadtteil über der Grenze");
            Assert.False(g.GeladenUeber, kennung + ": Netzdaten über der Grenze");
            TestContext.Current.SendDiagnosticMessage($"{kennung}: teuerstes Bild {g.Teuerstes.Dreiecke} Dreiecke / {g.Teuerstes.Aufrufe} Aufrufe");
            var wege = Laufzeiten.Rechnen(p.Stand(), Daten.Spiel.Inhalt, Daten.Spiel.Grafik);
            Assert.All(wege, w => Assert.NotNull(w.Minuten));

            p.AussenAendern("Laterne", a =>
            {
                var l = (List<object?>)Aussen.Laternen(a)[0]!;
                l[0] = 14.5;
                l[1] = 30.0;
                return true;
            });
            Assert.Contains(p.PruefenAussen(), f => f.Rot);
        }
    }
}
