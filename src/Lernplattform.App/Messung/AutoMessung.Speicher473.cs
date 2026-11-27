using System.Globalization;
using Avalonia.Input;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Patch A 1.19.1 (Sammelliste 473, Weisung P12-444): Speicher je Bild im Spiel. Die Figur geht in Echtzeit durch die
/// Stadt und in einem Innenraum; je Bild zaehlt der Speicher, den der Oberflaechen-Thread seit dem letzten Bild angelegt
/// hat (Spalte „KB je Bild“ in spiel_bilder.log), dazu die Speicherbereinigungen der Stufe 0 je 10 s. Grenzen aus der
/// Messung vor der Ursache (A 0.74.0 am Tab A8: rund 100 KB je Bild, 8 Bereinigungen je 10 s).
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Teil nur fuer diese Messung (lokal, schneller Lauf).</summary>
    public const string TeilSpeicher = "speicher";

    /// <summary>Grenze: Mittel des Speichers je Bild beim Gehen (Stadt und innen).</summary>
    public const double SpeicherJeBildKbGrenze = 200;

    /// <summary>Grenze: Speicherbereinigungen der Stufe 0 je 10 s beim Gehen (A 0.74.0 am Tab A8: 8, A 1.18.0: 80 bis 138).</summary>
    public const double GcJe10sGrenze = 20;

    /// <summary>Grenze: Speicher je Beschriften der Spielseite mit langem Spielstand (A 1.19.0 am PC: rund 1 200 KB).</summary>
    public const double BeschriftenKbGrenze = 60;

    /// <summary>Wege fuer den langen Spielstand (rund 460 Spielereignisse; Tab A8 von Nico: 549).</summary>
    public const int LangerStandWege = 520;

    /// <summary>
    /// Speicher je Beschriften (zweimal je Sekunde und nach jedem Halt) mit langem Spielstand: hoechstens
    /// <see cref="BeschriftenKbGrenze"/>, der Stand der Stadt wird dabei nie neu durch den Verlauf gerechnet. Gegenprobe ohne die
    /// zwei Merker (wie bis A 1.19.0): ueber der Grenze.
    /// </summary>
    private static int BeschriftenPruefen(SpielSeite seite, StadtSpiel spiel)
    {
        var inv = CultureInfo.InvariantCulture;
        (double Kb, double Ms, long Gerechnet) Messen()
        {
            for (var i = 0; i < 3; i++)
            {
                seite.Beschriften();
            }
            var gerechnet = spiel.Stadt.StandGerechnet;
            var b0 = GC.GetAllocatedBytesForCurrentThread();
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var i = 0; i < 20; i++)
            {
                seite.Beschriften();
            }
            return ((GC.GetAllocatedBytesForCurrentThread() - b0) / 20.0 / 1024, System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds / 20,
                spiel.Stadt.StandGerechnet - gerechnet);
        }
        var jetzt = Messen();
        (double Kb, double Ms, long Gerechnet) alt;
        try
        {
            Core.Spiel.Stadt.StandOhneMerkerFuerTests = true;
            SpielSeite.OffenOhneMerkerFuerTests = true;
            alt = Messen();
        }
        finally
        {
            Core.Spiel.Stadt.StandOhneMerkerFuerTests = false;
            SpielSeite.OffenOhneMerkerFuerTests = false;
        }
        var wert = string.Create(inv, $"{spiel.Ablauf.Stand.Verlauf.Count} Spielereignisse, {jetzt.Kb:F1} KB und {jetzt.Ms:F2} ms je Beschriften, Stand {jetzt.Gerechnet}x neu gerechnet; Gegenprobe ohne Merker {alt.Kb:F1} KB und {alt.Ms:F2} ms");
        Messwerte.Notiere("Spiel: Speicher je Beschriften: " + wert);
        if (jetzt.Kb > BeschriftenKbGrenze || jetzt.Gerechnet != 0 || alt.Kb <= BeschriftenKbGrenze || alt.Gerechnet < 20 || spiel.Ablauf.Stand.Verlauf.Count < 400)
        {
            Messwerte.Notiere(string.Create(inv, $"  FEHLER: Spiel Speicher je Beschriften: {wert} (Grenze {BeschriftenKbGrenze:F0} KB, Gegenprobe darueber und 20x neu gerechnet, mindestens 400 Spielereignisse)"));
            return 1;
        }
        return 0;
    }

    private static async Task<int> Speicher473Async(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null || !await GlAbwartenAsync(seite))
        {
            Messwerte.Notiere("Spiel: Speicher je Bild: ohne 3D nicht gemessen");
            return 0;
        }
        var vorher = seite.Spiel;
        var fl = seite.Flaeche;
        try
        {
            var spiel = Frisch(seite);
            await fl.NaechstesBildAsync();
            var sz = spiel.Szene;
            // ein langer Spielstand wie am Tab A8 (549 Spielereignisse): 520 Wege zu festen Orten (gleiche Folge in jedem Lauf)
            var zufall = new Random(473);
            for (var w = 0; w < LangerStandWege; w++)
            {
                spiel.GeheZu(sz.Orte[zufall.Next(sz.Orte.Count)].Id);
                spiel.SofortAnkommen();
            }
            await fl.NaechstesBildAsync();
            funde += BeschriftenPruefen(seite, spiel);
            async Task<List<Core.Spiel.Probe.BildZeiten.Bild>> GehenMessen(double sekunden, Func<bool> neuerWeg)
            {
                var bilder = new List<Core.Spiel.Probe.BildZeiten.Bild>();
                // ein paar Bilder Anlauf (das erste Bild nach dem Weg baut die Figur neu)
                for (var i = 0; i < 5; i++)
                {
                    await fl.NaechstesBildAsync();
                }
                StadtFlaeche.Mitschnitt = bilder;
                var uhr = System.Diagnostics.Stopwatch.StartNew();
                while (uhr.Elapsed.TotalSeconds < sekunden)
                {
                    if (!spiel.Geht && !neuerWeg())
                    {
                        break;
                    }
                    await fl.NaechstesBildAsync();
                }
                StadtFlaeche.Mitschnitt = null;
                return bilder;
            }
            void Werte(string wo, List<Core.Spiel.Probe.BildZeiten.Bild> bilder)
            {
                var n = Math.Max(1, bilder.Count);
                var kb = bilder.Sum(b => (double)b.Bytes) / n / 1024;
                var kbBild = bilder.Sum(b => (double)b.BytesBild) / n / 1024;
                var sek = Math.Max(0.001, bilder.Sum(b => b.AbstandMs) / 1000);
                var gc0 = bilder.Sum(b => b.Gc0) / sek * 10;
                var logik = bilder.Select(b => b.LogikMs).DefaultIfEmpty(0).Max();
                var wert = string.Create(inv, $"{bilder.Count} Bilder in {sek:F1} s, {kb:F1} KB je Bild (im Bild {kbBild:F1} KB), GC Stufe 0 {gc0:F1} je 10 s, Logik hoechstens {logik:F1} ms, Layout {bilder.Sum(b => b.Layouts)}x");
                Messwerte.Notiere("Spiel: Speicher je Bild " + wo + ": " + wert);
                if (bilder.Count < 30 || kb > SpeicherJeBildKbGrenze || gc0 > GcJe10sGrenze)
                {
                    Messwerte.Notiere(string.Create(inv, $"  FEHLER: Spiel Speicher je Bild {wo}: {wert} (Grenze {SpeicherJeBildKbGrenze:F0} KB je Bild, {GcJe10sGrenze:F0} Bereinigungen je 10 s, mindestens 30 Bilder)"));
                    funde++;
                }
            }

            void Druecken(Key taste) => seite.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = taste, Source = fl });
            void Los(Key taste) => seite.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = taste, Source = fl });
            // frei gehen (Tasten wie die Sticks am Tablet): je 1,5 s eine andere Richtung, damit die Figur nicht an einer Wand steht
            async Task<List<Core.Spiel.Probe.BildZeiten.Bild>> FreiMessen(double sekunden)
            {
                var bilder = new List<Core.Spiel.Probe.BildZeiten.Bild>();
                Key[] tasten = [Key.W, Key.D, Key.S, Key.A];
                var t = 0;
                Druecken(tasten[0]);
                for (var i = 0; i < 5; i++)
                {
                    await fl.NaechstesBildAsync();
                }
                StadtFlaeche.Mitschnitt = bilder;
                var uhr = System.Diagnostics.Stopwatch.StartNew();
                var wechsel = 1.5;
                while (uhr.Elapsed.TotalSeconds < sekunden)
                {
                    if (uhr.Elapsed.TotalSeconds > wechsel)
                    {
                        Los(tasten[t % 4]);
                        t++;
                        Druecken(tasten[t % 4]);
                        wechsel += 1.5;
                    }
                    await fl.NaechstesBildAsync();
                }
                StadtFlaeche.Mitschnitt = null;
                Los(tasten[t % 4]);
                fl.GehTastenLos();
                for (var i = 0; i < 40 && spiel.FreiOffen; i++)
                {
                    await fl.NaechstesBildAsync();
                }
                return bilder;
            }
            // 1 Stadt: zum weitesten Ort gehen, dann zum naechstweitesten (echtes Gehen, Kamera folgt)
            var ziele = sz.Orte.OrderByDescending(o => MathF.Abs(o.StehX - spiel.FigurX) + MathF.Abs(o.StehZ - spiel.FigurZ)).Take(4).ToList();
            var zi = 0;
            bool NaechstesZiel()
            {
                while (zi < ziele.Count)
                {
                    if (spiel.GeheZu(ziele[zi++].Id) == StadtGrund.Ok)
                    {
                        return true;
                    }
                }
                return false;
            }
            NaechstesZiel();
            Werte("Stadt gehen", await GehenMessen(6, NaechstesZiel));
            spiel.SofortAnkommen();
            Werte("Stadt frei gehen", await FreiMessen(6));

            // 2 innen: ins naechste Haus mit Innenraum, dort zwischen zwei Stellen hin und her
            spiel.SofortAnkommen();
            var hinein = false;
            foreach (var ort in sz.Orte.OrderBy(o => MathF.Abs(o.StehX - spiel.FigurX) + MathF.Abs(o.StehZ - spiel.FigurZ)))
            {
                if (spiel.GeheZu(ort.Id) != StadtGrund.Ok)
                {
                    continue;
                }
                spiel.SofortAnkommen();
                if (spiel.KannBetreten && spiel.Betreten() && spiel.ImInnenraum)
                {
                    hinein = true;
                    break;
                }
            }
            if (!hinein)
            {
                Messwerte.Notiere("  FEHLER: Spiel Speicher je Bild innen: kein Haus betreten");
                return funde + 1;
            }
            await fl.NaechstesBildAsync();
            var stellen = new[] { (1.0, 1.0), (4.0, 2.5), (2.0, 3.5) };
            var si = 0;
            bool InnenWeiter()
            {
                for (var v = 0; v < stellen.Length; v++)
                {
                    var (x, z) = stellen[si++ % stellen.Length];
                    if (spiel.GeheZu(x, z))
                    {
                        return true;
                    }
                }
                return false;
            }
            InnenWeiter();
            Werte("innen gehen", await GehenMessen(6, InnenWeiter));
            spiel.SofortAnkommen();
            Werte("innen frei gehen", await FreiMessen(6));

            // 3 Tablet quer (wie Galaxy Tab A8): Stadt antippen und mit dem Stick gehen
            if (Avalonia.Controls.TopLevel.GetTopLevel(ansicht) is Avalonia.Controls.Window fenster)
            {
                var (bv, hv) = (fenster.Width, fenster.Height);
                var (hb, hh) = Hoechstgroesse(fenster);
                try
                {
                    fenster.Width = Math.Min(1280, hb);
                    fenster.Height = Math.Min(800, hh);
                    await ansicht.BildFertigAsync();
                    await ansicht.BildFertigAsync();
                    spiel.SofortAnkommen();
                    for (var i = 0; i < 10; i++)
                    {
                        await ansicht.BildFertigAsync();
                        await fl.NaechstesBildAsync();
                    }
                    ziele = sz.Orte.OrderByDescending(o => MathF.Abs(o.StehX - spiel.FigurX) + MathF.Abs(o.StehZ - spiel.FigurZ)).Take(4).ToList();
                    zi = 0;
                    NaechstesZiel();
                    Werte("Tablet Stadt gehen", await GehenMessen(6, NaechstesZiel));
                    spiel.SofortAnkommen();
                    await fl.NaechstesBildAsync();
                    var b = fl.StickBereich;
                    var links = new Avalonia.Point(b.Breite / 4, b.Hoehe - b.Unten - (b.FreiHoehe / 4));
                    var bilder = new List<Core.Spiel.Probe.BildZeiten.Bild>();
                    fl.FingerAuf(4731, links);
                    fl.FingerBewegt(4731, links + new Avalonia.Point(0, -Sticks.AnfangAb * 2));
                    for (var i = 0; i < 5; i++)
                    {
                        await fl.NaechstesBildAsync();
                    }
                    StadtFlaeche.Mitschnitt = bilder;
                    var uhr = System.Diagnostics.Stopwatch.StartNew();
                    var rd = 0;
                    var dirs = new[] { new Avalonia.Point(0, -1), new Avalonia.Point(1, 0), new Avalonia.Point(0, 1), new Avalonia.Point(-1, 0) };
                    var wechsel = 1.5;
                    while (uhr.Elapsed.TotalSeconds < 6)
                    {
                        if (uhr.Elapsed.TotalSeconds > wechsel)
                        {
                            rd++;
                            wechsel += 1.5;
                        }
                        var d = dirs[rd % 4];
                        fl.FingerBewegt(4731, links + new Avalonia.Point(d.X * Sticks.Radius * 0.8, d.Y * Sticks.Radius * 0.8));
                        await fl.NaechstesBildAsync();
                    }
                    StadtFlaeche.Mitschnitt = null;
                    fl.FingerAb(4731, links);
                    Werte("Tablet Stadt Stick", bilder);
                }
                finally
                {
                    fenster.Width = bv;
                    fenster.Height = hv;
                    await ansicht.BildFertigAsync();
                }
            }
        }
        finally
        {
            StadtFlaeche.Mitschnitt = null;
            seite.SpielSetzen(vorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }
}
