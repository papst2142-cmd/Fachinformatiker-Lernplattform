using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Core.Rechner;

/// <summary>
/// Phase 3 W4b, V2/V3 (Weisung P3-21): Ergebniszeilen und Rechenweg der Karte "Bild und Video"
/// nach der freigegebenen Wortliste (anlagen/P3-20_Wortliste_W4b.md). Zuerst rechnet der
/// unveraenderte <see cref="Bildschirm"/>-Bericht (G26) mit seinen Fehlern; nur mit Dauer kommen
/// die Video-Zeilen dazu. Die Hinweiszeile "Punkt als Tausender gelesen." steht dann am Ende.
/// Zeilen wie beim Bildschirm: Beschriftung bis Spalte 22, dann ": "; am Handy kuerzt fit_lines.
/// </summary>
public static class VideoBerichte
{
    private static string Zeile(string beschriftung, string wert) => beschriftung.PadRight(22) + ": " + wert;

    private static string Weiter(string wert) => new string(' ', 22) + ": " + wert;

    /// <summary>Byte-Werte: ganz oder bis 3 Nachkommastellen, winzig "&lt; 0,001".</summary>
    private static string Kurz(double wert) => Datenmenge.Klein3(wert) ? KleinerAls3 : Datenmenge.GanzOderKurz(wert);

    /// <summary>2 Nachkommastellen, winzig "&lt; 0,01".</summary>
    private static string Zwei(double wert) => Datenmenge.Klein(wert) ? KleinerAls2 : Zahlen.DeZahl(wert);

    private static string Weniger(string text) => text switch
    {
        KleinerAls2 => WenigerAls2,
        KleinerAls3 => WenigerAls3,
        _ => text,
    };

    /// <summary>
    /// Ganzer Bericht der Karte: Bildschirm wie bisher, mit Dauer dazu die Video-Zeilen.
    /// RechnerFehler mit dem Wortlaut von Bildschirm bzw. der Wortliste W4b.
    /// </summary>
    public static (string Text, VideoEingabe? Video) Bericht(string? breiteText, string? hoeheText, string tiefeText, string fpsText,
        string? minutenText, string? kompressionText)
    {
        var bildschirm = Bildschirm.Bericht(breiteText, hoeheText, tiefeText, fpsText);
        if (PythonZeichen.Strip(minutenText ?? "").Length == 0)
        {
            return (bildschirm, null);
        }
        var mitFps = PythonZeichen.Strip(fpsText).Length > 0;
        var minuten = Zahlen.Lesen(minutenText, VideoReadMinutes);
        var kompression = Zahlen.Lesen(kompressionText, VideoFieldCompression);
        // Bildschirm.Bericht hat Breite, Hoehe, Farbtiefe und Bildrate schon geprueft
        var breite = (long)Zahlen.LesenGanz(breiteText, SCREEN_FIELD_WIDTH);
        var hoehe = (long)Zahlen.LesenGanz(hoeheText, SCREEN_FIELD_HEIGHT);
        PythonZeichen.Ganzzahl(tiefeText, out var tiefe);
        var fps = mitFps ? Zahlen.Lesen(fpsText, "Bildrate (fps)") : 0.0;
        var e = Video.Rechnen(breite, hoehe, (int)tiefe, fps, minuten, kompression);
        switch (e.Fehler)
        {
            case VideoFehler.Minuten:
                throw new RechnerFehler(minuten <= 0 ? VideoMinutesTooSmall : VideoMinutesTooBig);
            case VideoFehler.Bildrate:
                throw new RechnerFehler(VideoNeedsFps);
            case VideoFehler.Kompression:
                throw new RechnerFehler(VideoCompressionWrong);
        }
        var video = new VideoEingabe(fps, minuten, kompression, e);

        // Hinweiszeile des Bildschirm-Berichts ans Ende (nach den Video-Zeilen)
        var hinweisAlt = Zahlen.TausenderHinweis(breiteText, hoeheText, mitFps ? fpsText : "");
        if (hinweisAlt.Count > 0)
        {
            bildschirm = bildschirm[..^("\n" + string.Join("\n", hinweisAlt)).Length];
        }
        var zeilen = new List<string> { bildschirm, "" };
        zeilen.AddRange(Zeilen(video));
        zeilen.AddRange(Zahlen.TausenderHinweis(breiteText, hoeheText, mitFps ? fpsText : "", minutenText, kompressionText));
        return (string.Join("\n", zeilen), video);
    }

    private static string MinutenText(double minuten) =>
        Datenmenge.GanzOderKurz(minuten) + " " + (minuten == 1 ? Minute : Minuten);

    /// <summary>Die angehaengten Video-Zeilen (Wortliste Abschnitt 3).</summary>
    public static List<string> Zeilen(VideoEingabe v)
    {
        var e = v.Ergebnis;
        return
        [
            VideoHeader,
            Zeile(VideoLineDuration, MinutenText(v.Minuten) + " = " + Datenmenge.GanzOderKurz(e.Sekunden) + " " + Sekunden),
            Zeile(VideoLinePerSecond, Kurz(e.ByteS) + " Byte/s"),
            Zeile(VideoLineRaw, Kurz(e.Unkomprimiert) + " Byte"),
            Weiter(Zwei(Video.InMiB(e.Unkomprimiert)) + " MiB"),
            Weiter(Zwei(Video.InGiB(e.Unkomprimiert)) + " GiB"),
            Zeile(VideoLineCompression, Datenmenge.GanzOderKurz(v.Kompression) + Fach),
            Zeile(VideoLineCompressed, Kurz(e.Komprimiert) + " Byte"),
            Weiter(Zwei(Video.InMiB(e.Komprimiert)) + " MiB"),
            Weiter(Zwei(Video.InGiB(e.Komprimiert)) + " GiB"),
            Zeile(VideoLineRate, Zwei(e.MbitKomprimiert) + " Mbit/s"),
            Weiter(Zwei(e.MiBSKomprimiert) + " MiB/s"),
        ];
    }

    /// <summary>
    /// Rechenweg: die festen Schritte 1 bis 4 aus Python (PC- oder Handy-Fassung), mit Video dazu
    /// die Schritte 5 "Video" und 6 "Kompression" mit den eingegebenen Zahlen (Wortliste Abschnitt 5).
    /// </summary>
    public static string Rechenweg(VideoEingabe? v, bool schmal)
    {
        var fest = schmal ? CALC_EXPLAIN_SCREEN_HANDY : CALC_EXPLAIN_SCREEN;
        if (v is null)
        {
            return fest;
        }
        var e = v.Ergebnis;
        var byteS = Weniger(Kurz(e.ByteS));
        var unkomp = Weniger(Kurz(e.Unkomprimiert));
        var komp = Weniger(Kurz(e.Komprimiert));
        var y = Datenmenge.GanzOderKurz(v.Kompression);
        var zeilen = new List<string> { fest, "", VideoExplainStep5 };
        zeilen.AddRange(VideoExplainWhat);
        zeilen.Add(VideoExplainPerSecond);
        zeilen.Add("   " + Datenmenge.GanzOderKurz(e.ByteBild) + " x " + Zahlen.DeKurz(v.Fps, 3) + " = " + byteS + " Byte/s");
        zeilen.Add(VideoExplainAmount);
        zeilen.Add("   " + byteS + " x " + Datenmenge.GanzOderKurz(v.Minuten) + " x 60 = " + unkomp + " Byte");
        zeilen.Add(VideoExplainStep6);
        zeilen.Add(VideoExplainCompression);
        zeilen.Add("   " + unkomp + " / " + y + " = " + komp + " Byte");
        zeilen.Add("   MiB = " + komp + " / 1024^2 = " + Weniger(Zwei(Video.InMiB(e.Komprimiert))) + " MiB");
        zeilen.Add("   Datenrate = " + byteS + " / " + y + " x 8 / 1.000.000 = " + Weniger(Zwei(e.MbitKomprimiert)) + " Mbit/s");
        return string.Join("\n", zeilen);
    }

    /// <summary>Uebernahme: Wert, Einheit und Hinweiszeile (MiB auf 3 Stellen bzw. Byte aufgerundet).</summary>
    public static (string Text, string Einheit, string Hinweis) Uebernahme(VideoEingabe v)
    {
        var (text, einheit) = Video.Uebernahme(v.Ergebnis.Komprimiert);
        return (text, einheit, einheit == "Byte" ? VideoTakenByte : VideoTakenMiB);
    }
}

/// <summary>Gelesene Video-Eingabe (Bildrate, Minuten, Kompression) mit Ergebnis.</summary>
public sealed record VideoEingabe(double Fps, double Minuten, double Kompression, VideoErgebnis Ergebnis);
