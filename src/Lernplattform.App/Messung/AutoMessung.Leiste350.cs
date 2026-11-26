using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Texte;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.81.0 (Weisung P12-274, Sammelliste 350, Nico: „Der Menüpunkt Übersicht soll auch ins Handy rein. Punkte die hier
/// doppelt wären sollen entfernt werden (Übersicht, Karte)“ und „Spiel verlassen in die Leiste“).
/// 1. Untere Leiste ohne „Übersicht“ und „Karte“ in jeder Lage (draussen, drinnen, zugeklappt, mit Spielstand, im Bus), „Spiel
///    verlassen“ immer zu sehen (auch zugeklappt), ohne 3D bleibt „Orte“. 2. Reiter „Übersicht“ im Handy zeigt dieselben Texte und
///    Werte wie die Seite der Spiel-Tafel (Gegenprobe: der Reiter „Offen“ liefert andere Texte; ohne Spielstand kein Reiter).
/// 3. „Spiel verlassen“ aus der Leiste mit Maus, Finger, Eingabetaste und Screenreader (Name und Aufruf ueber die Automation).
/// Seit A 1.13.0 (Sammelliste 464, Nico 5a) gibt es das Dreieck zum Zuklappen nicht mehr (damit entfaellt auch das Verstecken von
/// „Spiel verlassen“ aus A 0.87.0, Sammelliste 371): „Spiel verlassen“ und das Handy sind in jeder Lage zu sehen.
/// Seit der Nachbesserung A 1.13.0 (Weisung P12-398, Nico 26.11.2026) gibt es „Spiel verlassen“ unten nicht mehr: das X oben rechts
/// ist in jeder Lage der eine Weg aus dem Spiel (Pruefungen 1 und 3 jetzt am X, gleich streng: Maus, Finger, Taste, Screenreader).
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Alle Texte der Teile in der Reihenfolge des Aufbaus (Start-Test 350, Fotos).</summary>
    internal static List<string> TexteVon(IEnumerable<Control> teile) =>
        [.. teile.SelectMany(t => t.GetSelfAndLogicalDescendants().OfType<TextBlock>()).Select(t => t.Text ?? "").Where(t => t.Length > 0)];

    private static async Task<int> LeisteUebersichtAsync(MainView ansicht)
    {
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Leiste 350 " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Leiste 350 " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null || seite.Tafel is not { } tafel)
        {
            Messwerte.Notiere("  FEHLER: Spiel Leiste 350: Spielseite nicht gebaut");
            return 1;
        }
        var gl = await GlAbwartenAsync(seite);
        void Lage(string name)
        {
            var leiste = seite.LeisteKnoepfe;
            var doppelt = leiste.Where(t => t is StadtTexte.Uebersicht or KarteTexte.Karte).ToList();
            // Weisung P12-398: genau ein Weg aus dem Spiel, das X oben rechts (kein zweiter Knopf unten)
            var verlassen = seite.XKnopf is { IsEffectivelyVisible: true } && !seite.VerlassenKnopf.IsEffectivelyVisible;
            var orte = !gl || seite.Spiel?.ImInnenraum == true || !seite.OrteKnopfSichtbar;
            Pruefe(name, doppelt.Count == 0 && verlassen && orte,
                "Leiste: " + string.Join(", ", leiste) + (doppelt.Count > 0 ? " (DOPPELT: " + string.Join(", ", doppelt) + ")" : "")
                + (verlassen ? ", X oben rechts" : seite.VerlassenKnopf.IsEffectivelyVisible ? ", Spiel verlassen UNTEN (zweiter Weg)" : ", X FEHLT") + (orte ? "" : ", Orte mit 3D da"));
        }
        var vorher = seite.Spiel;
        try
        {
            // 1. Leiste in allen Lagen
            var spiel = Frisch(seite);
            if (spiel.ImInnenraum)
            {
                spiel.Verlassen();
            }
            await ansicht.BildFertigAsync();
            Lage("draussen" + (gl ? "" : " ohne 3D"));
            Pruefe("ohne 3D Orte", gl || seite.OrteKnopfSichtbar, gl ? "mit 3D (kein Knopf Orte)" : seite.OrteKnopfSichtbar ? "Knopf Orte da" : "Knopf Orte FEHLT");
            // seit A 1.13.0 (Sammelliste 464, Nico 5a): kein Dreieck mehr, das Handy ist immer zu sehen; seit P12-398 das X statt
            // „Spiel verlassen“ unten
            var xDa = seite.XKnopf?.IsEffectivelyVisible == true;
            Pruefe("Handy und X ohne Dreieck", xDa && !seite.VerlassenKnopf.IsEffectivelyVisible && seite.QuestKnopf?.IsEffectivelyVisible == true,
                (xDa ? "X da" : "X FEHLT") + (seite.VerlassenKnopf.IsEffectivelyVisible ? ", Spiel verlassen UNTEN" : "") + (seite.QuestKnopf?.IsEffectivelyVisible == true ? ", Handy da" : ", Handy FEHLT"));
            // nach einem schmalen Fenster steht das X am PC wieder oben rechts in der Ecke (die Leiste klemmt nicht)
            if (TopLevel.GetTopLevel(ansicht) is Window fenster)
            {
                var (breiteVorher, hoeheVorher) = (fenster.Width, fenster.Height);
                try
                {
                    var (hoechstB, hoechstH) = Hoechstgroesse(fenster);
                    foreach (var (groesse, breite, hoehe) in new[] { ("schmal", SchmalBreite, SchmalHoehe), ("pc", PcBreite, PcHoehe) })
                    {
                        fenster.Width = Math.Min(breite, hoechstB);
                        fenster.Height = Math.Min(hoehe, hoechstH);
                        await ansicht.BildFertigAsync();
                        await ansicht.BildFertigAsync();
                        if (groesse == "pc")
                        {
                            var xr = seite.XKnopf is { IsEffectivelyVisible: true } xk && xk.TranslatePoint(default, seite) is { } xp ? new Rect(xp, xk.Bounds.Size) : (Rect?)null;
                            var ecke = xr is { } e && e.Right <= seite.Bounds.Width + 0.5 && e.Right >= seite.Bounds.Width - 24 && e.Top <= 24;
                            Pruefe("pc nach schmal", ecke && !seite.KnoepfeUnten,
                                (ecke ? "X oben rechts" : xr is null ? "X FEHLT" : "X NICHT in der Ecke") + (seite.KnoepfeUnten ? ", Knoepfe UNTER dem Handy" : ", Knoepfe in der Zeile"));
                        }
                    }
                }
                finally
                {
                    fenster.Width = breiteVorher;
                    fenster.Height = hoeheVorher;
                    await ansicht.BildFertigAsync();
                }
            }
            if (ZurBitweiche(spiel))
            {
                spiel.Betreten();
                await ansicht.BildFertigAsync();
                Lage("drinnen");
                spiel.Verlassen();
                await ansicht.BildFertigAsync();
            }
            else
            {
                Pruefe("drinnen", false, "Bitweiche nicht betreten");
            }
            // an der Haltestelle (Einsteigen und Hinsetzen stehen dort als Knoepfe des Orts)
            var halt = Frisch(seite);
            if (halt.Linie is { } linie && halt.Szene.Haltestellen.FirstOrDefault(x => linie.Halte.Any(l => l.Haltestelle == x.Nr)) is { } h)
            {
                halt.GeheZu(h.Haeuschen.X, h.Haeuschen.Y);
                halt.SofortAnkommen();
                await ansicht.BildFertigAsync();
                Lage("an der Haltestelle" + (halt.WartetAn == h.Nr ? "" : " (wartet nicht)"));
            }
            if (gl)
            {
                var bus = MitBus(seite);
                await ansicht.BildFertigAsync();
                if (bus.MitBusZu("amt") == StadtGrund.Ok)
                {
                    var u = Stopwatch.StartNew();
                    while (!bus.ImBus && bus.Geht && u.Elapsed.TotalSeconds < 60)
                    {
                        await seite.Flaeche.NaechstesBildAsync();
                    }
                    await ansicht.BildFertigAsync();
                    if (bus.ImBus)
                    {
                        Lage("im Bus");
                    }
                    else
                    {
                        Pruefe("im Bus", false, "nicht eingestiegen");
                    }
                }
                bus.SofortAnkommen();
            }

            // 2. Reiter Uebersicht = Seite der Tafel (gleiche Texte und Werte)
            Frisch(seite);
            await ansicht.BildFertigAsync();
            Pruefe("ohne Spielstand kein Reiter", !seite.UebersichtReiterMoeglich, seite.UebersichtReiterMoeglich ? "Reiter da (FALSCH)" : "kein Reiter");
            try
            {
                tafel.ProbeFirmaAnlegen();
            }
            catch (Exception e) when (e is SpielFehler or SpielBelegtAusnahme or InvalidOperationException)
            {
                Messwerte.Notiere("Spiel: Leiste 350: Durchgang schon da oder nicht angelegt (" + e.Message + ")");
            }
            seite.TafelZeigen();
            tafel.ProbeUebersicht();
            seite.TafelZeigen();
            await ansicht.BildFertigAsync();
            var alt = TexteVon(tafel.InhaltTeile);
            tafel.AllesSchliessen();
            await ansicht.BildFertigAsync();
            Pruefe("mit Spielstand Reiter", seite.UebersichtReiterMoeglich, seite.UebersichtReiterMoeglich ? "Reiter da" : "Reiter FEHLT");
            Lage("mit Spielstand");
            var offen = seite.HandyZeigenAsync(SpielSeite.HandyReiter.Uebersicht);
            await ansicht.BildFertigAsync();
            var neu = TexteVon(seite.UebersichtGezeigt);
            var erste = alt.Zip(neu).Select((p, i) => (p, i)).FirstOrDefault(x => x.p.First != x.p.Second);
            Pruefe("Reiter Uebersicht gleich der Tafel", seite.HandyReiterGerade == SpielSeite.HandyReiter.Uebersicht && alt.Count > 5 && alt.SequenceEqual(neu),
                $"Tafel {alt.Count} Texte, Handy {neu.Count} Texte" + (alt.SequenceEqual(neu) ? ", alle gleich" : erste.p.First is null ? "" : $", erster Unterschied Nr. {erste.i}: „{erste.p.First}“ / „{erste.p.Second}“"));
            var fund = HandyFund(Dialog.Offen);
            Pruefe("Reiter Uebersicht X oben rechts", fund is null, fund ?? "ok");
            Dialog.AlleSchliessen();
            await Task.WhenAny(offen, Task.Delay(3000));
            await ansicht.BildFertigAsync();
            // Gegenprobe: der Reiter „Offen“ liefert andere Texte (der Vergleich erkennt einen Unterschied)
            offen = seite.HandyZeigenAsync(SpielSeite.HandyReiter.Offen);
            await ansicht.BildFertigAsync();
            var andere = Dialog.Offen is { } handy ? TexteVon([handy]) : [];
            Pruefe("Gegenprobe Reiter Offen", andere.Count > 0 && !alt.SequenceEqual(andere), andere.Count == 0 ? "Handy nicht offen" : alt.SequenceEqual(andere) ? "GLEICH (Pruefung zu schwach)" : "anders");
            Dialog.AlleSchliessen();
            await Task.WhenAny(offen, Task.Delay(3000));
            await ansicht.BildFertigAsync();
        }
        finally
        {
            Dialog.AlleSchliessen();
            tafel.AllesSchliessen();
            seite.SpielSetzen(vorher);
            await ansicht.BildFertigAsync();
        }

        // 3. Spiel verlassen ueber das X oben rechts (seit P12-398 der einzige Weg, auch im alten Menue nach Frisch)
        var name = seite.XKnopf is { } xn ? AutomationProperties.GetName(xn) : null;
        Pruefe("Spiel verlassen Name", name == StadtTexte.Verlassen, "Name „" + name + "“");
        foreach (var art in new[] { "Maus", "Finger", "Eingabetaste", "Screenreader" })
        {
            var s = await SpielSeiteAsync(ansicht);
            if (s is null)
            {
                Pruefe("Spiel verlassen " + art, false, "Spielseite nicht gebaut");
                continue;
            }
            Frisch(s);
            await ansicht.BildFertigAsync();
            if (s.XKnopf is not { IsEffectivelyVisible: true } knopf)
            {
                Pruefe("Spiel verlassen " + art, false, "X nicht zu sehen");
                continue;
            }
            switch (art)
            {
                case "Maus":
                    await TippenAsync(ansicht, knopf, PointerType.Mouse);
                    break;
                case "Finger":
                    await TippenAsync(ansicht, knopf, PointerType.Touch);
                    break;
                case "Eingabetaste":
                    await TasteAsync(ansicht, knopf, Key.Enter);
                    break;
                default:
                    ControlAutomationPeer.CreatePeerForElement(knopf).GetProvider<IInvokeProvider>()?.Invoke();
                    break;
            }
            for (var i = 0; i < 20 && ansicht.AktuelleSeite == Seiten.Spiel; i++)
            {
                await ansicht.BildFertigAsync();
            }
            var weg = ansicht.AktuelleSeite != Seiten.Spiel;
            Pruefe("Spiel verlassen " + art, weg, "Seite " + ansicht.AktuelleSeite);
            s.SpielSetzen(vorher);
        }
        await ansicht.ZeigeAsync(Seiten.Spiel);
        await ansicht.BildFertigAsync();
        return funde;
    }
}
