using System.Globalization;
using System.Text.Json;
using Lernplattform.Core.Rechner;
using static Lernplattform.Tests.DatenmengeTests;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 3 W4b, V1/V2: Bild und Video mit Uebernahme. Es gibt kein Python-Vorbild; die
/// Vergleichswerte G29 (tests/Vergleich/video.json) rechnet ein unabhaengig aus Plan P3-15
/// Abschnitt 4.3 geschriebenes Skript (werkzeuge/vergleich/erzeuge_w4_video.py). Dazu die
/// Pruefaufgabe 7 von Chat (Fi-Austausch anlagen/W4_Pruefaufgaben.md).
/// </summary>
public class VideoTests
{
    private static readonly JsonElement G29 = VergleichsHilfe.Lade("video.json");

    private static double Zahl(JsonElement fall, string name) =>
        double.Parse(fall.GetProperty(name).GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static long Ganz(JsonElement fall, string name) =>
        long.Parse(fall.GetProperty(name).GetString()!, CultureInfo.InvariantCulture);

    private static VideoErgebnis Rechne(JsonElement fall) =>
        Video.Rechnen(Ganz(fall, "breite"), Ganz(fall, "hoehe"), (int)Ganz(fall, "tiefe"), Zahl(fall, "fps"), Zahl(fall, "minuten"), Zahl(fall, "kompression"));

    private static Dictionary<string, double> Werte(VideoErgebnis e) => new()
    {
        ["byte_bild"] = e.ByteBild,
        ["byte_s"] = e.ByteS,
        ["unkomp"] = e.Unkomprimiert,
        ["komp"] = e.Komprimiert,
        ["mbit_unkomp"] = e.MbitUnkomprimiert,
        ["mbit_komp"] = e.MbitKomprimiert,
        ["mib_s_komp"] = e.MiBSKomprimiert,
        ["unkomp_mib"] = Video.InMiB(e.Unkomprimiert),
        ["unkomp_gib"] = Video.InGiB(e.Unkomprimiert),
        ["komp_mib"] = Video.InMiB(e.Komprimiert),
        ["komp_gib"] = Video.InGiB(e.Komprimiert),
        ["sekunden"] = e.Sekunden,
    };

    private static readonly string[] Ganzzahltexte = ["byte_bild", "byte_s", "sekunden", "unkomp", "komp"];

    [Fact]
    public void VideoWieG29()
    {
        var faelle = G29.GetProperty("faelle").EnumerateArray().ToList();
        Assert.True(faelle.Count > 300, "zu wenige Faelle: " + faelle.Count);
        foreach (var fall in faelle)
        {
            var name = string.Join(" ", new[] { "breite", "hoehe", "tiefe", "fps", "minuten", "kompression" }.Select(n => fall.GetProperty(n).GetString()));
            var e = Rechne(fall);
            var fehler = fall.GetProperty("fehler");
            if (fehler.ValueKind != JsonValueKind.Null)
            {
                var erwartet = fehler.GetString() switch
                {
                    "minuten" => VideoFehler.Minuten,
                    "bildrate" => VideoFehler.Bildrate,
                    "kompression" => VideoFehler.Kompression,
                    var x => throw new InvalidOperationException("unbekannter Fehler " + x),
                };
                Assert.True(e.Fehler == erwartet, name + ": " + e.Fehler);
                continue;
            }
            Assert.True(e.Fehler == VideoFehler.Keiner, name + ": " + e.Fehler);
            var werte = Werte(e);
            foreach (var (schluessel, wert) in werte)
            {
                Assert.True(fall.GetProperty("werte").GetProperty(schluessel).GetString() == PyHex(wert), name + " -> " + schluessel);
                var text = Ganzzahltexte.Contains(schluessel) ? Datenmenge.GanzOderKurz(wert) : Zahlen.DeZahl(wert);
                Assert.True(fall.GetProperty("text").GetProperty(schluessel).GetString() == text, name + " Text " + schluessel + ": " + text);
            }
            var klein = fall.GetProperty("klein").EnumerateArray().Select(k => k.GetString()).ToHashSet();
            foreach (var schluessel in werte.Keys.Except(Ganzzahltexte))
            {
                Assert.True(klein.Contains(schluessel) == Datenmenge.Klein(werte[schluessel]), name + " klein " + schluessel);
            }
            var klein3 = fall.GetProperty("klein3").EnumerateArray().Select(k => k.GetString()).ToHashSet();
            foreach (var schluessel in new[] { "byte_s", "unkomp", "komp" })
            {
                Assert.True(klein3.Contains(schluessel) == Datenmenge.Klein3(werte[schluessel]), name + " klein3 " + schluessel);
            }

            // V2: Uebernahme und die Dauer bei 16 Mbit/s, 100 %
            var soll = fall.GetProperty("uebernahme");
            var (uText, uEinheit) = Video.Uebernahme(e.Komprimiert);
            Assert.True(soll.GetProperty("text").GetString() == uText, name + " Uebernahme " + uText);
            Assert.Equal(soll.GetProperty("einheit").GetString(), uEinheit);
            var groesse = double.Parse(uText.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
            Assert.Equal(soll.GetProperty("passt").GetBoolean(), Datenmenge.ImBereich(groesse));
            var faktor = uEinheit == "Byte" ? 1.0 : 1048576.0;
            var t = groesse * faktor * 8 / (16 * 1e6 * 100 / 100);
            var u = new UebertragungErgebnis(UebertragungFehler.Keiner, 0, 0, t);
            var dauer = soll.GetProperty("dauer");
            Assert.Equal(dauer.GetProperty("sekunden").GetString(), Zahlen.DeZahl(t));
            var (std, min, sek) = u.Aufgeteilt;
            Assert.Equal(dauer.GetProperty("std").GetInt64(), (long)std);
            Assert.Equal(dauer.GetProperty("min").GetInt32(), min);
            Assert.Equal(dauer.GetProperty("sek").GetString(), sek);
        }
    }

    [Fact]
    public void UebernahmeGrenzfaelle()
    {
        // 0,0005 MiB = 524,288 Byte: darunter ganze Byte aufgerundet, sonst MiB auf 3 Stellen
        Assert.Equal(("524", "Byte"), Video.Uebernahme(524));
        Assert.Equal(("4", "Byte"), Video.Uebernahme(3.214));
        Assert.Equal(("1", "Byte"), Video.Uebernahme(0.0001));
        Assert.Equal(("0,001", "MiB"), Video.Uebernahme(525));
        Assert.Equal(("355,957", "MiB"), Video.Uebernahme(373_248_000));
        // ohne Tausenderpunkte (sonst Hinweis "Punkt als Tausender gelesen.") und ohne Endnullen
        Assert.Equal(("1024", "MiB"), Video.Uebernahme(1024.0 * 1048576));
        Assert.Equal(("3164,063", "MiB"), Video.Uebernahme(3164.0625 * 1048576));
    }

    [Fact]
    public void Pruefaufgabe7()
    {
        // Chat, eigene Aufgabe 21.10.2026 (W4_Pruefaufgaben.md Nr. 7): 1280 x 720, 24 Bit, 30 fps, 1 Minute, 40-fach
        var e = Video.Rechnen(1280, 720, 24, 30, 1, 40);
        Assert.Equal(VideoFehler.Keiner, e.Fehler);
        Assert.Equal("2.764.800", Datenmenge.GanzOderKurz(e.ByteBild));
        Assert.Equal("4.976.640.000", Datenmenge.GanzOderKurz(e.Unkomprimiert));
        Assert.Equal("124.416.000", Datenmenge.GanzOderKurz(e.Komprimiert));
        Assert.Equal("118,65", Zahlen.DeZahl(Video.InMiB(e.Komprimiert)));
        Assert.Equal("663,55", Zahlen.DeZahl(e.MbitUnkomprimiert));
    }

    [Fact]
    public void Pruefreihenfolge()
    {
        Assert.Equal(VideoFehler.Minuten, Video.Rechnen(1920, 1080, 24, 0, 0, 0).Fehler);
        Assert.Equal(VideoFehler.Minuten, Video.Rechnen(1920, 1080, 24, 25, 10000.001, 1).Fehler);
        Assert.Equal(VideoFehler.Bildrate, Video.Rechnen(1920, 1080, 24, 0, 1, 0).Fehler);
        Assert.Equal(VideoFehler.Kompression, Video.Rechnen(1920, 1080, 24, 25, 1, 0.999).Fehler);
        Assert.Equal(VideoFehler.Kompression, Video.Rechnen(1920, 1080, 24, 25, 1, 1000.001).Fehler);
        Assert.Equal(VideoFehler.Keiner, Video.Rechnen(1920, 1080, 24, 25, 10000, 1000).Fehler);
    }
}
