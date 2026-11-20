using System.Globalization;
using System.Numerics;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.93.0 „Haltungen“ (Weisung P12-303, Sammelliste 352) ueber echte Bedienung in der Wohnung (frischer Probe-Ablauf mit
/// Sofa, nur im Speicher): „Springen“ mit der Leertaste, „Ducken“ ueber die Automation (wie ein Screenreader), „Kriechen“ mit X,
/// „Aufstehen“ mit Mausklick; Namen der Knoepfe wechseln, Ansagen stimmen; Gehen beendet die Haltung; in der Ich-Sicht sinkt das
/// Auge geduckt; auf dem Sofa „Zurücklehnen“ und „Aufrichten“. Dreiecke und Zeichenaufrufe sind in jeder Haltung dieselben wie im
/// Stehen (die Grenzen aller Stufen bleiben so unveraendert). Nur Darstellung: keine neue Zeile im Spielstand. Ohne 3D: keine Knoepfe.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> HaltungenAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Haltungen " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Haltungen " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            Messwerte.Notiere("  FEHLER: Spiel Haltungen: Spielseite nicht gebaut");
            return 1;
        }
        var vorher = seite.Spiel;
        var (springen, ducken, kriechen) = seite.HaltungKnoepfe;
        if (!await GlAbwartenAsync(seite))
        {
            Pruefe("ohne 3D keine Knoepfe", !springen.IsVisible && !ducken.IsVisible && !kriechen.IsVisible,
                $"sichtbar {springen.IsVisible}/{ducken.IsVisible}/{kriechen.IsVisible}");
            return funde;
        }
        try
        {
            var spiel = HaltungenWohnung(seite);
            if (spiel?.Innen is not { } innen)
            {
                Pruefe("Wohnung", false, "Sofa nicht aufgestellt oder Wohnung nicht betretbar");
                return funde;
            }
            var fl = seite.Flaeche;
            var zeilen = spiel.Ablauf.Stand.Verlauf.Count;
            async Task Bilder(int n)
            {
                for (var i = 0; i < n; i++)
                {
                    await fl.NaechstesBildAsync();
                }
            }
            string Name(Button b) => Avalonia.Automation.AutomationProperties.GetName(b) ?? "";
            void Taste(Key taste) => seite.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = taste, Source = fl });
            // eine Stelle mitten im Raum, an der Ducken und Kriechen Platz haben; seit A 0.98.0 (Sammelliste 396) dreht sich die Figur
            // dafuer nicht mehr von selbst, darum in jeder Richtung (15 Grad), in der sie nach dem Hingehen stehen kann
            var stelle = Enumerable.Range(0, 900).Select(i => new Vector2(0.6f + (i % 30 * 0.3f), 0.6f + (i / 30 * 0.3f)))
                .FirstOrDefault(p => p.X < innen.Breite && p.Y < innen.Tiefe
                    && Enumerable.Range(0, 24).All(r => innen.KoerperFrei(p, r * MathF.PI / 12f, Koerperhaltung.Kriechen)
                        && innen.KoerperFrei(p, r * MathF.PI / 12f, Koerperhaltung.Ducken)) && Enumerable.Range(0, 8).All(r => innen.SprungFrei(p, r * MathF.PI / 4f)) && spiel.GeheZu(p.X, p.Y));
            spiel.SofortAnkommen();
            await ansicht.BildFertigAsync();
            await Bilder(3);
            Pruefe("Knoepfe und Namen", springen.IsVisible && ducken.IsVisible && kriechen.IsVisible && Name(springen) == SpielProbeTexte.KnopfSpringen
                && Name(ducken) == SpielProbeTexte.KnopfDucken && Name(kriechen) == SpielProbeTexte.KnopfKriechen,
                $"\"{Name(springen)}\", \"{Name(ducken)}\", \"{Name(kriechen)}\" an ({stelle.X:0.00}, {stelle.Y:0.00})");
            var (d0, a0) = (fl.LetzteDreiecke, fl.LetzteZeichenaufrufe);

            // Leertaste: Sprung, Weg und Stelle bleiben, nach der Dauer wieder am Boden
            var ort = (spiel.FigurX, spiel.FigurZ);
            Taste(Key.Space);
            var hoch = 0f;
            var dreieckeSprung = 0L;
            for (var i = 0; i < 90 && spiel.Springt; i++)
            {
                await fl.NaechstesBildAsync();
                hoch = MathF.Max(hoch, spiel.SprungHoehe);
                dreieckeSprung = Math.Max(dreieckeSprung, fl.LetzteDreiecke);
            }
            Pruefe("Leertaste springt", hoch > Innenraum.SprungHoch * 0.5f && !spiel.Springt && ort == (spiel.FigurX, spiel.FigurZ) && Ansage.Zuletzt == SpielProbeTexte.DuSpringst
                && dreieckeSprung == d0, string.Create(inv, $"hoechste Hoehe {hoch:0.000}, Stelle gleich {ort == (spiel.FigurX, spiel.FigurZ)}, Ansage \"{Ansage.Zuletzt}\", Dreiecke {dreieckeSprung} (stehend {d0})"));

            // Screenreader: „Ducken“ ueber den Automation-Peer, der Name wird „Aufstehen“
            (ControlAutomationPeer.CreatePeerForElement(ducken) as IInvokeProvider)?.Invoke();
            await Bilder(3);
            Pruefe("Ducken (Screenreader)", spiel.Koerper == Koerperhaltung.Ducken && Name(ducken) == SpielProbeTexte.KnopfAufstehen && Ansage.Zuletzt == SpielProbeTexte.DuDuckst
                && fl.LetzteDreiecke == d0 && fl.LetzteZeichenaufrufe == a0,
                string.Create(inv, $"{spiel.Koerper}, Name \"{Name(ducken)}\", Ansage \"{Ansage.Zuletzt}\", {fl.LetzteDreiecke}/{fl.LetzteZeichenaufrufe} (stehend {d0}/{a0})"));

            // Taste X: von Ducken in Kriechen
            Taste(Key.X);
            await Bilder(3);
            Pruefe("Taste X kriecht", spiel.Koerper == Koerperhaltung.Kriechen && Name(kriechen) == SpielProbeTexte.KnopfAufstehen && Name(ducken) == SpielProbeTexte.KnopfDucken
                && fl.LetzteDreiecke == d0 && fl.LetzteZeichenaufrufe == a0, string.Create(inv, $"{spiel.Koerper}, {fl.LetzteDreiecke}/{fl.LetzteZeichenaufrufe}"));

            // Mausklick auf „Aufstehen“ (der Knopf „Kriechen“)
            kriechen.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Bilder(2);
            Pruefe("Aufstehen mit Klick", spiel.Koerper == Koerperhaltung.Stehen && Name(kriechen) == SpielProbeTexte.KnopfKriechen && Ansage.Zuletzt == SpielProbeTexte.DuStehst,
                $"{spiel.Koerper}, Ansage \"{Ansage.Zuletzt}\"");

            // Gehen beendet die Haltung
            Taste(Key.C);
            var geduckt = spiel.Koerper == Koerperhaltung.Ducken;
            spiel.GeheZu(stelle.X + 0.6, stelle.Y);
            await Bilder(2);
            spiel.SofortAnkommen();
            await Bilder(2);
            Pruefe("Gehen beendet Ducken", geduckt && spiel.Koerper == Koerperhaltung.Stehen && spiel.HaltungModell is null, $"geduckt {geduckt}, danach {spiel.Koerper}");

            // Ich-Sicht: geduckt sinkt das Auge
            spiel.GeheZu(stelle.X, stelle.Y);
            spiel.SofortAnkommen();
            var k = fl.AktuelleKamera!;
            fl.IchUmschalten();
            for (var i = 0; i < 200 && k.IchAnteil != k.IchZiel; i++)
            {
                await fl.NaechstesBildAsync();
            }
            await Bilder(2);
            var augeStehend = k.IchAuge.Y;
            seite.HaltungAusfuehren(Tastenbelegung.Ducken);
            await Bilder(3);
            var augeGeduckt = k.IchAuge.Y;
            Pruefe("Ich-Sicht: Auge sinkt geduckt", spiel.Koerper == Koerperhaltung.Ducken && augeGeduckt < augeStehend - (0.15f * Innenraum.FigurHoehe) && k.Ich,
                string.Create(inv, $"Auge stehend {augeStehend:0.000}, geduckt {augeGeduckt:0.000}, Ich {k.Ich}"));
            seite.HaltungAusfuehren(Tastenbelegung.Ducken);
            fl.IchUmschalten();
            for (var i = 0; i < 200 && k.IchAnteil != k.IchZiel; i++)
            {
                await fl.NaechstesBildAsync();
            }

            // Sofa: Hinsetzen, Zuruecklehnen, Aufrichten (Knoepfe unten, Screenreader)
            var sofa = innen.Sitzgelegenheiten.FirstOrDefault(p => p.Art == SitzArt.Sitzen && innen.ZurueckZu(p) is not null);
            var zurueckOk = false;
            var sofaText = "kein Sofa";
            if (sofa is not null)
            {
                var vor = sofa.Ort + (new Vector3(MathF.Sin(sofa.Blick), 0, MathF.Cos(sofa.Blick)) * 0.8f);
                spiel.GeheZu(vor.X, vor.Z);
                spiel.SofortAnkommen();
                async Task<bool> Druecken(string aktion)
                {
                    await ansicht.BildFertigAsync();
                    if (!seite.OrtKnoepfe.TryGetValue(aktion, out var knopf))
                    {
                        return false;
                    }
                    (ControlAutomationPeer.CreatePeerForElement(knopf) as IInvokeProvider)?.Invoke();
                    await ansicht.BildFertigAsync();
                    return true;
                }
                var schritte = new List<string>();
                async Task<bool> Schritt(string aktion, Func<bool> danach)
                {
                    var ok = await Druecken(aktion) && danach();
                    schritte.Add($"{aktion} {(ok ? "ok" : "NEIN")} (Haltung {spiel.HaltungArt}, Knoepfe {string.Join(",", spiel.Knoepfe.Select(x => x.Aktion))})");
                    return ok;
                }
                zurueckOk = await Schritt(StadtSpiel.AktionHinsetzen, () => spiel.HaltungArt == SitzArt.Sitzen)
                    && await Schritt(StadtSpiel.AktionZuruecklehnen, () => spiel.HaltungArt == SitzArt.Zurueck && !springen.IsVisible)
                    && await Schritt(StadtSpiel.AktionAufrichten, () => spiel.HaltungArt == SitzArt.Sitzen)
                    && await Schritt(StadtSpiel.AktionAufstehen, () => spiel.HaltungArt is null);
                sofaText = string.Join("; ", schritte);
            }
            Pruefe("Sofa zuruecklehnen und aufrichten", zurueckOk, sofaText);

            var neu = spiel.Ablauf.Stand.Verlauf.Count - zeilen;
            Pruefe("nur Darstellung", neu == 0, string.Create(inv, $"{neu} neue Zeilen im Spielstand"));
        }
        finally
        {
            Dialog.AlleSchliessen();
            seite.SpielSetzen(vorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }

    /// <summary>Frischer Probe-Ablauf mit aufgestelltem Sofa, in der Wohnung (null = nicht gelungen).</summary>
    private static StadtSpiel? HaltungenWohnung(SpielSeite seite)
    {
        var spiel = Frisch(seite);
        var a = spiel.Ablauf;
        a.Log(Ereignisse.Erledigt, new Core.Daten.PyObjekt { { "aufgabe", "probe-erspartes" }, { "tag", 1L }, { "richtig", false }, { "geld", 8000L } });
        a.Ausfuehren(SpielAktionen.MoebelKaufen(a.Stand, "sofa", "probe-sofa"));
        for (var y = 1.0; y < 12; y += 0.5)
        {
            for (var x = 1.0; x < 16; x += 0.5)
            {
                try
                {
                    a.Ausfuehren(SpielAktionen.AufstellenBei(a.Stand, "probe-sofa", x, y, 0L));
                    return spiel.Betreten() && spiel.FlaecheId == SpielDaten.OrtZuhause ? spiel : null;
                }
                catch (SpielFehler)
                {
                    // naechste Stelle
                }
            }
        }
        return null;
    }
}
