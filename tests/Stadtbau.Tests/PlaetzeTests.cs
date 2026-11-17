using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.56.0 (Sammelliste 362, Weisung P12-287, Nico 1a 2b 4a 5a): Karte vergroessern mit Wahl des Platzes und Stadtteil
/// einsetzen. Abnahmesatz 1: jedes Stueck liegt danach genau um die Lage des Platzes verschoben, Rueckgaengig ist bitgleich,
/// Platz 1 laesst jede Lage, wie sie ist. Die Pruefung der Lage rechnet unabhaengig von <see cref="KarteVerschieben"/> ueber
/// alle Zahlen der Stadt (Schluessel x/y, Punkte, Raster der gemalten Hoehen): jede Zahl, die keine Lage ist, bleibt gleich.
/// </summary>
public class PlaetzeTests
{
    private static Spieldaten D => Daten.Spiel;

    private static Projekt Stadt1(string name = "Stadt") => Projekt.Neu(name, D, Daten.NeueAblage());

    private static Projekt Leer(string name, double breite, double hoehe, Werkzeugordner? ablage = null) =>
        Projekt.Neu(name, D, ablage ?? Daten.NeueAblage(), null, (breite, hoehe));

    private static AussenStelle Setzen(Projekt p, AussenArt art, double x, double y, string? modell = null) =>
        p.AussenAendern("Setzen", a => AussenBearbeitung.Setzen(a, art, x, y, modell: modell)) ?? throw new InvalidOperationException(art + " nicht gesetzt");

    /// <summary>
    /// Alle Arten mit Lage, die die Stadt 1 (Grundlage 32 × 24) noch nicht hat, dazulegen (Notiz, Huegel, gemalte Hoehe,
    /// Katalog, Namen, Questgeber draussen, Beet, Laterne).
    /// </summary>
    private static void AllesDazu(Projekt p, double x0 = 0, double y0 = 0)
    {
        Setzen(p, AussenArt.Notiz, x0 + 1.5, y0 + 1.5);
        Setzen(p, AussenArt.Huegel, x0 + 27, y0 + 19);
        Setzen(p, AussenArt.Katalog, x0 + 29.5, y0 + 22.5, "car_kit/sedan");
        Setzen(p, AussenArt.Beet, x0 + 30.5, y0 + 1.5);
        Setzen(p, AussenArt.Laterne, x0 + 26.5, y0 + 1.5);
        p.AussenAendern("Namen und Hoehe", a =>
        {
            var sd = Aussen.Stadtdaten(a);
            Py.Setzen(sd, Stadtteile.AbschnittNamen, new List<object?>
            {
                new PyObjekt { { "name", "Nord" }, { "x1", Bearbeitung.Zahl(x0 + 2) }, { "z1", Bearbeitung.Zahl(y0 + 2) }, { "x2", Bearbeitung.Zahl(x0 + 12) }, { "z2", Bearbeitung.Zahl(y0 + 12) } },
            });
            if (Get(sd, Gelaende.Abschnitt) is not PyObjekt g)
            {
                g = new PyObjekt();
                Py.Setzen(sd, Gelaende.Abschnitt, g);
            }
            if (Get(g, "gemalt") is not List<object?> gemalt)
            {
                gemalt = [];
                Py.Setzen(g, "gemalt", gemalt);
            }
            gemalt.Add(new List<object?> { (long)((x0 + 30) / Gelaende.Raster), (long)((y0 + 22) / Gelaende.Raster), 0.5 });
        });
        var qg = p.QuestgeberNeu();
        p.QuestgeberPlatz(qg, new QuestgeberDaten.Platz(true, "", 0, x0 + 15.5, y0 + 12, 0));
    }

    // ---- unabhaengige Pruefung der Lage ------------------------------------------------------------

    private static readonly HashSet<string> SchluesselX = ["x", "x1", "x2"];
    private static readonly HashSet<string> SchluesselY = ["y", "z1", "z2"];
    private static readonly HashSet<string> Punktlisten = ["punkte", "weg", "laternen", "beete"];
    private static readonly HashSet<string> Punkt = ["tuer", "schild"];

    /// <summary>
    /// Vergleicht jede Zahl der Teile mit Lage vorher und nachher: Lage um (dx, dy) verschoben, alles andere gleich (auch die
    /// Art: ganze Zahl bleibt ganze Zahl). Rueckgabe: Abweichungen (leer = alles richtig) und Zahl der verschobenen Werte.
    /// </summary>
    private static (List<string> Fehler, int Verschoben) LageVergleich(PyObjekt vorher, PyObjekt nachher, double dx, double dy)
    {
        var fehler = new List<string>();
        var verschoben = 0;
        var tv = Aussen.Teile(vorher).ToDictionary(t => t.Pfad, t => t.Wert, StringComparer.Ordinal);
        var tn = Aussen.Teile(nachher).ToDictionary(t => t.Pfad, t => t.Wert, StringComparer.Ordinal);
        foreach (var (pfad, wert) in tv)
        {
            if (!tn.TryGetValue(pfad, out var neu))
            {
                fehler.Add(pfad + " fehlt");
                continue;
            }
            if (KarteVerschieben.OhneLage.Contains(pfad))
            {
                if (PythonJson.Standard(wert) != PythonJson.Standard(neu))
                {
                    fehler.Add(pfad + " (ohne Lage) geaendert");
                }
                continue;
            }
            Gehe(pfad, wert, neu, null, pfad == "stadtdaten/" + Questebene.AbschnittQuestgeber);
        }
        foreach (var pfad in tn.Keys.Where(k => !tv.ContainsKey(k)))
        {
            fehler.Add(pfad + " neu");
        }
        return (fehler, verschoben);

        void Zahl(string ort, object? a, object? b, double um)
        {
            var erwartet = Py.F(a) + um;
            if (b is not (long or double) || Math.Abs(Py.F(b) - erwartet) > 1e-9 || (a is long && um == Math.Floor(um) && b is not long))
            {
                fehler.Add(string.Create(CultureInfo.InvariantCulture, $"{ort}: {PythonJson.Standard(a)} -> {PythonJson.Standard(b)}, erwartet {erwartet}"));
            }
            else if (um != 0)
            {
                verschoben++;
            }
        }

        void PunktVergleich(string ort, object? a, object? b)
        {
            if (a is List<object?> { Count: >= 2 } la && la[0] is long or double && la[1] is long or double && b is List<object?> lb && lb.Count == la.Count)
            {
                Zahl(ort + "[0]", la[0], lb[0], dx);
                Zahl(ort + "[1]", la[1], lb[1], dy);
                for (var i = 2; i < la.Count; i++)
                {
                    Gehe(ort + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", la[i], lb[i], null, false);
                }
                return;
            }
            Gehe(ort, a, b, null, false);
        }

        void Gehe(string ort, object? a, object? b, string? schluessel, bool questgeber)
        {
            switch (a)
            {
                case PyObjekt oa when b is PyObjekt ob:
                    if (oa.Count != ob.Count)
                    {
                        fehler.Add(ort + ": andere Felder");
                        return;
                    }
                    // Questgeber: nur der Platz draussen hat eine Lage auf der Karte (drinnen = Lage im Raum)
                    var drinnen = questgeber && schluessel == "platz" && !Py.Wahr(Get(oa, "draussen"));
                    foreach (var (k, va) in oa)
                    {
                        if (!Py.Hat(ob, k))
                        {
                            fehler.Add(ort + "." + k + " fehlt");
                            continue;
                        }
                        var vb = ob[k];
                        if (!drinnen && SchluesselX.Contains(k) && va is long or double)
                        {
                            Zahl(ort + "." + k, va, vb, dx);
                        }
                        else if (!drinnen && SchluesselY.Contains(k) && va is long or double)
                        {
                            Zahl(ort + "." + k, va, vb, dy);
                        }
                        else if (Punkt.Contains(k))
                        {
                            PunktVergleich(ort + "." + k, va, vb);
                        }
                        else if (Punktlisten.Contains(k) && va is List<object?> pa && vb is List<object?> pb && pa.Count == pb.Count)
                        {
                            for (var i = 0; i < pa.Count; i++)
                            {
                                PunktVergleich(ort + "." + k + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", pa[i], pb[i]);
                            }
                        }
                        else if (k == "gemalt" && va is List<object?> ga && vb is List<object?> gb && ga.Count == gb.Count)
                        {
                            for (var i = 0; i < ga.Count; i++)
                            {
                                if (ga[i] is List<object?> { Count: 3 } e && gb[i] is List<object?> { Count: 3 } f)
                                {
                                    Zahl(ort + ".gemalt[" + i.ToString(CultureInfo.InvariantCulture) + "][0]", e[0], f[0], dx / Gelaende.Raster);
                                    Zahl(ort + ".gemalt[" + i.ToString(CultureInfo.InvariantCulture) + "][1]", e[1], f[1], dy / Gelaende.Raster);
                                    Gehe(ort + ".gemalt[" + i.ToString(CultureInfo.InvariantCulture) + "][2]", e[2], f[2], null, false);
                                }
                            }
                        }
                        else
                        {
                            Gehe(ort + "." + k, va, vb, k, questgeber);
                        }
                    }
                    return;
                case List<object?> la when b is List<object?> lb:
                    if (la.Count != lb.Count)
                    {
                        fehler.Add(ort + ": andere Laenge");
                        return;
                    }
                    // Listen von Punkten ohne bekannten Schluessel (automatische Laternen: Teil laternen_auto)
                    var punkte = ort.StartsWith(LaternenAutomatik.Teil, StringComparison.Ordinal) || ort is "laternen" or "beete";
                    for (var i = 0; i < la.Count; i++)
                    {
                        var o = ort + "[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                        if (punkte)
                        {
                            PunktVergleich(o, la[i], lb[i]);
                        }
                        else
                        {
                            Gehe(o, la[i], lb[i], null, questgeber);
                        }
                    }
                    return;
                default:
                    if (PythonJson.Standard(a) != PythonJson.Standard(b))
                    {
                        fehler.Add(ort + ": " + PythonJson.Standard(a) + " -> " + PythonJson.Standard(b));
                    }
                    return;
            }
        }
    }

    /// <summary>Stand ohne die Groesse der Karte (die waechst beim Vergroessern gewollt).</summary>
    private static PyObjekt OhneGroesse(PyObjekt a, double breite, double hoehe)
    {
        var k = (PyObjekt)Py.Kopie(a)!;
        Py.Setzen(Aussen.Welt(k), "breite", Bearbeitung.Zahl(breite));
        Py.Setzen(Aussen.Welt(k), "hoehe", Bearbeitung.Zahl(hoehe));
        return k;
    }

    // ---- Karte vergroessern ------------------------------------------------------------------------

    [Fact]
    public void Vergroessern_JedesStueckGenauUmDenPlatz_EinSchritt_RueckgaengigBitgleich()
    {
        var p = Stadt1();
        AllesDazu(p);
        Assert.Empty(KarteVerschieben.Unbekannt(p.AussenStand));
        var vorher = (PyObjekt)Py.Kopie(p.AussenStand)!;
        var text = Aussen.TeileText(p.AussenStand);
        var schritte = p.SchritteZurueck;
        Assert.Null(p.KarteVergroessern(128, 128, 4));
        Assert.Equal(schritte + 1, p.SchritteZurueck);
        Assert.Equal((128d, 128d), Aussen.Groesse(p.AussenStand));
        var (fehler, verschoben) = LageVergleich(OhneGroesse(vorher, 128, 128), p.AussenStand, 64, 64);
        Assert.True(fehler.Count == 0, string.Join("\n", fehler.Take(20)));
        Assert.True(verschoben > 300, "nur " + verschoben + " Werte verschoben");
        // jede Art mit Lage ist dabei (die Probe sieht alle Teile)
        foreach (var teil in Aussen.Teile(vorher).Select(t => t.Pfad).Where(KarteVerschieben.MitLage.Contains))
        {
            var (f1, n1) = LageVergleich(Nur(OhneGroesse(vorher, 128, 128), teil), Nur(p.AussenStand, teil), 64, 64);
            Assert.True(f1.Count == 0 && (n1 > 0 || teil == "stadtdaten/darstellung" || teil == "karte/modelle"), teil + ": " + n1 + " verschoben");
        }
        Assert.True(p.Rueckgaengig());
        Assert.Equal(text, Aussen.TeileText(p.AussenStand));
        Assert.True(p.Wiederholen());
        Assert.Empty(LageVergleich(OhneGroesse(vorher, 128, 128), p.AussenStand, 64, 64).Fehler);
    }

    /// <summary>Nur ein Teil (die anderen geleert), fuer die Zaehlung je Teil.</summary>
    private static PyObjekt Nur(PyObjekt a, string teil)
    {
        var k = (PyObjekt)Py.Kopie(a)!;
        foreach (var (pfad, _) in Aussen.Teile(a).Where(t => t.Pfad != teil && t.Pfad != "karte/welt").ToList())
        {
            Aussen.TeilSetzen(k, pfad, Py.Kopie(Aussen.Teile(a).First(t => t.Pfad == pfad).Wert), true);
        }
        return k;
    }

    [Fact]
    public void Vergroessern_Platz1_JedeLageBleibt()
    {
        var p = Stadt1();
        AllesDazu(p);
        var vorher = (PyObjekt)Py.Kopie(p.AussenStand)!;
        Assert.Null(p.KarteVergroessern(192, 64, 1));
        var (fehler, verschoben) = LageVergleich(OhneGroesse(vorher, 192, 64), p.AussenStand, 0, 0);
        Assert.Empty(fehler);
        Assert.Equal(0, verschoben);
        Assert.Equal(Aussen.TeileText(OhneGroesse(vorher, 192, 64)), Aussen.TeileText(p.AussenStand));
    }

    [Fact]
    public void Vergroessern_Gegenprobe_VergessenerTeilIstRot()
    {
        // Die Probe erkennt einen Teil, der nicht mitwandert: Huegel von Hand zurueckgesetzt
        var p = Stadt1();
        AllesDazu(p);
        var vorher = (PyObjekt)Py.Kopie(p.AussenStand)!;
        Assert.Null(p.KarteVergroessern(128, 64, 2));
        var falsch = (PyObjekt)Py.Kopie(p.AussenStand)!;
        var h = Objekt(AussenBearbeitung.HuegelListe(falsch)[0]);
        Py.Setzen(h, "x", Py.F(h["x"]) - 64);
        var (fehler, _) = LageVergleich(OhneGroesse(vorher, 128, 64), falsch, 64, 0);
        Assert.Contains(fehler, f => f.Contains("huegel", StringComparison.Ordinal));
        // ebenso eine Zahl ohne Lage, die sich aendert
        var falsch2 = (PyObjekt)Py.Kopie(p.AussenStand)!;
        Py.Setzen(Objekt(AussenBearbeitung.HuegelListe(falsch2)[0]), "x", Py.F(Objekt(AussenBearbeitung.HuegelListe(falsch2)[0])["x"]) + 0.5);
        Assert.NotEmpty(LageVergleich(OhneGroesse(vorher, 128, 64), falsch2, 64, 0).Fehler);
    }

    [Theory]
    [InlineData(96, 64, 1)]
    [InlineData(32, 64, 1)]
    [InlineData(64, 64, 1)]
    [InlineData(128, 64, 3)]
    [InlineData(64, 128, 3)]
    public void Vergroessern_Ungueltig_GrundUndNichtsGeaendert(int breite, int hoehe, int platz)
    {
        var p = Leer("Leer", 64, 64);
        AllesDazu(p);
        var text = Aussen.TeileText(p.AussenStand);
        var schritte = p.SchritteZurueck;
        Assert.NotNull(p.KarteVergroessern(breite, hoehe, platz));
        Assert.Equal(schritte, p.SchritteZurueck);
        Assert.Equal(text, Aussen.TeileText(p.AussenStand));
    }

    [Fact]
    public void Vergroessern_UnbekannterTeil_NichtsVerschoben()
    {
        var p = Stadt1();
        var a = (PyObjekt)Py.Kopie(p.AussenStand)!;
        Py.Setzen(Aussen.Stadtdaten(a), "neu_unbekannt", new List<object?> { new PyObjekt { { "x", 1L } } });
        Assert.Equal(["stadtdaten/neu_unbekannt"], KarteVerschieben.Unbekannt(a));
        var text = Aussen.TeileText(a);
        Assert.Equal(["stadtdaten/neu_unbekannt"], KarteVerschieben.Alles(a, 64, 0));
        Assert.Equal(text, Aussen.TeileText(a));
    }

    [Fact]
    public void Plaetze_Nummern_ZeilenweiseVonObenLinks()
    {
        Assert.Equal(16, Plaetze.Anzahl(256, 256));
        Assert.Equal(2, Plaetze.Anzahl(128, 64));
        Assert.Equal((0, 0), Plaetze.Lage(1, 256));
        Assert.Equal((192, 0), Plaetze.Lage(4, 256));
        Assert.Equal((0, 64), Plaetze.Lage(5, 256));
        Assert.Equal((192, 192), Plaetze.Lage(16, 256));
        Assert.Equal((64, 64), Plaetze.Lage(4, 128));
        Assert.False(Plaetze.Gibt(5, 128, 128));
        Assert.Equal(6, Plaetze.Bei(70, 70, 256, 256));
    }

    [Fact]
    public void Vergroessern_Format5_UndBis64Bitgleich()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Format", D, ablage);
        p.Speichern(true);
        var vorher = File.ReadAllText(p.Datei);
        Assert.DoesNotContain("\"formatversion\": 5", vorher, StringComparison.Ordinal);
        // bis 64 bleibt die Datei, wie sie war (nur die Groesse aendert sich, Format wie vorher)
        Assert.NotNull(p.KarteVergroessern(32, 64, 1));
        p.Speichern(true);
        Assert.Equal(vorher.Split('\n').Where(z => !z.Contains("\"erstellt\"", StringComparison.Ordinal)), File.ReadAllText(p.Datei).Split('\n').Where(z => !z.Contains("\"erstellt\"", StringComparison.Ordinal)));
        Assert.Null(p.KarteVergroessern(128, 64, 2));
        p.Speichern(true);
        Assert.Contains("\"formatversion\": 5", File.ReadAllText(p.Datei), StringComparison.Ordinal);
        // auch nach Rueckgaengig (die grosse Karte steht im Verlauf) bleibt es 5: ein altes Werkzeug koennte sie sonst lesen
        Assert.True(p.Rueckgaengig());
        p.Speichern(true);
        Assert.Contains("\"formatversion\": 5", File.ReadAllText(p.Datei), StringComparison.Ordinal);
        // wieder oeffnen: gleiche Stadt
        var neu = Projekt.Oeffnen(p.Datei, D, ablage);
        Assert.Equal(Aussen.TeileText(p.AussenStand), Aussen.TeileText(neu.AussenStand));
        Assert.True(neu.Wiederholen());
        Assert.Equal((128d, 64d), Aussen.Groesse(neu.AussenStand));
    }

    // ---- Stadtteil einsetzen -----------------------------------------------------------------------

    private static Dictionary<AussenArt, int> Zaehlen(PyObjekt a) =>
        AussenBearbeitung.Alle(a).GroupBy(t => t.Stelle.Art).ToDictionary(g => g.Key, g => g.Count());

    /// <summary>Gleich viele Stuecke je Art wie Ziel und Quelle zusammen (Haeuser zaehlen zusammen: Spielort oder Gebaeude ohne Ort).</summary>
    private static List<string> ArtenFehlen(PyObjekt ziel, PyObjekt quelle, PyObjekt ergebnis, int ohneOrt = 0)
    {
        var z = Zaehlen(ziel);
        var q = Zaehlen(quelle);
        var e = Zaehlen(ergebnis);
        var fehler = new List<string>();
        foreach (var art in z.Keys.Union(q.Keys).Union(e.Keys).Where(a => a is not (AussenArt.Haus or AussenArt.Gebaeude or AussenArt.Paketplatz)))
        {
            var soll = z.GetValueOrDefault(art) + q.GetValueOrDefault(art);
            if (e.GetValueOrDefault(art) != soll)
            {
                fehler.Add(art + ": " + e.GetValueOrDefault(art) + " statt " + soll);
            }
        }
        int Haeuser(Dictionary<AussenArt, int> d) => d.GetValueOrDefault(AussenArt.Haus) + d.GetValueOrDefault(AussenArt.Gebaeude);
        if (Haeuser(e) != Haeuser(z) + Haeuser(q))
        {
            fehler.Add("Haeuser: " + Haeuser(e) + " statt " + (Haeuser(z) + Haeuser(q)));
        }
        if (e.GetValueOrDefault(AussenArt.Gebaeude) != z.GetValueOrDefault(AussenArt.Gebaeude) + q.GetValueOrDefault(AussenArt.Gebaeude) + ohneOrt)
        {
            fehler.Add("Gebaeude ohne Ort: " + e.GetValueOrDefault(AussenArt.Gebaeude));
        }
        if (Stadtteile.Bereiche(Aussen.Stadtdaten(ergebnis)).Count != Stadtteile.Bereiche(Aussen.Stadtdaten(ziel)).Count + Stadtteile.Bereiche(Aussen.Stadtdaten(quelle)).Count)
        {
            fehler.Add("Stadtteil-Namen");
        }
        int Geber(PyObjekt a) => ListeOderLeer(Aussen.Stadtdaten(a), Questebene.AbschnittQuestgeber).Count;
        if (Geber(ergebnis) != Geber(ziel) + Geber(quelle))
        {
            fehler.Add("Questgeber");
        }
        return fehler;
    }

    [Fact]
    public void Einsetzen_Stadt1AufPlatz2_AllesDa_GenauVerschoben_EinSchritt()
    {
        var ablage = Daten.NeueAblage();
        var quelle = Projekt.Neu("Teil", D, ablage);
        AllesDazu(quelle);
        quelle.Speichern(true);
        var ziel = Leer("Gross", 128, 64, ablage);
        var vorher = (PyObjekt)Py.Kopie(ziel.AussenStand)!;
        var text = Aussen.TeileText(ziel.AussenStand);
        var vorschau = ziel.EinsetzenPruefen(quelle, 2);
        Assert.True(vorschau.Geht, vorschau.Grund);
        Assert.Equal(text, Aussen.TeileText(ziel.AussenStand));
        var schritte = ziel.SchritteZurueck;
        var e = ziel.StadtteilEinsetzen(quelle, 2);
        Assert.True(e.Geht, e.Grund);
        Assert.Equal(vorschau.Hinweise, e.Hinweise);
        Assert.Equal((64, 0), e.Ecke);
        Assert.Equal(schritte + 1, ziel.SchritteZurueck);
        var fehlt = ArtenFehlen(vorher, quelle.AussenStand, ziel.AussenStand);
        Assert.True(fehlt.Count == 0, string.Join("\n", fehlt));
        // jedes Stueck der Quelle liegt im Ziel genau um (64, 0) verschoben
        var mitten = AussenBearbeitung.Alle(ziel.AussenStand).Select(t => (t.Stelle.Art, X: Math.Round(t.X, 3), Y: Math.Round(t.Y, 3))).ToList();
        foreach (var (s, x, y) in AussenBearbeitung.Alle(quelle.AussenStand))
        {
            var art = s.Art is AussenArt.Haus or AussenArt.Gebaeude ? (AussenArt?)null : s.Art;
            Assert.True(mitten.Any(m => (art is null ? m.Art is AussenArt.Haus or AussenArt.Gebaeude : m.Art == art)
                && Math.Abs(m.X - (x + 64)) < 0.01 && Math.Abs(m.Y - y) < 0.01), $"{s.Art} {s.Ort} bei {x}, {y} fehlt bei {x + 64}, {y}");
        }
        // die Quelle bleibt unveraendert
        Assert.False(quelle.SchritteZurueck > 0 && quelle.AussenStand is null);
        Assert.True(ziel.Rueckgaengig());
        Assert.Equal(text, Aussen.TeileText(ziel.AussenStand));
    }

    [Fact]
    public void Einsetzen_Gegenprobe_ZwischenablageNimmtNichtAllesMit()
    {
        // Gleiche Zaehlung mit "Alles kopieren + einfuegen": Namen, Questgeber und gemalte Hoehen fehlen dort (rot)
        var ablage = Daten.NeueAblage();
        var quelle = Projekt.Neu("Teil", D, ablage);
        AllesDazu(quelle);
        var ziel = Leer("Gross", 128, 64, ablage);
        var vorher = (PyObjekt)Py.Kopie(ziel.AussenStand)!;
        var z = AussenBearbeitung.InZwischenablage(quelle, [.. AussenBearbeitung.Alle(quelle.AussenStand).Select(t => t.Stelle)], out _)!;
        ziel.AussenAendern(Texte.EinfuegenSchritt, a => AussenBearbeitung.Einfuegen(a, z, z.Mitte.X + 64, z.Mitte.Y, true, D.Fassung));
        Assert.NotEmpty(ArtenFehlen(vorher, quelle.AussenStand, ziel.AussenStand));
    }

    [Fact]
    public void Einsetzen_DoppelterSpielort_GebaeudeOhneOrt_QuestsImHinweis()
    {
        var ablage = Daten.NeueAblage();
        var quelle = Projekt.Neu("Teil", D, ablage);
        var ziel = Projekt.Neu("Ziel", D, ablage);
        Assert.Null(ziel.KarteVergroessern(128, 64, 1));
        var vorher = (PyObjekt)Py.Kopie(ziel.AussenStand)!;
        var orte = AussenBearbeitung.Alle(quelle.AussenStand).Count(t => t.Stelle.Art == AussenArt.Haus);
        Assert.True(orte > 5);
        var e = ziel.StadtteilEinsetzen(quelle, 2);
        Assert.True(e.Geht, e.Grund);
        // jeder Spielort der Quelle gibt es schon: alle kommen als Gebaeude ohne Ort (4a), Paketplatz bleibt einer
        var fehlt = ArtenFehlen(vorher, quelle.AussenStand, ziel.AussenStand, ohneOrt: orte);
        Assert.True(fehlt.Count == 0, string.Join("\n", fehlt));
        Assert.Equal(orte, AussenBearbeitung.Alle(ziel.AussenStand).Count(t => t.Stelle.Art == AussenArt.Haus));
        Assert.Contains(Texte.EinsetzenPaketplatz, e.Hinweise);
        Assert.NotEmpty(e.Hinweise);
    }

    [Fact]
    public void Einsetzen_Nie_Still_Belegt_Huegel_ZuGross_SichSelbst_PlatzFehlt()
    {
        var ablage = Daten.NeueAblage();
        var quelle = Projekt.Neu("Teil", D, ablage);
        var ziel = Projekt.Neu("Ziel", D, ablage);
        Assert.Null(ziel.KarteVergroessern(128, 64, 1));
        var text = Aussen.TeileText(ziel.AussenStand);
        var schritte = ziel.SchritteZurueck;
        void Nein(Projekt.EinsetzenErgebnis e, string erwartet)
        {
            Assert.False(e.Geht);
            Assert.Equal(erwartet, e.Grund);
            Assert.Equal(text, Aussen.TeileText(ziel.AussenStand));
            Assert.Equal(schritte, ziel.SchritteZurueck);
        }
        Assert.StartsWith("Auf Platz 1 liegt schon etwas", ziel.StadtteilEinsetzen(quelle, 1).Grund, StringComparison.Ordinal);
        Nein(ziel.StadtteilEinsetzen(quelle, 1), ziel.EinsetzenPruefen(quelle, 1).Grund!);
        Nein(ziel.StadtteilEinsetzen(quelle, 3), Texte.PlatzFehlt(3, 2));
        Nein(ziel.StadtteilEinsetzen(ziel, 2), Texte.EinsetzenSichSelbst);
        var gross = Leer("Gross", 128, 64, ablage);
        Nein(ziel.StadtteilEinsetzen(gross, 2), Texte.EinsetzenZuGross("Gross", 128, 64));
        // Huegel: zusammen mehr als 16
        var viele = Leer("Huegel", 64, 64, ablage);
        for (var i = 0; i < 9; i++)
        {
            Setzen(viele, AussenArt.Huegel, 5 + (i * 6), 30);
        }
        var huegelZiel = Leer("Huegel Ziel", 128, 64, ablage);
        for (var i = 0; i < 8; i++)
        {
            Setzen(huegelZiel, AussenArt.Huegel, 5 + (i * 6), 30);
        }
        var e = huegelZiel.StadtteilEinsetzen(viele, 2);
        Assert.Equal(Texte.EinsetzenHuegel(17), e.Grund);
        // ein Stueck ueber den Rand des Platzes (Strasse von Platz 1 nach Platz 2) belegt Platz 2
        var strasse = Leer("Strasse", 128, 64, ablage);
        strasse.AussenAendern("Strasse", a => Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { new List<object?> { 60L, 10L }, new List<object?> { 70L, 10L } } } }));
        Assert.StartsWith("Auf Platz 2 liegt schon etwas", strasse.EinsetzenPruefen(viele, 2).Grund, StringComparison.Ordinal);
        // bis an den Rand (x = 64) ist frei
        var randig = Leer("Rand", 128, 64, ablage);
        randig.AussenAendern("Strasse", a => Aussen.Landschaft(a).Add(new PyObjekt { { "typ", "strasse" }, { "punkte", new List<object?> { new List<object?> { 50L, 10L }, new List<object?> { 64L, 10L } } } }));
        Assert.True(randig.EinsetzenPruefen(viele, 2).Geht);
    }

    [Fact]
    public void Einsetzen_QuestKennungen_Neu_MitHinweis()
    {
        var ablage = Daten.NeueAblage();
        var quelle = Leer("Quelle", 64, 64, ablage);
        var g1 = quelle.QuestgeberNeu();
        quelle.QuestgeberPlatz(g1, new QuestgeberDaten.Platz(true, "", 0, 10, 10, 0));
        var ziel = Leer("Ziel", 128, 64, ablage);
        var g2 = ziel.QuestgeberNeu();
        ziel.QuestgeberPlatz(g2, new QuestgeberDaten.Platz(true, "", 0, 10, 10, 0));
        Assert.Equal(g1, g2);
        var e = ziel.StadtteilEinsetzen(quelle, 2);
        Assert.True(e.Geht, e.Grund);
        var ids = ListeOderLeer(Aussen.Stadtdaten(ziel.AussenStand), Questebene.AbschnittQuestgeber).OfType<PyObjekt>().Select(g => Py.Str(g["id"])).ToList();
        Assert.Equal(2, ids.Distinct().Count());
        Assert.Contains(e.Hinweise, h => h.Contains(g1, StringComparison.Ordinal));
    }

    // ---- gelbe Rand-Hinweise (Nico 3a) -------------------------------------------------------------

    private static void Linie(Projekt p, string typ, params (double X, double Y)[] punkte) =>
        p.AussenAendern("Linie", a => Aussen.Landschaft(a).Add(new PyObjekt
        {
            { "typ", typ },
            { "punkte", new List<object?>(punkte.Select(q => (object?)new List<object?> { Bearbeitung.Zahl(q.X), Bearbeitung.Zahl(q.Y) })) },
        }));

    [Fact]
    public void Rand_OffenesEnde_Gelb_MitStelle_VerbundenOhneHinweis()
    {
        var p = Leer("Rand", 128, 128);
        Assert.Empty(Plaetze.RandHinweise(p.AussenStand));
        // verbunden: zwei Strassen treffen sich genau an der Grenze x = 64
        Linie(p, "strasse", (40, 10), (64, 10));
        Linie(p, "strasse", (64, 10), (90, 10));
        Assert.Empty(Plaetze.RandHinweise(p.AussenStand));
        // versetzt: drueben liegt die Strasse 3 Felder weiter unten -> zwei gelbe Hinweise mit Stelle
        Linie(p, "strasse", (40, 30), (64, 30));
        Linie(p, "strasse", (64, 33), (90, 33));
        var h = Plaetze.RandHinweise(p.AussenStand);
        Assert.Equal(2, h.Count);
        Assert.All(h, f => Assert.False(f.Rot));
        Assert.Contains(h, f => f.X == 64 && f.Y == 30);
        Assert.Contains(h, f => f.X == 64 && f.Y == 33);
        // Bahn an der waagrechten Grenze y = 64, drueben nur eine Strasse (andere Art) -> Hinweis
        Linie(p, "bahn", (100, 40), (100, 64));
        Linie(p, "strasse", (100, 64), (100, 90));
        Assert.Equal(4, Plaetze.RandHinweise(p.AussenStand).Count);
        // aeusserer Rand der Karte zaehlt nicht
        Linie(p, "fluss", (110, 100), (128, 100));
        Assert.Equal(4, Plaetze.RandHinweise(p.AussenStand).Count);
        // die Liste der Pruefung zeigt sie (gelb)
        Assert.Equal(4, p.PruefenAussen().Count(f => !f.Rot && f.Tipp == Texte.RandOffen));
        // Karte bis 64: keine Plaetze-Grenze, nie ein Hinweis
        var klein = Leer("Klein", 64, 64);
        Linie(klein, "strasse", (10, 32), (64, 32));
        Assert.Empty(Plaetze.RandHinweise(klein.AussenStand));
    }

    // ---- Fund Nico 373 (S 0.56.2): heutige Stadt auf einen Platz setzen ----------------------------------

    private static Projekt NicoStadt1(Werkzeugordner? ablage = null) => Projekt.AusStadtDatei(
        File.ReadAllBytes(Path.Combine(Daten.Wurzel, "tests", "Stadtbau.Tests", "Testdaten", "nico", "stadt1_S0.53.1.fistadt")),
        D, ablage ?? Daten.NeueAblage(), "Nico 373");

    [Fact]
    public void S373_FallA_NicosStadt1_Platz4_GleichVergroessern_VorschauGleichSchritt_RueckgaengigBitgleich()
    {
        var p = NicoStadt1();
        Assert.Equal(1, p.StadtPlatz());
        Assert.Equal((0, 0, 64, 64), Projekt.StadtBereich(p.AussenStand));
        var vorher = (PyObjekt)Py.Kopie(p.AussenStand)!;
        var text = Aussen.TeileText(p.AussenStand);
        var vorschau = p.StadtSetzenPruefen(128, 128, 4);
        Assert.Equal(text, Aussen.TeileText(p.AussenStand));
        Assert.True(vorschau.Geht, vorschau.Grund);
        Assert.Equal((64, 64), vorschau.Versatz);
        Assert.Equal((64, 64, 128, 128), vorschau.Nachher);
        var schritte = p.SchritteZurueck;
        var plan = p.StadtSetzen(128, 128, 4);
        Assert.Equal(vorschau, plan);
        Assert.Equal(schritte + 1, p.SchritteZurueck);
        Assert.Equal((128d, 128d), Aussen.Groesse(p.AussenStand));
        Assert.Equal(vorschau.Nachher, Projekt.StadtBereich(p.AussenStand));
        Assert.Equal(4, p.StadtPlatz());
        var (fehler, verschoben) = LageVergleich(OhneGroesse(vorher, 128, 128), p.AussenStand, 64, 64);
        Assert.True(fehler.Count == 0, string.Join("\n", fehler.Take(20)));
        Assert.True(verschoben > 300, "nur " + verschoben + " Werte verschoben");
        // gleiche Stadt wie der Kern-Schritt „Karte vergroessern“ (seit S 0.56.0 geprueft)
        var q = NicoStadt1();
        Assert.Null(q.KarteVergroessern(128, 128, 4));
        Assert.Equal(Aussen.TeileText(q.AussenStand), Aussen.TeileText(p.AussenStand));
        Assert.True(p.Rueckgaengig());
        Assert.Equal(text, Aussen.TeileText(p.AussenStand));
    }

    [Fact]
    public void S373_FallB_KarteSchonGross_StadtAufAnderenPlatz_EinSchritt_RueckgaengigBitgleich()
    {
        var p = NicoStadt1();
        Assert.True(p.StadtSetzen(256, 128, 6).Geht);
        Assert.Equal((64, 64, 128, 128), Projekt.StadtBereich(p.AussenStand));
        var vorher = (PyObjekt)Py.Kopie(p.AussenStand)!;
        var text = Aussen.TeileText(p.AussenStand);
        var schritte = p.SchritteZurueck;
        // Karte 256 breit: vier Plaetze je Zeile; Platz 6 liegt bei (64, 64), Platz 3 bei (128, 0)
        var vorschau = p.StadtSetzenPruefen(256, 128, 3);
        Assert.True(vorschau.Geht, vorschau.Grund);
        Assert.Equal((64, -64), vorschau.Versatz);
        var plan = p.StadtSetzen(256, 128, 3);
        Assert.Equal(vorschau, plan);
        Assert.Equal(schritte + 1, p.SchritteZurueck);
        Assert.Equal((256d, 128d), Aussen.Groesse(p.AussenStand));
        Assert.Equal((128, 0, 192, 64), Projekt.StadtBereich(p.AussenStand));
        Assert.Equal(3, p.StadtPlatz());
        var (fehler, verschoben) = LageVergleich(vorher, p.AussenStand, 64, -64);
        Assert.True(fehler.Count == 0, string.Join("\n", fehler.Take(20)));
        Assert.True(verschoben > 300, "nur " + verschoben + " Werte verschoben");
        Assert.True(p.Rueckgaengig());
        Assert.Equal(text, Aussen.TeileText(p.AussenStand));
        Assert.True(p.Wiederholen());
        Assert.Equal(3, p.StadtPlatz());
    }

    [Fact]
    public void S373_NachSpeichernUndOeffnen_StadtAufNeuemPlatz()
    {
        var ablage = Daten.NeueAblage();
        var p = NicoStadt1(ablage);
        Assert.True(p.StadtSetzen(128, 128, 4).Geht);
        p.Speichern(true);
        var neu = Projekt.Oeffnen(p.Datei, D, ablage);
        Assert.Equal(Aussen.TeileText(p.AussenStand), Aussen.TeileText(neu.AussenStand));
        Assert.Equal(4, neu.StadtPlatz());
        Assert.Equal((128d, 128d), Aussen.Groesse(neu.AussenStand));
        Assert.True(neu.StadtSetzen(128, 128, 2).Geht);
        neu.Speichern(true);
        var wieder = Projekt.Oeffnen(neu.Datei, D, ablage);
        Assert.Equal(2, wieder.StadtPlatz());
    }

    [Fact]
    public void S373_Gruende_ImmerMitText_NichtsGeaendert()
    {
        var p = NicoStadt1();
        void Nein(int b, int t, int platz, string grund)
        {
            var text = Aussen.TeileText(p.AussenStand);
            var schritte = p.SchritteZurueck;
            var vorschau = p.StadtSetzenPruefen(b, t, platz);
            var plan = p.StadtSetzen(b, t, platz);
            Assert.Equal(vorschau, plan);
            Assert.Equal(grund, plan.Grund);
            Assert.Equal(schritte, p.SchritteZurueck);
            Assert.Equal(text, Aussen.TeileText(p.AussenStand));
        }
        Nein(64, 64, 1, Texte.StadtSchonDort(1));
        Nein(64, 64, 2, Texte.PlatzFehlt(2, 1));
        Nein(96, 64, 1, Texte.VergroessernGroesse);
        Nein(128, 64, 3, Texte.PlatzFehlt(3, 2));
        Assert.True(p.StadtSetzen(128, 128, 2).Geht);
        Nein(64, 128, 1, Texte.VergroessernKleiner);
        Nein(128, 128, 2, Texte.StadtSchonDort(2));
        // Stadt ueber zwei Plaetze: rechts gibt es keinen Platz mehr fuer sie
        var breit = Leer("Breit", 128, 64);
        Setzen(breit, AussenArt.Notiz, 10.5, 10.5);
        Setzen(breit, AussenArt.Notiz, 70.5, 10.5);
        Assert.Equal((0, 0, 128, 64), Projekt.StadtBereich(breit.AussenStand));
        var text = Aussen.TeileText(breit.AussenStand);
        Assert.Equal(Texte.StadtUeberRand(2, 2, 1), breit.StadtSetzen(128, 64, 2).Grund);
        Assert.Equal(text, Aussen.TeileText(breit.AussenStand));
        Assert.True(breit.StadtSetzen(192, 64, 2).Geht);
        Assert.Equal((64, 0, 192, 64), Projekt.StadtBereich(breit.AussenStand));
        // unbekannter Teil: Grund, nichts verschoben
        var u = NicoStadt1();
        u.AussenAendern("unbekannt", a => { Py.Setzen(Aussen.Stadtdaten(a), "neu_unbekannt", new List<object?> { new PyObjekt { { "x", 1L } } }); });
        var ut = Aussen.TeileText(u.AussenStand);
        Assert.Equal(Texte.TeilUnbekannt("stadtdaten/neu_unbekannt"), u.StadtSetzen(128, 64, 2).Grund);
        Assert.Equal(ut, Aussen.TeileText(u.AussenStand));
    }
}
