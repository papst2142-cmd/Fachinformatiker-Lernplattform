using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.64.0 / A 1.10.0 (Weisungen P12-380 bis P12-382, Sammelliste 414, Nico 2c): die 18 Raeume des Spiels lassen sich im
/// Werkzeug aendern (Waende, Tueren, Groesse). Jede Regel der Fundliste wird rot (mit Gegenprobe), ohne Aenderung bleibt alles
/// bitgleich, der Export traegt nur geaenderte Raeume, und das Spiel setzt sie 1:1 ein.
/// </summary>
public class SpielRaeumeAendernTests
{
    private static Spieldaten D => Daten.Spiel;

    [Fact]
    public void Alle18_Unveraendert_KeinRoterFund()
    {
        var p = Projekt.Neu("Spiel-Raeume pruefen", D, Daten.NeueAblage(), "moebelhaus");
        var raeume = p.SpielRaeumeListe();
        Assert.Equal(18, raeume.Count);
        var fehler = new List<string>();
        foreach (var r in raeume)
        {
            Assert.True(p.SpielRaumBearbeiten(r.Schluessel), r.Schluessel);
            var a = InnenFunde.Vorbereiten(p, [r.Schluessel]);
            foreach (var f in InnenFunde.Rechnen(a)[0].Funde.Where(f => f.Rot))
            {
                fehler.Add(r.Schluessel + ": " + f.Text);
            }
        }
        Assert.True(fehler.Count == 0, string.Join("\n", fehler));
        Assert.Empty(p.GeaenderteSpielRaeume);
    }

    private const string Apartment = "spiel:zuhause:apartment";
    private const string Bitweiche = "spiel:buero:bitweiche";
    private const string Firma1 = "spiel:buero:firma1";

    private static Projekt Neu(string name) => Projekt.Neu(name, D, Daten.NeueAblage(), "moebelhaus");

    /// <summary>Rote Funde eines Raums des Spiels ueber denselben Weg wie die Liste im Werkzeug und die Export-Sperre.</summary>
    private static List<string> Rot(Projekt p, string schluessel) =>
        [.. InnenFunde.Rechnen(InnenFunde.Vorbereiten(p, [schluessel]))[0].Funde.Where(f => f.Rot).Select(f => f.Text)];

    private static PyObjekt Flur(PyObjekt g) => (PyObjekt)g["flur"]!;

    private static PyObjekt RaumMit(PyObjekt g, string id) =>
        ((List<object?>)g["raeume"]!).OfType<PyObjekt>().First(r => Py.Str(r["id"]) == id);

    private static PyObjekt Kiste(double x, double y, double w, double h) =>
        new() { { "typ", "kartons" }, { "x", x }, { "y", y }, { "w", w }, { "h", h } };

    private static void Deko(PyObjekt bereich, PyObjekt d)
    {
        if (!Py.Hat(bereich, "deko"))
        {
            bereich.Add("deko", new List<object?>());
        }
        ((List<object?>)bereich["deko"]!).Add(d);
    }

    /// <summary>Regel „Tür nach draußen zugestellt“: Kiste am Eingang ist rot; Gegenprobe: dieselbe Kiste weiter hinten im Flur nicht.</summary>
    [Fact]
    public void Regel_TuerNachDraussenZugestellt_MitGegenprobe()
    {
        var p = Neu("Regel Eingang");
        Assert.Empty(Rot(p, Bitweiche));
        p.Aendern(Bitweiche, "Kiste", g =>
        {
            var f = Flur(g);
            var sp = Wege.StartPlatz(g);
            Deko(f, Kiste(sp.Item1 - 0.5, Py.F(f["y"]), 1.0, Py.F(f["h"])));
        });
        Assert.Contains(Rot(p, Bitweiche), t => t.StartsWith("Die Tür nach draußen ist zugestellt", StringComparison.Ordinal));
        Assert.True(p.Rueckgaengig());
        Assert.Empty(p.GeaenderteSpielRaeume);
        p.Aendern(Bitweiche, "Kiste klein", g =>
        {
            var f = Flur(g);
            var sp = Wege.StartPlatz(g);
            Deko(f, Kiste(sp.Item1 + 3, Py.F(f["y"]) + Py.F(f["h"]) - 0.5, 0.5, 0.5));
        });
        Assert.DoesNotContain(Rot(p, Bitweiche), t => t.StartsWith("Die Tür nach draußen", StringComparison.Ordinal));
    }

    /// <summary>Regel „Raum nicht erreichbar“: ein ganz zugestellter Raum ist rot; Gegenprobe: unveraendert nicht.</summary>
    [Fact]
    public void Regel_RaumNichtErreichbar_MitGegenprobe()
    {
        var p = Neu("Regel erreichbar");
        Assert.DoesNotContain(Rot(p, Apartment), t => t.Contains("nicht erreichbar", StringComparison.Ordinal));
        p.Aendern(Apartment, "Bad zu", g =>
        {
            var r = RaumMit(g, "bad");
            Deko(r, Kiste(Py.F(r["x"]), Py.F(r["y"]), Py.F(r["w"]), Py.F(r["h"])));
        });
        Assert.Contains(Rot(p, Apartment), t => t.StartsWith("Raum „", StringComparison.Ordinal) && t.EndsWith("ist vom Eingang aus nicht erreichbar.", StringComparison.Ordinal));
    }

    /// <summary>Regeln Flur 1,80 m (Buero) und Diele 1,32 m (Wohnung): knapp darunter rot, genau an der Grenze nicht.</summary>
    [Fact]
    public void Regel_FlurUndDiele_Mindestbreite_MitGegenprobe()
    {
        foreach (var (schluessel, mindest, wort) in new[] { (Bitweiche, SpielRaumRegeln.Flur, "Flur"), (Apartment, SpielRaumRegeln.Diele, "Diele") })
        {
            var p = Neu("Regel " + wort);
            double h0 = 0;
            p.Aendern(schluessel, "Flur genau", g =>
            {
                h0 = Py.F(Flur(g)["h"]);
                Py.Setzen(Flur(g), "h", mindest);
            });
            Assert.True(h0 >= mindest - 1e-9, wort + " heute schon zu schmal");
            Assert.DoesNotContain(Rot(p, schluessel), t => t.StartsWith(wort + " nur", StringComparison.Ordinal));
            p.Aendern(schluessel, "Flur schmal", g => Py.Setzen(Flur(g), "h", mindest - 0.1));
            Assert.Contains(Rot(p, schluessel), t => t.StartsWith(wort + " nur", StringComparison.Ordinal));
        }
    }

    /// <summary>Regel „Tür mindestens 0,90 m“: 1,4 Einheiten (0,84 m) rot, 1,5 Einheiten (0,90 m) nicht.</summary>
    [Fact]
    public void Regel_Tuerbreite_MitGegenprobe()
    {
        var p = Neu("Regel Tuer");
        p.Aendern(Apartment, "Tuer genau", g =>
        {
            var t = (PyObjekt)RaumMit(g, "bad")["tuer"]!;
            Py.Setzen(t, "bis", Py.F(t["von"]) + SpielRaumRegeln.Tuer);
        });
        Assert.DoesNotContain(Rot(p, Apartment), t => t.StartsWith("Tür zu", StringComparison.Ordinal));
        p.Aendern(Apartment, "Tuer schmal", g =>
        {
            var t = (PyObjekt)RaumMit(g, "bad")["tuer"]!;
            Py.Setzen(t, "bis", Py.F(t["von"]) + 1.4);
        });
        Assert.Contains(Rot(p, Apartment), t => t.StartsWith("Tür zu", StringComparison.Ordinal) && t.Contains("0,84 m", StringComparison.Ordinal));
    }

    /// <summary>Regel „Schreibtisch der Firma erreichbar“: Kiste auf einem Arbeitsplatz ist rot; Gegenprobe: daneben nicht.</summary>
    [Fact]
    public void Regel_ArbeitsplatzFirma_MitGegenprobe()
    {
        var p = Neu("Regel Arbeitsplatz");
        var (x, y, _) = SpielRaumRegeln.Plaetze("buero:firma1", D.Inhalt)[0];
        Assert.DoesNotContain(Rot(p, Firma1), t => t.StartsWith("Schreibtisch der Firma", StringComparison.Ordinal));
        p.Aendern(Firma1, "Kiste auf Platz", g =>
        {
            var r = ((List<object?>)g["raeume"]!).OfType<PyObjekt>().First(r => Py.F(r["x"]) <= x && x < Py.F(r["x"]) + Py.F(r["w"]) && Py.F(r["y"]) <= y && y < Py.F(r["y"]) + Py.F(r["h"]));
            Deko(r, Kiste(x - 0.25, y - 0.25, 0.5, 0.5));
        });
        Assert.Contains(Rot(p, Firma1), t => t.StartsWith("Schreibtisch der Firma", StringComparison.Ordinal));
    }

    /// <summary>
    /// Regeln „PC zu Hause“ und „Kleiderschrank“: in einer Wohnung, deren Raeume zu klein sind, ist kein Platz mehr (rot);
    /// Gegenprobe: die unveraenderte Wohnung hat fuer beide einen Platz.
    /// </summary>
    [Fact]
    public void Regel_SchreibtischUndKleiderschrank_MitGegenprobe()
    {
        foreach (var w in new[] { "apartment" })
        {
            foreach (var (moebel, _) in SpielRaumRegeln.WohnungBraucht)
            {
                Assert.True(SpielRaumRegeln.PlatzFuer(w, moebel, D.Inhalt), w + "/" + moebel);
            }
        }
        var p = Neu("Regel Moebel");
        p.Aendern(Apartment, "Raeume voll", g =>
        {
            foreach (var r in ((List<object?>)g["raeume"]!).OfType<PyObjekt>())
            {
                // Raeume mit Kisten fuellen, nur ein Streifen an der Tuer bleibt frei (alles erreichbar, aber nichts passt mehr)
                var oben = Py.F(r["y"]) < Py.F(Flur(g)["y"]);
                double x = Py.F(r["x"]), y = Py.F(r["y"]), rw = Py.F(r["w"]), rh = Py.F(r["h"]);
                Deko(r, oben ? Kiste(x, y, rw, rh - 0.8) : Kiste(x, y + 0.8, rw, rh - 0.8));
            }
        });
        var rot = Rot(p, Apartment);
        Assert.Contains(rot, t => t.Contains("Schreibtisch (PC)", StringComparison.Ordinal));
        Assert.Contains(rot, t => t.Contains("Kleiderschrank", StringComparison.Ordinal));
    }

    /// <summary>Regel „Raum passt nicht ins Gebäude“: ein Raum, der aus dem Haus ragt, ist rot; Gegenprobe: bis an die Wand nicht.</summary>
    [Fact]
    public void Regel_RaumPasstNichtInsGebaeude_MitGegenprobe()
    {
        var p = Neu("Regel Haus");
        p.Aendern(Apartment, "Kueche bis Wand", g =>
        {
            var r = RaumMit(g, "kueche");
            Py.Setzen(r, "w", Py.F(g["breite"]) - Py.F(r["x"]));
        });
        var vorher = Rot(p, Apartment);
        p.Aendern(Apartment, "Kueche zu breit", g =>
        {
            var r = RaumMit(g, "kueche");
            Py.Setzen(r, "w", Py.F(g["breite"]) - Py.F(r["x"]) + 1);
        });
        var nachher = Rot(p, Apartment);
        Assert.True(nachher.Except(vorher).Any(), string.Join("\n", nachher));
        Assert.DoesNotContain(vorher, t => t.Contains("ausserhalb", StringComparison.OrdinalIgnoreCase) || t.Contains("außerhalb", StringComparison.OrdinalIgnoreCase));
    }

    private static readonly DateTimeOffset Zeit = new(2026, 11, 25, 17, 0, 0, TimeSpan.FromHours(1));

    private static long Nummer(byte[] datei)
    {
        var text = System.Text.Encoding.UTF8.GetString(datei);
        var i = text.IndexOf("\"formatversion\": ", StringComparison.Ordinal) + "\"formatversion\": ".Length;
        return long.Parse(text[i..text.IndexOf(',', i)], System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Speichern und Oeffnen: mit geaendertem Raum des Spiels hat das Projekt Format 7 und oeffnet sich mit der Aenderung und dem
    /// Verlauf; „Zurück auf den Stand des Spiels“ ist ein Schritt, danach ist nichts mehr geaendert und das Projekt schreibt
    /// wieder Format 3 und dieselbe Stadt wie ohne Aenderung (bitgleich). Rueckgaengig holt die Aenderung zurueck.
    /// </summary>
    [Fact]
    public void Projekt_Format7_OeffnenMitAenderung_ZurueckBitgleich()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Raum aendern", D, ablage, "moebelhaus");
        var stadtVorher = p.Stand("2026-11-25T17:00:00+01:00").Schreiben();
        Assert.Equal(3, Nummer(p.Schreiben()));
        p.Aendern(Apartment, "Wand ziehen", WerkzeugGleichSpielTests.WandZiehen);
        p.Speichern(vonHand: true);
        Assert.Equal(7, Nummer(File.ReadAllBytes(p.Datei)));
        var geaendert = PythonJson.Standard(p.Grundriss(Apartment));

        var wieder = Projekt.Oeffnen(p.Datei, D, ablage);
        Assert.False(wieder.NurLesen);
        Assert.Equal([Apartment], wieder.GeaenderteSpielRaeume);
        Assert.Equal(geaendert, PythonJson.Standard(wieder.Grundriss(Apartment)));
        Assert.Equal(1, wieder.SchritteZurueck);

        Assert.True(wieder.SpielRaumZuruecksetzen(Apartment, "Zurück auf den Stand des Spiels"));
        Assert.Empty(wieder.GeaenderteSpielRaeume);
        Assert.Equal(2, wieder.SchritteZurueck);
        Assert.Equal(stadtVorher, wieder.Stand("2026-11-25T17:00:00+01:00").Schreiben());
        Assert.Equal(PythonJson.Standard(wieder.SpielRaumOriginal(Apartment)), PythonJson.Standard(wieder.Grundriss(Apartment)));
        Assert.False(wieder.SpielRaumZuruecksetzen(Apartment, "nochmal"));
        Assert.Throws<StadtbauFehler>(() => wieder.Exportieren(Zeit));
        Assert.True(wieder.Rueckgaengig());
        Assert.Equal(Apartment, wieder.LetzterSchrittSpielRaum);
        Assert.Equal(geaendert, PythonJson.Standard(wieder.Grundriss(Apartment)));
    }

    /// <summary>
    /// Export: nur der geaenderte Raum kommt als Ersatzstueck in die Datei (Format 5), sie besteht die Pruefung beim Einbau;
    /// als neues Projekt geoeffnet ist die Aenderung wieder da. Gegenproben: ohne Aenderung Format 1 ohne den Teil; ein Stueck,
    /// dessen Pruefsumme nicht mehr zum Spiel passt, und der Teil in einer anderen Stadt als Stadt 1 sind Funde (nie still).
    /// </summary>
    [Fact]
    public void Export_NurGeaenderteRaeume_Format5_Rundlauf_UnpassendIstFund()
    {
        var p = Neu("Export Raum");
        p.Aendern(Apartment, "Wand ziehen", WerkzeugGleichSpielTests.WandZiehen);
        var datei = p.Exportieren(Zeit);
        Assert.Equal(5, Nummer(datei));
        var f = StadtFassung.Lesen(datei);
        Assert.Equal(datei, f.Schreiben());
        Assert.Equal(["zuhause:apartment"], SpielRaumErsatz.Lesen(f.Stadtdaten).Select(s => s.Raum));
        Assert.Empty(StadtbauPruefung.Inhalt(f, D.Inhalt));

        var kopie = Projekt.AusStadtDatei(datei, D, Daten.NeueAblage(), "Export Raum Kopie");
        Assert.Equal([Apartment], kopie.GeaenderteSpielRaeume);
        Assert.Equal(PythonJson.Standard(p.Grundriss(Apartment)), PythonJson.Standard(kopie.Grundriss(Apartment)));

        // Gegenprobe ohne Aenderung im Raum: Notiz draussen, Datei Format 1 ohne den Teil
        var ohne = Neu("Export ohne Raum");
        ohne.Aendern(Apartment, "Wand ziehen", WerkzeugGleichSpielTests.WandZiehen);
        Assert.True(ohne.Rueckgaengig());
        Assert.Throws<StadtbauFehler>(() => ohne.Exportieren(Zeit));

        // Gegenprobe Pruefsumme: das Spiel hat den Raum inzwischen anders (alt passt nicht mehr) = Fund, beim Laden Fehler
        var stadtdaten = (PyObjekt)Py.Kopie(f.Stadtdaten)!;
        var stueck = (PyObjekt)((List<object?>)stadtdaten[SpielRaumErsatz.Abschnitt]!)[0]!;
        var alt = Py.Str(stueck["alt"]);
        Py.Setzen(stueck, "alt", alt[..^4] + (alt.EndsWith("0000", StringComparison.Ordinal) ? "1111" : "0000"));
        var falsch = new StadtFassung
        {
            StadtId = f.StadtId, Nummer = f.Nummer, Basis = f.Basis, Werkzeug = f.Werkzeug, Erstellt = f.Erstellt, Nachricht = f.Nachricht,
            Karte = f.Karte, Stadtdaten = stadtdaten, Innenraeume = f.Innenraeume, Laternen = f.Laternen, Beete = f.Beete,
        };
        Assert.Contains(StadtbauPruefung.Inhalt(falsch, D.Inhalt), t => t.Contains("passt nicht mehr zum Stand des Spiels", StringComparison.Ordinal));
        // Gegenprobe andere Stadt: der Teil gilt nur fuer Stadt 1
        var fremd = new StadtFassung
        {
            StadtId = "neu_probe", Nummer = 1, Basis = null, Werkzeug = f.Werkzeug, Erstellt = f.Erstellt, Nachricht = f.Nachricht,
            Karte = f.Karte, Stadtdaten = f.Stadtdaten, Innenraeume = f.Innenraeume, Laternen = f.Laternen, Beete = f.Beete,
        };
        Assert.Contains(StadtbauPruefung.Inhalt(fremd, D.Inhalt), t => t.Contains("nur in Stadt 1", StringComparison.Ordinal));
        Assert.Throws<StadtbauFehler>(() => SpielRaumErsatz.Inhalt(D.Inhalt, falsch));
    }

    /// <summary>
    /// Die Regeln des Spiels selbst auf den heutigen 18 Raeumen (ohne Abgleich): nur die feste Liste bekannter Funde. Talheim-Nord
    /// zeigt im bearbeitbaren Grundriss die Baustelle und den fertigen Bahnsteig zugleich (im Spiel je nach Auftrag nur eins),
    /// darum sind dort zwei Stellen zugestellt; der Abgleich mit dem heutigen Stand (<see cref="Alle18_Unveraendert_KeinRoterFund"/>)
    /// macht daraus keinen roten Fund. Gegenprobe: jede andere Stelle waere neu und rot.
    /// </summary>
    [Fact]
    public void RegelnDesSpiels_Heute_NurBekannteFunde()
    {
        var bekannt = new[]
        {
            "kunde:talheim_nord: Auftrag „Automaten-Check am Bahnsteig Talheim-Nord“: seine Stelle ist nicht erreichbar.",
            "kunde:talheim_nord: Auftrag „Fahrgastanzeige Talheim-Nord: Wartung“: seine Stelle ist nicht erreichbar.",
        };
        var funde = new List<string>();
        foreach (var r in SpielRaeume.Alle(D.Inhalt, D.Fassung.Innenraeume))
        {
            var k = Projekt.SpielRaumKennung(r.Schluessel);
            var g = SpielRaumErsatz.Grundlage(k, D.Inhalt, D.Fassung.Innenraeume)!;
            funde.AddRange(SpielRaumRegeln.Pruefen(k, g, D.Inhalt, D.Fassung.Innenraeume).Select(f => k + ": " + f.Text));
        }
        Assert.Equal(bekannt, funde);
    }
}
