using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// A 0.76.0, Welle 1 "Prüfung im echten Format" (Weisung P12-262): Messpunkte (Grenze 400 ms) fuer Laden der Saetze,
/// Lauf, Aufgabe wechseln, Anlagen umschalten, Bewertung und Ergebnis, dazu eine Bedienprobe mit Maus und Finger:
/// Reiter, Umschalter und ein Haekchen der Bewertung werden wie von Hand angetippt (Zeigerereignisse), ohne zu speichern.
/// Die Ansichten selbst prueft die Layoutpruefung ueber <see cref="KlausurTeil.ProbeNamen"/>.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> EchtFormatMesspunkteAsync(MainView ansicht)
    {
        var funde = 0;
        void Fund(string text)
        {
            Messwerte.Notiere("  FEHLER: Pruefung im echten Format: " + text);
            funde++;
        }
        // Laden und Ladepruefung der Saetze (einmalig, streng): keine Meldung erlaubt. A 1.5.0: wie die App selbst aus dem
        // Ordner der aktuellen Inhalte (Programm mit den vorab eingesetzten Fachrichtungs-Paketen), die eingebaute Datei allein
        // hat fuer AE/DPA/DV nur Platzhalter
        var uhr = Stopwatch.StartNew();
        var bestand = EchtSaetze.Laden(Zustand.Aktuell.Saetze(), Zustand.Aktuell.AusgeschlosseneSaetze());
        funde += Ergebnis("Pruefungssaetze im echten Format laden und pruefen", Messwiederholung.Einmal(uhr.Elapsed.TotalMilliseconds, Abbruch));
        Messwerte.Notiere("Pruefung im echten Format: " + bestand.Saetze.Count + " Saetze, " + bestand.Fehler.Count + " Meldungen der Ladepruefung");
        foreach (var fehler in bestand.Fehler)
        {
            Fund("Ladepruefung " + fehler.Kennung + ": " + fehler.Meldung);
        }
        if (bestand.Saetze.Count == 0)
        {
            Fund("kein Satz geladen");
            return funde;
        }
        if (!ansicht.GebauteSeiten.TryGetValue(Seiten.Trainer, out var gebaut) || gebaut is not TrainerSeite trainer)
        {
            Fund("Pruefungstrainer nicht gebaut");
            return funde;
        }
        await trainer.Geladen;
        await ansicht.ZeigeAsync(Seiten.Trainer);
        trainer.ArtWaehlen(TrainerTexte.Pruefung);
        await ansicht.BildFertigAsync();
        var k = trainer.Klausur!;
        var e = k.Echt;
        var ab = KlausurTeil.EchtAb + 1;
        async Task Messpunkt(string titel, Action schritt, Action? zurueck = null, bool wiederholbar = false) =>
            funde += Ergebnis(titel, await BildMessenAsync(ansicht, schritt, zurueck, wiederholbar));
        Task MesspunktW2(string titel, Action schritt, Action? zurueck, bool wiederholbar) => Messpunkt(titel, schritt, zurueck, wiederholbar);
        await Messpunkt("Pruefung im echten Format Auswahl mit Meldungen (bis zum Bild)", () => k.Probe(KlausurTeil.EchtAb), wiederholbar: true);
        await Messpunkt("Pruefung im echten Format Lauf zeigen, Aufgabe 1 (bis zum Bild)", () => k.Probe(ab + 1), wiederholbar: true);
        await Messpunkt("Pruefung im echten Format Aufgabe wechseln (bis zum Bild)", () => e.ProbeReiter("2", false), () => e.ProbeReiter("1", false));
        await Messpunkt("Pruefung im echten Format Anlagen umschalten (bis zum Bild)", () => e.ProbeReiter("1", true), () => e.ProbeReiter("1", false));
        await Messpunkt("Pruefung im echten Format Bewertung zeigen (bis zum Bild)", () => k.Probe(ab + 7), wiederholbar: true);
        await Messpunkt("Pruefung im echten Format Bewertung Aufgabe wechseln (bis zum Bild)", () => e.ProbeReiter("2", false), () => e.ProbeReiter("1", false));
        await Messpunkt("Pruefung im echten Format Ergebnis zeigen (bis zum Bild)", () => k.Probe(ab + 9), wiederholbar: true);

        // Bedienprobe: mit Maus und mit Finger je Reiter, Umschalter und Haekchen antippen
        foreach (var art in new[] { PointerType.Mouse, PointerType.Touch })
        {
            var name = art == PointerType.Mouse ? "Maus" : "Finger";
            k.Probe(ab + 1);
            await ansicht.BildFertigAsync();
            if (!await TippenAsync(ansicht, e.ProbeReiterPillen, "3 (24)", art) || e.ProbeStand.Reiter != "3")
            {
                Fund(name + ": Reiter \"Aufgabe 3\" antippen wechselt nicht (Reiter " + e.ProbeStand.Reiter + ")" + TrefferZuletzt);
            }
            if (!await TippenAsync(ansicht, e.ProbeUmschalter, EchtFormatTeil.UmschalterText(e.ProbeSatz!, e.ProbeSatz!.Aufgaben[2]), art)
                || !e.ProbeStand.Anlagen)
            {
                Fund(name + ": Umschalter \"Anlagen\" antippen zeigt die Anlagen nicht" + TrefferZuletzt);
            }
            k.Probe(ab + 7);
            await ansicht.BildFertigAsync();
            var vorher = e.ProbeStand.Summe;
            var haken = e.ProbeInhalt?.GetVisualDescendants().OfType<CheckBox>().FirstOrDefault(c => c.IsChecked == false);
            if (haken is null || !await TippenAsync(ansicht, haken, art) || e.ProbeStand.Summe == vorher)
            {
                Fund(name + ": Haekchen der Bewertung antippen aendert den Zwischenstand nicht (" + vorher + " / " + e.ProbeStand.Summe + ")");
            }
            Messwerte.Notiere("Pruefung im echten Format Bedienprobe " + name + ": Reiter, Anlagen, Haekchen (" + vorher + " -> " + e.ProbeStand.Summe + ")");
        }
        k.Probe(ab + 1);
        await ansicht.BildFertigAsync();
        funde += await AnlagenZahl363Async(ansicht, e, "SI/AP1");
        funde += await EchtWelle2Async(ansicht, k, e, MesspunktW2);
        funde += await EchtBaukastenAsync(ansicht, k, e, MesspunktW2);
        funde += await EchtAeAsync(ansicht, k, e, bestand, MesspunktW2);
        funde += await EchtDpaAsync(ansicht, k, e, bestand, MesspunktW2);
        funde += await EchtDvAsync(ansicht, k, e, bestand, MesspunktW2);
        funde += await EchtW1Async(ansicht, k, e, MesspunktW2);
        funde += await Baukasten456Async(ansicht, k, e);     // A 1.11.0: Baukasten in voller Breite (Sammelliste 456)
        k.Probe(-1);
        trainer.ArtWaehlen(TrainerTexte.Uebung);
        await ansicht.BildFertigAsync();
        return funde;
    }

    /// <summary>
    /// Patch A 0.84.1 (Sammelliste 363): fuer jede Aufgabe des gezeigten Satzes nennt der Umschalter "Anlagen (n)" genau so
    /// viele Anlagen, wie mit "Anlagen" (oder daneben in zwei Spalten) gezeigt werden. Gegenprobe: mit der alten Zahl (alle
    /// Anlagen des Satzes) muss die Pruefung bei einer Aufgabe mit weniger Anlagen anschlagen.
    /// </summary>
    internal static async Task<int> AnlagenZahl363Async(MainView ansicht, EchtFormatTeil e, string name)
    {
        var funde = 0;
        var satz = e.ProbeSatz;
        if (satz is null)
        {
            Messwerte.Notiere("  FEHLER: Pruefung im echten Format (363) " + name + ": kein Satz gezeigt");
            return 1;
        }
        async Task<bool> Passt(int n)
        {
            e.ProbeReiter((n + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), true);
            await ansicht.BildFertigAsync();
            var soll = EchtAnlagenAnzeige.Von(satz, satz.Aufgaben[n]).Count;
            var (text, karten) = e.ProbeAnlagenZahl;
            return text == Beschriftungen.Einsetzen(EchtTexte.Anlagen, soll) && karten == soll;
        }
        var zahlen = new List<string>();
        for (var n = 0; n < satz.Aufgaben.Count; n++)
        {
            if (!await Passt(n))
            {
                Messwerte.Notiere("  FEHLER: Pruefung im echten Format (363) " + satz.Kennung + " Aufgabe " + (n + 1) + ": Umschalter \""
                    + e.ProbeAnlagenZahl.Text + "\", gezeigt " + e.ProbeAnlagenZahl.Karten + " Anlagen");
                funde++;
            }
            zahlen.Add(e.ProbeAnlagenZahl.Karten.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        // Gegenprobe: erste Aufgabe mit weniger Anlagen als der Satz hat
        var weniger = Enumerable.Range(0, satz.Aufgaben.Count).FirstOrDefault(n => EchtAnlagenAnzeige.Von(satz, satz.Aufgaben[n]).Count < satz.Anlagen.Count, -1);
        var gegenprobe = "keine Aufgabe mit weniger Anlagen";
        if (weniger >= 0)
        {
            EchtAnlagenAnzeige.ZahlAltFuerTests = true;
            try
            {
                if (await Passt(weniger))
                {
                    Messwerte.Notiere("  FEHLER: Pruefung im echten Format (363) " + satz.Kennung + ": Gegenprobe alte Zahl schlaegt nicht an");
                    funde++;
                }
                gegenprobe = "alte Zahl rot bei Aufgabe " + (weniger + 1) + " (\"" + e.ProbeAnlagenZahl.Text + "\")";
            }
            finally
            {
                EchtAnlagenAnzeige.ZahlAltFuerTests = false;
            }
        }
        e.ProbeReiter("1", false);
        await ansicht.BildFertigAsync();
        Messwerte.Notiere("Pruefung im echten Format Anlagen-Zahl (363) " + name + " " + satz.Kennung + ": " + string.Join("/", zahlen)
            + (e.ProbeZweiSpalten.Gezeigt ? " (zwei Spalten)" : "") + ", Gegenprobe " + gegenprobe);
        return funde;
    }

    /// <summary>
    /// A 0.77.0, Welle 2 (Weisung P12-265): Messpunkte der gezeichneten Anlagen und der Zeichenaufgabe, Pruefung der
    /// Bildkaesten (nie abgeschnitten: breiter als der Platz nur mit "→ wischen"), zwei Spalten am breiten PC und eine
    /// Bedienprobe mit Maus und Finger: "Als Text zeigen" zeigt die Textfassung, "Auf Papier gezeichnet" zaehlt die
    /// Teilaufgabe als bearbeitet.
    /// </summary>
    private static async Task<int> EchtWelle2Async(MainView ansicht, KlausurTeil k, EchtFormatTeil e,
        Func<string, Action, Action?, bool, Task> messpunkt)
    {
        var funde = 0;
        void Fund(string text)
        {
            Messwerte.Notiere("  FEHLER: Pruefung im echten Format (Welle 2): " + text);
            funde++;
        }
        var ab = KlausurTeil.EchtAb + 1 + EchtFormatTeil.Welle2Ab;
        await messpunkt("Pruefung im echten Format Netzplan zeigen (bis zum Bild)", () => k.Probe(ab + 1), null, true);
        await messpunkt("Pruefung im echten Format Monitor-Bild zeigen (bis zum Bild)", () => k.Probe(ab + 2), null, true);
        await messpunkt("Pruefung im echten Format Zeichenaufgabe mit Vorgabe (bis zum Bild)", () => k.Probe(ab + 3), null, true);
        await messpunkt("Pruefung im echten Format Musterbild in der Bewertung (bis zum Bild)", () => k.Probe(ab + 4), null, true);
        await messpunkt("Pruefung im echten Format UML-Musterbild (bis zum Bild)", () => k.Probe(ab + 6), null, true);

        // Bildkaesten: jedes Bild passt in seinen Platz oder ist seitlich wischbar (mit Hinweis); jedes Bild hat einen Namen
        // A 0.78.0: dazu die Zeichenflaeche des Baukastens (ER im Lauf, UML) und Bild und Zaehlung in der Bewertung
        var bau = KlausurTeil.EchtAb + 1 + EchtFormatTeil.BaukastenAb;
        foreach (var art in new[] { ab + 1, ab + 2, ab + 3, ab + 4, ab + 6, bau, bau + 1, bau + 2 })
        {
            k.Probe(art);
            await ansicht.BildFertigAsync();
            // nur sichtbare Bildkaesten (A 0.78.0: der zugeklappte Baukasten einer Zeichenaufgabe hat keine Groesse)
            var kaesten = e.ProbeInhalt?.GetVisualDescendants().OfType<Bildkasten>().Where(x => x.IsEffectivelyVisible).ToList() ?? [];
            if (kaesten.Count == 0)
            {
                Fund(KlausurTeil.ProbeNamen[art] + ": kein gezeichnetes Bild");
            }
            foreach (var kasten in kaesten)
            {
                var platz = kasten.Rollen.Viewport.Width;
                if (kasten.Bild.Bounds.Width > platz + 1 && !kasten.WischHinweis)
                {
                    Fund(KlausurTeil.ProbeNamen[art] + ": Bild " + kasten.Bild.Bounds.Width.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " breiter als der Platz "
                        + platz.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " ohne Hinweis \"wischen\"");
                }
                // Das Bild muss gezeichnet sein (nicht leer, z. B. ohne Farben). Grenze: das Weglassen eines ganzen Bilds durch
                // den Bildschirm-Zusammenbau (Fund A 0.77.0, Text weit ausserhalb) sieht diese Probe nicht, weil sie ohne ihn
                // zeichnet (Gegenprobe gemacht); davor schuetzt EchtBild.Mitte, belegt mit den Fotos.
                var (gezeichnet, flaeche) = BildPixel(kasten.Bild);
                if (gezeichnet * 100 < flaeche)
                {
                    Fund(KlausurTeil.ProbeNamen[art] + ": Bild " + kasten.Bild.GetType().Name + " leer (" + gezeichnet + " von " + flaeche + " Bildpunkten gezeichnet)");
                }
                if (string.IsNullOrEmpty(Avalonia.Automation.AutomationProperties.GetName(kasten.Bild)))
                {
                    Fund(KlausurTeil.ProbeNamen[art] + ": Bild ohne Namen fuer den Screenreader");
                }
                Messwerte.Notiere("Pruefung im echten Format " + KlausurTeil.ProbeNamen[art] + ": Bild " + kasten.Bild.GetType().Name + " "
                    + kasten.Bild.Bounds.Width.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " x " + kasten.Bild.Bounds.Height.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + ", Platz "
                    + platz.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + (kasten.WischHinweis ? ", wischen" : ""));
            }
        }

        // zwei Spalten: wo der Platz reicht, stehen Aufgabe und Netzplan nebeneinander und der Umschalter ist weg
        k.Probe(ab);
        await ansicht.BildFertigAsync();
        var (gezeigt, soll) = e.ProbeZweiSpalten;
        var nebeneinander = e.ProbeInhalt?.Children.OfType<Grid>().Any() == true;
        if (gezeigt != soll || nebeneinander != soll || e.ProbeUmschalter?.IsVisible == soll)
        {
            Fund("zwei Spalten: soll " + soll + ", gezeigt " + gezeigt + ", Raster " + nebeneinander + ", Umschalter sichtbar "
                + e.ProbeUmschalter?.IsVisible);
        }
        Messwerte.Notiere("Pruefung im echten Format zwei Spalten (Aufgabe und Anlagen nebeneinander): " + (soll ? "ja" : "nein, Umschalter"));

        // Bedienprobe mit Maus und Finger
        foreach (var art in new[] { PointerType.Mouse, PointerType.Touch })
        {
            var name = art == PointerType.Mouse ? "Maus" : "Finger";
            k.Probe(ab + 1);
            await ansicht.BildFertigAsync();
            var texte = e.ProbeInhalt?.GetVisualDescendants().OfType<LernText>().Count(x => x.IsEffectivelyVisible) ?? 0;
            if (!await TippenAsync(ansicht, e.ProbeInhalt, EchtTexte.AlsText, art)
                || e.ProbeInhalt?.GetVisualDescendants().OfType<Button>().Any(b => Avalonia.Automation.AutomationProperties.GetName(b) == EchtTexte.BildZeigen) != true
                || (e.ProbeInhalt?.GetVisualDescendants().OfType<LernText>().Count(x => x.IsEffectivelyVisible) ?? 0) <= texte)
            {
                Fund(name + ": \"Als Text zeigen\" antippen zeigt die Textfassung nicht");
            }
            k.Probe(ab + 3);
            await ansicht.BildFertigAsync();
            var vorher = e.ProbeFortschritt;
            var papier = e.ProbeInhalt?.GetVisualDescendants().OfType<CheckBox>()
                .FirstOrDefault(c => Avalonia.Automation.AutomationProperties.GetName(c) == EchtTexte.Gezeichnet);
            var war = papier?.IsChecked == true;
            if (papier is null || !await TippenAsync(ansicht, papier, art) || papier.IsChecked == war || e.ProbeFortschritt == vorher)
            {
                Fund(name + ": \"Auf Papier gezeichnet\" antippen aendert den Fortschritt nicht (" + vorher + " / " + e.ProbeFortschritt + ")");
            }
            Messwerte.Notiere("Pruefung im echten Format Bedienprobe Welle 2 " + name + ": Als Text zeigen, Auf Papier gezeichnet ("
                + vorher + " -> " + e.ProbeFortschritt + ")");
        }
        return funde;
    }

    /// <summary>
    /// Ist das Bild gezeichnet? Das Bild allein (ganz, auch was erst nach dem Wischen sichtbar ist) in ein Bild zeichnen und
    /// die Bildpunkte zaehlen, die sich vom Grund (haeufigste Farbe) unterscheiden.
    /// </summary>
    internal static (int Gezeichnet, int Flaeche) BildPixel(Control bild)
    {
        var groesse = new PixelSize((int)Math.Ceiling(bild.Bounds.Width), (int)Math.Ceiling(bild.Bounds.Height));
        if (groesse.Width <= 0 || groesse.Height <= 0)
        {
            return (0, 1);
        }
        using var gemalt = new RenderTargetBitmap(groesse, new Vector(96, 96));
        gemalt.Render(bild);
        var pixel = Pixel(gemalt);
        var (x0, y0, x1, y1) = (0, 0, groesse.Width, groesse.Height);
        // Grund = haeufigste Farbe (das Bild hat keinen eigenen Hintergrund: durchsichtig)
        var zaehler = new Dictionary<Avalonia.Media.Color, int>();
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                var p = pixel[(y * groesse.Width) + x];
                zaehler[p] = zaehler.GetValueOrDefault(p) + 1;
            }
        }
        var grund = zaehler.MaxBy(z => z.Value).Key;
        var anders = 0;
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                var p = pixel[(y * groesse.Width) + x];
                if (Math.Abs(p.A - grund.A) + Math.Abs(p.R - grund.R) + Math.Abs(p.G - grund.G) + Math.Abs(p.B - grund.B) > 24)
                {
                    anders++;
                }
            }
        }
        return (anders, (x1 - x0) * (y1 - y0));
    }

    /// <summary>Knopf mit dem Namen in einem Bereich antippen (Druck und Loslassen in der Mitte).</summary>
    private static async Task<bool> TippenAsync(MainView ansicht, Control? bereich, string name, PointerType art)
    {
        var ziel = bereich?.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => Avalonia.Automation.AutomationProperties.GetName(b) == name);
        return ziel is not null && await TippenAsync(ansicht, ziel, art);
    }

    /// <summary>
    /// Sammelliste 433/458/460/461 (Weisung P12-380, W1/W2): so viele Bilder wartet die Probe hoechstens, bis die Treffer-Pruefung
    /// von Avalonia (dieselbe, die ein Knopf beim Loslassen nutzt: <c>GetVisualsAt</c>) den Knopf an seiner Stelle findet.
    /// </summary>
    internal const int TrefferBilderHoechstens = 10;

    /// <summary>Letzte Treffer-Pruefung vor dem Tippen (nur Bericht und Fund): Bilder bis zum Treffer, getroffenes Element.</summary>
    internal static string TrefferZuletzt { get; private set; } = "";

    /// <summary>
    /// W1/W2 (Weisung P12-380): Avalonia 12 prueft den Treffer gegen die beim Zeichnen gemerkten Grenzen. Die Probe tippte gleich
    /// nach dem ersten Bild der neuen Ansicht; standen die Grenzen noch vom Bild davor, ging der Klick ins Leere (1. Lauf rot).
    /// Jetzt wartet sie, bis der Knopf an der Stelle wirklich getroffen wird (hoechstens <see cref="TrefferBilderHoechstens"/>
    /// Bilder, das Tippen selbst geschieht genau einmal). Wird er nie getroffen, tippt sie nicht und meldet, was dort liegt.
    /// </summary>
    internal static async Task<bool> TreffbarAsync(MainView ansicht, Control ziel, Point lokal)
    {
        for (var bild = 0; bild <= TrefferBilderHoechstens; bild++)
        {
            var oben = ziel.GetVisualsAt(lokal).FirstOrDefault();
            if (oben is not null && (ReferenceEquals(oben, ziel) || ziel.IsVisualAncestorOf(oben)))
            {
                TrefferZuletzt = bild == 0 ? "" : " (Treffer nach " + bild + " Bildern)";
                return true;
            }
            if (bild == TrefferBilderHoechstens)
            {
                TrefferZuletzt = " (Stelle trifft nicht den Knopf, sondern " + (oben?.GetType().Name ?? "nichts") + ")";
                return false;
            }
            await ansicht.BildFertigAsync();
        }
        return false;
    }

    private static async Task<bool> TippenAsync(MainView ansicht, Control ziel, PointerType art)
    {
        TrefferZuletzt = "";
        ziel.BringIntoView();
        await ansicht.BildFertigAsync();
        if (TopLevel.GetTopLevel(ziel) is not { } wurzel)
        {
            return false;
        }
        var mitte = new Point(ziel.Bounds.Width / 2, ziel.Bounds.Height / 2);
        if (!await TreffbarAsync(ansicht, ziel, mitte))
        {
            return false;
        }
        var punkt = ziel.TranslatePoint(mitte, wurzel) ?? mitte;
        var zeiger = new Pointer(Pointer.GetNextFreeId(), art, true);
        var druck = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        var los = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
        ziel.RaiseEvent(new PointerPressedEventArgs(ziel, zeiger, wurzel, punkt, 0, druck, KeyModifiers.None));
        ziel.RaiseEvent(new PointerReleasedEventArgs(ziel, zeiger, wurzel, punkt, 0, los, KeyModifiers.None, MouseButton.Left));
        await ansicht.BildFertigAsync();
        return true;
    }
}
