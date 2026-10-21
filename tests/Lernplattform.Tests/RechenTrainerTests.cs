using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Lernplattform.Core.Rechner;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 3, R4: Rechen-Trainer genau wie Python v0.63.2 (Vergleichswerte G27,
/// werkzeuge/vergleich/erzeuge_rechentrainer.py): 6 Arten x 3 Stufen x feste Startwerte
/// (IPv4 auch schmal) mit Text, Feldern, Loesung, Rechenweg PC und Handy und der Pruefung von
/// vier Antwortsaetzen, Runden mit ganzzahligem Startwert, Zusammenfassung, _clean und je
/// Pruefer eine Fallliste. Kontrollfehler: Startwert als Zahl statt Text, Pruefer ohne
/// PythonKlein, Adressvergleich als Text statt als Adresse.
/// </summary>
public class RechenTrainerTests
{
    private static readonly JsonElement G27 = VergleichsHilfe.Lade("rechentrainer.json");

    private static string Paare(IEnumerable<(string, string)> paare) =>
        string.Join("‖", paare.Select(p => p.Item1 + "→" + p.Item2));

    private static string Paare(JsonElement e) =>
        string.Join("‖", e.EnumerateArray().Select(p => p[0].GetString() + "→" + p[1].GetString()));

    private static string Pruefung(IEnumerable<(string Schluessel, bool Richtig)> p) =>
        string.Join("‖", p.Select(x => x.Schluessel + "→" + (x.Richtig ? "ja" : "nein")));

    private static string Pruefung(JsonElement e) =>
        string.Join("‖", e.EnumerateArray().Select(p => p[0].GetString() + "→" + (p[1].GetBoolean() ? "ja" : "nein")));

    private static Dictionary<string, string> Antworten(JsonElement e) =>
        e.EnumerateArray().ToDictionary(p => p[0].GetString()!, p => p[1].GetString()!);

    [Fact]
    public void KonstantenWiePython()
    {
        var k = G27.GetProperty("konstanten");
        Assert.Equal(Paare(k.GetProperty("TRAINER_KINDS")), Paare(RechenTrainer.Arten));
        Assert.Equal(string.Join("‖", k.GetProperty("TRAINER_LEVELS").EnumerateArray().Select(s => s[0].GetInt32() + "→" + s[1].GetString())),
            string.Join("‖", RechenTrainer.Stufen.Select(s => s.Stufe + "→" + s.Name)));
        var a = new Abweichungen();
        a.Gleich(k.GetProperty("TRAINER_ROUND").GetRawText(), RechenTrainer.Runde.ToString(CultureInfo.InvariantCulture), "TRAINER_ROUND");
        a.Gleich(k.GetProperty("TRAINER_TITLE").GetString(), RechenTrainer.Titel, "TRAINER_TITLE");
        Assert.Equal(Abweichungen.Liste(k.GetProperty("TRAINER_NAMES")), Abweichungen.Liste(RechenTrainer.Namen));
        a.Gleich(k.GetProperty("TRAINER_GAP_OLD").GetString(), RechenTrainer.LueckeAlt, "TRAINER_GAP_OLD");
        a.Gleich(k.GetProperty("TRAINER_GAP_NEW").GetString(), RechenTrainer.LueckeNeu, "TRAINER_GAP_NEW");
        a.Pruefen(4);
        Assert.Equal("IPv6-Schreibweise", RechenTrainer.ArtName("ipv6"));
        Assert.Equal("Schwer", RechenTrainer.StufeName(3));
    }

    [Fact]
    public void AufgabenWiePython()
    {
        var a = new Abweichungen();
        var arten = new HashSet<string>();
        foreach (var f in G27.GetProperty("aufgaben").EnumerateArray())
        {
            string art = f.GetProperty("art").GetString()!, startwert = f.GetProperty("startwert").GetString()!;
            int stufe = f.GetProperty("stufe").GetInt32();
            bool schmal = f.GetProperty("schmal").GetBoolean();
            var fall = $"trainer_task({art}, {stufe}, {JsonSerializer.Serialize(startwert)}, schmal={schmal})";
            var t = RechenTrainer.Aufgabe(art, stufe, startwert, schmal);
            arten.Add(art);
            a.Gleich(art, t.Art, fall + " kind");
            a.Gleich(stufe.ToString(CultureInfo.InvariantCulture), t.Stufe.ToString(CultureInfo.InvariantCulture), fall + " level");
            a.Gleich(f.GetProperty("text").GetString(), t.Text, fall + " text");
            a.Gleich(Paare(f.GetProperty("felder")), Paare(t.Felder), fall + " fields");
            a.Gleich(Paare(f.GetProperty("loesung")), Paare(t.Felder.Select(x => (x.Schluessel, t.Loesung[x.Schluessel]))), fall + " solution");
            a.Gleich(Abweichungen.Liste(f.GetProperty("schritte")), Abweichungen.Liste(t.Schritte), fall + " steps");
            var schmalSoll = f.GetProperty("schritte_schmal");
            a.Gleich(schmalSoll.ValueKind == JsonValueKind.Null ? "<null>" : Abweichungen.Liste(schmalSoll),
                t.SchritteSchmal is null ? "<null>" : Abweichungen.Liste(t.SchritteSchmal), fall + " steps_schmal");
            a.Gleich(schmalSoll.ValueKind == JsonValueKind.Null ? Abweichungen.Liste(f.GetProperty("schritte")) : Abweichungen.Liste(schmalSoll),
                Abweichungen.Liste(t.HandySchritte), fall + " Handy-Rechenweg");
            var saetze = f.GetProperty("antworten").EnumerateArray().ToList();
            var pruefungen = f.GetProperty("pruefung").EnumerateArray().ToList();
            var alle = f.GetProperty("alle").EnumerateArray().ToList();
            for (var i = 0; i < saetze.Count; i++)
            {
                var antworten = Antworten(saetze[i]);
                a.Gleich(Pruefung(pruefungen[i]), Pruefung(t.Pruefen(antworten)), $"{fall} check(Satz {i})");
                a.Gleich(alle[i].GetBoolean() ? "ja" : "nein", t.AllesRichtig(antworten) ? "ja" : "nein", $"{fall} all_correct(Satz {i})");
            }
        }
        Assert.Equal(6, arten.Count);
        a.Pruefen(10000);
    }

    [Fact]
    public void RundenWiePython()
    {
        var a = new Abweichungen();
        foreach (var f in G27.GetProperty("runden").EnumerateArray())
        {
            string art = f.GetProperty("art").GetString()!;
            int stufe = f.GetProperty("stufe").GetInt32(), anzahl = f.GetProperty("anzahl").GetInt32();
            long startwert = f.GetProperty("startwert").GetInt64();
            bool schmal = f.GetProperty("schmal").GetBoolean();
            var fall = $"trainer_round({art}, {stufe}, {startwert}, {anzahl}, schmal={schmal})";
            var runde = RechenTrainer.RundeBauen(art, stufe, startwert, anzahl, schmal);
            var soll = f.GetProperty("aufgaben").EnumerateArray().ToList();
            a.Gleich(soll.Count.ToString(CultureInfo.InvariantCulture), runde.Count.ToString(CultureInfo.InvariantCulture), fall + " Anzahl");
            for (var i = 0; i < Math.Min(soll.Count, runde.Count); i++)
            {
                var t = runde[i];
                a.Gleich(soll[i][0].GetString(), t.Text, $"{fall}[{i}] text");
                a.Gleich(Paare(soll[i][1]), Paare(t.Felder.Select(x => (x.Schluessel, t.Loesung[x.Schluessel]))), $"{fall}[{i}] solution");
                a.Gleich(soll[i][2].GetInt32().ToString(CultureInfo.InvariantCulture), t.Schritte.Count.ToString(CultureInfo.InvariantCulture), $"{fall}[{i}] len(steps)");
            }
        }
        a.Pruefen(1000);
    }

    [Fact]
    public void ZusammenfassungWiePython()
    {
        var a = new Abweichungen();
        foreach (var f in G27.GetProperty("summen").EnumerateArray())
        {
            var werte = f[0].EnumerateArray().Select(w => w.GetBoolean()).ToList();
            a.Gleich(f[1].GetString(), RechenTrainer.Zusammenfassung(werte), $"trainer_summary({werte.Count} Werte)");
        }
        a.Pruefen(8);
    }

    [Fact]
    public void BereinigenWiePython()
    {
        var a = new Abweichungen();
        foreach (var f in G27.GetProperty("bereinigen").EnumerateArray())
        {
            a.Gleich(f[1].GetString(), RechenTrainer.Bereinigen(f[0].GetString()), $"_clean({JsonSerializer.Serialize(f[0].GetString())})");
        }
        a.Pruefen(15);
    }

    [Fact]
    public void IntMitBasisWiePython()
    {
        var a = new Abweichungen();
        foreach (var f in G27.GetProperty("int_basis").EnumerateArray())
        {
            var text = f[0].GetString()!;
            var basis = f[1].GetInt32();
            var ist = RechenTrainer.PythonInt(text, basis, out var wert) ? wert.ToString(CultureInfo.InvariantCulture) : null;
            a.Gleich(f[2].ValueKind == JsonValueKind.Null ? null : f[2].GetString(), ist, $"int({JsonSerializer.Serialize(text)}, {basis})");
        }
        a.Pruefen(150);
    }

    private static Func<string, bool> Pruefer(string art, string erwartet) => art switch
    {
        "ip" => RechenTrainer.IpPruefer(PythonIp.Adresse(erwartet)!),
        "int" => RechenTrainer.ZahlPruefer(BigInteger.Parse(erwartet, CultureInfo.InvariantCulture)),
        "bin" => RechenTrainer.BasisPruefer(BigInteger.Parse(erwartet, CultureInfo.InvariantCulture), 2),
        "hex" => RechenTrainer.BasisPruefer(BigInteger.Parse(erwartet, CultureInfo.InvariantCulture), 16),
        "netz" => RechenTrainer.NetzPruefer(PythonIp.Netz(erwartet)!),
        "ipv6text" => RechenTrainer.Ipv6TextPruefer(erwartet),
        "praefix" => RechenTrainer.PraefixPruefer(int.Parse(erwartet, CultureInfo.InvariantCulture)),
        "anzahl" => RechenTrainer.AnzahlPruefer(BigInteger.Parse(erwartet, CultureInfo.InvariantCulture)),
        "anzahl_potenz" => RechenTrainer.AnzahlPruefer(BigInteger.Parse(erwartet, CultureInfo.InvariantCulture), potenz: true),
        _ => throw new ArgumentOutOfRangeException(nameof(art), art, null),
    };

    [Fact]
    public void PrueferWiePython()
    {
        var a = new Abweichungen();
        var arten = new HashSet<string>();
        foreach (var p in G27.GetProperty("pruefer").EnumerateArray())
        {
            string art = p.GetProperty("art").GetString()!, erwartet = p.GetProperty("erwartet").GetString()!;
            arten.Add(art);
            var pruefer = Pruefer(art, erwartet);
            foreach (var f in p.GetProperty("faelle").EnumerateArray())
            {
                var eingabe = f[0].GetString()!;
                a.Gleich(f[1].GetBoolean() ? "richtig" : "falsch", pruefer(eingabe) ? "richtig" : "falsch",
                    $"{art}({erwartet}) mit {JsonSerializer.Serialize(eingabe)}");
            }
        }
        Assert.Equal(9, arten.Count);
        a.Pruefen(250);
    }

    /// <summary>
    /// Eingaben um Pythons Grenze von 4300 Ziffern: bis dahin wie Python; wo Python einen ValueError
    /// wirft (Pruefen bricht ab), wertet C# nach Weisung P3-12 Frage 3 "falsch" (Sammelliste 34).
    /// </summary>
    [Fact]
    public void GrosseEingabenFalschStattAbsturz()
    {
        var a = new Abweichungen();
        var absturz = 0;
        foreach (var f in G27.GetProperty("gross").EnumerateArray())
        {
            string art = f[0].GetString()!, erwartet = f[1].GetString()!, eingabe = f[3].GetString()!;
            var python = f[4].ValueKind == JsonValueKind.String ? "falsch (Python: ValueError)" : f[4].GetBoolean() ? "richtig" : "falsch";
            absturz += f[4].ValueKind == JsonValueKind.String ? 1 : 0;
            string ist;
            try
            {
                ist = Pruefer(art, erwartet)(eingabe) ? "richtig" : python.StartsWith("falsch (", StringComparison.Ordinal) ? python : "falsch";
            }
            catch (Exception fehler)
            {
                ist = "Ausnahme " + fehler.GetType().Name;
            }
            a.Gleich(python, ist, $"{art}({erwartet}) mit {f[2].GetInt32()} Zeichen");
        }
        Assert.True(absturz >= 10, "zu wenige Faelle ueber der Grenze");
        a.Pruefen(70);
    }

    /// <summary>Kein Sollwert fuer den Startwert einer neuen Runde (Python ohne festen Startwert): nur die Regel.</summary>
    [Fact]
    public void NeueRundeNachRegel()
    {
        for (var i = 0; i < 200; i++)
        {
            var startwert = RechenTrainer.NeuerStartwert();
            Assert.InRange(startwert, 0, (1L << 30) - 1);
        }
        foreach (var (art, _) in RechenTrainer.Arten)
        {
            foreach (var (stufe, _) in RechenTrainer.Stufen)
            {
                var runde = RechenTrainer.RundeBauen(art, stufe, RechenTrainer.NeuerStartwert());
                Assert.Equal(RechenTrainer.Runde, runde.Count);
                Assert.All(runde, t => Assert.Equal((art, stufe), (t.Art, t.Stufe)));
                Assert.All(runde, t => Assert.True(t.AllesRichtig(t.Loesung), $"{art} {stufe}: Loesung nicht richtig: {t.Text}"));
            }
        }
        // Gleicher Startwert = gleiche Runde; Aufgabe n entspricht trainer_task(art, stufe, "startwert-n")
        var eins = RechenTrainer.RundeBauen("teilen", 3, 4711);
        var zwei = RechenTrainer.RundeBauen("teilen", 3, 4711);
        Assert.Equal(eins.Select(t => t.Text), zwei.Select(t => t.Text));
        Assert.Equal(RechenTrainer.Aufgabe("teilen", 3, "4711-7").Text, eins[7].Text);
        Assert.Throws<ArgumentOutOfRangeException>(() => RechenTrainer.Aufgabe("raid", 1, "1"));
        Assert.Throws<ArgumentOutOfRangeException>(() => RechenTrainer.Aufgabe("ipv4", 4, "1"));
    }
}
