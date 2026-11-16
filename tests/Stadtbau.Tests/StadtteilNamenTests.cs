using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.55.0 (Weisung P12-282 Punkt 2): Stadtteil-Namen im Werkzeug. Nicos Ziel: Bereiche ziehen und benennen, jede
/// Aenderung ein Schritt (Rueckgaengig), ein ungueltiger Stand wird mit Grund abgelehnt; die Namen kommen in die Stadt-Datei
/// (Format 3) und die Projektdatei (Format 5); ohne Namen bleibt alles bitgleich wie vorher. Echte Dateien der neuen Nummern
/// (<c>projekt_S0.55.0_namen.fisbau</c>, <c>stadt_S0.55.0_namen.fistadt</c>) gleichen dem, was das Werkzeug heute schreibt
/// (neu schreiben mit <c>STADTBAU_TESTDATEI_SCHREIBEN=1</c>).
/// </summary>
public class StadtteilNamenTests
{
    private const string NeuProjekt = "projekt_S0.55.0_namen.fisbau";
    private const string NeuStadt = "stadt_S0.55.0_namen.fistadt";
    private static readonly DateTimeOffset Zeit = new(2026, 11, 16, 15, 0, 0, TimeSpan.FromHours(1));

    private static Projekt Leer(Werkzeugordner? ablage = null) => Projekt.Neu("Namen", Daten.Spiel, ablage ?? Daten.NeueAblage(), leer: (32, 24));

    private static byte[] Testdatei(string name) => Daten.Lesen(TestdateiTests.Ordner + name) ?? throw new FileNotFoundException(name);

    [Fact]
    public void Anlegen_Umbenennen_Verschieben_Loeschen_JeEinSchritt_Rueckgaengig()
    {
        var p = Leer();
        Assert.Empty(p.StadtteilEintraege());
        Assert.Null(p.StadtteilNeu("Altstadt", 0, 0, 16, 12));
        Assert.Null(p.StadtteilNeu("Hafen", 16, 0, 32, 24));
        Assert.Equal(["Altstadt", "Hafen"], p.StadtteilEintraege().Select(b => b!.Name));
        Assert.Null(p.StadtteilUmbenennen(0, "Mitte"));
        Assert.Null(p.StadtteilBereich(0, 1, 1, 15, 11, verschoben: false));
        Assert.Equal(new Stadtteile.Bereich("Mitte", 1, 1, 15, 11), p.StadtteilEintraege()[0]);
        Assert.Null(p.StadtteilLoeschen(1));
        Assert.Single(p.StadtteilEintraege());
        // Rueckgaengig Schritt fuer Schritt bis zum Anfang: der Abschnitt ist dann ganz weg
        var schritte = 0;
        while (p.Rueckgaengig())
        {
            schritte++;
        }
        Assert.Equal(5, schritte);
        Assert.False(Py.Hat(Aussen.Stadtdaten(p.AussenStand), Stadtteile.AbschnittNamen));
        Assert.Empty(p.GeaenderteTeile);
    }

    [Fact]
    public void Abgelehnt_MitGrund_OhneSchritt()
    {
        var p = Leer();
        Assert.Null(p.StadtteilNeu("Altstadt", 0, 0, 16, 12));
        var vorher = Aussen.TeileText(p.AussenStand);
        var faelle = new (Func<string?> Tun, string Teil)[]
        {
            (() => p.StadtteilNeu("altstadt", 16, 0, 32, 12), "tragen Stadtteil"),
            (() => p.StadtteilNeu("Neu", 10, 0, 20, 12), "überdecken sich"),
            (() => p.StadtteilNeu("Neu", 20, 0, 22, 12), "kleiner als 4"),
            (() => p.StadtteilNeu("Neu", 20, 0, 40, 12), "über den Rand"),
            (() => p.StadtteilNeu("", 20, 0, 30, 12), "keinen Namen"),
            (() => p.StadtteilNeu("Ein sehr sehr langer Name x", 20, 0, 30, 12), "länger als 24"),
            (() => p.StadtteilUmbenennen(0, " Altstadt"), "Leerzeichen"),
            (() => p.StadtteilBereich(0, -1, 0, 15, 12, verschoben: true), "über den Rand"),
            (() => p.StadtteilUmbenennen(3, "X"), Texte.StadtteilFehlt),
        };
        foreach (var (tun, teil) in faelle)
        {
            var grund = tun();
            Assert.NotNull(grund);
            Assert.Contains(teil, grund, StringComparison.Ordinal);
        }
        Assert.Equal(vorher, Aussen.TeileText(p.AussenStand));
        Assert.Equal(1, p.Verlauf.Count(s => s.Was == Texte.StadtteilNeuSchritt));
    }

    [Fact]
    public void Rechteck_AufRasterUndKarte()
    {
        Assert.Equal((1.5, 0.0, 10.0, 24.0), StadtteilNamen.Rechteck(10.1, 30, 1.6, -2, 32, 24));
        Assert.Equal((0.0, 0.0, 32.0, 24.0), StadtteilNamen.Rechteck(-5, -5, 50, 50, 32, 24));
    }

    [Fact]
    public void Vorschlag_ErsterFreierName()
    {
        var p = Leer();
        Assert.Equal("Stadtteil 1", StadtteilNamen.Vorschlag(p.AussenStand));
        Assert.Null(p.StadtteilNeu("stadtteil 1", 0, 0, 8, 8));
        Assert.Equal("Stadtteil 2", StadtteilNamen.Vorschlag(p.AussenStand));
    }

    [Fact]
    public void Pruefung_KaputterEintrag_RotMitTipp()
    {
        var p = Leer();
        Assert.Null(p.StadtteilNeu("Altstadt", 0, 0, 16, 12));
        p.AussenAendern("Test", a =>
        {
            ((List<object?>)Aussen.Stadtdaten(a)[Stadtteile.AbschnittNamen]!).Add(new PyObjekt { { "name", "Kaputt" } });
            return true;
        });
        Assert.Equal([true, false], p.StadtteilEintraege().Select(b => b is not null));
        var funde = Pruefen(p);
        var fund = Assert.Single(funde, f => f.Text.Contains("Stadtteil Nr. 2 ist falsch gespeichert", StringComparison.Ordinal));
        Assert.True(fund.Rot);
        Assert.Equal(StadtbauPruefung.StadtteilTipp, fund.Tipp);
        // Loeschen des kaputten Eintrags geht, danach gruen
        Assert.Null(p.StadtteilLoeschen(1));
        Assert.DoesNotContain(Pruefen(p), f => f.Text.Contains("Stadtteil", StringComparison.Ordinal));
    }

    private static List<StadtbauFund> Pruefen(Projekt p) =>
        StadtbauPruefung.Aussen(Aussen.Fassung(p.AussenStand, Daten.Spiel.Fassung, new PyObjekt(), 1, null, "t", "", ""), Daten.Spiel.Inhalt, Daten.Spiel.Grafik);

    [Fact]
    public void OhneNamen_Format3Und1_MitNamen_Format5Und3()
    {
        var ohne = Leer();
        ohne.Speichern(vonHand: true);
        Assert.Contains("\"formatversion\": 3,", File.ReadAllText(ohne.Datei, Encoding.UTF8), StringComparison.Ordinal);
        // ein Name, der wieder geloescht ist, laesst in der Stadt-Datei nichts zurueck (Format 1); die Projektdatei behaelt
        // ihn im Verlauf und bekommt darum 5 (wie der Arbeitsort)
        Assert.Null(ohne.StadtteilNeu("Weg", 0, 0, 8, 8));
        Assert.Null(ohne.StadtteilLoeschen(0));
        ohne.AussenAendern("Baum", a => AussenBearbeitung.Setzen(a, AussenArt.Baum, 20, 20));
        Assert.Contains("\"formatversion\": 1,", Encoding.UTF8.GetString(ohne.Exportieren(Zeit)), StringComparison.Ordinal);
        Assert.Contains("\"formatversion\": 5,", File.ReadAllText(ohne.Datei, Encoding.UTF8), StringComparison.Ordinal);
        var mit = NeuesProjekt(Daten.NeueAblage());
        Assert.Contains("\"formatversion\": 5,", File.ReadAllText(mit.Datei, Encoding.UTF8), StringComparison.Ordinal);
        var stadt = mit.Exportieren(Zeit);
        Assert.Contains("\"formatversion\": 3,", Encoding.UTF8.GetString(stadt), StringComparison.Ordinal);
        var f = StadtFassung.Lesen(stadt);
        Assert.Equal(["Altstadt", "Am Hafen"], Stadtteile.Bereiche(f.Stadtdaten).Select(b => b.Name));
        Assert.Equal(stadt, f.Schreiben());
    }

    private static Projekt NeuesProjekt(Werkzeugordner ablage)
    {
        var p = Leer(ablage);
        Assert.Null(p.StadtteilNeu("Altstadt", 0, 0, 16, 12));
        Assert.Null(p.StadtteilNeu("Am Hafen", 16, 0, 32, 24));
        p.Speichern(vonHand: true);
        return p;
    }

    [Fact]
    public void NeueDateien_S0550_GleichDemWerkzeug_UndLesbar()
    {
        var p = NeuesProjekt(Daten.NeueAblage());
        var projekt = File.ReadAllBytes(p.Datei);
        var stadt = p.Exportieren(Zeit);
        if (Environment.GetEnvironmentVariable("STADTBAU_TESTDATEI_SCHREIBEN") == "1")
        {
            File.WriteAllBytes(Path.Combine(Daten.Wurzel, TestdateiTests.Ordner + NeuProjekt), projekt);
            File.WriteAllBytes(Path.Combine(Daten.Wurzel, TestdateiTests.Ordner + NeuStadt), stadt);
        }
        Assert.Equal(OhneWerkzeug(Testdatei(NeuProjekt)), OhneWerkzeug(projekt));
        Assert.Equal(OhneWerkzeug(Testdatei(NeuStadt)), OhneWerkzeug(stadt));
        // die echte Datei oeffnet sich, die Namen sind da, die Stadt-Datei schreibt sich bitgleich zurueck
        var ablage = Daten.NeueAblage();
        Directory.CreateDirectory(ablage.Projekte);
        var datei = Path.Combine(ablage.Projekte, "Namen S0550" + Projekt.Endung);
        File.WriteAllBytes(datei, Testdatei(NeuProjekt));
        var q = Projekt.Oeffnen(datei, Daten.Spiel, ablage);
        Assert.False(q.NurLesen);
        Assert.Equal(["Altstadt", "Am Hafen"], q.StadtteilEintraege().Select(b => b!.Name));
        var f = StadtFassung.Lesen(Testdatei(NeuStadt));
        Assert.Equal(Testdatei(NeuStadt), f.Schreiben());
    }

    private static string OhneWerkzeug(byte[] daten) =>
        string.Join("\n", Encoding.UTF8.GetString(daten).Split('\n').Where(z => !z.StartsWith(" \"werkzeug\": ", StringComparison.Ordinal) && !z.StartsWith(" \"pruefsumme\": ", StringComparison.Ordinal)));

    [Fact]
    public void FormatFuer_NurMitTeilDerNamen()
    {
        Assert.Equal(3, Projekt.FormatFuerTests("{}", "[]"));
        Assert.Equal(5, Projekt.FormatFuerTests("{\"stadtdaten/stadtteile\": [[]]}", "[]"));
        Assert.Equal(5, Projekt.FormatFuerTests("{}", "[{\"vorher\": {\"stadtdaten/stadtteile\": []}}]"));
        // ein Text mit demselben Wortlaut ist maskiert und zaehlt nicht
        Assert.Equal(3, Projekt.FormatFuerTests("{\"karte/name\": [\"\\\"stadtdaten/stadtteile\\\": \"]}", "[]"));
    }
}
