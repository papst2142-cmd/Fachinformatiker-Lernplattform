using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.2.0 (Weisung P12-334 Punkt 2, Nico: Weg B): Spielstaende werden beim ersten Laden mit A 1.2.0 auf die breiten
/// Grundrisse umgerechnet. Testdaten sind echte Spielstaende der alten Grundrisse (<c>Testdaten/spielstaende_a110.json</c>,
/// einmal mit den Spielregeln und Grundrissen bis A 1.1.0 erzeugt und danach fest: Moebel in allen Raeumen, an den Waenden und in
/// der Diele, ein Spielstand mit zwei eingerichteten Wohnungen). Geprueft: nach der Umrechnung steht jedes Moebel im selben Raum,
/// keines in einer Wand oder Tuer, nichts verloren (Zaehlprobe vorher = nachher); Gegenprobe ohne Umrechnung; ein zweiter Start
/// rechnet nicht erneut um; ohne Kopie keine Umrechnung; ueber die Datenbank nur angehaengte Zeilen und die Kopie vor_update_*.
/// Neu schreiben nur mit <c>LERNPLATTFORM_SPIELSTAENDE_SCHREIBEN=1</c> (nie noetig, die Datei ist ein fester alter Stand).
/// </summary>
public class GrundrissUmrechnungTests
{
    private static string Pfad => Path.Combine(Pfade.Wurzel, "tests", "Lernplattform.Tests", "Testdaten", "spielstaende_a110.json");

    /// <summary>Inhalt bis A 1.1.0 (alte Grundrisse) mit den Moebeln der Laeden, wie das Spiel ihn damals lud.</summary>
    private static readonly Lazy<PyObjekt> InhaltAlt = new(() =>
        Geschaefte.NeueMoebelEinfuegen((PyObjekt)Py.Kopie(SpielInhaltTests.InhaltPython)!, SpielGeschaefteTests.Daten.Value));

    private static PyObjekt InhaltNeu => SpielCharakterTests.InhaltMitNeuen.Value;

    private static GrundrisseBreit.Daten Breit => GrundrisseBreitTests.Daten.Value;

    private static PyObjekt Aussehen() => new() { { "haut", "mittel" }, { "frisur", "locken" }, { "haarfarbe", "braun" }, { "oberteil", "gruen" }, { "kreis", "cyan" } };

    private static Func<string> Uhr()
    {
        var zaehler = 0;
        return () =>
        {
            zaehler += 7;
            return new DateTime(2026, 11, 1).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        };
    }

    // -- Testdaten (einmal erzeugt) --------------------------------------------------------------

    /// <summary>Raeume und Diele eines Wohnungsgrundrisses (Id, Flaeche).</summary>
    private static List<(string Id, double X, double Y, double W, double H)> Bereiche(PyObjekt g) =>
        [.. SpielDaten.Liste(g["raeume"]).Select(SpielDaten.Objekt).Select(r => (Py.Str(r["id"]), Py.F(r["x"]), Py.F(r["y"]), Py.F(r["w"]), Py.F(r["h"]))),
            ("diele", 0.0, Py.F(SpielDaten.Objekt(g["flur"])["y"]), Py.F(g["breite"]), Py.F(SpielDaten.Objekt(g["flur"])["h"]))];

    /// <summary>Ein Spielstand mit alten Regeln: Moebel nach und nach in jeden Raum und in die Diele, an die Waende gerueckt.</summary>
    private static List<object?> Erzeugen(IReadOnlyList<string> wohnungen, int stueckeJeBereich)
    {
        var speicher = new SpielSpeicher(Uhr());
        var a = new Spielablauf(InhaltAlt.Value, speicher);
        a.Profil("Nico", Aussehen());
        a.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", "probe-erspartes" }, { "tag", 1L }, { "richtig", false }, { "geld", 90000L } });
        var laden = Wohnung.Laden(InhaltAlt.Value).Select(m => Py.Str(m["id"])).ToList();
        var n = 0;
        foreach (var wohnung in wohnungen)
        {
            if (!Py.Gleich(a.Stand.Wohnung, wohnung))
            {
                a.Umziehen(wohnung, false);
            }
            var g = SpielDaten.Objekt(SpielKern.Regeln.Bleibe(a.Stand.Wohnung, a.Stand.Inhalt)!["gebaeude"]);
            // Kartons nach dem Umzug: erst die mitgebrachten Moebel aufstellen (jedes Stueck an seinen Platz in einem Raum)
            foreach (var b in Bereiche(g))
            {
                var punkte = new[] { (b.X + b.W / 2, b.Y + 0.3), (b.X + b.W / 2, b.Y + b.H - 0.3), (b.X + 0.3, b.Y + b.H / 2), (b.X + b.W - 0.3, b.Y + b.H / 2), (b.X + b.W / 2, b.Y + b.H / 2) };
                var gestellt = 0;
                foreach (var (cx, cy) in punkte)
                {
                    if (gestellt >= stueckeJeBereich)
                    {
                        break;
                    }
                    for (var versuch = 0; versuch < laden.Count; versuch++)
                    {
                        var moebel = laden[(n + versuch) % laden.Count];
                        var stueck = "m" + (++n).ToString("D4", CultureInfo.InvariantCulture);
                        a.Ausfuehren(SpielAktionen.MoebelKaufen(a.Stand, moebel, stueck));
                        try
                        {
                            a.Ausfuehren(SpielAktionen.AufstellenBei(a.Stand, stueck, cx, cy, (long)(n % 2)));
                            gestellt++;
                            break;
                        }
                        catch (SpielFehler)
                        {
                            a.Ausfuehren(SpielAktionen.MoebelVerkaufen(a.Stand, stueck));
                        }
                    }
                }
            }
        }
        return [.. speicher.Ereignisse().Select(e => (object?)new List<object?> { e.Zeit, e.Art, e.Daten })];
    }

    private static readonly Lazy<List<(string Name, List<object?> Ereignisse)>> Spielstaende = new(() =>
    {
        if (Environment.GetEnvironmentVariable("LERNPLATTFORM_SPIELSTAENDE_SCHREIBEN") == "1")
        {
            var datei = new PyObjekt
            {
                { "_hinweis", new List<object?> { "Welle A 1.2.0 (Weisung P12-334): Spielstaende mit den alten Grundrissen (bis A 1.1.0), einmal erzeugt, danach fest." } },
                { "spielstaende", new List<object?>
                    {
                        new PyObjekt { { "name", "apartment" }, { "ereignisse", Erzeugen(["apartment"], 2) } },
                        new PyObjekt { { "name", "zweizimmer" }, { "ereignisse", Erzeugen(["zweizimmer"], 2) } },
                        new PyObjekt { { "name", "altbau" }, { "ereignisse", Erzeugen(["altbau"], 3) } },
                        new PyObjekt { { "name", "loft" }, { "ereignisse", Erzeugen(["loft"], 3) } },
                        new PyObjekt { { "name", "umzug_apartment_loft" }, { "ereignisse", Erzeugen(["apartment", "loft"], 2) } },
                    }
                },
            };
            File.WriteAllText(Pfad, PythonJson.Standard(datei) + "\n", new System.Text.UTF8Encoding(false));
        }
        var gelesen = SpielDaten.Objekt(Py.Lesen(File.ReadAllBytes(Pfad)));
        return [.. SpielDaten.Liste(gelesen["spielstaende"]).Select(SpielDaten.Objekt)
            .Select(s => (Py.Str(s["name"]), SpielDaten.Liste(s["ereignisse"])))];
    });

    /// <summary>Der Spielstand mit den neuen Inhalten geladen (wie A 1.2.0 ihn beim Start liest).</summary>
    private static (Spielablauf Ablauf, SpielSpeicher Speicher) Laden(List<object?> ereignisse, PyObjekt? inhalt = null)
    {
        var speicher = new SpielSpeicher(Uhr());
        foreach (var e in ereignisse.Cast<List<object?>>())
        {
            speicher.Schreiben((string)e[1]!, PythonJson.Standard(e[2]));
        }
        return (new Spielablauf(inhalt ?? InhaltNeu, speicher), speicher);
    }

    /// <summary>Je Wohnung und Moebelstueck: Bereich, in dem es ganz steht (null = in keinem, also in einer Wand), und Platz.</summary>
    private static Dictionary<(string Wohnung, string Stueck), (string? Bereich, double X, double Y, double W, double H)> Plaetze(Spielstand stand, PyObjekt inhalt)
    {
        var ergebnis = new Dictionary<(string, string), (string?, double, double, double, double)>();
        foreach (var (w, wert) in stand.Einrichtungen)
        {
            var g = SpielDaten.Objekt(SpielKern.Regeln.Bleibe(w, inhalt)!["gebaeude"]);
            foreach (var (stueck, p) in SpielDaten.Objekt(SpielDaten.Objekt(wert)["moebel"]))
            {
                var platz = SpielDaten.Liste(p);
                var m = Wohnung.MoebelStueck(stand.Moebel.Get(stueck), inhalt)!;
                var (bw, bh) = Wohnung.Groesse(m, platz[2]);
                double x = Py.F(platz[0]), y = Py.F(platz[1]), mw = Py.F(bw), mh = Py.F(bh);
                var bereich = Bereiche(g).FirstOrDefault(b => x >= b.X - 1e-6 && y >= b.Y - 1e-6 && x + mw <= b.X + b.W + 1e-6 && y + mh <= b.Y + b.H + 1e-6).Id;
                ergebnis[(Py.Str(w), stueck)] = (bereich, x, y, mw, mh);
            }
        }
        return ergebnis;
    }

    [Fact]
    public void AlteSpielstaende_Umgerechnet_JedesMoebelImSelbenRaum_KeinesInDerWand_NichtsVerloren_Gegenprobe()
    {
        var staende = Spielstaende.Value;
        Assert.Equal(5, staende.Count);
        var moebelGesamt = 0;
        var dieleGesamt = 0;
        var gegenprobe = 0;
        var gerueckt = 0;
        foreach (var (name, ereignisse) in staende)
        {
            // vorher: im alten Spiel (alte Grundrisse) steht jedes Stueck ganz in einem Bereich und ohne Platzproblem
            var (altAblauf, _) = Laden(ereignisse, InhaltAlt.Value);
            var vorher = Plaetze(altAblauf.Stand, InhaltAlt.Value);
            Assert.All(vorher, p => Assert.NotNull(p.Value.Bereich));
            foreach (var m in Wohnung.AufgestellteMoebel(altAblauf.Stand))
            {
                Assert.Equal("", Wohnung.PlatzProblem(altAblauf.Stand, m.Stueck, m.Moebel, m.X, m.Y, m.Dreh));
            }
            Assert.True(vorher.Count >= 8, $"{name}: zu wenige Moebel ({vorher.Count})");
            Assert.Contains(vorher, p => p.Value.Bereich == "diele");

            var (ablauf, speicher) = Laden(ereignisse);
            // Gegenprobe: ohne Umrechnung stehen im breiten Grundriss Moebel nicht mehr in ihrem Raum (oder in einer Wand)
            var ohne = Plaetze(ablauf.Stand, InhaltNeu);
            gegenprobe += ohne.Count(p => p.Value.Bereich != vorher[p.Key].Bereich);

            var zeilen = speicher.Geschrieben.Count;
            var ergebnis = GrundrissUmrechnung.Ausfuehren(ablauf, Breit, () => "vor_update_test.db");
            Assert.Equal(ablauf.Stand.Einrichtungen.Count, ergebnis.Wohnungen);
            Assert.Equal(zeilen + ergebnis.Wohnungen, speicher.Geschrieben.Count);   // nur angehaengt
            Assert.All(speicher.Geschrieben.Skip(zeilen), z => Assert.Equal(Ereignisse.Einrichtung, z.Art));

            // nachher: Zaehlprobe, jedes Stueck im selben Raum an derselben Stelle des Raums, keines in einer Wand oder Tuer
            var nachher = Plaetze(ablauf.Stand, InhaltNeu);
            Assert.Equal(vorher.Keys.OrderBy(k => k), nachher.Keys.OrderBy(k => k));
            foreach (var (k, v) in vorher)
            {
                var n = nachher[k];
                Assert.True(n.Bereich == v.Bereich, $"{name} {k}: {v.Bereich} -> {n.Bereich}");
                // laengs der Wand an derselben Stelle; nur vor einer breiteren Tuer rueckt ein Stueck ein wenig zur Seite
                Assert.Equal(v.W, n.W, 6);
                Assert.True(Math.Abs(n.X - v.X) <= GrundrissUmrechnung.RueckenHoechstens + 1e-6, $"{name} {k}: x {v.X} -> {n.X}");
                gerueckt += Math.Abs(n.X - v.X) > 1e-6 ? 1 : 0;
                moebelGesamt++;
                dieleGesamt += v.Bereich == "diele" ? 1 : 0;
            }
            foreach (var w in ablauf.Stand.Einrichtungen.Select(p => p.Key).ToList())
            {
                ablauf.Stand.Wohnung = w;
                foreach (var m in Wohnung.AufgestellteMoebel(ablauf.Stand))
                {
                    Assert.True(Wohnung.PlatzProblem(ablauf.Stand, m.Stueck, m.Moebel, m.X, m.Y, m.Dreh) == "", $"{name} {w} {m.Stueck} {m.Typ} {m.X}/{m.Y} {m.W}x{m.H}: {Wohnung.PlatzProblem(ablauf.Stand, m.Stueck, m.Moebel, m.X, m.Y, m.Dreh)}");
                }
            }
            // Boeden und Moebelbesitz bleiben
            Assert.Equal(altAblauf.Stand.Moebel.Count, ablauf.Stand.Moebel.Count);
        }
        Assert.True(moebelGesamt >= 60, $"zu wenige Moebel: {moebelGesamt}");
        Assert.True(dieleGesamt >= 5, $"zu wenige Moebel in der Diele: {dieleGesamt}");
        Assert.True(gegenprobe > 0, "Gegenprobe: ohne Umrechnung stehen alle Moebel richtig");
        Assert.True(gerueckt * 10 <= moebelGesamt, $"zu viele Stuecke seitlich gerueckt: {gerueckt} von {moebelGesamt}");
    }

    [Fact]
    public void ZweiterStart_RechnetNichtErneutUm_AuchNachEinemUmbau_OhneKopieKeineUmrechnung()
    {
        var (_, ereignisse) = Spielstaende.Value.Single(s => s.Name == "umzug_apartment_loft");
        // ohne Kopie: nichts geschrieben, Satz fuers Protokoll
        var (ohne, ohneSpeicher) = Laden(ereignisse);
        var zeilen = ohneSpeicher.Geschrieben.Count;
        var keine = GrundrissUmrechnung.Ausfuehren(ohne, Breit, () => null);
        Assert.Equal(0, keine.Wohnungen);
        Assert.Contains("nicht umgerechnet", keine.Protokoll, StringComparison.Ordinal);
        Assert.Equal(zeilen, ohneSpeicher.Geschrieben.Count);
        Assert.Throws<IOException>(() => GrundrissUmrechnung.Ausfuehren(ohne, Breit, () => throw new UnauthorizedAccessException()).Kopie ?? throw new IOException());

        var (ablauf, speicher) = Laden(ereignisse);
        Assert.Equal(2, GrundrissUmrechnung.Ausfuehren(ablauf, Breit, () => "k.db").Wohnungen);
        var einmal = Plaetze(ablauf.Stand, InhaltNeu);
        // zweiter Start: dieselben Zeilen neu gelesen, nichts zu tun
        var alle = speicher.Ereignisse().Select(e => (object?)new List<object?> { e.Zeit, e.Art, e.Daten }).ToList();
        var (zweiter, zweiterSpeicher) = Laden(alle);
        Assert.Empty(GrundrissUmrechnung.Ereignisse(zweiter.Stand, Breit));
        Assert.Equal(0, GrundrissUmrechnung.Ausfuehren(zweiter, Breit, () => throw new InvalidOperationException("keine Kopie noetig")).Wohnungen);
        Assert.Equal(einmal, Plaetze(zweiter.Stand, InhaltNeu));
        // ein Umbau im breiten Grundriss (neues Ereignis ohne Merker) loest keine zweite Umrechnung aus
        var m = Wohnung.AufgestellteMoebel(zweiter.Stand)[0];
        zweiter.Ausfuehren(SpielAktionen.Einpacken(zweiter.Stand, m.Stueck));
        Assert.Empty(GrundrissUmrechnung.Ereignisse(zweiter.Stand, Breit));
        Assert.Equal(alle.Count + 1, zweiterSpeicher.Geschrieben.Count);
        // Gegenprobe: ohne die Daten der breiten Grundrisse gibt es nichts umzurechnen
        Assert.Empty(GrundrissUmrechnung.Ereignisse(Laden(ereignisse).Ablauf.Stand, GrundrisseBreit.Daten.Leer));
    }

    [Fact]
    public void UeberDieDatenbank_KopieVorUpdate_NurAngehaengt_ZweiterStartNichts()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var lernstand = new Lernstand(pfad);
        var schreiber = new LernstandSchreiber(lernstand);
        var (_, ereignisse) = Spielstaende.Value.Single(s => s.Name == "altbau");
        var spiel = new Spielverwaltung(schreiber, "PC", InhaltNeu, sperre: Vorgaenge.Sperren);
        spiel.NeuerLauf(1);
        foreach (var e in ereignisse.Cast<List<object?>>())
        {
            spiel.ZumSchreiben().Log((string)e[1]!, (PyObjekt)Py.Kopie(e[2])!);
        }
        spiel.Neuladen();
        var vorher = Zeilen(lernstand);
        var sicherungen = Path.Combine(ordner.Pfad, Uebernahme.SicherungsOrdner);
        Assert.False(Directory.Exists(sicherungen) && Directory.EnumerateFiles(sicherungen, "vor_update_*").Any());

        var ergebnis = spiel.GrundrisseUmrechnen(Breit);
        Assert.Equal(1, ergebnis.Wohnungen);
        Assert.NotNull(ergebnis.Kopie);
        Assert.True(File.Exists(ergebnis.Kopie));
        Assert.StartsWith(Lernplattform.Core.Update.UpdateAblauf.KopiePraefix, Path.GetFileName(ergebnis.Kopie), StringComparison.Ordinal);
        Assert.Contains("umgerechnet", ergebnis.Protokoll, StringComparison.Ordinal);
        // die Kopie hat den Stand vor der Umrechnung
        Assert.Equal(vorher.Count, Zeilen(new Lernstand(ergebnis.Kopie!)).Count);
        var nachher = Zeilen(lernstand);
        Assert.Equal(vorher, nachher.Take(vorher.Count));
        Assert.Equal(vorher.Count + 1, nachher.Count);

        // zweiter Start (neue Verwaltung ueber derselben Datenbank): keine Umrechnung, keine Kopie
        var neu = new Spielverwaltung(new LernstandSchreiber(new Lernstand(pfad)), "PC", InhaltNeu, sperre: Vorgaenge.Sperren);
        neu.Neuladen();
        var zweiter = neu.GrundrisseUmrechnen(Breit);
        Assert.Equal(0, zweiter.Wohnungen);
        Assert.Null(zweiter.Kopie);
        Assert.Single(Directory.EnumerateFiles(sicherungen, "vor_update_*"));
        Assert.Equal(nachher.Count, Zeilen(lernstand).Count);
    }

    /// <summary>
    /// Belastungsprobe: 40 zufaellige alte Spielstaende (fester Startwert; Moebel an zufaelligen Stellen in allen Wohnungen, Umzuege
    /// dazwischen) werden umgerechnet; danach zufaellige Umbauten und ein Neuladen. Nie eine Ausnahme, nichts verloren, jedes Stueck
    /// im selben Bereich, kein Platzproblem, das es vorher nicht gab, und nie eine zweite Umrechnung.
    /// </summary>
    [Fact]
    public void Belastungsprobe_ZufaelligeAlteSpielstaende_UmrechnenUmbauenNeuladen()
    {
        var zufall = new Random(1202);
        var wohnungen = SpielDaten.Liste(SpielDaten.Objekt(InhaltAlt.Value["wohnungen"])["wohnungen"]).Select(w => Py.Str(SpielDaten.Objekt(w)["id"])).ToList();
        var laden = Wohnung.Laden(InhaltAlt.Value).Select(m => Py.Str(m["id"])).ToList();
        var umgerechnet = 0;
        for (var runde = 0; runde < 40; runde++)
        {
            var speicher = new SpielSpeicher(Uhr());
            var a = new Spielablauf(InhaltAlt.Value, speicher);
            a.Profil("Probe", Aussehen());
            a.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", "probe-erspartes" }, { "tag", 1L }, { "richtig", false }, { "geld", 900000L } });
            var n = 0;
            foreach (var w in wohnungen.OrderBy(_ => zufall.Next()).Take(1 + zufall.Next(3)).OrderBy(wohnungen.IndexOf))
            {
                if (!Py.Gleich(a.Stand.Wohnung, w))
                {
                    a.Umziehen(w, false);
                }
                var g = SpielDaten.Objekt(Wohnung.Bleibe(w, a.Stand.Inhalt)!["gebaeude"]);
                for (var i = 0; i < 12; i++)
                {
                    var stueck = "z" + (++n).ToString("D4", CultureInfo.InvariantCulture);
                    a.Ausfuehren(SpielAktionen.MoebelKaufen(a.Stand, laden[zufall.Next(laden.Count)], stueck));
                    try
                    {
                        a.Ausfuehren(SpielAktionen.AufstellenBei(a.Stand, stueck, zufall.NextDouble() * Py.F(g["breite"]), zufall.NextDouble() * Py.F(g["hoehe"]), (long)zufall.Next(4)));
                    }
                    catch (SpielFehler)
                    {
                        // passt nicht: bleibt im Karton (wie im Spiel)
                    }
                }
            }
            var ereignisse = speicher.Ereignisse().Select(e => (object?)new List<object?> { e.Zeit, e.Art, e.Daten }).ToList();
            var (alt, _) = Laden(ereignisse, InhaltAlt.Value);
            var vorher = Plaetze(alt.Stand, InhaltAlt.Value);
            var (ablauf, neuSpeicher) = Laden(ereignisse);
            umgerechnet += GrundrissUmrechnung.Ausfuehren(ablauf, Breit, () => "k.db").Wohnungen;
            var nachher = Plaetze(ablauf.Stand, InhaltNeu);
            Assert.Equal(vorher.Keys.OrderBy(k => k), nachher.Keys.OrderBy(k => k));
            Assert.All(vorher, p => Assert.True(p.Value.Bereich == nachher[p.Key].Bereich, $"Runde {runde} {p.Key}: {p.Value.Bereich} -> {nachher[p.Key].Bereich}"));
            foreach (var w in ablauf.Stand.Einrichtungen.Select(p => p.Key).ToList())
            {
                ablauf.Stand.Wohnung = w;
                alt.Stand.Wohnung = w;
                var altOk = Wohnung.AufgestellteMoebel(alt.Stand).Where(m => Wohnung.PlatzProblem(alt.Stand, m.Stueck, m.Moebel, m.X, m.Y, m.Dreh) == "").Select(m => m.Stueck).ToHashSet();
                foreach (var m in Wohnung.AufgestellteMoebel(ablauf.Stand).Where(m => altOk.Contains(m.Stueck)))
                {
                    Assert.True(Wohnung.PlatzProblem(ablauf.Stand, m.Stueck, m.Moebel, m.X, m.Y, m.Dreh) == "", $"Runde {runde} {w} {m.Stueck} {m.Typ}");
                }
            }
            // Umbauen im breiten Grundriss, dann neu laden: keine zweite Umrechnung
            ablauf.Stand.Wohnung = alt.Stand.Wohnung = Laden(ereignisse).Ablauf.Stand.Wohnung;
            foreach (var m in Wohnung.AufgestellteMoebel(ablauf.Stand).Take(3))
            {
                ablauf.Ausfuehren(SpielAktionen.Einpacken(ablauf.Stand, m.Stueck));
            }
            var (neu, _) = Laden([.. neuSpeicher.Ereignisse().Select(e => (object?)new List<object?> { e.Zeit, e.Art, e.Daten })]);
            Assert.Empty(GrundrissUmrechnung.Ereignisse(neu.Stand, Breit));
        }
        Assert.True(umgerechnet >= 40, $"zu wenige Wohnungen umgerechnet: {umgerechnet}");
    }

    private static List<string> Zeilen(Lernstand lernstand)
    {
        using var v = lernstand.Verbindung();
        using var b = v.CreateCommand();
        b.CommandText = "SELECT id, typ, daten FROM spiel_ereignisse ORDER BY id";
        var liste = new List<string>();
        using var r = b.ExecuteReader();
        while (r.Read())
        {
            liste.Add(r.GetInt64(0).ToString(CultureInfo.InvariantCulture) + "|" + r.GetString(1) + "|" + r.GetString(2));
        }
        return liste;
    }
}
