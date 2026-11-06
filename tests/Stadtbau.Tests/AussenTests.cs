using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Phase 13 Welle 3 (Weisung P12-18, Plan Abschnitt 3): Stadt draussen bearbeiten. Setzen, Verschieben (Haus nimmt Tuer,
/// Schild und Fussweg mit), Drehen (Strasse, Park; Haeuser nicht), Kopieren, Loeschen (Orte und Paketplatz nicht),
/// Gehweg, Paketplatz, leere Stadt; Projekt mit Verlauf, Formatnummer 2 und Lesen der Formatnummer 1.
/// </summary>
public class AussenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static PyObjekt Stadt1() => Aussen.Aus(D.Fassung);

    private static string Json(object? o) => PythonJson.Standard(o);

    [Fact]
    public void AusUndZurueck_GleichDerFassung0()
    {
        var f = D.Fassung;
        var a = Stadt1();
        var zurueck = Aussen.Fassung(a, f, f.Innenraeume, f.Nummer, f.Basis, f.Werkzeug, f.Erstellt, f.Nachricht);
        Assert.Equal(Json(f.Karte), Json(zurueck.Karte));
        Assert.Equal(Json(f.Stadtdaten), Json(zurueck.Stadtdaten));
        Assert.Equal(f.Laternen, zurueck.Laternen);
        Assert.Equal(f.Beete, zurueck.Beete);
        // Kopie: Aendern beruehrt die Basis nicht
        Aussen.Laternen(a).Clear();
        Assert.Equal(22, f.Laternen.Count);
    }

    [Fact]
    public void Teile_SetzenUndEntfernen()
    {
        var a = Stadt1();
        var teile = Aussen.TeileText(a);
        Assert.Contains("karte/welt", teile.Keys);
        Assert.Contains("stadtdaten/zugaenge", teile.Keys);
        Assert.Contains("laternen", teile.Keys);
        Aussen.TeilSetzen(a, "stadtdaten/neu", 5L, vorhanden: true);
        Assert.Equal("5", Aussen.TeileText(a)["stadtdaten/neu"]);
        Aussen.TeilSetzen(a, "stadtdaten/neu", null, vorhanden: false);
        Assert.Equal(teile, Aussen.TeileText(a));
    }

    [Fact]
    public void Setzen_JedeArt_UndAusserhalbNichts()
    {
        var a = Stadt1();
        var strasse = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 22.2, 2.1)!;
        var l = Objekt(Aussen.Landschaft(a)[strasse.Nummer]);
        Assert.Equal("[[20.0, 2.0], [24.0, 2.0]]", Json(l["punkte"]));
        Assert.Equal(AussenBearbeitung.NeuerGehweg, Py.F(l["gehweg"]));
        var park = AussenBearbeitung.Setzen(a, AussenArt.Park, 22, 2)!;
        Assert.Equal(3.0, Py.F(Objekt(Aussen.Landschaft(a)[park.Nummer])["w"]));
        Assert.Null(AussenBearbeitung.Setzen(a, AussenArt.Gebaeude, 22, 2));
        var haus = AussenBearbeitung.Setzen(a, AussenArt.Gebaeude, 22, 2, modell: "filiale")!;
        Assert.Equal("filiale", Py.Str(Objekt(Aussen.Landschaft(a)[haus.Nummer])["modell"]));
        var baum = AussenBearbeitung.Setzen(a, AussenArt.Baum, 3.33, 3.36)!;
        Assert.Equal((3.5, 3.5), AussenBearbeitung.Lage(a, baum));
        var fein = AussenBearbeitung.Setzen(a, AussenArt.Baum, 3.33, 3.36, raster: false)!;
        Assert.Equal((3.3, 3.4), AussenBearbeitung.Lage(a, fein));
        var beet = AussenBearbeitung.Setzen(a, AussenArt.Beet, 4, 4)!;
        Assert.Equal(2, Aussen.Beete(a).Count);
        var laterne = AussenBearbeitung.Setzen(a, AussenArt.Laterne, 5, 5)!;
        Assert.Equal("[5.0, 5.0, false]", Json(Aussen.Laternen(a)[laterne.Nummer]));
        Assert.Null(AussenBearbeitung.Setzen(a, AussenArt.Haus, 5, 5));
        Assert.Null(AussenBearbeitung.Setzen(a, AussenArt.Paketplatz, 5, 5));
        Assert.Null(AussenBearbeitung.Setzen(a, AussenArt.Baum, -1, 5));
        Assert.Null(AussenBearbeitung.Setzen(a, AussenArt.Baum, 5, 99));
        Assert.All(new[] { strasse, park, haus, baum, beet, laterne }, s => Assert.True(AussenBearbeitung.Gueltig(a, s)));
    }

    [Fact]
    public void NeueStrasse_RastetAnNachbarstrasseEin()
    {
        var a = Stadt1();
        // senkrechte Strasse x 7,5: eine neue waagrechte Strasse mit dem linken Ende bei 7,9 rastet auf 7,5 ein
        var s = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 9.9, 15)!;
        var punkte = Liste(Objekt(Aussen.Landschaft(a)[s.Nummer])["punkte"]);
        Assert.Equal("[7.5, 15.0]", Json(punkte[0]));
        Assert.Equal("[12.0, 15.0]", Json(punkte[1]));
    }

    [Fact]
    public void Verschieben_HausNimmtTuerSchildUndWegMit_WegEndetAnDerStrasse()
    {
        var a = Stadt1();
        var haus = new AussenStelle(AussenArt.Haus, -1, 0, "zuhause");
        var tuerVorher = Json(AussenBearbeitung.Zugang(a, "zuhause")!["tuer"]);
        AussenBearbeitung.Verschieben(a, haus, -1.04, -0.51);
        var o = AussenBearbeitung.Ort(a, "zuhause")!;
        Assert.Equal(25.0, Py.F(o["x"]));
        Assert.Equal(17.5, Py.F(o["y"]));
        var z = AussenBearbeitung.Zugang(a, "zuhause")!;
        Assert.NotEqual(tuerVorher, Json(z["tuer"]));
        Assert.Equal("[26.7, 19.3]", Json(z["tuer"]));
        // der Fussweg wurde bis zur Strasse y 20,8 verlaengert
        var weg = Liste(z["weg"]);
        Assert.Equal("[26.7, 20.8]", Json(weg[^1]));
        // Gegenprobe: ohne Strasse in Reichweite bleibt der Weg (die Pruefung meldet ihn rot)
        var w = new List<object?> { new List<object?> { 1.0, 1.0 }, new List<object?> { 1.0, 1.5 } };
        Assert.False(AussenBearbeitung.WegAnschliessen(w, []));
    }

    [Fact]
    public void Verschieben_Strasse_Park_Gruen_Laterne_Paketplatz()
    {
        var a = Stadt1();
        var s = new AussenStelle(AussenArt.Strasse, 6);
        AussenBearbeitung.Verschieben(a, s, 0.3, 0);
        Assert.Equal("[[30.3, 14.4], [30.3, 20.8]]", Json(Objekt(Aussen.Landschaft(a)[6])["punkte"]));
        var p = new AussenStelle(AussenArt.Park, 0);
        AussenBearbeitung.Verschieben(a, p, 0.2, 0.2);
        Assert.Equal(8.6, Py.F(Objekt(Aussen.Landschaft(a)[0])["x"]));
        var l = new AussenStelle(AussenArt.Laterne, 0);
        AussenBearbeitung.Verschieben(a, l, 0.12, 0, raster: false);
        Assert.Equal(8.1, Py.F(Liste(Aussen.Laternen(a)[0])[0]));
        var paket = new AussenStelle(AussenArt.Paketplatz, 0);
        var vorher = AussenBearbeitung.PaketWerte(a);
        AussenBearbeitung.Verschieben(a, paket, 0, 0.1, raster: false);
        Assert.Equal((Math.Round(vorher.Vor + 0.1, 2), vorher.Seitlich), AussenBearbeitung.PaketWerte(a));
    }

    [Fact]
    public void Drehen_StrasseUndPark_HaeuserUndLaternenNicht()
    {
        var a = Stadt1();
        var s = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 22, 2)!;
        Assert.True(AussenBearbeitung.Drehen(a, s));
        Assert.Equal("[[22.0, 0.0], [22.0, 4.0]]", Json(Objekt(Aussen.Landschaft(a)[s.Nummer])["punkte"]));
        var p = new AussenStelle(AussenArt.Park, 1);
        Assert.True(AussenBearbeitung.Drehen(a, p));
        var park = Objekt(Aussen.Landschaft(a)[1]);
        Assert.Equal((7.2, 7.0), (Py.F(park["w"]), Py.F(park["h"])));
        var haus = new AussenStelle(AussenArt.Haus, -1, 0, "zuhause");
        Assert.Equal(Texte.HausDrehenNicht, AussenBearbeitung.DrehenNicht(haus));
        Assert.False(AussenBearbeitung.Drehen(a, haus));
        Assert.Equal(Texte.LaterneDrehenNicht, AussenBearbeitung.DrehenNicht(new AussenStelle(AussenArt.Laterne, 0)));
        Assert.Equal(Texte.DrehenNichtNoetig, AussenBearbeitung.DrehenNicht(new AussenStelle(AussenArt.Baum, 0)));
    }

    [Fact]
    public void Kopieren_UndLoeschen_OrteUndPaketplatzBleiben()
    {
        var a = Stadt1();
        var stellwerk = AussenBearbeitung.Alle(a).First(x => x.Stelle.Art == AussenArt.Gebaeude).Stelle;
        var kopie = AussenBearbeitung.Kopieren(a, stellwerk)!;
        Assert.Equal(AussenArt.Gebaeude, kopie.Art);
        Assert.True(AussenBearbeitung.Loeschen(a, stellwerk));
        // der Zierweg bleibt, solange noch ein Stellwerk steht; mit dem letzten geht er
        var zierwege = Liste(Objekt(Aussen.Stadtdaten(a)["darstellung"])["zierwege"]);
        Assert.Contains(zierwege, z => Py.Gleich(Objekt(z)["modell"], "stellwerk"));
        Assert.True(AussenBearbeitung.Loeschen(a, new AussenStelle(AussenArt.Gebaeude, kopie.Nummer - 1)));
        Assert.DoesNotContain(zierwege, z => Py.Gleich(Objekt(z)["modell"], "stellwerk"));
        var laternen = Aussen.Laternen(a).Count;
        Assert.True(AussenBearbeitung.Loeschen(a, new AussenStelle(AussenArt.Laterne, 3)));
        Assert.Equal(laternen - 1, Aussen.Laternen(a).Count);
        Assert.Equal(Texte.OrtLoeschenNicht, AussenBearbeitung.LoeschenNicht(new AussenStelle(AussenArt.Haus, -1, 0, "zuhause")));
        Assert.False(AussenBearbeitung.Loeschen(a, new AussenStelle(AussenArt.Haus, -1, 0, "zuhause")));
        Assert.False(AussenBearbeitung.Loeschen(a, new AussenStelle(AussenArt.Paketplatz, 0)));
    }

    [Fact]
    public void Gehweg_Laenge_Laterne_Beschreibung()
    {
        var a = Stadt1();
        var s = new AussenStelle(AussenArt.Strasse, 7);
        Assert.Equal(0f, AussenBearbeitung.Gehweg(a, s));
        AussenBearbeitung.GehwegSetzen(a, s, 0.4);
        Assert.Equal(0.4f, AussenBearbeitung.Gehweg(a, s));
        Assert.Contains("Gehweg 0,4", AussenBearbeitung.Beschreibung(a, s, x => x), StringComparison.Ordinal);
        AussenBearbeitung.GehwegSetzen(a, s, 0);
        Assert.False(Py.Hat(Objekt(Aussen.Landschaft(a)[7]), "gehweg"));
        Assert.Equal(10.9, AussenBearbeitung.Laenge(a, s));
        AussenBearbeitung.LaengeSetzen(a, s, 6);
        Assert.Equal(6, AussenBearbeitung.Laenge(a, s));
        var l = new AussenStelle(AussenArt.Laterne, 0);
        AussenBearbeitung.LaterneDoppelt(a, l, true);
        Assert.StartsWith(Texte.LaterneDoppelt + " Nr. 1", AussenBearbeitung.Beschreibung(a, l, x => x), StringComparison.Ordinal);
        Assert.StartsWith(Texte.Haus + " „Wohnung“", AussenBearbeitung.Beschreibung(a, new AussenStelle(AussenArt.Haus, -1, 0, "zuhause"), _ => "Wohnung"), StringComparison.Ordinal);
    }

    [Fact]
    public void StueckBei_KleinesVorHausVorStrasse()
    {
        var a = Stadt1();
        Assert.Equal(new AussenStelle(AussenArt.Laterne, 0), AussenBearbeitung.StueckBei(a, 8.05, 0.7));
        var (hx, hy, hw, hh) = AussenBearbeitung.HausFlaeche(a, new AussenStelle(AussenArt.Haus, -1, 0, "zuhause"));
        Assert.Equal("zuhause", AussenBearbeitung.StueckBei(a, hx + (hw / 2), hy + (hh / 2))!.Ort);
        Assert.Equal(new AussenStelle(AussenArt.Strasse, 3), AussenBearbeitung.StueckBei(a, 12, 20.9));
        Assert.Null(AussenBearbeitung.StueckBei(a, 31.5, 0.5));
    }

    [Fact]
    public void LeereStadt_GroesseUndInhalt()
    {
        Assert.True(Aussen.GroesseGueltig(8, 64));
        Assert.True(Aussen.GroesseGueltig(20.5, 16));
        Assert.False(Aussen.GroesseGueltig(7.5, 16));
        Assert.False(Aussen.GroesseGueltig(20.3, 16));
        Assert.False(Aussen.GroesseGueltig(65, 16));
        var f = Aussen.Leer(D.Fassung, "neu_test", 20, 16);
        Assert.Empty(Liste(f.Karte["orte"]));
        Assert.Empty(f.Laternen);
        Assert.Empty(f.Innenraeume);
        Assert.Equal(Json(D.Fassung.Karte["modelle"]), Json(f.Karte["modelle"]));
        Assert.Empty(StadtbauPruefung.Format(Py.Lesen(f.Schreiben())));
        Assert.Empty(StadtbauPruefung.Inhalt(f, D.Inhalt));
        Assert.Empty(StadtbauPruefung.Aussen(f, D.Inhalt, D.Grafik));
    }

    // ---- Projekt -----------------------------------------------------------------------------------

    [Fact]
    public void Projekt_AussenAendern_RueckgaengigWiederholen_UndGespeichert()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Draußen", D, ablage);
        Assert.EndsWith("Draussen.fisbau", p.Datei, StringComparison.Ordinal);
        Assert.False(p.AussenGeaendert);
        var s = p.AussenAendern("Setzen", a => AussenBearbeitung.Setzen(a, AussenArt.Baum, 22, 2));
        Assert.NotNull(s);
        Assert.Equal(["karte/welt"], p.GeaenderteTeile);
        var schritt = p.Verlauf[^1];
        Assert.Equal(Projekt.StadtOrt, schritt.Ort);
        Assert.StartsWith("{\"karte/welt\": [", schritt.Vorher, StringComparison.Ordinal);
        // nichts geaendert: kein Schritt
        p.AussenAendern("nichts", _ => { });
        Assert.Single(p.Verlauf);
        p.AussenAendern("Laterne weg", a => AussenBearbeitung.Loeschen(a, new AussenStelle(AussenArt.Laterne, 0)));
        Assert.Equal(["karte/welt", "laternen"], p.GeaenderteTeile);
        // gespeichert und wieder geoeffnet: gleicher Stand
        var wieder = Projekt.Oeffnen(p.Datei, D, ablage);
        Assert.Equal(Json(p.AussenStand), Json(wieder.AussenStand));
        Assert.Equal(2, wieder.Verlauf.Count);
        Assert.True(wieder.Rueckgaengig());
        Assert.True(wieder.LetzterSchrittDraussen);
        Assert.Equal(22, Aussen.Laternen(wieder.AussenStand).Count);
        Assert.True(wieder.Rueckgaengig());
        Assert.False(wieder.AussenGeaendert);
        Assert.True(wieder.Wiederholen());
        Assert.Equal(["karte/welt"], wieder.GeaenderteTeile);
        Assert.Equal("Draußen", Projekt.AnzeigeName(wieder.Datei));
    }

    [Fact]
    public void Projekt_Formatnummer1_LaesstSichOeffnen()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Alt", D, ablage, "bank");
        // Datei so, wie Welle 2 (S 0.1.0) sie schrieb: Formatnummer 1 ohne "leer" und "aussen"
        var zeilen = File.ReadAllText(p.Datei, Encoding.UTF8).Split('\n')
            .Where(z => !z.StartsWith(" \"leer\"", StringComparison.Ordinal) && !z.StartsWith(" \"aussen\"", StringComparison.Ordinal))
            .Select(z => z.Replace("\"formatversion\": 3,", "\"formatversion\": 1,", StringComparison.Ordinal));
        File.WriteAllText(p.Datei, string.Join('\n', zeilen), new UTF8Encoding(false));
        var alt = Projekt.Oeffnen(p.Datei, D, ablage);
        Assert.False(alt.NurLesen);
        Assert.Equal("bank", alt.Ort);
        // Gegenprobe: Formatnummer 2 ohne "aussen" fehlt ein Feld
        File.WriteAllText(p.Datei, string.Join('\n', zeilen).Replace("\"formatversion\": 1,", "\"formatversion\": 3,", StringComparison.Ordinal), new UTF8Encoding(false));
        var f = Assert.Throws<StadtbauFehler>(() => Projekt.Oeffnen(p.Datei, D, ablage));
        Assert.Contains("Feld „leer“ fehlt.", f.Funde);
    }

    [Fact]
    public void Projekt_LeereStadt_StrasseUndBaum_Export()
    {
        var ablage = Daten.NeueAblage();
        Assert.Throws<ArgumentException>(() => Projekt.Neu("Zu klein", D, ablage, leer: (6, 16)));
        var p = Projekt.Neu("Neue Stadt", D, ablage, leer: (20, 16));
        Assert.Empty(p.Orte);
        Assert.Equal("neu_neue_stadt", p.Basis.StadtId);
        Assert.Equal("neu_aerger_mit_der_stra_e_123456", Projekt.LeerStadtId("Ärger mit der Stra%e 12345678"));
        Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now));
        p.AussenAendern("Straße", a => AussenBearbeitung.Setzen(a, AussenArt.Strasse, 10, 8));
        p.AussenAendern("Baum", a => AussenBearbeitung.Setzen(a, AussenArt.Baum, 10, 6));
        var datei = p.Exportieren(new DateTimeOffset(2026, 11, 6, 9, 30, 0, TimeSpan.FromHours(1)));
        var f = StadtFassung.Lesen(datei);
        Assert.Equal("neu_neue_stadt", f.StadtId);
        Assert.Equal(1, f.Nummer);
        Assert.Null(f.Basis);
        Assert.Equal(2, Aussen.Landschaft(Aussen.Aus(f)).Count);
        Assert.StartsWith("neu_neue_stadt_Neue_Stadt_2026-11-06_0930", p.ExportName(new DateTimeOffset(2026, 11, 6, 9, 30, 0, TimeSpan.Zero)), StringComparison.Ordinal);
        var wieder = Projekt.Oeffnen(p.Datei, D, ablage);
        Assert.Equal((20.0, 16.0), wieder.Leer);
        Assert.Equal(Json(p.AussenStand), Json(wieder.AussenStand));
    }

    [Fact]
    public void Projekt_Export_DraussenRot_Gesperrt_GruenGeht()
    {
        var p = Projekt.Neu("Rot draußen", D, Daten.NeueAblage());
        // Baum mitten in die Wohnung: rot, Export gesperrt
        var (hx, hy, hw, hh) = AussenBearbeitung.HausFlaeche(p.AussenStand, new AussenStelle(AussenArt.Haus, -1, 0, "zuhause"));
        p.AussenAendern("Baum", a => AussenBearbeitung.Setzen(a, AussenArt.Baum, hx + (hw / 2), hy + (hh / 2), raster: false));
        var f = Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now));
        Assert.Contains(f.Funde, t => t.StartsWith("Stadt draußen: Ein Baum steht im Haus", StringComparison.Ordinal));
        p.Rueckgaengig();
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        p.AussenAendern("Baum", a => AussenBearbeitung.Setzen(a, AussenArt.Baum, 22, 2));
        var neu = StadtFassung.Lesen(p.Exportieren(DateTimeOffset.Now));
        Assert.Equal(1, neu.Nummer);
        Assert.Equal(0, neu.Basis);
        Assert.Equal(Json(p.AussenStand["karte"]), Json(neu.Karte));
    }

    [Theory]
    [InlineData("Möbelhaus umbauen", "Moebelhaus umbauen")]
    [InlineData("Straße Süd", "Strasse Sued")]
    [InlineData("Café", "Cafe")]
    [InlineData("Ärger Öl Übung", "Aerger Oel Uebung")]
    public void DateiName_AsciiMitUmlauten(string name, string datei) => Assert.Equal(datei, Projekt.DateiName(name));

    [Fact]
    public void Vorschlaege_MitUmlauten()
    {
        // Sammelliste 219 Punkt 3: vorgegebene Projektnamen mit Umlauten (Datei bleibt ASCII)
        Assert.Equal("Möbelhaus umbauen", Texte.Vorschlag(Texte.NeuInnenraum, "Möbelhaus"));
        Assert.Equal("Stadt 1 überarbeiten", Texte.Vorschlag(Texte.NeuStadt, null));
        Assert.Equal("Neue Stadt", Texte.Vorschlag(Texte.NeuLeer, null));
        Assert.All(new[] { "Möbelhaus umbauen", "Stadt 1 überarbeiten", "Neue Stadt" }, n => Assert.True(Projekt.NameGueltig(n)));
        var p = Projekt.Neu(Texte.Vorschlag(Texte.NeuInnenraum, D.OrtName("moebelhaus")), D, Daten.NeueAblage(), "moebelhaus");
        Assert.Equal("Möbelhaus umbauen", p.Name);
        Assert.EndsWith("Moebelhaus umbauen.fisbau", p.Datei, StringComparison.Ordinal);
    }

    [Fact]
    public void Ansicht3D_Stadt1_UndLeereStadt_ProbeLaufen()
    {
        var spiel = Ansicht3D.Bauen(D.Fassung, D, Ansicht3D.Einstellung.Grenzen);
        Assert.Equal(Festzeit.Weihnachten, spiel.Szene.Fest);
        Assert.Equal(Jahreszeit.Winter, spiel.Szene.Jahreszeit);
        Assert.NotEmpty(spiel.Szene.Haeuser);
        // Probe laufen: zur Bank gehen wie im Spiel (nur im Speicher)
        Assert.Equal(StadtGrund.Ok, spiel.GeheZu("bitweiche"));
        // leere Stadt: ohne alles und mit einer Strasse
        var p = Projekt.Neu("Leer 3D", D, Daten.NeueAblage(), leer: (20, 16));
        var leer = Ansicht3D.Bauen(p.Stand(), D, Ansicht3D.Einstellung.Standard);
        Assert.Empty(leer.Szene.Haeuser);
        p.AussenAendern("Straße", a => AussenBearbeitung.Setzen(a, AussenArt.Strasse, 10, 8));
        var mitStrasse = Ansicht3D.Bauen(p.Stand(), D, new Ansicht3D.Einstellung(true, Jahreszeit.Herbst, null));
        Assert.Single(mitStrasse.Szene.Strassen);
        Assert.True(mitStrasse.GeheZu(10.0, 8.0));
        for (var i = 0; i < 200 && mitStrasse.Fortschreiten(0.05); i++)
        {
        }
        Assert.InRange(mitStrasse.FigurX, 9.0, 11.0);
    }
}
