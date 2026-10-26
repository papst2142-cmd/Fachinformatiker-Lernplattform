using System.Globalization;
using System.Text;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Nachtrag 1 zu P7-13 (Wunsch 4): Zeitmessung je 3D-Bild (Abstand zum letzten Bild, Logik,
/// Vorbereitung, Zeichnen, Arbeit danach, Speicherbereinigung je Stufe, angelegter Speicher). Haelt die
/// letzten Bilder in einem festen Ring (je Bild nichts Neues im Speicher) und fasst je 10 Sekunden eine
/// Zeile zusammen. Der Text steht in <see cref="DateiName"/> im Datenordner und im Problembericht.
/// Nichts davon geht in den Spielstand.
/// Weisung P7-15 (Haenger): dazu die Luecke vom Ende des letzten Bildes bis zum Beginn dieses Bildes,
/// was in dieser Luecke auf dem Oberflaechen-Thread lief (Layout-Durchlaeufe, Layout der Spielseite,
/// eigene Auftraege wie die Textzeile), der Speicher nur im Bild und je Bild der Teil, dem die meiste
/// Zeit gehoert (<see cref="Teil"/>). Nur Messung, keine Grenze.
/// </summary>
public sealed class BildZeiten
{
    public const string DateiName = "spiel_bilder.log";
    public const int DateiMax = 16 * 1024;

    /// <summary>Laenger als dies zwischen zwei Bildern gilt als Pause (nichts zu zeichnen), nicht als langsames Bild.</summary>
    public const double PauseAb = 0.25;

    /// <summary>Ein Bild gilt als langsam, wenn es laenger als dies dauerte (60 Bilder/s = 16,7 ms).</summary>
    public const double LangsamAbMs = 20;

    public const double FensterSekunden = 10;
    public const int FensterMax = 40;
    public const int LangsamMax = 12;

    /// <summary>
    /// Ein Bild. <paramref name="LueckeMs"/>: vom Ende des letzten Bildes bis zum Beginn dieses;
    /// <paramref name="Layouts"/>/<paramref name="LayoutMs"/>: Layout-Durchlaeufe des Fensters und Zeit fuer
    /// das Layout der Spielseite in der Luecke; <paramref name="AuftraegeMs"/>: eigene Auftraege des
    /// Oberflaechen-Threads in der Luecke (Textzeile, Tafel, Eingaben); <paramref name="BytesBild"/>:
    /// Speicher, den das Zeichnen selbst anlegt (<paramref name="Bytes"/> zaehlt auch die Luecke).
    /// </summary>
    public readonly record struct Bild(double Zeit, double AbstandMs, double LogikMs, double VorbereitungMs, double ZeichnenMs, double DanachMs,
        int Gc0, int Gc1, int Gc2, long Bytes, bool Innen, double LueckeMs = 0, int Layouts = 0, double LayoutMs = 0, double AuftraegeMs = 0,
        long BytesBild = 0, double WegMs = 0, double EreignisMs = 0, string? System = null, double RuheMs = 0, string? Eingabe = null);

    /// <summary>
    /// Weisung P7-17: der Teil der Luecke nach der Ruhe. Stand die Bildschleife (nichts zu zeichnen), gehoert die
    /// Zeit bis zum ersten Anlass (Beruehrung, Anforderung) keinem Bild; nur der Rest bis zum Bildbeginn zaehlt.
    /// </summary>
    public static double LueckeOhneRuhe(Bild b) => Math.Max(0, b.LueckeMs - b.RuheMs);

    /// <summary>Weisung P7-16: Teile der Auftraege eines Bildes (Wegsuche, Ereignis schreiben), leer ohne beide.</summary>
    public static string Davon(Bild b) => b.WegMs > 0 || b.EreignisMs > 0
        ? " (davon Wegsuche " + Ms(b.WegMs) + ", Ereignis " + Ms(b.EreignisMs) + ")"
        : "";

    /// <summary>Zeit der Luecke, die keinem gemessenen Teil gehoert (Oberflaeche ohne eigene Auftraege, Grafiktreiber, System).</summary>
    public static double AusserhalbMs(Bild b) => b.LueckeMs < PauseAb * 1000 ? Math.Max(0, LueckeOhneRuhe(b) - b.LayoutMs - b.AuftraegeMs) : 0;

    /// <summary>
    /// Teil mit der meisten Zeit in diesem Bild: Logik, Vorbereitung, Zeichnen, Danach, Layout, Auftraege
    /// oder ausserhalb (Luecke ohne gemessene Arbeit); mit Speicherbereinigung der Zusatz "+ GC".
    /// </summary>
    public static string Teil(Bild b)
    {
        var (name, wert) = ("Logik", b.LogikMs);
        foreach (var (n, w) in (ReadOnlySpan<(string, double)>)[("Vorbereitung", b.VorbereitungMs), ("Zeichnen", b.ZeichnenMs), ("Danach", b.DanachMs),
            ("Layout", b.LayoutMs), ("Aufträge", b.AuftraegeMs), ("außerhalb", AusserhalbMs(b))])
        {
            if (w > wert)
            {
                (name, wert) = (n, w);
            }
        }
        return b.Gc0 + b.Gc1 + b.Gc2 > 0 ? name + " + GC" : name;
    }

    private readonly Bild[] _ring = new Bild[2048];   // 10 s bei 120 Bildern/s passen hinein
    private int _anzahl;
    private int _naechster;
    private double _fensterAb = -1;
    private readonly List<string> _fenster = [];
    private readonly List<Bild> _langsam = [];

    /// <summary>Kopf (Grafik, Geraet), steht ueber den Zeilen.</summary>
    public string Kopf { get; set; } = "";

    /// <summary>Bilder seit dem Start (ohne Pausen-Abstaende).</summary>
    public long Gesamt { get; private set; }

    /// <summary>Ein gezeichnetes Bild aufnehmen; <paramref name="bild"/>.Zeit in Sekunden seit dem Start.</summary>
    public void Aufnehmen(Bild bild)
    {
        if (_fensterAb < 0)
        {
            _fensterAb = bild.Zeit;
        }
        else if (bild.Zeit - _fensterAb >= FensterSekunden)
        {
            FensterSchliessen(bild.Zeit);
        }
        _ring[_naechster] = bild;
        _naechster = (_naechster + 1) % _ring.Length;
        _anzahl = Math.Min(_anzahl + 1, _ring.Length);
        Gesamt++;
        var dauer = Dauer(bild);
        if (dauer > LangsamAbMs)
        {
            _langsam.Add(bild);
            if (_langsam.Count > LangsamMax * 4)
            {
                // nur die langsamsten behalten (selten, nicht je Bild)
                _langsam.Sort((a, b) => Dauer(b).CompareTo(Dauer(a)));
                _langsam.RemoveRange(LangsamMax, _langsam.Count - LangsamMax);
            }
        }
    }

    /// <summary>
    /// Dauer eines Bildes. Mit gemessener Luecke (Weisung P7-15): Luecke plus eigene Rechenzeit, also vom Ende
    /// des letzten Bildes bis zum Ende dieses Bildes; nach einer Pause (Luecke ab <see cref="PauseAb"/>) nur die
    /// gemessene Arbeit. Vorher zaehlte der Abstand von Beginn zu Beginn: dann stand die Zeit eines langsamen
    /// Bildes beim naechsten Bild, dessen eigene Teile fast 0 waren. Ohne Luecke (aeltere Messung) wie vorher.
    /// </summary>
    public static double Dauer(Bild b)
    {
        var eigen = b.LogikMs + b.VorbereitungMs + b.ZeichnenMs + b.DanachMs;
        if (b.LueckeMs > 0)
        {
            return eigen + (b.LueckeMs < PauseAb * 1000 ? LueckeOhneRuhe(b) : b.LayoutMs + b.AuftraegeMs);
        }
        return b.AbstandMs > 0 && b.AbstandMs < PauseAb * 1000 ? b.AbstandMs : eigen;
    }

    // Arbeitsflaechen fuer Zeilen und Text: je Fenster nichts Neues im Speicher ausser der Zeile selbst
    private readonly Bild[] _arbeit = new Bild[2048];
    private readonly double[] _werte = new double[2048];

    /// <summary>Bilder ab <paramref name="ab"/> der Reihe nach in die Arbeitsflaeche; gibt die Anzahl.</summary>
    private int Seit(double ab)
    {
        var n = 0;
        for (var i = 0; i < _anzahl; i++)
        {
            var b = _ring[(_naechster - _anzahl + i + _ring.Length) % _ring.Length];
            if (b.Zeit >= ab)
            {
                _arbeit[n++] = b;
            }
        }
        return n;
    }

    private void FensterSchliessen(double jetzt)
    {
        var n = Seit(_fensterAb);
        _fensterAb = jetzt;
        if (n == 0)
        {
            return;
        }
        _fenster.Add(Zeile(_arbeit.AsSpan(0, n), _werte));
        if (_fenster.Count > FensterMax)
        {
            _fenster.RemoveAt(0);
        }
    }

    /// <summary>Ein Bild im Lauf: Abstand unter der Pause und ohne Ruhe davor (Weisung P7-17: nach Ruhe zaehlt es wie nach einer Pause).</summary>
    private static bool ImLauf(Bild b) => b.AbstandMs > 0 && b.AbstandMs < PauseAb * 1000 && b.RuheMs <= 0;

    public static string Ms(double wert) => wert.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>
    /// Eine Zeile fuer die Bilder eines Fensters: Anzahl, Bilder je Sekunde im Lauf (ohne Pausen), Abstand
    /// Mitte/95 %/hoechster, je Teil Mittel/hoechster, Speicherbereinigungen je Stufe, KB je Bild; dazu
    /// Luecke Mitte/95 %/hoechste, Layout-Durchlaeufe, Auftraege, Speicher im Bild und der groesste Speicher eines Bildes.
    /// </summary>
    public static string Zeile(IReadOnlyList<Bild> bilder) => Zeile([.. bilder], new double[bilder.Count]);

    private static string Zeile(ReadOnlySpan<Bild> bilder, double[] werte)
    {
        var lauf = 0;
        foreach (var b in bilder)
        {
            if (ImLauf(b))
            {
                werte[lauf++] = b.AbstandMs;
            }
        }
        var abstaende = werte.AsSpan(0, lauf);
        abstaende.Sort();
        double summe = 0;
        var langsam = 0;
        foreach (var a in abstaende)
        {
            summe += a;
            langsam += a > LangsamAbMs ? 1 : 0;
        }
        var proSekunde = lauf > 0 ? 1000 / (summe / lauf) : 0;
        var abstand = Verteilung(abstaende);
        var l = 0;
        foreach (var b in bilder)
        {
            if (ImLauf(b))
            {
                werte[l++] = b.LueckeMs;
            }
        }
        werte.AsSpan(0, l).Sort();
        var luecke = Verteilung(werte.AsSpan(0, l));
        int innen = 0, gc0 = 0, gc1 = 0, gc2 = 0, layouts = 0;
        long bytes = 0, bytesBild = 0, bytesMax = 0;
        foreach (var b in bilder)
        {
            innen += b.Innen ? 1 : 0;
            (gc0, gc1, gc2, layouts) = (gc0 + b.Gc0, gc1 + b.Gc1, gc2 + b.Gc2, layouts + b.Layouts);
            bytes += b.Bytes;
            bytesBild += b.BytesBild;
            bytesMax = Math.Max(bytesMax, b.Bytes);
        }
        string Teil(ReadOnlySpan<Bild> alle, Func<Bild, double> wert)
        {
            double s = 0, m = 0;
            foreach (var b in alle)
            {
                var w = wert(b);
                s += w;
                m = Math.Max(m, w);
            }
            return Ms(s / alle.Length) + "/" + Ms(m);
        }
        return string.Create(CultureInfo.InvariantCulture,
            $"{TimeSpan.FromSeconds(bilder[0].Zeit):hh\\:mm\\:ss} {(innen * 2 >= bilder.Length ? "innen" : "Stadt")}: {bilder.Length} Bilder, {proSekunde:0} je s im Lauf, Abstand {abstand} ms, " +
            $"Logik {Teil(bilder, b => b.LogikMs)}, Vorbereitung {Teil(bilder, b => b.VorbereitungMs)}, Zeichnen {Teil(bilder, b => b.ZeichnenMs)}, Danach {Teil(bilder, b => b.DanachMs)} ms, " +
            $"GC {gc0}/{gc1}/{gc2}, {(double)bytes / bilder.Length / 1024:0.0} KB je Bild, " +
            $"{langsam} langsam; Lücke {luecke} ms, Layout {layouts}x {Teil(bilder, b => b.LayoutMs)} ms, " +
            $"Aufträge {Teil(bilder, b => b.AuftraegeMs)} ms, im Bild {(double)bytesBild / bilder.Length / 1024:0.0} KB, höchstens {bytesMax / 1024} KB");
    }

    private static string Verteilung(ReadOnlySpan<double> werte) =>
        werte.Length > 0 ? Ms(werte[werte.Length / 2]) + "/" + Ms(werte[Math.Min(werte.Length - 1, (int)(werte.Length * 0.95))]) + "/" + Ms(werte[^1]) : "-";

    /// <summary>Der ganze Text: Kopf, Erklaerung, Zeilen je 10 s (auch das laufende Fenster), die langsamsten Bilder; hoechstens <see cref="DateiMax"/> Bytes.</summary>
    public string Text()
    {
        var sb = new StringBuilder();
        sb.Append("Bildzeiten im Spiel (je 10 s; Zeiten Mittel/hoechster in ms; Abstand Mitte/95 %/hoechster)\n");
        sb.Append("Lücke = Ende des letzten Bildes bis Beginn des nächsten; Ruhe = Teil der Lücke ohne Anlass (nichts zu zeichnen); außerhalb = Lücke ohne Ruhe, Layout und eigene Aufträge; Dauer = Lücke ohne Ruhe + Bild\n");
        if (Kopf.Length > 0)
        {
            sb.Append(Kopf).Append('\n');
        }
        sb.Append(string.Create(CultureInfo.InvariantCulture, $"Bilder gesamt: {Gesamt}\n"));
        var zeilen = new List<string>(_fenster);
        var offen = Seit(Math.Max(_fensterAb, 0));
        if (offen > 0)
        {
            zeilen.Add(Zeile(_arbeit.AsSpan(0, offen), _werte) + " (laufend)");
        }
        _langsam.Sort((a, b) => Dauer(b).CompareTo(Dauer(a)));
        var schluss = new StringBuilder();
        for (var i = 0; i < Math.Min(LangsamMax, _langsam.Count); i++)
        {
            var b = _langsam[i];
            schluss.Append(string.Create(CultureInfo.InvariantCulture,
                $"  {TimeSpan.FromSeconds(b.Zeit):hh\\:mm\\:ss\\.ff} {(b.Innen ? "innen" : "Stadt")}: {Ms(Dauer(b))} ms (Logik {Ms(b.LogikMs)}, Vorbereitung {Ms(b.VorbereitungMs)}, " +
                $"Zeichnen {Ms(b.ZeichnenMs)}, Danach {Ms(b.DanachMs)}, GC {b.Gc0}/{b.Gc1}/{b.Gc2}, {b.Bytes / 1024} KB) Lücke {Ms(b.LueckeMs)}{(b.RuheMs > 0 ? ", Ruhe " + Ms(b.RuheMs) : "")}, " +
                $"Layout {b.Layouts}x {Ms(b.LayoutMs)}, Aufträge {Ms(b.AuftraegeMs)}{Davon(b)}, außerhalb {Ms(AusserhalbMs(b))}, im Bild {b.BytesBild / 1024} KB, Teil: {Teil(b)}{(b.Eingabe is { Length: > 0 } eingabe ? "; " + eingabe : "")}{(b.System is { Length: > 0 } system ? "; " + system : "")}\n"));
        }
        if (schluss.Length > 0)
        {
            schluss.Insert(0, "Langsamste Bilder (über " + Ms(LangsamAbMs) + " ms):\n");
        }
        // aelteste Zeilen fallen weg, bis alles in die Grenze passt (jede Zeile nur einmal gezaehlt)
        var groesse = Encoding.UTF8.GetByteCount(sb.ToString()) + Encoding.UTF8.GetByteCount(schluss.ToString()) + 1;
        var laengen = zeilen.Select(z => Encoding.UTF8.GetByteCount(z) + 1).ToList();
        groesse += laengen.Sum() - (zeilen.Count > 0 ? 1 : 0);
        var weg = 0;
        while (zeilen.Count - weg > 1 && groesse > DateiMax)
        {
            groesse -= laengen[weg++];
        }
        sb.Append(string.Join("\n", zeilen.Skip(weg))).Append('\n').Append(schluss);
        var text = sb.ToString();
        return Encoding.UTF8.GetByteCount(text) <= DateiMax ? text : text[..(DateiMax / 4)];
    }
}
