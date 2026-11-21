using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Lernplattform.Core.Spiel;
using Lernplattform.Core;
using Lernplattform.Core.Design;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.99.0 (Weisungen P12-319/P12-320, Sammelliste 400): die zwei runden Knoepfe am rechten Daumen (Springen gross,
/// Haltung klein) wie am Handy und Tablet, am Handy quer (S24 Ultra) und am Tablet quer (Tab A9+). Geprueft: beide sichtbar,
/// Groesse 64/46 bzw. 72/52, ausserhalb der Stick-Zonen und der Leiste, die Mitte trifft den Knopf; Antippen duckt (Symbol
/// „Ducken“, leuchtet, Name „Aufstehen“), lang druecken kriecht, Antippen steht auf, Springen springt; die Menue-Eintraege
/// fehlen. Gegenprobe: ohne runde Knoepfe (PC) sind sie weg und die drei Menue-Eintraege da.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> Daumen400Async(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Daumen 400 " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Daumen 400 " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null || TopLevel.GetTopLevel(ansicht) is not Window fenster)
        {
            Messwerte.Notiere("  FEHLER: Spiel Daumen 400: Spielseite oder Fenster fehlt");
            return 1;
        }
        if (!await GlAbwartenAsync(seite))
        {
            Messwerte.Notiere("Spiel: Daumen 400: ohne 3D keine runden Knoepfe (nicht gemessen)");
            return 0;
        }
        var vorher = seite.Spiel;
        var (breiteVorher, hoeheVorher) = (fenster.Width, fenster.Height);
        var tafelVorher = seite.Tafel?.IsVisible ?? false;
        // der Zustand „zugeklappt“ ist eine Einstellung: danach wie vorher (sonst fehlen spaeteren Pruefungen die Knoepfe)
        var zuVorher = seite.HinweisZu;
        var (springen, haltung) = seite.DaumenKnoepfe;
        string Name(Control c) => Avalonia.Automation.AutomationProperties.GetName(c) ?? "";
        void Tippen(DaumenKnopf k) => (ControlAutomationPeer.CreatePeerForElement(k) as IInvokeProvider)?.Invoke();
        async Task Warten()
        {
            await ansicht.BildFertigAsync();
            await Task.Delay(400);
            await ansicht.BildFertigAsync();
        }
        try
        {
            seite.DaumenKnoepfeFuerTests = true;
            var spiel = HaltungenWohnung(seite);
            if (spiel?.Innen is not { } innen)
            {
                Pruefe("Wohnung", false, "Sofa nicht aufgestellt oder Wohnung nicht betretbar");
                return funde;
            }
            var stelle = Enumerable.Range(0, 900).Select(i => new Vector2(0.6f + (i % 30 * 0.3f), 0.6f + (i / 30 * 0.3f)))
                .FirstOrDefault(p => p.X < innen.Breite && p.Y < innen.Tiefe
                    && Enumerable.Range(0, 24).All(r => innen.KoerperFrei(p, r * MathF.PI / 12f, Koerperhaltung.Kriechen)
                        && innen.KoerperFrei(p, r * MathF.PI / 12f, Koerperhaltung.Ducken)) && Enumerable.Range(0, 8).All(r => innen.SprungFrei(p, r * MathF.PI / 4f)) && spiel.GeheZu(p.X, p.Y));
            spiel.SofortAnkommen();
            if (seite.Tafel is { } t0)
            {
                t0.IsVisible = false;
            }
            var (hoechstB, hoechstH) = Hoechstgroesse(fenster);
            foreach (var (geraet, b, h, gSoll, kSoll) in new[] { ("Handy quer (S24 Ultra)", 832.0, 384.0, 56.0, 40.0), ("Tablet quer (Tab A9+)", 1280.0, 800.0, 64.0, 46.0) })
            {
                fenster.Width = Math.Min(b, hoechstB);
                fenster.Height = Math.Min(h, hoechstH);
                if (!seite.HinweisZu)
                {
                    seite.HinweisUmschalten();
                }
                if (seite.Tafel is { IsVisible: true } t)
                {
                    t.IsVisible = false;
                }
                await Warten();
                var fl = seite.Flaeche;
                var bereich = fl.StickBereich;
                var punkt = string.Create(inv, $"{geraet} {fenster.Width:0}x{fenster.Height:0}");
                if (seite.DaumenRechteck(springen) is not { } rs || seite.DaumenRechteck(haltung) is not { } rh || fl.TranslatePoint(default, seite) is not { } f0)
                {
                    Pruefe(punkt + " sichtbar", false, $"Springen {springen.IsVisible}, Haltung {haltung.IsVisible}");
                    continue;
                }
                // Groesse nach der kleineren Seite des Bilds (seit A 1.0.0 Handy 56/40, Tablet 64/46; bei kleinerem Fenster des Rechners die Regel)
                var (g, k) = HaltungKnopf.Groesse(bereich.Breite, bereich.Hoehe);
                Pruefe(punkt + " Groesse", rs.Width == g && rh.Width == k && (fenster.Width < b || fenster.Height < h || (g == gSoll && k == kSoll)),
                    string.Create(inv, $"Springen {rs.Width:0}, Haltung {rh.Width:0} (Regel {g:0}/{k:0}, Geraet {gSoll:0}/{kSoll:0})"));
                // kein Punkt der Kreise liegt in einer Stick-Zone; die Mitte trifft den Knopf; nichts ueberlappt die Leiste
                bool InZone(Rect r) => Enumerable.Range(0, 72).Any(i =>
                {
                    var w = i * Math.PI / 36;
                    var p = r.Center + new Point(Math.Cos(w) * r.Width / 2, Math.Sin(w) * r.Height / 2);
                    return Sticks.SeiteFuer(p.X, p.Y, bereich) is not null;
                });
                bool Trifft(DaumenKnopf knopf, Rect r) => seite.InputHitTest(r.Center + f0) as Visual is { } v && (ReferenceEquals(v, knopf) || knopf.IsVisualAncestorOf(v));
                var leiste = seite.LeisteRechteck();
                var aufLeiste = leiste is { } lr && (lr.Intersects(rs.Translate(f0)) || lr.Intersects(rh.Translate(f0)));
                Pruefe(punkt + " Lage", !InZone(rs) && !InZone(rh) && Trifft(springen, rs) && Trifft(haltung, rh) && !aufLeiste && !rs.Intersects(rh)
                    && rs.Center.X > bereich.Breite / 2 && rh.Center.X > bereich.Breite / 2,
                    string.Create(inv, $"Springen ({rs.Center.X:0}, {rs.Center.Y:0}), Haltung ({rh.Center.X:0}, {rh.Center.Y:0}), Bild {bereich.Breite:0}x{bereich.Hoehe:0}, in Stick-Zone {InZone(rs)}/{InZone(rh)}, Treffer {Trifft(springen, rs)}/{Trifft(haltung, rh)}, auf der Leiste {aufLeiste}"));
                // A 1.0.0 (Sammelliste 405): Springen unten rechts in der Ecke (Rand 16), sonst ueber der Leiste
                var ecke = Math.Abs(rs.Right - (bereich.Breite - HaltungKnopf.Rand)) < 1 && (Math.Abs(rs.Bottom - (bereich.Hoehe - HaltungKnopf.Rand)) < 1
                    || Math.Abs(rs.Bottom - (bereich.Hoehe - bereich.Unten - HaltungKnopf.Rand)) < 1);
                Pruefe(punkt + " Ecke", ecke, string.Create(inv, $"Springen rechts {rs.Right:0} unten {rs.Bottom:0}, Bild {bereich.Breite:0}x{bereich.Hoehe:0}, Leiste unten {bereich.Unten:0}"));
                Pruefe(punkt + " Ruhe", haltung.Symbol == DaumenSymbol.Stehen && !haltung.Leuchtet && Name(haltung) == SpielProbeTexte.KnopfDucken && Name(springen) == SpielProbeTexte.KnopfSpringen,
                    $"Symbol {haltung.Symbol}, leuchtet {haltung.Leuchtet}, Namen \"{Name(springen)}\", \"{Name(haltung)}\"");

                // Antippen: Ducken, Symbol, Leuchten in der Grundfarbe, Name „Aufstehen“
                Tippen(haltung);
                await Warten();
                var akzent = Application.Current?.TryFindResource("Pinsel.accent", out var a) == true && a is Avalonia.Media.ISolidColorBrush ab
                    ? $"#{ab.Color.R:X2}{ab.Color.G:X2}{ab.Color.B:X2}" : "-";
                var kontrast = haltung.Farben.Symbol.StartsWith('#') && akzent.StartsWith('#') ? Farbwelt.Kontrast(haltung.Farben.Symbol, akzent) : 0;
                Pruefe(punkt + " Antippen duckt", spiel.Koerper == Koerperhaltung.Ducken && haltung.Symbol == DaumenSymbol.Ducken && haltung.Leuchtet
                    && string.Equals(haltung.Farben.Fuellung, akzent, StringComparison.OrdinalIgnoreCase) && kontrast >= Farbwelt.MindestKontrast && Name(haltung) == SpielProbeTexte.KnopfAufstehen,
                    $"{spiel.Koerper}, Symbol {haltung.Symbol}, leuchtet {haltung.Leuchtet} ({haltung.Farben.Fuellung}, Grundfarbe {akzent}, Symbolfarbe {haltung.Farben.Symbol}, Kontrast {kontrast:0.00}, mindestens {Farbwelt.MindestKontrast}), Name \"{Name(haltung)}\"");
                // lang druecken (geduckt): Kriechen
                haltung.LangFuerTests();
                await Warten();
                Pruefe(punkt + " Lang druecken kriecht", spiel.Koerper == Koerperhaltung.Kriechen && haltung.Symbol == DaumenSymbol.Kriechen && haltung.Leuchtet && Name(haltung) == SpielProbeTexte.KnopfAufstehen,
                    $"{spiel.Koerper}, Symbol {haltung.Symbol}, leuchtet {haltung.Leuchtet}");
                // lang druecken im Kriechen: nichts
                haltung.LangFuerTests();
                await Warten();
                Pruefe(punkt + " Lang im Kriechen bleibt", spiel.Koerper == Koerperhaltung.Kriechen, $"{spiel.Koerper}");
                // Antippen: Aufstehen
                Tippen(haltung);
                await Warten();
                Pruefe(punkt + " Antippen steht auf", spiel.Koerper == Koerperhaltung.Stehen && haltung.Symbol == DaumenSymbol.Stehen && !haltung.Leuchtet && Name(haltung) == SpielProbeTexte.KnopfDucken,
                    $"{spiel.Koerper}, Symbol {haltung.Symbol}, leuchtet {haltung.Leuchtet}");
                // lang druecken im Stehen: gleich Kriechen; dann wieder auf
                haltung.LangFuerTests();
                await Warten();
                var kriechtAusStand = spiel.Koerper == Koerperhaltung.Kriechen;
                Tippen(haltung);
                await Warten();
                Pruefe(punkt + " Lang im Stehen kriecht", kriechtAusStand && spiel.Koerper == Koerperhaltung.Stehen, $"kriecht {kriechtAusStand}, danach {spiel.Koerper}");
                // Springen
                Tippen(springen);
                var sprang = spiel.Springt;
                for (var i = 0; i < 90 && spiel.Springt; i++)
                {
                    await fl.NaechstesBildAsync();
                }
                Pruefe(punkt + " Springen", sprang && !spiel.Springt, $"sprang {sprang}");
            }

            // ohne runde Knoepfe (PC) sind sie unsichtbar; seit A 1.0.0 (Sammelliste 404) gibt es auch keine Menue-Eintraege mehr
            seite.DaumenKnoepfeFuerTests = false;
            seite.SpielSetzen(spiel);
            if (seite.HinweisZu)
            {
                seite.HinweisUmschalten();
            }
            await Warten();
            var drei = new[] { SpielProbeTexte.KnopfSpringen, SpielProbeTexte.KnopfDucken, SpielProbeTexte.KnopfKriechen };
            Pruefe("PC: runde Knoepfe weg, keine Menue-Eintraege", !springen.IsVisible && !haltung.IsVisible && !seite.LeisteKnoepfe.Any(drei.Contains),
                $"rund {springen.IsVisible}/{haltung.IsVisible}, Leiste: {string.Join(", ", seite.LeisteKnoepfe)}");
        }
        finally
        {
            seite.DaumenKnoepfeFuerTests = null;
            fenster.Width = breiteVorher;
            fenster.Height = hoeheVorher;
            if (seite.HinweisZu != zuVorher)
            {
                seite.HinweisUmschalten();
            }
            seite.SpielSetzen(vorher);
            if (seite.Tafel is { } t1)
            {
                t1.IsVisible = tafelVorher;
            }
            await ansicht.BildFertigAsync();
        }
        return funde;
    }
}
