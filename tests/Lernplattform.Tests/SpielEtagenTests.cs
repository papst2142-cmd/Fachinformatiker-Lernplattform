using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 13 Welle 5 „Etagen“ (Weisung P12-73 mit P12-34, Nico 1a 2b 3a 4a): Treppe und Fahrstuhl im Spiel, die neue
/// Ereignisart „aufenthalt“, der Neustart auf der Etage, die fehlende Etage, die Fingerabdruecke und die Ladepruefung.
/// Das Moebelhaus bekommt dafuer im Test ein 1. Obergeschoss (Fassung 0 hat keine Etagen).
/// </summary>
public class SpielEtagenTests
{
    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    /// <summary>Treppe im Flur des Moebelhauses (vorn nach +x, Ankunft rechts davor).</summary>
    internal static PyObjekt Stueck(string typ, string id, double x = 8, double y = 12.2, double w = 3.0, double h = 1.6) => new()
    {
        { "typ", typ }, { "x", x }, { "y", y }, { "w", w }, { "h", h }, { "dreh", 3L }, { Etagen.FeldLink, id },
    };

    /// <summary>
    /// innenraeume.json mit Etagen im Moebelhaus: <paramref name="etagen"/> Obergeschosse (Vorschlag aus dem Erdgeschoss),
    /// eine Treppe EG ↔ 1. OG oder ein Fahrstuhl ueber alle Etagen.
    /// </summary>
    internal static PyObjekt MitEtagen(int etagen = 1, string art = Etagen.Treppe, Action<PyObjekt>? danach = null)
    {
        var innen = (PyObjekt)Py.Kopie(SpielInnenraumTests.Innenraeume.Value)!;
        var eg = Objekt(Objekt(innen["moebelhaus"])["gebaeude"]);
        var liste = new List<object?>();
        for (var k = 1; k <= etagen; k++)
        {
            liste.Add(new PyObjekt { { "gebaeude", Etagen.Vorschlag(eg) } });
        }
        Py.Setzen(eg, Etagen.FeldEtagen, liste);
        var stuecke = (art == Etagen.Treppe ? new[] { 0, 1 } : Enumerable.Range(0, etagen + 1).ToArray()).ToList();
        var halte = new List<object?>();
        var w = art == Etagen.Treppe ? 3.0 : 2.2;
        var h = art == Etagen.Treppe ? 1.6 : 2.2;
        foreach (var k in stuecke)
        {
            var d = Stueck(art, "u1", 8, art == Etagen.Treppe ? 12.2 : 12.8, w, h);
            Liste(Objekt(Etagen.Gebaeude(eg, k)["flur"])["deko"]).Add(d);
            var (ax, ay, blick) = Etagen.Vor(d);
            halte.Add(new PyObjekt { { "etage", (long)k }, { "ankunft", new List<object?> { ax, ay, blick } } });
        }
        Py.Setzen(eg, Etagen.FeldUebergaenge, new List<object?> { new PyObjekt { { "id", "u1" }, { "art", art }, { "halte", halte } } });
        danach?.Invoke(eg);
        return innen;
    }

    private static Spielablauf NeuerAblauf()
    {
        var ablauf = new Spielablauf(Inhalt);
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        return ablauf;
    }

    private static StadtSpiel Spiel(Spielablauf ablauf, PyObjekt innenraeume, string? fehlerLog = null) =>
        new(new Stadt(Inhalt, SpielStadtTests.Daten.Value), ablauf, null, innenraeume, SpielGeschaefteTests.Daten.Value, null, () => null,
            () => Jahreszeit.Sommer, null, fehlerLog);

    /// <summary>Ins Moebelhaus gehen (bis zur Oeffnung warten) und hinein.</summary>
    internal static StadtSpiel ImMoebelhaus(Spielablauf ablauf, PyObjekt innenraeume, string? fehlerLog = null)
    {
        var spiel = Spiel(ablauf, innenraeume, fehlerLog);
        var grund = spiel.GeheZu("moebelhaus");
        Assert.True(grund is StadtGrund.Ok or StadtGrund.SchonDa, grund.ToString());
        spiel.SofortAnkommen();
        if (!spiel.KannBetreten)
        {
            Assert.Equal(StadtGrund.Ok, spiel.Warten());
        }
        Assert.True(spiel.Betreten());
        Assert.Equal("geschaeft:moebelhaus", spiel.FlaecheId);
        return spiel;
    }

    /// <summary>Vor das Stueck des Uebergangs gehen (an seine Ankunft im Erdgeschoss).</summary>
    private static void VorDieTreppe(StadtSpiel spiel)
    {
        var eg = Objekt(Objekt(spiel.Innenraeume!["moebelhaus"])["gebaeude"]);
        var halt = Etagen.Von(eg, "moebelhaus", "u1")!.Auf(spiel.Etage)!;
        spiel.GeheZu(halt.X, halt.Y);
        spiel.SofortAnkommen();
    }

    [Fact]
    public void Treppe_HochUndRunter_SchreibtAufenthalt()
    {
        var ablauf = NeuerAblauf();
        var spiel = ImMoebelhaus(ablauf, MitEtagen());
        VorDieTreppe(spiel);
        var hoch = spiel.Knoepfe.Single(k => k.Aktion.StartsWith(Etagen.AktionEtage, StringComparison.Ordinal));
        Assert.Equal("Treppe hoch zum 1. Obergeschoss", hoch.Name);
        var zahl = ablauf.Stand.Verlauf.Count;
        Assert.True(spiel.Knopf(hoch.Aktion));
        Assert.Equal(1, spiel.Etage);
        Assert.Equal("geschaeft:moebelhaus@1", spiel.FlaecheId);
        Assert.Equal("moebelhaus", spiel.Ort);
        // beim Betreten im Hintergrund vorbereitet (Plan Abschnitt 5)
        Assert.Equal(1, spiel.EtageVorbereitetGenommen);
        // Figur an der Ankunft, Blick vom Stueck weg (vorn = +x, Blick 90)
        Assert.Equal((11.6f, 13.0f, 90f), (spiel.FigurX, spiel.FigurZ, spiel.FigurRichtung));
        Assert.Equal(zahl + 1, ablauf.Stand.Verlauf.Count);
        var e = ablauf.Stand.Verlauf[^1];
        Assert.Equal(Etagen.Aufenthalt, e.Art);
        Assert.Equal("{\"tag\": 1, \"stadt\": \"stadt1\", \"ort\": \"moebelhaus\", \"etage\": 1, \"uebergang\": \"u1\"}", PythonJson.Standard(e.Daten));
        // oben: hinaus geht es nicht, auch nicht ueber die Ortsliste
        spiel.Verlassen();
        Assert.True(spiel.ImInnenraum);
        Assert.Equal(Etagen.NurUnten, spiel.Zeile);
        Assert.Equal(StadtGrund.KeinWeg, spiel.GeheZu("bitweiche"));
        Assert.True(spiel.ImInnenraum);
        Assert.Equal(zahl + 1, ablauf.Stand.Verlauf.Count);
        // der Knopf zurueck steht gleich an der Ankunft
        var runter = spiel.Knoepfe.Single(k => k.Aktion.StartsWith(Etagen.AktionEtage, StringComparison.Ordinal));
        Assert.Equal("Treppe runter zum Erdgeschoss", runter.Name);
        Assert.True(spiel.Knopf(runter.Aktion));
        Assert.Equal(0, spiel.Etage);
        Assert.Equal("geschaeft:moebelhaus", spiel.FlaecheId);
        // unten wieder hinaus: Aufenthalt draussen (Etage null)
        spiel.Verlassen();
        Assert.False(spiel.ImInnenraum);
        Assert.Equal(Etagen.Aufenthalt, ablauf.Stand.Verlauf[^1].Art);
        Assert.Null(ablauf.Stand.Verlauf[^1].Daten["etage"]);
        Assert.Equal(zahl + 3, ablauf.Stand.Verlauf.Count);
    }

    [Fact]
    public void OhneEtagen_KeinKnopfUndKeinAufenthalt()
    {
        // Gegenprobe: Fassung 0 (keine Etagen) schreibt beim Betreten und Verlassen nichts Neues
        var ablauf = NeuerAblauf();
        var spiel = ImMoebelhaus(ablauf, SpielInnenraumTests.Innenraeume.Value);
        spiel.GeheZu(11.6, 13.0);
        spiel.SofortAnkommen();
        Assert.DoesNotContain(spiel.Knoepfe, k => k.Aktion.StartsWith(Etagen.AktionEtage, StringComparison.Ordinal));
        spiel.Verlassen();
        Assert.DoesNotContain(ablauf.Stand.Verlauf, e => e.Art == Etagen.Aufenthalt);
        Assert.Empty(spiel.EtagenErsatzKnoepfe());
    }

    [Fact]
    public void Neustart_StehtWiederAufDerEtage()
    {
        var ablauf = NeuerAblauf();
        var innen = MitEtagen();
        var spiel = ImMoebelhaus(ablauf, innen);
        VorDieTreppe(spiel);
        Assert.True(spiel.EtageWechseln("u1", 1));
        // Neustart: neuer StadtSpiel aus demselben Spielstand
        var neu = Spiel(ablauf, innen);
        Assert.True(neu.ImInnenraum);
        Assert.Equal(1, neu.Etage);
        Assert.Equal((11.6f, 13.0f, 90f), (neu.FigurX, neu.FigurZ, neu.FigurRichtung));
        // Gegenprobe: nach dem Verlassen steht die Figur nach dem Neustart vor dem Haus (wie bisher)
        Assert.True(neu.EtageWechseln("u1", 0));
        neu.Verlassen();
        var draussen = Spiel(ablauf, innen);
        Assert.False(draussen.ImInnenraum);
        Assert.Equal("moebelhaus", draussen.Jetzt.Ort);
        // Gegenprobe: ein Weg in der Stadt nach dem Aufenthalt zaehlt als juenger
        var zwei = ImMoebelhaus(ablauf, innen);
        VorDieTreppe(zwei);
        Assert.True(zwei.EtageWechseln("u1", 1));
        ablauf.Log(Stadt.Gewartet, new PyObjekt { { "tag", ablauf.Stand.Tag }, { "ort", "moebelhaus" }, { "minuten", 0L } });
        Assert.False(Spiel(ablauf, innen).ImInnenraum);
    }

    [Fact]
    public void Neustart_EtageFehlt_VorDemHausUndZeileImFehlerLog()
    {
        using var ordner = new TempOrdner();
        var log = Path.Combine(ordner.Pfad, "fehler.log");
        var ablauf = NeuerAblauf();
        var spiel = ImMoebelhaus(ablauf, MitEtagen());
        VorDieTreppe(spiel);
        Assert.True(spiel.EtageWechseln("u1", 1));
        // neue Fassung ohne die Etage: kein Absturz, die Figur steht vor dem Haus, eine Zeile in fehler.log
        var neu = Spiel(ablauf, SpielInnenraumTests.Innenraeume.Value, log);
        Assert.False(neu.ImInnenraum);
        Assert.Equal("moebelhaus", neu.Jetzt.Ort);
        Assert.Contains("Etage 1 in moebelhaus gibt es nicht mehr", neu.NeustartFehler, StringComparison.Ordinal);
        Assert.Contains("Etage 1 in moebelhaus gibt es nicht mehr", File.ReadAllText(log), StringComparison.Ordinal);
        // Gegenprobe: mit der Etage keine Zeile
        Assert.Null(Spiel(ablauf, MitEtagen(), log).NeustartFehler);
    }

    [Fact]
    public void Fahrstuhl_FenstermitZielen()
    {
        var ablauf = NeuerAblauf();
        var spiel = ImMoebelhaus(ablauf, MitEtagen(3, Etagen.Fahrstuhl));
        VorDieTreppe(spiel);
        var knopf = spiel.Knoepfe.Single(k => k.Aktion.StartsWith(Etagen.AktionFahrstuhl, StringComparison.Ordinal));
        Assert.Equal(Etagen.FahrstuhlKnopf, knopf.Name);
        string? gemeldet = null;
        spiel.Aktion += a => gemeldet = a;
        Assert.True(spiel.Knopf(knopf.Aktion));
        Assert.Equal("fahrstuhl:u1", gemeldet);
        Assert.Equal(0, spiel.Etage);
        Assert.Equal([(1, "1. Obergeschoss"), (2, "2. Obergeschoss"), (3, "3. Obergeschoss")], spiel.FahrstuhlZiele("u1"));
        Assert.True(spiel.EtageWechseln("u1", 3));
        Assert.Equal(3, spiel.Etage);
        Assert.Equal([(0, "Erdgeschoss"), (1, "1. Obergeschoss"), (2, "2. Obergeschoss")], spiel.FahrstuhlZiele("u1"));
        // ohne 3D: der Knopf steht unter der Ortsliste
        Assert.Equal([("fahrstuhl:u1", "Fahrstuhl")], spiel.EtagenErsatzKnoepfe());
        // Gegenprobe: eine Etage, auf der er nicht haelt, geht nicht
        Assert.False(spiel.EtageWechseln("u1", 4));
        Assert.Equal(3, spiel.Etage);
    }

    [Fact]
    public void Zug_IstUnbekannt_KeinWechsel()
    {
        // Spaeter faehrt der Zug ueber dieselbe Form; bis Beta 1 ist die Art "zug" unbekannt (Gegenprobe: Treppe geht)
        var innen = MitEtagen(danach: eg => Py.Setzen(Objekt(Liste(eg[Etagen.FeldUebergaenge])[0]), "art", "zug"));
        var ablauf = NeuerAblauf();
        var spiel = ImMoebelhaus(ablauf, innen);
        VorDieTreppe(spiel);
        Assert.DoesNotContain(spiel.Knoepfe, k => k.Aktion.StartsWith(Etagen.AktionEtage, StringComparison.Ordinal));
        Assert.False(spiel.EtageWechseln("u1", 1));
        Assert.Contains(StadtbauPruefung.EtagenAlle(innen, null), s => s.Contains("„zug“ ist keine bekannte Art", StringComparison.Ordinal));
        Assert.DoesNotContain(StadtbauPruefung.EtagenAlle(MitEtagen(), null), s => s.Contains("keine bekannte Art", StringComparison.Ordinal));
    }

    [Fact]
    public void Ladepruefung_TestEtagenGruen_GroessereEtageRot()
    {
        Assert.Empty(StadtbauPruefung.EtagenAlle(MitEtagen(), null));
        { var f = StadtbauPruefung.EtagenAlle(MitEtagen(3, Etagen.Fahrstuhl), null); if (f.Count > 0) Assert.Fail(string.Join("\n", f)); }
        Assert.Empty(StadtbauPruefung.EtagenAlle(SpielInnenraumTests.Innenraeume.Value, null));
        var gross = MitEtagen(danach: eg => Py.Setzen(Etagen.Gebaeude(eg, 1), "breite", Py.F(eg["breite"]) + 1));
        Assert.Contains(StadtbauPruefung.EtagenAlle(gross, null), s => s.Contains("größer als das Erdgeschoss", StringComparison.Ordinal));
    }

    [Fact]
    public void Obergeschoss_OhneEingangstuer()
    {
        var innen = MitEtagen();
        var eg = Innenraum.Bauen("geschaeft:moebelhaus", NeuerAblauf().Stand, innen, null, SpielGeschaefteTests.Daten.Value)!;
        var og = Innenraum.Bauen("geschaeft:moebelhaus@1", NeuerAblauf().Stand, innen, null, SpielGeschaefteTests.Daten.Value)!;
        Assert.Equal("moebelhaus", og.Ort);
        Assert.Empty(og.Personen);
        Assert.NotEmpty(eg.Personen);
        // die Aussenwand links ist oben geschlossen: mehr Wandstuecke ohne Tueroeffnung? nein: gleich viele Teile, aber die
        // Wand links reicht ohne Luecke durch (eine Tuer teilt sie im Erdgeschoss)
        Assert.Null(Innenraum.Bauen("geschaeft:moebelhaus@2", NeuerAblauf().Stand, innen, null, SpielGeschaefteTests.Daten.Value));
        Assert.Equal("moebelhaus", Innenraum.OrtFuer("geschaeft:moebelhaus@3"));
        Assert.Null(Innenraum.OrtFuer("zuhause@1"));
    }

    /// <summary>
    /// Nico 2b: der Aufenthalt aendert am Spielstand nur die Historie. G43 (f2b59ce8) mit eingestreuten Aufenthalten: gleiche
    /// Einzelwerte, ohne die Aufenthalte derselbe Hash; Gegenprobe: mit ihnen ein anderer Hash.
    /// </summary>
    [Fact]
    public void Fingerabdruck_G43_BleibtMitAufenthalten()
    {
        var original = SpielFingerabdruckTests.Gespielt.Value.Stand;
        var mit = new List<SpielEreignis>();
        foreach (var e in original.Verlauf)
        {
            mit.Add(e);
            if (e.Art == Ereignisse.Erledigt && e.Daten["tag"] is long tag)
            {
                mit.Add(new SpielEreignis(e.Zeit, Etagen.Aufenthalt, Etagen.AufenthaltDaten(tag, "stadt1", "moebelhaus", 1, "u1")));
            }
        }
        var stand = new Spielstand(mit, Inhalt);
        Assert.True(mit.Count > original.Verlauf.Count + 10);
        Assert.Equal(["history"], stand.Unterschiede(original));
        Assert.Equal(PythonJson.Text(SpielFingerabdruckTests.Werte(original)), PythonJson.Text(SpielFingerabdruckTests.Werte(stand)));
        var ohne = new Spielstand(stand.Verlauf.Where(e => e.Art != Etagen.Aufenthalt), Inhalt);
        Assert.Equal(SpielFingerabdruckTests.SollHash, SpielFingerabdruckTests.Hash(ohne));
        Assert.NotEqual(SpielFingerabdruckTests.SollHash, SpielFingerabdruckTests.Hash(stand));
    }

    /// <summary>Die Stadt-Uhr (zweiter Fingerabdruck 03d0a012) sieht Aufenthalte nicht: gleicher Stand mit und ohne.</summary>
    [Fact]
    public void StadtUhr_IgnoriertAufenthalte()
    {
        var (spiel, _) = SpielStadtTests.StadtSzenario();
        var stadt = new Stadt(Inhalt, SpielStadtTests.Daten.Value);
        var mit = new List<SpielEreignis>();
        foreach (var e in spiel.Stand.Verlauf)
        {
            mit.Add(e);
            if (e.Art == Stadt.Gegangen)
            {
                mit.Add(new SpielEreignis(e.Zeit, Etagen.Aufenthalt, Etagen.AufenthaltDaten((long)e.Daten["tag"]!, "stadt1", "moebelhaus", 2, "u1")));
            }
        }
        var stand = new Spielstand(mit, Inhalt);
        Assert.Equal(stadt.Stand(spiel.Stand), stadt.Stand(stand));
        Assert.Equal(PythonJson.Text(SpielFingerabdruckTests.Werte(spiel.Stand)), PythonJson.Text(SpielFingerabdruckTests.Werte(stand)));
    }
}
