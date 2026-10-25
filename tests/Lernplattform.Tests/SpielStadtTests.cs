using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, Weisung P7-6: der Stadt-Kern ohne Darstellung (<see cref="Stadt"/>).
/// Wie Python: welche Orte auf der Karte sind und wie sie heissen (Vergleichswerte G48 Teil "Stadt",
/// <c>spiel_stadt.json</c>, Erzeuger <c>werkzeuge/vergleich/erzeuge_spiel_stadt.py</c>).
/// Ohne Python-Vorlage (feste C#-Faelle): Raster, Wegsuche, Tagesuhr, Oeffnungszeiten, neue Orte,
/// der zweite Fingerabdruck fuer das Neue und der Nachweis, dass f2b59ce8 unveraendert bleibt.
/// </summary>
public class SpielStadtTests
{
    /// <summary>Zweiter Fingerabdruck (nur C#): Szenario <see cref="StadtSzenario"/>, Ereignisse und Werte.</summary>
    internal const string SollStadtHash = "86ac8f1907ea9f67f2bba91f4cb4003c9042894de2f99d1e8e87c1d1872e59ac";

    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_stadt.json");

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    internal static readonly Lazy<PyObjekt> Daten = new(() => Stadt.Laden(Path.Combine(Pfade.Wurzel, "inhalte_csharp", "spiel")));

    private static Stadt NeueStadt(PyObjekt? daten = null) => new(Inhalt, daten ?? Daten.Value);

    private static object? W(JsonElement e) => Py.Lesen(e.GetRawText());

    private static string T(object? wert) => PythonJson.Text(wert);

    private static PyObjekt Aussehen() => new() { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } };

    // -- wie Python ------------------------------------------------------------

    private static List<PyObjekt> AlteRegeln => [.. SpielDaten.ListeOderLeer(SpielDaten.Objekt(Inhalt["orte"]), "orte").Select(SpielDaten.Objekt)];

    private static void SichtGleich(JsonElement soll, Spielstand? stand, string wo)
    {
        Assert.Equal(T(W(soll.GetProperty("frei"))), T(AlteRegeln.Select(r => (object?)Stadt.OrtFrei(r, stand, Inhalt)).ToList()));
        var orte = Stadt.Orte(stand, Inhalt, AlteRegeln)
            .Select(o => (object?)new List<object?> { o["id"], o["name"], o["kurz"], o["rolle"], o["ansicht"], o["hinweis"] }).ToList();
        var s = T(W(soll.GetProperty("orte")));
        Assert.True(s == T(orte), wo + "\n  Python: " + s + "\n  C#:     " + T(orte));
    }

    [Fact]
    public void OrteWiePython()
    {
        SichtGleich(Soll.GetProperty("ohne_stand"), null, "ohne Spielstand");
        var neuaufbau = VergleichsHilfe.Lade("spiel_neuaufbau.json").GetProperty("protokolle").EnumerateArray()
            .ToDictionary(p => p.GetProperty("name").GetString()!, p => p.GetProperty("ereignisse"));
        var finger = VergleichsHilfe.Lade("spiel_fingerabdruck.json").GetProperty("ereignisse");
        var anzahl = 0;
        foreach (var protokoll in Soll.GetProperty("protokolle").EnumerateArray())
        {
            var name = protokoll.GetProperty("name").GetString()!;
            JsonElement roh;
            List<SpielEreignis> ereignisse;
            if (name.StartsWith("szenario045", StringComparison.Ordinal))
            {
                roh = finger;
                ereignisse = [.. finger.EnumerateArray().Select(e => new SpielEreignis("", e[0].GetString()!, (PyObjekt)W(e[1])!))];
            }
            else
            {
                roh = neuaufbau[name];
                ereignisse = SpielNeuaufbauTests.Ereignisse(roh);
            }
            var liste = (List<object?>)W(roh)!;
            if (protokoll.TryGetProperty("zusatz", out var zusatz))
            {
                liste.Add(W(zusatz));
                ereignisse.Add(new SpielEreignis("", zusatz[0].GetString()!, (PyObjekt)W(zusatz[1])!));
            }
            // dasselbe Protokoll wie im Erzeuger
            Assert.Equal(protokoll.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(PythonJson.Utf8(T(liste)))));
            foreach (var stand in protokoll.GetProperty("staende").EnumerateArray())
            {
                var n = stand.GetProperty("n").GetInt32();
                SichtGleich(stand, new Spielstand(ereignisse.Take(n), Inhalt), name + " n=" + n);
                anzahl++;
            }
        }
        Assert.True(anzahl > 300, anzahl + " Staende");
    }

    // -- Inhalte, Raster, Wege ---------------------------------------------------

    [Fact]
    public void InhaltOhneFehler()
    {
        var stadt = NeueStadt();
        Assert.Empty(stadt.Pruefen());
        Assert.Equal(12, stadt.Regeln.Count);
        Assert.Equal(["moebelhaus", "elektromarkt"], stadt.Regeln.Skip(10).Select(r => (string)r["id"]!));
        Assert.Equal((64L, 48L), (stadt.Breite, stadt.Hoehe));
        // Sammelliste 99: die neuen Orte genau mit Rolle, Ansicht und Sortiment
        Assert.Equal(["geschaeft:moebelhaus/moebel", "geschaeft:elektromarkt/elektro"],
            stadt.Regeln.Skip(10).Select(r => r["rolle"] + ":" + r["id"] + "/" + r["sortiment"]));
        Assert.Equal(["geschaeft:moebelhaus", "geschaeft:elektromarkt"], stadt.Regeln.Skip(10).Select(r => (string)r["ansicht"]!));
        // Kontrollfehler: jeder Fehler wird gefunden
        PyObjekt Kaputt(Action<PyObjekt> aendern)
        {
            var d = (PyObjekt)Py.Lesen(T(Daten.Value))!;
            aendern(d);
            return d;
        }
        PyObjekt O(PyObjekt d, string k) => SpielDaten.Objekt(d[k]);
        var faelle = new List<(string Erwartet, PyObjekt Daten)>
        {
            ("moebelhaus: Tuer liegt nicht auf einem Weg", Kaputt(d => Py.Setzen(O(O(d, "zugaenge"), "moebelhaus"), "tuer", new List<object?> { 15.9, 22.6 }))),
            ("elektromarkt: kein Zugang", Kaputt(d => O(d, "zugaenge").RemoveAll(p => p.Key == "elektromarkt"))),
            ("bitweiche: Oeffnungszeit ungueltig", Kaputt(d => Py.Setzen(O(d, "oeffnungszeiten"), "bitweiche", new List<object?> { 900L, 600L }))),
            ("Oeffnungszeit fuer unbekannten Ort: kino", Kaputt(d => Py.Setzen(O(d, "oeffnungszeiten"), "kino", new List<object?> { 600L, 900L }))),
            ("heim muss ein Ort ohne Oeffnungszeit sein", Kaputt(d => Py.Setzen(O(d, "oeffnungszeiten"), "zuhause", new List<object?> { 600L, 900L }))),
            ("uhr ungueltig", Kaputt(d => Py.Setzen(O(d, "uhr"), "abend", 300L))),
            ("Heimweg nach dem Abend endet nach Mitternacht", Kaputt(d => Py.Setzen(O(d, "uhr"), "minuten_je_schritt", 3L))),
            ("Ort doppelt: zuhause", Kaputt(d => Py.Setzen((PyObjekt)((List<object?>)d["neue_orte"]!)[0]!, "id", "zuhause"))),
            ("moebelhaus: Modell fehlt in neue_modelle", Kaputt(d => O(d, "neue_modelle").RemoveAll(p => p.Key == "moebelhaus"))),
            ("elektromarkt: unbekannte Freischaltung bald", Kaputt(d => Py.Setzen((PyObjekt)((List<object?>)d["neue_orte"]!)[1]!, "frei", new PyObjekt { { "bald", true } }))),
            ("moebelhaus: unbekannte Rolle kunde", Kaputt(d => Py.Setzen((PyObjekt)((List<object?>)d["neue_orte"]!)[0]!, "rolle", "kunde"))),
            ("elektromarkt: Ansicht muss geschaeft:elektromarkt sein", Kaputt(d => Py.Setzen((PyObjekt)((List<object?>)d["neue_orte"]!)[1]!, "ansicht", "geschaeft:moebelhaus"))),
            ("moebelhaus: unbekanntes Sortiment lebensmittel", Kaputt(d => Py.Setzen((PyObjekt)((List<object?>)d["neue_orte"]!)[0]!, "sortiment", "lebensmittel"))),
            ("elektromarkt: unbekanntes Sortiment None", Kaputt(d => ((PyObjekt)((List<object?>)d["neue_orte"]!)[1]!).RemoveAll(p => p.Key == "sortiment"))),
            ("elektromarkt: unbekanntes Feld kurz", Kaputt(d => Py.Setzen((PyObjekt)((List<object?>)d["neue_orte"]!)[1]!, "kurz", "Elektro"))),
            ("talheim_nord: von zuhause aus nicht erreichbar", Kaputt(d =>
                Py.Setzen(O(O(d, "zugaenge"), "talheim_nord"), "weg", new List<object?> { new List<object?> { 30.2, 5.55 }, new List<object?> { 30.2, 6.5 } }))),
        };
        foreach (var (erwartet, daten) in faelle)
        {
            Assert.Contains(erwartet, NeueStadt(daten).Pruefen());
        }
    }

    [Fact]
    public void WegeZwischenAllenOrten()
    {
        var stadt = NeueStadt();
        var ids = stadt.Regeln.Select(r => (string)r["id"]!).ToList();
        foreach (var a in ids)
        {
            foreach (var b in ids)
            {
                var weg = stadt.Weg(a, b);
                Assert.NotNull(weg);
                Assert.Equal(stadt.Tuer(a), weg.Zellen[0]);
                Assert.Equal(stadt.Tuer(b), weg.Zellen[^1]);
                // jeder Schritt geht in eine Nachbarzelle auf dem Raster
                for (var i = 1; i < weg.Zellen.Count; i++)
                {
                    Assert.Contains(weg.Zellen[i], stadt.Begehbar);
                    Assert.Equal(1, Math.Abs(weg.Zellen[i].X - weg.Zellen[i - 1].X) + Math.Abs(weg.Zellen[i].Y - weg.Zellen[i - 1].Y));
                }
                // kuerzester Weg: hin und zurueck gleich lang
                Assert.Equal(weg.Schritte, stadt.Weg(b, a)!.Schritte);
            }
        }
        Assert.Null(stadt.Weg("zuhause", "kino"));
        // feste Werte (Schritte ab zu Hause); aendern sie sich, aendert sich die Spielzeit der Wege
        var soll = "bitweiche 30, talheim 46, talheim_nord 51, betriebswerkstatt 20, lindenau 67, birkenhain 56, "
            + "gewerbehof 31, filiale 82, lokschuppen 80, moebelhaus 27, elektromarkt 9";
        var ist = string.Join(", ", ids.Skip(1).Select(b => b + " " + stadt.Weg("zuhause", b)!.Schritte));
        Assert.True(soll == ist, "Schritte ab zu Hause: " + ist);
    }

    // -- Tagesuhr und Oeffnungszeiten --------------------------------------------

    private static Spielablauf NeuesSpiel()
    {
        var zaehler = 0;
        var spiel = new Spielablauf(Inhalt, new SpielSpeicher(() =>
        {
            zaehler += 7;
            return new DateTime(2026, 10, 3).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }));
        return spiel;
    }

    [Fact]
    public void TagesuhrUndOeffnungszeiten()
    {
        var stadt = NeueStadt();
        var spiel = NeuesSpiel();
        Assert.Equal(StadtGrund.KeinProfil, stadt.Gehen(spiel, "bitweiche").Grund);
        Assert.Equal(StadtGrund.KeinProfil, stadt.Warten(spiel).Grund);
        Assert.Equal(StadtGrund.KeinProfil, stadt.Betreten(spiel.Stand));
        spiel.Profil("Nico", Aussehen(), Ereignisse.Normal);
        Assert.Equal(new StadtStand(1, "zuhause", 420), stadt.Stand(spiel.Stand));
        Assert.Equal("07:00", Stadt.Uhrzeit(stadt.Stand(spiel.Stand).Uhr));
        Assert.Equal(StadtGrund.Ok, stadt.Betreten(spiel.Stand));
        Assert.Equal(StadtGrund.SchonDa, stadt.Gehen(spiel, "zuhause").Grund);
        Assert.Equal(StadtGrund.Unbekannt, stadt.Gehen(spiel, "kino").Grund);
        Assert.Equal(StadtGrund.Gesperrt, stadt.Gehen(spiel, "lokschuppen").Grund);
        Assert.Equal(StadtGrund.Gesperrt, stadt.Gehen(spiel, "gewerbehof").Grund);
        Assert.Equal(StadtGrund.NichtGeschlossen, stadt.Warten(spiel).Grund);
        var anzahl = spiel.Stand.Verlauf.Count;
        var bestenliste = T(spiel.Speicher.Bestenliste().Select(z => (object?)new List<object?> { z.Art, z.Schluessel, z.Wert, z.Daten }).ToList());
        var erfolge = spiel.Stand.Verlauf.Count(e => e.Art == Ereignisse.Erfolg);

        // 30 Schritte zu je 2 Minuten
        var weg = stadt.Gehen(spiel, "bitweiche");
        Assert.True(weg.Ok);
        Assert.Equal("{\"tag\":1,\"von\":\"zuhause\",\"nach\":\"bitweiche\",\"schritte\":30,\"minuten\":60}", T(weg.Daten));
        Assert.Equal(new StadtStand(1, "bitweiche", 480), stadt.Stand(spiel.Stand));
        Assert.Equal(anzahl + 1, spiel.Stand.Verlauf.Count);
        // ein Weg schreibt nur sein Ereignis: keine Erfolge, keine Zeile in der Bestenliste
        Assert.Equal(erfolge, spiel.Stand.Verlauf.Count(e => e.Art == Ereignisse.Erfolg));
        Assert.Equal(bestenliste, T(spiel.Speicher.Bestenliste().Select(z => (object?)new List<object?> { z.Art, z.Schluessel, z.Wert, z.Daten }).ToList()));
        Assert.Equal(StadtGrund.Ok, stadt.Betreten(spiel.Stand));

        // Moebelhaus oeffnet um 10:00: geschlossen, warten, dann offen
        Assert.True(stadt.Gehen(spiel, "moebelhaus").Ok);
        Assert.Equal(new StadtStand(1, "moebelhaus", 480 + 2 * stadt.Weg("bitweiche", "moebelhaus")!.Schritte), stadt.Stand(spiel.Stand));
        Assert.Equal(StadtGrund.Geschlossen, stadt.Betreten(spiel.Stand));
        var warten = stadt.Warten(spiel);
        Assert.True(warten.Ok);
        Assert.Equal(new StadtStand(1, "moebelhaus", 600), stadt.Stand(spiel.Stand));
        Assert.Equal(StadtGrund.Ok, stadt.Betreten(spiel.Stand));
        Assert.Equal(StadtGrund.NichtGeschlossen, stadt.Warten(spiel).Grund);

        // Hin und her, bis der Abend kommt: dann nur noch heim
        string[] runde = ["elektromarkt", "moebelhaus"];
        var i = 0;
        while (true)
        {
            var ergebnis = stadt.Gehen(spiel, runde[i++ % 2]);
            if (!ergebnis.Ok)
            {
                Assert.Equal(StadtGrund.Abend, ergebnis.Grund);
                break;
            }
        }
        var abends = stadt.Stand(spiel.Stand);
        Assert.True(abends.Uhr <= stadt.Abend);
        Assert.True(abends.Uhr + 2 * stadt.Weg(abends.Ort, runde[(i - 1) % 2])!.Schritte > stadt.Abend);
        Assert.Equal(StadtGrund.Abend, stadt.Gehen(spiel, "bitweiche").Grund);
        Assert.True(stadt.Gehen(spiel, "zuhause").Ok);
        Assert.True(stadt.Stand(spiel.Stand).Uhr > stadt.Abend);
        Assert.Equal(StadtGrund.Abend, stadt.Gehen(spiel, "elektromarkt").Grund);

        // Die Verwaltung schliesst um 17:00: danach oeffnet sie heute nicht mehr
        Assert.Equal((480L, 1020L), stadt.Oeffnungszeit("lindenau"));
        Assert.False(stadt.Offen("lindenau", 1020));
        Assert.True(stadt.Offen("lindenau", 1019));
        Assert.True(stadt.Offen("zuhause", 5000));

        // Feierabend: neuer Tag, wieder zu Hause um 07:00; Tickets und Geld wie ohne Stadt
        while (spiel.Stand.OffeneTickets().Count > 0)
        {
            spiel.Verschieben(spiel.Stand.OffeneTickets()[0]["id"]);
        }
        spiel.Feierabend();
        Assert.Equal(new StadtStand(2, "zuhause", 420), stadt.Stand(spiel.Stand));
    }

    [Fact]
    public void ZuUndOeffnetHeuteNichtMehr()
    {
        var stadt = NeueStadt();
        var spiel = NeuesSpiel();
        spiel.Profil("Nico", Aussehen());
        // Die Bitweiche schliesst um 19:00, der Abend beginnt um 20:00: um 19:10 vor der Tuer
        spiel.Log(Stadt.Gegangen, new PyObjekt { { "tag", 1L }, { "von", "zuhause" }, { "nach", "bitweiche" }, { "schritte", 30L }, { "minuten", 730L } });
        Assert.Equal(new StadtStand(1, "bitweiche", 1150), stadt.Stand(spiel.Stand));
        Assert.Equal(StadtGrund.OeffnetNichtMehr, stadt.Betreten(spiel.Stand));
        Assert.Equal(StadtGrund.OeffnetNichtMehr, stadt.Warten(spiel).Grund);
        // Kontrollfehler: um 18:59 ist sie noch offen
        var frueh = NeuesSpiel();
        frueh.Profil("Nico", Aussehen());
        frueh.Log(Stadt.Gegangen, new PyObjekt { { "tag", 1L }, { "von", "zuhause" }, { "nach", "bitweiche" }, { "schritte", 30L }, { "minuten", 719L } });
        Assert.Equal(StadtGrund.Ok, stadt.Betreten(frueh.Stand));
    }

    [Fact]
    public void KaputteStadtEreignisseZaehlenNicht()
    {
        var stadt = NeueStadt();
        var spiel = NeuesSpiel();
        spiel.Profil("Nico", Aussehen());
        var vorher = stadt.Stand(spiel.Stand);
        spiel.Log(Stadt.Gegangen, new PyObjekt { { "tag", "1" }, { "nach", "bitweiche" }, { "minuten", 10L } });
        spiel.Log(Stadt.Gegangen, new PyObjekt { { "tag", 1L }, { "nach", "bitweiche" }, { "minuten", -5L } });
        spiel.Log(Stadt.Gegangen, new PyObjekt { { "tag", 1L }, { "nach", 7L }, { "minuten", 10L } });
        spiel.Log(Stadt.Gewartet, new PyObjekt { { "tag", 1L }, { "minuten", 2.5 } });
        spiel.Log(Stadt.Gegangen, new PyObjekt { { "tag", 2L }, { "nach", "bitweiche" }, { "minuten", 10L } });
        spiel.Log(Stadt.Gegangen, new PyObjekt { { "tag", 1L }, { "nach", "kino" }, { "minuten", 10L } });
        spiel.Log(Stadt.Gewartet, new PyObjekt { { "tag", 1L }, { "ort", "zuhause" }, { "minuten", 5000L } });
        Assert.Equal(vorher, stadt.Stand(spiel.Stand));
        // Kontrollfehler: ein gueltiges Ereignis zaehlt
        spiel.Log(Stadt.Gewartet, new PyObjekt { { "tag", 1L }, { "ort", "zuhause" }, { "minuten", 5L } });
        Assert.Equal(vorher with { Uhr = vorher.Uhr + 5 }, stadt.Stand(spiel.Stand));
    }

    // -- Fingerabdruecke ---------------------------------------------------------

    [Fact]
    public void AltesSpielBleibtGleich()
    {
        // f2b59ce8: dasselbe Szenario mit Stadt-Ereignissen dazwischen ergibt denselben Spielstand
        var original = SpielFingerabdruckTests.Gespielt.Value.Stand;
        var mit = new List<SpielEreignis>();
        long tag = 1;
        foreach (var e in original.Verlauf)
        {
            mit.Add(e);
            if (e.Art == Ereignisse.Profil || e.Art == Ereignisse.Feierabend)
            {
                tag = e.Art == Ereignisse.Feierabend ? Py.Int(e.Daten["tag"]) + 1 : tag;
                mit.Add(new SpielEreignis(e.Zeit, Stadt.Gegangen, new PyObjekt { { "tag", tag }, { "von", "zuhause" }, { "nach", "bitweiche" }, { "schritte", 30L }, { "minuten", 60L } }));
                mit.Add(new SpielEreignis(e.Zeit, Stadt.Gewartet, new PyObjekt { { "tag", tag }, { "ort", "bitweiche" }, { "minuten", 3L } }));
            }
        }
        var stand = new Spielstand(mit, Inhalt);
        Assert.True(mit.Count > original.Verlauf.Count + 300);
        Assert.Equal(["history"], stand.Unterschiede(original));
        Assert.Equal(T(SpielFingerabdruckTests.Werte(original)), T(SpielFingerabdruckTests.Werte(stand)));
        var ohneStadt = new Spielstand(stand.Verlauf.Where(e => e.Art != Stadt.Gegangen && e.Art != Stadt.Gewartet), Inhalt);
        Assert.Equal(SpielFingerabdruckTests.SollHash, SpielFingerabdruckTests.Hash(ohneStadt));
        Assert.Equal(SpielFingerabdruckTests.SollHash, SpielFingerabdruckTests.Hash(original));
        // Kontrollfehler: ein Stadt-Ereignis im Hash aendert ihn
        Assert.NotEqual(SpielFingerabdruckTests.SollHash, SpielFingerabdruckTests.Hash(stand));
    }

    /// <summary>
    /// Zweites Szenario (nur C#): sechs Arbeitstage mit Wegen zur Arbeit, zu den Geschaeften (mit Warten)
    /// und heim; Tickets werden richtig geloest oder verschoben, abends Feierabend.
    /// </summary>
    internal static (Spielablauf Spiel, List<object?> Stadtstaende) StadtSzenario(PyObjekt? daten = null)
    {
        var stadt = NeueStadt(daten);
        var spiel = NeuesSpiel();
        spiel.Profil("Nico", Aussehen(), Ereignisse.Normal);
        var staende = new List<object?>();
        void Merken(StadtErgebnis e)
        {
            var s = stadt.Stand(spiel.Stand);
            staende.Add(new List<object?> { e.Grund.ToString(), s.Tag, s.Ort, s.Uhr });
        }
        for (var tag = 1; tag <= 6; tag++)
        {
            Merken(stadt.Gehen(spiel, "bitweiche"));
            foreach (var _ in Enumerable.Range(0, 40))
            {
                var offen = spiel.Stand.OffeneTickets();
                if (offen.Count == 0)
                {
                    break;
                }
                var auftrag = offen[0];
                var vorbereitet = spiel.Stand.Vorbereitet(auftrag) ?? auftrag;
                var antwort = Bewertung.Loesung(vorbereitet, spiel.Stand.VerfuegbareTeile(vorbereitet), Inhalt) ?? new PyObjekt();
                try
                {
                    spiel.Loesen(auftrag["id"], antwort, tag % 3 == 0);
                }
                catch (SpielFehler)
                {
                    spiel.Verschieben(auftrag["id"]);
                }
            }
            Merken(stadt.Gehen(spiel, tag % 2 == 0 ? "elektromarkt" : "moebelhaus"));
            if (stadt.Betreten(spiel.Stand) == StadtGrund.Geschlossen)
            {
                Merken(stadt.Warten(spiel));
            }
            Merken(stadt.Gehen(spiel, "talheim"));
            Merken(stadt.Gehen(spiel, "zuhause"));
            spiel.Feierabend();
            Merken(new StadtErgebnis(StadtGrund.Ok));
        }
        return (spiel, staende);
    }

    private static string StadtHash(Spielablauf spiel, List<object?> staende)
    {
        var s = spiel.Stand;
        var blob = new PyObjekt
        {
            { "events", s.Verlauf.Select(e => (object?)new List<object?> { e.Art, e.Daten }).ToList() },
            { "werte", SpielFingerabdruckTests.Werte(s) },
            { "stadt", staende },
        };
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(SpielFingerabdruckTests.Dumps(blob, sortiert: true, ascii: false))));
    }

    [Fact]
    public void ZweiterFingerabdruck()
    {
        var (spiel, staende) = StadtSzenario();
        var s = spiel.Stand;
        var hash = StadtHash(spiel, staende);
        Assert.True(s.Verlauf.Count(e => e.Art == Stadt.Gegangen) >= 18, "zu wenige Wege");
        Assert.Contains(s.Verlauf, e => e.Art == Stadt.Gewartet);
        Assert.Equal(7, s.Tag);
        Assert.Equal(0, spiel.Abweichungen);
        Assert.True(SollStadtHash == hash, "zweiter Fingerabdruck: " + hash);
        // Kontrollfehler: 3 statt 2 Minuten je Schritt aendert den Fingerabdruck
        var langsam = (PyObjekt)Py.Lesen(T(Daten.Value))!;
        Py.Setzen(SpielDaten.Objekt(langsam["uhr"]), "minuten_je_schritt", 3L);
        var (spiel3, staende3) = StadtSzenario(langsam);
        Assert.NotEqual(SollStadtHash, StadtHash(spiel3, staende3));
    }
}
