using System.Diagnostics;
using System.Globalization;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Spiel;

/// <summary>
/// Weisung P8-52 (W12a, Teilplan P8-51 Abschnitt 4.1): Leistungsprobe nur in der Probe-APK (<see cref="An"/> setzt
/// nur die Probe-APK). Erst einmal Vorladen (alle Netze auf dem Prozessor, Stadt neu in die Grafikkarte) mit der Zeit
/// bis "fertig", dann die Stadt in der Gesamtansicht in Stufen je <see cref="StufeSekunden"/> s mit langsamem Schwenk:
/// 1- bis 4-fache Dreiecke, dann 20, 40 und 80 Zeichenaufrufe. Je Stufe eine Zeile (Bilder/s, Mitte, 95 %, hoechstes
/// Bild, Werte von Android mit Waermezustand) in spiel_bilder.log und damit im Problembericht. Nur Messung: kein
/// Spielstand, kein Ereignis, nichts gespeichert ausser spiel_bilder.log.
/// </summary>
public static class Leistungsprobe
{
    /// <summary>Knopf "Leistungsprobe" in der Stadt zeigen (nur Probe-APK).</summary>
    public static bool An { get; set; }

    /// <summary>Bildschirm waehrend der Probe anlassen (Android: Fenster mit KeepScreenOn), sonst null.</summary>
    public static Action<bool>? BildschirmAn { get; set; }

    public const double StufeSekunden = 10;

    /// <summary>Die Stufen: (Name, Stadt so oft, mindestens so viele Zeichenaufrufe).</summary>
    public static readonly (string Name, int Mehrfach, int Aufrufe)[] Stufen =
    [
        ("Dreiecke x1", 1, 0),
        ("Dreiecke x2", 2, 0),
        ("Dreiecke x3", 3, 0),
        ("Dreiecke x4", 4, 0),
        ("Aufrufe 20", 1, 20),
        ("Aufrufe 40", 1, 40),
        ("Aufrufe 80", 1, 80),
    ];

    public static bool Laeuft { get; private set; }

    /// <summary>Die ganze Probe; gibt die Zeilen (stehen auch in <see cref="BildZeiten.Probe"/>).</summary>
    public static async Task<IReadOnlyList<string>> LaufenAsync(StadtFlaeche flaeche, StadtSpiel spiel, double sekunden = StufeSekunden)
    {
        var zeilen = new List<string>();
        void Zeile(string text)
        {
            zeilen.Add(text);
            flaeche.Zeiten.Probe.Add(text);
        }
        if (Laeuft || flaeche.AktuelleKamera is not { } kamera)
        {
            return zeilen;
        }
        Laeuft = true;
        BildschirmAn?.Invoke(true);
        var vorher = StadtFlaeche.Mitschnitt;
        var bilder = new List<BildZeiten.Bild>();
        var abstand = kamera.Abstand;
        var (zielX, zielZ) = (kamera.ZielX, kamera.ZielZ);
        try
        {
            flaeche.Zeiten.Probe.Clear();
            // ohne Uhrzeit des Geraets: das Datum liest im Spiel nur Festzeit.Datum()
            Zeile("Leistungsprobe: je Stufe "
                + sekunden.ToString("0", CultureInfo.InvariantCulture) + " s, Grafik " + flaeche.GlKurz);
            Zeile(await VorladenAsync(flaeche, spiel, bilder));
            kamera.Zoomen(kamera.Abstand / Kamera.AbstandMax);
            kamera.Setzen(spiel.Szene.Breite / 2, spiel.Szene.Tiefe / 2);
            foreach (var (name, mehrfach, aufrufe) in Stufen)
            {
                StadtFlaeche.ProbeMehrfach = mehrfach;
                StadtFlaeche.ProbeAufrufe = aufrufe;
                // erst ein paar Bilder ohne Wertung (neue Stufe einschwingen)
                StadtFlaeche.Mitschnitt = null;
                for (var i = 0; i < 5; i++)
                {
                    await flaeche.NaechstesBildAsync();
                }
                bilder.Clear();
                StadtFlaeche.Mitschnitt = bilder;
                var beginn = Stopwatch.GetTimestamp();
                await SchwenkAsync(flaeche, sekunden);
                StadtFlaeche.Mitschnitt = null;
                var dauer = Stopwatch.GetElapsedTime(beginn).TotalSeconds;
                Zeile(StufenZeile(name, bilder, dauer, flaeche.LetzteDreiecke, flaeche.LetzteZeichenaufrufe, Systemwerte.Seit?.Invoke(beginn)));
            }
        }
        finally
        {
            StadtFlaeche.ProbeMehrfach = 1;
            StadtFlaeche.ProbeAufrufe = 0;
            StadtFlaeche.Mitschnitt = vorher;
            flaeche.Dauerlauf = false;
            flaeche.VorBild = null;
            kamera.Zoomen(kamera.Abstand / abstand);
            kamera.Setzen(zielX, zielZ);
            flaeche.AnfordernBild();
            BildschirmAn?.Invoke(false);
            Laeuft = false;
        }
        return zeilen;
    }

    /// <summary>Vorladen messen: alle Netze auf dem Prozessor (im Hintergrund), dann die Stadt neu hochladen (ein Bild).</summary>
    private static async Task<string> VorladenAsync(StadtFlaeche flaeche, StadtSpiel spiel, List<BildZeiten.Bild> bilder)
    {
        var inv = CultureInfo.InvariantCulture;
        var beginn = Stopwatch.GetTimestamp();
        var (szene, stand, innenraeume, grafik, geschaefte, feste) = (spiel.Szene, spiel.Ablauf.Stand.Kopie(), spiel.Innenraeume, spiel.Grafik, spiel.GeschaefteDaten, spiel.Feste);
        var (ecken, raeume, cpuMs) = await Task.Run(() =>
        {
            var uhr = Stopwatch.StartNew();
            long e = 0;
            void Zaehle(Netz n) => e += n.AnzahlEcken;
            Zaehle(szene.NetzBauen());
            foreach (var haus in szene.Haeuser)
            {
                Zaehle(szene.HausNetz(haus));
            }
            if (szene.GruenNetzBauen() is { } gn)
            {
                Zaehle(gn);
            }
            if (grafik is not null)
            {
                Zaehle(szene.PaketNetzBauen(false));
                Zaehle(szene.PaketNetzBauen(true));
            }
            var r = 0;
            foreach (var ort in szene.Orte.Where(o => Innenraum.OrtFuer(o.Ansicht) is not null))
            {
                if (Innenraum.Bauen(ort.Ansicht, stand, innenraeume, grafik, geschaefte, null, feste, szene.Jahreszeit) is not { } innen)
                {
                    continue;
                }
                innen.Vorbereiten();
                Zaehle(innen.NetzOhneWaende);
                foreach (var w in innen.Waende)
                {
                    Zaehle(innen.WandNetzFertig(w));
                }
                r++;
            }
            return (e, r, uhr.Elapsed.TotalMilliseconds);
        });
        bilder.Clear();
        StadtFlaeche.Mitschnitt = bilder;
        var hoch = Stopwatch.GetTimestamp();
        flaeche.ProbeNeuLaden();
        await flaeche.NaechstesBildAsync();
        await flaeche.NaechstesBildAsync();
        StadtFlaeche.Mitschnitt = null;
        var hochMs = Stopwatch.GetElapsedTime(hoch).TotalMilliseconds;
        var bildMax = bilder.Count > 0 ? bilder.Max(BildZeiten.Dauer) : 0;
        var vorbereitung = bilder.Count > 0 ? bilder.Max(b => b.VorbereitungMs) : 0;
        return string.Create(inv,
            $"Leistungsprobe Vorladen: Prozessor {cpuMs:F0} ms ({raeume} Innenräume, {ecken} Ecken), Stadt hochladen {vorbereitung:F0} ms, längstes Bild {bildMax:F0} ms, bis fertig {Stopwatch.GetElapsedTime(beginn).TotalMilliseconds:F0} ms (davon Hochladen und 2 Bilder {hochMs:F0} ms)");
    }

    /// <summary>Langsam hin und her schwenken, bis die Zeit um ist.</summary>
    private static async Task SchwenkAsync(StadtFlaeche flaeche, double sekunden)
    {
        var schritt = 0;
        var uhr = Stopwatch.StartNew();
        var fertig = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Bild()
        {
            if (uhr.Elapsed.TotalSeconds >= sekunden)
            {
                flaeche.Dauerlauf = false;
                fertig.TrySetResult();
            }
        }
        flaeche.VorBild = () =>
        {
            var richtung = (schritt++ / 120 % 2 == 0) ? 1f : -1f;
            flaeche.AktuelleKamera?.Verschieben(1.5f * richtung, 0f, (float)Math.Max(1, flaeche.Bounds.Height));
        };
        flaeche.BildGezeichnet += Bild;
        flaeche.Dauerlauf = true;
        flaeche.AnfordernBild();
        await fertig.Task;
        flaeche.BildGezeichnet -= Bild;
        flaeche.VorBild = null;
    }

    /// <summary>Zeile einer Stufe: Bilder, je s, Dauer Mitte/95 %/hoechstes, ueber 16,7 und 33,3 ms, GC, Dreiecke, Aufrufe, Android.</summary>
    public static string StufenZeile(string name, IReadOnlyList<BildZeiten.Bild> bilder, double sekunden, long dreiecke, int aufrufe, string? system)
    {
        var inv = CultureInfo.InvariantCulture;
        if (bilder.Count == 0)
        {
            return "Leistungsprobe " + name + ": keine Bilder";
        }
        var dauern = bilder.Select(BildZeiten.Dauer).OrderBy(d => d).ToList();
        var p95 = dauern[Math.Min(dauern.Count - 1, (int)(dauern.Count * 0.95))];
        var gc = bilder.Sum(b => b.Gc0 + b.Gc1 + b.Gc2);
        return string.Create(inv,
            $"Leistungsprobe {name}: {dreiecke} Dreiecke, {aufrufe} Aufrufe, {bilder.Count} Bilder, {bilder.Count / Math.Max(sekunden, 0.001):F0} je s, Dauer Mitte {dauern[dauern.Count / 2]:F1} / 95 % {p95:F1} / höchstes {dauern[^1]:F1} ms, über 16,7 ms {dauern.Count(d => d > 1000.0 / 60)}, über 33,3 ms {dauern.Count(d => d > 2000.0 / 60)}, GC {gc}")
            + (system is { Length: > 0 } ? "; " + system : "");
    }
}
