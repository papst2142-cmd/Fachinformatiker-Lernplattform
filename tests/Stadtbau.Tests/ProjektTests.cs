using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Phase 13 Welle 2 (Weisung P12-17, Plan Abschnitte 3 und 5): Projekt des Werkzeugs. Neu, Oeffnen, Speichern,
/// automatisches Speichern, 20 Sicherungen, Rueckgaengig (unbegrenzt offen, 200 Schritte gespeichert), nur lesen bei
/// neuerer Formatnummer, Export (.fistadt) gesperrt bei Rot, Datei speichern ohne Ueberschreiben, An CC senden.
/// </summary>
public class ProjektTests
{
    private static Spieldaten D => Daten.Spiel;

    private static Stelle ErstesRegal(Projekt p, string ort) =>
        Bearbeitung.Alle(p.Grundriss(ort)).First(s => Py.Str(s.Deko["typ"]) == "regal" && s.Stelle.Bereich != "flur").Stelle;

    [Theory]
    [InlineData("Möbelhaus neu", true)]
    [InlineData("Test_1-a", true)]
    [InlineData("", false)]
    [InlineData(" vorne", false)]
    [InlineData("a/b", false)]
    [InlineData("a.b", false)]
    [InlineData("12345678901234567890123456789012345678901", false)]
    public void NameGueltig(string name, bool gueltig) => Assert.Equal(gueltig, Projekt.NameGueltig(name));

    [Fact]
    public void Neu_LegtDateiUndSicherungAn_DoppelterNameGehtNicht()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Erstes", D, ablage, "elektromarkt");
        Assert.True(File.Exists(p.Datei));
        Assert.Equal("elektromarkt", p.Ort);
        Assert.Single(new Sicherungen(ablage, "Erstes").Liste());
        Assert.Throws<IOException>(() => Projekt.Neu("Erstes", D, ablage));
        Assert.Throws<ArgumentException>(() => Projekt.Neu("a/b", D, ablage));
        Assert.Equal([p.Datei], Projekt.Liste(ablage));
        // Projektdatei: je Feld eine Zeile, feste Reihenfolge
        var zeilen = File.ReadAllText(p.Datei, Encoding.UTF8).Split('\n');
        Assert.Equal("{", zeilen[0]);
        Assert.Equal(Projekt.Felder, zeilen.Skip(1).Take(Projekt.Felder.Length).Select(z => z.Split('"')[1]));
    }

    [Fact]
    public void Aendern_Rueckgaengig_Wiederholen_UndAutomatischGespeichert()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Undo", D, ablage, "elektromarkt");
        var original = PythonJson.Standard(p.Grundriss("elektromarkt"));
        var regal = ErstesRegal(p, "elektromarkt");
        p.Aendern("elektromarkt", "Drehen", g => Bearbeitung.Drehen(g, regal));
        var gedreht = PythonJson.Standard(p.Grundriss("elektromarkt"));
        Assert.NotEqual(original, gedreht);
        Assert.Equal(["elektromarkt"], p.GeaenderteOrte);
        Assert.Equal(1, p.SchritteZurueck);
        // automatisch gespeichert: wieder oeffnen zeigt die Aenderung
        Assert.Equal(gedreht, PythonJson.Standard(Projekt.Oeffnen(p.Datei, D, ablage).Grundriss("elektromarkt")));
        Assert.True(p.Rueckgaengig());
        Assert.Equal(original, PythonJson.Standard(p.Grundriss("elektromarkt")));
        Assert.Empty(p.GeaenderteOrte);
        Assert.False(p.Rueckgaengig());
        Assert.True(p.Wiederholen());
        Assert.Equal(gedreht, PythonJson.Standard(p.Grundriss("elektromarkt")));
        Assert.False(p.Wiederholen());
        // keine Aenderung, kein Schritt
        p.Aendern("elektromarkt", "nichts", _ => { });
        Assert.Equal(1, p.SchritteZurueck);
    }

    [Fact]
    public void Rueckgaengig_UnbegrenztOffen_200SchritteGespeichert()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Lang", D, ablage, "moebelhaus");
        var regal = ErstesRegal(p, "moebelhaus");
        var staende = new List<string> { PythonJson.Standard(p.Grundriss("moebelhaus")) };
        for (var i = 0; i < 230; i++)
        {
            p.Aendern("moebelhaus", "Drehen", g => Bearbeitung.Drehen(g, regal));
            staende.Add(PythonJson.Standard(p.Grundriss("moebelhaus")));
        }
        Assert.Equal(230, p.SchritteZurueck);
        var wieder = Projekt.Oeffnen(p.Datei, D, ablage);
        Assert.Equal(Projekt.GespeicherteSchritte, wieder.SchritteZurueck);
        Assert.Equal(staende[^1], PythonJson.Standard(wieder.Grundriss("moebelhaus")));
        for (var i = 0; i < Projekt.GespeicherteSchritte; i++)
        {
            Assert.True(wieder.Rueckgaengig());
        }
        Assert.False(wieder.Rueckgaengig());
        Assert.Equal(staende[230 - Projekt.GespeicherteSchritte], PythonJson.Standard(wieder.Grundriss("moebelhaus")));
        // im offenen Projekt geht es bis ganz zum Anfang
        for (var i = 0; i < 230; i++)
        {
            Assert.True(p.Rueckgaengig());
        }
        Assert.Equal(staende[0], PythonJson.Standard(p.Grundriss("moebelhaus")));
    }

    [Fact]
    public void Sicherungen_Hoechstens20_AeltesteWirdUeberschrieben()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Sicher", D, ablage);
        var zeit = DateTime.Now.AddDays(1);
        p.Uhr = () => zeit;
        for (var i = 0; i < 25; i++)
        {
            zeit = zeit.AddMinutes(1);
            p.Speichern(vonHand: true);
        }
        var liste = new Sicherungen(ablage, "Sicher").Liste();
        Assert.Equal(Sicherungen.Hoechstens, liste.Count);
        Assert.Equal(Enumerable.Range(1, 20).Select(Sicherungen.Platz).Order(StringComparer.Ordinal), liste.Select(Path.GetFileName).Order(StringComparer.Ordinal));
        // automatisch: eine Sicherung hoechstens alle 5 Minuten
        var regal = ErstesRegal(p, "moebelhaus");
        var vorher = File.GetLastWriteTimeUtc(liste[0]);
        zeit = zeit.AddMinutes(1);
        p.Aendern("moebelhaus", "Drehen", g => Bearbeitung.Drehen(g, regal));
        Assert.Equal(vorher, File.GetLastWriteTimeUtc(new Sicherungen(ablage, "Sicher").Liste()[0]));
        zeit = zeit.AddMinutes(5);
        p.Aendern("moebelhaus", "Drehen", g => Bearbeitung.Drehen(g, regal));
        Assert.NotEqual(vorher, File.GetLastWriteTimeUtc(new Sicherungen(ablage, "Sicher").Liste()[0]));
        Assert.Equal(Sicherungen.Hoechstens, new Sicherungen(ablage, "Sicher").Liste().Count);
    }

    private static string Ersetzt(string text, string alt, string neu)
    {
        Assert.Contains(alt, text, StringComparison.Ordinal);
        return text.Replace(alt, neu, StringComparison.Ordinal);
    }

    [Fact]
    public void NeuereFormatnummer_NurLesen_AndereBasis_Fehler()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Neuer", D, ablage, "bank");
        var text = File.ReadAllText(p.Datei, Encoding.UTF8);
        File.WriteAllText(p.Datei, Ersetzt(text, "\"formatversion\": 3,", "\"formatversion\": " + (Projekt.FormatVersion + 1) + ","), new UTF8Encoding(false));
        var nur = Projekt.Oeffnen(p.Datei, D, ablage);
        Assert.True(nur.NurLesen);
        Assert.Throws<InvalidOperationException>(() => nur.Aendern("bank", "x", _ => { }));
        var vorher = File.ReadAllBytes(p.Datei);
        nur.Speichern(vonHand: true);
        Assert.Equal(vorher, File.ReadAllBytes(p.Datei));
        // Gegenprobe: Formatnummer 3 laesst sich bearbeiten
        File.WriteAllText(p.Datei, text, new UTF8Encoding(false));
        Assert.False(Projekt.Oeffnen(p.Datei, D, ablage).NurLesen);
        File.WriteAllText(p.Datei, text.Replace("\"basis\": 0,", "\"basis\": 7,", StringComparison.Ordinal), new UTF8Encoding(false));
        Assert.Throws<StadtbauFehler>(() => Projekt.Oeffnen(p.Datei, D, ablage));
        File.WriteAllText(p.Datei, "kein json", new UTF8Encoding(false));
        Assert.Throws<StadtbauFehler>(() => Projekt.Oeffnen(p.Datei, D, ablage));
    }

    [Fact]
    public void Export_OhneAenderung_Gesperrt()
    {
        var p = Projekt.Neu("Leer", D, Daten.NeueAblage());
        var f = Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now));
        Assert.Contains("noch keine Änderung", f.Funde[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Export_ElektromarktMitFund216_Gesperrt()
    {
        var p = Projekt.Neu("Elektro", D, Daten.NeueAblage(), "elektromarkt");
        p.Aendern("elektromarkt", "Boden", g => Bearbeitung.Boden(g, "flur", "teppich"));
        var f = Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now));
        Assert.Equal(8, f.Funde.Count);
        Assert.All(f.Funde, z => Assert.StartsWith("Elektromarkt: ", z, StringComparison.Ordinal));
        Assert.Throws<StadtbauFehler>(() => p.ExportSpeichern(DateTimeOffset.Now));
        Assert.Empty(Directory.GetFiles(p.Werkzeugordner.Exporte));
    }

    [Fact]
    public void Export_Moebelhaus_GueltigeFassung_SpielBautDenGrundriss()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Moebel neu", D, ablage, "moebelhaus");
        var regal = ErstesRegal(p, "moebelhaus");
        p.Aendern("moebelhaus", "Boden", g => Bearbeitung.Boden(g, "flur", "teppich"));
        p.Nachricht = "Probe";
        var zeit = new DateTimeOffset(2026, 11, 6, 9, 30, 0, TimeSpan.FromHours(1));
        var datei = p.Exportieren(zeit);
        Assert.Empty(StadtbauPruefung.Format(Py.Lesen(datei)));
        var f = StadtFassung.Lesen(datei);
        Assert.Empty(StadtbauPruefung.Inhalt(f, D.Inhalt));
        Assert.Equal(1, f.Nummer);
        Assert.Equal(0, f.Basis);
        Assert.Equal("Probe", f.Nachricht);
        Assert.Equal(Werkzeug.Kennzeile, f.Werkzeug);
        Assert.Equal("2026-11-06T09:30:00+01:00", f.Erstellt);
        // Karte, Stadtdaten, Laternen und Beete bleiben wie in Fassung 0; nur der Moebelhaus-Innenraum ist neu
        Assert.Equal(PythonJson.Text(D.Fassung.Karte), PythonJson.Text(f.Karte));
        Assert.Equal(PythonJson.Text(D.Fassung.Stadtdaten), PythonJson.Text(f.Stadtdaten));
        Assert.Equal(PythonJson.Text(p.Grundriss("moebelhaus")), PythonJson.Text(Innenraum.LadenGebaeude(f.Innenraeume, "moebelhaus")));
        foreach (var ort in D.Orte.Where(o => o != "moebelhaus"))
        {
            Assert.Equal(PythonJson.Text(InnenGrundriss.Grundriss(D.Fassung.Innenraeume, ort)), PythonJson.Text(Innenraum.LadenGebaeude(f.Innenraeume, ort)));
        }
        // nie ueberschreiben
        var a = p.ExportSpeichern(zeit);
        var b = p.ExportSpeichern(zeit);
        Assert.Equal("stadt1_Moebel_neu_2026-11-06_0930.fistadt", Path.GetFileName(a));
        Assert.Equal("stadt1_Moebel_neu_2026-11-06_0930_2.fistadt", Path.GetFileName(b));
        _ = regal;
    }

    [Fact]
    public void Senden_Name_UndWegVonBerichtSenden()
    {
        var zeit = new DateTimeOffset(2026, 11, 6, 9, 30, 5, TimeSpan.FromHours(1));
        Assert.Equal("stadtbau/2026-11-06_093005_PC_S0.53.0.fistadt", Projekt.SendeName(zeit, "PC", 1));
        Assert.Equal("stadtbau/2026-11-06_093005_Tablet_S0.53.0_3.fistadt", Projekt.SendeName(zeit, "Tablet", 3));
        var p = Projekt.Neu("Senden", D, Daten.NeueAblage(), "moebelhaus");
        p.Aendern("moebelhaus", "Boden", g => Bearbeitung.Boden(g, "flur", "teppich"));
        var datei = p.Exportieren(zeit);
        string? name = null;
        string? nachricht = null;
        var ergebnis = Projekt.Senden(datei, zeit, "PC", (d, n, text) =>
        {
            Assert.Equal(datei, d);
            name = n(1);
            nachricht = text;
            return new VersandErgebnis(VersandArt.Gesendet, n(1), 201);
        });
        Assert.Equal(VersandArt.Gesendet, ergebnis.Art);
        Assert.Equal("stadtbau/2026-11-06_093005_PC_S0.53.0.fistadt", name);
        Assert.Equal("Stadtbau S 0.53.0 PC", nachricht);
        // ohne eingebauten Schluessel (Tests, PR-Laeufe): nichts gesendet, kein Netz
        Assert.Equal(VersandArt.NichtVerfuegbar, Projekt.Senden(datei, zeit, "PC", null).Art);
    }

    [Fact]
    public void Werkzeug_VersionUndStufe()
    {
        Assert.Equal("0.53.0", Werkzeug.Version);
        Assert.Equal("S", Werkzeug.Stufe);
        Assert.Equal("vS0.53.0", Werkzeug.Tag);
        Assert.Equal("Fachinformatiker Stadtbau S 0.53.0", Werkzeug.Kennzeile);
        Assert.True(Lernplattform.Core.Update.ReleaseLinie.IstStadtbau(Werkzeug.Tag));
        Assert.False(Lernplattform.Core.Update.ReleaseLinie.IstLernplattform(Werkzeug.Tag));
    }
}
