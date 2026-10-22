using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, W2: Fachrichtungen und Klausur-Rechnungen gegen G36 (tests/Vergleich/fachrichtungen.json,
/// unabhaengiges Skript werkzeuge/vergleich/erzeuge_fachrichtungen.py nur aus der Spezifikation P4-5).
/// SI muss dabei gleich G32 sein (KlausurTests und der CI-Schritt "Vergleich"). Dazu die
/// Kontrollfehler aus Abschnitt 7 der Spezifikation: jeder falsche Nachbau muss hier auffallen.
/// </summary>
public partial class FachrichtungenTests
{
    private static readonly JsonNode G36 = Lade();
    private static readonly JsonNode G32 = KlausurTests.G32;

    private static JsonNode Lade()
    {
        using var f = File.OpenRead(Pfade.Vergleich("fachrichtungen.json"));
        return JsonNode.Parse(f)!;
    }

    private static string Py(JsonNode? k) => KlausurTests.Py(k);

    private static string Py(object? w) => KlausurTests.Py(w);

    private static double Zahl(JsonNode? k) => Json.Zahl(k)!.Value;

    private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

    private static PyObjekt Konfiguration(Fachrichtung f) => new()
    {
        { "name", f.Name }, { "projekt", f.Projekt }, { "projekt_stunden", (long)f.ProjektStunden },
        { "bereiche", f.Bereiche.Select(b => (object?)new PyObjekt
            {
                { "rolle", b.Rolle }, { "art", b.Art }, { "kurz", b.Kurz }, { "name", b.Name }, { "minuten", (long)b.Minuten },
                { "gewicht", (long)b.Gewicht }, { "ergaenzung", b.Ergaenzung }, { "quelle", b.Quelle },
            }).ToList() },
    };

    [Fact]
    public void Konfiguration_WieSpezifikation()
    {
        var erwartet = G36["fachrichtungen"]!.AsObject();
        Assert.Equal(erwartet.Select(p => p.Key), Fachrichtung.Alle.Select(f => f.Id));
        Assert.Equal(Ausbildung.Werte, Fachrichtung.Alle.Select(f => f.Id));
        foreach (var f in Fachrichtung.Alle)
        {
            Assert.Equal(Py(erwartet[f.Id]), Py(Konfiguration(f)));
            Assert.Equal(100, f.Bereiche.Sum(b => b.Gewicht));
            Assert.Equal(80, f.Bereiche.Where(b => Fachrichtung.Teil2.Contains(b.Rolle)).Sum(b => b.Gewicht));
            Assert.Equal(["b1", "b2", "wiso"], f.Bereiche.Where(b => b.Ergaenzung).Select(b => b.Rolle));
        }
        // si ist der Standard (unbekannt und null auch) und hat genau die Python-Bereiche von G32
        Assert.Equal("si", Fachrichtung.Nach(null).Id);
        Assert.Equal("si", Fachrichtung.Nach("xx").Id);
        Assert.Equal(Py(G32["gesamt"]!["bereiche"]!.AsObject().Select(p => (object?)p.Value!.GetValue<string>()).ToList()),
            Py(Fachrichtung.Nach(null).Bereiche.Select(b => (object?)b.Kurz).ToList()));
        Assert.Equal(Klausur.Arten.Select(a => a.Art).Where(a => a != Klausur.Wiso).Skip(1),
            Fachrichtung.Nach("si").Bereiche.Where(b => b.Rolle is "b1" or "b2").Select(b => b.Art));
        Assert.Equal(Klausur.Arten.Where(a => a.Art != Klausur.Ap1 && a.Art != Klausur.Wiso).Select(a => a.Name),
            Fachrichtung.Nach("si").Bereiche.Where(b => b.Rolle is "b1" or "b2").Select(b => b.Name));
    }

    [Fact]
    public void Bewertung_WieSpezifikation()
    {
        foreach (var t in G36["teilpunkte"]!.AsArray())
        {
            Assert.Equal(Py(t![1]), Py(Kriterien.Teilpunkte((int)t[0]!).Select(p => (object?)(long)p).ToList()));
        }
        foreach (var v in G36["vorschlag"]!.AsArray())
        {
            var (maximum, n, k) = ((int)v![0]!, (int)v[1]!, (int)v[2]!);
            Assert.Equal((int)v[3]!, Klausur.Vorschlag(maximum, [.. Enumerable.Repeat(true, k), .. Enumerable.Repeat(false, n - k)]));
        }
        foreach (var w in G36["wiso"]!.AsArray())
        {
            Assert.Equal(Bits(Zahl(w![2])), Bits(Bewertung.WisoPunkte((int)w[0]!, (int)w[1]!)));
        }
        foreach (var a in G36["ap"]!.AsArray())
        {
            Assert.Equal((long)a![2]!, Bewertung.Erreicht((int)a[0]!, a[1]!.DeepClone()));
        }
        foreach (var p in G36["punkte"]!.AsArray())
        {
            var wert = Wert((string)p!["wert"]!);
            Assert.Equal((string)p["text"]!, PythonRunden.PunkteText(wert));
            if (p["abgerundet"] is { } ab)
            {
                Assert.Equal(Bits(Zahl(ab)), Bits(Bewertung.Abrunden(wert)));
                Assert.Equal((string)p["note"]!, Trainer.Note(wert));
            }
            else
            {
                Assert.False(double.IsFinite(wert));
            }
        }
        foreach (var s in G36["schwach"]!.AsArray())
        {
            var themen = s![0]!.AsObject().Select(p => (p.Key, Zahl(p.Value![0]), Zahl(p.Value[1])));
            Assert.Equal(Py(s[1]), Py(Bewertung.Schwach(themen).ToList<object?>()));
        }
        var zeit = G36["zeit"]!;
        var start = (long)zeit["start_utc"]!;
        foreach (var r in zeit["rest"]!.AsArray())
        {
            var zustand = new JsonObject { ["start"] = "2023-11-14 23:13:20", ["start_utc"] = start, ["minuten"] = (int)r![0]! };
            var jetzt = Json.Ganz(r[1]) ? start + (long)r[1]! : start + Zahl(r[1]);
            Assert.Equal((long)r[2]!, Klausur.Rest(zustand, jetzt, KlausurTests.Berlin));
            Klausur.Abgeben(zustand, jetzt, KlausurTests.Berlin);
            Assert.Equal((long)r[3]!, (long)zustand["dauer"]!);
        }
        foreach (var t in zeit["text"]!.AsArray())
        {
            Assert.Equal((string)t![1]!, Klausur.Zeittext((long)t[0]!));
        }
    }

    private static double Wert(string repr) => double.Parse(repr.Replace("inf", "Infinity", StringComparison.Ordinal)
        .Replace("nan", "NaN", StringComparison.Ordinal), CultureInfo.InvariantCulture);

    private static Dictionary<string, double> Rollen(double[] werte) =>
        Fachrichtung.Rollen.Select((r, i) => (r, werte[i])).ToDictionary();

    [Fact]
    public void Gesamtergebnis_JeFachrichtung()
    {
        var anzahl = 0;
        foreach (var g in G36["gesamt"]!.AsArray())
        {
            var werte = g![0]!.AsArray().Select(Zahl).ToArray();
            var r = Bewertung.Gesamt(Fachrichtung.Nach("si"), Rollen(werte));
            var ort = Py(g[0]);
            Assert.True(Bits(Zahl(g[1])) == Bits(r.Gesamt), ort);
            Assert.True(Bits(Zahl(g[2])) == Bits(r.Teil2), ort);
            Assert.Equal((string)g[3]!, r.Note);
            Assert.Equal((bool)g[4]!, r.Bestanden);
            Assert.Equal(Py(g[5]), Py(r.Regeln.Select(x => (object?)x.Ok).ToList()));
            Assert.Equal((int)g[6]!, r.Ausreichend);
            Assert.True((int)g[7]! == r.ErgaenzungArt, ort);
            Assert.True(Json.Text(g[8]) == r.ErgaenzungRolle, ort);
            Assert.True((int?)g[9] == r.Muendlich, ort);
            anzahl++;
        }
        Assert.Equal(7776 + 300, anzahl);
        foreach (var f in Fachrichtung.Alle)
        {
            foreach (var t in G36["texte"]![f.Id]!.AsArray())
            {
                var r = Bewertung.Gesamt(f, Rollen(t![0]!.AsArray().Select(Zahl).ToArray()));
                Assert.Equal(Py(t[1]), Py(r.Regeln.Select(x => (object?)x.Text).ToList()));
                Assert.Equal((string)t[2]!, r.Ergaenzung);
            }
            Assert.Equal(80, G36["texte"]![f.Id]!.AsArray().Count);
            foreach (var t in G36["fehlend"]![f.Id]!.AsArray())
            {
                var r = Bewertung.Gesamt(f, t![0]!.AsObject().ToDictionary(p => p.Key, p => Zahl(p.Value)));
                Assert.False(r.Vollstaendig);
                Assert.Equal(Py(t[1]), Py(r.Fehlend.ToList<object?>()));
            }
        }
    }

    [Fact]
    public void Kriterien_WieSpezifikation()
    {
        var anzahl = 0;
        foreach (var k in G36["kriterien"]!.AsArray())
        {
            var teile = KlausurTests.Klausurinhalte.Teile((string)k![0]!, (int)k[1]!);
            Assert.Equal((bool)k[2]!, teile is not null);
            if (teile is not null)
            {
                Assert.Equal((string)k[3]!, teile.Einleitung);
                Assert.Equal(Py(k[4]), Py(teile.Teile.Select(t => (object?)new List<object?>
                    { t.Text, (long)t.Punkte, t.Loesung, t.Kriterien.ToList<object?>() }).ToList()));
            }
            anzahl++;
        }
        Assert.Equal(595, anzahl);
    }

    /// <summary>Teil "kontrolle" von G32: Faelle, die das Raster von G36 fuer drei Kontrollfehler nicht trifft.</summary>
    [Fact]
    public void Kontrollfaelle_WieInPython()
    {
        var k = G32["kontrolle"]!;
        foreach (var g in k["gesamt"]!.AsArray())
        {
            var r = Bewertung.Gesamt(Fachrichtung.Nach("si"), Rollen(g![0]!.AsArray().Select(Zahl).ToArray()));
            Assert.Equal(Bits(Zahl(g[1])), Bits(r.Gesamt));
            Assert.Equal(Bits(Zahl(g[2])), Bits(r.Teil2));
            Assert.Equal((string)g[3]!, r.Note);
            Assert.Equal((bool)g[4]!, r.Bestanden);
            Assert.Equal(Py(g[5]), Py(r.Regeln.Select(x => (object?)x.Ok).ToList()));
        }
        foreach (var r in k["runden"]!.AsArray())
        {
            Assert.Equal(Bits(Zahl(r![1])), Bits(PythonRundenStellen.Runde(Wert((string)r[0]!), 1)));
        }
        foreach (var t in k["kriterien"]!.AsArray())
        {
            Assert.Equal(Py(t![1]), Py(Kriterien.Aus((string)t[0]!).ToList<object?>()));
        }
    }

    // -- Kontrollfehler (Spezifikation P4-5 Abschnitt 7): jeder muss gegen G36/G32 auffallen ---------

    private static int Abweichungen(Func<Fachrichtung, double[], (double Gesamt, double Teil2, string Note, bool Bestanden, string Ergaenzung)> rechnen)
    {
        var anzahl = 0;
        var si = Fachrichtung.Nach("si");
        foreach (var g in G36["gesamt"]!.AsArray())
        {
            var r = rechnen(si, g![0]!.AsArray().Select(Zahl).ToArray());
            anzahl += Bits(Zahl(g[1])) != Bits(r.Gesamt) || Bits(Zahl(g[2])) != Bits(r.Teil2) || (string)g[3]! != r.Note
                || (bool)g[4]! != r.Bestanden ? 1 : 0;
        }
        foreach (var g in G32["kontrolle"]!["gesamt"]!.AsArray())
        {
            var r = rechnen(si, g![0]!.AsArray().Select(Zahl).ToArray());
            anzahl += Bits(Zahl(g[1])) != Bits(r.Gesamt) || Bits(Zahl(g[2])) != Bits(r.Teil2) || (string)g[3]! != r.Note
                || (bool)g[4]! != r.Bestanden ? 1 : 0;
        }
        foreach (var f in Fachrichtung.Alle)
        {
            foreach (var t in G36["texte"]![f.Id]!.AsArray())
            {
                anzahl += (string)t![2]! != rechnen(f, t[0]!.AsArray().Select(Zahl).ToArray()).Ergaenzung ? 1 : 0;
            }
        }
        return anzahl;
    }

    /// <summary>Nachbau mit einem Fehler: summe, teil2, note, bereiche fuer die Ergaenzung, Gleichstand.</summary>
    private static (double, double, string, bool, string) Falsch(Fachrichtung f, double[] w, bool einfacheSumme = false,
        bool teil2DurchVier = false, bool noteAbgerundet = false, bool ergaenzungAlle = false, bool gleichstandReihenfolge = false)
    {
        double Summe(IEnumerable<double> x) => einfacheSumme ? x.Aggregate(0.0, (a, b) => a + b) : PythonSumme.Summe(x);
        (double G, double T) Werte(double[] v) => (Summe(f.Bereiche.Select((b, i) => v[i] * b.Gewicht)) / 100.0,
            teil2DurchVier ? Summe(v.Skip(1)) / 4 : Summe(f.Bereiche.Skip(1).Select((b, i) => v[i + 1] * b.Gewicht)) / 80);
        bool Ok(double[] v)
        {
            var (g, t) = Werte(v);
            return g >= 50 && t >= 50 && v.Skip(1).Count(x => x >= 50) >= 3 && v.Skip(1).All(x => x >= 30);
        }
        var (gesamt, teil2) = Werte(w);
        var bestanden = Ok(w);
        var text = "";
        if (!bestanden)
        {
            var moeglich = new List<(int M, string Kurz, int Stelle)>();
            for (var i = 0; i < 5; i++)
            {
                if ((!f.Bereiche[i].Ergaenzung && !ergaenzungAlle) || w[i] >= 50)
                {
                    continue;
                }
                var p = (double[])w.Clone();
                p[i] = (2 * w[i] + 100) / 3.0;
                if (!Ok(p))
                {
                    continue;
                }
                for (var m = 0; m <= 100; m++)
                {
                    p[i] = (2 * w[i] + m) / 3.0;
                    if (Ok(p))
                    {
                        moeglich.Add((m, f.Bereiche[i].Kurz, i));
                        break;
                    }
                }
            }
            text = moeglich.Count == 0
                ? Beschriftungen.Einsetzen(PruefungTexte.ErgaenzungNein, f.Bereich("b1").Kurz, f.Bereich("b2").Kurz)
                : Beschriftungen.Einsetzen(PruefungTexte.ErgaenzungJa, (gleichstandReihenfolge
                    ? moeglich.OrderBy(m => m.M).ThenBy(m => m.Stelle) : moeglich.OrderBy(m => m.M).ThenBy(m => m.Kurz, StringComparer.Ordinal)).First().Kurz,
                    Core.Dashboard.DashboardTexte.Plural(moeglich.Min(m => m.M), "Punkt", "Punkte"));
        }
        return (Bewertung.Abrunden(gesamt), Bewertung.Abrunden(teil2), Trainer.Note(noteAbgerundet ? Bewertung.Abrunden(gesamt) : gesamt), bestanden, text);
    }

    [Fact]
    public void Kontrollfehler_NachbauRichtig()
    {
        // der fehlerfreie Nachbau stimmt ueberall: die Abweichungen unten kommen nur vom eingebauten Fehler
        Assert.Equal(0, Abweichungen((f, w) => Falsch(f, w)));
        Assert.Equal(0, Abweichungen((f, w) =>
        {
            var r = Bewertung.Gesamt(f, Rollen(w));
            return (r.Gesamt, r.Teil2, r.Note, r.Bestanden, r.Ergaenzung);
        }));
    }

    [Fact]
    public void Kontrollfehler_EinfacheSumme() => Assert.True(Abweichungen((f, w) => Falsch(f, w, einfacheSumme: true)) > 0);

    [Fact]
    public void Kontrollfehler_Teil2DurchVier() => Assert.True(Abweichungen((f, w) => Falsch(f, w, teil2DurchVier: true)) > 0);

    [Fact]
    public void Kontrollfehler_NoteAusAbgerundetemWert() => Assert.True(Abweichungen((f, w) => Falsch(f, w, noteAbgerundet: true)) > 0);

    [Fact]
    public void Kontrollfehler_ErgaenzungInAp1OderProjekt() => Assert.True(Abweichungen((f, w) => Falsch(f, w, ergaenzungAlle: true)) > 0);

    [Fact]
    public void Kontrollfehler_GleichstandNachReihenfolge() =>
        Assert.True(Abweichungen((f, w) => Falsch(f, w, gleichstandReihenfolge: true)) > 0);

    [Fact]
    public void Kontrollfehler_Konfiguration()
    {
        var ae = Fachrichtung.Nach("ae");
        Assert.NotEqual(Py(G36["fachrichtungen"]!["ae"]), Py(Konfiguration(ae with { ProjektStunden = 40 })));
        // DV mit falschen Bereichsnamen (b1 und b2 vertauscht, wie im ersten Entwurf R1)
        var dv = Fachrichtung.Nach("dv");
        var vertauscht = dv with
        {
            Bereiche = [dv.Bereiche[0], dv.Bereiche[1], dv.Bereiche[2] with { Name = dv.Bereiche[3].Name },
                dv.Bereiche[3] with { Name = dv.Bereiche[2].Name }, dv.Bereiche[4]],
        };
        Assert.NotEqual(Py(G36["fachrichtungen"]!["dv"]), Py(Konfiguration(vertauscht)));
        // si als Standard: eine andere Vorgabe liefert nicht die Python-Texte von G32
        var falsch = 0;
        foreach (var g in G32["gesamt"]!["faelle"]!.AsArray().Where(g => !(bool)g![4]!).Take(500))
        {
            var r = Bewertung.Gesamt(Fachrichtung.Nach("ae"), Rollen(g![0]!.AsArray().Select(Zahl).ToArray()));
            falsch += r.Ergaenzung != (string)g[7]! ? 1 : 0;
        }
        Assert.True(falsch > 0);
    }

    [Fact]
    public void Kontrollfehler_PunktetextMitMathRound()
    {
        var falsch = G36["punkte"]!.AsArray().Count(p =>
        {
            var w = Wert((string)p!["wert"]!);
            if (!double.IsFinite(w))
            {
                return false;
            }
            var t = Math.Round(w, 1).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
            return (t.EndsWith(",0", StringComparison.Ordinal) ? t[..^2] : t) != (string)p["text"]!;
        });
        Assert.True(falsch > 0);
    }

    [Fact]
    public void Kontrollfehler_WisoRundenOhnePythonRundenStellen()
    {
        // Im WiSo-Bereich (hoechstens 30 Fragen) rechnet Math.Round gleich; der Fehler faellt erst ueber
        // die allgemeinen round(x, 1)-Werte von G32 "kontrolle" auf (WisoPunkte nutzt dieselbe Rundung).
        var falsch = G32["kontrolle"]!["runden"]!.AsArray().Count(r =>
            Bits(Math.Round(Wert((string)r![0]!), 1)) != Bits(Zahl(r[1])));
        Assert.True(falsch > 0);
        Assert.Equal(Bits(PythonRundenStellen.Runde(100.0 * 7 / 9, 1)), Bits(Bewertung.WisoPunkte(7, 9)));
    }

    [GeneratedRegex(@"(?<![^\W\d_])Z\.( ?)B\.")]
    private static partial Regex NetBeispiel();

    [GeneratedRegex(@"(?<=[.!?])\s+(?=[A-ZÄÖÜ0-9„""(])")]
    private static partial Regex NetSatz();

    [Fact]
    public void Kontrollfehler_SatztrennungMitNetW()
    {
        // Nachbau mit den Zeichenklassen von .NET (\w, \s, \d) statt Python
        List<string> Falsch(string text)
        {
            var liste = new List<string>();
            foreach (var roh in text.Split('\n'))
            {
                var zeile = Regex.Replace(roh.Trim(), @"^[-•*]+(?=\s|$)", "").Trim();
                if (zeile.Length == 0)
                {
                    continue;
                }
                var s = NetBeispiel().Replace(zeile, m => "Z\x02" + m.Groups[1].Value + "B\x02");
                for (var n = 0; n < Kriterien.Abkuerzungen.Count; n++)
                {
                    s = s.Replace(Kriterien.Abkuerzungen[n], "\0" + n + "\0", StringComparison.Ordinal);
                }
                s = Regex.Replace(s, @"(\d)\.(\d)", "$1\x01$2");
                foreach (var stueck in NetSatz().Split(s))
                {
                    var satz = stueck;
                    for (var n = 0; n < Kriterien.Abkuerzungen.Count; n++)
                    {
                        satz = satz.Replace("\0" + n + "\0", Kriterien.Abkuerzungen[n], StringComparison.Ordinal);
                    }
                    satz = satz.Replace('\x01', '.').Replace('\x02', '.').Trim();
                    if (satz.Length > 2)
                    {
                        liste.Add(satz);
                    }
                }
            }
            return liste.Count > 0 ? liste : [text.Trim()];
        }
        var falsch = G32["kontrolle"]!["kriterien"]!.AsArray().Count(t => Py(t![1]) != Py(Falsch((string)t[0]!).ToList<object?>()));
        Assert.True(falsch > 0);
    }
}
