using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.58.0 (Weisung P12-298, Sammelliste 377 bis 379), je Bedienweg Maus, Finger und Screenreader (Aufruf ueber die
/// Automation):
/// <list type="bullet">
/// <item>377: Karte „Grenzen live“ klappt ein und auf; eingeklappt bleibt die Kopfzeile mit dem groessten Anteil (rot, wenn
/// ueber der Grenze); steht sie neben der Kamera-Karte, ist die Unterkante buendig (jede Groesse, auf- und eingeklappt).</item>
/// <item>378: in der oberen Leiste stehen Spielgeschichte, Quests und Questgeber nicht mehr, dafuer „Spiel ▾“ nach „Gebäude
/// entwerfen“; ein Tipp oeffnet die Spielgeschichte mit der Seitenleiste (drei Seiten, Namen wie bisher), dort wird jede
/// Seite gewaehlt, danach klappt sie ein (sie liegt ueber der Seite und nimmt ihr keine Breite); ein zweiter Tipp auf
/// „Spiel ▴“ klappt sie ebenfalls ein, ein dritter wieder aus.</item>
/// <item>379: die Knopfleiste ist eine Zeile (alle Knoepfe gleiche Hoehe und Zeile, hoechstens 50 Bildpunkte hoch); jedes
/// Menue zeigt seine Eintraege, ein Eintrag schliesst es; „Werkzeuge: Maßband“ steht nach der Wahl im Knopf; Taste L wirkt
/// auch eingeklappt; eingeklappt bleibt nur „Knöpfe ›“. Verdeckte Flaeche der Karte als Messwert je Groesse.</item>
/// </list>
/// </summary>
public static partial class StadtbauMessung
{
    private static readonly string[] Bedienwege = ["Maus", "Finger", "Screenreader"];

    private static async Task DrueckenAsync(Hauptansicht a, Button knopf, string weg)
    {
        if (weg == "Screenreader")
        {
            if (ControlAutomationPeer.CreatePeerForElement(knopf).GetProvider<IInvokeProvider>() is { } aufruf)
            {
                aufruf.Invoke();
            }
            await BildAsync(a);
            return;
        }
        var mitte = new Point(knopf.Bounds.Width / 2, knopf.Bounds.Height / 2);
        await AntippbarAsync(a, knopf);
        var zeiger = new Pointer(Pointer.GetNextFreeId(), weg == "Maus" ? PointerType.Mouse : PointerType.Touch, true);
        var wurzel = Wurzel(knopf);
        var ort = knopf.TranslatePoint(mitte, wurzel) ?? mitte;
        knopf.RaiseEvent(new PointerPressedEventArgs(knopf, zeiger, wurzel, ort, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        knopf.RaiseEvent(new PointerReleasedEventArgs(knopf, zeiger, wurzel, ort, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
        await BildAsync(a);
    }

    /// <summary>
    /// Wie ein Mensch: erst tippen, wenn der Knopf (z. B. in einem eben geoeffneten Menue) antippbar ist, hoechstens 10 Bilder
    /// lang (danach tippt die Probe trotzdem, und ein nicht angekommener Tipp wird rot).
    /// </summary>
    private static async Task AntippbarAsync(Hauptansicht a, Control knopf)
    {
        for (var i = 0; i < 10 && !knopf.GetVisualsAt(new Point(knopf.Bounds.Width / 2, knopf.Bounds.Height / 2)).Any(); i++)
        {
            await BildAsync(a);
        }
    }

    /// <summary>Anteil der Karte (Raster 4 Punkte) unter Knopfleiste, Ebenen, Grenzen und Kamera-Karte in Prozent.</summary>
    private static double VerdecktProzent(Hauptansicht a)
    {
        var karte = a.Karte;
        var rects = new List<Rect>();
        foreach (var c in new Control?[] { a.WerkzeugLeiste, a.EbenenListe, a.GrenzLeiste, a.KameraDraussen })
        {
            if (c is { IsEffectivelyVisible: true } && c.TranslatePoint(new Point(0, 0), karte) is { } p)
            {
                rects.Add(new Rect(p, c.Bounds.Size));
            }
        }
        long alle = 0, unter = 0;
        for (var y = 2.0; y < karte.Bounds.Height; y += 4)
        {
            for (var x = 2.0; x < karte.Bounds.Width; x += 4)
            {
                alle++;
                unter += rects.Any(r => r.Contains(new Point(x, y))) ? 1 : 0;
            }
        }
        return 100.0 * unter / Math.Max(1, alle);
    }

    private static async Task Leiste379Async(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Leiste", null);
        await a.AussenGeprueft;
        await a.GrenzenLiveFertig;
        a.ReiterZeigen(innen: false);

        // ---- 379: eine Zeile, Menues, aktiver Modus, Einklappen ----
        foreach (var (name, b, h) in Groessen)
        {
            await GroesseAsync(a, f, b, h);
            a.Karte.Einpassen();
            await BildAsync(a);
            var knoepfe = a.LeisteKnoepfe.Values.Where(k => k.IsEffectivelyVisible).ToList();
            var oben = knoepfe.Select(k => k.TranslatePoint(new Point(0, 0), a.WerkzeugLeiste)?.Y ?? -1).Distinct().ToList();
            Messwerte.Notiere(string.Create(Inv, $"Leiste 379 {name}: {knoepfe.Count} Knoepfe, Hoehe {a.WerkzeugLeiste.Bounds.Height:F0}, verdeckt {VerdecktProzent(a):F1} %"));
            if (knoepfe.Count != 4 || oben.Count != 1 || a.WerkzeugLeiste.Bounds.Height > 50)
            {
                Fund(string.Create(Inv, $"Leiste 379 {name}: {knoepfe.Count} statt 4 Knoepfe, {oben.Count} Zeilen, Hoehe {a.WerkzeugLeiste.Bounds.Height:F0} (hoechstens 50)"));
            }
        }
        var menues = new Dictionary<string, Button[]>
        {
            [Texte.LeisteWaehlen] = [],
            [Texte.LeisteWerkzeuge] = [a.MassbandKnopf, a.NotizKnopf, a.PlaetzeKnopf, a.NamenKnopf],
            [Texte.LeisteAnzeigen] = [a.EbenenKnopf, a.WaermeKnopf, a.StadtteileKnopf],
        };
        menues[Texte.LeisteWaehlen] = [a.MehrereKnopf];
        foreach (var weg in Bedienwege)
        {
            foreach (var (menue, eintraege) in menues)
            {
                await DrueckenAsync(a, a.LeisteKnoepfe[menue], weg);
                var fehlt = eintraege.Where(e => !e.IsEffectivelyVisible || !a.LeisteMenueKarte.IsVisualAncestorOf(e)).Select(e => AutomationProperties.GetName(e)).ToList();
                if (a.LeisteMenueOffen != menue || fehlt.Count > 0)
                {
                    Fund($"Leiste 379 {weg}: Menue \"{menue}\" offen {a.LeisteMenueOffen}, fehlt: {string.Join(", ", fehlt)}");
                }
                await DrueckenAsync(a, a.LeisteKnoepfe[menue], weg);
                if (a.LeisteMenueOffen is not null)
                {
                    Fund($"Leiste 379 {weg}: zweiter Tipp schliesst \"{menue}\" nicht");
                }
            }
            // Massband aus dem Menue: Menue zu, Modus im Knopf
            await DrueckenAsync(a, a.LeisteKnoepfe[Texte.LeisteWerkzeuge], weg);
            await DrueckenAsync(a, a.MassbandKnopf, weg);
            var text = AutomationProperties.GetName(a.LeisteKnoepfe[Texte.LeisteWerkzeuge]);
            if (!a.Karte.MassbandAn || a.LeisteMenueOffen is not null || text != Texte.LeisteWerkzeuge + ": " + Texte.Massband)
            {
                Fund($"Leiste 379 {weg}: Massband an {a.Karte.MassbandAn}, Menue {a.LeisteMenueOffen}, Knopf \"{text}\"");
            }
            a.MassbandUmschalten();
            await BildAsync(a);
            // Einklappen: nur „Knöpfe ›“ bleibt, Taste L wirkt weiter
            await DrueckenAsync(a, a.LeisteKlappKnopf!, weg);
            var sichtbar = a.LeisteKnoepfe.Values.Count(k => k.IsEffectivelyVisible);
            a.TasteFuerTests(Key.L);
            await BildAsync(a);
            var taste = a.Karte.MassbandAn;
            a.MassbandUmschalten();
            if (!a.LeisteEingeklappt || sichtbar != 0 || !a.LeisteKlappKnopf!.IsEffectivelyVisible || !taste)
            {
                Fund($"Leiste 379 {weg}: eingeklappt {a.LeisteEingeklappt}, sichtbar {sichtbar}, Taste L {taste}");
            }
            Messwerte.Notiere(string.Create(Inv, $"Leiste 379 {weg} eingeklappt: verdeckt {VerdecktProzent(a):F1} %"));
            await DrueckenAsync(a, a.LeisteKlappKnopf!, weg);
            if (a.LeisteEingeklappt || a.LeisteKnoepfe.Values.Count(k => k.IsEffectivelyVisible) != 4)
            {
                Fund($"Leiste 379 {weg}: Aufklappen zeigt die vier Knoepfe nicht");
            }
        }

        // ---- 377: Grenzen live einklappen, Unterkante buendig ----
        foreach (var weg in Bedienwege)
        {
            foreach (var zu in new[] { true, false })
            {
                await DrueckenAsync(a, a.GrenzKlappKnopf, weg);
                var kopf = a.GrenzKopfText;
                var rotRichtig = a.GrenzenLiveStand is not { } g || a.GrenzKopfRot == (g.Anteil > 1);
                if (a.GrenzenEingeklappt != zu || !kopf.Contains('%', StringComparison.Ordinal) || !rotRichtig)
                {
                    Fund($"Grenzen 377 {weg}: eingeklappt {a.GrenzenEingeklappt} statt {zu}, Kopf \"{kopf}\", rot richtig {rotRichtig}");
                }
                foreach (var (name, b, h) in Groessen)
                {
                    await GroesseAsync(a, f, b, h);
                    await BildAsync(a);
                    var g1 = a.GrenzLeiste.TranslatePoint(new Point(0, a.GrenzLeiste.Bounds.Height), a.Karte);
                    var k1 = a.KameraDraussen.TranslatePoint(new Point(0, a.KameraDraussen.Bounds.Height), a.Karte);
                    var nebeneinander = g1 is { } gp && a.GrenzLeiste.TranslatePoint(new Point(a.GrenzLeiste.Bounds.Width, 0), a.Karte) is { } gr
                        && a.KameraDraussen.TranslatePoint(new Point(0, 0), a.Karte) is { } kl && gr.X <= kl.X + 0.5;
                    Messwerte.Notiere(string.Create(Inv, $"Grenzen 377 {weg} {name} {(zu ? "zu" : "auf")}: Unterkante {g1?.Y:F1} / Kamera {k1?.Y:F1}, nebeneinander {nebeneinander}, verdeckt {VerdecktProzent(a):F1} %"));
                    if (!nebeneinander || g1 is null || k1 is null || Math.Abs(g1.Value.Y - k1.Value.Y) > 1)
                    {
                        Fund(string.Create(Inv, $"Grenzen 377 {weg} {name}: nicht buendig neben der Kamera-Karte ({g1?.Y:F1} / {k1?.Y:F1}, nebeneinander {nebeneinander})"));
                    }
                }
            }
        }

        // ---- 378: Reiter „Spiel ▾“ ----
        var leiste = a.KopfKnoepfe.Where(k => k.Knopf.IsEffectivelyVisible && !a.SpielSeitenleiste.IsVisualAncestorOf(k.Knopf)).Select(k => k.Text).ToList();
        if (leiste.Contains(Texte.Spielgeschichte) || leiste.Contains(Texte.Quests) || leiste.Contains(Texte.Questgeber) || !leiste.Contains(Texte.SpielMenue))
        {
            Fund("Spiel 378: obere Leiste " + string.Join(", ", leiste));
        }
        var entwerfen = KopfKnopf(a, Texte.GebaeudeEntwerfen).TranslatePoint(default, a);
        var drei = KopfKnopf(a, Texte.Ansicht3D).TranslatePoint(default, a);
        var spiel = KopfKnopf(a, Texte.SpielMenue).TranslatePoint(default, a);
        if (entwerfen is null || drei is null || spiel is null || !(drei.Value.X < entwerfen.Value.X && entwerfen.Value.X < spiel.Value.X))
        {
            Fund($"Spiel 378: Reihenfolge 3D {drei} < Entwerfen {entwerfen} < Spiel {spiel} falsch");
        }
        foreach (var weg in Bedienwege)
        {
            a.ReiterZeigen(innen: false);
            await BildAsync(a);
            await DrueckenAsync(a, KopfKnopf(a, Texte.SpielMenue), weg);
            var seiten = Hauptansicht.SpielSeiten.Where(s => KopfKnopf(a, s).IsEffectivelyVisible && a.SpielSeitenleiste.IsVisualAncestorOf(KopfKnopf(a, s))).ToList();
            if (!a.SpielAusgeklappt || !a.GeschichteOffen || seiten.Count != 3)
            {
                Fund($"Spiel 378 {weg}: ausgeklappt {a.SpielAusgeklappt}, Spielgeschichte {a.GeschichteOffen}, Seiten {string.Join(", ", seiten)}");
            }
            var breiteZu = SeitenBreite(a);
            foreach (var s in new[] { Texte.Quests, Texte.Questgeber })
            {
                if (!a.SpielAusgeklappt)
                {
                    await DrueckenAsync(a, KopfKnopf(a, Texte.SpielMenue), weg);
                }
                if (Math.Abs(SeitenBreite(a) - breiteZu) > 0.5)
                {
                    Fund($"Spiel 378 {weg}: Seitenleiste nimmt der Seite Breite ({SeitenBreite(a):0} statt {breiteZu:0})");
                }
                await DrueckenAsync(a, KopfKnopf(a, s), weg);
                await BildAsync(a);
                if (a.QuestSeite != s || a.SpielAusgeklappt)
                {
                    Fund($"Spiel 378 {weg}: Seite {s} nicht gewaehlt ({a.QuestSeite}) oder Seitenleiste nicht eingeklappt ({a.SpielAusgeklappt})");
                }
            }
            await DrueckenAsync(a, KopfKnopf(a, Texte.SpielMenue), weg);
            if (!a.SpielAusgeklappt || a.QuestSeite != Texte.Questgeber)
            {
                Fund($"Spiel 378 {weg}: Tipp auf „Spiel“ klappt nicht aus (Seitenleiste {a.SpielAusgeklappt}, Seite {a.QuestSeite})");
            }
            await DrueckenAsync(a, KopfKnopf(a, Texte.SpielMenue), weg);
            if (a.SpielAusgeklappt || a.QuestSeite != Texte.Questgeber)
            {
                Fund($"Spiel 378 {weg}: zweiter Tipp klappt nicht ein (Seitenleiste {a.SpielAusgeklappt}, Seite {a.QuestSeite})");
            }
            await DrueckenAsync(a, KopfKnopf(a, Texte.SpielMenue), weg);
            if (!a.SpielAusgeklappt)
            {
                Fund($"Spiel 378 {weg}: dritter Tipp klappt nicht wieder aus");
            }
        }
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }

    /// <summary>Breite der Seite unter „Spiel“ (die Seitenleiste darf sie nicht verkleinern).</summary>
    private static double SeitenBreite(Hauptansicht a) => a.SpielSeiteBreite;
}
