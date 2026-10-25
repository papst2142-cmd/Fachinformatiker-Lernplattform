using System.Globalization;
using System.Text;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Nachtrag 1 zu P7-13 (Wunsch 4): Zeitmessung je 3D-Bild (Abstand zum letzten Bild, Logik,
/// Vorbereitung, Zeichnen, Arbeit danach, Speicherbereinigung je Stufe, angelegter Speicher). Haelt die
/// letzten Bilder in einem festen Ring (je Bild nichts Neues im Speicher) und fasst je 10 Sekunden eine
/// Zeile zusammen. Der Text steht in <see cref="DateiName"/> im Datenordner und im Problembericht.
/// Nichts davon geht in den Spielstand.
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

    public readonly record struct Bild(double Zeit, double AbstandMs, double LogikMs, double VorbereitungMs, double ZeichnenMs, double DanachMs,
        int Gc0, int Gc1, int Gc2, long Bytes, bool Innen);

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

    /// <summary>Dauer eines Bildes: der Abstand, sonst (nach einer Pause) die eigene Rechenzeit.</summary>
    public static double Dauer(Bild b) =>
        b.AbstandMs > 0 && b.AbstandMs < PauseAb * 1000 ? b.AbstandMs : b.LogikMs + b.VorbereitungMs + b.ZeichnenMs + b.DanachMs;

    private IEnumerable<Bild> Seit(double ab)
    {
        for (var i = 0; i < _anzahl; i++)
        {
            var b = _ring[(_naechster - _anzahl + i + _ring.Length) % _ring.Length];
            if (b.Zeit >= ab)
            {
                yield return b;
            }
        }
    }

    private void FensterSchliessen(double jetzt)
    {
        var bilder = Seit(_fensterAb).ToList();
        _fensterAb = jetzt;
        if (bilder.Count == 0)
        {
            return;
        }
        _fenster.Add(Zeile(bilder));
        if (_fenster.Count > FensterMax)
        {
            _fenster.RemoveAt(0);
        }
    }

    private static string Ms(double wert) => wert.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>
    /// Eine Zeile fuer die Bilder eines Fensters: Anzahl, Bilder je Sekunde im Lauf (ohne Pausen), Abstand
    /// Mitte/95 %/hoechster, je Teil Mittel/hoechster, Speicherbereinigungen je Stufe, KB je Bild.
    /// </summary>
    public static string Zeile(IReadOnlyList<Bild> bilder)
    {
        var lauf = bilder.Where(b => b.AbstandMs > 0 && b.AbstandMs < PauseAb * 1000).Select(b => b.AbstandMs).Order().ToList();
        string Teil(Func<Bild, double> wert) => Ms(bilder.Average(wert)) + "/" + Ms(bilder.Max(wert));
        var proSekunde = lauf.Count > 0 ? 1000 / lauf.Average() : 0;
        var abstand = lauf.Count > 0 ? Ms(lauf[lauf.Count / 2]) + "/" + Ms(lauf[Math.Min(lauf.Count - 1, (int)(lauf.Count * 0.95))]) + "/" + Ms(lauf[^1]) : "-";
        var innen = bilder.Count(b => b.Innen) * 2 >= bilder.Count ? "innen" : "Stadt";
        return string.Create(CultureInfo.InvariantCulture,
            $"{TimeSpan.FromSeconds(bilder[0].Zeit):hh\\:mm\\:ss} {innen}: {bilder.Count} Bilder, {proSekunde:0} je s im Lauf, Abstand {abstand} ms, " +
            $"Logik {Teil(b => b.LogikMs)}, Vorbereitung {Teil(b => b.VorbereitungMs)}, Zeichnen {Teil(b => b.ZeichnenMs)}, Danach {Teil(b => b.DanachMs)} ms, " +
            $"GC {bilder.Sum(b => b.Gc0)}/{bilder.Sum(b => b.Gc1)}/{bilder.Sum(b => b.Gc2)}, {bilder.Average(b => b.Bytes) / 1024:0.0} KB je Bild, " +
            $"{lauf.Count(a => a > LangsamAbMs)} langsam");
    }

    /// <summary>Der ganze Text: Kopf, Erklaerung, Zeilen je 10 s (auch das laufende Fenster), die langsamsten Bilder; hoechstens <see cref="DateiMax"/> Bytes.</summary>
    public string Text()
    {
        var sb = new StringBuilder();
        sb.Append("Bildzeiten im Spiel (je 10 s; Zeiten Mittel/hoechster in ms; Abstand Mitte/95 %/hoechster)\n");
        if (Kopf.Length > 0)
        {
            sb.Append(Kopf).Append('\n');
        }
        sb.Append(string.Create(CultureInfo.InvariantCulture, $"Bilder gesamt: {Gesamt}\n"));
        var zeilen = new List<string>(_fenster);
        var offen = Seit(Math.Max(_fensterAb, 0)).ToList();
        if (offen.Count > 0)
        {
            zeilen.Add(Zeile(offen) + " (laufend)");
        }
        var langsam = _langsam.OrderByDescending(Dauer).Take(LangsamMax).Select(b => string.Create(CultureInfo.InvariantCulture,
            $"  {TimeSpan.FromSeconds(b.Zeit):hh\\:mm\\:ss\\.ff} {(b.Innen ? "innen" : "Stadt")}: {Ms(Dauer(b))} ms (Logik {Ms(b.LogikMs)}, Vorbereitung {Ms(b.VorbereitungMs)}, " +
            $"Zeichnen {Ms(b.ZeichnenMs)}, Danach {Ms(b.DanachMs)}, GC {b.Gc0}/{b.Gc1}/{b.Gc2}, {b.Bytes / 1024} KB)")).ToList();
        var schluss = langsam.Count > 0 ? "Langsamste Bilder (über " + Ms(LangsamAbMs) + " ms):\n" + string.Join("\n", langsam) + "\n" : "";
        // aelteste Zeilen fallen weg, bis alles in die Grenze passt
        while (zeilen.Count > 1 && Encoding.UTF8.GetByteCount(sb.ToString() + string.Join("\n", zeilen) + "\n" + schluss) > DateiMax)
        {
            zeilen.RemoveAt(0);
        }
        sb.Append(string.Join("\n", zeilen)).Append('\n').Append(schluss);
        var text = sb.ToString();
        return Encoding.UTF8.GetByteCount(text) <= DateiMax ? text : text[..(DateiMax / 4)];
    }
}
