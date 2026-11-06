using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Phase 13 Welle 4c (Weisung P12-32): Blueprints. Speichern, Laden, Setzen (gleiches Gebaeude wie das Original, nur
/// andere Lage), kaputte Datei, neuere Formatnummer, Umbenennen und Loeschen, Sicherungen, Pruefung an belegter Stelle,
/// je mit Gegenprobe.
/// </summary>
public class BlueprintTests
{
    private static Spieldaten D => Daten.Spiel;

    private static readonly DateTime Zeit = new(2026, 11, 6, 18, 0, 0, DateTimeKind.Local);

    private static AussenStelle Moebelhaus => new(AussenArt.Haus, -1, 0, "moebelhaus");

    private static string Json(object? o) => PythonJson.Standard(o);

    /// <summary>Blueprint des Moebelhauses aus Stadt 1, mit einer Aenderung im Innenraum (ein Regal gedreht).</summary>
    private static (Projekt P, Blueprint B) MoebelhausMitAenderung(Werkzeugordner ablage, string name = "Mein Möbelhaus")
    {
        var p = Projekt.Neu("Quelle " + Guid.NewGuid().ToString("N")[..6], D, ablage, "moebelhaus");
        var regal = Bearbeitung.Alle(p.Grundriss("moebelhaus")).First(s => Py.Str(s.Deko["typ"]) == "regal" && s.Stelle.Bereich != "flur").Stelle;
        p.Aendern("moebelhaus", "Drehen", g => Bearbeitung.Drehen(g, regal));
        return (p, Blueprint.Aus(p, Moebelhaus, name)!);
    }

    /// <summary>Leere Stadt mit einer langen waagrechten Strasse bei y = 12.</summary>
    private static Projekt LeereStadt(Werkzeugordner ablage, string name = "Leer mit Straße")
    {
        var p = Projekt.Neu(name, D, ablage, leer: (32, 24));
        p.AussenAendern("Straße", a =>
        {
            var s = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 4, 12)!;
            AussenBearbeitung.LaengeSetzen(a, s, 28);
        });
        return p;
    }

    /// <summary>
    /// Mitte fuer den Blueprint 1 vor der Strasse bei y = 12 (Fahrbahn und Gehweg bleiben frei); beim Setzen wird der
    /// Fussweg bis zur Strasse verlaengert.
    /// </summary>
    private static (double X, double Y) MitteAnStrasse(Blueprint b, double x)
    {
        var ende = Liste(b.Weg[^1]);
        var richtung = Math.Sign(Py.F(ende[1]) - Py.F(b.Tuer![1]));
        var m = b.Mitte();
        return (x, 12 - Py.F(ende[1]) + m.Y - richtung);
    }

    [Fact]
    public void Aus_Moebelhaus_EnthaeltModellTuerWegSchildUndInnenraum()
    {
        var (p, b) = MoebelhausMitAenderung(Daten.NeueAblage());
        var a = p.AussenStand;
        var ort = AussenBearbeitung.Ort(a, "moebelhaus")!;
        var z = AussenBearbeitung.Zugang(a, "moebelhaus")!;
        Assert.Equal("moebelhaus", b.Modell);
        Assert.Equal(Json(Get(AussenBearbeitung.Modelle(a), "moebelhaus")), Json(b.Teile));
        Assert.Equal(Py.F(Liste(z["tuer"])[0]) - Py.F(ort["x"]), Py.F(b.Tuer![0]), 6);
        Assert.Equal(Py.F(Liste(z["tuer"])[1]) - Py.F(ort["y"]), Py.F(b.Tuer![1]), 6);
        Assert.Equal(Liste(z["weg"]).Count, b.Weg.Count);
        Assert.NotNull(b.Schild);
        // der Innenraum ist der bearbeitete (mit dem gedrehten Regal), nicht der aus Fassung 0
        Assert.Equal(Json(p.Grundriss("moebelhaus")), Json(b.Innenraum));
        Assert.NotEqual(Json(p.Original("moebelhaus")), Json(b.Innenraum));
        Assert.Equal(Json(p.Personen("moebelhaus")), Json(b.Personen));
        Assert.Equal("stadt1", Py.Str(b.Herkunft["stadt"]));
        // Gegenprobe: Strasse, Baum und Laterne sind keine Gebaeude
        Assert.Null(Blueprint.Aus(p, new AussenStelle(AussenArt.Strasse, 0), "Straße"));
        Assert.Null(Blueprint.Aus(p, new AussenStelle(AussenArt.Laterne, 0), "Laterne"));
        Assert.Throws<ArgumentException>(() => Blueprint.Aus(p, Moebelhaus, "a/b"));
    }

    [Fact]
    public void Aus_GebaeudeOhneOrt_StellwerkMitTuerOhneInnenraum()
    {
        var p = Projekt.Neu("Stellwerk", D, Daten.NeueAblage());
        var i = Aussen.Landschaft(p.AussenStand).FindIndex(t => t is PyObjekt o && Py.Gleich(o["modell"], "stellwerk"));
        var b = Blueprint.Aus(p, new AussenStelle(AussenArt.Gebaeude, i), "Stellwerk Nord")!;
        Assert.Equal("stellwerk", b.Modell);
        Assert.NotNull(b.Tuer);
        Assert.True(b.Weg.Count >= 2);
        Assert.Null(b.Innenraum);
        Assert.Empty(b.Personen);
    }

    [Fact]
    public void Speichern_Laden_GleichesGebaeude_JeFeldEineZeile_UndSicherung()
    {
        var ablage = Daten.NeueAblage();
        var (_, b) = MoebelhausMitAenderung(ablage);
        var pfad = b.Speichern(ablage, Zeit);
        Assert.Equal(Path.Combine(ablage.Blueprints, "Mein Moebelhaus.fisblueprint"), pfad);
        var zeilen = File.ReadAllText(pfad, Encoding.UTF8).Split('\n');
        Assert.Equal("{", zeilen[0]);
        Assert.Equal(Blueprint.Felder, zeilen.Skip(1).Take(Blueprint.Felder.Length).Select(z => z.Split('"')[1]));
        var eintrag = Assert.Single(Blueprint.Alle(ablage));
        Assert.Equal("Mein Möbelhaus", eintrag.Name);
        Assert.Null(eintrag.Fehler);
        Assert.True(eintrag.Vorlage!.GleichesGebaeude(b));
        Assert.Equal(b.Schreiben(), eintrag.Vorlage.Schreiben());
        Assert.Single(Blueprint.SicherungenFuer(ablage, "Mein Möbelhaus").Liste());
        // doppelter Name (auch in anderer Schreibweise) geht nicht, nichts wird ueberschrieben
        Assert.Throws<IOException>(() => b.Speichern(ablage, Zeit));
        Assert.Throws<IOException>(() => MoebelhausMitAenderung(ablage, "mein möbelhaus").B.Speichern(ablage, Zeit));
        Assert.Single(Blueprint.Alle(ablage));
        // Gegenprobe: ein anderes Gebaeude ist nicht gleich
        var p2 = Projekt.Neu("Andere", D, ablage);
        Assert.False(Blueprint.Aus(p2, new AussenStelle(AussenArt.Haus, -1, 0, "elektromarkt"), "E")!.GleichesGebaeude(b));
        Assert.False(Blueprint.Aus(p2, Moebelhaus, "M")!.GleichesGebaeude(b));   // ohne das gedrehte Regal
    }

    [Fact]
    public void Setzen_InLeereStadt_GleichesGebaeudeNurAndereLage_OhneRotenFund()
    {
        var ablage = Daten.NeueAblage();
        var (_, b) = MoebelhausMitAenderung(ablage);
        var p = LeereStadt(ablage);
        Assert.Empty(p.PruefenAussen().Where(f => f.Rot).Select(f => f.Text));
        var (x, y) = MitteAnStrasse(b, 10);
        string? grund = null;
        var s = p.AussenAendern(Texte.BlueprintSetzen, a => b.Setzen(a, x, y, out grund));
        Assert.Null(grund);
        Assert.Equal(AussenArt.Gebaeude, s!.Art);
        var a = p.AussenStand;
        var haus = Objekt(Aussen.Landschaft(a)[s.Nummer]);
        Assert.Equal("moebelhaus", Py.Str(haus["modell"]));
        // gleiches Gebaeude, nur andere Lage: aus dem gesetzten Gebaeude wieder ein Blueprint, dann gleich (ohne Innenraum,
        // der kommt in die Datei nicht ueber das Gebaeude ohne Ort)
        var wieder = Blueprint.Aus(p, s, "Wieder")!;
        Assert.Equal(b.Modell, wieder.Modell);
        Assert.Equal(Json(b.Teile), Json(wieder.Teile));
        Assert.Equal(Json(b.Tuer), Json(wieder.Tuer));
        Assert.Equal(Json(b.Weg[0]), Json(wieder.Weg[0]));
        Assert.Equal(b.Weg.Count, wieder.Weg.Count);
        // der Fussweg reicht bis an die Strasse (verlaengert), Gegenprobe: im Blueprint endet er 1 davor
        Assert.Equal(12.0, Py.F(Liste(wieder.Weg[^1])[1]) + Py.F(haus["y"]), 4);
        Assert.True(Math.Abs(Py.F(Liste(wieder.Weg[^1])[1]) - Py.F(Liste(b.Weg[^1])[1])) > 0.5);
        Assert.NotEqual(Json(AussenBearbeitung.Ort(Projekt.Neu("Stadt 1", D, ablage).AussenStand, "moebelhaus")!["x"]), Json(haus["x"]));
        var zier = Blueprint.Zierweg(a, "moebelhaus")!;
        Assert.Equal("Mein Möbelhaus", Py.Str(zier["name"]));
        // Pruefung draussen wie fuer jedes Gebaeude: gruen
        Assert.Empty(p.PruefenAussen().Where(f => f.Rot).Select(f => f.Text));
        // ein ganz normales Gebaeude: verschieben nimmt Tuer und Weg mit, Rueckgaengig nimmt alles weg
        p.AussenAendern("Verschieben", aa => AussenBearbeitung.Verschieben(aa, s, 2, 0));
        Assert.Equal(Py.F(b.Tuer![0]) + Py.F(Objekt(Aussen.Landschaft(p.AussenStand)[s.Nummer])["x"]), Py.F(Liste(Blueprint.Zierweg(p.AussenStand, "moebelhaus")!["tuer"])[0]), 6);
        p.Rueckgaengig();
        p.Rueckgaengig();
        Assert.Null(Blueprint.Zierweg(p.AussenStand, "moebelhaus"));
        Assert.Single(Aussen.Landschaft(p.AussenStand));
    }

    [Fact]
    public void Setzen_AnBelegteStelle_Rot_WieJedesGebaeude()
    {
        var ablage = Daten.NeueAblage();
        var (_, b) = MoebelhausMitAenderung(ablage);
        var p = LeereStadt(ablage);
        var (x, y) = MitteAnStrasse(b, 10);
        // erst ein anderes Gebaeude genau dorthin, dann der Blueprint: ineinander, rot
        p.AussenAendern("Haus", a => AussenBearbeitung.Setzen(a, AussenArt.Gebaeude, x, y, modell: "elektromarkt"));
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Rot && f.Text.Contains("ineinander", StringComparison.Ordinal));
        p.AussenAendern(Texte.BlueprintSetzen, a => b.Setzen(a, x, y, out _));
        Assert.Contains(p.PruefenAussen(), f => f.Rot && f.Text.Contains("ineinander", StringComparison.Ordinal));
        // auf der Strasse: rot
        var q = LeereStadt(ablage, "Leer zwei");
        q.AussenAendern(Texte.BlueprintSetzen, a => b.Setzen(a, 10, 12, out _));
        Assert.Contains(q.PruefenAussen(), f => f.Rot);
        // ausserhalb der Karte: nichts gesetzt, mit Grund
        string? grund = null;
        Assert.Null(q.AussenAendern(Texte.BlueprintSetzen, a => b.Setzen(a, 40, 12, out grund)));
        Assert.Equal(Texte.BlueprintAusserhalb, grund);
    }

    [Fact]
    public void Setzen_ZweimalDasselbeModellMitTuer_Rot_Gegenprobe()
    {
        var ablage = Daten.NeueAblage();
        var (_, b) = MoebelhausMitAenderung(ablage);
        var p = LeereStadt(ablage);
        var (x, y) = MitteAnStrasse(b, 7);
        p.AussenAendern(Texte.BlueprintSetzen, a => b.Setzen(a, x, y, out _));
        Assert.DoesNotContain(p.PruefenAussen(), f => f.Text.StartsWith(Texte.BlueprintSchonDa, StringComparison.Ordinal));
        p.AussenAendern(Texte.BlueprintSetzen, a => b.Setzen(a, x + 8, y, out _));
        var f = Assert.Single(p.PruefenAussen(), f => f.Text.StartsWith(Texte.BlueprintSchonDa, StringComparison.Ordinal));
        Assert.True(f.Rot);
        Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now));
    }

    [Fact]
    public void Setzen_ModellFehltInDerStadt_KommtNachNeueModelle_AnderesModellGleichenNamens_Nicht()
    {
        var ablage = Daten.NeueAblage();
        var (_, b) = MoebelhausMitAenderung(ablage);
        var p = LeereStadt(ablage);
        // Modell aus der leeren Stadt nehmen: der Blueprint bringt es mit
        p.AussenAendern("ohne", a => Objekt(Aussen.Stadtdaten(a)["neue_modelle"]).RemoveAll(e => e.Key == "moebelhaus"));
        Assert.Null(Get(AussenBearbeitung.Modelle(p.AussenStand), "moebelhaus"));
        var (x, y) = MitteAnStrasse(b, 10);
        p.AussenAendern(Texte.BlueprintSetzen, a => b.Setzen(a, x, y, out _));
        Assert.Equal(Json(b.Teile), Json(Get(AussenBearbeitung.Modelle(p.AussenStand), "moebelhaus")));
        // Gegenprobe: gleicher Name, andere Teile: nicht gesetzt
        var anders = new Blueprint { Name = "Anders", Herkunft = b.Herkunft, Modell = "moebelhaus", Teile = [new PyObjekt { { "art", "block" }, { "x", 0L }, { "y", 0L }, { "w", 1L }, { "t", 1L }, { "h", 1L } }] };
        string? grund = null;
        var vorher = Aussen.Landschaft(p.AussenStand).Count;
        Assert.Null(p.AussenAendern(Texte.BlueprintSetzen, a => anders.Setzen(a, 20, 5, out grund)));
        Assert.Equal(Texte.BlueprintModellAnders("moebelhaus"), grund);
        Assert.Equal(vorher, Aussen.Landschaft(p.AussenStand).Count);
    }

    [Fact]
    public void Kaputt_Neuer_Fremd_StehenMitHinweisInDerListe_NichtsVerschwindet()
    {
        var ablage = Daten.NeueAblage();
        var (_, b) = MoebelhausMitAenderung(ablage);
        var gut = b.Schreiben();
        void Datei(string name, byte[] daten) => File.WriteAllBytes(Path.Combine(ablage.Blueprints, name + Blueprint.Endung), daten);
        Datei("ok", gut);
        Datei("abgeschnitten", gut[..(gut.Length / 2)]);
        var text = Encoding.UTF8.GetString(gut);
        Datei("veraendert", Encoding.UTF8.GetBytes(text.Replace("\"Mein Möbelhaus\"", "\"Dein Möbelhaus\"", StringComparison.Ordinal)));
        Datei("neuer", Encoding.UTF8.GetBytes(text.Replace("\"formatversion\": 1", "\"formatversion\": 2", StringComparison.Ordinal)));
        Datei("fremd", Encoding.UTF8.GetBytes("{\"format\": \"fisbau\"}"));
        Datei("feldfehlt", Encoding.UTF8.GetBytes(text.Replace(" \"schild\":", " \"schildx\":", StringComparison.Ordinal)));
        var liste = Blueprint.Alle(ablage).ToDictionary(e => Path.GetFileNameWithoutExtension(e.Datei));
        Assert.Equal(6, liste.Count);
        Assert.Null(liste["ok"].Fehler);
        Assert.Equal(Texte.BlueprintKaputt, liste["abgeschnitten"].Fehler);
        Assert.Equal(Texte.BlueprintPruefsumme, liste["veraendert"].Fehler);
        Assert.Equal(Texte.BlueprintNeuer, liste["neuer"].Fehler);
        Assert.True(liste["neuer"].Neuer);
        Assert.False(liste["veraendert"].Neuer);
        Assert.Equal(Texte.BlueprintKeiner, liste["fremd"].Fehler);
        Assert.Contains("Feld „schild“ fehlt.", liste["feldfehlt"].Fehler, StringComparison.Ordinal);
        Assert.Contains("Feld „schildx“ ist unbekannt.", liste["feldfehlt"].Fehler, StringComparison.Ordinal);
        Assert.All(liste.Values.Where(e => e.Fehler is not null), e => Assert.Null(e.Vorlage));
        // Gegenprobe: dieselbe Datei unveraendert ist lesbar
        Assert.True(Blueprint.Lesen(gut).GleichesGebaeude(b));
        // Werte falsch, Pruefsumme passend neu gerechnet: trotzdem kaputt
        var falsch = new PyObjekt();
        falsch.AddRange(((PyObjekt)Py.Lesen(gut)!).Where(p => p.Key != "pruefsumme"));
        Py.Setzen(falsch, "tuer", "Tür");
        falsch.Add("pruefsumme", StadtFassung.Pruefsumme(falsch));
        Assert.Equal([Texte.BlueprintKaputt], Assert.Throws<StadtbauFehler>(() => Blueprint.Lesen(PythonJson.Utf8(PythonJson.Standard(falsch)))).Funde);
    }

    [Fact]
    public void Umbenennen_UndLoeschen_MitSicherung_Gegenprobe()
    {
        var ablage = Daten.NeueAblage();
        var (_, b) = MoebelhausMitAenderung(ablage);
        var alt = b.Speichern(ablage, Zeit);
        var (_, c) = MoebelhausMitAenderung(ablage, "Zweites");
        c.Speichern(ablage, Zeit);
        // auf einen vorhandenen Namen: Fehler, beide bleiben
        Assert.Throws<IOException>(() => Blueprint.Umbenennen(ablage, alt, "Zweites", Zeit));
        Assert.Throws<ArgumentException>(() => Blueprint.Umbenennen(ablage, alt, "a/b", Zeit));
        Assert.Equal(2, Blueprint.Alle(ablage).Count);
        var neu = Blueprint.Umbenennen(ablage, alt, "Möbelhaus Süd", Zeit);
        Assert.False(File.Exists(alt));
        Assert.Equal(Path.Combine(ablage.Blueprints, "Moebelhaus Sued.fisblueprint"), neu.Datei);
        Assert.Equal(["Möbelhaus Süd", "Zweites"], Blueprint.Alle(ablage).Select(e => e.Name));
        Assert.True(Blueprint.Alle(ablage)[0].Vorlage!.GleichesGebaeude(b));
        // nur Gross-/Kleinschreibung aendern geht (gleiche Datei auf Windows)
        Blueprint.Umbenennen(ablage, neu.Datei!, "Möbelhaus süd", Zeit);
        Assert.Equal("Möbelhaus süd", Blueprint.Alle(ablage)[0].Name);
        Assert.Equal(2, Blueprint.Alle(ablage).Count);
        // beide Namen haben ihre Sicherung
        Assert.Equal(2, Blueprint.SicherungenFuer(ablage, "Mein Möbelhaus").Liste().Count);
        Assert.NotEmpty(Blueprint.SicherungenFuer(ablage, "Möbelhaus Süd").Liste());
        // Loeschen: Datei weg, Inhalt in der Sicherung (auch eine kaputte Datei)
        var weg = Blueprint.Alle(ablage).First(e => e.Name == "Zweites").Datei;
        var inhalt = File.ReadAllBytes(weg);
        Blueprint.Loeschen(ablage, weg, Zeit.AddMinutes(1));
        Assert.False(File.Exists(weg));
        Assert.Equal(inhalt, File.ReadAllBytes(Blueprint.SicherungenFuer(ablage, "Zweites").Liste()[0]));
        var kaputt = Path.Combine(ablage.Blueprints, "kaputt" + Blueprint.Endung);
        File.WriteAllText(kaputt, "{");
        Blueprint.Loeschen(ablage, kaputt, Zeit);
        Assert.Equal("{", File.ReadAllText(Blueprint.SicherungenFuer(ablage, "kaputt").Liste()[0]));
        Assert.Single(Blueprint.Alle(ablage));
    }

    [Fact]
    public void Sicherungen_ImEigenenOrdner_KeinProjektKannSoHeissen()
    {
        Assert.False(Projekt.NameGueltig(Sicherungen.BlueprintOrdner));
        var ablage = Daten.NeueAblage();
        var (_, b) = MoebelhausMitAenderung(ablage);
        b.Speichern(ablage, Zeit);
        Assert.True(Directory.Exists(Path.Combine(ablage.Sicherungen, Sicherungen.BlueprintOrdner, "Mein Moebelhaus")));
        Assert.Equal(Blueprint.Endung, Path.GetExtension(Blueprint.SicherungenFuer(ablage, "Mein Möbelhaus").Liste()[0]));
        // Projekte behalten ihre Endung (Gegenprobe)
        Assert.All(Directory.GetFiles(Path.Combine(ablage.Sicherungen), "*", SearchOption.TopDirectoryOnly), f => Assert.Fail(f));
        Assert.All(Directory.GetDirectories(ablage.Sicherungen).Where(d => !d.EndsWith(Sicherungen.BlueprintOrdner, StringComparison.Ordinal)),
            d => Assert.All(Directory.GetFiles(d), f => Assert.Equal(Projekt.Endung, Path.GetExtension(f))));
    }
}
