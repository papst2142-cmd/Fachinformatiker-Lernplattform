using System.Globalization;
using System.Text.Json;
using Lernplattform.Core.Rechner;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 3 W4a, D1/D2: Datenmengen und Uebertragungsdauer. Es gibt kein Python-Vorbild; die
/// Vergleichswerte G28 (tests/Vergleich/datenmengen.json) rechnet ein unabhaengig aus Plan
/// P3-15 Abschnitt 4 geschriebenes Skript (werkzeuge/vergleich/erzeuge_w4_datenmengen.py).
/// Dazu die Pruefaufgaben 1 bis 6 von Chat (Fi-Austausch anlagen/W4_Pruefaufgaben.md).
/// </summary>
public class DatenmengeTests
{
    private static readonly JsonElement G28 = VergleichsHilfe.Lade("datenmengen.json");

    /// <summary>Python float.hex(x) fuer endliche Zahlen (13 Hexziffern, Exponent mit Vorzeichen).</summary>
    internal static string PyHex(double x)
    {
        var bits = BitConverter.DoubleToUInt64Bits(x);
        var vorzeichen = (bits >> 63) != 0 ? "-" : "";
        var exponent = (int)((bits >> 52) & 0x7FF);
        var mantisse = bits & 0xFFFFFFFFFFFFFUL;
        if (exponent == 0 && mantisse == 0)
        {
            return vorzeichen + "0x0.0p+0";
        }
        var fuehrend = exponent == 0 ? 0 : 1;
        var e = exponent == 0 ? -1022 : exponent - 1023;
        return vorzeichen + "0x" + fuehrend + "." + mantisse.ToString("x13", CultureInfo.InvariantCulture)
            + "p" + (e >= 0 ? "+" : "") + e.ToString(CultureInfo.InvariantCulture);
    }

    private static double Zahl(JsonElement fall, string name) =>
        double.Parse(fall.GetProperty(name).GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture);

    [Fact]
    public void PyHexWiePython()
    {
        Assert.Equal("0x1.0000000000000p+0", PyHex(1.0));
        Assert.Equal("0x1.2cc0000000000p+3", PyHex(9.3984375));
        Assert.Equal("0x1.999999999999ap-4", PyHex(0.1));
        Assert.Equal("0x0.0p+0", PyHex(0.0));
    }

    [Fact]
    public void DatenmengenWieG28()
    {
        var faelle = G28.GetProperty("datenmengen").EnumerateArray().ToList();
        Assert.True(faelle.Count > 300, "zu wenige Faelle: " + faelle.Count);
        foreach (var fall in faelle)
        {
            var einheit = fall.GetProperty("einheit").GetString()!;
            var name = fall.GetProperty("wert").GetString() + " " + einheit;
            var e = Datenmenge.Rechnen(Zahl(fall, "wert"), einheit);
            if (fall.GetProperty("fehler").ValueKind != JsonValueKind.Null)
            {
                Assert.True(e is null, name);
                continue;
            }
            Assert.True(e is not null, name);
            Assert.Equal(fall.GetProperty("byte").GetString(), PyHex(e.Byte));
            Assert.Equal(fall.GetProperty("bit").GetString(), PyHex(e.Bit));
            var klein = fall.GetProperty("klein").EnumerateArray().Select(k => k.GetString()).ToList();
            foreach (var ziel in Datenmenge.Dezimal.Concat(Datenmenge.Binaer))
            {
                Assert.True(fall.GetProperty("werte").GetProperty(ziel).GetString() == PyHex(e.Werte[ziel]), name + " -> " + ziel);
                Assert.Equal(klein.Contains(ziel), Datenmenge.Klein(e.Werte[ziel]));
            }
            foreach (var ziel in new[] { "Bit", "Byte" }.Concat(Datenmenge.Dezimal).Concat(Datenmenge.Binaer))
            {
                Assert.True(fall.GetProperty("text").GetProperty(ziel).GetString() == e.Text(ziel), name + " Text " + ziel + ": " + e.Text(ziel));
            }
        }
    }

    [Fact]
    public void UebertragungWieG28()
    {
        var faelle = G28.GetProperty("uebertragung").EnumerateArray().ToList();
        Assert.True(faelle.Count > 300, "zu wenige Faelle: " + faelle.Count);
        foreach (var fall in faelle)
        {
            var name = string.Join(" ", new[] { "groesse", "einheit", "datenrate", "rate_einheit", "prozent" }.Select(n => fall.GetProperty(n).GetString()));
            var e = Uebertragung.Rechnen(Zahl(fall, "groesse"), fall.GetProperty("einheit").GetString()!, Zahl(fall, "datenrate"),
                fall.GetProperty("rate_einheit").GetString()!, Zahl(fall, "prozent"));
            var fehler = fall.GetProperty("fehler");
            if (fehler.ValueKind != JsonValueKind.Null)
            {
                Assert.True(fehler.GetString() == e.Fehler.ToString().ToLowerInvariant(), name);
                continue;
            }
            Assert.True(e.Fehler == UebertragungFehler.Keiner, name);
            Assert.Equal(fall.GetProperty("bit").GetString(), PyHex(e.Bit));
            Assert.Equal(fall.GetProperty("rate").GetString(), PyHex(e.Rate));
            Assert.True(fall.GetProperty("sekunden").GetString() == PyHex(e.Sekunden), name);
            var text = fall.GetProperty("text");
            Assert.Equal(text.GetProperty("bit").GetString(), Datenmenge.GanzOderKurz(e.Bit));
            Assert.Equal(text.GetProperty("rate").GetString(), Datenmenge.GanzOderKurz(e.Rate));
            Assert.True(text.GetProperty("sekunden").GetString() == Zahlen.DeZahl(e.Sekunden), name);
            var (stunden, minuten, sekunden) = e.Aufgeteilt;
            Assert.True(text.GetProperty("std").GetRawText() == stunden.ToString(CultureInfo.InvariantCulture), name);
            Assert.True(text.GetProperty("min").GetInt32() == minuten, name);
            Assert.True(text.GetProperty("sek").GetString() == sekunden, name);
        }
    }

    // ---- Pruefaufgaben von Chat (Quelle: "Chat, eigene Aufgabe 21.10.2026", W4_Pruefaufgaben.md) ----

    [Theory]
    [InlineData(2.5, "GB", "MB", "2.500,00")] // Aufgabe 1: 2.500 MB
    [InlineData(512, "MiB", "KiB", "524.288,00")] // Aufgabe 2: 524.288 KiB
    [InlineData(750, "GB", "GiB", "698,49")] // Aufgabe 3: 698,49 GiB
    public void PruefaufgabenUmrechnen(double wert, string von, string nach, string erwartet)
    {
        Assert.Equal(erwartet, Datenmenge.Rechnen(wert, von)!.Text(nach));
    }

    [Fact]
    public void Pruefaufgabe4BitEinesGiB()
    {
        Assert.Equal("8.589.934.592", Datenmenge.Rechnen(1, "GiB")!.Text("Bit"));
    }

    [Theory]
    [InlineData(1.2, "GB", 50, 100, "192,00", 0, 3, "12,00")] // Aufgabe 5: 192 s = 3 min 12 s
    [InlineData(3, "GiB", 100, 80, "322,12", 0, 5, "22,12")] // Aufgabe 6: 322,12 s = 5 min 22,12 s
    public void PruefaufgabenDauer(double groesse, string einheit, double rate, double prozent, string sek, int std, int min, string rest)
    {
        var e = Uebertragung.Rechnen(groesse, einheit, rate, "Mbit/s", prozent);
        Assert.Equal(sek, Zahlen.DeZahl(e.Sekunden));
        var (stunden, minuten, sekunden) = e.Aufgeteilt;
        Assert.Equal(std, (int)stunden);
        Assert.Equal(min, minuten);
        Assert.Equal(rest, sekunden);
    }

    // ---- Planbeispiele P3-15 4.1/4.2 und Regeln ----

    [Fact]
    public void PlanbeispieleUndGrenzen()
    {
        Assert.Equal("0,91", Datenmenge.Rechnen(1, "TB")!.Text("TiB"));
        Assert.Equal("465,66", Datenmenge.Rechnen(500, "GB")!.Text("GiB"));
        Assert.Equal("268,44", Datenmenge.Rechnen(256, "MiB")!.Text("MB"));
        Assert.Equal("0,125", Datenmenge.Rechnen(1, "Bit")!.Text("Byte"));
        Assert.Null(Datenmenge.Rechnen(0, "GB"));
        Assert.Null(Datenmenge.Rechnen(1e12 + 0.001, "GB"));
        // 10^12 TiB: Byte ganz, als kuerzester Dezimaltext (DeGanz(double) zeigte falsche Endziffern)
        Assert.Equal("1.099.511.627.776.000.000.000.000", Datenmenge.Rechnen(1e12, "TiB")!.Text("Byte"));
        var dvd = Uebertragung.Rechnen(4.7, "GB", 16, "Mbit/s", 100);
        Assert.Equal((0, 39, "10,00"), ((int)dvd.Aufgeteilt.Stunden, dvd.Aufgeteilt.Minuten, dvd.Aufgeteilt.Sekunden));
        Assert.Equal(UebertragungFehler.Prozent, Uebertragung.Rechnen(1, "GB", 16, "Mbit/s", 100.001).Fehler);
        Assert.Equal(UebertragungFehler.Groesse, Uebertragung.Rechnen(0, "GB", 0, "Mbit/s", 0).Fehler);
    }

    [Fact]
    public void AufteilungUebertraegtNieSechzigSekunden()
    {
        // 59,995 s rundet auf 60,00 und wird zu 1 Minute 0,00 Sekunden
        var e = new UebertragungErgebnis(UebertragungFehler.Keiner, 0, 0, 59.995);
        Assert.Equal("60,00", Zahlen.DeZahl(e.Sekunden));
        Assert.Equal((0, 1, "0,00"), ((int)e.Aufgeteilt.Stunden, e.Aufgeteilt.Minuten, e.Aufgeteilt.Sekunden));
        var f = new UebertragungErgebnis(UebertragungFehler.Keiner, 0, 0, 3599.995);
        Assert.Equal((1, 0, "0,00"), ((int)f.Aufgeteilt.Stunden, f.Aufgeteilt.Minuten, f.Aufgeteilt.Sekunden));
    }
}
