using System.Security.Cryptography;
using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W5 (Weisung P7-10): Vergleichswerte G49 (<c>spiel_orte.json</c>, Erzeuger
/// <c>werkzeuge/vergleich/erzeuge_spiel_orte.py</c>). Auf den Spielstaenden aus G44 Aktionen (Moebel,
/// Umzuege) und G47 Firma (Personal, Filiale) ergeben Grundriss mit sichtbarer Einrichtung (zu Hause
/// home_building), Personen am Ort, die Texte unter der Ansicht (place_message) an vielen Punkten,
/// Wegpruefung und Vorschau beim Aufstellen in C# genau die Werte von Python.
/// </summary>
public class SpielOrteTests
{
    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_orte.json");

    private static readonly string[] PersonFelder =
        ["id", "name", "rolle", "platz", "mitarbeiter", "projekt_text", "kundenticket", "umsatz", "in_weiterbildung", "im_coaching", "lagerist", "macke"];

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static List<double> Raster(double von, double bis)
    {
        var werte = new List<double>();
        for (var v = von; v < bis; v += 1.25)
        {
            werte.Add(v);
        }
        return werte;
    }

    private static List<(double X, double Y)> Punkte(PyObjekt gebaeude)
    {
        var eingang = (PyObjekt)((PyObjekt)gebaeude["flur"]!)["eingang"]!;
        var mitte = (Py.F(eingang["von"]) + Py.F(eingang["bis"])) / 2.0;
        var punkte = new List<(double, double)> { (0.6, mitte), (1.6, mitte) };
        var x = 0.4;
        while (x < Py.F(gebaeude["breite"]))
        {
            var y = 0.45;
            while (y < Py.F(gebaeude["hoehe"]))
            {
                punkte.Add((x, y));
                y += 1.25;
            }
            x += 1.25;
        }
        return punkte;
    }

    private static List<object?> Knoepfe(List<(string Aktion, string Name)> knoepfe) =>
        [.. knoepfe.Select(k => (object?)new List<object?> { k.Aktion, k.Name })];

    private static PyObjekt OrtWerte(Spielstand stand, string ort)
    {
        var gebaeude = SpielOrte.Gebaeude(ort, stand);
        var leute = ort == SpielDaten.OrtZuhause ? [] : SpielOrte.Personen(ort, stand);
        var nachrichten = new List<object?>();
        foreach (var (x, y) in Punkte(gebaeude))
        {
            var (titel, text, knoepfe) = SpielOrte.Nachricht(ort, x, y, null, stand, gebaeude);
            nachrichten.Add(new List<object?> { x, y, null, titel, text, Knoepfe(knoepfe) });
        }
        foreach (var person in leute)
        {
            var platz = (List<object?>)person["platz"]!;
            var (titel, text, knoepfe) = SpielOrte.Nachricht(ort, Py.F(platz[0]), Py.F(platz[1]), person, stand, gebaeude);
            nachrichten.Add(new List<object?> { platz[0], platz[1], person["id"], titel, text, Knoepfe(knoepfe) });
        }
        var personen = leute.Select(p =>
        {
            var auswahl = new PyObjekt();
            foreach (var k in PersonFelder.Where(k => Py.Hat(p, k)))
            {
                auswahl.Add(k, p[k]);
            }
            return (object?)auswahl;
        }).ToList();
        return new PyObjekt { { "ort", ort }, { "gebaeude", gebaeude }, { "personen", personen }, { "nachrichten", nachrichten } };
    }

    private static PyObjekt WohnungWerte(Spielstand stand)
    {
        var gesperrt = Wohnung.Wegpruefung(stand);
        var gebaeude = (PyObjekt)Wohnung.Bleibe(stand.Wohnung, Inhalt)!["gebaeude"]!;
        var punkte = ((List<object?>)gebaeude["raeume"]!).Select(r => (PyObjekt)r!)
            .Select(r => (Py.F(r["x"]) + Py.F(r["w"]) / 2.0, Py.F(r["y"]) + Py.F(r["h"]) / 2.0)).ToList();
        punkte.AddRange([(0.3, 0.3), (Py.F(gebaeude["breite"]) - 0.4, Py.F(gebaeude["hoehe"]) / 2.0), (2.0, Py.F(((PyObjekt)gebaeude["flur"]!)["y"]) + 0.5)]);
        var vorschau = new List<object?>();
        foreach (var stueck in stand.Moebel.Keys.Select(Py.Str).OrderBy(k => k, StringComparer.Ordinal))
        {
            foreach (var (cx, cy) in punkte)
            {
                foreach (var dreh in new long[] { 0, 1 })
                {
                    var p = Wohnung.Vorschau(stand, stueck, cx, cy, dreh);
                    var (text, farbe) = Wohnung.VorschauText(p);
                    vorschau.Add(new List<object?> { stueck, cx, cy, dreh, p, new List<object?> { text, farbe } });
                }
            }
        }
        return new PyObjekt
        {
            { "wohnung", stand.Wohnung }, { "gesperrt", gesperrt.Cast<object?>().ToList() }, { "warnung", Wohnung.WegWarnung(gesperrt) }, { "vorschau", vorschau },
        };
    }

    private static object? Stichpunkt(Spielstand stand)
    {
        var orte = new List<string> { SpielDaten.OrtZuhause, SpielDaten.OrtBuero };
        orte.AddRange(SpielDaten.Kundenorte(Inhalt).Where(o => Py.F(SpielDaten.Get(o, "ab_tag", 1L)) <= stand.Tag).Select(o => Py.Str(o["id"])));
        if (Py.Wahr(stand.Zweigstelle))
        {
            orte.Add(SpielDaten.OrtFiliale);
        }
        return PyAusgabe.Wert(new PyObjekt
        {
            { "orte", orte.Select(o => (object?)OrtWerte(stand, o)).ToList() }, { "wohnung", WohnungWerte(stand) },
        });
    }

    private static List<SpielEreignis> Ereignisse(string quelle, string name) =>
        SpielNeuaufbauTests.Ereignisse(VergleichsHilfe.Lade(quelle + ".json").GetProperty("laeufe").EnumerateArray()
            .First(l => l.GetProperty("name").GetString() == name).GetProperty("ereignisse"));

    /// <summary>Gleicher Text, sonst die Stelle der ersten Abweichung mit Umgebung.</summary>
    private static void Gleich(string wo, string soll, string ist)
    {
        if (soll == ist)
        {
            return;
        }
        var i = 0;
        while (i < soll.Length && i < ist.Length && soll[i] == ist[i])
        {
            i++;
        }
        var von = Math.Max(0, i - 160);
        Assert.Fail(wo + " weicht ab bei Zeichen " + i + "\n  Python: " + soll[von..Math.Min(soll.Length, i + 160)]
            + "\n  C#:     " + ist[von..Math.Min(ist.Length, i + 160)]);
    }

    [Fact]
    public void JederStichpunktWiePython()
    {
        var anzahl = 0;
        var voll = 0;
        foreach (var lauf in Soll.GetProperty("laeufe").EnumerateArray())
        {
            var quelle = lauf.GetProperty("quelle").GetString()!;
            var name = lauf.GetProperty("name").GetString()!;
            var ereignisse = Ereignisse(quelle, name);
            foreach (var punkt in lauf.GetProperty("staende").EnumerateArray())
            {
                var n = punkt.GetProperty("n").GetInt32();
                var stand = new Spielstand(ereignisse.Take(n), Inhalt);
                var ist = PythonJson.Text(Stichpunkt(stand));
                var wo = quelle + "/" + name + " n=" + n;
                if (punkt.TryGetProperty("werte", out var werte))
                {
                    Gleich(wo, PythonJson.Text(Py.Lesen(werte.GetRawText())), ist);
                    voll++;
                }
                Assert.True(punkt.GetProperty("sha256").GetString() == Convert.ToHexStringLower(SHA256.HashData(PythonJson.Utf8(ist))), wo + ": SHA-256 weicht ab");
                anzahl++;
            }
        }
        Assert.True(anzahl >= 50, "zu wenige Stichpunkte: " + anzahl);
        Assert.True(voll >= 16, "zu wenige volle Stichpunkte: " + voll);
    }
}
