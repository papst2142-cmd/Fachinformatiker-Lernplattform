using System.Globalization;
using System.Text.Json;
using Lernplattform.Core.Rechner;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 3 W4c, W1/W2: Geld (decimal, Cent je Schritt) und die sechs WiSo-Rechner. Es gibt kein
/// Python-Vorbild; die Vergleichswerte G30 (tests/Vergleich/wiso.json) rechnet ein unabhaengig aus
/// Plan P3-15 Abschnitt 4.4 und einer Praezisierung geschriebenes Skript
/// (werkzeuge/vergleich/erzeuge_w4_wiso.py). Dazu die Pruefaufgaben 8 bis 14 von Chat
/// (Fi-Austausch anlagen/W4_Pruefaufgaben.md) und die Leseregel fuer Geld (Auflage P3-16 4a).
/// </summary>
public class WisoTests
{
    private static readonly JsonElement G30 = VergleichsHilfe.Lade("wiso.json");

    /// <summary>Eingabe aus G30 ("1020.50") so, wie sie getippt wird ("1020,50"), gelesen mit Geld.Lesen.</summary>
    private static decimal E(JsonElement eingabe, string name) =>
        Geld.Lesen(eingabe.GetProperty(name).GetString()!.Replace('.', ','), name);

    private static string G(decimal wert) => wert.ToString("F2", CultureInfo.InvariantCulture);

    private static (WisoFehler? Fehler, Dictionary<string, object>? Werte) Rechne(string rechner, JsonElement e) => rechner switch
    {
        "bezug" => Werte(Wiso.Bezug(E(e, "lep"), E(e, "rabatt"), E(e, "skonto"), E(e, "bezugskosten")), w => new()
        {
            ["rabatt"] = G(w.Rabatt), ["zep"] = G(w.Zep), ["skonto"] = G(w.Skonto), ["bep"] = G(w.Bep), ["bezugspreis"] = G(w.Bezugspreis),
        }),
        "verkauf" => Werte(Wiso.Verkauf(E(e, "bp"), E(e, "hk"), E(e, "gewinn"), E(e, "kskonto"), E(e, "krabatt"), E(e, "ust")), w => new()
        {
            ["hk"] = G(w.Hk), ["sk"] = G(w.Sk), ["gewinn"] = G(w.Gewinn), ["bvp"] = G(w.Bvp), ["kskonto"] = G(w.Kskonto),
            ["zvp"] = G(w.Zvp), ["krabatt"] = G(w.Krabatt), ["lvp"] = G(w.Lvp), ["ust"] = G(w.Ust), ["brutto"] = G(w.Brutto),
        }),
        "deckung" => Werte(Wiso.Deckung(E(e, "preis"), E(e, "vk"), E(e, "fix"), E(e, "menge")), w => new()
        {
            ["db"] = G(w.Db), ["db_gesamt"] = G(w.DbGesamt), ["ergebnis"] = G(w.Ergebnis), ["gs_dezimal"] = G(w.GsDezimal),
            ["gewinnschwelle"] = w.Gewinnschwelle.ToString(CultureInfo.InvariantCulture),
        }),
        "afa" => Werte(Wiso.Abschreibung(E(e, "ak"), E(e, "nd"), E(e, "monat")), w => new()
        {
            ["jaehrlich"] = G(w.Jaehrlich),
            ["jahre"] = string.Join(" ", w.Jahre.Select(j => j.Jahr + "/" + j.Monate + "/" + G(j.Betrag) + "/" + G(j.Rest))),
        }),
        "zinsen" => Werte(Wiso.Zinsen(E(e, "kapital"), E(e, "zinssatz"), E(e, "tage")), w => new()
        {
            ["zinsen"] = G(w.Zinsen), ["endkapital"] = G(w.Endkapital),
        }),
        "amort" => Werte(Wiso.Amortisation(E(e, "investition"), E(e, "einsparung"), E(e, "laufend")), w => new()
        {
            ["rueckfluss"] = G(w.Rueckfluss), ["jahre"] = G(w.Jahre),
        }),
        _ => throw new ArgumentException(rechner),
    };

    private static (WisoFehler?, Dictionary<string, object>?) Werte<T>(WisoErgebnis<T> e, Func<T, Dictionary<string, object>> werte)
        where T : class => (e.Fehler, e.Werte is null ? null : werte(e.Werte));

    private static string Code(WisoFehlerArt art) => art switch
    {
        WisoFehlerArt.ZuKlein => "zu_klein",
        WisoFehlerArt.ZuGross => "zu_gross",
        WisoFehlerArt.Nachkommastellen => "nachkommastellen",
        WisoFehlerArt.NichtGanz => "nicht_ganz",
        WisoFehlerArt.KeinDeckungsbeitrag => "kein_db",
        WisoFehlerArt.Nie => "nie",
        _ => throw new ArgumentOutOfRangeException(nameof(art)),
    };

    /// <summary>G30-Wert als Vergleichstext; AfA-Jahre als "jahr/monate/betrag/rest".</summary>
    private static string Soll(JsonElement wert) => wert.ValueKind switch
    {
        JsonValueKind.Array => string.Join(" ", wert.EnumerateArray().Select(j =>
            j.GetProperty("jahr").GetInt32() + "/" + j.GetProperty("monate").GetInt32() + "/"
            + j.GetProperty("betrag").GetString() + "/" + j.GetProperty("rest").GetString())),
        JsonValueKind.Number => wert.GetRawText(),
        _ => wert.GetString()!,
    };

    [Fact]
    public void WisoWieG30()
    {
        var faelle = G30.GetProperty("faelle").EnumerateArray().ToList();
        Assert.True(faelle.Count >= 300, "zu wenige Faelle: " + faelle.Count);
        var abweichungen = new List<string>();
        foreach (var fall in faelle)
        {
            var rechner = fall.GetProperty("rechner").GetString()!;
            var eingabe = fall.GetProperty("eingabe");
            var name = rechner + " " + string.Join(" ", eingabe.EnumerateObject().Select(p => p.Name + "=" + p.Value.GetString()));
            var (fehler, werte) = Rechne(rechner, eingabe);
            var sollFehler = fall.GetProperty("fehler");
            if (sollFehler.ValueKind != JsonValueKind.Null)
            {
                var soll = (sollFehler.GetProperty("feld").ValueKind == JsonValueKind.Null ? "-" : sollFehler.GetProperty("feld").GetString())
                    + ":" + sollFehler.GetProperty("art").GetString();
                var ist = fehler is null ? "kein Fehler" : (fehler.Feld ?? "-") + ":" + Code(fehler.Art);
                if (soll != ist)
                {
                    abweichungen.Add(name + ": Fehler soll " + soll + ", ist " + ist);
                }
                continue;
            }
            if (fehler is not null)
            {
                abweichungen.Add(name + ": unerwarteter Fehler " + fehler);
                continue;
            }
            var sollWerte = fall.GetProperty("werte");
            Assert.Equal(sollWerte.EnumerateObject().Select(p => p.Name), werte!.Keys);
            foreach (var p in sollWerte.EnumerateObject())
            {
                if (Soll(p.Value) != (string)werte[p.Name])
                {
                    abweichungen.Add(name + ": " + p.Name + " soll " + Soll(p.Value) + ", ist " + werte[p.Name]);
                }
            }
        }
        Assert.True(abweichungen.Count == 0, abweichungen.Count + " Abweichungen:\n" + string.Join("\n", abweichungen.Take(20)));
    }

    [Fact]
    public void G30HatAlleRechnerUndFehlerarten()
    {
        var faelle = G30.GetProperty("faelle").EnumerateArray().ToList();
        foreach (var rechner in new[] { "bezug", "verkauf", "deckung", "afa", "zinsen", "amort" })
        {
            Assert.True(faelle.Count(f => f.GetProperty("rechner").GetString() == rechner && f.GetProperty("fehler").ValueKind == JsonValueKind.Null) >= 50, rechner);
        }
        var arten = faelle.Where(f => f.GetProperty("fehler").ValueKind != JsonValueKind.Null)
            .Select(f => f.GetProperty("fehler").GetProperty("art").GetString()).ToHashSet();
        Assert.Equal(new HashSet<string?> { "zu_klein", "zu_gross", "nachkommastellen", "nicht_ganz", "kein_db", "nie" }, arten);
    }

    // ---- Pruefaufgaben von Chat (Quelle: "Chat, eigene Aufgabe 21.10.2026", W4_Pruefaufgaben.md) ----

    [Fact]
    public void Pruefaufgabe8Bezug()
    {
        var w = Wiso.Bezug(840.00m, 25, 3, 18.00m).Werte!;
        Assert.Equal((210.00m, 630.00m, 18.90m, 611.10m, 629.10m), (w.Rabatt, w.Zep, w.Skonto, w.Bep, w.Bezugspreis));
    }

    [Fact]
    public void Pruefaufgabe9Verkauf()
    {
        var w = Wiso.Verkauf(629.10m, 25, 12, 2, 5, 19).Werte!;
        Assert.Equal([157.28m, 786.38m, 94.37m, 880.75m, 17.97m, 898.72m, 47.30m, 946.02m, 179.74m, 1125.76m],
            new[] { w.Hk, w.Sk, w.Gewinn, w.Bvp, w.Kskonto, w.Zvp, w.Krabatt, w.Lvp, w.Ust, w.Brutto });
    }

    [Fact]
    public void Pruefaufgabe10Deckung()
    {
        var w = Wiso.Deckung(80.00m, 52.00m, 20000.00m, 900).Werte!;
        Assert.Equal((28.00m, 25200.00m, 5200.00m, 714.29m, 715L), (w.Db, w.DbGesamt, w.Ergebnis, w.GsDezimal, w.Gewinnschwelle));
    }

    [Fact]
    public void Pruefaufgabe11Abschreibung()
    {
        var w = Wiso.Abschreibung(4500.00m, 5, 10).Werte!;
        Assert.Equal(900.00m, w.Jaehrlich);
        Assert.Equal([225.00m, 900.00m, 900.00m, 900.00m, 900.00m, 675.00m], w.Jahre.Select(j => j.Betrag));
        Assert.Equal([3, 12, 12, 12, 12, 9], w.Jahre.Select(j => j.Monate));
        Assert.Equal(2475.00m, w.Jahre[2].Rest);
        Assert.Equal(0m, w.Jahre[^1].Rest);
        Assert.Equal(4500.00m, w.Jahre.Sum(j => j.Betrag));
    }

    [Fact]
    public void Pruefaufgabe12und13Zinsen()
    {
        Assert.Equal(36.00m, Wiso.Zinsen(8000.00m, 3.6m, 45).Werte!.Zinsen);
        Assert.Equal(83.33m, Wiso.Zinsen(12500.00m, 2.4m, 100).Werte!.Zinsen);
    }

    [Fact]
    public void Pruefaufgabe14Amortisation()
    {
        // erster Fall; der zweite (3 Jahre 8 Monate) folgt nach der Entscheidung zur Monatsregel (P3-16 4d)
        var w = Wiso.Amortisation(18000.00m, 7500.00m, 1500.00m).Werte!;
        Assert.Equal((6000.00m, 3.00m), (w.Rueckfluss, w.Jahre));
    }

    // ---- Geld: Lesen, Runden, Schreibweise ----

    [Theory]
    [InlineData("0,1", "0.1")]
    [InlineData("0,07", "0.07")]
    [InlineData("19,99", "19.99")]
    [InlineData("1.024,60", "1024.60")]
    [InlineData("1.000", "1000")]
    [InlineData("2.5", "2.5")]
    [InlineData("-0", "0")]
    [InlineData("1234567890123456,789", "1234567890123456.789")]
    public void GeldLiestDirektAlsDecimal(string text, string soll)
    {
        // der letzte Fall hat 19 Ziffern: ueber double waere er nicht mehr genau (Kontrollfehler "Eingabe ueber double")
        var wert = Geld.Lesen(text, "Betrag");
        Assert.Equal(soll, wert.ToString(CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("", "Bitte einen Wert eingeben: Rabatt (%).")]
    [InlineData("abc", "Bitte eine Zahl eingeben: Rabatt (%). Zum Beispiel 1.000 oder 1,5.")]
    [InlineData("1,2345", "Bitte eine Zahl eingeben: Rabatt (%). Zum Beispiel 1.000 oder 1,5.")]
    public void GeldLesefehlerWieLeseregel(string text, string meldung)
    {
        Assert.Equal(meldung, Assert.Throws<RechnerFehler>(() => Geld.Lesen(text, "Rabatt (%)")).Message);
        Assert.Equal(meldung, Assert.Throws<RechnerFehler>(() => Zahlen.Lesen(text, "Rabatt (%)")).Message);
    }

    [Theory]
    [InlineData("10.205", "10.21")]
    [InlineData("10.215", "10.22")]
    [InlineData("157.265", "157.27")]
    [InlineData("-0.005", "-0.01")]
    [InlineData("0.004", "0.00")]
    public void RundenKaufmaennisch(string wert, string soll)
    {
        // 10,205 und 157,265: "zur geraden Zahl" (Math.Round Standard) gaebe 10,20 bzw. 157,26
        Assert.Equal(soll, G(Geld.Runden(decimal.Parse(wert, CultureInfo.InvariantCulture))));
    }

    [Theory]
    [InlineData("1024.6", 2, "1.024,60")]
    [InlineData("-4000", 2, "-4.000,00")]
    [InlineData("0", 2, "0,00")]
    [InlineData("1000000000", 2, "1.000.000.000,00")]
    [InlineData("714.285714", 2, "714,29")]
    [InlineData("0.995", 2, "1,00")]
    [InlineData("-0.001", 2, "0,00")]
    public void GeldText(string wert, int stellen, string soll) =>
        Assert.Equal(soll, Geld.Text(decimal.Parse(wert, CultureInfo.InvariantCulture), stellen));

    [Theory]
    [InlineData("2.5", "2,5")]
    [InlineData("99.999", "99,999")]
    [InlineData("15", "15")]
    [InlineData("1000", "1.000")]
    public void GeldKurz(string wert, string soll) => Assert.Equal(soll, Geld.Kurz(decimal.Parse(wert, CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("40000", "100", 400L)]
    [InlineData("20000", "28", 715L)]
    [InlineData("0", "3", 0L)]
    [InlineData("1000000000", "0.01", 100000000000L)]
    [InlineData("100.01", "100", 2L)]
    public void AufrundenExakt(string zaehler, string nenner, long soll) =>
        Assert.Equal(soll, Geld.Aufrunden(decimal.Parse(zaehler, CultureInfo.InvariantCulture), decimal.Parse(nenner, CultureInfo.InvariantCulture)));

    [Fact]
    public void NachkommastellenNachDemWert()
    {
        Assert.False(Geld.MehrAlsCent(19.990m));
        Assert.True(Geld.MehrAlsCent(19.995m));
        Assert.True(Geld.IstGanz(12.000m));
        Assert.False(Geld.IstGanz(2.5m));
        Assert.Equal(new WisoFehler("lep", WisoFehlerArt.Nachkommastellen), Wiso.Bezug(19.995m, 0, 0, 0).Fehler);
        Assert.Null(Wiso.Bezug(19.990m, 0, 0, 0).Fehler);
    }
}
