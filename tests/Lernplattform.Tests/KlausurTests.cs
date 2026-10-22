using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Zufall;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, W2: Klausursimulation gegen G32 (tests/Vergleich/pruefung.json, erzeugt von
/// werkzeuge/vergleich/erzeuge_pruefung.py aus Python v0.63.2, Zeitzone Europe/Berlin).
/// Jeder Teil von G32 wird hier gegen den C#-Kern geprueft; Zahlen bitgenau, Texte zeichengleich,
/// JSON mit Reihenfolge der Schluessel und Unterschied ganze Zahl / Kommazahl.
/// </summary>
public class KlausurTests
{
    internal static readonly JsonNode G32 = LadeG32();
    internal static readonly Lerninhalte Inhalte = Lerninhalte.Laden(Pfade.Inhalte);
    internal static readonly KlausurInhalte Klausurinhalte = new(Inhalte, Umbenennungen.Laden(Pfade.Inhalte));
    internal static readonly TimeZoneInfo Berlin = SpeichernTests.Berlin;

    private static JsonNode LadeG32()
    {
        using var f = File.OpenRead(Pfade.Vergleich("pruefung.json"));
        return JsonNode.Parse(f, documentOptions: new JsonDocumentOptions { MaxDepth = 256 })!;
    }

    /// <summary>JSON wie Python es schreibt (Reihenfolge, 5 ist nicht 5.0).</summary>
    internal static string Py(JsonNode? knoten) => PythonDump.Text(Json.Wert(knoten));

    internal static string Py(object? wert) => PythonDump.Text(wert);

    internal static PyObjekt Objekt(IEnumerable<KeyValuePair<string, object?>> paare)
    {
        var o = new PyObjekt();
        o.AddRange(paare);
        return o;
    }

    private static double Zahl(JsonNode? knoten) => Json.Zahl(knoten)!.Value;

    private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

    [Fact]
    public void Pruefungsbereiche()
    {
        var erwartet = G32["pruefungen"]!.AsArray();
        Assert.Equal(erwartet.Count, Klausur.Arten.Count);
        for (var i = 0; i < erwartet.Count; i++)
        {
            var e = erwartet[i]!.AsObject();
            var a = Klausur.Arten[i];
            var ist = new PyObjekt { { "art", a.Art }, { "kurz", a.Kurz }, { "minuten", (long)a.Minuten }, { "quelle", a.Quelle }, { "name", a.Name } };
            if (a.QuizThemen is { } q)
            {
                ist.Add("quiz_themen", q.ToList<object?>());
            }
            else if (a.Pool is { } pool)
            {
                ist.Add("themen", "4ausPool");
                ist.Add("pool", pool.ToList<object?>());
            }
            else
            {
                ist.Add("themen", a.Themen!.Select(t => (object?)t.ToList<object?>()).ToList());
            }
            Assert.Equal(Py(e), Py(ist));
        }
    }

    [Fact]
    public void KriterienUndTeilaufgaben()
    {
        var k = G32["kriterien"]!;
        var szenarien = k["szenarien"]!.AsArray();
        Assert.Equal(Inhalte.Ap1Szenarien.Count + Inhalte.Ap2Szenarien.Count, szenarien.Count);
        foreach (var s in szenarien)
        {
            var quelle = (string)s!["quelle"]!;
            var index = (int)s["index"]!;
            var teile = Klausurinhalte.Teile(quelle, index);
            var ort = quelle + " " + index;
            if (s["teile"] is null)
            {
                Assert.True(teile is null, ort);
                var szenario = Klausurinhalte.Quelle(quelle)[index];
                var aufgabe = Kriterien.Aufteilen(szenario.Text("text")!);
                var loesung = Kriterien.Aufteilen(szenario.Text("solution")!);
                Assert.Equal(Py(s["split"]), Py(new List<object?>
                {
                    new List<object?> { aufgabe.Einleitung, aufgabe.Teile.ToList<object?>() },
                    new List<object?> { loesung.Einleitung, loesung.Teile.ToList<object?>() },
                }));
                continue;
            }
            Assert.True(teile is not null, ort);
            Assert.Equal(Py(s["teile"]), Py(new PyObjekt
            {
                { "titel", teile.Titel }, { "thema", teile.Thema }, { "einleitung", teile.Einleitung },
                { "teile", teile.Teile.Select(t => (object?)new List<object?> { t.Text, (long)t.Punkte, t.Loesung, t.Kriterien.ToList<object?>() }).ToList() },
            }));
        }
        foreach (var (schluessel, liste) in k["nutzbar"]!.AsObject())
        {
            var teile = schluessel.Split('|');
            Assert.Equal(Py(liste), Py(Klausurinhalte.Nutzbar(teile[0], teile[1]).Select(i => (object?)(long)i).ToList()));
        }
        foreach (var z in k["zusatz"]!.AsArray())
        {
            var text = (string)z![0]!;
            var (einleitung, teile) = Kriterien.Aufteilen(text);
            Assert.Equal(Py(z[1]), Py(new List<object?> { einleitung, teile.ToList<object?>() }));
            Assert.Equal(Py(z[2]), Py(Kriterien.Aus(text).ToList<object?>()));
        }
        foreach (var t in k["teilpunkte"]!.AsArray())
        {
            Assert.Equal(Py(t![1]), Py(Kriterien.Teilpunkte((int)t[0]!).Select(p => (object?)(long)p).ToList()));
        }
    }

    internal static List<KlausurEintrag> Verlauf(JsonNode? liste) => liste!.AsArray()
        .Select(e => new KlausurEintrag("", Json.Text(e!["art"]), null, null, e["daten"]?.DeepClone(), null)).ToList();

    private static DateTime NaivBerlin(string iso) =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture), Berlin).DateTime;

    [Fact]
    public void NeuePruefung()
    {
        var neu = G32["neu"]!;
        var verlaeufe = neu["verlaeufe"]!.AsObject();
        var jetzt = (string)neu["jetzt"]!;
        var anzahl = 0;
        foreach (var fall in neu["faelle"]!.AsArray())
        {
            var art = (string)fall!["art"]!;
            var verlauf = Verlauf(verlaeufe[art + "|" + (string)fall["verlauf"]!]);
            var startwert = (long)fall["startwert"]!;
            var ort = art + " " + (string)fall["verlauf"]! + " " + startwert;
            var naiv = Klausur.NeuOrtszeit(Klausurinhalte, art, verlauf, new PythonZufall(startwert), NaivBerlin(jetzt), Berlin);
            Assert.True(Py(fall["zustand"]) == Py(naiv), ort);
            var echt = Klausur.Neu(Klausurinhalte, art, verlauf, new PythonZufall(startwert), DateTimeOffset.Parse(jetzt, CultureInfo.InvariantCulture), Berlin);
            Assert.True(Py(fall["zustand"]) == Py(echt), ort);
            Assert.Equal(Py(fall["benutzt"]), Py(Klausur.Gestellt(Klausurinhalte, verlauf, art).Sortiert().ToList<object?>()));
            anzahl++;
        }
        Assert.Equal(90, anzahl);
        // Zeitumstellung: Python mit naiver Ortszeit (fold=0) und mit echtem Augenblick
        foreach (var m in neu["momente"]!.AsArray())
        {
            var moment = (string)m![0]!;
            var naiv = Klausur.NeuOrtszeit(Klausurinhalte, Klausur.Wiso, [], new PythonZufall(7), NaivBerlin(moment), Berlin);
            Assert.Equal((string)m[1]!, (string)naiv["start"]!);
            Assert.Equal((long)m[2]!, (long)naiv["start_utc"]!);
            var echt = Klausur.Neu(Klausurinhalte, Klausur.Wiso, [], new PythonZufall(7), DateTimeOffset.Parse(moment, CultureInfo.InvariantCulture), Berlin);
            Assert.Equal((string)m[3]!, (string)echt["start"]!);
            Assert.Equal((long)m[4]!, (long)echt["start_utc"]!);
        }
    }

    [Fact]
    public void Zeit()
    {
        var zeit = G32["zeit"]!;
        var anzahl = 0;
        foreach (var fall in zeit["faelle"]!.AsArray())
        {
            var zustand = fall!["zustand"]!.DeepClone().AsObject();
            var jetzt = Zahl(fall["jetzt_utc"]);
            var ort = zustand.ToJsonString() + " @ " + jetzt.ToString("R", CultureInfo.InvariantCulture);
            Assert.True(Bits(Zahl(fall["vergangen"])) == Bits(Klausur.Vergangen(zustand, jetzt, Berlin)), ort);
            Assert.True((long)fall["rest"]! == Klausur.Rest(zustand, jetzt, Berlin), ort);
            Assert.Equal((string)fall["frist"]!, Klausur.Frist(zustand).ToString(Klausur.ZeitFormat, CultureInfo.InvariantCulture));
            Klausur.Abgeben(zustand, jetzt, Berlin);
            Assert.True((long)fall["dauer"]! == (long)zustand["dauer"]!, ort);
            Assert.Equal((string)fall["phase"]!, (string)zustand["phase"]!);
            anzahl++;
        }
        Assert.Equal(144, anzahl);
        foreach (var t in zeit["texte"]!.AsArray())
        {
            Assert.Equal((string)t![1]!, Klausur.Zeittext((long)t[0]!));
        }
    }

    [Fact]
    public void Bewertung_WieInPython()
    {
        var b = G32["bewertung"]!;
        foreach (var v in b["vorschlag"]!.AsArray())
        {
            var (maximum, n, k) = ((int)v![0]!, (int)v[1]!, (int)v[2]!);
            var haken = Enumerable.Repeat(true, k).Concat(Enumerable.Repeat(false, n - k)).ToList();
            Assert.Equal((int)v[3]!, Klausur.Vorschlag(maximum, haken));
        }
        foreach (var a in b["auswertungen"]!.AsArray())
        {
            var zustand = a!["zustand"]!.DeepClone().AsObject();
            if (a["wiso"] is JsonArray wiso)
            {
                Assert.Equal(Py(wiso), Py(Bewertung.Wiso(Klausurinhalte, zustand)
                    .Select(z => (object?)new List<object?> { z.Frage, z.Gewaehlt, z.Richtig, z.Ok }).ToList()));
            }
            var ergebnis = Bewertung.Auswerten(Klausurinhalte, zustand);
            var e = a["ergebnis"]!;
            Assert.Equal(Bits(Zahl(e["punkte"])), Bits(ergebnis.Punkte));
            Assert.Equal((string)e["note"]!, ergebnis.Note);
            Assert.Equal((long)e["dauer"]!, ergebnis.Dauer);
            Assert.Equal(Py(e["daten"]), Py(ergebnis.Daten));
            Assert.Equal((string)a["daten_text"]!, PythonDump.Text(ergebnis.Daten));
            Assert.Equal(Py(a["schwach"]), Py(Bewertung.Schwach(ergebnis.Themen).ToList<object?>()));
        }
        foreach (var s in b["schwach"]!.AsArray())
        {
            var themen = s![0]!.AsObject().Select(p => (p.Key, Zahl(p.Value![0]), Zahl(p.Value[1]))).ToList();
            Assert.Equal(Py(s[1]), Py(Bewertung.Schwach(themen).ToList<object?>()));
        }
        var letzte = b["letzte_ergebnisse"]!;
        var ist = Bewertung.Letzte(letzte[0]!.AsArray().Select(e => ((string)e!["art"]!, Zahl(e["punkte"]))));
        Assert.Equal(Py(letzte[1]), Py(Objekt(ist.Select(p => new KeyValuePair<string, object?>(p.Key, p.Value)))));
    }

    /// <summary>Spalten der Rollen (SI): Python rechnet mit den Bereichsnamen, C# mit Rollen.</summary>
    private static readonly string[] Si = ["ap1", "projekt", "konzeption", "netzwerke", "wiso"];

    [Fact]
    public void Gesamtergebnis_WieInPython()
    {
        var g = G32["gesamt"]!;
        var si = Fachrichtung.Nach("si");
        foreach (var p in g["punkte"]!.AsArray())
        {
            var wert = double.Parse(((string)p!["wert"]!).Replace("inf", "Infinity", StringComparison.Ordinal).Replace("nan", "NaN", StringComparison.Ordinal),
                CultureInfo.InvariantCulture);
            Assert.Equal((string)p["text"]!, PythonRunden.PunkteText(wert));
            if (p["abgerundet"] is { } ab)
            {
                Assert.Equal(Bits(Zahl(ab)), Bits(Bewertung.Abrunden(wert)));
                Assert.Equal((string)p["note"]!, Trainer.Note(wert));
            }
        }
        Assert.Equal(Py(g["regeln"]), Py(Bewertung.Regeln(si, [50.0, 50.0, 50.0, 50.0, 50.0]).Select(r => (object?)r.Text).ToList()));
        var anzahl = 0;
        foreach (var f in g["faelle"]!.AsArray())
        {
            var werte = f![0]!.AsArray().Select(Zahl).ToArray();
            var r = Bewertung.Gesamt(si, Fachrichtung.Rollen.Select((rolle, i) => (rolle, werte[i])).ToDictionary());
            var ort = Py(f[0]);
            Assert.True(r.Vollstaendig, ort);
            Assert.True(Bits(Zahl(f[1])) == Bits(r.Gesamt), ort);
            Assert.True(Bits(Zahl(f[2])) == Bits(r.Teil2), ort);
            Assert.Equal((string)f[3]!, r.Note);
            Assert.Equal((bool)f[4]!, r.Bestanden);
            Assert.Equal(Py(f[5]), Py(r.Regeln.Select(x => (object?)x.Ok).ToList()));
            Assert.Equal((string)f[6]!, r.Regeln[2].Text);
            Assert.True((string)f[7]! == r.Ergaenzung, ort);
            anzahl++;
        }
        Assert.Equal(6 * 6 * 6 * 6 * 6 + 300, anzahl);
        foreach (var f in g["fehlend"]!.AsArray())
        {
            var werte = f![0]!.AsObject().ToDictionary(p => Fachrichtung.Rollen[Array.IndexOf(Si, p.Key)], p => Zahl(p.Value));
            var r = Bewertung.Gesamt(si, werte);
            Assert.False(r.Vollstaendig);
            Assert.False((bool)f[1]!["complete"]!);
            Assert.Equal(Py(f[1]!["fehlend"]), Py(r.Fehlend.ToList<object?>()));
        }
        Assert.Equal(Py(g["gewichte"]), Py(Objekt(si.Bereiche.Select((b, i) => new KeyValuePair<string, object?>(Si[i], (long)b.Gewicht)))));
        Assert.Equal(Py(g["teil2"]), Py(Si.Skip(1).ToList<object?>()));
        Assert.Equal(Py(g["bereiche"]), Py(Objekt(si.Bereiche.Select((b, i) => new KeyValuePair<string, object?>(Si[i], b.Kurz)))));
        var ergaenzung = Bewertung.Gesamt(si, new Dictionary<string, double> { ["ap1"] = 100, ["projekt"] = 100, ["b1"] = 0, ["b2"] = 0, ["wiso"] = 0 });
        Assert.Equal((string)G32["ergaenzung_texte"]![0]!, ergaenzung.Ergaenzung);
    }

    [Fact]
    public void LaufendePruefung_Anpassen()
    {
        var m = G32["migration"]!;
        foreach (var f in m["faelle"]!.AsArray())
        {
            var zustand = f![0]!.DeepClone().AsObject();
            LaufendePruefung.Migrieren(Klausurinhalte, zustand);
            Assert.Equal(Py(f[1]), Py(zustand));
        }
        Assert.Equal(45, m["faelle"]!.AsArray().Count);
        foreach (var a in m["ausrichten"]!.AsArray())
        {
            var haken = a![0]!.AsArray().Select(h => (bool)h!).ToList();
            var alt = a[1]!.AsArray().Select(h => (string)h!).ToList();
            var neu = a[2]!.AsArray().Select(h => (string)h!).ToList();
            Assert.Equal(Py(a[3]), Py(LaufendePruefung.Ausrichten(haken, alt, neu).Select(h => (object?)h).ToList()));
        }
    }

    [Fact]
    public void Abschluss_SchreibtDieZeilenWiePython()
    {
        using var ordner = new TempOrdner();
        var abschluss = G32["abschluss"]!;
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        foreach (var aufruf in abschluss["aufrufe"]!.AsArray())
        {
            var uids = aufruf!["uids"]!.AsArray().Select(u => (string)u!).ToList();
            var jetzt = DateTimeOffset.Parse((string)aufruf["utc"]!, CultureInfo.InvariantCulture);
            var naechste = 0;
            var schreiber = new LernstandSchreiber(lernstand, SpeichernTests.FesteUhr(() => jetzt), () => uids[naechste++]);
            var einstellungen = new JsonObject();
            var ablage = new LaufendePruefung(() => einstellungen.DeepClone().AsObject(), a => { a(einstellungen); return true; });
            var zustand = aufruf["zustand"]!.DeepClone().AsObject();
            zustand[LaufendePruefung.KennungSchluessel] = uids[^1];
            Assert.NotNull(Abschluss.Abschliessen(Klausurinhalte, schreiber, ablage, zustand));
            Assert.Equal(uids.Count - 1, naechste);
        }
        using var db = VergleichsHilfe.Oeffnen(lernstand.Pfad);
        foreach (var (tabelle, daten) in abschluss["tabellen"]!.AsObject())
        {
            var spalten = daten!["spalten"]!.AsArray().Select(s => (string)s!).ToList();
            var zeilen = VergleichsHilfe.Abfrage(db, $"SELECT {string.Join(", ", spalten)} FROM {tabelle} ORDER BY id");
            var typen = VergleichsHilfe.Abfrage(db, $"SELECT {string.Join(", ", spalten.Select(s => $"typeof({s})"))} FROM {tabelle} ORDER BY id");
            var erwartet = daten["zeilen"]!.AsArray();
            Assert.Equal(erwartet.Count, zeilen.Count);
            for (var i = 0; i < zeilen.Count; i++)
            {
                Assert.Equal(VergleichsHilfe.Zeile(JsonDocument.Parse(erwartet[i]!.ToJsonString()).RootElement), VergleichsHilfe.Zeile(zeilen[i]));
                Assert.Equal(Py(daten["typen"]![i]), Py(typen[i].ToList()));
            }
        }
    }

    /// <summary>Nr 81 (Wortliste W2): erst mit der Ansicht "Uebersicht" des Abschlussprojekts (W3), bekannte Abweichung.</summary>
    internal static readonly IReadOnlyDictionary<string, string> BekannteAbweichungen = new Dictionary<string, string>
    {
        ["ProjektHinweisW3"] = "Nr 81 erst in W3 (Weisung P4-6, 3a): die Ansicht \"Übersicht\" des Abschlussprojekts fehlt in W2",
    };

    [Fact]
    public void Texte_ZeichengleichZuPython()
    {
        var anzahl = 0;
        foreach (var (name, text) in G32["texte"]!.AsObject())
        {
            var feld = typeof(PruefungTexte).GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (BekannteAbweichungen.ContainsKey(name))
            {
                Assert.Null(feld);
                Assert.Equal(" Die Projektnote schätzt du im Abschlussprojekt unter „Übersicht“.", (string)text!);
                continue;
            }
            Assert.True(feld is not null, name + " fehlt in PruefungTexte");
            Assert.Equal((string)text!, (string)feld.GetValue(null)!);
            anzahl++;
        }
        Assert.Equal(67, anzahl);
        Assert.Equal(TrainerTexte.Arten.Select(a => a.Text), [PruefungTexte.Uebung, PruefungTexte.Pruefung]);
        Assert.Equal("Note nach IHK-Schlüssel (Orientierung, keine amtliche Note)", TrainerTexte.NoteLang);
        // Kurz- und Langnamen der Auswahl (Wortliste Nr 9-13) wie EXAMS, Gesamtergebnis wie AREA_NAME
        Assert.Equal(["AP1", "Konzeption", "Netzwerke", "WiSo"], Klausur.Arten.Select(a => a.Kurz));
    }
}
