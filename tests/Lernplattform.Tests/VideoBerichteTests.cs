using System.Globalization;
using System.Text.Json;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Texte;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 3 W4b, V2/V3 (Weisung P3-21): Karte "Bild und Video" nach der freigegebenen Wortliste
/// (Fi-Austausch anlagen/P3-20_Wortliste_W4b.md): Ergebniszeilen, Fehler, Rechenweg, Zustand
/// des Uebernahme-Knopfs (Auflage a), Uebernahme Ende-zu-Ende mit beiden Hinweiszeilen und
/// Uebernahme bei Eingabefehler der Uebertragung (Auflage b), Zeilen hoechstens 28 am Handy.
/// </summary>
public partial class VideoBerichteTests
{
    [System.Text.RegularExpressions.GeneratedRegex(@"^[0-9.,]+ (Byte|Byte/s|MiB|GiB|Mbit/s|MiB/s)$")]
    private static partial System.Text.RegularExpressions.Regex ZahlMitEinheit();

    private static readonly JsonElement G29 = VergleichsHilfe.Lade("video.json");

    private static VideoEingabe Beispiel() => VideoBerichte.Bericht("1920", "1080", "24", "25", "2", "50").Video!;

    [Fact]
    public void OhneDauerGenauWieBildschirm()
    {
        foreach (var (b, h, t, f) in new[] { ("1920", "1080", "24", "30"), ("1.920", "1.080", "32", ""), ("640", "480", "8", "29,97"), ("1", "1", "16", "") })
        {
            foreach (var kompression in new[] { "1", "", "abc", "0" })
            {
                var (text, video) = VideoBerichte.Bericht(b, h, t, f, "", kompression);
                Assert.Equal(Bildschirm.Bericht(b, h, t, f), text);
                Assert.Null(video);
                (text, video) = VideoBerichte.Bericht(b, h, t, f, "   ", kompression);
                Assert.Equal(Bildschirm.Bericht(b, h, t, f), text);
                Assert.Null(video);
            }
        }
    }

    [Fact]
    public void ErgebniszeilenWieWortliste()
    {
        var (text, video) = VideoBerichte.Bericht("1920", "1080", "24", "25", "2", "50");
        Assert.NotNull(video);
        var soll = string.Join("\n",
            "VIDEO (NUR BILD, OHNE TON)",
            "Dauer                 : 2 Minuten = 120 Sekunden",
            "Byte pro Sekunde      : 155.520.000 Byte/s",
            "Unkomprimiert         : 18.662.400.000 Byte",
            "                      : 17.797,85 MiB",
            "                      : 17,38 GiB",
            "Kompression           : 50-fach",
            "Komprimiert           : 373.248.000 Byte",
            "                      : 355,96 MiB",
            "                      : 0,35 GiB",
            "Datenrate komprimiert : 24,88 Mbit/s",
            "                      : 2,97 MiB/s");
        Assert.Equal(Bildschirm.Bericht("1920", "1080", "24", "25") + "\n\n" + soll, text);
        // Einzahl "1 Minute", Minuten wie gelesen (bis 3 Nachkommastellen)
        Assert.Contains("Dauer                 : 1 Minute = 60 Sekunden", VideoBerichte.Bericht("1920", "1080", "24", "25", "1", "1").Text.Split('\n'));
        Assert.Contains("Dauer                 : 0,5 Minuten = 30 Sekunden", VideoBerichte.Bericht("1920", "1080", "24", "25", "0,5", "1").Text.Split('\n'));
        Assert.Contains("Dauer                 : 0,001 Minuten = 0,06 Sekunden", VideoBerichte.Bericht("1920", "1080", "24", "25", "0,001", "1").Text.Split('\n'));
        // Mehr als 3 Nachkommastellen liest die Leseregel nicht (Gegenpruefung): keine "0 Minuten", keine "1 Minuten"
        foreach (var m in new[] { "0,0001", "1,0004" })
        {
            Assert.StartsWith("Bitte eine Zahl eingeben: Dauer (Minuten).", Assert.Throws<RechnerFehler>(() => VideoBerichte.Bericht("1920", "1080", "24", "25", m, "1")).Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TausenderHinweisAmEnde()
    {
        var text = VideoBerichte.Bericht("1.920", "1080", "24", "25", "2", "50").Text.Split('\n');
        Assert.Equal("Punkt als Tausender gelesen.", text[^1]);
        Assert.Single(text, z => z == "Punkt als Tausender gelesen.");
        Assert.Equal("                      : 2,97 MiB/s", text[^2]);
        text = VideoBerichte.Bericht("1920", "1080", "24", "25", "1.000", "50").Text.Split('\n');
        Assert.Equal("Punkt als Tausender gelesen.", text[^1]);
        Assert.Single(text, z => z == "Punkt als Tausender gelesen.");
        Assert.DoesNotContain("Punkt als Tausender gelesen.", VideoBerichte.Bericht("1920", "1080", "24", "25", "2", "50").Text, StringComparison.Ordinal);
    }

    [Fact]
    public void FehlerInDerReihenfolgeDerWortliste()
    {
        string Fehler(string b, string h, string t, string f, string m, string y) =>
            Assert.Throws<RechnerFehler>(() => VideoBerichte.Bericht(b, h, t, f, m, y)).Message;
        // 1. zuerst die Bildschirm-Fehler, unveraendert
        Assert.Equal(Assert.Throws<RechnerFehler>(() => Bildschirm.Bericht("abc", "1080", "24", "")).Message, Fehler("abc", "1080", "24", "", "x", ""));
        // 2. Lesefehler mit den Beschriftungen; leere Kompression ist ein Fehler
        Assert.Equal("Bitte einen Wert eingeben: Kompression (y-fach).", Fehler("1920", "1080", "24", "25", "2", ""));
        Assert.Contains("Dauer (Minuten)", Fehler("1920", "1080", "24", "25", "abc", "1"), StringComparison.Ordinal);
        Assert.Contains("Kompression (y-fach)", Fehler("1920", "1080", "24", "25", "2", "abc"), StringComparison.Ordinal);
        // 3. Bereichsfehler: Dauer, dann Bildrate, dann Kompression
        Assert.Equal("Die Dauer muss größer als 0 sein.", Fehler("1920", "1080", "24", "", "0", "0"));
        Assert.Equal("Die Dauer muss größer als 0 sein.", Fehler("1920", "1080", "24", "25", "-1", "1"));
        Assert.Equal("Die Dauer ist zu groß (höchstens 10.000 Minuten).", Fehler("1920", "1080", "24", "25", "10000,001", "1"));
        Assert.Equal("Für ein Video bitte eine Bildrate größer als 0 eingeben.", Fehler("1920", "1080", "24", "", "2", "0"));
        Assert.Equal("Die Kompression muss mindestens 1 und höchstens 1.000 sein.", Fehler("1920", "1080", "24", "25", "2", "0,5"));
        Assert.Equal("Die Kompression muss mindestens 1 und höchstens 1.000 sein.", Fehler("1920", "1080", "24", "25", "2", "1000,1"));
        Assert.NotNull(VideoBerichte.Bericht("1920", "1080", "24", "25", "10.000", "1.000").Video);
    }

    [Fact]
    public void RechenwegWieWortliste()
    {
        var v = Beispiel();
        var soll = string.Join("\n",
            "SCHRITT 5: Video (mit den eingegebenen Zahlen)",
            "   Ein Video ist eine Folge von Bildern. Hier zählt nur das",
            "   Bild, ohne Ton.",
            "   Byte pro Sekunde = Byte je Bild x Bildrate",
            "   6.220.800 x 25 = 155.520.000 Byte/s",
            "   Datenmenge = Byte pro Sekunde x Minuten x 60",
            "   155.520.000 x 2 x 60 = 18.662.400.000 Byte",
            "SCHRITT 6: Kompression",
            "   Bei y-facher Kompression wird die Datenmenge durch y geteilt.",
            "   18.662.400.000 / 50 = 373.248.000 Byte",
            "   MiB = 373.248.000 / 1024^2 = 355,96 MiB",
            "   Datenrate = 155.520.000 / 50 x 8 / 1.000.000 = 24,88 Mbit/s");
        Assert.Equal(CALC_EXPLAIN_SCREEN + "\n\n" + soll, VideoBerichte.Rechenweg(v, false));
        Assert.Equal(CALC_EXPLAIN_SCREEN_HANDY + "\n\n" + soll, VideoBerichte.Rechenweg(v, true));
        Assert.Equal(CALC_EXPLAIN_SCREEN, VideoBerichte.Rechenweg(null, false));
        Assert.Equal(CALC_EXPLAIN_SCREEN_HANDY, VideoBerichte.Rechenweg(null, true));
    }

    /// <summary>Auflage a: Knopf aktiv nur mit Video-Ergebnis; gesperrt nach Feldaenderung, nach Fehler und ohne Dauer.</summary>
    [Fact]
    public void UebernahmeKnopfZustaende()
    {
        var z = new VideoKartenZustand();
        Assert.False(z.KnopfAktiv);
        z.Rechnen("1920", "1080", "24", "30", "", "1");                  // ohne Dauer
        Assert.False(z.KnopfAktiv);
        Assert.Null(z.Weg);
        z.Rechnen("1920", "1080", "24", "25", "2", "50");                // Video-Ergebnis
        Assert.True(z.KnopfAktiv);
        var weg = z.Weg;
        Assert.NotNull(weg);
        z.Geaendert("1920", "1080", "24", "25", "2", "50");             // Aenderung gemeldet, Felder wie berechnet
        Assert.True(z.KnopfAktiv);
        foreach (var (b, h, t, f, m, y) in new[]
        {
            ("1921", "1080", "24", "25", "2", "50"), ("1920", "1081", "24", "25", "2", "50"), ("1920", "1080", "32", "25", "2", "50"),
            ("1920", "1080", "24", "30", "2", "50"), ("1920", "1080", "24", "25", "3", "50"), ("1920", "1080", "24", "25", "2", "5"),
            ("1920", "1080", "24", "25", "2,0", "50"),
        })
        {
            z.Rechnen("1920", "1080", "24", "25", "2", "50");
            Assert.True(z.KnopfAktiv);
            z.Geaendert(b, h, t, f, m, y);                                // Feldaenderung (auch nur in der Schreibweise)
            Assert.False(z.KnopfAktiv);
            z.Geaendert("1920", "1080", "24", "25", "2", "50");         // zurueckgeaendert: bleibt gesperrt bis zum Rechnen
            Assert.False(z.KnopfAktiv);
        }
        Assert.Equal(2, z.Weg!.Minuten);                                  // Rechenweg bleibt bei der letzten gueltigen Berechnung
        z.Rechnen("1920", "1080", "24", "25", "2", "50");
        Assert.True(z.KnopfAktiv);
        Assert.Throws<RechnerFehler>(() => z.Rechnen("1920", "1080", "24", "", "2", "50"));   // Fehler (Dauer ohne Bildrate)
        Assert.False(z.KnopfAktiv);
        Assert.Equal(2, z.Weg!.Minuten);                                  // Rechenweg: letzte gueltige Berechnung
        Assert.Throws<RechnerFehler>(() => z.Rechnen("abc", "1080", "24", "25", "2", "50"));  // Bildschirm-Fehler
        Assert.False(z.KnopfAktiv);
        z.Rechnen("1920", "1080", "24", "25", "2", "50");
        Assert.True(z.KnopfAktiv);
        z.Rechnen("1920", "1080", "24", "25", "", "50");                 // Dauer geleert
        Assert.False(z.KnopfAktiv);
        Assert.Null(z.Weg);
    }

    /// <summary>Auflage a: Ende-zu-Ende 355,957 MiB bei 16 Mbit/s und 100 %, mit Hinweiszeile.</summary>
    [Fact]
    public void UebernahmeEndeZuEnde()
    {
        var (text, einheit, hinweis) = VideoBerichte.Uebernahme(Beispiel());
        Assert.Equal(("355,957", "MiB", "Aus Bild und Video übernommen (auf 3 Nachkommastellen gerundet)."), (text, einheit, hinweis));
        var a = UebertragungKarte.Rechnen(text, einheit, TransferDefaultRate, "Mbit/s", TransferDefaultPercent, false, hinweis);
        Assert.Equal("", a.Fehler);
        Assert.Equal(hinweis, a.Hinweis);
        var zeilen = a.Text.Split('\n');
        Assert.Contains(TransferLineDuration.PadRight(22) + ": 186,62 Sekunden", zeilen);
        Assert.Contains(TransferLineSplit.PadRight(22) + ": 0 Std. 3 Min. 6,62 Sek.", zeilen);
        Assert.DoesNotContain("Punkt als Tausender gelesen.", a.Text, StringComparison.Ordinal);
        // Normales Berechnen danach: keine Hinweiszeile
        Assert.Equal("", UebertragungKarte.Rechnen(text, einheit, TransferDefaultRate, "Mbit/s", TransferDefaultPercent, false).Hinweis);
    }

    /// <summary>Auflage a: Grenzfall unter 0,0005 MiB in ganzen Byte, aufgerundet, mit zweiter Hinweiszeile.</summary>
    [Fact]
    public void UebernahmeGrenzfallByte()
    {
        var z = new VideoKartenZustand();
        z.Rechnen("1", "1", "8", "1", "0,001", "1000");
        var (text, einheit, hinweis) = VideoBerichte.Uebernahme(z.Video!);
        Assert.Equal(("1", "Byte", "Aus Bild und Video übernommen (in Byte, aufgerundet)."), (text, einheit, hinweis));
        var a = UebertragungKarte.Rechnen(text, einheit, TransferDefaultRate, "Mbit/s", TransferDefaultPercent, false, hinweis);
        Assert.Equal(("", hinweis), (a.Fehler, a.Hinweis));
        // knapp ueber der Grenze bleibt es MiB (G29 deckt die Grenze genau ab)
        z.Rechnen("1024", "512", "8", "1", "1", "60");                    // 524.288 Byte x 60 / 60 = 0,5 MiB
        Assert.Equal(("0,5", "MiB"), (VideoBerichte.Uebernahme(z.Video!).Text, VideoBerichte.Uebernahme(z.Video!).Einheit));
    }

    /// <summary>Auflage b: Eingabefehler der Uebertragung -> nur der rote Hinweis, keine Hinweiszeile.</summary>
    [Fact]
    public void UebernahmeMitFehlerDerUebertragung()
    {
        var (text, einheit, hinweis) = VideoBerichte.Uebernahme(Beispiel());
        foreach (var (rate, prozent) in new[] { ("", "100"), ("abc", "100"), ("0", "100"), ("16", ""), ("16", "0") })
        {
            var a = UebertragungKarte.Rechnen(text, einheit, rate, "Mbit/s", prozent, false, hinweis);
            Assert.Null(a.Eingabe);
            Assert.Equal("", a.Text);
            Assert.Equal("", a.Hinweis);
            Assert.Equal(Assert.Throws<RechnerFehler>(() => DatenBerichte.UebertragungLesen(text, einheit, rate, "Mbit/s", prozent)).Message, a.Fehler);
        }
        Assert.Equal("Bitte einen Wert eingeben: Datenrate.", UebertragungKarte.Rechnen(text, einheit, "", "Mbit/s", "100", true, hinweis).Fehler);
    }

    /// <summary>Alle G29-Faelle mit Uebernahme: Uebertragung bei 16 Mbit/s und 100 % wie G29 (Sekunden und Aufteilung).</summary>
    [Fact]
    public void UebernahmeAlleG29Faelle()
    {
        var n = 0;
        foreach (var fall in G29.GetProperty("faelle").EnumerateArray())
        {
            if (!fall.TryGetProperty("uebernahme", out var u) || u.ValueKind != JsonValueKind.Object || !u.GetProperty("passt").GetBoolean())
            {
                continue;
            }
            var a = UebertragungKarte.Rechnen(u.GetProperty("text").GetString(), u.GetProperty("einheit").GetString()!, "16", "Mbit/s", "100", true, "H");
            Assert.Equal("", a.Fehler);
            var d = u.GetProperty("dauer");
            var sek = d.GetProperty("sekunden").GetString()!;
            var zeilen = a.Text.Split('\n');
            var dauer = Assert.Single(zeilen, z => z.StartsWith(TransferLineDuration + " ", StringComparison.Ordinal));
            var aufgeteilt = Assert.Single(zeilen, z => z.StartsWith(TransferLineSplit + " ", StringComparison.Ordinal));
            // winzige Dauer zeigt "< 0,01" (W4a, P3-17 3c), G29 rechnet dann "0,00"
            var klein = sek == "0,00";
            Assert.EndsWith(": " + (klein ? KleinerAls2 : sek) + " Sekunden", dauer, StringComparison.Ordinal);
            Assert.EndsWith(": " + Zahlen.DeGanz(System.Numerics.BigInteger.Parse(d.GetProperty("std").GetRawText(), CultureInfo.InvariantCulture))
                + " Std. " + d.GetProperty("min").GetRawText() + " Min. " + (klein ? KleinerAls2 : d.GetProperty("sek").GetString()) + " Sek.",
                aufgeteilt, StringComparison.Ordinal);
            Assert.Equal("H", a.Hinweis);
            n++;
        }
        Assert.Equal(323, n);
    }

    /// <summary>
    /// Weisung P3-21 Punkt 2: am Handy (fit_lines, 28 Zeichen, einheitlich) ist keine Zeile der
    /// neuen Rechenwege und Ergebniszeilen laenger als 28 Zeichen, ausser ein einzelnes unteilbares
    /// Wort; im Rechenweg steht im Zeilenkern nur Einfachleerzeichen. Alle G29-Faelle ohne Fehler.
    /// </summary>
    [Fact]
    public void ZeilenHoechstens28AmHandy()
    {
        var n = 0;
        foreach (var fall in G29.GetProperty("faelle").EnumerateArray())
        {
            if (fall.GetProperty("fehler").ValueKind != JsonValueKind.Null)
            {
                continue;
            }
            double Zahl(string name) => double.Parse(fall.GetProperty(name).GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture);
            var e = Video.Rechnen((long)Zahl("breite"), (long)Zahl("hoehe"), (int)Zahl("tiefe"), Zahl("fps"), Zahl("minuten"), Zahl("kompression"));
            var v = new VideoEingabe(Zahl("fps"), Zahl("minuten"), Zahl("kompression"), e);
            var weg = VideoBerichte.Rechenweg(v, true);
            var neu = weg[(CALC_EXPLAIN_SCREEN_HANDY.Length + 2)..];
            Assert.DoesNotContain("  ", string.Join("\n", neu.Split('\n').Select(z => z.TrimStart(' '))));
            foreach (var zeile in Zeilen.Kasten(weg, 28, einheitlich: true).Split('\n'))
            {
                Assert.True(zeile.Length <= 28 || !zeile.Trim().Contains(' ', StringComparison.Ordinal), fall.GetRawText()[..60] + ": \"" + zeile + "\"");
            }
            // Ergebniszeilen: fit_lines haelt Zahl und Einheit zusammen (wie Python, Zeilen.Wert); laenger
            // als 28 Zeichen darf nur ein solches unteilbares Stueck sein (sehr grosse Werte)
            foreach (var zeile in Zeilen.Kasten(string.Join("\n", VideoBerichte.Zeilen(v)), 28, einheitlich: true).Split('\n'))
            {
                Assert.True(zeile.Length <= 28 || ZahlMitEinheit().IsMatch(zeile.Trim()), fall.GetRawText()[..60] + ": \"" + zeile + "\"");
            }
            n++;
        }
        Assert.Equal(324, n);
        foreach (var text in new[] { VideoBerichte.Bericht("1.920", "1080", "24", "29,97", "10.000", "1").Text, VideoBerichte.Uebernahme(Beispiel()).Hinweis, VideoTakenByte })
        {
            foreach (var zeile in Zeilen.Kasten(text, 28, einheitlich: true).Split('\n'))
            {
                Assert.True(zeile.Length <= 28 || !zeile.Trim().Contains(' ', StringComparison.Ordinal), "\"" + zeile + "\"");
            }
        }
    }

    /// <summary>Verdrahtung in der Oberflaeche (Quelltext, wie RechenTrainerAblaufTests.AppVerdrahtung).</summary>
    [Fact]
    public void AppVerdrahtung()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null && !File.Exists(System.IO.Path.Combine(ordner.FullName, "global.json")))
        {
            ordner = ordner.Parent;
        }
        Assert.NotNull(ordner);
        var views = System.IO.Path.Combine(ordner.FullName, "src", "Lernplattform.App", "Views");
        var karte = File.ReadAllText(System.IO.Path.Combine(views, "VideoKarte.cs"));
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(karte, @"_uebernehmen\.IsEnabled = _zustand\.KnopfAktiv;").Count);
        Assert.Contains("foreach (var feld in new[] { _breite, _hoehe, _fps, _minuten, _kompression })", karte, StringComparison.Ordinal);
        Assert.Contains("feld.TextChanged += (_, _) => Geaendert();", karte, StringComparison.Ordinal);
        Assert.Contains("new Pillen(COLOR_DEPTHS, COLOR_DEPTHS[2].Item1, _ => Geaendert())", karte, StringComparison.Ordinal);
        Assert.Contains("RechnerTeile.Feld(\"\", VideoFieldMinutes", karte, StringComparison.Ordinal);
        Assert.Contains("RechnerTeile.Karte(VideoTitle,", karte, StringComparison.Ordinal);
        var daten = File.ReadAllText(System.IO.Path.Combine(views, "DatenKarten.cs"));
        Assert.Contains("UebertragungKarte.Rechnen(", daten, StringComparison.Ordinal);
        Assert.DoesNotContain("DatenBerichte.UebertragungLesen(_groesse", daten, StringComparison.Ordinal);
        var seite = File.ReadAllText(System.IO.Path.Combine(views, "RechnerSeite.cs"));
        Assert.Contains("Waehle(\"uebertragung\");\n            _datenKarten.Uebernehmen(text, einheit, hinweis);", seite.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("SCREEN_TITLE", seite, StringComparison.Ordinal);
    }
}
