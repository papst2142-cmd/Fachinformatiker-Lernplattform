using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Lernplattform.Core.Rechner;

namespace Lernplattform.Tests;

/// <summary>Sammelt Abweichungen und meldet die ersten am Ende (ein Test, viele Faelle).</summary>
internal sealed class Abweichungen
{
    private readonly List<string> liste = [];
    private int anzahl;

    public void Gleich(string? erwartet, string? ist, string fall)
    {
        anzahl++;
        if (erwartet != ist)
        {
            liste.Add($"{fall}\n  Python: {Zeigen(erwartet)}\n  C#:     {Zeigen(ist)}");
        }
    }

    public void Wahr(bool ok, string fall)
    {
        anzahl++;
        if (!ok)
        {
            liste.Add(fall);
        }
    }

    private static string Zeigen(string? text) => text is null ? "<null>" : JsonSerializer.Serialize(text);

    public void Pruefen(int mindestens = 1)
    {
        Assert.True(anzahl >= mindestens, $"nur {anzahl} Faelle geprueft");
        Assert.True(liste.Count == 0, $"{liste.Count} von {anzahl} Faellen weichen ab:\n" + string.Join("\n", liste.Take(12)));
    }

    public static string Text(JsonElement e) => e.ValueKind == JsonValueKind.Null ? "<null>" : e.GetString()!;

    public static string Liste(JsonElement e) => string.Join("\u2016", e.EnumerateArray().Select(Text));

    public static string Liste(IEnumerable<string> e) => string.Join("\u2016", e);

    /// <summary>Ergebnis eines Aufrufs als Text: Wert oder "Fehler: Meldung" (RechnerFehler).</summary>
    public static string Versuch(Func<string> aufruf)
    {
        try
        {
            return aufruf();
        }
        catch (RechnerFehler fehler)
        {
            return "Fehler: " + fehler.Message;
        }
    }

    /// <summary>{"wert": ...} oder {"fehler": ...} aus den Vergleichswerten als Text.</summary>
    public static string Erwartet(JsonElement e) =>
        e.TryGetProperty("fehler", out var f) ? "Fehler: " + f.GetString() : Wert(e.GetProperty("wert"));

    private static string Wert(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString()!,
        JsonValueKind.Number => e.GetRawText(),
        _ => e.GetRawText(),
    };
}

/// <summary>
/// Phase 3, R1: Zahlen lesen/schreiben, Textregeln und Umbruch genau wie Python v0.63.2
/// (Vergleichswerte G24, werkzeuge/vergleich/erzeuge_rechner_zahlen.py). Kontrollfehler A1:
/// double.Parse mit Laendereinstellung, Math.Round statt kaufmaennisch ueber den Dezimaltext,
/// "01.000" angenommen, string.Length statt Codepunkte, char.IsDigit statt isdigit.
/// </summary>
public class RechnerZahlenTests
{
    private static readonly JsonElement G24 = VergleichsHilfe.Lade("rechner_zahlen.json");

    private static string Bits(double wert) => BitConverter.DoubleToInt64Bits(wert).ToString(CultureInfo.InvariantCulture);

    [Fact]
    public void ZeichenWiePython()
    {
        var a = new Abweichungen();
        var z = G24.GetProperty("zeichen");
        var ziffern = new HashSet<int>();
        foreach (var b in z.GetProperty("isdigit").EnumerateArray())
        {
            for (var c = b[0].GetInt32(); c <= b[1].GetInt32(); c++)
            {
                ziffern.Add(c);
            }
        }
        var dezimal = z.GetProperty("isdecimal").EnumerateArray().ToDictionary(e => e[0].GetInt32(), e => e[1].GetInt32());
        var leer = z.GetProperty("isspace").EnumerateArray().Select(e => e.GetInt32()).ToHashSet();
        var ende = z.GetProperty("zeilenende").EnumerateArray().Select(e => e.GetInt32()).ToHashSet();
        var falt = z.GetProperty("casefold").EnumerateArray().ToDictionary(e => e[0].GetInt32(), e => e[1].GetString()!);
        Assert.Equal(1530, falt.Count);
        for (var c = 0; c < 0x110000; c++)
        {
            if (c is >= 0xD800 and < 0xE000)
            {
                continue;
            }
            var hex = $"U+{c:X4}";
            a.Wahr(PythonZeichen.IstZiffer(c) == ziffern.Contains(c), "isdigit " + hex);
            a.Wahr(PythonZeichen.Dezimalwert(c) == (dezimal.TryGetValue(c, out var d) ? d : -1), "isdecimal " + hex);
            a.Wahr(PythonZeichen.IstLeer(c) == leer.Contains(c), "isspace " + hex);
            a.Wahr(PythonZeichen.IstZeilenende(c) == ende.Contains(c), "splitlines " + hex);
            var text = char.ConvertFromUtf32(c);
            a.Gleich(falt.TryGetValue(c, out var f) ? f : text, PythonZeichen.Falten(text), "casefold " + hex);
        }
        a.Pruefen(1_000_000);
    }

    [Fact]
    public void TexteWiePython()
    {
        var a = new Abweichungen();
        foreach (var fall in G24.GetProperty("texte").EnumerateArray())
        {
            var text = fall.GetProperty("text").GetString()!;
            var name = JsonSerializer.Serialize(text);
            a.Gleich(Abweichungen.Liste(fall.GetProperty("split")), Abweichungen.Liste(PythonZeichen.Split(text)), "split " + name);
            a.Gleich(fall.GetProperty("strip").GetString(), PythonZeichen.Strip(text), "strip " + name);
            a.Gleich(Abweichungen.Liste(fall.GetProperty("splitlines")), Abweichungen.Liste(PythonZeichen.SplitLines(text)), "splitlines " + name);
            a.Gleich(fall.GetProperty("casefold").GetString(), PythonZeichen.Falten(text), "casefold " + name);
            a.Wahr(fall.GetProperty("len").GetInt32() == PythonZeichen.Laenge(text), "len " + name);
            a.Wahr(fall.GetProperty("isdigit").GetBoolean() == PythonZeichen.IstZiffernText(text), "isdigit " + name);
            var ganz = PythonZeichen.Ganzzahl(text, out var wert) ? wert.ToString(CultureInfo.InvariantCulture) : null;
            a.Gleich(fall.GetProperty("int").ValueKind == JsonValueKind.Null ? null : fall.GetProperty("int").GetString(), ganz, "int " + name);
        }
        a.Pruefen(30);
    }

    [Fact]
    public void LesenWiePython()
    {
        var a = new Abweichungen();
        foreach (var fall in G24.GetProperty("lesen").EnumerateArray())
        {
            var e = fall.GetProperty("text");
            var text = e.ValueKind == JsonValueKind.Null ? null : e.GetString();
            var name = JsonSerializer.Serialize(text);
            a.Gleich(Abweichungen.Erwartet(fall.GetProperty("komma")), Abweichungen.Versuch(() => Bits(Zahlen.Lesen(text, "Feld"))), "read_number " + name);
            a.Gleich(Abweichungen.Erwartet(fall.GetProperty("ganz")),
                Abweichungen.Versuch(() => Zahlen.LesenGanz(text, "Feld").ToString(CultureInfo.InvariantCulture)), "read_number ganz " + name);
            a.Gleich(Abweichungen.Liste(fall.GetProperty("hinweis")), Abweichungen.Liste(Zahlen.TausenderHinweis(text)), "Hinweis " + name);
        }
        foreach (var fall in G24.GetProperty("hinweise").EnumerateArray())
        {
            var texte = fall[0].EnumerateArray().Select(t => t.ValueKind == JsonValueKind.Null ? null : t.GetString()).ToArray();
            a.Gleich(Abweichungen.Liste(fall[1]), Abweichungen.Liste(Zahlen.TausenderHinweis(texte)), "Hinweis mehrere");
        }
        a.Pruefen(2000);
    }

    [Fact]
    public void ZahltextWiePython()
    {
        var a = new Abweichungen();
        foreach (var fall in G24.GetProperty("zahltext").EnumerateArray())
        {
            var wert = BitConverter.Int64BitsToDouble(fall.GetProperty("bits").GetInt64());
            var name = wert.ToString("R", CultureInfo.InvariantCulture);
            for (var n = 0; n < 5; n++)
            {
                a.Gleich(fall.GetProperty("de_number")[n].GetString(), Zahlen.DeZahl(wert, n), $"de_number({name}, {n})");
                a.Gleich(fall.GetProperty("de_short")[n].GetString(), Zahlen.DeKurz(wert, n), $"_de_short({name}, {n})");
            }
            var ganz = fall.GetProperty("de_int");
            if (ganz.ValueKind != JsonValueKind.Null)
            {
                a.Gleich(ganz.GetString(), Zahlen.DeGanz(wert), $"de_int({name})");
            }
        }
        foreach (var fall in G24.GetProperty("ganzzahlen").EnumerateArray())
        {
            var wert = BigInteger.Parse(fall.GetProperty("wert").GetString()!, CultureInfo.InvariantCulture);
            a.Gleich(fall.GetProperty("de_int").GetString(), Zahlen.DeGanz(wert), $"de_int({wert})");
            a.Gleich(fall.GetProperty("plural").GetString(), Zahlen.Mehrzahl(wert, "Strang", "Stränge"), $"plural({wert})");
        }
        a.Pruefen(1000);
    }

    [Fact]
    public void KastenWiePython()
    {
        var a = new Abweichungen();
        foreach (var fall in G24.GetProperty("kasten").EnumerateArray())
        {
            var text = fall.GetProperty("text").GetString()!;
            foreach (var breite in fall.GetProperty("breiten").EnumerateObject())
            {
                var zeichen = int.Parse(breite.Name, CultureInfo.InvariantCulture);
                for (var i = 0; i < 2; i++)
                {
                    var erwartet = breite.Value[i];
                    var soll = erwartet.ValueKind == JsonValueKind.String ? erwartet.GetString() : "Fehler: ValueError";
                    string ist;
                    try
                    {
                        ist = Zeilen.Kasten(text, zeichen, einheitlich: i == 1);
                    }
                    catch (FormatException)
                    {
                        ist = "Fehler: ValueError";
                    }
                    a.Gleich(soll, ist, $"fit_lines({JsonSerializer.Serialize(text[..Math.Min(text.Length, 50)])}, {zeichen}, {i == 1})");
                }
            }
        }
        a.Pruefen(4000);
    }

    [Fact]
    public void HelferWiePython()
    {
        var a = new Abweichungen();
        foreach (var fall in G24.GetProperty("fit").EnumerateArray())
        {
            var zeile = fall.GetProperty("zeile").GetString()!;
            foreach (var breite in new[] { 28, 75 })
            {
                a.Gleich(Abweichungen.Liste(fall.GetProperty(breite.ToString(CultureInfo.InvariantCulture))),
                    Abweichungen.Liste(Zeilen.Einpassen([zeile], breite)), $"_fit_lines({JsonSerializer.Serialize(zeile)}, {breite})");
            }
        }
        foreach (var f in G24.GetProperty("absatz").EnumerateArray())
        {
            a.Gleich(Abweichungen.Liste(f[3]), Abweichungen.Liste(Zeilen.Absatz(f[0].GetString()!, f[1].GetString()!, f[2].GetInt32())), "_wrap_para " + f[0]);
        }
        foreach (var f in G24.GetProperty("aufzaehlen").EnumerateArray())
        {
            var items = f[1].EnumerateArray().Select(e => e.GetString()!).ToList();
            a.Gleich(Abweichungen.Liste(f[5]), Abweichungen.Liste(Zeilen.Aufzaehlen(f[0].GetString()!, items, f[2].GetString()!, f[3].GetString()!, f[4].GetInt32())),
                "_join_items " + f[0]);
        }
        foreach (var f in G24.GetProperty("tabellen").EnumerateArray())
        {
            var koepfe = f[0].EnumerateArray().Select(e => e.GetString()!).ToList();
            var zeilen = f[1].EnumerateArray().Select(z => (IReadOnlyList<string>)[.. z.EnumerateArray().Select(e => e.GetString()!)]).ToList();
            var breiten = f[2].EnumerateArray().Select(e => e.GetInt32()).ToList();
            a.Gleich(Abweichungen.Liste(f[3]), Abweichungen.Liste(Zeilen.PcTabelle(koepfe, zeilen, breiten)), "_pc_table " + koepfe[0]);
        }
        foreach (var f in G24.GetProperty("adressen").EnumerateArray())
        {
            a.Gleich(Abweichungen.Liste(f[2]), Abweichungen.Liste(Zeilen.HandyAdresse(f[0].GetString()!, f[1].GetString()!)), $"_handy_address {f[0]} {f[1]}");
        }
        a.Pruefen(1000);
    }
}
