using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.81.0 (Weisung P12-272, Sammelliste 349, Nico 1b 2c 3a 4b): Hinsetzen und Hinlegen ueber echte Bedienung. In der
/// Wohnung (frischer Probe-Ablauf mit PC und Bett, nur im Speicher): Stuhl und Bett mit Maus und Finger in der 3D-Ansicht
/// antippen (Figur setzt bzw. legt sich, erst dann „PC benutzen“ bzw. „Schlafen“), „Aufstehen“ antippen und woanders hin
/// tippen; „Hinsetzen“, „Hinlegen“ und „Aufstehen“ mit der Eingabetaste und ueber die Automation (wie ein Screenreader). An der
/// Haltestelle nur ueber „Hinsetzen“ (nie von allein). Nur Darstellung: kein neuer Eintrag im Spielstand. Gegenprobe: mit
/// dem Verhalten bis A 0.80.0 stehen die PC-Knoepfe im Stehen da, die Pruefung meldet das.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Fund im Stehen vor dem PC-Stuhl (null = in Ordnung): PC-Knoepfe duerfen erst im Sitzen da sein (1b).</summary>
    internal static string? HaltungPcFund(StadtSpiel spiel) =>
        spiel.MeinPlatz is null && spiel.Knoepfe.Any(k => StadtSpiel.PcAktionen.Contains(k.Aktion))
            ? "PC-Knoepfe im Stehen: " + string.Join(",", spiel.Knoepfe.Select(k => k.Aktion))
            : spiel.MeinPlatz is null && !spiel.Knoepfe.Any(k => k.Aktion == StadtSpiel.AktionHinsetzen) ? "kein „Hinsetzen“ am PC-Stuhl" : null;

    private static async Task<int> HaltungAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Haltung " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Haltung " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            Messwerte.Notiere("  FEHLER: Spiel Haltung: Spielseite nicht gebaut");
            return 1;
        }
        var vorher = seite.Spiel;
        try
        {
            var spiel = HaltungWohnung(seite);
            if (spiel?.Innen is not { } innen)
            {
                Pruefe("Wohnung", false, "PC und Bett nicht aufgestellt oder Wohnung nicht betretbar");
                return funde;
            }
            await ansicht.BildFertigAsync();
            var fl = seite.Flaeche;
            await fl.NaechstesBildAsync();
            var pc = innen.Sitzgelegenheiten.FirstOrDefault(p => p.Art == SitzArt.PcStuhl);
            var bett = innen.Sitzgelegenheiten.FirstOrDefault(p => p.Art == SitzArt.Liegen);
            if (pc is null || bett is null)
            {
                Pruefe("Plaetze", false, $"PC-Stuhl {pc is not null}, Bett {bett is not null}");
                return funde;
            }
            var zeilen = spiel.Ablauf.Stand.Verlauf.Count;
            string Knoepfe() => string.Join(",", spiel.Knoepfe.Select(k => k.Aktion));
            // vor dem Tisch stehen: noch keine PC-Knoepfe, dafuer „Hinsetzen“
            var neben = (X: pc.Rechteck.X2 + 0.5f, Z: pc.Mitte.Y);
            async Task Stehen(float x, float z)
            {
                spiel.GeheZu(x, z);
                spiel.SofortAnkommen();
                await ansicht.BildFertigAsync();
            }
            await Stehen(neben.X, neben.Z);
            Pruefe("vor dem PC stehend", HaltungPcFund(spiel) is null, HaltungPcFund(spiel) ?? "nur „Hinsetzen“");

            // Maus und Finger: Stuhl antippen, „Aufstehen“ antippen; Bett antippen, woanders hin tippen
            foreach (var art in new[] { PointerType.Mouse, PointerType.Touch })
            {
                var name = art == PointerType.Mouse ? "Maus" : "Finger";
                await Stehen(neben.X, neben.Z);
                var getippt = await BodenTippenAsync(ansicht, fl, pc.Mitte, art);
                spiel.SofortAnkommen();
                await ansicht.BildFertigAsync();
                var sitzt = spiel.MeinPlatz?.Art == SitzArt.PcStuhl && seite.OrtKnoepfe.ContainsKey(Spielwelt.AktionComputer)
                    && seite.OrtKnoepfe.ContainsKey(StadtSpiel.AktionAufstehen);
                Pruefe(name + " Stuhl antippen", getippt && sitzt, getippt ? (sitzt ? "sitzt, PC-Knoepfe da" : "sitzt nicht: " + Knoepfe()) : "Stuhl nicht im Bild");
                var auf = seite.OrtKnoepfe.TryGetValue(StadtSpiel.AktionAufstehen, out var aufKnopf) && await TippenAsync(ansicht, aufKnopf, art);
                Pruefe(name + " Aufstehen", auf && spiel.MeinPlatz is null && !seite.OrtKnoepfe.ContainsKey(Spielwelt.AktionComputer),
                    spiel.MeinPlatz is null ? "steht" : "sitzt noch: " + Knoepfe());
                getippt = await BodenTippenAsync(ansicht, fl, bett.Mitte, art);
                spiel.SofortAnkommen();
                await ansicht.BildFertigAsync();
                var liegt = spiel.MeinPlatz?.Art == SitzArt.Liegen && spiel.Haltung?.Liegt == true && seite.OrtKnoepfe.ContainsKey("schlafen");
                Pruefe(name + " Bett antippen", getippt && liegt, getippt ? (liegt ? "liegt, „Schlafen“ da" : "liegt nicht: " + Knoepfe()) : "Bett nicht im Bild");
                getippt = await BodenTippenAsync(ansicht, fl, new Vector2(neben.X, neben.Z), art);
                await ansicht.BildFertigAsync();
                Pruefe(name + " woanders hin tippen", getippt && spiel.MeinPlatz is null && spiel.Haltung is null, spiel.MeinPlatz is null ? "steht auf und geht" : "liegt noch");
                spiel.SofortAnkommen();
            }

            // Eingabetaste und Screenreader: „Hinsetzen“ am Stuhl, „Hinlegen“ am Bett, je „Aufstehen“
            foreach (var weg in new[] { "Eingabetaste", "Screenreader" })
            {
                async Task<bool> Druecken(string aktion)
                {
                    await ansicht.BildFertigAsync();
                    if (!seite.OrtKnoepfe.TryGetValue(aktion, out var knopf))
                    {
                        return false;
                    }
                    if (weg == "Eingabetaste")
                    {
                        knopf.Focus(NavigationMethod.Tab);
                        knopf.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = knopf });
                    }
                    else
                    {
                        (ControlAutomationPeer.CreatePeerForElement(knopf) as IInvokeProvider)?.Invoke();
                    }
                    await ansicht.BildFertigAsync();
                    return true;
                }
                await Stehen(neben.X, neben.Z);
                var ok = await Druecken(StadtSpiel.AktionHinsetzen) && spiel.MeinPlatz?.Art == SitzArt.PcStuhl && seite.OrtKnoepfe.ContainsKey(Spielwelt.AktionComputer);
                ok = ok && await Druecken(StadtSpiel.AktionAufstehen) && spiel.MeinPlatz is null;
                Pruefe(weg + " Hinsetzen und Aufstehen", ok, ok ? "sitzt, dann steht" : Knoepfe());
                var vorBett = Vor(bett);
                await Stehen(vorBett.X, vorBett.Y);
                ok = await Druecken(StadtSpiel.AktionHinlegen) && spiel.MeinPlatz?.Art == SitzArt.Liegen && seite.OrtKnoepfe.ContainsKey("schlafen");
                ok = ok && await Druecken(StadtSpiel.AktionAufstehen) && spiel.MeinPlatz is null;
                Pruefe(weg + " Hinlegen und Aufstehen", ok, ok ? "liegt, dann steht" : Knoepfe());
            }
            var neu = spiel.Ablauf.Stand.Verlauf.Count - zeilen;
            Pruefe("nur Darstellung", neu == 0, string.Create(inv, $"{neu} neue Zeilen im Spielstand"));

            // Gegenprobe: Verhalten bis A 0.80.0 (PC-Knoepfe im Stehen) wird gemeldet
            StadtSpiel.HaltungAltFuerTests(true);
            try
            {
                await Stehen(pc.Mitte.X, pc.Mitte.Y);
                var gegen = HaltungPcFund(spiel);
                Pruefe("Gegenprobe wie bis A 0.80.0", gegen is not null, gegen ?? "KEIN Fund (Pruefung zu schwach)");
            }
            finally
            {
                StadtSpiel.HaltungAltFuerTests(false);
            }
            spiel.Verlassen();
            await ansicht.BildFertigAsync();

            // Haltestelle (4b): nur ueber „Hinsetzen“, nie von allein
            await BankAsync(ansicht, seite, Pruefe);
        }
        finally
        {
            seite.SpielSetzen(vorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }

    /// <summary>Stelle 0,6 vor einem Platz (in Blickrichtung der Figur dort, also vor dem Moebel).</summary>
    private static Vector2 Vor(Sitzgelegenheit p)
    {
        var richtung = p.Art == SitzArt.Liegen
            ? new Vector2(p.Mitte.X - p.Ort.X, p.Mitte.Y - p.Ort.Z)
            : new Vector2(MathF.Sin(p.Blick), MathF.Cos(p.Blick));
        if (p.Art == SitzArt.Liegen)
        {
            // neben das Bett: quer zur Liegerichtung bis zum Rand plus 0,4
            var quer = Vector2.Normalize(new Vector2(-richtung.Y, richtung.X));
            var halb = (MathF.Abs(quer.X) * (p.Rechteck.X2 - p.Rechteck.X1) / 2) + (MathF.Abs(quer.Y) * (p.Rechteck.Z2 - p.Rechteck.Z1) / 2);
            return p.Mitte + (quer * (halb + 0.4f));
        }
        return new Vector2(p.Ort.X, p.Ort.Z) + (richtung * 0.6f);
    }

    /// <summary>Frischer Probe-Ablauf mit aufgestelltem PC und Bett, in der Wohnung (null = nicht gelungen).</summary>
    private static StadtSpiel? HaltungWohnung(SpielSeite seite)
    {
        var spiel = Frisch(seite);
        var a = spiel.Ablauf;
        a.Log(Ereignisse.Erledigt, new Core.Daten.PyObjekt { { "aufgabe", "probe-erspartes" }, { "tag", 1L }, { "richtig", false }, { "geld", 8000L } });
        foreach (var (moebel, stueck) in new[] { (Spielwelt.PcTyp, "probe-pc"), ("bett_einzel", "probe-bett") })
        {
            a.Ausfuehren(SpielAktionen.MoebelKaufen(a.Stand, moebel, stueck));
            var gestellt = false;
            for (var y = 1.0; y < 12 && !gestellt; y += 0.5)
            {
                for (var x = 1.0; x < 16 && !gestellt; x += 0.5)
                {
                    try
                    {
                        a.Ausfuehren(SpielAktionen.AufstellenBei(a.Stand, stueck, x, y, 0L));
                        gestellt = true;
                    }
                    catch (SpielFehler)
                    {
                        // naechste Stelle
                    }
                }
            }
            if (!gestellt)
            {
                return null;
            }
        }
        return spiel.Betreten() && spiel.FlaecheId == SpielDaten.OrtZuhause ? spiel : null;
    }

    /// <summary>Auf einen Bodenpunkt der 3D-Ansicht tippen (Maus oder Finger); false = Punkt nicht im Bild.</summary>
    private static async Task<bool> BodenTippenAsync(MainView ansicht, StadtFlaeche fl, Vector2 boden, PointerType art)
    {
        await fl.NaechstesBildAsync();
        if (fl.AktuelleKamera?.Bildpunkt(new Vector3(boden.X, 0, boden.Y), (float)fl.Bounds.Width, (float)fl.Bounds.Height) is not { } b
            || b.X < 1 || b.Y < 1 || b.X > fl.Bounds.Width - 1 || b.Y > fl.Bounds.Height - 1)
        {
            return false;
        }
        var p = new Point(b.X, b.Y);
        var zeiger = new Pointer(Pointer.GetNextFreeId(), art, true);
        Druecken(fl, zeiger, p);
        Loslassen(fl, zeiger, p);
        await ansicht.BildFertigAsync();
        return true;
    }

    /// <summary>Haltestelle: hingehen, kein Sitzen von allein; „Hinsetzen“ und „Aufstehen“ ueber Maus, Finger, Taste, Screenreader.</summary>
    private static async Task BankAsync(MainView ansicht, SpielSeite seite, Action<string, bool, string> Pruefe)
    {
        var spiel = Frisch(seite);
        var h = spiel.Linie is { } linie ? spiel.Szene.Haltestellen.FirstOrDefault(x => linie.Halte.Any(l => l.Haltestelle == x.Nr)) : null;
        if (h is null)
        {
            Pruefe("Haltestelle", false, "Stadt ohne Buslinie");
            return;
        }
        spiel.GeheZu(h.Haeuschen.X, h.Haeuschen.Y);
        spiel.SofortAnkommen();
        await ansicht.BildFertigAsync();
        Pruefe("Haltestelle nicht von allein", spiel.WartetAn == h.Nr && spiel.AufDerBank is null && seite.OrtKnoepfe.ContainsKey(StadtSpiel.AktionHinsetzen),
            $"wartet an {spiel.WartetAn}, Bank {spiel.AufDerBank}, Knoepfe {string.Join(",", spiel.Knoepfe.Select(k => k.Aktion))}");
        foreach (var weg in new[] { "Maus", "Finger", "Eingabetaste", "Screenreader" })
        {
            async Task<bool> Druecken(string aktion)
            {
                await ansicht.BildFertigAsync();
                if (!seite.OrtKnoepfe.TryGetValue(aktion, out var knopf))
                {
                    return false;
                }
                switch (weg)
                {
                    case "Maus":
                        return await TippenAsync(ansicht, knopf, PointerType.Mouse);
                    case "Finger":
                        return await TippenAsync(ansicht, knopf, PointerType.Touch);
                    case "Eingabetaste":
                        knopf.Focus(NavigationMethod.Tab);
                        knopf.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = knopf });
                        break;
                    default:
                        (ControlAutomationPeer.CreatePeerForElement(knopf) as IInvokeProvider)?.Invoke();
                        break;
                }
                await ansicht.BildFertigAsync();
                return true;
            }
            var ok = await Druecken(StadtSpiel.AktionHinsetzen) && spiel.AufDerBank == h.Nr && spiel.Haltung is not null;
            ok = ok && await Druecken(StadtSpiel.AktionAufstehen) && spiel.AufDerBank is null && spiel.Haltung is null && spiel.WartetAn == h.Nr;
            Pruefe("Bank " + weg, ok, ok ? "sitzt, dann steht" : string.Join(",", spiel.Knoepfe.Select(k => k.Aktion)));
        }
    }
}
