using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// S 0.9.0 (Weisung P12-48): Blueprint-Format 2 mit den Feldern <c>bauteile</c> und <c>eingang</c> (selbst gebaute
/// Gebaeude aus dem Editor). Format 1 bleibt lesbar (echte Datei aus S 0.8.0). Setzen schreibt das Gebaeude nach
/// <c>stadtdaten.bauteile</c>, die 3D-Ansicht zeichnet es, der Export bleibt gelb gesperrt bis zum Einbau (W4b).
/// </summary>
public class BlueprintEntwurfTests
{
    private static readonly DateTime Zeit = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Local);

    private const string Tuer = Bauteile.Praefix + "building-door";
    private const string Fenster = Bauteile.Praefix + "building-window";
    private const string Dach = Bauteile.Praefix + "roof-flat-center";

    private static string Json(object? o) => PythonJson.Standard(o);

    /// <summary>Kopie mit anderen Bauteilen bzw. anderem Innenraum (Blueprint ist keine record-Klasse).</summary>
    private static Blueprint Mit(Blueprint b, bool ohneBauteile = false, PyObjekt? innenraum = null) => new()
    {
        Name = b.Name,
        Herkunft = b.Herkunft,
        Modell = b.Modell,
        Teile = b.Teile,
        Tuer = b.Tuer,
        Weg = b.Weg,
        Schild = b.Schild,
        Innenraum = innenraum ?? b.Innenraum,
        Personen = b.Personen,
        Bauteile = ohneBauteile ? null : b.Bauteile,
        Eingang = b.Eingang,
    };

    /// <summary>Haus 3 x 2 mit Tuer vorne in der Mitte, Fenstern und Flachdach.</summary>
    private static Entwurf Haus()
    {
        var e = new Entwurf();
        for (var x = 0; x < 3; x++)
        {
            for (var y = 0; y < 2; y++)
            {
                e.Setzen(x == 1 && y == 1 ? Tuer : Fenster, x, y, 0, y == 1 ? 0 : 2);
                e.Setzen(Dach, x, y, 1, 0);
            }
        }
        Assert.True(e.EingangFestlegen(e.Teile.FindIndex(t => t.Id == Tuer)));
        Assert.DoesNotContain(e.Pruefen(Katalog.Bauteil), f => f.Rot);
        return e;
    }

    [Fact]
    public void Format2_SpeichernLesen_GleicheTeileUndEingang_WeiterbauenGleich()
    {
        var ablage = Daten.NeueAblage();
        var e = Haus();
        var b = e.AlsBlueprint("Mein Haus", Katalog.Bauteil, "Projekt", "neu_x", out var neu);
        Assert.True(neu);
        Assert.Equal("bau:mein_haus", b.Modell);
        var pfad = b.Speichern(ablage, Zeit);
        var text = File.ReadAllText(pfad);
        Assert.Contains("\"formatversion\": 2", text, StringComparison.Ordinal);
        var gelesen = Blueprint.Lesen(File.ReadAllBytes(pfad));
        Assert.Equal(Json(b.Bauteile), Json(gelesen.Bauteile));
        Assert.Equal(Json(b.Eingang), Json(gelesen.Eingang));
        var vorlage = Assert.Single(Blueprint.Alle(ablage)).Vorlage!;
        Assert.Equal(pfad, vorlage.Datei);
        var weiter = Entwurf.Aus(vorlage)!;
        Assert.Equal(pfad, weiter.Datei);
        Assert.Equal(e.Teile, weiter.Teile);
        Assert.Equal(e.Eingaenge, weiter.Eingaenge);
        Assert.Equal("Mein Haus", weiter.Name);
        Assert.Equal("bau:mein_haus", weiter.Modell);
        // Tuer liegt an der Aussenkante des Eingangs (Sued, Feld 1,1), der Weg fuehrt nach draussen
        var gm = Haus().Modell3D("x", Katalog.Bauteil);
        Assert.Equal(1.5, Py.F(b.Tuer![0]), 6);
        Assert.Equal(gm.Max.Z, Py.F(b.Tuer[1]), 4);
        Assert.InRange(Py.F(b.Tuer[1]), 2.0, 2.1);
        Assert.True(Py.F(Liste(b.Weg[^1])[1]) > 2.0);
        // das Modell im Blueprint ist EIN Block um das ganze Gebaeude
        Assert.Single(b.Teile);
    }

    [Fact]
    public void Format1_EchteDateiAusS080_BleibtLesbar_OhneBauteile()
    {
        var pfad = Path.Combine(Daten.Wurzel, "tests", "Stadtbau.Tests", "Testdaten", "blueprint_S0.8.0.fisblueprint");
        var text = File.ReadAllText(pfad);
        Assert.Contains("\"formatversion\": 1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"bauteile\"", text, StringComparison.Ordinal);
        var b = Blueprint.Lesen(File.ReadAllBytes(pfad));
        Assert.Equal("Möbelhaus aus S080", b.Name);
        Assert.Null(b.Bauteile);
        Assert.Null(b.Eingang);
        // kein selbst gebautes Gebaeude: der Editor kann ihn nicht weiterbauen
        Assert.Null(Entwurf.Aus(b));
        // in der Liste steht er ohne Hinweis
        var ablage = Daten.NeueAblage();
        File.Copy(pfad, Path.Combine(ablage.Blueprints, Path.GetFileName(pfad)));
        var eintrag = Assert.Single(Blueprint.Alle(ablage));
        Assert.Null(eintrag.Fehler);
        Assert.NotNull(eintrag.Vorlage);
    }

    [Fact]
    public void Format2_NurEinFeldDerBeiden_IstKaputt()
    {
        var b = Haus().AlsBlueprint("Haus", Katalog.Bauteil, "P", "s", out _);
        var text = System.Text.Encoding.UTF8.GetString(b.Schreiben());
        // eingang ohne bauteile (beide oder keins): Pruefsumme neu, damit nur die Regel faellt
        var ohne = Mit(b, ohneBauteile: true).Schreiben();
        Assert.Throws<StadtbauFehler>(() => Blueprint.Lesen(ohne));
        Assert.NotNull(Blueprint.Lesen(System.Text.Encoding.UTF8.GetBytes(text)).Bauteile);
    }

    [Fact]
    public void AlsBlueprint_MitRotemFund_GehtNicht()
    {
        var e = Haus();
        e.Eingaenge.Clear();
        var f = Assert.Throws<StadtbauFehler>(() => e.AlsBlueprint("Haus", Katalog.Bauteil, "P", "s", out _));
        Assert.Contains(Texte.EntwurfKeinEingang, f.Funde);
    }

    [Fact]
    public void Weiterbauen_GleicheGrundflaeche_BehaeltInnenraum_SonstNeuerVorschlag()
    {
        var ablage = Daten.NeueAblage();
        var b = Haus().AlsBlueprint("Haus", Katalog.Bauteil, "P", "s", out _);
        // der Innenraum wurde bearbeitet (anderer Boden im Flur)
        var innen = (PyObjekt)Py.Kopie(b.Innenraum)!;
        Py.Setzen(Objekt(innen["flur"])!, "boden", "holz");
        b = Mit(b, innenraum: innen);
        b.Speichern(ablage, Zeit);
        var weiter = Entwurf.Aus(Blueprint.Alle(ablage).Single().Vorlage!)!;
        // ein Dachteil mehr: Grundflaeche gleich -> Innenraum bleibt
        weiter.Setzen(Bauteile.Praefix + "detail-ac-a", 0, 0, 1, 0);
        var gleich = weiter.AlsBlueprint("Haus", Katalog.Bauteil, "P", "s", out var neu1);
        Assert.False(neu1);
        Assert.Equal(Json(innen), Json(gleich.Innenraum));
        Assert.Equal(b.Modell, gleich.Modell);
        // breiter: neuer Vorschlag
        weiter.Setzen(Fenster, 3, 1, 0, 0);
        weiter.Setzen(Dach, 3, 1, 1, 0);
        var breiter = weiter.AlsBlueprint("Haus", Katalog.Bauteil, "P", "s", out var neu2);
        Assert.True(neu2);
        Assert.NotEqual(Json(innen), Json(breiter.Innenraum));
        // Ersetzen: die alte Fassung kommt vorher in die Sicherungen
        var datei = Blueprint.Alle(ablage).Single().Datei;
        breiter.Ersetzen(ablage, datei, Zeit.AddMinutes(1));
        Assert.Equal(Json(breiter.Bauteile), Json(Blueprint.Lesen(File.ReadAllBytes(datei)).Bauteile));
        Assert.True(Directory.GetFiles(ablage.Sicherungen, "*", SearchOption.AllDirectories).Count(f => f.Contains("Haus", StringComparison.Ordinal)) >= 3);
    }

    [Fact]
    public void Setzen_InLeereStadt_SchreibtBauteile_3DKenntDasModell_ExportGelbGesperrt()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Eigenbau", Daten.Spiel, ablage, leer: (32, 24));
        p.AussenAendern("Straße", a =>
        {
            var s = AussenBearbeitung.Setzen(a, AussenArt.Strasse, 4, 12)!;
            AussenBearbeitung.LaengeSetzen(a, s, 28);
        });
        var b = Haus().AlsBlueprint("Eigenes Haus", Katalog.Bauteil, p.Name, p.Basis.StadtId, out _);
        string? grund = null;
        var stelle = p.AussenAendern(Texte.BlueprintSetzen, a => b.Setzen(a, 10, 8.5, out grund));
        Assert.Null(grund);
        Assert.NotNull(stelle);
        var a = p.AussenStand;
        var eintrag = Assert.IsType<PyObjekt>(Bauteile.Gebaeude(Aussen.Stadtdaten(a))["bau:eigenes_haus"]);
        Assert.Equal(Json(b.Bauteile), Json(eintrag["teile"]));
        // 3D: die Grafik kennt das zusammengesetzte Modell
        var g = Ansicht3D.MitGebaeuden(Daten.Spiel.Grafik, Aussen.Fassung(a, p.Basis, new PyObjekt(), 0, null, "", "", ""))!;
        Assert.Equal(Bauteile.Dreiecke(Haus().Teile, Katalog.Bauteil), g.Modell("bau:eigenes_haus")!.Dreiecke);
        // Pruefung draussen: gelber Hinweis, kein roter Fund; der Export ist gesperrt
        var funde = p.PruefenAussen();
        Assert.Contains(funde, f => !f.Rot && f.Text == Texte.EigenesGebaeudeNochNicht("Eigenes Haus"));
        Assert.DoesNotContain(funde, f => f.Rot);
        var fehler = Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now));
        Assert.Contains(fehler.Funde, t => t.EndsWith(Texte.EigenesGebaeudeNochNicht("Eigenes Haus"), StringComparison.Ordinal));
        // Gegenprobe: Gebaeude wieder weg -> Export geht
        p.AussenAendern("Löschen", x => AussenBearbeitung.Loeschen(x, stelle!));
        Assert.Empty(Blueprint.EigeneGebaeude(p.AussenStand));
        Assert.NotEmpty(p.Exportieren(DateTimeOffset.Now));
    }
}
