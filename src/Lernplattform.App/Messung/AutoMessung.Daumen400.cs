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
/// Welle A 0.99.0 (Weisungen P12-319/P12-320, Sammelliste 400), seit A 1.7.0 (Sammelliste 447, Weisung P12-367, Nico 1a 2a 4a):
/// die drei runden Knoepfe am rechten Daumen (Springen gross in der Ecke, Hinknien und Hinlegen klein im Bogen darueber) am Handy
/// quer (S24 Ultra) und am Tablet quer (Tab A9+). Geprueft: alle drei sichtbar, Groesse 64/48 bzw. 76/56, ausserhalb der
/// Stick-Zonen und der Leiste, die Mitte trifft den Knopf, Springen in der Ecke, Hinknien links daneben, Hinlegen darueber; das
/// Symbol zeigt die naechste Haltung, die aktuelle leuchtet in der Grundfarbe; Hinknien tippen = hinknien, noch einmal = aufstehen,
/// lang oder nach unten wischen = hinlegen, Hinlegen tippen = hinlegen bzw. aufstehen, Springen springt. Am PC: drei kleine
/// Symbole unten rechts mit ihrer Taste darunter, mit der Maus antippbar, keine Menue-Eintraege.
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
        var (springen, haltung, liegen) = seite.DaumenKnoepfe;
        string Name(Control c) => Avalonia.Automation.AutomationProperties.GetName(c) ?? "";
        void Tippen(DaumenKnopf k) => (ControlAutomationPeer.CreatePeerForElement(k) as IInvokeProvider)?.Invoke();
        async Task Warten()
        {
            await ansicht.BildFertigAsync();
            await Task.Delay(400);
            await ansicht.BildFertigAsync();
        }
        string Akzent() => Application.Current?.TryFindResource("Pinsel.accent", out var a) == true && a is Avalonia.Media.ISolidColorBrush ab
            ? $"#{ab.Color.R:X2}{ab.Color.G:X2}{ab.Color.B:X2}" : "-";
        // ein Knopf in Ruhe (weisse Figur auf dunkler Scheibe) oder leuchtend (Grundfarbe, Symbol mit Mindestkontrast)
        bool Zeigt(DaumenKnopf k, DaumenSymbol symbol, bool leuchtet, string name)
        {
            var akzent = Akzent();
            var ok = k.Symbol == symbol && k.Leuchtet == leuchtet && Name(k) == name;
            return leuchtet
                ? ok && string.Equals(k.Farben.Fuellung, akzent, StringComparison.OrdinalIgnoreCase) && k.Farben.Symbol.StartsWith('#')
                    && Farbwelt.Kontrast(k.Farben.Symbol, akzent) >= Farbwelt.MindestKontrast
                : ok && k.Farben.Symbol == "#FFFFFF" && k.Farben.Fuellung is null;
        }
        string Zustand(StadtSpiel spiel) =>
            $"{spiel.Koerper}; Hinknien {haltung.Symbol}/{haltung.Leuchtet}/\"{Name(haltung)}\"; Hinlegen {liegen.Symbol}/{liegen.Leuchtet}/\"{Name(liegen)}\"";
        try
        {
            seite.DaumenKnoepfeFuerTests = true;
            var spiel = HaltungenWohnung(seite);
            if (spiel?.Innen is not { } innen)
            {
                Pruefe("Wohnung", false, "Sofa nicht aufgestellt oder Wohnung nicht betretbar");
                return funde;
            }
            var stelle = HaltungsStelle(spiel, innen);
            if (seite.Tafel is { } t0)
            {
                t0.IsVisible = false;
            }
            var (hoechstB, hoechstH) = Hoechstgroesse(fenster);
            foreach (var (geraet, b, h, gSoll, kSoll) in new[] { ("Handy quer (S24 Ultra)", 832.0, 384.0, 64.0, 48.0), ("Tablet quer (Tab A9+)", 1280.0, 800.0, 76.0, 56.0) })
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
                if (seite.DaumenRechteck(springen) is not { } rs || seite.DaumenRechteck(haltung) is not { } rh || seite.DaumenRechteck(liegen) is not { } rl
                    || fl.TranslatePoint(default, seite) is not { } f0)
                {
                    Pruefe(punkt + " sichtbar", false, $"Springen {springen.IsVisible}, Hinknien {haltung.IsVisible}, Hinlegen {liegen.IsVisible}");
                    continue;
                }
                // Groesse nach der kleineren Seite des Bilds (Handy 64/48, Tablet 76/56; bei kleinerem Fenster des Rechners die Regel)
                var (g, k) = HaltungKnopf.Groessen(bereich.Breite, bereich.Hoehe);
                Pruefe(punkt + " Groesse", rs.Width == g && rh.Width == k && rl.Width == k && (fenster.Width < b || fenster.Height < h || (g == gSoll && k == kSoll)),
                    string.Create(inv, $"Springen {rs.Width:0}, Hinknien {rh.Width:0}, Hinlegen {rl.Width:0} (Regel {g:0}/{k:0}, Geraet {gSoll:0}/{kSoll:0})"));
                // kein Punkt der Kreise liegt in einer Stick-Zone; die Mitte trifft den Knopf; nichts ueberlappt die Leiste oder einen anderen
                bool InZone(Rect r) => Enumerable.Range(0, 72).Any(i =>
                {
                    var w = i * Math.PI / 36;
                    var p = r.Center + new Point(Math.Cos(w) * r.Width / 2, Math.Sin(w) * r.Height / 2);
                    return Sticks.SeiteFuer(p.X, p.Y, bereich) is not null;
                });
                bool Trifft(DaumenKnopf knopf, Rect r) => seite.InputHitTest(r.Center + f0) as Visual is { } v && (ReferenceEquals(v, knopf) || knopf.IsVisualAncestorOf(v));
                bool Ueber(Rect a, Rect c) => Math.Sqrt(Math.Pow(a.Center.X - c.Center.X, 2) + Math.Pow(a.Center.Y - c.Center.Y, 2)) < (a.Width + c.Width) / 2;
                var leiste = seite.LeisteRechteck();
                var aufLeiste = leiste is { } lr && new[] { rs, rh, rl }.Any(r => lr.Intersects(r.Translate(f0)));
                Pruefe(punkt + " Lage", !InZone(rs) && !InZone(rh) && !InZone(rl) && Trifft(springen, rs) && Trifft(haltung, rh) && Trifft(liegen, rl) && !aufLeiste
                    && !Ueber(rs, rh) && !Ueber(rs, rl) && !Ueber(rh, rl) && new[] { rs, rh, rl }.All(r => r.Center.X > bereich.Breite / 2),
                    string.Create(inv, $"Springen ({rs.Center.X:0}, {rs.Center.Y:0}), Hinknien ({rh.Center.X:0}, {rh.Center.Y:0}), Hinlegen ({rl.Center.X:0}, {rl.Center.Y:0}), Bild {bereich.Breite:0}x{bereich.Hoehe:0}, in Stick-Zone {InZone(rs)}/{InZone(rh)}/{InZone(rl)}, Treffer {Trifft(springen, rs)}/{Trifft(haltung, rh)}/{Trifft(liegen, rl)}, auf der Leiste {aufLeiste}"));
                // Anordnung B (Nico 1a): Springen in der Ecke (Rand 16 nach rechts und unten bzw. ueber der Leiste), Hinknien links daneben
                // etwas hoeher, Hinlegen fast senkrecht darueber
                var unten = bereich.Hoehe - HaltungKnopf.Rand;
                var obenLeiste = bereich.Hoehe - bereich.Unten - HaltungKnopf.Rand;
                var ecke = Math.Abs(rs.Right - (bereich.Breite - HaltungKnopf.Rand)) < 1 && (Math.Abs(rs.Bottom - unten) < 1 || Math.Abs(rs.Bottom - obenLeiste) < 1);
                var bogen = rh.Center.X < rs.Center.X && rh.Center.Y < rs.Center.Y && rl.Center.Y < rh.Center.Y && rl.Center.X > rh.Center.X && rl.Center.X < rs.Center.X;
                Pruefe(punkt + " Anordnung B", ecke && bogen, string.Create(inv, $"Springen rechts {rs.Right:0} unten {rs.Bottom:0} (Bild {bereich.Breite:0}x{bereich.Hoehe:0}, Leiste {bereich.Unten:0}), Bogen {bogen}"));
                Pruefe(punkt + " Ruhe", Zeigt(springen, DaumenSymbol.Springen, false, SpielProbeTexte.KnopfSpringen) && Zeigt(haltung, DaumenSymbol.Knien, false, SpielProbeTexte.KnopfHinknien)
                    && Zeigt(liegen, DaumenSymbol.Liegen, false, SpielProbeTexte.KnopfHinlegen), Zustand(spiel) + $", Farben {springen.Farben}/{haltung.Farben}/{liegen.Farben}");

                // Hinknien antippen: kniet, Hinknien leuchtet und zeigt Aufstehen
                Tippen(haltung);
                await Warten();
                Pruefe(punkt + " Hinknien tippen kniet", spiel.Koerper == Koerperhaltung.Ducken && Zeigt(haltung, DaumenSymbol.Aufstehen, true, SpielProbeTexte.KnopfAufstehen)
                    && Zeigt(liegen, DaumenSymbol.Liegen, false, SpielProbeTexte.KnopfHinlegen), Zustand(spiel) + $", Farben {haltung.Farben}, Grundfarbe {Akzent()}");
                // Hinlegen antippen (kniend): liegt, Hinlegen leuchtet und zeigt Aufstehen
                Tippen(liegen);
                await Warten();
                Pruefe(punkt + " Hinlegen tippen liegt", spiel.Koerper == Koerperhaltung.Kriechen && Zeigt(liegen, DaumenSymbol.Aufstehen, true, SpielProbeTexte.KnopfAufstehen)
                    && Zeigt(haltung, DaumenSymbol.Knien, false, SpielProbeTexte.KnopfHinknien), Zustand(spiel));
                // Hinknien lang (liegend): bleibt liegen
                haltung.LangFuerTests();
                await Warten();
                Pruefe(punkt + " Hinknien lang im Liegen bleibt", spiel.Koerper == Koerperhaltung.Kriechen, Zustand(spiel));
                // Hinlegen antippen (liegend): steht auf
                Tippen(liegen);
                await Warten();
                Pruefe(punkt + " Hinlegen tippen steht auf", spiel.Koerper == Koerperhaltung.Stehen && Zeigt(liegen, DaumenSymbol.Liegen, false, SpielProbeTexte.KnopfHinlegen), Zustand(spiel));
                // Hinknien lang (stehend): gleich hinlegen; Hinknien antippen (liegend): kniet; noch einmal: steht
                haltung.LangFuerTests();
                await Warten();
                var liegtLang = spiel.Koerper == Koerperhaltung.Kriechen;
                Tippen(haltung);
                await Warten();
                var kniet = spiel.Koerper == Koerperhaltung.Ducken;
                Tippen(haltung);
                await Warten();
                Pruefe(punkt + " Hinknien lang legt hin", liegtLang && kniet && spiel.Koerper == Koerperhaltung.Stehen, $"liegt {liegtLang}, kniet {kniet}, danach {spiel.Koerper}");
                // nach unten wischen (Nico 2a): ab 24 Punkten hinlegen; Gegenprobe: 10 Punkte tun nichts
                haltung.WischenFuerTests(10);
                await Warten();
                var kurz = spiel.Koerper;
                haltung.WischenFuerTests(30);
                await Warten();
                var gewischt = spiel.Koerper;
                Tippen(liegen);
                await Warten();
                Pruefe(punkt + " Hinknien nach unten wischen legt hin", kurz == Koerperhaltung.Stehen && gewischt == Koerperhaltung.Kriechen && spiel.Koerper == Koerperhaltung.Stehen,
                    $"10 Punkte {kurz}, 30 Punkte {gewischt}, danach {spiel.Koerper}");
                // Springen
                Tippen(springen);
                var sprang = spiel.Springt;
                for (var i = 0; i < 90 && spiel.Springt; i++)
                {
                    await fl.NaechstesBildAsync();
                }
                Pruefe(punkt + " Springen", sprang && !spiel.Springt, $"sprang {sprang}");
            }

            // PC (Nico 4a): drei kleine Symbole unten rechts mit ihrer Taste darunter, mit der Maus antippbar; keine Menue-Eintraege
            seite.DaumenKnoepfeFuerTests = false;
            seite.SpielSetzen(spiel);
            if (seite.HinweisZu)
            {
                seite.HinweisUmschalten();
            }
            fenster.Width = Math.Min(1280, hoechstB);
            fenster.Height = Math.Min(800, hoechstH);
            await Warten();
            var drei = new[] { SpielProbeTexte.KnopfSpringen, SpielProbeTexte.KnopfDucken, SpielProbeTexte.KnopfKriechen, SpielProbeTexte.KnopfHinknien };
            var pcB = seite.Flaeche.StickBereich;
            var (pg, pk) = HaltungKnopf.GroessenTasten;
            if (seite.DaumenRechteck(springen) is not { } ps || seite.DaumenRechteck(haltung) is not { } ph || seite.DaumenRechteck(liegen) is not { } pl)
            {
                Pruefe("PC: drei Symbole sichtbar", false, $"Springen {springen.IsVisible}, Hinknien {haltung.IsVisible}, Hinlegen {liegen.IsVisible}");
            }
            else
            {
                var tasten = new[] { (HaltungKnopf.Knopf.Springen, Tastenbelegung.Springen, ps), (HaltungKnopf.Knopf.Knien, Tastenbelegung.Ducken, ph), (HaltungKnopf.Knopf.Liegen, Tastenbelegung.Kriechen, pl) }
                    .Select(t => (t.Item1, Soll: Tastenbelegung.Aktuell.Tasten(t.Item2).Count > 0 ? Tastenbelegung.Aktuell.AnzeigeFuer(t.Item2) : "–", Knopf: t.Item3, Schild: seite.TastenSchild(t.Item1))).ToList();
                var schilderOk = tasten.All(t => t.Schild is { } sch && sch.Text == t.Soll && sch.Lage.Top >= t.Knopf.Bottom && sch.Lage.Bottom <= pcB.Hoehe
                    && Math.Abs(sch.Lage.Center.X - t.Knopf.Center.X) < 1);
                Pruefe("PC: drei Symbole mit Taste unten rechts", ps.Width == pg && ph.Width == pk && pl.Width == pk && Math.Abs(ps.Right - (pcB.Breite - HaltungKnopf.Rand)) < 1
                    && pl.Right < ph.Left && ph.Right < ps.Left && schilderOk && Zeigt(haltung, DaumenSymbol.Knien, false, SpielProbeTexte.KnopfHinknien) && !seite.LeisteKnoepfe.Any(drei.Contains),
                    string.Join(", ", tasten.Select(t => $"{t.Item1} {t.Knopf.Center.X:0}/{t.Knopf.Center.Y:0} Taste \"{t.Schild?.Text}\" soll \"{t.Soll}\"")) + $", Leiste: {string.Join(", ", seite.LeisteKnoepfe)}");
                // mit der Maus antippen: wie die Taste C
                Tippen(haltung);
                await Warten();
                var pcKniet = spiel.Koerper == Koerperhaltung.Ducken && Zeigt(haltung, DaumenSymbol.Aufstehen, true, SpielProbeTexte.KnopfAufstehen);
                Tippen(haltung);
                await Warten();
                Pruefe("PC: Symbol antippen kniet, noch einmal steht auf", pcKniet && spiel.Koerper == Koerperhaltung.Stehen, Zustand(spiel));
            }
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
