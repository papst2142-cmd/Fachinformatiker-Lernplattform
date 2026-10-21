using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 3 W4a, D3/D5/D6 (Weisung P3-17), W4b V4 (Weisung P3-21): Trainer-Arten "einheiten",
/// "dauer" und "video" gegen die
/// Vergleichswerte G31 (tests/Vergleich/rechentrainer_w4.json aus dem unabhaengigen Skript
/// werkzeuge/vergleich/erzeuge_w4_rechentrainer.py), Pruefer (genau, nur Sekunden in
/// "dauer - Schwer" +-0,01), Rundungsgrenzfaelle (Auflage 4c), Eingabefehler ohne Speichern und
/// genau eine Zeile je gueltigem "Prüfen" (Auflage 4d), Ergebnis und Rechenweg der beiden
/// Karten nach der Wortliste (mit den Aenderungen P3-17 Punkt 3) und sehr lange Dauer (4a).
/// </summary>
public class DatenTrainerTests
{
    private static readonly JsonElement G31 = VergleichsHilfe.Lade("rechentrainer_w4.json");

    // ---------------------------------------------------------------- G31

    [Fact]
    public void AufgabenWieG31()
    {
        var aufgaben = G31.GetProperty("aufgaben").EnumerateArray().ToList();
        // W4a: einheiten, dauer; W4b (Weisung P3-21): video; W4c (Weisung P3-23): sechs WiSo-Arten;
        // je 3 Stufen x 40 Startwerte
        Assert.Equal(1080, aufgaben.Count);
        Assert.Equal(["einheiten", "dauer", "video", "bezug", "verkauf", "deckung", "afa", "zinsen", "amort"],
            aufgaben.Select(a => a.GetProperty("art").GetString()).Distinct());
        Assert.All(aufgaben.GroupBy(a => a.GetProperty("art").GetString()), g => Assert.Equal(120, g.Count()));
        foreach (var soll in aufgaben)
        {
            var art = soll.GetProperty("art").GetString()!;
            var stufe = soll.GetProperty("stufe").GetInt32();
            var startwert = soll.GetProperty("startwert").GetString()!;
            var name = $"{art}-{stufe}-{startwert}";
            var ist = RechenTrainer.Aufgabe(art, stufe, startwert);
            Assert.True(soll.GetProperty("text").GetString() == ist.Text, name + ": " + ist.Text);
            var felder = soll.GetProperty("felder").EnumerateArray().Select(f => (f[0].GetString()!, f[1].GetString()!)).ToList();
            Assert.Equal(felder, ist.Felder.ToList());
            var loesung = soll.GetProperty("loesung").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
            Assert.True(loesung.Count == ist.Loesung.Count, name);
            foreach (var (schluessel, wert) in loesung)
            {
                Assert.True(wert == ist.Loesung[schluessel], name + " " + schluessel + ": " + ist.Loesung[schluessel]);
            }
            var schritte = soll.GetProperty("schritte").EnumerateArray().Select(z => z.GetString()!).ToList();
            Assert.True(schritte.SequenceEqual(ist.Schritte), name + ": " + string.Join(" | ", ist.Schritte));
            Assert.True(ist.Zahlenfelder, name);
            Assert.Equal(ist.Schritte, ist.HandySchritte);
        }
    }

    [Fact]
    public void NamenUndBereiche()
    {
        Assert.Equal("Datenmengen", RechenTrainer.ArtName("einheiten"));
        Assert.Equal("Übertragungsdauer", RechenTrainer.ArtName("dauer"));
        Assert.Equal("Bild und Video", RechenTrainer.ArtName("video"));
        Assert.Equal("Bezugskalkulation", RechenTrainer.ArtName("bezug"));
        Assert.Equal("Verkaufskalkulation", RechenTrainer.ArtName("verkauf"));
        Assert.Equal("Deckungsbeitrag", RechenTrainer.ArtName("deckung"));
        Assert.Equal("Abschreibung", RechenTrainer.ArtName("afa"));
        Assert.Equal("Zinsen", RechenTrainer.ArtName("zinsen"));
        Assert.Equal("Amortisation", RechenTrainer.ArtName("amort"));
        Assert.Equal(["netzwerk", "daten", "wiso"], RechenTrainer.Bereiche.Select(b => b.Wert));
        Assert.Equal(["Netzwerk", "Daten", "WiSo"], RechenTrainer.Bereiche.Select(b => b.Name));
        Assert.Equal(RechenTrainer.Arten, RechenTrainer.Bereiche[0].Arten);
        Assert.Equal(["einheiten", "dauer", "video"], RechenTrainer.Bereiche[1].Arten.Select(a => a.Art));
        Assert.Equal(["bezug", "verkauf", "deckung", "afa", "zinsen", "amort"], RechenTrainer.Bereiche[2].Arten.Select(a => a.Art));
        // TRAINER_KINDS (G27) bleibt bei den sechs Netzwerk-Arten
        Assert.Equal(6, RechenTrainer.Arten.Count);
        Assert.Equal(15, RechenTrainer.AlleArten.Count);
    }

    // ---------------------------------------------------------------- Pruefer (F5a)

    private static Dictionary<string, string> Loesung(TrainerAufgabe a) => a.Loesung.ToDictionary(p => p.Key, p => p.Value);

    [Fact]
    public void LoesungIstRichtigUndDanebenFalsch()
    {
        // W4c: auch die sechs WiSo-Arten (genau, Geld.Wert)
        foreach (var art in new[] { "einheiten", "dauer", "video", "bezug", "verkauf", "deckung", "afa", "zinsen", "amort" })
        {
            for (var stufe = 1; stufe <= 3; stufe++)
            {
                foreach (var a in RechenTrainer.RundeBauen(art, stufe, "77", 20))
                {
                    Assert.True(a.AllesRichtig(Loesung(a)), a.Text);
                    Assert.Null(a.EingabeFehler(Loesung(a)));
                    foreach (var (schluessel, _) in a.Felder)
                    {
                        var falsch = Loesung(a);
                        var soll = decimal.Parse(falsch[schluessel].Replace(".", "").Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
                        // 0,02 daneben ist immer falsch, auch bei den Sekunden mit +-0,01
                        falsch[schluessel] = (soll + 0.02m).ToString(System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
                        Assert.False(a.AllesRichtig(falsch), a.Text + " " + schluessel + " " + falsch[schluessel]);
                    }
                }
            }
        }
    }

    [Fact]
    public void GenauBisAufDieSekundenInSchwer()
    {
        // Ganze Zahlen genau; Tausenderpunkt und Komma nach der Leseregel
        var genau = DatenTrainer.GenauPruefer("2.000");
        Assert.True(genau("2000"));
        Assert.True(genau("2.000"));
        Assert.True(genau("2000,0"));
        Assert.False(genau("2000,001"));
        Assert.False(genau("1999"));
        // Schwer "einheiten": genau auf 2 Stellen
        var zwei = DatenTrainer.GenauPruefer("465,66");
        Assert.True(zwei("465,66"));
        Assert.True(zwei("465,660"));
        Assert.False(zwei("465,67"));
        Assert.False(zwei("465,7"));
        Assert.False(zwei("465,665"));
        // Sekunden in "dauer - Schwer": +-0,01
        var nahe = DatenTrainer.NahePruefer("22,12");
        Assert.True(nahe("22,12"));
        Assert.True(nahe("22,11"));
        Assert.True(nahe("22,13"));
        Assert.True(nahe("22,115"));
        Assert.False(nahe("22,10"));
        Assert.False(nahe("22,14"));
        Assert.False(nahe("22,131"));
        // Groesste lesbare Zahl (20 Zeichen) ist einfach falsch, kein Absturz (Gegenpruefung W4a);
        // laengere Eingaben lehnt die Leseregel ab
        var riesig = new string('9', 20);
        Assert.False(genau(riesig));
        Assert.False(nahe(riesig));
        Assert.False(genau("1" + new string('0', 400)));
        // In den uebrigen Feldern von "dauer - Schwer" gilt genau
        var a = RechenTrainer.Aufgabe("dauer", 3, "0-0");
        Assert.Equal(("4", "26", "7,17"), (a.Loesung["stunden"], a.Loesung["minuten"], a.Loesung["sekunden"]));
        Assert.True(a.AllesRichtig(new Dictionary<string, string> { ["stunden"] = "4", ["minuten"] = "26", ["sekunden"] = "7,18" }));
        Assert.False(a.AllesRichtig(new Dictionary<string, string> { ["stunden"] = "4", ["minuten"] = "26,01", ["sekunden"] = "7,17" }));
        Assert.False(a.AllesRichtig(new Dictionary<string, string> { ["stunden"] = "4", ["minuten"] = "26", ["sekunden"] = "7,19" }));
    }

    // ---------------------------------------------------------------- Rundungsgrenzen (Auflage 4c)

    [Fact]
    public void GrenzpruefungenRechnenWieDecimal()
    {
        Assert.True(DatenTrainer.DritteStelleFuenf(1.235));
        Assert.True(DatenTrainer.DritteStelleFuenf(1.2351));
        Assert.True(DatenTrainer.DritteStelleFuenf(0.005));
        Assert.False(DatenTrainer.DritteStelleFuenf(1.2349));
        Assert.False(DatenTrainer.DritteStelleFuenf(5));
        Assert.False(DatenTrainer.DritteStelleFuenf(1e-7));
        Assert.True(DatenTrainer.OhneMinutenUeberlauf(59.994));
        Assert.False(DatenTrainer.OhneMinutenUeberlauf(59.995));
        Assert.False(DatenTrainer.OhneMinutenUeberlauf(119.999));
        Assert.True(DatenTrainer.OhneMinutenUeberlauf(120));
        Assert.True(DatenTrainer.OhneMinutenUeberlauf(179.9949));
    }

    private static double Zahl(string text) =>
        double.Parse(text.Replace(".", "").Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void KeineAufgabeAnEinerRundungsgrenze()
    {
        // 400 Aufgaben je Schwer-Art: x bzw. t aus den Rechenschritten nachgerechnet
        for (var n = 0; n < 400; n++)
        {
            var einheiten = RechenTrainer.Aufgabe("einheiten", 3, "grenze-" + n);
            var letzte = einheiten.Schritte[^1];                     // "Byte -> binär: B / 1024^3 = L GiB"
            var bytes = Zahl(letzte[(letzte.IndexOf(": ", StringComparison.Ordinal) + 2)..letzte.IndexOf(" / ", StringComparison.Ordinal)]);
            var nach = letzte[(letzte.LastIndexOf(' ') + 1)..];
            var x = bytes / Datenmenge.Faktor(nach);
            Assert.True(x >= 0.01 && !DatenTrainer.DritteStelleFuenf(x), einheiten.Text + " " + x.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(Zahlen.DeZahl(x), einheiten.Loesung["ergebnis"]);

            var dauer = RechenTrainer.Aufgabe("dauer", 3, "grenze-" + n);
            var zeile = dauer.Schritte[2];                           // "Dauer: B / R = T Sekunden"
            var teile = zeile["Dauer: ".Length..zeile.IndexOf(" = ", StringComparison.Ordinal)].Split(" / ");
            var t = Zahl(teile[0]) / Zahl(teile[1]);
            Assert.True(t >= 60 && t < 360000, dauer.Text);
            Assert.True(!DatenTrainer.DritteStelleFuenf(t) && DatenTrainer.OhneMinutenUeberlauf(t), dauer.Text + " " + t.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            Assert.True(Zahl(dauer.Loesung["sekunden"]) < 60 && int.Parse(dauer.Loesung["minuten"], System.Globalization.CultureInfo.InvariantCulture) < 60, dauer.Text);
        }
    }

    [Fact]
    public void VideoKeineAufgabeAnEinerRundungsgrenze()
    {
        // Auflage P3-21 3d: 400 Aufgaben je Mittel/Schwer, Wert aus den Rechenschritten nachgerechnet
        for (var n = 0; n < 400; n++)
        {
            var mittel = RechenTrainer.Aufgabe("video", 2, "grenze-" + n);
            var zeile = mittel.Schritte[1];                          // "Datenrate: B x F x 8 / 1.000.000 = L Mbit/s"
            var faktoren = zeile["Datenrate: ".Length..zeile.IndexOf(" / ", StringComparison.Ordinal)].Split(" x ");
            var rate = (double)(long)(Zahl(faktoren[0]) * Zahl(faktoren[1]) * 8) / 1_000_000;
            Assert.False(DatenTrainer.DritteStelleFuenf(rate), mittel.Text);
            Assert.Equal(Zahlen.DeZahl(rate), mittel.Loesung["ergebnis"]);
            Assert.Equal("Ergebnis (Mbit/s)", mittel.Felder[0].Beschriftung);

            var schwer = RechenTrainer.Aufgabe("video", 3, "grenze-" + n);
            var komp = schwer.Schritte[2];                           // "Komprimiert: U / y = K Byte"
            var teile = komp["Komprimiert: ".Length..komp.IndexOf(" = ", StringComparison.Ordinal)].Split(" / ");
            var k = Zahl(teile[0]) / Zahl(teile[1]);
            var gib = k >= 1024.0 * 1024 * 1024;
            var x = gib ? k / (1024.0 * 1024 * 1024) : k / (1024 * 1024);
            Assert.False(DatenTrainer.DritteStelleFuenf(x), schwer.Text);
            Assert.Equal(Zahlen.DeZahl(x), schwer.Loesung["ergebnis"]);
            Assert.Equal(gib ? "Ergebnis (GiB)" : "Ergebnis (MiB)", schwer.Felder[0].Beschriftung);
            // Byte je Bild immer ganz
            Assert.DoesNotContain(",", schwer.Schritte[0]);
        }
    }

    [Fact]
    public void VideoPruefaufgabe7()
    {
        // Chat, eigene Aufgabe 21.10.2026 (W4_Pruefaufgaben.md Nr. 7): Rechenweg-Arithmetik der Trainer-Schritte
        // (Ganzzahl-Reihenfolge wie DatenTrainer.Video); mit Video.Rechnen gerechnet in VideoTests.Pruefaufgabe7
        Assert.Equal("2.764.800", Zahlen.DeZahl(1280 * 720 * 24 / 8, 0));
        Assert.Equal("663,55", Zahlen.DeZahl((double)(1280L * 720 * 24 / 8 * 30 * 8) / 1_000_000));
        Assert.Equal("118,65", Zahlen.DeZahl((double)(1280L * 720 * 24 / 8 * 30 * 1 * 60) / 40 / (1024 * 1024)));
    }

    // ---------------------------------------------------------------- Speichern (Auflage 4d)

    private sealed class Aufbau : IDisposable
    {
        private readonly TempOrdner _ordner = new();
        public readonly string Pfad;
        public readonly RechenTrainerAblauf Ablauf;
        public int Anstoesse;

        public Aufbau(string art, int stufe)
        {
            Pfad = Path.Combine(_ordner.Pfad, Lernstand.DateiName);
            Ablauf = new RechenTrainerAblauf(new LernstandSchreiber(new Lernstand(Pfad, _ => { })), () => 4711);
            Ablauf.NachSpeichern += () => Anstoesse++;
            Ablauf.SetzeArt(art);
            Ablauf.SetzeStufe(stufe);
        }

        public List<string> Zeilen()
        {
            if (!File.Exists(Pfad))
            {
                return [];
            }
            using var db = VergleichsHilfe.Oeffnen(Pfad);
            return VergleichsHilfe.Abfrage(db, "SELECT art, stufe, correct FROM trainer_aufgaben ORDER BY id")
                .Select(z => $"{z[0]}:{z[1]}:{z[2]}").ToList();
        }

        public void Dispose() => _ordner.Dispose();
    }

    [Theory]
    [InlineData("einheiten", 1)]
    [InlineData("einheiten", 3)]
    [InlineData("dauer", 2)]
    [InlineData("dauer", 3)]
    public void UngueltigeEingabeSpeichertNichts(string art, int stufe)
    {
        using var t = new Aufbau(art, stufe);
        var a = t.Ablauf;
        a.RundeStarten();
        var aufgabe = a.Aufgabe!;
        var erstes = aufgabe.Felder[0];
        foreach (var eingabe in new[] { "", "   ", "abc", "500 MB", "12 Sek.", "1,2345", "1.2.3" })
        {
            var antworten = Loesung(aufgabe);
            antworten[erstes.Schluessel] = eingabe;
            Assert.Null(a.Druecken(antworten, false));
            Assert.NotEqual("", a.Eingabefehler);
            Assert.Contains(erstes.Beschriftung, a.Eingabefehler);
            Assert.Equal(TrainerZustand.Offen, a.Zustand);
            Assert.Empty(a.Ergebnisse);
            Assert.Empty(t.Zeilen());
            Assert.Equal(0, t.Anstoesse);
        }
        // Groesste lesbare Zahl: gueltig, also eine falsche Antwort; 30 Ziffern sind ein Eingabefehler
        Assert.Null(aufgabe.EingabeFehler(new Dictionary<string, string>(Loesung(aufgabe)) { [erstes.Schluessel] = new string('9', 20) }));
        Assert.NotNull(aufgabe.EingabeFehler(new Dictionary<string, string>(Loesung(aufgabe)) { [erstes.Schluessel] = "1" + new string('0', 30) }));
        // Fehlendes Feld zaehlt wie leer
        Assert.Null(a.Druecken(new Dictionary<string, string>(), false));
        Assert.Equal("Bitte einen Wert eingeben: " + erstes.Beschriftung + ".", a.Eingabefehler);
        Assert.Empty(t.Zeilen());
        // Danach gueltig, aber falsch: genau eine Zeile, Hinweis weg
        var falsch = aufgabe.Felder.ToDictionary(f => f.Schluessel, _ => "0");
        var pruefung = a.Druecken(falsch, false);
        Assert.NotNull(pruefung);
        Assert.False(pruefung.AllesRichtig);
        Assert.Equal("", a.Eingabefehler);
        Assert.Equal([$"{art}:{stufe}:0"], t.Zeilen());
        Assert.Equal(1, t.Anstoesse);
        // Zweiter Druck = weiter, schreibt nie
        Assert.Null(a.Druecken(falsch, false));
        Assert.Single(t.Zeilen());
    }

    [Theory]
    [InlineData("dauer", 3, "99")]
    [InlineData("bezug", 1, "-1")]
    [InlineData("verkauf", 3, "-1")]
    [InlineData("deckung", 3, "-1")]
    [InlineData("afa", 2, "-1")]
    [InlineData("zinsen", 3, "-1")]
    [InlineData("amort", 3, "-1")]
    public void JeGueltigemPruefenGenauEineZeile(string art, int stufe, string daneben)
    {
        // W4c: auch die WiSo-Arten; "daneben" ist nie die Loesung (WiSo-Loesungen sind nie negativ)
        using var t = new Aufbau(art, stufe);
        var a = t.Ablauf;
        a.RundeStarten();
        var erwartet = new List<string>();
        for (var i = 0; i < RechenTrainer.Runde; i++)
        {
            var aufgabe = a.Aufgabe!;
            if (i % 3 == 0)
            {
                Assert.Null(a.Druecken(new Dictionary<string, string> { [aufgabe.Felder[0].Schluessel] = "abc" }, false));
            }
            var richtig = i % 2 == 0;
            var pruefung = a.Druecken(richtig ? Loesung(aufgabe) : aufgabe.Felder.ToDictionary(f => f.Schluessel, _ => daneben), i % 2 == 1)!;
            Assert.Equal(richtig, pruefung.AllesRichtig);
            erwartet.Add($"{art}:{stufe}:{(richtig ? 1 : 0)}");
            Assert.Equal(erwartet, t.Zeilen());
            a.Druecken(Loesung(aufgabe), false);
        }
        Assert.Equal(TrainerZustand.Beendet, a.Zustand);
        Assert.Equal(RechenTrainer.Runde, t.Zeilen().Count);
        Assert.Equal(RechenTrainer.Runde, t.Anstoesse);
    }

    [Fact]
    public void NetzwerkArtenSpeichernLeerWieBisher()
    {
        // R4/R7 bleibt: bei den sechs Netzwerk-Arten ist leer eine falsche Antwort (eine Zeile)
        using var t = new Aufbau("ipv4", 1);
        t.Ablauf.RundeStarten();
        Assert.False(t.Ablauf.Aufgabe!.Zahlenfelder);
        Assert.NotNull(t.Ablauf.Druecken(new Dictionary<string, string>(), false));
        Assert.Equal(["ipv4:1:0"], t.Zeilen());
    }

    // ---------------------------------------------------------------- Karten: Ergebnis und Rechenweg

    [Fact]
    public void DatenmengenWieWortliste()
    {
        var d = DatenBerichte.DatenmengeLesen("4,7", "GB");
        Assert.Equal(string.Join("\n",
            "Eingabe               : 4,7 GB",
            "Byte                  : 4.700.000.000 Byte",
            "Bit                   : 37.600.000.000 Bit",
            "",
            "Dezimal (Faktor 1000)          Binär (Faktor 1024)",
            "kB  : 4.700.000,00             KiB : 4.589.843,75",
            "MB  : 4.700,00                 MiB : 4.482,27",
            "GB  : 4,70                     GiB : 4,38",
            "TB  : < 0,01                   TiB : < 0,01"), DatenBerichte.DatenmengeBericht(d, false));
        Assert.Equal(string.Join("\n",
            "Eingabe : 4,7 GB",
            "Byte : 4.700.000.000 Byte",
            "Bit : 37.600.000.000 Bit",
            "",
            "Dezimal (Faktor 1000)",
            "kB  : 4.700.000,00",
            "MB  : 4.700,00",
            "GB  : 4,70",
            "TB  : < 0,01",
            "",
            "Binär (Faktor 1024)",
            "KiB : 4.589.843,75",
            "MiB : 4.482,27",
            "GiB : 4,38",
            "TiB : < 0,01"), DatenBerichte.DatenmengeBericht(d, true));
        var weg = DatenBerichte.DatenmengeRechenweg(d).Split('\n');
        Assert.Equal("RECHENWEG DATENMENGEN", weg[0]);
        Assert.Contains("   Byte = 4,7 x 1000^3 = 4.700.000.000 Byte", weg);
        Assert.Contains("   Bit = 4.700.000.000 x 8 = 37.600.000.000 Bit", weg);
        Assert.Contains("   kB = 4.700.000.000 / 1000^1 = 4.700.000,00 kB", weg);
        Assert.Contains("   TiB = 4.700.000.000 / 1024^4 = weniger als 0,01 TiB", weg);
        Assert.DoesNotContain(weg, z => z.Contains("= <", StringComparison.Ordinal));
        Assert.Equal("   deshalb ist die Zahl in KiB kleiner als in kB.", weg[^1]);
    }

    [Fact]
    public void KleinRegelNachF2a()
    {
        // Kontrollfehler "immer < bei x < 0,01": 0,005 rundet auf 0,01 und steht als Zahl
        Assert.False(Datenmenge.Klein(0.005));
        Assert.True(Datenmenge.Klein(0.004999));
        Assert.Equal("0,01", Datenmenge.Rechnen(5, "MB")!.Anzeige("GB"));
        Assert.Equal("< 0,01", Datenmenge.Rechnen(4, "MB")!.Anzeige("GB"));
        // Bit/Byte: "< 0,001" statt "0"
        var bit = Datenmenge.Rechnen(0.001, "Bit")!;
        Assert.Equal("0", bit.Text("Byte"));
        Assert.Equal("< 0,001", bit.Anzeige("Byte"));
        Assert.Equal("0,001", bit.Anzeige("Bit"));
        var weg = DatenBerichte.DatenmengeRechenweg(DatenBerichte.DatenmengeLesen("0,001", "Bit")).Split('\n');
        Assert.Contains("   Byte = 0,001 / 8 = weniger als 0,001 Byte", weg);
        Assert.Contains("   Bit = Eingabe", weg);
        Assert.Equal("0,125", Datenmenge.Rechnen(1, "Bit")!.Anzeige("Byte"));
    }

    [Fact]
    public void DatenmengenFehlerUndTausenderpunkt()
    {
        Assert.Equal("Bitte einen Wert eingeben: Datenmenge.", Assert.Throws<RechnerFehler>(() => DatenBerichte.DatenmengeLesen("", "GB")).Message);
        Assert.StartsWith("Bitte eine Zahl eingeben: Datenmenge.", Assert.Throws<RechnerFehler>(() => DatenBerichte.DatenmengeLesen("4,7 GB", "GB")).Message);
        Assert.Equal(RechnerTexte.DataTooSmall, Assert.Throws<RechnerFehler>(() => DatenBerichte.DatenmengeLesen("0", "GB")).Message);
        Assert.Equal(RechnerTexte.DataTooSmall, Assert.Throws<RechnerFehler>(() => DatenBerichte.DatenmengeLesen("-1", "GB")).Message);
        Assert.Equal(RechnerTexte.DataTooBig, Assert.Throws<RechnerFehler>(() => DatenBerichte.DatenmengeLesen("1.000.000.000.001", "GB")).Message);
        var gross = DatenBerichte.DatenmengeBericht(DatenBerichte.DatenmengeLesen("1.000.000.000.000", "TiB"), false).Split('\n');
        Assert.Equal("Punkt als Tausender gelesen.", gross[^1]);
        // Bei 10^12 TiB passen zwei Spalten nicht in 75 Zeichen: untereinander
        Assert.All(gross, z => Assert.True(z.Length <= DatenBerichte.PcBreite, z));
        Assert.Contains("Binär (Faktor 1024)", gross);
    }

    [Fact]
    public void UebertragungWieWortliste()
    {
        var u = DatenBerichte.UebertragungLesen("4,7", "GB", "16", "Mbit/s", "100");
        Assert.Equal(string.Join("\n",
            "Dateigröße            : 4,7 GB = 4.700.000.000 Byte",
            "Datenmenge            : 37.600.000.000 Bit",
            "Datenrate             : 16 Mbit/s",
            "Nutzbare Leistung     : 100 %",
            "Wirksame Datenrate    : 16.000.000 bit/s",
            "Dauer                 : 2.350,00 Sekunden",
            "Aufgeteilt            : 0 Std. 39 Min. 10,00 Sek."), DatenBerichte.UebertragungBericht(u, false));
        Assert.Equal(string.Join("\n",
            "RECHENWEG ÜBERTRAGUNGSDAUER",
            "SCHRITT 1: Dateigröße in Bit umrechnen",
            "   Datenraten zählen Bit pro Sekunde, deshalb wird die",
            "   Dateigröße zuerst in Bit umgerechnet (1 Byte = 8 Bit).",
            "   Byte = 4,7 x 1000^3 = 4.700.000.000 Byte",
            "   Bit = 4.700.000.000 x 8 = 37.600.000.000 Bit",
            "SCHRITT 2: Wirksame Datenrate ermitteln",
            "   Datenraten sind immer dezimal.",
            "   1 Mbit/s = 1.000.000 bit/s.",
            "   Wirksame Datenrate = Datenrate x nutzbare Leistung / 100",
            "   16 x 1.000.000 x 100 / 100 = 16.000.000 bit/s",
            "SCHRITT 3: Dauer berechnen",
            "   Dauer = Bit / wirksame Datenrate",
            "   37.600.000.000 / 16.000.000 = 2.350,00 Sekunden",
            "SCHRITT 4: In Stunden, Minuten und Sekunden aufteilen",
            "   1 Std. = 3600 Sek., 1 Min. = 60 Sek.",
            "   2.350,00 Sek. = 0 Std. 39 Min. 10,00 Sek."), DatenBerichte.UebertragungRechenweg(u));
    }

    [Fact]
    public void UebertragungKleinUndFehler()
    {
        // 1 Byte bei 1.000.000 Gbit/s: t > 0, gerundet 0,00 (P3-17 3c)
        var u = DatenBerichte.UebertragungLesen("1", "Byte", "1.000.000", "Gbit/s", "100");
        var bericht = DatenBerichte.UebertragungBericht(u, false).Split('\n');
        Assert.Contains("Dauer                 : < 0,01 Sekunden", bericht);
        Assert.Contains("Aufgeteilt            : 0 Std. 0 Min. < 0,01 Sek.", bericht);
        Assert.Contains("Dateigröße            : 1 Byte", bericht);
        var weg = DatenBerichte.UebertragungRechenweg(u).Split('\n');
        Assert.Contains("   8 / 1.000.000.000.000.000 = weniger als 0,01 Sekunden", weg);
        Assert.Contains("   weniger als 0,01 Sek. = 0 Std. 0 Min. weniger als 0,01 Sek.", weg);
        // Wirksame Rate, deren Text "0" waere: "< 0,001 bit/s"
        var r = DatenBerichte.UebertragungLesen("1", "Byte", "0,001", "kbit/s", "0,001");
        Assert.Contains("Wirksame Datenrate    : < 0,001 bit/s", DatenBerichte.UebertragungBericht(r, false).Split('\n'));
        Assert.Equal(RechnerTexte.TransferSizeTooSmall, Assert.Throws<RechnerFehler>(() => DatenBerichte.UebertragungLesen("0", "GB", "16", "Mbit/s", "100")).Message);
        Assert.Equal(RechnerTexte.TransferSizeTooBig, Assert.Throws<RechnerFehler>(() => DatenBerichte.UebertragungLesen("1.000.000.000.001", "GB", "16", "Mbit/s", "100")).Message);
        Assert.Equal(RechnerTexte.TransferRateTooSmall, Assert.Throws<RechnerFehler>(() => DatenBerichte.UebertragungLesen("1", "GB", "0", "Mbit/s", "100")).Message);
        Assert.Equal(RechnerTexte.TransferRateTooBig, Assert.Throws<RechnerFehler>(() => DatenBerichte.UebertragungLesen("1", "GB", "1.000.000,001", "Mbit/s", "100")).Message);
        Assert.Equal(RechnerTexte.TransferPercentWrong, Assert.Throws<RechnerFehler>(() => DatenBerichte.UebertragungLesen("1", "GB", "16", "Mbit/s", "0")).Message);
        Assert.Equal(RechnerTexte.TransferPercentWrong, Assert.Throws<RechnerFehler>(() => DatenBerichte.UebertragungLesen("1", "GB", "16", "Mbit/s", "100,001")).Message);
        Assert.Equal("Bitte einen Wert eingeben: Nutzbare Leistung (%).", Assert.Throws<RechnerFehler>(() => DatenBerichte.UebertragungLesen("1", "GB", "16", "Mbit/s", "")).Message);
    }

    /// <summary>
    /// Weisung P3-19: Am Handy (fit_lines, 28 Zeichen, einheitlich) ist keine Rechenweg-Zeile laenger
    /// als 28 Zeichen, ausser ein einzelnes unteilbares Wort. fit_lines bricht Zeilen mit
    /// Doppelleerzeichen im Kern nicht um (Spalte), deshalb steht dort nur Einfachleerzeichen.
    /// </summary>
    [Fact]
    public void RechenwegeUmbrechenAufHandybreite()
    {
        var wege = new List<(string Name, string Text)>();
        foreach (var (wert, einheit) in new[] { ("4,7", "GB"), ("1", "Bit"), ("0,001", "Bit"), ("1.000.000.000.000", "TiB"), ("5", "Byte") })
        {
            wege.Add(($"Datenmengen {wert} {einheit}", DatenBerichte.DatenmengeRechenweg(DatenBerichte.DatenmengeLesen(wert, einheit))));
        }
        foreach (var (g, e, r, re, p) in new[]
        {
            ("4,7", "GB", "16", "Mbit/s", "100"), ("1", "Byte", "16", "Mbit/s", "100"), ("4.700.000.000", "Byte", "16", "Mbit/s", "100"),
            ("1.000.000.000.000", "TiB", "0,001", "kbit/s", "0,1"), ("1", "Byte", "1.000.000", "Gbit/s", "100"),
            ("1", "Byte", "0,001", "kbit/s", "0,001"),
        })
        {
            wege.Add(($"Uebertragung {g} {e} {r} {re} {p}", DatenBerichte.UebertragungRechenweg(DatenBerichte.UebertragungLesen(g, e, r, re, p))));
        }
        foreach (var soll in G31.GetProperty("aufgaben").EnumerateArray())
        {
            var a = RechenTrainer.Aufgabe(soll.GetProperty("art").GetString()!, soll.GetProperty("stufe").GetInt32(), soll.GetProperty("startwert").GetString()!);
            wege.Add(($"Trainer {a.Art}-{a.Stufe}-{soll.GetProperty("startwert").GetString()}", string.Join("\n", a.HandySchritte)));
        }
        foreach (var (name, text) in wege)
        {
            Assert.DoesNotContain("  ", string.Join("\n", text.Split('\n').Select(z => z.TrimStart(' '))));
            foreach (var zeile in Zeilen.Kasten(text, 28, einheitlich: true).Split('\n'))
            {
                Assert.True(zeile.Length <= 28 || !zeile.Trim().Contains(' ', StringComparison.Ordinal), name + ": \"" + zeile + "\"");
            }
        }
    }

    [Fact]
    public void SehrLangeDauerExaktAufgeteilt()
    {
        // Auflage 4a: 10^12 TiB mit 0,001 kbit/s bei 0,1 % = rund 8,8 * 10^27 s; Aufteilung exakt als Ganzzahl
        var u = DatenBerichte.UebertragungLesen("1.000.000.000.000", "TiB", "0,001", "kbit/s", "0,1");
        var (std, min, sek) = u.Ergebnis.Aufgeteilt;
        Assert.Equal(System.Numerics.BigInteger.Parse("2443359172835555555555555"), std);
        Assert.Equal((33, "20,00"), (min, sek));
        var bericht = DatenBerichte.UebertragungBericht(u, false).Split('\n');
        Assert.Contains("Dauer                 : 8.796.093.022.208.000.000.000.000.000,00 Sekunden", bericht);
        Assert.Contains("Aufgeteilt            : 2.443.359.172.835.555.555.555.555 Std. 33 Min. 20,00 Sek.", bericht);
        Assert.Equal(std * 360000 + min * 6000 + 2000, u.Ergebnis.Hundertstel);
    }
}
