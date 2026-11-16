using System.Diagnostics;
using System.Globalization;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Spiel;

/// <summary>
/// Welle Nachladen A 0.82.0 (Weisung P12-280): Probe mit der grossen Testkarte (256 × 256) fuer den Start-Test (Teil
/// <c>nachladen</c>) und die Probe-APK (Knopf „Testkarte“, nur dort). Die Testkarte kommt nie ins Spiel: der Start-Test liest
/// sie aus <see cref="Umgebung"/> (Pfad in der Kopie des Repos), die Probe-APK aus ihrer eingebetteten Datei (<see cref="Quelle"/>).
/// Gemessen wird nur: Gang ueber die Karte (laengstes Bild, Loecher, Netzdaten, sofort gebaute Kacheln, Zeiten „Kachel
/// nachladen“) und eine Ankunft (Ziel vorher vorbereitet, dann ein Sprung dorthin). Kein Spielstand, kein Ereignis.
/// </summary>
public static class Nachladeprobe
{
    /// <summary>Umgebungsvariable mit dem Pfad der Testkarte (Start-Test).</summary>
    public const string Umgebung = "LERNPLATTFORM_TESTKARTE";

    /// <summary>Probe-APK: die eingebettete Testkarte (sonst null).</summary>
    public static Func<byte[]?>? Quelle { get; set; }

    /// <summary>Knopf „Testkarte“ in der Stadt zeigen (nur Probe-APK).</summary>
    public static bool An => Quelle is not null;

    /// <summary>Gehtempo der Probe: doppelt so schnell wie die Figur (strenger als zu Fuss).</summary>
    public const float Tempo = 2 * StadtSpiel.Tempo;

    /// <summary>Die Bytes der Testkarte oder null (nicht vorhanden).</summary>
    public static byte[]? Laden()
    {
        if (Quelle?.Invoke() is { } b)
        {
            return b;
        }
        return Environment.GetEnvironmentVariable(Umgebung) is { Length: > 0 } pfad && File.Exists(pfad) ? File.ReadAllBytes(pfad) : null;
    }

    /// <summary>Ein Spiel auf der Testkarte mit Grafik, Geschaeften und Festen des laufenden Spiels.</summary>
    public static StadtSpiel Spiel(byte[] datei, StadtSpiel alt)
    {
        var (stadt, innen) = SpielStart.AusDateiFuerTests(datei);
        return SpielStart.Neu(stadt, alt.Grafik, innen, alt.GeschaefteDaten, alt.Feste, Festzeit.Heute, Jahreszeiten.Heute);
    }

    /// <summary>Weg des Gangs: Schlangenlinie ueber die ganze Karte (Reihen im Abstand <paramref name="reihen"/>).</summary>
    public static List<(float X, float Z)> Schlange(float breite, float tiefe, float reihen = 48f, float rand = 8f)
    {
        var punkte = new List<(float X, float Z)>();
        var links = true;
        for (var z = rand; z <= tiefe - rand + 0.01f; z += reihen)
        {
            punkte.Add((links ? rand : breite - rand, z));
            punkte.Add((links ? breite - rand : rand, z));
            links = !links;
        }
        return punkte;
    }

    /// <summary>Ergebnis eines Gangs oder einer Ankunft.</summary>
    public sealed record Ergebnis(int Bilder, double LaengstesMs, double MitteMs, int BilderMitLoch, int LoecherHoechstens, long HoechstBytes,
        int Sofort, IReadOnlyList<double> BauMs, IReadOnlyList<double> HochladenMs, double Sekunden, double Weg)
    {
        public double HoechstMb => HoechstBytes / 1048576.0;
    }

    /// <summary>
    /// Die Figur geht mit <paramref name="tempo"/> (Feld je s, nach der Uhr, ein Bild hoechstens 0,25 s) den Weg ab. Jedes Bild
    /// wird gemessen (Dauer, Kacheln ohne Nahform im Bild). Die Kamera folgt der Figur wie im Spiel.
    /// </summary>
    public static async Task<Ergebnis> GangAsync(StadtFlaeche flaeche, StadtSpiel spiel, IReadOnlyList<(float X, float Z)> weg, float tempo = Tempo,
        double hoechstensSekunden = 600)
    {
        // erst am Anfang des Wegs stehen und warten, bis dort alles Noetige oben ist (Laden der Karte und ein Sprung
        // gehoeren zu „Stadt bauen“ bzw. zur Ankunft, nicht zum Gehen); hoechstens 30 s
        var (x0, z0) = weg[0];
        spiel.HinstellenFuerTests(x0, z0);
        var warten = Stopwatch.StartNew();
        for (var ruhig = 0; ruhig < 5 && warten.Elapsed.TotalSeconds < 30;)
        {
            await flaeche.NaechstesBildAsync();
            ruhig = flaeche.Lader is { } l && l.Naechste() is null && !l.Fehlend().Any() && flaeche.KachelnLoch == 0 ? ruhig + 1 : 0;
        }
        var bilder = new List<BildZeiten.Bild>();
        var vorher = StadtFlaeche.Mitschnitt;
        var lader = flaeche.Lader;
        var (sofort0, bau0, hoch0) = (lader?.Sofort ?? 0, lader?.BauZeiten.Count ?? 0, flaeche.KachelHochladenZeiten.Count);
        var mitLoch = 0;
        var loecher = 0;
        var gesamt = 0.0;
        var uhr = Stopwatch.StartNew();
        var letzte = 0.0;
        var stelle = 0;
        var (x, z) = weg[0];
        spiel.HinstellenFuerTests(x, z);
        var fertig = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void NachBild()
        {
            if (flaeche.KachelnLoch > 0)
            {
                mitLoch++;
                loecher = Math.Max(loecher, flaeche.KachelnLoch);
            }
            if (stelle >= weg.Count - 1 || uhr.Elapsed.TotalSeconds > hoechstensSekunden)
            {
                flaeche.Dauerlauf = false;
                fertig.TrySetResult();
            }
        }
        flaeche.VorBild = () =>
        {
            var jetzt = uhr.Elapsed.TotalSeconds;
            var rest = tempo * Math.Min(0.25, jetzt - letzte);
            letzte = jetzt;
            while (rest > 0 && stelle < weg.Count - 1)
            {
                var (zx, zz) = weg[stelle + 1];
                var (dx, dz) = (zx - x, zz - z);
                var weite = MathF.Sqrt((dx * dx) + (dz * dz));
                if (weite <= rest)
                {
                    (x, z) = (zx, zz);
                    rest -= weite;
                    gesamt += weite;
                    stelle++;
                }
                else
                {
                    x += (float)(dx / weite * rest);
                    z += (float)(dz / weite * rest);
                    gesamt += rest;
                    rest = 0;
                }
            }
            spiel.HinstellenFuerTests(x, z);
        };
        try
        {
            StadtFlaeche.Mitschnitt = bilder;
            flaeche.BildGezeichnet += NachBild;
            flaeche.Dauerlauf = true;
            flaeche.AnfordernBild();
            await fertig.Task;
        }
        finally
        {
            flaeche.BildGezeichnet -= NachBild;
            flaeche.VorBild = null;
            flaeche.Dauerlauf = false;
            StadtFlaeche.Mitschnitt = vorher;
        }
        return Auswerten(flaeche, bilder, mitLoch, loecher, sofort0, bau0, hoch0, uhr.Elapsed.TotalSeconds, gesamt);
    }

    /// <summary>
    /// Ankunft mit Bus oder Zug: das Ziel wird vorgegeben (wie das Ende des Wegs im Spiel) und im Hintergrund vorbereitet (bis
    /// <paramref name="warteSekunden"/>), dann springt die Figur dorthin; gemessen werden die Bilder danach.
    /// </summary>
    public static async Task<(Ergebnis Ergebnis, bool Vorbereitet)> AnkunftAsync(StadtFlaeche flaeche, StadtSpiel spiel, float zx, float zz,
        double warteSekunden = 20, int bilderDanach = 30)
    {
        var lader = flaeche.Lader;
        var noetig = Ladeplan.Noetig(zx, zz, spiel.Szene.Breite, spiel.Szene.Tiefe);
        flaeche.ZielFuerProbe = (zx, zz);
        var vorbereitet = false;
        try
        {
            var uhr = Stopwatch.StartNew();
            while (uhr.Elapsed.TotalSeconds < warteSekunden)
            {
                await flaeche.NaechstesBildAsync();
                if (lader is not null && noetig.All(k => lader.Bereit.ContainsKey(k) || lader.Geladen.ContainsKey(k)))
                {
                    vorbereitet = true;
                    break;
                }
                await Task.Delay(20);
            }
            var bilder = new List<BildZeiten.Bild>();
            var vorher = StadtFlaeche.Mitschnitt;
            var (sofort0, bau0, hoch0) = (lader?.Sofort ?? 0, lader?.BauZeiten.Count ?? 0, flaeche.KachelHochladenZeiten.Count);
            var mitLoch = 0;
            var loecher = 0;
            var dauer = Stopwatch.StartNew();
            StadtFlaeche.Mitschnitt = bilder;
            try
            {
                spiel.HinstellenFuerTests(zx, zz);
                flaeche.AktuelleKamera?.Setzen(zx, zz);
                for (var i = 0; i < bilderDanach; i++)
                {
                    await flaeche.NaechstesBildAsync();
                    if (flaeche.KachelnLoch > 0)
                    {
                        mitLoch++;
                        loecher = Math.Max(loecher, flaeche.KachelnLoch);
                    }
                }
            }
            finally
            {
                StadtFlaeche.Mitschnitt = vorher;
            }
            return (Auswerten(flaeche, bilder, mitLoch, loecher, sofort0, bau0, hoch0, dauer.Elapsed.TotalSeconds, 0), vorbereitet);
        }
        finally
        {
            flaeche.ZielFuerProbe = null;
        }
    }

    private static Ergebnis Auswerten(StadtFlaeche flaeche, List<BildZeiten.Bild> bilder, int mitLoch, int loecher, int sofort0, int bau0, int hoch0,
        double sekunden, double weg)
    {
        var lader = flaeche.Lader;
        var dauern = bilder.Select(BildZeiten.Dauer).Order().ToList();
        return new Ergebnis(bilder.Count, dauern.Count > 0 ? dauern[^1] : 0, dauern.Count > 0 ? dauern[dauern.Count / 2] : 0, mitLoch, loecher,
            lader?.HoechstBytes ?? 0, (lader?.Sofort ?? 0) - sofort0, [.. (lader?.BauZeiten ?? []).Skip(bau0)],
            [.. flaeche.KachelHochladenZeiten.Skip(hoch0)], sekunden, weg);
    }

    /// <summary>Eine Zeile (spiel_bilder.log und Start-Test).</summary>
    public static string Zeile(string name, Ergebnis e)
    {
        var inv = CultureInfo.InvariantCulture;
        static string Reihe(IReadOnlyList<double> w)
        {
            if (w.Count == 0)
            {
                return "keine";
            }
            var s = w.Order().ToList();
            return string.Create(CultureInfo.InvariantCulture, $"{s.Count} Kacheln, Mitte {s[s.Count / 2]:F1} / 95 % {s[Math.Min(s.Count - 1, (int)(s.Count * 0.95))]:F1} / hoechstens {s[^1]:F1} ms");
        }
        return string.Create(inv,
            $"Nachladeprobe {name}: {e.Bilder} Bilder in {e.Sekunden:F1} s ({e.Weg:F0} Felder), laengstes Bild {e.LaengstesMs:F1} ms (Mitte {e.MitteMs:F1}), Bilder mit fehlender Nahform {e.BilderMitLoch} (hoechstens {e.LoecherHoechstens} Kacheln), Netzdaten hoechstens {e.HoechstMb:F1} MB, sofort gebaut {e.Sofort}; Kachel bauen (Hintergrund): {Reihe(e.BauMs)}; Kachel hochladen: {Reihe(e.HochladenMs)}");
    }
}
