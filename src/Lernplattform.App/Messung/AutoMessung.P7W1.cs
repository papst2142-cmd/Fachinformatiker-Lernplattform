using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 7, W1 (3D-Stichprobe, Plan Abschnitt 4, Grenzen Nico 24.10.2026 Frage 3a), ab W4 die
/// ganze Stadt (Weisung P7-9): dazu Vollbild beim Betreten und "Spiel verlassen", Gesten erreichen
/// die 3D-Flaeche (keine ScrollView darum, die Ebene darueber laesst die Mitte frei), Tippen auf ein
/// Haus geht hin, Tasten bewegen die Kamera.
/// Pflicht-Messpunkt "GL-Zusammenhang erzeugt" auf Windows und Linux (fehlt er im normalen Lauf, ist der Lauf rot;
/// macOS ohne GL zeigt die Ortsliste und ist gruen, Weisung P7-9 Nachtrag 2;
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

    /// <summary>Nachtrag 1 zu P7-12 (Wunsch 4): Grenzen der Gesamtansicht (ganz herausgezoomt), unabhaengig vom Grafiktreiber.</summary>
    public const long GesamtDreieckeGrenze = 100_000;

    public const int GesamtAufrufeGrenze = 20;

    /// <summary>Wert von LERNPLATTFORM_MESSUNG_TEIL fuer den kurzen Lauf ohne OpenGL (mit --software).</summary>
    public const string TeilErsatz = "spiel-ersatz";

    /// <summary>Anzahl der Spiel-Ansichten je Groesse und Stufe (fuer den Bericht).</summary>
    public const int P7W1AnsichtenAnzahl = 7;

    /// <summary>Ansichten, die nicht aufgebaut werden konnten (zaehlen wie Layoutfunde).</summary>
    private static readonly List<string> P7W1AnsichtFehler = [];

    private static async Task<SpielSeite?> SpielSeiteAsync(MainView ansicht)
    {
        await ansicht.ZeigeAsync(Seiten.Spiel);
        if (!ansicht.GebauteSeiten.TryGetValue(Seiten.Spiel, out var s) || s is not SpielSeite seite)
        {
            return null;
        }
        // Inhalte laden im Hintergrund; ohne Spiel gibt es nichts zu pruefen (zaehlt als Fehler)
        var fertig = await Task.WhenAny(seite.Geladen, Task.Delay(TimeSpan.FromSeconds(30)));
        return fertig == seite.Geladen && seite.Geladen.IsCompletedSuccessfully ? seite : null;
    }

    /// <summary>Frischer Spielstand je Durchgang (sonst waere es nach vielen Wegen Abend).</summary>
    private static StadtSpiel Frisch(SpielSeite seite)
    {
        var alt = seite.Spiel!;
        var neu = SpielStart.Neu(alt.Stadt, alt.Grafik, alt.Innenraeume, alt.GeschaefteDaten, alt.Feste, Core.Spiel.Festzeit.Heute);
        seite.SpielSetzen(neu);
        return neu;
    }

    /// <summary>Zur Bitweiche gehen und (falls noetig) bis zur Oeffnung warten; true, wenn man hinein kann.</summary>
    private static bool ZurBitweiche(StadtSpiel spiel)
    {
        spiel.GeheZu("bitweiche");
        spiel.SofortAnkommen();
        if (!spiel.KannBetreten && spiel.KannWarten)
        {
            spiel.Warten();
        }
        return spiel.KannBetreten;
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
            Messwerte.Notiere("  FEHLER: Spiel Seite: nicht gebaut oder Inhalte nicht geladen");
            return 1;
        }
        // CC0-Modelle muessen geladen sein (sonst zeichnete die Stadt still eigene Geometrie)
        var grafik = seite.Spiel?.Grafik;
        Messwerte.Notiere("Spiel: CC0-Grafik: " + (grafik is null ? "nicht geladen (" + SpielStart.GrafikFehler + ")"
            : grafik.Modelle.Count.ToString(CultureInfo.InvariantCulture) + " Modelle, " + seite.Spiel!.Szene.Stuecke.Count.ToString(CultureInfo.InvariantCulture) + " in der Stadt"));
        if (grafik is null || seite.Spiel!.Szene.Stuecke.Count == 0)
        {
            Messwerte.Notiere("  FEHLER: Spiel CC0-Grafik: nicht geladen oder nicht in der Stadt");
            return 1;
        }
        if (!await GlAbwartenAsync(seite))
        {
            if (OperatingSystem.IsMacOS())
            {
                // Weisung P7-9 Nachtrag 2 (Nico 25.10.2026): macOS ohne GL = ohne 3D, Ortsliste sichtbar, kein
                // Fehler; die App versucht dort weiter 3D (kein Sonderweg). Rot nur, wenn der Ersatz fehlt.
                await ansicht.BildFertigAsync();
                var ersatz = seite.ErsatzSichtbar && seite.OrtslisteSichtbar;
                Messwerte.Notiere("Spiel: GL-Zusammenhang erzeugt: nein (macOS: ohne 3D, Ortsliste " + (ersatz ? "sichtbar" : "FEHLT") + ")");
                if (!ersatz)
                {
                    Messwerte.Notiere("  FEHLER: Spiel macOS ohne GL: Hinweis und Ortsliste nicht sichtbar");
                    return 1;
                }
                return 0;
            }
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

        funde += await P7W4BedienungAsync(ansicht, seite);

        // Wechsel Stadt -> Innenraum und zurueck (wiederholbar)
        var spiel = Frisch(seite);
        if (!ZurBitweiche(spiel))
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

        funde += await P7W5Async(ansicht, seite);

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

        funde += await GesamtansichtAsync(seite);

        // Weg suchen: von der Haustuer "Zuhause" zur Filiale am anderen Ende der Stadt (Rechnung ohne Bild)
        double Suche()
        {
            var uhr = Stopwatch.StartNew();
            var weg = spiel.Stadt.Weg("zuhause", "filiale");
            var ms = uhr.Elapsed.TotalMilliseconds;
            return weg is null ? double.NaN : ms;
        }
        funde += Ergebnis("Spiel Weg suchen", await RechnungMessenAsync(Suche, SpielWegGrenze));
        Messwerte.Notiere(string.Create(inv, $"Spiel: Bilder/s nach dem Schwenk {seite.Flaeche.BilderJeSekunde}, Bilder gesamt {seite.Flaeche.BilderGesamt}"));
        return funde;
    }

    /// <summary>
    /// W5 (Weisung P7-10): Wechsel Stadt -> Wohnung und Stadt -> Kunde (wiederholbar, 400 ms),
    /// "Zur Arbeit" fuehrt ins Buero, Einrichten waehlt ein Moebel, ein Haus vor der Figur wird
    /// halb durchsichtig gezeichnet (Sammelliste 102). Jede Pruefung, die nicht gelingt, ist ein Fund.
    /// </summary>
    private static async Task<int> P7W5Async(MainView ansicht, SpielSeite seite)
    {
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel " + punkt + ": " + wert);
                funde++;
            }
        }
        async Task<double?> Wechsel(Action schritt)
        {
            var uhr = Stopwatch.StartNew();
            schritt();
            await seite.Flaeche.NaechstesBildAsync();
            await ansicht.BildFertigAsync();
            return uhr.Elapsed.TotalMilliseconds;
        }

        // Stadt -> Wohnung (das neue Spiel steht vor der Wohnung)
        var spiel = Frisch(seite);
        await seite.Flaeche.NaechstesBildAsync();
        if (!spiel.KannBetreten)
        {
            Pruefe("Wohnung betretbar", false, spiel.Zeile);
            return funde;
        }
        async Task Hinaus() { spiel.Verlassen(); await seite.Flaeche.NaechstesBildAsync(); }
        var rein = await Wechsel(() => spiel.Betreten());
        var wohnungOk = spiel.FlaecheId == "zuhause";
        funde += Ergebnis("Spiel Stadt -> Wohnung", await Messwiederholung.MessenAsync(rein, Abbruch,
            () => Wechsel(() => spiel.Betreten()), Hinaus, Task.Delay));
        if (!spiel.ImInnenraum)
        {
            spiel.Betreten();
        }
        Pruefe("Wohnung betreten", wohnungOk && spiel.FlaecheId == "zuhause", "Flaeche " + spiel.FlaecheId);

        // Einrichten: Modus an, die Startmatratze antippen waehlt sie, "Fertig" beendet den Modus
        spiel.EinrichtenUmschalten();
        spiel.GeheZu(1.0, 1.0);
        await seite.Flaeche.NaechstesBildAsync();
        await ansicht.BildFertigAsync();
        var gewaehlt = spiel.Einrichten && spiel.Ausgewaehlt == "start-matratze" && spiel.AusgewaehltSteht;
        var name = spiel.Ausgewaehlt ?? "nichts gewaehlt";
        spiel.EinrichtenUmschalten();
        Pruefe("Einrichten waehlt ein Moebel", gewaehlt && !spiel.Einrichten, name);

        // "Zur Arbeit": hinaus, zum Buero gehen und gleich hinein
        var arbeit = spiel.Knopf("buero");
        spiel.SofortAnkommen();
        await seite.Flaeche.NaechstesBildAsync();
        Pruefe("Zur Arbeit fuehrt ins Buero", arbeit && spiel.FlaecheId == "buero", "Flaeche " + spiel.FlaecheId + " (" + spiel.Zeile.Replace('\n', ' ') + ")");
        spiel.Verlassen();

        // Stadt -> Kunde (Talheim, ein Kundenort mit Innenraum)
        spiel.GeheZu("talheim");
        spiel.SofortAnkommen();
        if (!spiel.KannBetreten && spiel.KannWarten)
        {
            spiel.Warten();
        }
        if (!spiel.KannBetreten)
        {
            Pruefe("Kunde betretbar", false, spiel.Zeile);
        }
        else
        {
            await seite.Flaeche.NaechstesBildAsync();
            var kunde = await Wechsel(() => spiel.Betreten());
            funde += Ergebnis("Spiel Stadt -> Kunde", await Messwiederholung.MessenAsync(kunde, Abbruch,
                () => Wechsel(() => spiel.Betreten()), Hinaus, Task.Delay));
            if (!spiel.ImInnenraum)
            {
                spiel.Betreten();
            }
            Pruefe("Kunde betreten", spiel.FlaecheId.StartsWith("kunde", StringComparison.Ordinal), "Flaeche " + spiel.FlaecheId);
            spiel.Verlassen();
        }

        // Haus vor der Figur: halb durchsichtig. Figur neben die Bitweiche, Kamera in jede Richtung drehen,
        // bis das Haus zwischen Auge und Figur steht; die Zeichnung muss es dann durchsichtig zeichnen
        spiel = Frisch(seite);
        var kamera = seite.Flaeche.AktuelleKamera!;
        var haus = spiel.Szene.Haeuser.First(h => h.Id == "bitweiche");
        var mitte = (haus.Min + haus.Max) / 2;
        var gezeichnet = 0;
        var gesehen = false;
        foreach (var (px, pz) in new[] { (mitte.X, haus.Min.Z - 0.8f), (mitte.X, haus.Max.Z + 0.8f), (haus.Min.X - 0.8f, mitte.Z), (haus.Max.X + 0.8f, mitte.Z) })
        {
            if (gesehen)
            {
                break;
            }
            spiel.GeheZu(px, pz);
            spiel.SofortAnkommen();
            foreach (var drehung in new[] { 0f, 90f, 180f, 270f })
            {
                kamera.Drehung = drehung;
                kamera.Setzen(spiel.FigurX, spiel.FigurZ);
                if (!spiel.Szene.Verdeckt(kamera.Auge, spiel.FigurX, spiel.FigurZ, Figur.StadtHoehe).Contains(haus))
                {
                    continue;
                }
                gesehen = true;
                await seite.Flaeche.NaechstesBildAsync();
                await seite.Flaeche.NaechstesBildAsync();
                gezeichnet = seite.Flaeche.VerdecktZahl;
                break;
            }
        }
        kamera.Drehung = 0;
        Pruefe("Haus vor der Figur durchsichtig", gesehen && gezeichnet > 0, gesehen
            ? gezeichnet.ToString(CultureInfo.InvariantCulture) + " Haus/Haeuser durchsichtig gezeichnet"
            : "keine Stelle gefunden, an der die Bitweiche die Figur verdeckt");
        Frisch(seite);
        await seite.Flaeche.NaechstesBildAsync();
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
            flaeche.AktuelleKamera?.Verschieben(6f * richtung, 0f, (float)Math.Max(1, flaeche.Bounds.Height));
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

    /// <summary>
    /// Nachtrag 1 zu P7-12 (Wunsch 4, S24 55 Bilder/s in der Uebersicht): ganz herausgezoomt auf die
    /// Stadtmitte. Messpunkte, die nicht vom Grafiktreiber abhaengen: Zeichenaufrufe und Dreiecke je Bild
    /// (Grenzen <see cref="GesamtAufrufeGrenze"/>, <see cref="GesamtDreieckeGrenze"/>); dazu Zeit je Bild beim
    /// Schwenk in dieser Ansicht (Grenzen wie beim Schwenk, bis zu 3 Laeufe) und Rechenzeit je Bild.
    /// </summary>
    private static async Task<int> GesamtansichtAsync(SpielSeite seite)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        var flaeche = seite.Flaeche;
        if (flaeche.AktuelleKamera is not { } kamera || seite.Spiel is not { } spiel)
        {
            Messwerte.Notiere("  FEHLER: Spiel Gesamtansicht: keine Kamera");
            return 1;
        }
        var abstand = kamera.Abstand;
        kamera.Zoomen(kamera.Abstand / Kamera.AbstandMax);
        kamera.Setzen(spiel.Szene.Breite / 2, spiel.Szene.Tiefe / 2);
        await flaeche.NaechstesBildAsync();
        await flaeche.NaechstesBildAsync();
        var aufrufe = flaeche.LetzteZeichenaufrufe;
        var dreiecke = flaeche.LetzteDreiecke;
        Messwerte.Notiere(string.Create(inv, $"Spiel: Gesamtansicht (Abstand {kamera.Abstand:F0}): {aufrufe} Zeichenaufrufe, {dreiecke} Dreiecke je Bild, Rechenzeit {flaeche.LetzteZeichenzeitMs:F1} ms (Grenzen {GesamtAufrufeGrenze} Aufrufe, {GesamtDreieckeGrenze} Dreiecke)"));
        if (aufrufe > GesamtAufrufeGrenze || dreiecke > GesamtDreieckeGrenze || aufrufe == 0)
        {
            Messwerte.Notiere(string.Create(inv, $"  FEHLER: Spiel Gesamtansicht: {aufrufe} Zeichenaufrufe, {dreiecke} Dreiecke (Grenzen {GesamtAufrufeGrenze}, {GesamtDreieckeGrenze})"));
            funde++;
        }
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
        Messwerte.Notiere("Spiel: Zeit je Bild in der Gesamtansicht (" + SpielBilder + " Bilder): " + text);
        if (laeufe.All(l => l.Median > SpielBildMedian || l.P95 > SpielBild95))
        {
            Messwerte.Notiere(string.Create(inv, $"  FEHLER: Spiel Zeit je Bild Gesamtansicht: {text} (Grenze Median {SpielBildMedian:F0} ms, 95 % {SpielBild95:F0} ms; Einzelmessungen {laeufe.Count} Laeufe)"));
            funde++;
        }
        kamera.Zoomen(kamera.Abstand / abstand);
        kamera.Setzen(spiel.FigurX, spiel.FigurZ);
        return funde;
    }

    /// <summary>
    /// Jeder sichtbare Knopf der Ebene ueber der Stadt liegt ganz auf der Seite ("Spiel verlassen",
    /// Hineingehen, Warten, Orte); sonst ist er am Handy quer nicht erreichbar. Knoepfe in einer
    /// Rolle (Ortsliste) zaehlen nicht, die erreicht man durch Rollen.
    /// </summary>
    private static List<string> KnoepfeImBild(SpielSeite seite, string ansichtName)
    {
        var fehler = new List<string>();
        var seiteRahmen = new Rect(seite.Bounds.Size).Inflate(0.5);
        foreach (var knopf in seite.Ebene.GetVisualDescendants().OfType<Button>())
        {
            if (!knopf.IsEffectivelyVisible || knopf.GetVisualAncestors().OfType<ScrollViewer>().Any())
            {
                continue;
            }
            var lage = knopf.TranslatePoint(default, seite);
            if (lage is not { } p || !seiteRahmen.Contains(new Rect(p, knopf.Bounds.Size)))
            {
                fehler.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{ansichtName}: Knopf \"{(knopf.Content as TextBlock)?.Text ?? knopf.Content?.ToString()}\" nicht ganz im Bild ({lage?.X:F0},{lage?.Y:F0} {knopf.Bounds.Width:F0}x{knopf.Bounds.Height:F0}, Seite {seite.Bounds.Width:F0}x{seite.Bounds.Height:F0})"));
            }
        }
        return fehler;
    }

    /// <summary>Ansichten der Spiel-Seite fuer die Layoutpruefung: Stadt, Ortsliste, geschlossenes Haus, Buero, Kunde, Einrichten, ohne 3D.</summary>
    private static async IAsyncEnumerable<string> P7W1Ansichten(MainView ansicht)
    {
        var seite = await SpielSeiteAsync(ansicht);
        if (seite is null)
        {
            P7W1AnsichtFehler.Add("spiel: Seite nicht gebaut oder Inhalte nicht geladen");
            yield break;
        }
        var spiel = Frisch(seite);
        await ansicht.BildFertigAsync();
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/stadt"));
        yield return "spiel/stadt";
        seite.OrtslisteZeigen(true);
        await ansicht.BildFertigAsync();
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/orte"));
        yield return "spiel/orte";
        seite.OrtslisteZeigen(false);
        // Haus mit Oeffnungszeit, das bei der Ankunft noch zu hat (Moebelhaus oeffnet um 10:00): Hinweis und "Warten"
        spiel.GeheZu("moebelhaus");
        spiel.SofortAnkommen();
        seite.Flaeche.KameraAufFigur();
        if (!spiel.KannWarten)
        {
            P7W1AnsichtFehler.Add("spiel/haus: Moebelhaus ist bei der Ankunft nicht geschlossen (" + spiel.Zeile + ")");
        }
        await ansicht.BildFertigAsync();
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/haus"));
        yield return "spiel/haus";
        if (!ZurBitweiche(spiel) || !spiel.Betreten())
        {
            // die Layoutpruefung darf unter "spiel/buero" nie still die Stadt pruefen (zaehlt als Layoutfund)
            P7W1AnsichtFehler.Add("spiel/buero: Buero nicht betretbar");
            yield break;
        }
        await ansicht.BildFertigAsync();
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/buero"));
        yield return "spiel/buero";
        spiel.Verlassen();
        // W5: Kunde (Talheim) und die Wohnung im Modus "Einrichten" mit gewaehltem Moebel und Tafel
        spiel.GeheZu("talheim");
        spiel.SofortAnkommen();
        if (!spiel.KannBetreten && spiel.KannWarten)
        {
            spiel.Warten();
        }
        if (!spiel.Betreten())
        {
            P7W1AnsichtFehler.Add("spiel/kunde: Kunde nicht betretbar (" + spiel.Zeile + ")");
            yield break;
        }
        await ansicht.BildFertigAsync();
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/kunde"));
        yield return "spiel/kunde";
        spiel.Verlassen();
        spiel = Frisch(seite);
        if (!spiel.Betreten())
        {
            P7W1AnsichtFehler.Add("spiel/einrichten: Wohnung nicht betretbar (" + spiel.Zeile + ")");
            yield break;
        }
        spiel.EinrichtenUmschalten();
        spiel.Waehlen("start-matratze");
        await ansicht.BildFertigAsync();
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/einrichten"));
        yield return "spiel/einrichten";
        spiel.EinrichtenUmschalten();
        spiel.Verlassen();
        seite.ErsatzVorfuehren(true);
        await ansicht.BildFertigAsync();
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/ohne-3d"));
        yield return "spiel/ohne-3d";
        seite.ErsatzVorfuehren(false);
        await ansicht.BildFertigAsync();
    }

    /// <summary>
    /// W4 (Weisung P7-9 Punkt 2 und 3a): Vollbild, Gesten, Tippen, Tasten, "Spiel verlassen".
    /// Jede Pruefung, die nicht gelingt, ist ein Fund (rot).
    /// </summary>
    private static async Task<int> P7W4BedienungAsync(MainView ansicht, SpielSeite seite)
    {
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel " + punkt + ": " + wert);
                funde++;
            }
        }
        var spiel = Frisch(seite);
        await seite.Flaeche.NaechstesBildAsync();
        await ansicht.BildFertigAsync();

        // Vollbild: die Seite fuellt das ganze Fenster, Kopfleiste und Navigation sind weg
        var top = TopLevel.GetTopLevel(ansicht);
        // Nachtrag 1 zu P7-13: der sichere Rand wird am Inhalt des TopLevel abgeschaltet (sonst schwarzer Streifen)
        var voll = ansicht.Vollbild && Vollbild.An && top is not null && top.Content is Control inhalt && !TopLevel.GetAutoSafeAreaPadding(inhalt)
            && Math.Abs(seite.Bounds.Width - ansicht.Bounds.Width) < 1 && Math.Abs(seite.Bounds.Height - ansicht.Bounds.Height) < 1;
        Pruefe("Vollbild", voll, string.Create(CultureInfo.InvariantCulture,
            $"{(voll ? "ja" : "nein")} (Seite {seite.Bounds.Width:F0} x {seite.Bounds.Height:F0}, Fenster {ansicht.Bounds.Width:F0} x {ansicht.Bounds.Height:F0})"));

        // Gesten: keine ScrollView um die 3D-Flaeche, und ein Finger in der Mitte trifft die Flaeche selbst
        var scroll = seite.Flaeche.GetVisualAncestors().OfType<ScrollViewer>().Count();
        var mitte = seite.Flaeche.TranslatePoint(new Point(seite.Flaeche.Bounds.Width / 2, seite.Flaeche.Bounds.Height / 2), ansicht);
        var getroffen = mitte is { } m ? ansicht.InputHitTest(m) as Visual : null;
        var trifft = getroffen is not null && (getroffen == seite.Flaeche || getroffen.GetVisualAncestors().Contains(seite.Flaeche));
        Pruefe("Gesten erreichen die 3D-Flaeche", scroll == 0 && trifft,
            "ScrollView darum " + scroll + ", Mitte trifft " + (getroffen?.GetType().Name ?? "nichts"));

        // Tippen auf ein Haus geht hin (Bitweiche: Mitte ihres Kastens auf den Bildschirm gerechnet)
        var kamera = seite.Flaeche.AktuelleKamera!;
        var ort = spiel.Szene.Ort("bitweiche")!;
        kamera.Setzen((ort.KastenMin.X + ort.KastenMax.X) / 2, (ort.KastenMin.Z + ort.KastenMax.Z) / 2);
        await seite.Flaeche.NaechstesBildAsync();
        var punkt = kamera.Bildpunkt((ort.KastenMin + ort.KastenMax) / 2, (float)seite.Flaeche.Bounds.Width, (float)seite.Flaeche.Bounds.Height);
        if (punkt is { } p)
        {
            seite.Flaeche.Tippen(new Point(p.X, p.Y));
        }
        Pruefe("Tippen auf ein Haus geht hin", spiel.Jetzt.Ort == "bitweiche", "Ziel " + spiel.Jetzt.Ort + " (" + spiel.Zeile + ")");
        spiel.SofortAnkommen();

        // P7-10 Nachtrag 1 (Wunsch 3): Tippen auf einen freien Platz (Park am Fluss) geht dorthin und kostet Spielzeit
        var frei = new System.Numerics.Vector3(4f, 0f, 19.5f);
        kamera.Setzen(frei.X, frei.Z);
        await seite.Flaeche.NaechstesBildAsync();
        var uhrVorher = spiel.Jetzt.Uhr;
        if (kamera.Bildpunkt(frei, (float)seite.Flaeche.Bounds.Width, (float)seite.Flaeche.Bounds.Height) is { } fp)
        {
            seite.Flaeche.Tippen(new Point(fp.X, fp.Y));
        }
        spiel.SofortAnkommen();
        var dort = Math.Abs(spiel.FigurX - frei.X) < 0.3f && Math.Abs(spiel.FigurZ - frei.Z) < 0.3f;
        var gelaufen = spiel.ImFreien && dort && spiel.Jetzt.Uhr > uhrVorher && spiel.Ablauf.Stand.Verlauf[^1].Art == Lernplattform.Core.Spiel.Stadt.Gelaufen;
        Pruefe("Tippen auf einen freien Platz geht hin", gelaufen, string.Create(CultureInfo.InvariantCulture,
            $"Figur bei {spiel.FigurX:F2}/{spiel.FigurZ:F2}, Uhr {uhrVorher} -> {spiel.Jetzt.Uhr} ({spiel.Zeile})"));
        // Kamera zurueck zur Bitweiche (dort stehen die Namensschilder fuer die naechste Pruefung)
        kamera.Setzen((ort.KastenMin.X + ort.KastenMax.X) / 2, (ort.KastenMin.Z + ort.KastenMax.Z) / 2);

        // Tasten: Pfeil verschiebt, Q dreht, + zoomt
        float x = kamera.ZielX, d = kamera.Drehung, a = kamera.Abstand;
        var tasten = seite.Flaeche.Taste(Key.Left) & seite.Flaeche.Taste(Key.Q) & seite.Flaeche.Taste(Key.OemPlus);
        var bewegt = tasten && kamera.ZielX != x && kamera.Drehung != d && kamera.Abstand < a;
        Pruefe("Tasten bewegen die Kamera", bewegt, bewegt ? "ja" : "nein");
        kamera.Drehung = 0;

        // Schilder: mindestens ein Ortsname steht ueber der Stadt
        await seite.Flaeche.NaechstesBildAsync();
        await ansicht.BildFertigAsync();
        Pruefe("Namensschilder sichtbar", seite.SchilderSichtbar > 0, seite.SchilderSichtbar.ToString(CultureInfo.InvariantCulture));

        // Handy quer (Groesse des S24 quer, 836 x 385): alle Knoepfe ganz im Bild, auch mit dem langen
        // Hinweis beim Oeffnen und mit offener Ortsliste
        if (top is Window fenster)
        {
            var (breite, hoehe, mindest) = (fenster.Width, fenster.Height, fenster.MinHeight);
            fenster.MinHeight = 0; // das PC-Fenster ist sonst mindestens 480 hoch
            fenster.Width = 836;
            fenster.Height = 385;
            Frisch(seite);
            await seite.Flaeche.NaechstesBildAsync();
            await ansicht.BildFertigAsync();
            var quer = KnoepfeImBild(seite, "quer");
            seite.OrtslisteZeigen(true);
            await ansicht.BildFertigAsync();
            quer.AddRange(KnoepfeImBild(seite, "quer mit Ortsliste"));
            seite.OrtslisteZeigen(false);
            var groesse = string.Create(CultureInfo.InvariantCulture, $"Seite {seite.Bounds.Width:F0} x {seite.Bounds.Height:F0}");
            Pruefe("Handy quer: Knoepfe im Bild", quer.Count == 0 && seite.Bounds.Height < 400, quer.Count == 0 ? "ja (" + groesse + ")" : string.Join("; ", quer));
            fenster.Width = breite;
            fenster.Height = hoehe;
            fenster.MinHeight = mindest;
            await ansicht.BildFertigAsync();
            await ansicht.BildFertigAsync();
        }

        // "Spiel verlassen" fuehrt zurueck zur Seite davor, Kopfleiste und Navigation sind wieder da
        seite.VerlassenKnopf.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        for (var i = 0; i < 20 && ansicht.AktuelleSeite == Seiten.Spiel; i++)
        {
            await ansicht.BildFertigAsync();
        }
        var zurueck = ansicht.AktuelleSeite != Seiten.Spiel && !ansicht.Vollbild && !Vollbild.An
            && top?.Content is Control danach && TopLevel.GetAutoSafeAreaPadding(danach);
        Pruefe("Spiel verlassen", zurueck, "Seite " + ansicht.AktuelleSeite + ", Vollbild " + (ansicht.Vollbild ? "an" : "aus"));
        await ansicht.ZeigeAsync(Seiten.Spiel);
        await ansicht.BildFertigAsync();
        return funde;
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
        var spiel = seite.Spiel!;
        var vorher = spiel.Jetzt.Uhr;
        spiel.GeheZu("bitweiche");
        spiel.SofortAnkommen();
        var weiter = spiel.Jetzt.Uhr > vorher;
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
