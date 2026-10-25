using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W5 (Weisung P7-10): Innenraeume in 3D aus dem Grundriss der Python-Linie (Buero,
/// Wohnung, Kunden, Filiale) und aus innenraeume.json (Serverfarm), Wechsel Stadt und Innenraum,
/// Einrichten der Wohnung ueber die Aktionen des Spiel-Kerns, Haus halb durchsichtig (Sammelliste 102).
/// Feste C#-Faelle (die Wege und Texte selbst pruefen G48/G49).
/// </summary>
public class SpielInnenraumTests
{
    internal static readonly Lazy<PyObjekt> Innenraeume = new(() =>
        (PyObjekt)Py.Lesen(File.ReadAllBytes(Path.Combine(Pfade.Wurzel, "inhalte_csharp", "spiel", "innenraeume.json")))!);

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static Spielablauf NeuerAblauf()
    {
        var ablauf = new Spielablauf(Inhalt);
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        return ablauf;
    }

    private static StadtSpiel NeuesSpiel(Spielablauf? ablauf = null) =>
        new(new Stadt(Inhalt, SpielStadtTests.Daten.Value), ablauf ?? NeuerAblauf(), null, Innenraeume.Value);

    [Theory]
    [InlineData("buero", "buero")]
    [InlineData("zuhause", "zuhause")]
    [InlineData("filiale", "filiale")]
    [InlineData("serverfarm", "serverfarm")]
    [InlineData("kunde:talheim", "talheim")]
    [InlineData("schalter:bank", "bank")]
    [InlineData("schalter:amt", "amt")]
    [InlineData("schalter:kino", null)]
    [InlineData("mitbewerber:bitweiche", null)]
    [InlineData("firma", null)]
    [InlineData("firma:gebaeude", null)]
    [InlineData(null, null)]
    public void AnsichtZuOrt(string? ansicht, string? ort) => Assert.Equal(ort, Innenraum.OrtFuer(ansicht));

    /// <summary>Jeder Innenraum eines neuen Spielstands: Groesse und Personen wie der Python-Grundriss, Start begehbar.</summary>
    [Fact]
    public void JederInnenraumWieDerGrundriss()
    {
        var stand = NeuerAblauf().Stand;
        var ansichten = new List<string> { "buero", "zuhause", "serverfarm" };
        ansichten.AddRange(SpielDaten.Kundenorte(Inhalt).Select(k => "kunde:" + Py.Str(k["id"])));
        foreach (var ansicht in ansichten)
        {
            var innen = Innenraum.Bauen(ansicht, stand, Innenraeume.Value, SpielGrafikTests.Bibliothek.Value);
            Assert.NotNull(innen);
            Assert.Equal(ansicht, innen!.Ansicht);
            Assert.Equal((float)Py.F(innen.Gebaeude["breite"]), innen.Breite);
            Assert.Equal((float)Py.F(innen.Gebaeude["hoehe"]), innen.Tiefe);
            Assert.True(Wege.KannStehen(innen.StartX, innen.StartZ, innen.Lauf), ansicht + ": Start nicht begehbar");
            if (ansicht != "serverfarm")
            {
                var soll = Innenraum.OrtFuer(ansicht) == SpielDaten.OrtZuhause ? [] : SpielOrte.Personen(Innenraum.OrtFuer(ansicht)!, stand);
                Assert.Equal(soll.Select(p => Py.Str(p["id"])), innen.Personen.Select(p => p.Id));
            }
            foreach (var p in innen.Personen)
            {
                // jede Person ist erreichbar (bis auf Reichweite heran) oder steht schon in Reichweite
                var nah = Math.Sqrt(Math.Pow(p.X - innen.StartX, 2) + Math.Pow(p.Z - innen.StartZ, 2)) <= Wege.Reichweite;
                Assert.True(nah || innen.Weg(innen.StartX, innen.StartZ, p.X, p.Z, Wege.Reichweite).Count > 0, ansicht + ": " + p.Id + " nicht erreichbar");
            }
            var netz = innen.NetzBauen();
            Assert.True(netz.AnzahlEcken > 500, ansicht);
            Assert.All(netz.Ecken, e => Assert.True(float.IsFinite(e)));
            // jedes Einrichtungsstueck hat Geometrie in seinem Rechteck
            foreach (var d in Deko(innen.Gebaeude))
            {
                Assert.True(HatGeometrie(innen, d), ansicht + ": " + Py.Str(d["typ"]) + " ohne Geometrie");
            }
        }
    }

    private static IEnumerable<PyObjekt> Deko(PyObjekt gebaeude) =>
        SpielDaten.Liste(gebaeude["raeume"]).Select(SpielDaten.Objekt).SelectMany(r => SpielDaten.ListeOderLeer(r, "deko"))
            .Concat(SpielDaten.ListeOderLeer(SpielDaten.Objekt(gebaeude["flur"]), "deko")).Select(SpielDaten.Objekt);

    private static bool HatGeometrie(Innenraum innen, PyObjekt d)
    {
        float x = (float)Py.F(d["x"]) - 0.05f, z = (float)Py.F(d["y"]) - 0.05f;
        float x2 = x + (float)Py.F(d["w"]) + 0.1f, z2 = z + (float)Py.F(d["h"]) + 0.1f;
        bool Drin(float px, float pz) => x <= px && px <= x2 && z <= pz && pz <= z2;
        return innen.Teile.Any(t => t.Y2 > 0.045f && Drin((t.X1 + t.X2) / 2, (t.Z1 + t.Z2) / 2))
            || innen.Stuecke.Any(s => Drin(s.Lage.M41, s.Lage.M43) || StueckMitte(innen, s) is { } m && Drin(m.X, m.Y));
    }

    private static Vector2? StueckMitte(Innenraum innen, ModellStueck s)
    {
        var gm = innen.Grafik!.Modell(s.Name)!;
        var m = Vector3.Transform((gm.Min + gm.Max) / 2, s.Lage);
        return new Vector2(m.X, m.Z);
    }

    /// <summary>Jedes Moebel-Modell der Liste wird von einem Einrichtungstyp gebraucht (und umgekehrt).</summary>
    [Fact]
    public void JederTypHatSeinModell()
    {
        var typen = new HashSet<string>();
        foreach (var g in new[] { SpielDaten.Objekt(Inhalt["gebaeude"]), SpielDaten.Objekt(SpielDaten.Objekt(Innenraeume.Value["serverfarm"])["gebaeude"]) }
            .Concat(SpielDaten.Kundenorte(Inhalt).Select(k => SpielDaten.Objekt(k["gebaeude"])))
            .Concat(SpielDaten.Liste(SpielDaten.Objekt(Inhalt["wohnungen"])["wohnungen"]).Select(w => SpielDaten.Objekt(SpielDaten.Objekt(w)["gebaeude"]))))
        {
            typen.UnionWith(Deko(g).Select(d => Py.Str(d["typ"])));
        }
        var moebel = SpielDaten.Liste(SpielDaten.Objekt(Inhalt["wohnungen"])["moebel"]).Select(SpielDaten.Objekt).ToList();
        var deko = new List<object?>();
        var x = 0.5;
        foreach (var typ in typen)
        {
            deko.Add(new PyObjekt { { "typ", typ }, { "x", x }, { "y", 1.0 }, { "w", 2.0 }, { "h", 1.0 } });
            x += 3;
        }
        foreach (var m in moebel)
        {
            deko.Add(new PyObjekt { { "typ", m["typ"] }, { "x", x }, { "y", 1.0 }, { "w", m["w"] }, { "h", m["h"] }, { "dreh", 0L } });
            x += 4;
        }
        var gebaeude = new PyObjekt
        {
            { "breite", x + 2 }, { "hoehe", 10.0 },
            { "flur", new PyObjekt { { "y", 6.0 }, { "h", 2.0 }, { "eingang", new PyObjekt { { "von", 6.3 }, { "bis", 7.7 } } }, { "spieler", new List<object?> { 1.2, 7.0 } } } },
            { "raeume", new List<object?> { new PyObjekt { { "id", "a" }, { "name", "A" }, { "x", 0.0 }, { "y", 0.0 }, { "w", x + 2 }, { "h", 6.0 }, { "tuer", new PyObjekt { { "von", 1.0 }, { "bis", 2.4 } } }, { "deko", deko } } } },
        };
        var innen = Innenraum.AusGrundriss("buero", gebaeude, SpielGrafikTests.Bibliothek.Value);
        var genutzt = innen.Stuecke.Select(s => s.Name).ToHashSet();
        Assert.Equal(Innenraum.Modelle.OrderBy(n => n, StringComparer.Ordinal), genutzt.OrderBy(n => n, StringComparer.Ordinal));
        foreach (var d in deko.Select(SpielDaten.Objekt))
        {
            Assert.True(HatGeometrie(innen, d), Py.Str(d["typ"]) + " ohne Geometrie");
        }
        // ohne Grafik-Bibliothek: eigene Kloetze an denselben Stellen
        var ohne = Innenraum.AusGrundriss("buero", gebaeude, null);
        Assert.Empty(ohne.Stuecke);
        Assert.True(ohne.Teile.Count > innen.Teile.Count);
    }

    /// <summary>Die Serverfarm aus innenraeume.json: alle Raeume ueber Tueren erreichbar, die drei Personen auf ihren Plaetzen.</summary>
    [Fact]
    public void ServerfarmBegehbarMitPersonen()
    {
        var innen = Innenraum.Bauen(Innenraum.Serverfarm, NeuerAblauf().Stand, Innenraeume.Value, null)!;
        Assert.Equal(["arendt", "brauer", "ruppert"], innen.Personen.Select(p => p.Id));
        Assert.Equal((23.7f, 10.7f), (innen.Personen[0].X, innen.Personen[0].Z));
        foreach (var raum in SpielDaten.Liste(innen.Gebaeude["raeume"]).Select(SpielDaten.Objekt))
        {
            var tuer = SpielDaten.Objekt(raum["tuer"]);
            var mitteTuer = (Py.F(tuer["von"]) + Py.F(tuer["bis"])) / 2;
            var innenY = Py.F(raum["y"]) + (Py.F(raum["y"]) < 6 ? Py.F(raum["h"]) - 1 : 1);
            Assert.True(innen.Weg(innen.StartX, innen.StartZ, mitteTuer, innenY).Count > 0, Py.Str(raum["id"]) + " nicht erreichbar");
        }
        foreach (var p in innen.Personen)
        {
            // Personen stehen nicht in Waenden oder Einrichtung (Basis-Raster wie _base_grid)
            Assert.Contains(Wege.Zelle(p.X, p.Z), innen.Lauf.Basis);
        }
        var (titel, text, knoepfe) = innen.Nachricht(innen.StartX, innen.StartZ, NeuerAblauf().Stand);
        Assert.Equal("Du bist im Flur.", titel);
        Assert.False(string.IsNullOrEmpty(text));
        Assert.Empty(knoepfe);
        var (titelPerson, _, _) = innen.Nachricht(12.5, 5.0, NeuerAblauf().Stand);
        Assert.Equal("Konstantin Brauer · Leiter Rechenzentrum", titelPerson);
    }

    /// <summary>Gespielter Stand mit Firma und Filiale: Firmenbuero und Filiale mit den Mitarbeitern als Personen.</summary>
    [Fact]
    public void FirmenbueroUndFiliale()
    {
        var stand = SpielFingerabdruckTests.Gespielt.Value.Stand;
        foreach (var ansicht in new[] { "buero", "filiale" })
        {
            var innen = Innenraum.Bauen(ansicht, stand, Innenraeume.Value, null);
            Assert.NotNull(innen);
            var ort = Innenraum.OrtFuer(ansicht)!;
            Assert.Equal(SpielOrte.FirmenPersonen(stand, ort).Select(p => Py.Str(p["id"])), innen!.Personen.Select(p => p.Id));
            Assert.Equal(SpielOrte.Gebaeude(ort, stand)["breite"], innen.Gebaeude["breite"]);
        }
        // neuer Spielstand: keine Filiale
        Assert.Null(Innenraum.Bauen("filiale", NeuerAblauf().Stand, Innenraeume.Value, null));
    }

    /// <summary>Hinein, Text wie home_message, "Zur Arbeit" geht hinaus zum Buero und gleich hinein.</summary>
    [Fact]
    public void WohnungZurArbeitUndInsBuero()
    {
        var spiel = NeuesSpiel();
        Assert.True(spiel.KannBetreten);
        Assert.True(spiel.Betreten());
        Assert.Equal("zuhause", spiel.FlaecheId);
        Assert.Equal(SpielDaten.OrtZuhause, spiel.Ort);
        // am Start steht die Figur an der Wohnungstuer
        Assert.StartsWith("Wohnungstür\nVon hier geht es zur Arbeit.", spiel.Zeile);
        Assert.Equal([("buero", "Zur Arbeit")], spiel.Knoepfe);
        var uhr = spiel.Jetzt.Uhr;
        Assert.True(spiel.Knopf("buero"));
        Assert.False(spiel.ImInnenraum);
        Assert.True(spiel.Geht);
        spiel.SofortAnkommen();
        Assert.Equal("bitweiche", spiel.Jetzt.Ort);
        Assert.True(spiel.Jetzt.Uhr > uhr);
        Assert.True(spiel.ImInnenraum);
        Assert.Equal("buero", spiel.FlaecheId);
        Assert.False(spiel.Knopf("feierabend"));
        // zu einer Person gehen: bis auf Reichweite heran, dann ihr Text
        var person = spiel.Innen!.Personen[0];
        Assert.True(spiel.GeheZu(person.X, person.Z));
        spiel.SofortAnkommen();
        Assert.True(Math.Sqrt(Math.Pow(spiel.FigurX - person.X, 2) + Math.Pow(spiel.FigurZ - person.Z, 2)) <= Wege.Reichweite + 0.2);
        Assert.True(spiel.Zeile.Contains(person.Name, StringComparison.Ordinal) || spiel.Zeile.Contains(Py.Str(Betrieb.KurzName(person.Name)), StringComparison.Ordinal), spiel.Zeile);
    }

    /// <summary>Einrichten: waehlen, in den Karton, mit Vorschau aufstellen, drehen; jede Aenderung ein Ereignis.</summary>
    [Fact]
    public void EinrichtenStelltMoebelUm()
    {
        var spiel = NeuesSpiel();
        Assert.True(spiel.Betreten());
        Assert.True(spiel.KannEinrichten);
        spiel.EinrichtenUmschalten();
        Assert.True(spiel.Einrichten);
        Assert.Empty(spiel.Knoepfe);
        Assert.Empty(spiel.ImKarton());
        // die Startmatratze steht bei (0,4 | 0,4): antippen waehlt sie
        Assert.True(spiel.GeheZu(1.0, 1.0));
        Assert.Equal("start-matratze", spiel.Ausgewaehlt);
        Assert.True(spiel.AusgewaehltSteht);
        var ereignisse = spiel.Ablauf.Stand.Verlauf.Count;
        Assert.True(spiel.InDenKarton());
        Assert.Equal(ereignisse + 1, spiel.Ablauf.Stand.Verlauf.Count);
        Assert.Equal(Ereignisse.Einrichtung, spiel.Ablauf.Stand.Verlauf[^1].Art);
        Assert.False(spiel.AusgewaehltSteht);
        Assert.Equal(["start-matratze"], spiel.ImKarton().Select(k => k.Stueck));
        // auf den Boden tippen: Vorschau, die passt; zweites Tippen darauf stellt auf
        Assert.True(spiel.GeheZu(5.0, 3.0));
        Assert.NotNull(spiel.Vorschau);
        Assert.Equal("gruen", spiel.VorschauFarbe);
        Assert.EndsWith("Passt. Klicken oder loslassen zum Aufstellen.", spiel.Zeile);
        var v = spiel.Vorschau!;
        Assert.True(spiel.GeheZu(Py.F(v["x"]) + 0.1, Py.F(v["y"]) + 0.1));
        Assert.Null(spiel.Vorschau);
        Assert.True(spiel.AusgewaehltSteht);
        Assert.Empty(spiel.ImKarton());
        var platz = Wohnung.AufgestellteMoebel(spiel.Ablauf.Stand).Single(m => m.Stueck == "start-matratze");
        Assert.Equal((Py.F(v["x"]), Py.F(v["y"])), (platz.X, platz.Y));
        // drehen: eine Vierteldrehung
        Assert.True(spiel.Drehen());
        Assert.Equal(1, Wohnung.AufgestellteMoebel(spiel.Ablauf.Stand).Single(m => m.Stueck == "start-matratze").Dreh);
        // die 3D-Einrichtung folgt dem Spielstand
        Assert.Contains("matratze@", spiel.Innen!.Signatur);
        spiel.EinrichtenUmschalten();
        Assert.False(spiel.Einrichten);
        Assert.Null(spiel.Ausgewaehlt);
    }

    /// <summary>Laeuft ein anderer Vorgang, schreibt Einrichten nichts und die Zeile sagt AbgleichTexte.Belegt.</summary>
    [Fact]
    public void BelegtZeigtDenHinweis()
    {
        var speicher = new SchaltSpeicher();
        var ablauf = new Spielablauf(Inhalt, speicher);
        ablauf.Profil("Probe", new PyObjekt { { "oberteil", "cyan" } }, Ereignisse.Normal);
        var spiel = NeuesSpiel(ablauf);
        Assert.True(spiel.Betreten());
        spiel.EinrichtenUmschalten();
        spiel.Waehlen("start-matratze");
        var vorher = speicher.Ereignisse().Count;
        speicher.Belegt = true;
        Assert.False(spiel.InDenKarton());
        Assert.Equal(AbgleichTexte.Belegt, spiel.Zeile);
        Assert.Equal(vorher, speicher.Ereignisse().Count);
        Assert.True(spiel.AusgewaehltSteht);
        speicher.Belegt = false;
        Assert.True(spiel.InDenKarton());
        Assert.Equal(vorher + 1, speicher.Ereignisse().Count);
    }

    private sealed class SchaltSpeicher : ISpielSpeicher
    {
        private readonly SpielSpeicher _innen = new();

        public bool Belegt { get; set; }

        public IDisposable? Sitzung() => Belegt ? null : _innen.Sitzung();

        public string? Schreiben(string art, string daten) => _innen.Schreiben(art, daten);

        public List<SpielEreignis> Ereignisse() => _innen.Ereignisse();

        public bool BestenSchreiben(string lauf, string art, string schluessel, object? wert, string daten) => _innen.BestenSchreiben(lauf, art, schluessel, wert, daten);

        public List<BestenZeile> Bestenliste() => _innen.Bestenliste();

        public bool BestenlisteLoeschen() => _innen.BestenlisteLoeschen();

        public SpielStempel? Stempel() => _innen.Stempel();

        public List<SpielZeile> ZeilenNach(long nummer) => _innen.ZeilenNach(nummer);
    }

    /// <summary>Sammelliste 102: ein Haus zwischen Auge und Figur wird erkannt, eins daneben nicht.</summary>
    [Fact]
    public void HausVorDerFigurIstVerdeckend()
    {
        var spiel = NeuesSpiel();
        var szene = spiel.Szene;
        Assert.True(szene.Haeuser.Count >= szene.Orte.Count);
        var haus = szene.Haeuser.First(h => h.Id == "bitweiche");
        var mitte = (haus.Min + haus.Max) / 2;
        // Figur genau hinter dem Haus (Norden), Auge davor (Sueden, oben)
        var auge = new Vector3(mitte.X, 12, haus.Max.Z + 10);
        var verdeckt = szene.Verdeckt(auge, mitte.X, haus.Min.Z - 0.5f, Figur.StadtHoehe);
        Assert.Contains(haus, verdeckt);
        // Figur vor dem Haus: nicht verdeckt
        Assert.DoesNotContain(haus, szene.Verdeckt(auge, mitte.X, haus.Max.Z + 1f, Figur.StadtHoehe));
        Assert.True(StadtSzene.Schneidet(new Vector3(-1, 0.5f, 0.5f), new Vector3(2, 0.5f, 0.5f), Vector3.Zero, Vector3.One));
        Assert.False(StadtSzene.Schneidet(new Vector3(-1, 2, 0.5f), new Vector3(2, 2, 0.5f), Vector3.Zero, Vector3.One));
        // Stadt ohne Haeuser und die Haeuser einzeln ergeben zusammen alle Teile
        var alle = szene.NetzBauen().AnzahlEcken + szene.Haeuser.Sum(h => szene.HausNetz(h).AnzahlEcken);
        Assert.True(alle > szene.NetzBauen().AnzahlEcken);
        var einzeln = szene.Haeuser.Sum(h => h.TeileBis - h.TeileVon);
        Assert.True(einzeln > 0 && einzeln < szene.Teile.Count);
    }
    /// <summary>
    /// Nachtrag 1 zu P7-13 (Wunsch 3, Sammelliste 102): in jedem Gebaeude sind die vorderen Aussenwaende
    /// (zur Kamera) und jede Wand zwischen Auge und Figur halb durchsichtig, die hintere Wand nie. Netz
    /// ohne Waende plus die Waende einzeln ergeben das ganze Netz.
    /// </summary>
    [Fact]
    public void VordereWaendeInJedemGebaeudeDurchsichtig()
    {
        var ablauf = NeuerAblauf();
        ablauf.Stand.TageFertig = 200;
        var stand = ablauf.Stand;
        var ansichten = new List<string> { "buero", "zuhause", "serverfarm" };
        ansichten.AddRange(SpielDaten.Kundenorte(Inhalt).Select(k => "kunde:" + Py.Str(k["id"])));
        ansichten.AddRange(["geschaeft:moebelhaus", "geschaeft:elektromarkt"]);
        var gesehen = 0;
        var durch = new HashSet<InnenWand>();
        foreach (var ansicht in ansichten)
        {
            var innen = Innenraum.Bauen(ansicht, stand, Innenraeume.Value, null);
            Assert.True(innen is not null, ansicht);
            gesehen++;
            var aussen = innen!.Waende.Where(w => w.Aussen != Vector2.Zero).ToList();
            Assert.Equal(4, aussen.Count);
            Assert.Equal(innen.NetzBauen().AnzahlEcken, innen.NetzBauen(ohneWaende: true).AnzahlEcken + innen.Waende.Sum(w => innen.WandNetz(w).AnzahlEcken));
            // Kamera wie die Flaeche: schraeg von vorn (Sueden) auf die Mitte
            var kamera = new Kamera(innen.Breite / 2, innen.Tiefe / 2, Math.Clamp(Math.Max(innen.Breite, innen.Tiefe) * 0.85f, 12f, 28f)) { Neigung = 58f };
            innen.Durchsichtig(kamera.Auge, innen.StartX, innen.StartZ, Innenraum.FigurHoehe, durch);
            Assert.Contains(aussen.Single(w => w.Aussen == new Vector2(0, 1)), durch);
            Assert.DoesNotContain(aussen.Single(w => w.Aussen == new Vector2(0, -1)), durch);
            Assert.DoesNotContain(aussen.Single(w => w.Aussen == new Vector2(-1, 0)), durch);
            Assert.DoesNotContain(aussen.Single(w => w.Aussen == new Vector2(1, 0)), durch);
            // jede Innenwand, hinter der die Figur steht (Figur noerdlich der Wand, Auge suedlich), wird durchsichtig
            foreach (var wand in innen.Waende.Where(w => w.Aussen == Vector2.Zero && w.Max.X - w.Min.X > 0.6f))
            {
                var x = (wand.Min.X + wand.Max.X) / 2;
                var auge = new Vector3(x, 12, wand.Max.Z + 8);
                innen.Durchsichtig(auge, x, wand.Min.Z - 0.4f, Innenraum.FigurHoehe, durch);
                Assert.Contains(wand, durch);
                // Figur vor der Wand: diese Wand bleibt fest
                innen.Durchsichtig(auge, x, wand.Max.Z + 0.6f, Innenraum.FigurHoehe, durch);
                Assert.DoesNotContain(wand, durch);
            }
        }
        Assert.Equal(ansichten.Count, gesehen);
    }
    /// <summary>
    /// Nachtrag 1 zu P7-13 (Wunsch 5): im Elektromarkt liegt in jedem Regal mit Teilen Ware in drei Boeden,
    /// auf den Auslagetischen Notebooks, Monitore und Grafikkarten; die Geraete fuer die Wohnung je zweimal. Im
    /// Buero bleiben Regale ohne Ware leer.
    /// </summary>
    [Fact]
    public void VolleRegaleImElektromarkt()
    {
        var innen = Innenraum.Bauen("geschaeft:elektromarkt", NeuerAblauf().Stand, Innenraeume.Value, null)!;
        var deko = Deko(innen.Gebaeude).ToList();
        int Darin(PyObjekt d, float ab) => innen.Teile.Count(t =>
        {
            var (mx, mz) = ((t.X1 + t.X2) / 2, (t.Z1 + t.Z2) / 2);
            return t.Y1 >= ab && mx >= Py.F(d["x"]) && mx <= Py.F(d["x"]) + Py.F(d["w"]) && mz >= Py.F(d["y"]) && mz <= Py.F(d["y"]) + Py.F(d["h"]);
        });
        var regale = deko.Where(d => Py.Gleich(d["typ"], "regal") && Py.Str(d["ware"]).StartsWith("teile:", StringComparison.Ordinal)).ToList();
        Assert.Equal(15, regale.Count);
        Assert.All(regale, d => Assert.True(Darin(d, 0.05f) >= 6, Py.Str(d["ware"]) + ": " + Darin(d, 0.05f)));
        var auslagen = deko.Where(d => Py.Gleich(d["typ"], "auslage")).ToList();
        Assert.Equal(["teile:notebook", "teile:monitor", "teile:gpu"], auslagen.Select(d => Py.Str(d["ware"])));
        Assert.All(auslagen, d => Assert.True(Darin(d, 0.74f * Innenraum.Meter) >= 3, Py.Str(d["ware"]) + " ohne Ware auf dem Tisch"));
        // Geraete fuer die Wohnung je zweimal ausgestellt
        Assert.All(["fernseher", "spielkonsole", "waschmaschine"], w => Assert.Equal(2, deko.Count(d => Py.Gleich(d["ware"], w))));
        // Kontrollfehler: dasselbe Regal ohne Ware ist leer (das Regal selbst steht ab dem Boden)
        var leer = Innenraum.AusGrundriss("buero", new PyObjekt
        {
            { "breite", 8.0 }, { "hoehe", 8.0 },
            { "flur", new PyObjekt { { "y", 6.0 }, { "h", 2.0 }, { "eingang", new PyObjekt { { "von", 6.3 }, { "bis", 7.7 } } }, { "spieler", new List<object?> { 1.2, 7.0 } } } },
            { "raeume", new List<object?> { new PyObjekt { { "id", "a" }, { "name", "A" }, { "x", 0.0 }, { "y", 0.0 }, { "w", 8.0 }, { "h", 6.0 }, { "tuer", new PyObjekt { { "von", 1.0 }, { "bis", 2.4 } } },
                { "deko", new List<object?> { new PyObjekt { { "typ", "regal" }, { "x", 0.4 }, { "y", 0.3 }, { "w", 2.4 }, { "h", 0.6 } } } } } } },
        }, null);
        Assert.Equal(0, leer.Teile.Count(t => t.Y1 >= 0.05f && t.X1 >= 0.39f && t.X2 <= 2.81f && t.Z2 <= 0.91f));
    }
}
