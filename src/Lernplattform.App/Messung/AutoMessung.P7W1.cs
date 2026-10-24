using System.Diagnostics;
using System.Globalization;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 7, W1 (3D-Stichprobe, Plan Abschnitt 4, Grenzen Nico 24.10.2026 Frage 3a):
/// Pflicht-Messpunkt "GL-Zusammenhang erzeugt" (fehlt er im normalen Lauf, ist der Lauf rot,
/// sonst waeren die Bildzeiten ein falsches Gruen ueber den Ersatzweg), Stadt oeffnen bis
/// erstes Bild (einmalig, streng), Wechsel Stadt -> Innenraum und zurueck (wiederholbar),
/// Zeit je Bild beim Kameraschwenk (Median und 95 %, 120 Bilder), Weg suchen. Dazu die
/// Ansichten der Seite fuer die Layoutpruefung und der eigene Lauf ohne OpenGL
/// (<see cref="TeilErsatz"/>). Nichts wird gespeichert.
/// </summary>
public static partial class AutoMessung
{
    public const double SpielBildMedian = 50;
    public const double SpielBild95 = 100;
    public const double SpielWegGrenze = 50;
    public const int SpielBilder = 120;

    /// <summary>Wert von LERNPLATTFORM_MESSUNG_TEIL fuer den kurzen Lauf ohne OpenGL (mit --software).</summary>
    public const string TeilErsatz = "spiel-ersatz";

    /// <summary>Anzahl der Spiel-Ansichten je Groesse und Stufe (fuer den Bericht).</summary>
    public const int P7W1AnsichtenAnzahl = 5;

    /// <summary>Ansichten, die nicht aufgebaut werden konnten (zaehlen wie Layoutfunde).</summary>
    private static readonly List<string> P7W1AnsichtFehler = [];

    private static async Task<SpielSeite?> SpielSeiteAsync(MainView ansicht)
    {
        await ansicht.ZeigeAsync(Seiten.Spiel);
        return ansicht.GebauteSeiten.TryGetValue(Seiten.Spiel, out var s) ? s as SpielSeite : null;
    }

    private static async Task<bool> GlAbwartenAsync(SpielSeite seite)
    {
        var fertig = await Task.WhenAny(seite.GlEntschieden, Task.Delay(SpielSeite.GlWarten + TimeSpan.FromSeconds(3)));
        return fertig == seite.GlEntschieden && seite.GlEntschieden.Result;
    }

    private static async Task<int> P7W1MesspunkteAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        var seite = await SpielSeiteAsync(ansicht);
        if (seite is null)
        {
            Messwerte.Notiere("  FEHLER: Spiel Seite: nicht gebaut");
            return 1;
        }
        if (!await GlAbwartenAsync(seite))
        {
            Messwerte.Notiere("Spiel: GL-Zusammenhang erzeugt: nein");
            Messwerte.Notiere("  FEHLER: Spiel GL-Zusammenhang: fehlt (" + (seite.Flaeche.GlFehler ?? "OpenGL nicht verfuegbar, Ersatz Ortsliste") + ")");
            return 1;
        }
        Messwerte.Notiere("Spiel: GL-Zusammenhang erzeugt: " + seite.Flaeche.GlText);
        var funde = 0;
        // Stadt oeffnen: vom Anhaengen der Seite bis zum ersten 3D-Bild (einmalig, streng)
        for (var i = 0; i < 60 && seite.OeffnenMs is null; i++)
        {
            await ansicht.BildFertigAsync();
        }
        funde += Ergebnis("Spiel Stadt oeffnen bis erstes Bild", Messwiederholung.Einmal(seite.OeffnenMs, Abbruch));

        // Wechsel Stadt -> Innenraum und zurueck (wiederholbar)
        var spiel = seite.Spiel;
        var buero = spiel.Welt.Haeuser.First(h => h.HatInnenraum);
        spiel.GeheZu(buero);
        spiel.SofortAnkommen();
        spiel.Warten(buero.MinutenBisOffen(spiel.Minute));
        if (!spiel.KannBetreten)
        {
            Messwerte.Notiere("  FEHLER: Spiel Wechsel: Buero nicht betretbar (" + spiel.Zeile + ")");
            return funde + 1;
        }
        await seite.Flaeche.NaechstesBildAsync();
        async Task<double?> Wechsel(Action schritt)
        {
            var uhr = Stopwatch.StartNew();
            schritt();
            await seite.Flaeche.NaechstesBildAsync();
            await ansicht.BildFertigAsync();
            return uhr.Elapsed.TotalMilliseconds;
        }
        async Task Hinein() { spiel.Betreten(); await seite.Flaeche.NaechstesBildAsync(); }
        async Task Hinaus() { spiel.Verlassen(); await seite.Flaeche.NaechstesBildAsync(); }
        var rein = await Wechsel(() => spiel.Betreten());
        funde += Ergebnis("Spiel Stadt -> Innenraum", await Messwiederholung.MessenAsync(rein, Abbruch,
            () => Wechsel(() => spiel.Betreten()), Hinaus, Task.Delay));
        var raus = await Wechsel(() => spiel.Verlassen());
        funde += Ergebnis("Spiel Innenraum -> Stadt", await Messwiederholung.MessenAsync(raus, Abbruch,
            () => Wechsel(() => spiel.Verlassen()), Hinein, Task.Delay));
        // die Wechsel muessen wirklich gewechselt haben (sonst haette die Messung nur die Stadt gezeichnet)
        var verlassenOk = !spiel.ImInnenraum;
        var betretenOk = spiel.Betreten() && spiel.ImInnenraum;
        spiel.Verlassen();
        if (!verlassenOk || !betretenOk || spiel.ImInnenraum)
        {
            Messwerte.Notiere("  FEHLER: Spiel Wechsel: Betreten oder Verlassen gelingt nicht");
            funde++;
        }

        // Zeit je Bild beim Kameraschwenk: Median <= 50 ms, 95 % <= 100 ms (bis zu 3 Laeufe, rot nur, wenn alle rot)
        var laeufe = new List<(double Median, double P95)>();
        for (var lauf = 0; lauf < Messwiederholung.Hoechstens; lauf++)
        {
            var (median, p95) = await SchwenkAsync(seite);
            laeufe.Add((median, p95));
            if (median <= SpielBildMedian && p95 <= SpielBild95)
            {
                break;
            }
            await Task.Delay(Messwiederholung.Pause);
        }
        var text = string.Join("; ", laeufe.Select(l => string.Create(inv, $"Median {l.Median:F1} ms, 95 % {l.P95:F1} ms")));
        Messwerte.Notiere("Spiel: Zeit je Bild beim Kameraschwenk (" + SpielBilder + " Bilder): " + text);
        if (laeufe.All(l => l.Median > SpielBildMedian || l.P95 > SpielBild95))
        {
            Messwerte.Notiere(string.Create(inv, $"  FEHLER: Spiel Zeit je Bild: {text} (Grenze Median {SpielBildMedian:F0} ms, 95 % {SpielBild95:F0} ms; Einzelmessungen {laeufe.Count} Laeufe)"));
            funde++;
        }

        // Weg suchen: von der Haustuer "Zuhause" zum Bahnhof (Rechnung ohne Bild)
        var start = spiel.Welt.Haeuser[0];
        var ziel = spiel.Welt.Haeuser[^1];
        var wege = spiel.Welt.StadtFlaeche.Wege;
        double Suche()
        {
            var uhr = Stopwatch.StartNew();
            var weg = wege.Weg((start.TuerX, start.TuerZ), (ziel.TuerX, ziel.TuerZ));
            var ms = uhr.Elapsed.TotalMilliseconds;
            return weg.Count == 0 ? double.NaN : ms;
        }
        funde += Ergebnis("Spiel Weg suchen", await RechnungMessenAsync(Suche, SpielWegGrenze));
        Messwerte.Notiere(string.Create(inv, $"Spiel: Bilder/s nach dem Schwenk {seite.Flaeche.BilderJeSekunde}, Bilder gesamt {seite.Flaeche.BilderGesamt}"));
        return funde;
    }

    private static async Task<(double Median, double P95)> SchwenkAsync(SpielSeite seite)
    {
        var flaeche = seite.Flaeche;
        var zeiten = new List<double>();
        var uhr = Stopwatch.StartNew();
        var letztes = -1.0;
        var fertig = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var schritt = 0;
        void Bild()
        {
            var jetzt = uhr.Elapsed.TotalMilliseconds;
            if (letztes >= 0)
            {
                zeiten.Add(jetzt - letztes);
            }
            letztes = jetzt;
            if (zeiten.Count >= SpielBilder)
            {
                flaeche.Dauerlauf = false;
                fertig.TrySetResult();
            }
        }
        flaeche.VorBild = () =>
        {
            // hin und her schwenken, damit die Ansicht in der Stadt bleibt
            var richtung = (schritt++ / 30 % 2 == 0) ? 1f : -1f;
            flaeche.AktuelleKamera.Verschieben(6f * richtung, 0f, (float)Math.Max(1, flaeche.Bounds.Height));
        };
        flaeche.BildGezeichnet += Bild;
        flaeche.Dauerlauf = true;
        flaeche.AnfordernBild();
        var ende = await Task.WhenAny(fertig.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        flaeche.BildGezeichnet -= Bild;
        flaeche.VorBild = null;
        flaeche.Dauerlauf = false;
        if (ende != fertig.Task || zeiten.Count == 0)
        {
            return (double.PositiveInfinity, double.PositiveInfinity);
        }
        var sortiert = zeiten.OrderBy(z => z).ToList();
        double Stelle(double anteil) => sortiert[Math.Min(sortiert.Count - 1, (int)Math.Ceiling(anteil * sortiert.Count) - 1)];
        var median = sortiert.Count % 2 == 1 ? sortiert[sortiert.Count / 2] : (sortiert[(sortiert.Count / 2) - 1] + sortiert[sortiert.Count / 2]) / 2;
        return (median, Stelle(0.95));
    }

    /// <summary>Ansichten der Spiel-Seite fuer die Layoutpruefung: Stadt, Ortsliste, geschlossenes Haus, Buero, ohne 3D.</summary>
    private static async IAsyncEnumerable<string> P7W1Ansichten(MainView ansicht)
    {
        var seite = await SpielSeiteAsync(ansicht);
        if (seite is null)
        {
            yield break;
        }
        var spiel = seite.Spiel;
        await ansicht.BildFertigAsync();
        yield return "spiel/stadt";
        seite.OrtslisteZeigen(true);
        await ansicht.BildFertigAsync();
        yield return "spiel/orte";
        seite.OrtslisteZeigen(false);
        var bahnhof = spiel.Welt.Haeuser[^1];
        spiel.GeheZu(bahnhof);
        spiel.SofortAnkommen();
        await ansicht.BildFertigAsync();
        yield return "spiel/haus";
        var buero = spiel.Welt.Haeuser.First(h => h.HatInnenraum);
        spiel.GeheZu(buero);
        spiel.SofortAnkommen();
        spiel.Warten(buero.MinutenBisOffen(spiel.Minute));
        if (!spiel.Betreten())
        {
            // die Layoutpruefung darf unter "spiel/buero" nie still die Stadt pruefen (zaehlt als Layoutfund)
            P7W1AnsichtFehler.Add("spiel/buero: Buero nicht betretbar");
            yield break;
        }
        await ansicht.BildFertigAsync();
        yield return "spiel/buero";
        spiel.Verlassen();
        seite.ErsatzVorfuehren(true);
        await ansicht.BildFertigAsync();
        yield return "spiel/ohne-3d";
        seite.ErsatzVorfuehren(false);
        await ansicht.BildFertigAsync();
    }

    /// <summary>
    /// Eigener kurzer Lauf ohne OpenGL (Schalter --software bzw. LERNPLATTFORM_SOFTWARE=1, nur Windows
    /// und Linux): die Seite muss den Hinweis und die Ortsliste zeigen, kein Absturz, Layout ohne Funde.
    /// </summary>
    public static async Task<int> ErsatzLaufAsync(MainView ansicht)
    {
        var funde = 0;
        var seite = await SpielSeiteAsync(ansicht);
        if (seite is null)
        {
            Messwerte.Notiere("  FEHLER: Spiel ohne OpenGL: Seite nicht gebaut");
            return 1;
        }
        var gl = await GlAbwartenAsync(seite);
        await ansicht.BildFertigAsync();
        Messwerte.Notiere("Spiel ohne OpenGL: GL-Zusammenhang " + (gl ? "erzeugt (" + seite.Flaeche.GlText + ")" : "nicht erzeugt (erwartet)"));
        if (gl)
        {
            Messwerte.Notiere("  FEHLER: Spiel ohne OpenGL: Zeichnen in Software liefert trotzdem OpenGL, der Ersatzweg ist so nicht pruefbar");
            funde++;
        }
        var ok = seite.ErsatzSichtbar && seite.OrtslisteSichtbar;
        Messwerte.Notiere("Spiel ohne OpenGL: Hinweis und Ortsliste " + (ok ? "sichtbar" : "FEHLEN"));
        if (!ok)
        {
            Messwerte.Notiere("  FEHLER: Spiel ohne OpenGL: Hinweis oder Ortsliste fehlt");
            funde++;
        }
        // ohne 3D sofort ankommen, die Wegzeit zaehlt trotzdem
        var vorher = seite.Spiel.Minute;
        seite.Spiel.GeheZu(seite.Spiel.Welt.Haeuser[^1]);
        seite.Spiel.SofortAnkommen();
        var weiter = seite.Spiel.Minute > vorher;
        Messwerte.Notiere("Spiel ohne OpenGL: Weg ueber die Ortsliste " + (weiter ? "kostet Spielzeit" : "FEHLER, keine Spielzeit"));
        funde += weiter ? 0 : 1;
        await ansicht.BildFertigAsync();
        var layout = Layoutpruefung.Pruefe(ansicht);
        foreach (var fund in layout.Take(20))
        {
            Messwerte.Notiere("  spiel/ohne-3d: " + fund);
        }
        funde += layout.Count;
        Messwerte.Notiere(funde == 0 ? "URTEIL: gruen" : string.Create(CultureInfo.InvariantCulture, $"URTEIL: rot ({funde} Funde)"));
        return funde;
    }
}
