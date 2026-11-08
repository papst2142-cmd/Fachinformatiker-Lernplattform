using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Restwelle S 0.22.1 (Weisung P12-94, Nico 1a zu Meldung P12-62): selbst gebaute Gebaeude sperren das Speichern und Senden
/// der Stadt-Datei nicht mehr, frei stehend und als Aussehen eines Orts gleich. Der gelbe Hinweis „kommt mit dem Einbau ins
/// Spiel“ steht in beiden Faellen; rot sperrt weiter. Der Export traegt alles, was der Einbau (W4b) braucht (Bauteile,
/// Modell, Etagen), und besteht die Ladepruefung des Spiels. Das Spiel zeichnet eigene Gebaeude erst ab W4b: hier nur Nachweis.
/// </summary>
public class EigeneGebaeudeExportTests
{
    private static string Json(object? o) => PythonJson.Standard(o);

    private static Projekt Stadt1()
    {
        var p = Projekt.Neu("Eigenbau " + Guid.NewGuid().ToString("N")[..6], Daten.Spiel, Daten.NeueAblage());
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        return p;
    }

    private static Blueprint Eigenes(string name) => Entwurf.Probehaus().AlsBlueprint(name, Katalog.Bauteil, "P", "stadt1", out _);

    /// <summary>Export lesen und wie beim Einbau pruefen: Format, Inhalt, Welt des Spiels, mitgelieferte Stuecke und Etagen.</summary>
    private static StadtFassung ExportGeprueft(Projekt p)
    {
        var st = StadtFassung.Lesen(p.Exportieren(DateTimeOffset.Now));
        Assert.Equal([], StadtbauPruefung.Inhalt(st, Daten.Spiel.Inhalt));
        Assert.Equal([], SpielPruefung.Welt(st.MitKarte(Daten.Spiel.Inhalt)));
        Assert.Equal([], StadtbauPruefung.Verbaut(st, Daten.Spiel.Grafik));
        return st;
    }

    private static void MitAlterSperre(Action a)
    {
        Projekt.EigeneSperreAltFuerTests.Value = true;
        try
        {
            a();
        }
        finally
        {
            Projekt.EigeneSperreAltFuerTests.Value = false;
        }
    }

    [Fact]
    public void Ort_MitEigenemAussehen_UndEtage_GelbExportTraegtBauteileModellEtagen()
    {
        var p = Stadt1();
        var b = Eigenes("Mein Möbelhaus");
        var r = p.AussenAendern("Aussehen tauschen", a => OrtAussehen.Tauschen(a, "moebelhaus", b, Aussen.Aus(p.Basis)));
        Assert.Null(r.Grund);
        p.EtagenAendern("moebelhaus", "Etage hinzufügen", (eg, _) => EtagenBearbeitung.Hinzufuegen(eg));
        p.EtagenAendern("moebelhaus", "Treppe", (eg, _) =>
        {
            var s = Bearbeitung.Setzen(eg, Etagen.Treppe, 9, 13)!;
            var d = Bearbeitung.Deko(eg, s);
            Py.Setzen(d, "x", 8.0);
            Py.Setzen(d, "y", 12.2);
            Py.Setzen(d, "w", 3.0);
            Py.Setzen(d, "h", 1.6);
            Py.Setzen(d, "dreh", 3L);
            Assert.True(EtagenBearbeitung.ZielSetzen(eg, 0, s, 1));
        });

        // gelber Hinweis auch am Ort (bis S 0.22.0 fehlte er dort, Meldung P12-62 Abschnitt 3), kein Rot
        Assert.Equal([("moebelhaus", "Möbelhaus")], Blueprint.EigeneOrte(p.AussenStand));
        var funde = p.PruefenAussen();
        Assert.Contains(funde, f => !f.Rot && f.Text == Texte.EigenesGebaeudeNochNicht("Möbelhaus"));
        Assert.Empty(p.RoteFunde());

        // Export: Modell am Ort (das Moebelhaus ist ein neuer Ort: stadtdaten.neue_orte/neue_modelle), Bauteile, Etagen
        var st = ExportGeprueft(p);
        var ort = Liste(st.Stadtdaten["neue_orte"]).OfType<PyObjekt>().Single(o => Py.Gleich(o["id"], "moebelhaus"));
        Assert.Equal(b.Modell, Py.Str(ort["modell"]));
        var teile = Objekt(Objekt(Bauteile.Gebaeude(st.Stadtdaten))[b.Modell]);
        Assert.Equal(Json(b.Bauteile), Json(teile["teile"]));
        Assert.NotNull(Get(Objekt(st.Stadtdaten["neue_modelle"]), b.Modell));
        var eg = Objekt(Objekt(st.Innenraeume["moebelhaus"])["gebaeude"]);
        Assert.Equal(1, Etagen.Anzahl(eg));
        Assert.Single(Etagen.Uebergaenge(eg, "moebelhaus"));
        // Nachweis: die Grafik des Spiels kennt das Modell erst mit dem Einbau (W4b), das Werkzeug setzt es zusammen
        Assert.Null(Daten.Spiel.Grafik!.Modell(b.Modell));
        Assert.NotNull(Ansicht3D.MitGebaeuden(Daten.Spiel.Grafik, st)!.Modell(b.Modell));

        // Gegenprobe: die alte Regel liess den Ort ohne Hinweis grün durch, die alte Sperre galt nur frei stehend
        MitAlterSperre(() => Assert.NotEmpty(p.Exportieren(DateTimeOffset.Now)));
    }

    /// <summary>
    /// Frei stehend in Stadt 1: gelb, Export geht und laedt im Spiel. Gegenproben: alte Sperre (Export gesperrt) und alter
    /// Ablageort des Modells (stadtdaten.neue_modelle: die Ladepruefung kennt das Modell nicht, Export scheitert).
    /// </summary>
    [Fact]
    public void FreiStehend_InStadt1_GelbExportLaedt_ModellInDerKarte_RotSperrtWeiter()
    {
        var p = Stadt1();
        var b = Eigenes("Werkhalle");
        string? grund = null;
        var stelle = p.AussenAendern(Texte.BlueprintSetzen, a => b.Setzen(a, 13, 16, out grund));
        Assert.Null(grund);
        Assert.NotNull(stelle);
        Assert.Contains(p.PruefenAussen(), f => !f.Rot && f.Text == Texte.EigenesGebaeudeNochNicht("Werkhalle"));
        Assert.Empty(p.RoteFunde());
        Assert.NotNull(Get(Objekt(Aussen.Karte(p.AussenStand)["modelle"]), b.Modell));

        var st = ExportGeprueft(p);
        var welt = Objekt(st.Karte["welt"]);
        Assert.Contains(Liste(welt["landschaft"]).OfType<PyObjekt>(), g => Py.Gleich(g["typ"], "gebaeude") && Py.Gleich(g["modell"], b.Modell));
        Assert.NotNull(Get(Objekt(st.Karte["modelle"]), b.Modell));
        Assert.NotNull(Objekt(Bauteile.Gebaeude(st.Stadtdaten))[b.Modell]);
        Assert.Contains(Liste(Objekt(st.Stadtdaten["darstellung"])["zierwege"]).OfType<PyObjekt>(), z => Py.Gleich(z["modell"], b.Modell));

        // Gegenprobe alte Sperre: gesperrt
        MitAlterSperre(() => Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now)));

        // rot sperrt weiter: ein Baum mitten auf dem eigenen Gebaeude
        p.AussenAendern("Baum", a => AussenBearbeitung.Setzen(a, AussenArt.Baum, 13, 16));
        Assert.Contains(p.PruefenAussen(), f => f.Rot);
        Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now));
    }

    [Fact]
    public void Gegenprobe_ModellNurInNeueModelle_SpielLaedtDieKarteNicht()
    {
        var p = Stadt1();
        var b = Eigenes("Werkhalle");
        Blueprint.NeueModelleAltFuerTests.Value = true;
        try
        {
            p.AussenAendern(Texte.BlueprintSetzen, a => b.Setzen(a, 13, 16, out _));
        }
        finally
        {
            Blueprint.NeueModelleAltFuerTests.Value = false;
        }
        Assert.Null(Get(Objekt(Aussen.Karte(p.AussenStand)["modelle"]), b.Modell));
        var f = Assert.Throws<StadtbauFehler>(() => p.Exportieren(DateTimeOffset.Now));
        Assert.Contains(f.Funde, t => t.Contains("unbekanntes Modell 'bau:werkhalle'", StringComparison.Ordinal));
    }
}
