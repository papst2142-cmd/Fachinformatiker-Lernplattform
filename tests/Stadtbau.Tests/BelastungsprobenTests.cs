using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Belastungsproben als feste Tests (Weisung P12-201, Vorschlag E1 aus Meldung P12-119). Grundlage sind die lokalen Proben der
/// Gesamtpruefung P12-199; hier mit festem Samen, damit jeder Lauf dieselben Stellen prueft. Vier Proben:
/// <list type="number">
/// <item>Jede Aktion an jedem Stueck draussen (Stadt 1, Nicos Teststadt, Stadt mit Gelaende): kein Absturz, Pruefung und 3D bauen,
/// Rueckgaengig gibt genau den Ausgangsstand, ein angebotenes Drehen/Spiegeln/Loeschen wirkt auch.</item>
/// <item>Jede Art an festen Zufallsstellen: kein Absturz und „Werkzeug gruen =&gt; Spiel laedt“.</item>
/// <item>Huegel und Pinsel an festen Zufallsstellen: Pruefung, Grenzen, Rueckgaengig, Drehen nur wo es wirkt, Gruppe dreht wie einzeln.</item>
/// <item>Jedes Stueck jedes Innenraums: kein Absturz, Rueckgaengig genau.</item>
/// </list>
/// Jede Probe hat eine Gegenprobe: mit dem alten Verhalten (Schalter *AltFuerTests) oder mit einem absichtlich eingebauten Fehler
/// muss sie einen Fund melden.
/// </summary>
public class BelastungsprobenTests
{
    private static Spieldaten D => Daten.Spiel;

    private static byte[] NicoDatei => File.ReadAllBytes(Path.Combine(Daten.Wurzel, "tests", "Stadtbau.Tests", "Testdaten", "nico", "teststadt_S0.40.0.fistadt"));

    /// <summary>Die Staedte der Proben: Stadt 1 (Fassung 0), Nicos Teststadt S 0.40.0, Stadt 1 mit Huegeln und einem Pinselzug.</summary>
    internal static Projekt Laden(string stadt)
    {
        var p = stadt switch
        {
            "stadt1" or "gelaende" => Projekt.Neu("Belastung " + stadt, D, Daten.NeueAblage()),
            "nico" => Projekt.AusStadtDatei(NicoDatei, D, Daten.NeueAblage(), "Belastung nico"),
            _ => throw new ArgumentOutOfRangeException(nameof(stadt)),
        };
        if (stadt == "gelaende")
        {
            // fester Platz auf freier Wiese (Fassung 0, GelaendePlatz), ein runder und ein laenglicher Huegel und ein Pinselzug
            p.AussenAendern("Huegel rund", a => AussenBearbeitung.Setzen(a, AussenArt.Huegel, 4, 4));
            p.AussenAendern("Huegel lang", a =>
            {
                var s = AussenBearbeitung.Setzen(a, AussenArt.Huegel, 9, 4);
                if (s is not null)
                {
                    AussenBearbeitung.HuegelWerteSetzen(a, s, 1.5, 0.6, true, 4);
                }
                return s;
            });
            p.AussenAendern("Pinsel", a => AussenBearbeitung.Pinselzug(a, PinselArt.Heben, [new(14, 4), new(15, 4.5f), new(16, 4)], 1));
        }
        return p;
    }

    /// <summary>
    /// Stand draussen als Text zum Vergleich. Fund 306 (offen, Vorschlag E7): nach Rueckgaengig kann ein leerer Teil
    /// <c>laternen_auto: {}</c> stehen bleiben, wo vorher keiner war; beide Seiten werden darum gleich behandelt (leerer Teil = kein
    /// Teil). Jede andere Abweichung bleibt ein Fund.
    /// </summary>
    internal static string Vergleich(PyObjekt aussen)
    {
        var k = (PyObjekt)Py.Kopie(aussen)!;
        k.RemoveAll(e => e.Key == LaternenAutomatik.Teil && e.Value is PyObjekt o && o.Count == 0);
        return PythonJson.Standard(k);
    }

    private static string Kurz(Exception e) => $"{e.GetType().Name} {e.Message} {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}";

    // ---- 1. Jede Aktion an jedem Stueck draussen ------------------------------------------------------------------------

    private static readonly (string Name, Func<PyObjekt, AussenStelle, object?> Tun, Func<PyObjekt, AussenStelle, string?>? Nicht)[] Aktionen =
    [
        ("drehen90", (a, s) => AussenBearbeitung.Drehen(a, s), (a, s) => AussenBearbeitung.DrehenNicht(a, s)),
        ("drehen15", (a, s) => AussenBearbeitung.Drehen(a, s, 15), null),
        ("schieben", (a, s) => { AussenBearbeitung.Verschieben(a, s, 1, 0.5); return true; }, null),
        ("kopieren", (a, s) => AussenBearbeitung.Kopieren(a, s), null),
        ("loeschen", (a, s) => AussenBearbeitung.Loeschen(a, s), null),
        ("spiegeln", (a, s) => AussenBearbeitung.Spiegeln(a, s), (a, s) => AussenBearbeitung.SpiegelnNicht(a, s)),
    ];

    /// <summary>
    /// Fuehrt jede Aktion an jeder Stelle aus und nimmt sie wieder zurueck. Jede <paramref name="bauJede"/>-te Aktion baut danach
    /// Pruefung und 3D-Ansicht. <paramref name="zwischendurch"/> laeuft nach jeder Aktion vor dem Rueckgaengig (nur Gegenprobe).
    /// </summary>
    internal static List<string> AktionenProbe(Projekt p, IEnumerable<AussenStelle> stellen, int bauJede, Action<Projekt>? zwischendurch = null)
    {
        var funde = new List<string>();
        var start = Vergleich(p.AussenStand);
        var n = 0;
        foreach (var s in stellen)
        {
            foreach (var (name, tun, nicht) in Aktionen)
            {
                var wo = $"{s.Art} Nr. {s.Nummer + 1}/{s.Punkt} {name}";
                var vorher = p.SchritteZurueck;
                string? grund = null;
                try
                {
                    grund = nicht?.Invoke(p.AussenStand, s);
                    p.AussenAendern(name, a => tun(a, s));
                }
                catch (Exception e)
                {
                    funde.Add($"{wo}: {Kurz(e)}");
                }
                // angeboten (kein Grund) heisst: die Aktion wirkt (Sammelliste 303: Knopf „Drehen“ am runden Huegel tat nichts)
                if (nicht is not null && grund is null && p.SchritteZurueck == vorher)
                {
                    funde.Add($"{wo}: angeboten, aber ohne Wirkung");
                }
                if (bauJede > 0 && n++ % bauJede == 0)
                {
                    try
                    {
                        var st = p.Stand();
                        _ = Projekt.PruefenAussen(st, D);
                        _ = Ansicht3D.Bauen(st, D, Ansicht3D.Einstellung.Standard).Szene.Laternen.Count;
                    }
                    catch (Exception e)
                    {
                        funde.Add($"{wo} Pruefung/3D: {Kurz(e)}");
                    }
                }
                zwischendurch?.Invoke(p);
                while (p.SchritteZurueck > vorher)
                {
                    p.Rueckgaengig();
                }
                var jetzt = Vergleich(p.AussenStand);
                if (jetzt != start)
                {
                    funde.Add($"{wo}: Rueckgaengig gibt nicht den Ausgangsstand");
                    start = jetzt;
                }
            }
        }
        return funde;
    }

    [Theory]
    [InlineData("stadt1")]
    [InlineData("nico")]
    [InlineData("gelaende")]
    public void JedeAktion_JedesStueck_RueckgaengigGenau(string stadt)
    {
        var p = Laden(stadt);
        var stellen = AussenBearbeitung.Alle(p.AussenStand).Select(z => z.Stelle).ToList();
        Assert.True(stellen.Count >= 50, $"{stadt}: nur {stellen.Count} Stellen");
        var funde = AktionenProbe(p, stellen, bauJede: 7);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    /// <summary>Gegenprobe Fund 305: mit dem alten Bau der 3D-Ansicht (S 0.42.0) bricht der Bau ab, die Probe meldet es.</summary>
    [Fact]
    public void Gegenprobe_Aktionen_LaterneAlt_MeldetAbbruch()
    {
        var p = Laden("nico");
        Assert.Empty(AktionenProbe(p, [new AussenStelle(AussenArt.Laterne, 10)], bauJede: 1));
        Ansicht3D.LaterneAltFuerTests = true;
        try
        {
            Assert.Contains(AktionenProbe(p, [new AussenStelle(AussenArt.Laterne, 10)], bauJede: 1), f => f.Contains("schieben Pruefung/3D", StringComparison.Ordinal));
        }
        finally
        {
            Ansicht3D.LaterneAltFuerTests = false;
        }
    }

    /// <summary>Gegenprobe Sammelliste 303: mit der alten Regel bietet der runde Huegel „Drehen“ an, das nichts tut.</summary>
    [Fact]
    public void Gegenprobe_Aktionen_RunderHuegelAlt_MeldetDrehenOhneWirkung()
    {
        var p = Laden("gelaende");
        var rund = new AussenStelle(AussenArt.Huegel, 0);
        Assert.Empty(AktionenProbe(p, [rund], bauJede: 0));
        AussenBearbeitung.RunderHuegelAltFuerTests = true;
        try
        {
            Assert.Contains(AktionenProbe(p, [rund], bauJede: 0), f => f.Contains("drehen90: angeboten, aber ohne Wirkung", StringComparison.Ordinal));
        }
        finally
        {
            AussenBearbeitung.RunderHuegelAltFuerTests = false;
        }
    }

    /// <summary>Gegenprobe des Vergleichs: eine Aenderung neben dem Verlauf (Rueckgaengig kann sie nicht zuruecknehmen) wird gemeldet.</summary>
    [Fact]
    public void Gegenprobe_Aktionen_AenderungNebenDemVerlauf_WirdGemeldet()
    {
        var p = Laden("stadt1");
        var baum = AussenBearbeitung.Alle(p.AussenStand).Select(z => z.Stelle).First(s => s.Art == AussenArt.Baum);
        Assert.Empty(AktionenProbe(p, [baum], bauJede: 0));
        var funde = AktionenProbe(p, [baum], bauJede: 0, zwischendurch: q => AussenBearbeitung.Verschieben(q.AussenStand, baum, 0.5, 0));
        Assert.Contains(funde, f => f.Contains("Rueckgaengig gibt nicht den Ausgangsstand", StringComparison.Ordinal));
    }

    // ---- 2. Jede Art an festen Zufallsstellen: Werkzeug gruen => Spiel laedt --------------------------------------------

    /// <summary>
    /// Laedt die Stadt wie das Spiel (Karte, Stadt, 3D streng wie im Spiel: eine Laterne ohne Platz bricht ab, Ladepruefung
    /// <see cref="StadtbauPruefung.Verbaut"/> ohne Katalog-Funde, weil die Probe keine Stuecke einbaut). null = laedt.
    /// </summary>
    internal static string? SpielLaedt(StadtFassung st)
    {
        var alt = Ansicht3D.LaterneAltFuerTests;
        try
        {
            _ = st.StadtFuer(st.MitKarte(D.Inhalt));
            Ansicht3D.LaterneAltFuerTests = true;
            _ = Ansicht3D.Bauen(st, D, Ansicht3D.Einstellung.Grenzen).Szene.Laternen.Count;
            var v = StadtbauPruefung.Verbaut(st, D.Grafik).Where(t => !t.Contains("Katalog", StringComparison.OrdinalIgnoreCase)).ToList();
            return v.Count > 0 ? "Ladepruefung: " + string.Join(" | ", v) : null;
        }
        catch (Exception e)
        {
            return "Spiel: " + Kurz(e);
        }
        finally
        {
            Ansicht3D.LaterneAltFuerTests = alt;
        }
    }

    private static HashSet<string> Rot(StadtFassung st, string? ohneRegel) =>
        [.. Projekt.PruefenAussen(st, D).Where(f => f.Rot && (ohneRegel is null || !f.Text.Contains(ohneRegel, StringComparison.Ordinal))).Select(f => f.Text)];

    private static readonly (AussenArt Art, string? Modell)[] Arten =
    [
        (AussenArt.Strasse, null), (AussenArt.Baum, null), (AussenArt.Park, null), (AussenArt.Beet, null), (AussenArt.Laterne, null),
        (AussenArt.Kurve, "bogen:90:2"), (AussenArt.Kreisverkehr, "kreisverkehr:2"), (AussenArt.Fluss, null), (AussenArt.Teich, null),
        (AussenArt.Steg, null), (AussenArt.Fussweg, null), (AussenArt.Bahn, null), (AussenArt.Zebrastreifen, null), (AussenArt.Ampel, null),
        (AussenArt.Haltestelle, null), (AussenArt.Parkplatz, null), (AussenArt.Huegel, null),
    ];

    /// <summary>
    /// Setzt jede Art an <paramref name="jeArt"/> festen Zufallsstellen (Samen <paramref name="samen"/>, Raster 0,5). Nach jedem
    /// Setzen: Pruefung, 3D und Grenzen ohne Absturz; ist die Pruefung gruen (kein neuer roter Fund gegenueber dem Ausgang), muss
    /// das Spiel die Stadt laden. <paramref name="vorher"/> laeuft vor jedem Setzen (nur Gegenprobe), <paramref name="ohneRegel"/>
    /// blendet eine Regel der Pruefung aus (nur Gegenprobe). <paramref name="teil"/> 1 oder 2 = nur jede zweite Art (Teile laufen
    /// nebeneinander), 0 = alle.
    /// </summary>
    internal static List<string> SetzenProbe(Projekt p, int jeArt, int samen, Action<PyObjekt>? vorher = null, string? ohneRegel = null, int teil = 0)
    {
        var funde = new List<string>();
        var rot0 = Rot(p.Stand(), ohneRegel);
        var spiel0 = SpielLaedt(p.Stand());
        Assert.True(spiel0 is null, $"Ausgangsstadt laedt nicht: {spiel0}");
        var (breite, hoehe) = Aussen.Groesse(p.AussenStand);
        var gesetzt = 0;
        var arten = Arten.Where((_, i) => teil == 0 || i % 2 == teil - 1).ToList();
        foreach (var (art, modell) in arten)
        {
            // je Art ein eigener fester Samen: jede Art prueft dieselben Stellen, egal welcher Teil laeuft
            var rnd = new Random(samen * 100 + (int)art);
            for (var k = 0; k < jeArt; k++)
            {
                var x = Math.Round(rnd.NextDouble() * breite * 2) / 2;
                var y = Math.Round(rnd.NextDouble() * hoehe * 2) / 2;
                var wo = $"{art} ({x}; {y})";
                var schritte = p.SchritteZurueck;
                AussenStelle? s = null;
                try
                {
                    s = p.AussenAendern("setzen", a =>
                    {
                        vorher?.Invoke(a);
                        return AussenBearbeitung.Setzen(a, art, x, y, true, modell);
                    });
                }
                catch (Exception e)
                {
                    funde.Add($"{wo} setzen: {Kurz(e)}");
                }
                if (s is not null)
                {
                    gesetzt++;
                    var st = p.Stand();
                    try
                    {
                        if (Rot(st, ohneRegel).IsSubsetOf(rot0) && SpielLaedt(st) is { } f)
                        {
                            funde.Add($"{wo}: Werkzeug gruen, aber {f}");
                        }
                        // 3D-Ansicht und Grenzen live an jedem vierten gesetzten Stueck (die Testzeit bleibt klein)
                        if (gesetzt % 4 == 1)
                        {
                            _ = Ansicht3D.Bauen(st, D, Ansicht3D.Einstellung.Standard).Szene.Laternen.Count;
                            _ = GrenzenLive.Fuer(st, D);
                        }
                    }
                    catch (Exception e)
                    {
                        funde.Add($"{wo} Pruefung/3D/Grenzen: {Kurz(e)}");
                    }
                }
                while (p.SchritteZurueck > schritte)
                {
                    p.Rueckgaengig();
                }
            }
        }
        Assert.True(gesetzt * 2 >= arten.Count * jeArt, $"nur {gesetzt} Stuecke gesetzt");
        return funde;
    }


    /// <summary>
    /// Gegenprobe „Werkzeug gruen =&gt; Spiel laedt“ (Fund 305: Laterne Nr. 11 in Nicos Teststadt um (1; 0,5) verschoben): fehlen
    /// der Pruefung die roten Funde zu dieser Laterne („findet keinen freien Platz“, „steht auf der Insel“), haelt das Werkzeug die
    /// Stadt fuer gruen, das Spiel laedt sie nicht.
    /// </summary>
    [Fact]
    public void Gegenprobe_Setzen_OhneLaternenRegel_MeldetSpielLaedtNicht()
    {
        void Verschieben(PyObjekt a)
        {
            if (Aussen.Laternen(a)[10] is List<object?> l && Py.F(l[0]) < 32)
            {
                AussenBearbeitung.Verschieben(a, new AussenStelle(AussenArt.Laterne, 10), 1, 0.5);
            }
        }
        var mit = SetzenProbe(Laden("nico"), jeArt: 1, samen: 7, Verschieben);
        Assert.DoesNotContain(mit, f => f.Contains("Werkzeug gruen", StringComparison.Ordinal));
        var ohne = SetzenProbe(Laden("nico"), jeArt: 1, samen: 7, Verschieben, ohneRegel: "Laterne Nr. 11 ");
        Assert.Contains(ohne, f => f.Contains("Werkzeug gruen, aber Spiel", StringComparison.Ordinal));
    }

    // ---- 3. Huegel und Pinsel -------------------------------------------------------------------------------------------

    /// <summary>
    /// <paramref name="runden"/> Huegel und Pinselzuege an festen Zufallsstellen (auch ueber den Rand). Nach jeder Aenderung:
    /// Pruefung und Grenzen ohne Absturz; je Huegel: Drehen nur angeboten, wo es wirkt, Gruppe aus dem Huegel und einem Baum dreht
    /// den Huegel wie einzeln; am Ende Rueckgaengig bis zum Ausgang.
    /// </summary>
    internal static List<string> GelaendeProbe(Projekt p, int runden, int samen)
    {
        var funde = new List<string>();
        var start = Vergleich(p.AussenStand);
        var (breite, hoehe) = Aussen.Groesse(p.AussenStand);
        var rnd = new Random(samen);
        for (var runde = 0; runde < runden; runde++)
        {
            var x = rnd.NextDouble() * (breite + 2) - 1;
            var y = rnd.NextDouble() * (hoehe + 2) - 1;
            var art = (PinselArt)(runde % 3);
            AussenStelle? huegel = null;
            try
            {
                if (runde % 2 == 0)
                {
                    var (r, h, lang, l) = (1 + rnd.Next(5) * 0.5, 0.2 + rnd.Next(14) * 0.1, rnd.Next(2) == 0, 2 + rnd.Next(9) * 0.5);
                    huegel = p.AussenAendern("Huegel", a =>
                    {
                        var s = AussenBearbeitung.Setzen(a, AussenArt.Huegel, x, y);
                        if (s is not null)
                        {
                            AussenBearbeitung.HuegelWerteSetzen(a, s, r, h, lang, l);
                        }
                        return s;
                    });
                }
                else
                {
                    var zug = Enumerable.Range(0, 8).Select(i => new Vector2((float)(x + i * 0.4), (float)(y + Math.Sin(i) * 0.6))).ToList();
                    var groesse = AussenBearbeitung.PinselGroessen[rnd.Next(AussenBearbeitung.PinselGroessen.Count)];
                    p.AussenAendern("Pinsel", a => AussenBearbeitung.Pinselzug(a, PinselArt.Heben, zug, groesse));
                    if (art != PinselArt.Heben)
                    {
                        p.AussenAendern("Pinsel 2", a => AussenBearbeitung.Pinselzug(a, art, zug, 1));
                    }
                }
            }
            catch (Exception e)
            {
                funde.Add($"Runde {runde} aendern: {Kurz(e)}");
            }
            if (huegel is { } hs)
            {
                funde.AddRange(HuegelDrehenPruefen(p.AussenStand, hs, x, y).Select(f => $"Runde {runde}: {f}"));
            }
            var st = p.Stand();
            try
            {
                _ = Projekt.PruefenAussen(st, D);
                _ = GrenzenLive.Fuer(st, D);
            }
            catch (Exception e)
            {
                funde.Add($"Runde {runde} Pruefung/Grenzen: {Kurz(e)}");
            }
        }
        if (AussenBearbeitung.HuegelListe(p.AussenStand).Count == 0)
        {
            funde.Add("kein Huegel gesetzt");
        }
        while (p.Rueckgaengig())
        {
        }
        if (Vergleich(p.AussenStand) != start)
        {
            funde.Add("Rueckgaengig bis zum Anfang gibt nicht den Ausgangsstand");
        }
        return funde;
    }

    /// <summary>Drehen am Huegel (auf Kopien): angeboten = wirkt; die Gruppe aus Huegel und Baum dreht den Huegel wie einzeln.</summary>
    private static List<string> HuegelDrehenPruefen(PyObjekt aussen, AussenStelle huegel, double x, double y)
    {
        var funde = new List<string>();
        var einzeln = Daten.Kopie(aussen);
        var vor = AussenBearbeitung.HuegelWerte(einzeln, huegel);
        var angeboten = AussenBearbeitung.DrehenNicht(einzeln, huegel) is null;
        var gedreht = AussenBearbeitung.Drehen(einzeln, huegel);
        var nach = AussenBearbeitung.HuegelWerte(einzeln, huegel);
        if (angeboten && (!gedreht || nach == vor))
        {
            funde.Add($"Huegel Nr. {huegel.Nummer + 1}: Drehen angeboten, aber ohne Wirkung");
        }
        if (!vor.Laenglich)
        {
            return funde;
        }
        var gruppe = Daten.Kopie(aussen);
        var baum = AussenBearbeitung.Setzen(gruppe, AussenArt.Baum, Math.Round(x) + 0.5, Math.Round(y) + 0.5);
        if (baum is null)
        {
            return funde;
        }
        _ = AussenBearbeitung.Drehen(gruppe, [huegel, baum]);
        var inGruppe = AussenBearbeitung.HuegelWerte(gruppe, huegel);
        if (inGruppe.Dreh != nach.Dreh)
        {
            funde.Add($"Huegel Nr. {huegel.Nummer + 1}: in der Gruppe Richtung {inGruppe.Dreh}, einzeln {nach.Dreh}");
        }
        return funde;
    }

    // ---- 4. Jedes Stueck jedes Innenraums -------------------------------------------------------------------------------

    private static readonly (string Name, Action<PyObjekt, Stelle> Tun)[] InnenAktionen =
    [
        ("schieben", (g, s) => Bearbeitung.Verschieben(g, s, 1, 0.5)),
        ("drehen", (g, s) => Bearbeitung.Drehen(g, s)),
        ("kopieren", (g, s) => Bearbeitung.Kopieren(g, s)),
        ("loeschen", (g, s) => Bearbeitung.Loeschen(g, s)),
    ];

    /// <summary>Jede Aktion an jedem Deko-Stueck jedes Innenorts, Pruefung jede fuenfte, Rueckgaengig genau.</summary>
    internal static List<string> InnenProbe(Projekt p, IEnumerable<string> orte, Action<Projekt>? nachRueckgaengig = null)
    {
        var funde = new List<string>();
        var n = 0;
        foreach (var ort in orte)
        {
            var g0 = p.Grundriss(ort);
            var start = PythonJson.Standard(g0);
            var stellen = new List<Stelle>();
            foreach (var b in new[] { "flur" }.Concat(Liste(g0["raeume"]).OfType<PyObjekt>().Select(r => Py.Str(r["id"]))))
            {
                if (Bearbeitung.Bereich(g0, b) is { } bereich && Py.Hat(bereich, "deko"))
                {
                    stellen.AddRange(Enumerable.Range(0, Liste(bereich["deko"]).Count).Select(i => new Stelle(b, i)));
                }
            }
            if (stellen.Count == 0)
            {
                funde.Add($"{ort}: keine Stuecke");
            }
            foreach (var s in stellen)
            {
                foreach (var (name, tun) in InnenAktionen)
                {
                    var vorher = p.SchritteZurueck;
                    try
                    {
                        p.Aendern(ort, name, g => tun(g, s));
                    }
                    catch (Exception e)
                    {
                        funde.Add($"{ort} {s} {name}: {Kurz(e)}");
                    }
                    if (n++ % 5 == 0)
                    {
                        try
                        {
                            _ = p.Pruefen(ort);
                        }
                        catch (Exception e)
                        {
                            funde.Add($"{ort} {s} {name} Pruefung: {Kurz(e)}");
                        }
                    }
                    while (p.SchritteZurueck > vorher)
                    {
                        p.Rueckgaengig();
                    }
                    nachRueckgaengig?.Invoke(p);
                    var jetzt = PythonJson.Standard(p.Grundriss(ort));
                    if (jetzt != start)
                    {
                        funde.Add($"{ort} {s} {name}: Rueckgaengig gibt nicht den Ausgangsstand");
                        start = jetzt;
                    }
                }
            }
        }
        return funde;
    }

}

/// <summary>Belastungsprobe 2, Stadt 1, Teil 1 der Arten (eigene Klasse: die Teile laufen nebeneinander).</summary>
public class BelastungSetzenStadt1Teil1Tests
{
    [Fact]
    public void JedeArt_FesteZufallsstellen_WerkzeugGruenSpielLaedt()
    {
        var funde = BelastungsprobenTests.SetzenProbe(BelastungsprobenTests.Laden("stadt1"), jeArt: 20, samen: 7, teil: 1);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }
}

/// <summary>Belastungsprobe 2, Stadt 1, Teil 2 der Arten (eigene Klasse: die Teile laufen nebeneinander).</summary>
public class BelastungSetzenStadt1Teil2Tests
{
    [Fact]
    public void JedeArt_FesteZufallsstellen_WerkzeugGruenSpielLaedt()
    {
        var funde = BelastungsprobenTests.SetzenProbe(BelastungsprobenTests.Laden("stadt1"), jeArt: 20, samen: 7, teil: 2);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }
}

/// <summary>Belastungsprobe 2, Nicos Teststadt, Teil 1 der Arten (eigene Klasse: die Teile laufen nebeneinander).</summary>
public class BelastungSetzenNicoTeil1Tests
{
    [Fact]
    public void JedeArt_FesteZufallsstellen_WerkzeugGruenSpielLaedt()
    {
        var funde = BelastungsprobenTests.SetzenProbe(BelastungsprobenTests.Laden("nico"), jeArt: 20, samen: 7, teil: 1);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }
}

/// <summary>Belastungsprobe 2, Nicos Teststadt, Teil 2 der Arten (eigene Klasse: die Teile laufen nebeneinander).</summary>
public class BelastungSetzenNicoTeil2Tests
{
    [Fact]
    public void JedeArt_FesteZufallsstellen_WerkzeugGruenSpielLaedt()
    {
        var funde = BelastungsprobenTests.SetzenProbe(BelastungsprobenTests.Laden("nico"), jeArt: 20, samen: 7, teil: 2);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }
}


/// <summary>Belastungsprobe 3 (Huegel und Pinsel) mit Gegenproben.</summary>
public class BelastungGelaendeTests
{
    [Theory]
    [InlineData("stadt1")]
    [InlineData("nico")]
    public void HuegelUndPinsel_FesteZufallsstellen(string stadt)
    {
        var funde = BelastungsprobenTests.GelaendeProbe(BelastungsprobenTests.Laden(stadt), runden: 30, samen: 11);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    /// <summary>Gegenproben Sammelliste 303 und Fund 304: mit den alten Regeln meldet die Gelaende-Probe beides.</summary>
    [Fact]
    public void Gegenprobe_Gelaende_AlteRegeln_WerdenGemeldet()
    {
        AussenBearbeitung.RunderHuegelAltFuerTests = true;
        try
        {
            Assert.Contains(BelastungsprobenTests.GelaendeProbe(BelastungsprobenTests.Laden("stadt1"), runden: 30, samen: 11), f => f.Contains("Drehen angeboten, aber ohne Wirkung", StringComparison.Ordinal));
        }
        finally
        {
            AussenBearbeitung.RunderHuegelAltFuerTests = false;
        }
        AussenBearbeitung.GruppeHuegelAltFuerTests = true;
        try
        {
            Assert.Contains(BelastungsprobenTests.GelaendeProbe(BelastungsprobenTests.Laden("stadt1"), runden: 30, samen: 11), f => f.Contains("in der Gruppe Richtung", StringComparison.Ordinal));
        }
        finally
        {
            AussenBearbeitung.GruppeHuegelAltFuerTests = false;
        }
    }

}

/// <summary>Belastungsprobe 4 (Innenraeume) mit Gegenprobe.</summary>
public class BelastungInnenTests
{
    [Fact]
    public void JedesStueckInnen_RueckgaengigGenau()
    {
        var p = BelastungsprobenTests.Laden("stadt1");
        var orte = p.Innenorte.ToList();
        Assert.True(orte.Count >= 5, $"nur {orte.Count} Innenorte");
        var funde = BelastungsprobenTests.InnenProbe(p, orte);
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    /// <summary>Gegenprobe Innenraum: nimmt Rueckgaengig einen Schritt nicht ganz zurueck (hier: gleich wiederholt), meldet die Probe es.</summary>
    [Fact]
    public void Gegenprobe_Innen_RueckgaengigUnvollstaendig_WirdGemeldet()
    {
        var p = BelastungsprobenTests.Laden("stadt1");
        var ort = p.Innenorte.First();
        Assert.DoesNotContain(BelastungsprobenTests.InnenProbe(p, [ort]), f => !f.Contains("keine Stuecke", StringComparison.Ordinal));
        var funde = BelastungsprobenTests.InnenProbe(p, [ort], q => q.Wiederholen());
        Assert.Contains(funde, f => f.Contains("Rueckgaengig gibt nicht den Ausgangsstand", StringComparison.Ordinal));
    }
}
