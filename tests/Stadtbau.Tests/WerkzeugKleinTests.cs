using System.Numerics;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.27.0 (Weisung P12-118 Teil B, Kurzplan Meldung P12-79 Wuensche 3, 4, 5): Massband, Ebenen ein/aus, Notizen.
/// Notizen stehen nur in der Projektdatei (nie in der Stadt-Datei, nie im Spiel), jede Aenderung an ihnen ist ein Schritt;
/// das Massband ist nur Anzeige; ausgeblendete Ebenen sind nicht anwaehlbar.
/// </summary>
public class WerkzeugKleinTests
{
    private static Spieldaten D => Daten.Spiel;

    private const string Merktext = "Hier kommt der Spielplatz hin (Merktext 4711)";

    private static Projekt Leer(Werkzeugordner? ablage = null) => Projekt.Neu("Werkzeug klein", D, ablage ?? Daten.NeueAblage(), leer: (32, 24));

    private static AussenStelle NotizSetzen(Projekt p, double x, double y, string? text = null)
    {
        var s = p.AussenAendern("Notiz", a => AussenBearbeitung.Setzen(a, AussenArt.Notiz, x, y))!;
        if (text is not null)
        {
            p.AussenAendern("Text", a => AussenBearbeitung.NotizTextSetzen(a, s, text));
        }
        return s;
    }

    // ---- Notizen ----------------------------------------------------------------------------------

    [Fact]
    public void Notiz_SetzenTextSchiebenLoeschen_JeEinSchritt_RueckgaengigGeht()
    {
        var p = Leer();
        var s = NotizSetzen(p, 10.2, 6.1);
        Assert.Equal(1, p.SchritteZurueck);
        Assert.Equal(new AussenStelle(AussenArt.Notiz, 0), s);
        Assert.Equal(Texte.NotizNeu, AussenBearbeitung.NotizText(p.AussenStand, s));
        Assert.Equal((10.0, 6.0), AussenBearbeitung.Lage(p.AussenStand, s));

        Assert.Null(p.AussenAendern("Text", a => AussenBearbeitung.NotizTextSetzen(a, s, "  " + Merktext + "  ")));
        Assert.Equal(2, p.SchritteZurueck);
        Assert.Equal(Merktext, AussenBearbeitung.NotizText(p.AussenStand, s));

        p.AussenAendern("Schieben", a => { AussenBearbeitung.Verschieben(a, s, 2, -1); return 0; });
        Assert.Equal(3, p.SchritteZurueck);
        Assert.Equal((12.0, 5.0), AussenBearbeitung.Lage(p.AussenStand, s));

        p.AussenAendern("Löschen", a => AussenBearbeitung.Loeschen(a, s));
        Assert.Equal(4, p.SchritteZurueck);
        Assert.Empty(AussenBearbeitung.NotizListe(p.AussenStand));

        Assert.True(p.Rueckgaengig());
        Assert.Equal((12.0, 5.0), AussenBearbeitung.Lage(p.AussenStand, s));
        Assert.True(p.Rueckgaengig());
        Assert.True(p.Rueckgaengig());
        Assert.Equal(Texte.NotizNeu, AussenBearbeitung.NotizText(p.AussenStand, s));
        Assert.True(p.Rueckgaengig());
        Assert.Empty(AussenBearbeitung.NotizListe(p.AussenStand));
    }

    [Fact]
    public void Notiz_TextLeerOderZuLang_KeinSchritt()
    {
        var p = Leer();
        var s = NotizSetzen(p, 4, 4);
        var vorher = p.SchritteZurueck;
        Assert.Equal(Texte.NotizLeer, p.AussenAendern("Text", a => AussenBearbeitung.NotizTextSetzen(a, s, "   ")));
        Assert.Equal(Texte.NotizZuLang, p.AussenAendern("Text", a => AussenBearbeitung.NotizTextSetzen(a, s, new string('x', AussenBearbeitung.NotizHoechstens + 1))));
        Assert.Equal(vorher, p.SchritteZurueck);
        Assert.Null(p.AussenAendern("Text", a => AussenBearbeitung.NotizTextSetzen(a, s, new string('x', AussenBearbeitung.NotizHoechstens))));
    }

    [Fact]
    public void Notiz_KopierenUndBeschreibung()
    {
        var p = Leer();
        var s = NotizSetzen(p, 4, 4, Merktext);
        var kopie = p.AussenAendern("Kopie", a => AussenBearbeitung.Kopieren(a, s))!;
        Assert.Equal((5.0, 4.0), AussenBearbeitung.Lage(p.AussenStand, kopie));
        Assert.Equal(Merktext, AussenBearbeitung.NotizText(p.AussenStand, kopie));
        Assert.StartsWith(Texte.Notiz + " Nr. 2: „Hier kommt", AussenBearbeitung.Beschreibung(p.AussenStand, kopie, o => o), StringComparison.Ordinal);
        Assert.NotNull(AussenBearbeitung.DrehenNicht(s));
    }

    [Fact]
    public void Notiz_NurInDerProjektdatei_NieInDerStadtDatei()
    {
        var ablage = Daten.NeueAblage();
        var p = Leer(ablage);
        p.AussenAendern("Straße", a => AussenBearbeitung.Setzen(a, AussenArt.Strasse, 10, 8));
        NotizSetzen(p, 6, 12, Merktext);

        // Gegenprobe: die Suche findet den Text dort, wo die Notiz steht (Projektdatei)
        Assert.Contains(Merktext, File.ReadAllText(p.Datei, Encoding.UTF8), StringComparison.Ordinal);
        var wieder = Projekt.Oeffnen(p.Datei, D, ablage);
        Assert.Equal(Merktext, AussenBearbeitung.NotizText(wieder.AussenStand, new AussenStelle(AussenArt.Notiz, 0)));

        // nie in der Stadt-Datei (Export) und damit nie im Spiel
        var export = p.Exportieren(new DateTimeOffset(2026, 11, 9, 15, 0, 0, TimeSpan.FromHours(1)));
        var text = Encoding.UTF8.GetString(export);
        Assert.DoesNotContain(Merktext, text, StringComparison.Ordinal);
        Assert.DoesNotContain(AussenBearbeitung.NotizTeil, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Merktext, PythonJson.Standard(p.Stand().Stadtdaten), StringComparison.Ordinal);
        // eine Stadt-Datei als neues Projekt geoeffnet hat keine Notiz
        var ausDatei = Projekt.AusStadtDatei(export, D, ablage, "Aus der Datei");
        Assert.Empty(AussenBearbeitung.NotizListe(ausDatei.AussenStand));

        // Gegenprobe: laegen die Notizen in den Stadtdaten (falscher Ort), faende dieselbe Pruefung sie in der Stadt-Datei
        var falsch = Leer();
        falsch.AussenAendern("Straße", a => AussenBearbeitung.Setzen(a, AussenArt.Strasse, 10, 8));
        falsch.AussenAendern("Falsch", a => { Py.Setzen(Aussen.Stadtdaten(a), AussenBearbeitung.NotizTeil, Py.Kopie(AussenBearbeitung.NotizListe(p.AussenStand))); return 0; });
        Assert.Contains(Merktext, Encoding.UTF8.GetString(falsch.Exportieren(DateTimeOffset.Now)), StringComparison.Ordinal);
    }

    [Fact]
    public void Notiz_ZaehltNichtAlsAenderungDerStadt()
    {
        var p = Leer();
        NotizSetzen(p, 6, 12, Merktext);
        Assert.False(p.AussenGeaendert);
        Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now));
        // Gegenprobe: ein Baum ist eine Aenderung
        p.AussenAendern("Baum", a => AussenBearbeitung.Setzen(a, AussenArt.Baum, 4, 4));
        Assert.True(p.AussenGeaendert);
    }

    [Fact]
    public void Notiz_RuehrtDieAutomatischenLaternenNichtAn()
    {
        var p = Leer();
        p.AussenAendern("Straße", a =>
        {
            Aussen.Landschaft(a).Add(new PyObjekt
            {
                { "typ", "strasse" },
                { "punkte", new List<object?> { new List<object?> { 2.0, 8.0 }, new List<object?> { 30.0, 8.0 } } },
                { "gehweg", 0.35 },
            });
            return 0;
        });
        var laternen = PythonJson.Standard(Aussen.Laternen(p.AussenStand));
        Assert.NotEqual("[]", laternen);
        var s = NotizSetzen(p, 6, 8.5, Merktext);
        p.AussenAendern("Schieben", a => { AussenBearbeitung.Verschieben(a, s, 3, 0); return 0; });
        Assert.Equal(laternen, PythonJson.Standard(Aussen.Laternen(p.AussenStand)));
    }

    [Fact]
    public void Notiz_InDreiDNieAnwaehlbar_InDerDraufsichtOben()
    {
        var p = Leer();
        p.AussenAendern("Baum", a => AussenBearbeitung.Setzen(a, AussenArt.Baum, 10, 10));
        NotizSetzen(p, 10, 10, Merktext);
        Assert.Equal(AussenArt.Notiz, AussenBearbeitung.StueckBei(p.AussenStand, 10, 10)?.Art);
        Assert.Equal(AussenArt.Baum, AussenBearbeitung.StueckBei(p.AussenStand, 10, 10, aus: AussenBearbeitung.NurDraufsicht)?.Art);
        // 3D: Strahl senkrecht von oben auf (10, 10)
        var treffer = Bauen3D.Treffer(p.AussenStand, new Vector3(10, 20, 10), new Vector3(0, -1, 0), [], 0.3);
        Assert.Equal(AussenArt.Baum, treffer?.Art);
        Assert.Empty(Bauen3D.Umriss(p.AussenStand, new AussenStelle(AussenArt.Notiz, 0)));
    }

    // ---- Ebenen -------------------------------------------------------------------------------------

    [Fact]
    public void Ebenen_AusgeblendetesNichtAnwaehlbar_DarunterSchon()
    {
        var p = Leer();
        p.AussenAendern("Park", a => AussenBearbeitung.Setzen(a, AussenArt.Park, 12, 12));
        p.AussenAendern("Baum", a => AussenBearbeitung.Setzen(a, AussenArt.Baum, 12, 12));
        p.AussenAendern("Laterne", a => AussenBearbeitung.Setzen(a, AussenArt.Laterne, 20, 4));
        var a = p.AussenStand;
        // Gegenprobe: ohne Ausblenden greift der Baum und die Laterne
        Assert.Equal(AussenArt.Baum, AussenBearbeitung.StueckBei(a, 12, 12)?.Art);
        Assert.Equal(AussenArt.Laterne, AussenBearbeitung.StueckBei(a, 20, 4)?.Art);
        Assert.Null(AussenBearbeitung.StueckBei(a, 12, 12, aus: Ebenen.Arten([Ebene.Gruen])));
        Assert.Null(AussenBearbeitung.StueckBei(a, 20, 4, aus: Ebenen.Arten([Ebene.Laternen])));
        // Laternen aus: der Baum bleibt anwaehlbar
        Assert.Equal(AussenArt.Baum, AussenBearbeitung.StueckBei(a, 12, 12, aus: Ebenen.Arten([Ebene.Laternen]))?.Art);
    }

    [Fact]
    public void Ebenen_JedeArtGehoertZuHoechstensEinerEbene_NamenUndKennungenEindeutig()
    {
        Assert.Equal(10, Ebenen.Alle.Count); // seit S 0.42.0 mit Ebene „Gelände“
        Assert.Equal(Ebenen.Alle.Count, Ebenen.Alle.Select(Ebenen.Kennung).Distinct().Count());
        Assert.Equal(Ebenen.Alle.Count, Ebenen.Alle.Select(Ebenen.Name).Distinct().Count());
        var arten = Ebenen.Alle.SelectMany(Ebenen.ArtenVon).ToList();
        Assert.Equal(arten.Count, arten.Distinct().Count());
        Assert.All(Ebenen.Alle, e => Assert.Equal(e, Ebenen.Aus(Ebenen.Kennung(e))));
        // Strasse und Haus lassen sich nie ausblenden
        Assert.Null(Ebenen.Von(AussenArt.Strasse));
        Assert.Null(Ebenen.Von(AussenArt.Haus));
        Assert.Equal(Ebene.Wasser, Ebenen.Von(AussenArt.Steg));
        Assert.Equal(Ebene.Gelaende, Ebenen.Von(AussenArt.Huegel));
    }

    [Fact]
    public void Ebenen_GemerktInEinstellungen()
    {
        var o = Daten.NeueAblage();
        Assert.Empty(WerkzeugEinstellungen.EbenenAus(o));
        WerkzeugEinstellungen.RasterZeigenSetzen(o, false);
        WerkzeugEinstellungen.EbenenAusSetzen(o, [Ebene.Notizen, Ebene.Laternen]);
        Assert.Equal(new HashSet<Ebene> { Ebene.Laternen, Ebene.Notizen }, WerkzeugEinstellungen.EbenenAus(o).ToHashSet());
        // andere Schluessel bleiben, Reihenfolge wie die Liste
        Assert.False(WerkzeugEinstellungen.RasterZeigen(o));
        Assert.Contains("\"laternen\",", File.ReadAllText(WerkzeugEinstellungen.Datei(o)).Replace(" ", "").Replace("\n", ""), StringComparison.Ordinal);
        // unbekannte Kennung zaehlt nicht, keine Liste = alles zu sehen
        File.WriteAllText(WerkzeugEinstellungen.Datei(o), "{\"ebenen_aus\": [\"gibtsnicht\", \"funde\", 3]}");
        Assert.Equal([Ebene.Funde], WerkzeugEinstellungen.EbenenAus(o).ToList());
        File.WriteAllText(WerkzeugEinstellungen.Datei(o), "{\"ebenen_aus\": \"funde\"}");
        Assert.Empty(WerkzeugEinstellungen.EbenenAus(o));
        // kaputte Datei: nichts gemerkt, nie still ueberschrieben
        File.WriteAllText(WerkzeugEinstellungen.Datei(o), "{kaputt");
        Assert.Empty(WerkzeugEinstellungen.EbenenAus(o));
        Assert.Throws<IOException>(() => WerkzeugEinstellungen.EbenenAusSetzen(o, [Ebene.Funde]));
        Assert.Equal("{kaputt", File.ReadAllText(WerkzeugEinstellungen.Datei(o)));
    }

    // ---- Massband -----------------------------------------------------------------------------------

    [Fact]
    public void Massband_LaengeInFeldernMitEinerNachkommastelle()
    {
        Assert.Equal("3,5 Felder", Massband.Text(3.5));
        Assert.Equal("6,2 Felder", Massband.Text(6.2360679));
        Assert.Equal("1,0 Felder", Massband.Text(0.96));
        Assert.Equal("0,0 Felder", Massband.Text(0));
    }

    [Fact]
    public void Massband_RastetAnEckenUndMittenEin_SonstRaster()
    {
        var p = Leer();
        var haus = p.AussenAendern("Haus", a => AussenBearbeitung.Setzen(a, AussenArt.Gebaeude, 12, 12, modell: "filiale"))!;
        var (hx, hy, hw, hh) = AussenBearbeitung.HausFlaeche(p.AussenStand, haus);
        var a = p.AussenStand;
        // nahe der Ecke oben links: genau die Ecke
        Assert.Equal(new Vector2((float)hx, (float)hy), Massband.Fangen(a, hx + 0.12, hy - 0.1, 0.4, true));
        // nahe der Mitte der rechten Kante: genau diese Mitte
        Assert.Equal(new Vector2((float)(hx + hw), (float)(hy + (hh / 2))), Massband.Fangen(a, hx + hw + 0.2, hy + (hh / 2) + 0.1, 0.4, true));
        // weit weg von allem: Raster 0,5, ohne Einrasten 0,1
        Assert.Equal(new Vector2(3.5f, 20f), Massband.Fangen(a, 3.4, 20.2, 0.4, true));
        Assert.Equal(new Vector2(3.4f, 20.2f), Massband.Fangen(a, 3.43, 20.18, 0.4, false));
    }

    [Fact]
    public void Massband_TippAnfangTippEnde_NurAnzeige()
    {
        var p = Leer();
        p.AussenAendern("Baum", a => AussenBearbeitung.Setzen(a, AussenArt.Baum, 4, 4));
        var vorher = PythonJson.Standard(p.AussenStand);
        var schritte = p.SchritteZurueck;
        var m = new Massband();
        Assert.False(m.Tippen(p.AussenStand, 4.1, 3.9, 0.3, true));
        Assert.Equal(new Vector2(4, 4), m.Anfang);
        Assert.Null(m.Laenge);
        m.Schweben(p.AussenStand, 7, 4, 0.3, true);
        Assert.Equal(3, m.Laenge!.Value, 5);
        Assert.True(m.Tippen(p.AussenStand, 4.1, 8.1, 0.3, true));
        Assert.Equal(new Vector2(4, 8), m.Ende);
        Assert.Null(m.Schwebe);
        Assert.Equal("4,0 Felder", Massband.Text(m.Laenge!.Value));
        // der dritte Tipp beginnt neu
        Assert.False(m.Tippen(p.AussenStand, 10, 10, 0.3, true));
        Assert.Null(m.Ende);
        // nichts geaendert, kein Schritt
        Assert.Equal(vorher, PythonJson.Standard(p.AussenStand));
        Assert.Equal(schritte, p.SchritteZurueck);
    }

    [Fact]
    public void Massband_AusgeblendetesFaengtNicht()
    {
        var p = Leer();
        p.AussenAendern("Laterne", a => AussenBearbeitung.Setzen(a, AussenArt.Laterne, 10.3, 10.3));
        var a = p.AussenStand;
        var laterne = AussenBearbeitung.Lage(a, new AussenStelle(AussenArt.Laterne, 0));
        Assert.Equal(new Vector2((float)laterne.X, (float)laterne.Y), Massband.Fangen(a, laterne.X + 0.1, laterne.Y, 0.4, false));
        Assert.NotEqual(new Vector2((float)laterne.X, (float)laterne.Y), Massband.Fangen(a, laterne.X + 0.1, laterne.Y, 0.4, false, Ebenen.Arten([Ebene.Laternen])));
    }
}
