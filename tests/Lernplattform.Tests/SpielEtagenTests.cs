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

    // ---- Nachbesserung Treppe (Weisung P12-74) ----------------------------------------------------------------------

    /// <summary>Teile im Rechteck der Treppe (x 8–11, y 12,2–13,8): Stufen (Holz) und Gelaender (Metall) getrennt.</summary>
    private static (List<Teil> Stufen, List<Teil> Gelaender) TreppenTeile(Innenraum raum)
    {
        var drin = raum.Teile.Where(t => t.Art == TeilArt.Quader && t.X1 >= 8 - 1e-4 && t.X2 <= 11 + 1e-4 && t.Z1 >= 12.2 - 1e-4 && t.Z2 <= 13.8 + 1e-4).ToList();
        var metall = Netz.Farbe("#8E8AA3");
        var schacht = Netz.Farbe("#211E2A");
        return ([.. drin.Where(t => t.Farbe != metall && t.Farbe != schacht)], [.. drin.Where(t => t.Farbe == metall)]);
    }

    /// <summary>Groesste Durchdringung zweier Quader (alle drei Achsen ueberlappen; 0 = beruehren oder getrennt).</summary>
    private static float Durchdringung(Teil a, Teil b) =>
        Math.Max(0, new[] { Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1), Math.Min(a.Y2, b.Y2) - Math.Max(a.Y1, b.Y1), Math.Min(a.Z2, b.Z2) - Math.Max(a.Z1, b.Z1) }.Min());

    private static float GroessteDurchdringung(Innenraum raum)
    {
        var (stufen, gelaender) = TreppenTeile(raum);
        Assert.NotEmpty(stufen);
        Assert.NotEmpty(gelaender);
        return stufen.SelectMany(s => gelaender.Select(g => Durchdringung(s, g))).Max();
    }

    private static Innenraum Raum(PyObjekt innen, int etage) =>
        Innenraum.Bauen("geschaeft:moebelhaus" + (etage > 0 ? "@" + etage : ""), NeuerAblauf().Stand, innen, null, SpielGeschaefteTests.Daten.Value)!;

    /// <summary>
    /// P12-74 a: oben (Treppe nur nach unten) liegt die Treppe als Loch im Boden: keine Stufe ueber 0,2, Gelaender an drei
    /// Seiten, die Seite der Ankunft (vorn = +x) offen; unten (Erdgeschoss) und im Mittelstock die Treppe nach oben.
    /// Gegenprobe: die alte Darstellung baut oben die Stufen nach oben.
    /// </summary>
    [Fact]
    public void Treppe_Oben_FuehrtSichtbarNachUnten()
    {
        var innen = MitEtagen();
        var (stufenOben, gelaenderOben) = TreppenTeile(Raum(innen, 1));
        Assert.True(stufenOben.Max(t => t.Y2) < 0.2f, "oben ragen Stufen auf: " + stufenOben.Max(t => t.Y2));
        // Zugang vorn (+x): kein Gelaender quer an der Vorderkante x 11
        Assert.DoesNotContain(gelaenderOben, g => g.X2 > 10.9f && g.Z2 - g.Z1 > 0.5f);
        // hinten quer (x 8) und an beiden Seiten laengs
        Assert.Contains(gelaenderOben, g => g.X1 < 8.1f && g.Z2 - g.Z1 > 1.0f);
        Assert.Contains(gelaenderOben, g => g.Z1 < 12.3f && g.X2 - g.X1 > 2.0f);
        Assert.Contains(gelaenderOben, g => g.Z2 > 13.7f && g.X2 - g.X1 > 2.0f);
        var (stufenUnten, _) = TreppenTeile(Raum(innen, 0));
        Assert.True(stufenUnten.Max(t => t.Y2) > Innenraum.WandHoehe * 0.9f);
        // Gegenprobe: alte Darstellung
        Innenraum.TreppeAltFuerTests.Value = true;
        try
        {
            Assert.True(TreppenTeile(Raum(innen, 1)).Stufen.Max(t => t.Y2) > Innenraum.WandHoehe * 0.9f);
        }
        finally
        {
            Innenraum.TreppeAltFuerTests.Value = false;
        }
    }

    /// <summary>Mittelstock (Treppe EG–1 und 1–2): im 1. OG fuehrt die Treppe 1–2 nach oben, nur im 2. OG nach unten.</summary>
    [Fact]
    public void Treppe_Mittelstock_NachObenWieHeute()
    {
        var innen = MitEtagen(2, danach: eg =>
        {
            var halte = new List<object?>();
            foreach (var k in new[] { 1, 2 })
            {
                var d = Stueck(Etagen.Treppe, "u2", 3, 12.2);
                Liste(Objekt(Etagen.Gebaeude(eg, k)["flur"])["deko"]).Add(d);
                var (ax, ay, blick) = Etagen.Vor(d);
                halte.Add(new PyObjekt { { "etage", (long)k }, { "ankunft", new List<object?> { ax, ay, blick } } });
            }
            Liste(eg[Etagen.FeldUebergaenge]).Add(new PyObjekt { { "id", "u2" }, { "art", Etagen.Treppe }, { "halte", halte } });
        });
        var eg = Objekt(Objekt(innen["moebelhaus"])["gebaeude"]);
        bool Unten(int etage, string id) => Etagen.Einrichtung(Etagen.FuerDarstellung(eg, "moebelhaus", etage))
            .Any(d => Py.Gleich(Get(d, Etagen.FeldLink), id) && Py.Gleich(Get(d, Etagen.FeldRichtung), Etagen.NachUnten));
        Assert.False(Unten(0, "u1"));
        Assert.True(Unten(1, "u1"));
        Assert.False(Unten(1, "u2"));
        Assert.True(Unten(2, "u2"));
        // die gespeicherten Daten bleiben ohne das Feld (nur die Kopie fuer die Darstellung)
        Assert.DoesNotContain(Etagen.Einrichtung(Etagen.Gebaeude(eg, 1)), d => Py.Hat(d, Etagen.FeldRichtung));
    }

    /// <summary>
    /// P12-74 b: Pfosten und Gelaender stehen neben den Stufen, Durchdringung 0 (Grenze 0,02 wie SpielSitzenTests), unten
    /// und oben. Gegenprobe: die alte Lage (Pfosten in der ersten Stufe) ist rot.
    /// </summary>
    [Fact]
    public void Treppe_PfostenNebenDenStufen()
    {
        var innen = MitEtagen();
        Assert.True(GroessteDurchdringung(Raum(innen, 0)) <= 0.02f);
        Assert.True(GroessteDurchdringung(Raum(innen, 1)) <= 0.02f);
        Innenraum.TreppeAltFuerTests.Value = true;
        try
        {
            Assert.True(GroessteDurchdringung(Raum(innen, 0)) > 0.02f);
        }
        finally
        {
            Innenraum.TreppeAltFuerTests.Value = false;
        }
    }

    /// <summary>P12-74 a: die Figur geht nicht ueber das Treppenloch (Flaeche im Laufraster gesperrt), der Knopf bleibt.</summary>
    [Fact]
    public void Treppe_Oben_LochNichtBegehbar()
    {
        var spiel = ImMoebelhaus(NeuerAblauf(), MitEtagen());
        VorDieTreppe(spiel);
        Assert.True(spiel.Knopf(spiel.Knoepfe.Single(k => k.Aktion.StartsWith(Etagen.AktionEtage, StringComparison.Ordinal)).Aktion));
        Assert.Equal(1, spiel.Etage);
        spiel.GeheZu(9.5, 13.0);
        spiel.SofortAnkommen();
        Assert.False(spiel.FigurX is > 8f and < 11f && spiel.FigurZ is > 12.2f and < 13.8f, $"Figur im Treppenloch: {spiel.FigurX} {spiel.FigurZ}");
        Assert.Contains(spiel.Knoepfe, k => k.Name == "Treppe runter zum Erdgeschoss");
    }

    /// <summary>
    /// P12-74 c: oben ohne Verkaufsperson nur der Satz zu den Waren; im Erdgeschoss unveraendert (mit Verkauf). Gegenprobe:
    /// eine Person oben bringt den alten Satz zurueck.
    /// </summary>
    [Fact]
    public void Hinweis_Oben_OhneVerkauf()
    {
        var stand = NeuerAblauf().Stand;
        var innen = MitEtagen();
        Assert.Equal("Tippe auf eine Ware oder sprich mit dem Verkauf.", Raum(innen, 0).Nachricht(11.6, 13.0, stand).Text);
        Assert.Equal(Lernplattform.Core.Texte.SpielProbeTexte.EtageOhneVerkauf, Raum(innen, 1).Nachricht(11.6, 13.0, stand).Text);
        var og = Etagen.Gebaeude(Objekt(Objekt(innen["moebelhaus"])["gebaeude"]), 1);
        var mitPerson = Innenraum.AusGrundrissMitPersonen("geschaeft:moebelhaus@1", og,
            [new PyObjekt { { "id", "p" }, { "name", "Ines" }, { "rolle", "Verkauf" }, { "text", "Hallo" }, { "platz", new List<object?> { 20.0, 4.0 } } }], null);
        Assert.Equal("Tippe auf eine Ware oder sprich mit dem Verkauf.", mitPerson.Nachricht(11.6, 13.0, stand).Text);
    }
}
