using Lernplattform.Core.Daten;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.25.1 (Weisung P12-108 Punkt 2, Sammelliste 251): eine Stadt-Datei (.fistadt) als NEUES Projekt oeffnen. Die Datei
/// des neuen Projekts ergibt wieder genau dieselbe Stadt-Datei (leere Stadt und Stadt 1 mit geaendertem Innenraum),
/// nichts wird ueberschrieben. Gegenprobe: ohne die Innenraeume fehlt die Aenderung.
/// </summary>
public class StadtDateiOeffnenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static byte[] Datei(Projekt p) => p.Stand("2026-11-09T07:00:00+01:00").Schreiben();

    [Fact]
    public void LeereStadt_GleicheDateiWieder_UndFrei()
    {
        var p = Projekt.Neu("Teststadt", D, Daten.NeueAblage(), leer: (40, 32));
        p.AussenAendern("Straße", a => AussenBearbeitung.Setzen(a, AussenArt.Strasse, 10, 8));
        p.AussenAendern("Baum", a => AussenBearbeitung.Setzen(a, AussenArt.Baum, 10, 6));
        var vorher = Datei(p);

        var ablage = Daten.NeueAblage();
        var q = Projekt.AusStadtDatei(vorher, D, ablage, "Teststadt");

        Assert.Equal((40.0, 32.0), q.Leer);
        Assert.True(q.Frei);
        Assert.Equal(vorher, Datei(q));
        // gespeichert und wieder lesbar
        var wieder = Projekt.Oeffnen(q.Datei, D, ablage);
        Assert.Equal(vorher, Datei(wieder));
    }

    [Fact]
    public void Stadt1_InnenraumUndDraussen_GleicheDateiWieder()
    {
        var p = Projekt.Neu("Moebel neu", D, Daten.NeueAblage(), "moebelhaus");
        p.Aendern("moebelhaus", "Boden", g => Bearbeitung.Boden(g, "flur", "teppich"));
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        p.Nachricht = "Probe";
        var vorher = Datei(p);

        var ablage = Daten.NeueAblage();
        // W4b (Weisung P12-228): eine Stadt-Datei oeffnet mit 1 Minute je Schritt (W4bTests); hier geht es um die
        // unveraenderte Rueckgabe, darum mit der Uhr wie gesendet
        Projekt q;
        Aussen.UhrAltFuerTests = true;
        try
        {
            q = Projekt.AusStadtDatei(vorher, D, ablage, "Moebel neu");
        }
        finally
        {
            Aussen.UhrAltFuerTests = false;
        }

        Assert.Null(q.Leer);
        Assert.Equal(["moebelhaus"], q.GeaenderteOrte);
        Assert.True(q.AussenGeaendert);
        Assert.Equal("Probe", q.Nachricht);
        Assert.Equal(vorher, Datei(q));
        Assert.Equal(vorher, Datei(Projekt.Oeffnen(q.Datei, D, ablage)));
    }

    [Fact]
    public void Gegenprobe_OhneInnenraeume_FehltDieAenderung()
    {
        var p = Projekt.Neu("Moebel neu", D, Daten.NeueAblage(), "moebelhaus");
        p.Aendern("moebelhaus", "Boden", g => Bearbeitung.Boden(g, "flur", "teppich"));
        var vorher = Datei(p);
        Projekt.StadtDateiOhneInnenFuerTests.Value = true;
        try
        {
            var ohne = Projekt.AusStadtDatei(vorher, D, Daten.NeueAblage(), "Moebel neu");
            Assert.Empty(ohne.GeaenderteOrte);
            Assert.NotEqual(vorher, Datei(ohne));
        }
        finally
        {
            Projekt.StadtDateiOhneInnenFuerTests.Value = false;
        }
    }

    [Fact]
    public void NieUeberschreiben_UndFreierName()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Stadt 1", D, ablage, leer: (24, 16));
        p.AussenAendern("Straße", a => AussenBearbeitung.Setzen(a, AussenArt.Strasse, 10, 8));
        var datei = Datei(p);
        var inhalt = File.ReadAllBytes(p.Datei);
        Assert.Throws<IOException>(() => Projekt.AusStadtDatei(datei, D, ablage, "Stadt 1"));
        Assert.Equal(inhalt, File.ReadAllBytes(p.Datei));

        Assert.Equal("Stadt 1 2", Projekt.NameAusStadtDatei("neu_stadt_1_Stadt_1_2026-11-08_2150.fistadt", "neu_stadt_1", ablage));
        Assert.Equal("Teststadt", Projekt.NameAusStadtDatei("stadt1_Teststadt_2026-11-08_2150_2.fistadt", "stadt1", ablage));
        Assert.Equal(Texte.StadtDateiName, Projekt.NameAusStadtDatei("2026-11-08_215000_PC_S0.23.0.fistadt", "neu_x", ablage));
        var neu = Projekt.AusStadtDatei(datei, D, ablage, Projekt.NameAusStadtDatei("x.fistadt", "neu_stadt_1", ablage));
        Assert.Equal(Texte.StadtDateiName, neu.Name);
        Assert.Equal(2, Projekt.Liste(ablage).Count);
        Assert.Equal(inhalt, File.ReadAllBytes(p.Datei));
    }

    [Fact]
    public void KaputteOderFremdeDatei_Fehler()
    {
        var ablage = Daten.NeueAblage();
        Assert.Throws<StadtbauFehler>(() => Projekt.AusStadtDatei("{}"u8.ToArray(), D, ablage, "Kaputt"));
        Assert.Throws<StadtbauFehler>(() => Projekt.AusStadtDatei("kein json"u8.ToArray(), D, ablage, "Kaputt"));
        // Stadt 1 auf einer anderen Fassung als der eingebauten
        var p = Projekt.Neu("Moebel neu", D, Daten.NeueAblage(), "moebelhaus");
        var f = p.Stand("2026-11-09T07:00:00+01:00");
        var fremd = new StadtFassung
        {
            StadtId = f.StadtId, Nummer = f.Nummer, Basis = f.Basis + 5, Werkzeug = f.Werkzeug, Erstellt = f.Erstellt, Nachricht = f.Nachricht,
            Karte = f.Karte, Stadtdaten = f.Stadtdaten, Innenraeume = f.Innenraeume, Laternen = f.Laternen, Beete = f.Beete,
        };
        var e = Assert.Throws<StadtbauFehler>(() => Projekt.AusStadtDatei(fremd.Schreiben(), D, ablage, "Fremd"));
        Assert.Contains("Fassung 5", e.Funde[0], StringComparison.Ordinal);
        Assert.Empty(Projekt.Liste(ablage));
    }
}
