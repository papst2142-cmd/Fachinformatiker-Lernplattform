using System.Security.Cryptography;
using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W2: Vergleichswerte G46 (<c>spiel_bewertung.json</c>, Erzeuger
/// <c>werkzeuge/vergleich/erzeuge_spiel_bewertung.py</c>): Loesung, Probleme, Pruefung und
/// Nutzdaten der Auswertung jedes Auftrags genau wie <c>fisi_game.evaluate</c> (Python 0.63.2),
/// fuer feste Antworten und fuer die Auswertungen zweier mit der Python-Spiel-API gespielter Laeufe.
/// </summary>
public class SpielBewertungTests
{
    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_bewertung.json");

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static object? W(JsonElement e) => Py.Lesen(e.GetRawText());

    private static string Sha(object? wert) => Convert.ToHexStringLower(SHA256.HashData(PythonJson.Utf8(PythonJson.Text(wert))));

    private static string T(object? wert) => PythonJson.Text(wert);

    private static PyObjekt EinfachBalancing() =>
        Bewertung.EinfachBalancing(new Spielstand(SpielNeuaufbauTests.Ereignisse(Soll.GetProperty("einfach_ereignisse")), Inhalt));

    private static List<object?> HardwareKennungen()
    {
        var hw = (PyObjekt)Inhalt["hardware"]!;
        return [.. ((List<object?>)hw["teile"]!).Concat((List<object?>)hw["ersatzteile"]!).Select(t => ((PyObjekt)t!)["id"])];
    }

    /// <summary>Wert oder die Art der Ausnahme (Python speichert {"ausnahme": Name}).</summary>
    private static void GleichOderAusnahme(JsonElement soll, Func<object?> rechnen, string wo)
    {
        if (soll.ValueKind == JsonValueKind.Object && soll.TryGetProperty("ausnahme", out var art))
        {
            var fehler = Record.Exception(() => rechnen());
            Assert.True(fehler is not null, wo + ": Python wirft " + art.GetString() + ", C# nicht");
            return;
        }
        Assert.Equal(wo + ": " + T(W(soll)), wo + ": " + T(rechnen()));
    }

    [Fact]
    public void EinfachBalancingWiePython()
    {
        var soll = Soll.GetProperty("balancing_einfach");
        var ist = EinfachBalancing();
        Assert.Equal(T(W(soll.GetProperty("fehlerkosten"))), T(ist["fehlerkosten"]));
        Assert.Equal(T(W(soll.GetProperty("verlust_faktor"))), T(ist["verlust_faktor"]));
        Assert.Equal(soll.GetProperty("sha256").GetString(), Sha(ist));
    }

    [Fact]
    public void JederAuftragWiePython()
    {
        var kennungen = HardwareKennungen();
        var lager = new PyDict();
        for (var i = 0; i < kennungen.Count; i++)
        {
            lager[kennungen[i]] = (long)(i % 3);
        }
        var themen = new PyObjekt();
        foreach (var a in ((List<object?>)Inhalt["aufgaben"]!).Cast<PyObjekt>())
        {
            foreach (var (k, _) in a["anforderungen"] as PyObjekt ?? [])
            {
                Py.Setzen(themen, k, 100.0);
            }
        }
        var einfach = EinfachBalancing();
        var antworten = 0;
        foreach (var eintrag in Soll.GetProperty("aufgaben").EnumerateArray())
        {
            var id = eintrag.GetProperty("id").GetString()!;
            var auftrag = Py.Flach(SpielDaten.Auftrag(id, Inhalt)!);
            if (eintrag.GetProperty("extra").ValueKind == JsonValueKind.Object)
            {
                foreach (var (k, v) in (PyObjekt)W(eintrag.GetProperty("extra"))!)
                {
                    Py.Setzen(auftrag, k, v);
                }
            }
            var wo = id + (eintrag.GetProperty("extra").ValueKind == JsonValueKind.Object ? " (extra)" : "");
            var bereitB = (Py.Hat(auftrag, "teile") ? (List<object?>)auftrag["teile"]! : []).Concat(kennungen.Where(k => Py.Kleiner(0L, lager[k]))).ToList();
            GleichOderAusnahme(eintrag.GetProperty("loesung"), () => Bewertung.Loesung(auftrag, null, Inhalt), wo + " Loesung");
            GleichOderAusnahme(eintrag.GetProperty("loesung_b"), () => Bewertung.Loesung(auftrag, bereitB, Inhalt), wo + " Loesung B");
            if (eintrag.TryGetProperty("koerbe", out var koerbe))
            {
                var ist = Bewertung.Warenkoerbe(auftrag, Inhalt);
                Assert.Equal(koerbe.GetProperty("anzahl").GetInt32(), ist.Count);
                Assert.Equal(T(W(koerbe.GetProperty("erste"))), T(ist.Take(3).Cast<object?>().ToList()));
                Assert.Equal(koerbe.GetProperty("sha256").GetString(), Sha(ist.Cast<object?>().ToList()));
            }
            foreach (var zeile in eintrag.GetProperty("antworten").EnumerateArray())
            {
                var name = wo + " " + zeile.GetProperty("name").GetString();
                var antwort = W(zeile.GetProperty("antwort"));
                var a = zeile.GetProperty("a");
                GleichOderAusnahme(a, () => Bewertung.Auswerten(auftrag, Py.Kopie(antwort), false, [], 3, null, null, Inhalt, null), name + " A");
                var probleme = zeile.TryGetProperty("probleme", out var p) ? p
                    : a.TryGetProperty("probleme", out var ap) ? ap : JsonDocument.Parse("[]").RootElement;
                GleichOderAusnahme(probleme, () => Bewertung.AntwortProbleme(auftrag, Py.Kopie(antwort), null, Inhalt), name + " Probleme");
                GleichOderAusnahme(zeile.GetProperty("pruefung"), () =>
                {
                    var (richtig, fehler) = Bewertung.Pruefen(auftrag, Py.Kopie(antwort), null, Inhalt);
                    return new List<object?> { richtig, fehler };
                }, name + " Pruefung");
                if (zeile.GetProperty("b_richtig").ValueKind == JsonValueKind.Null)
                {
                    Assert.NotNull(Record.Exception(() => Bewertung.Auswerten(auftrag, Py.Kopie(antwort), true, themen, 17, einfach, bereitB, Inhalt, lager)));
                }
                else
                {
                    var b = Bewertung.Auswerten(auftrag, Py.Kopie(antwort), true, themen, 17, einfach, bereitB, Inhalt, lager);
                    Assert.True(zeile.GetProperty("b").GetString() == Sha(b), name + " B weicht ab: " + T(b));
                }
                antworten++;
            }
        }
        Assert.True(antworten > 4000);
    }

    [Fact]
    public void GespielteAuswertungenWiePython()
    {
        var anzahl = 0;
        foreach (var lauf in Soll.GetProperty("gespielt").EnumerateArray())
        {
            var ereignisse = SpielNeuaufbauTests.Ereignisse(lauf.GetProperty("ereignisse"));
            foreach (var fall in lauf.GetProperty("faelle").EnumerateArray())
            {
                var n = fall.GetProperty("n").GetInt32();
                var wo = lauf.GetProperty("name").GetString() + " n=" + n + " " + fall.GetProperty("aufgabe").GetString();
                var stand = new Spielstand(ereignisse.Take(n), Inhalt);
                Assert.Equal(fall.GetProperty("tag").GetInt64(), stand.Tag);
                var auftrag = stand.Vorbereitet(SpielDaten.Auftrag(fall.GetProperty("aufgabe").GetString(), Inhalt))!;
                var daten = Bewertung.Auswerten(auftrag, W(fall.GetProperty("antwort")), fall.GetProperty("hilfe").GetBoolean(),
                    (PyObjekt)W(fall.GetProperty("wissen"))!, stand.Tag, Bewertung.EinfachBalancing(stand), stand.VerfuegbareTeile(auftrag), Inhalt, stand.Lager());
                Assert.Equal(wo + ": " + T(W(fall.GetProperty("daten"))), wo + ": " + T(daten));
                anzahl++;
            }
        }
        Assert.True(anzahl > 400);
    }

    private static double ZahlAusSoll(JsonElement e) => e.ValueKind == JsonValueKind.String
        ? e.GetString() switch { "inf" => double.PositiveInfinity, "-inf" => double.NegativeInfinity, _ => double.NaN }
        : e.GetDouble();

    [Fact]
    public void TexteWiePython()
    {
        var t = Soll.GetProperty("texte");
        foreach (var f in t.GetProperty("zahl").EnumerateArray())
        {
            var ist = Bewertung.ZahlLesen(W(f[0]));
            var wo = "parse_number(" + f[0].GetRawText() + ")";
            if (f[1].ValueKind == JsonValueKind.Null)
            {
                Assert.True(ist is null, wo + " = None, C# " + ist);
            }
            else
            {
                Assert.True(ist is double d && (d.Equals(ZahlAusSoll(f[1])) || (double.IsNaN(d) && double.IsNaN(ZahlAusSoll(f[1])))), wo + " = " + f[1] + ", C# " + ist);
            }
        }
        foreach (var f in t.GetProperty("praefix").EnumerateArray())
        {
            GleichOderAusnahme(f[1], () => Bewertung.PraefixLesen(W(f[0])), "parse_prefix(" + f[0].GetRawText() + ")");
        }
        foreach (var f in t.GetProperty("ip").EnumerateArray())
        {
            Assert.Equal(T(W(f[1])), T(Bewertung.IpLesen(W(f[0]))));
        }
        foreach (var f in t.GetProperty("num").EnumerateArray())
        {
            object? wert = f[0].ValueKind == JsonValueKind.String ? ZahlAusSoll(f[0]) : W(f[0]);
            Assert.Equal(f[0].GetRawText() + " " + f[1].GetString(), f[0].GetRawText() + " " + PyFormat.Num(wert));
        }
        foreach (var f in t.GetProperty("euro").EnumerateArray())
        {
            Assert.Equal(f[0].GetRawText() + " " + f[1].GetString(), f[0].GetRawText() + " " + PyFormat.Euro(W(f[0])));
        }
        foreach (var f in t.GetProperty("plural").EnumerateArray())
        {
            Assert.Equal(f[1].GetString(), PyFormat.Plural(W(f[0]), "Arbeitstag", "Arbeitstage"));
        }
        foreach (var f in t.GetProperty("netzteil").EnumerateArray())
        {
            var cpu = SpielDaten.Teil(f[0].GetString(), Inhalt);
            var gpu = f[1].ValueKind == JsonValueKind.Null ? null : SpielDaten.Teil(f[1].GetString(), Inhalt);
            Assert.Equal(f[2].GetInt64(), Bewertung.NetzteilNoetig(cpu, gpu, Inhalt));
        }
    }

    [Fact]
    public void ProzentWiePython()
    {
        // Feste Faelle des %-Operators (aus python3.12 -I abgelesen)
        Assert.Equal("3 von 7", PyFormat.Prozent("%d von %d", 3L, 7.9));
        Assert.Equal("50 % Reserve", PyFormat.Prozent("%d %% Reserve", 50L));
        Assert.Equal("True None 2.5", PyFormat.Prozent("%s %s %s", true, null, 2.5));
        Assert.Equal("0.12 0.14 -0.00", PyFormat.Prozent("%.2f %.2f %.2f", 0.125, 0.135, -0.001));
        Assert.Equal("  -5|7    |+3|007", PyFormat.Prozent("%4d|%-5d|%+d|%03d", -5L, 7L, 3L, 7L));
        Assert.ThrowsAny<FormatException>(() => PyFormat.Prozent("%d", "x"));
        Assert.ThrowsAny<FormatException>(() => PyFormat.Prozent("%d %d", 1L));
    }
}
