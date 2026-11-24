using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Lernplattform.Core.Design;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 1.6.0 (Sammelliste 443/444, Weisungen P12-359/P12-360, Nico 1c 3a 4). In der Wohnung mit echten Tueren und ohne
/// Dreieck-Menue: am PC die Anzeige „F: …“ (Haupthandlung, Taste F tippen = ausfuehren, halten = Auswahl aller Handlungen, Esc
/// schliesst sie), oben rechts in der Ecke nur das X (Name „Spiel verlassen“, Flaeche in der Farbe des Programms, Symbol mit
/// Kontrast nach <see cref="Farbwelt.MindestKontrast"/>, Weisung P12-361), unten links das runde Symbol der Ich-Sicht und rechts
/// daneben das Handy-Symbol (Antippen oeffnet das Spiel-Handy); die Knoepfe des Orts, „Spiel verlassen“, „Ich-Sicht“ und das Handy
/// unten gibt es nicht. Vor einer Raumtuer: F oeffnet sie, danach
/// „F: Tür schließen“. Am Handy quer und am Tablet quer: der runde Handlungsknopf ueber dem linken Stick (Symbol Tuer, Name
/// darueber), rechts daneben das Symbol der Ich-Sicht, keiner im Kreis des Sticks, keiner auf dem anderen, die Mitte trifft den
/// Knopf; Antippen oeffnet, lang druecken zeigt die Auswahl; das Handy-Symbol rechts neben dem linken Stick (Nico 1a, nicht im
/// Kreis, nicht auf anderen Knoepfen, auf ihm beginnt kein Stick). Layoutpruefung der Seite in beiden Lagen. Gegenprobe: mit dem Menue
/// wie bis A 1.5.0 fehlen Anzeige und Knoepfe, „Spiel verlassen“ steht wieder unten.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> Handlung444Async(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Handlung 444 " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Handlung 444 " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null || TopLevel.GetTopLevel(ansicht) is not Window fenster)
        {
            Messwerte.Notiere("  FEHLER: Spiel Handlung 444: Spielseite oder Fenster fehlt");
            return 1;
        }
        if (!await GlAbwartenAsync(seite))
        {
            Messwerte.Notiere("Spiel: Handlung 444: ohne 3D bleibt das Menue (nicht gemessen)");
            return 0;
        }
        var vorher = seite.Spiel;
        var (breiteVorher, hoeheVorher) = (fenster.Width, fenster.Height);
        var zuVorher = seite.HinweisZu;
        string Name(Control c) => Avalonia.Automation.AutomationProperties.GetName(c) ?? "";
        void Tippen(DaumenKnopf k) => (ControlAutomationPeer.CreatePeerForElement(k) as IInvokeProvider)?.Invoke();
        void Taste(Key taste, bool runter) => seite.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = runter ? InputElement.KeyDownEvent : InputElement.KeyUpEvent, Key = taste, Source = seite.Flaeche,
        });
        async Task Warten()
        {
            await ansicht.BildFertigAsync();
            await Task.Delay(300);
            await ansicht.BildFertigAsync();
        }
        Rect? Rechteck(Control c) => c.IsEffectivelyVisible && c.TranslatePoint(default, seite) is { } p ? new Rect(p, c.Bounds.Size) : null;
        string FText() => seite.FAnzeige?.Content switch
        {
            TextBlock t => t.Text ?? "",
            string s => s,
            _ => "",
        };
        try
        {
            seite.MenueAltFuerTests = false;
            seite.TuerenSelbstFuerTests = false;
            seite.DaumenKnoepfeFuerTests = false;
            var spiel = HaltungWohnung(seite);
            if (spiel?.Innen is not { } innen || innen.RaumTueren.Count == 0)
            {
                Pruefe("Wohnung", false, "Wohnung nicht betretbar oder ohne Raumtuer");
                return funde;
            }
            spiel.TuerenOeffnenSelbst = false;
            if (seite.Tafel is { } t0)
            {
                t0.IsVisible = false;
            }
            var (hoechstB, hoechstH) = Hoechstgroesse(fenster);
            fenster.Width = Math.Min(1360, hoechstB);
            fenster.Height = Math.Min(880, hoechstH);
            if (seite.HinweisZu)
            {
                seite.HinweisUmschalten();
            }
            // am Ausgang: „F: Zur Stadt“, oben rechts nur das X, unten links Ich-Sicht und Handy, keine Knoepfe des Orts unten
            spiel.Ansehen(innen, innen.StartX, innen.StartZ, 0f);
            await Warten();
            var taste = Tastenbelegung.Aktuell.AnzeigeFuer(Tastenbelegung.Handeln);
            Pruefe("PC Ausgang", seite.HandlungModus && seite.FAnzeige is { IsEffectivelyVisible: true } && FText() == string.Format(inv, SpielProbeTexte.HandlungTaste, taste, SpielProbeTexte.ZurStadt),
                $"Modus {seite.HandlungModus}, Anzeige \"{FText()}\"");
            if (seite.XKnopf is not { } x || seite.IchRund is not { } ich || seite.HandyRund is not { } handy)
            {
                Pruefe("PC Knoepfe", false, $"X {seite.XKnopf is not null}, Ich {seite.IchRund is not null}, Handy {seite.HandyRund is not null}");
                return funde;
            }
            var kopf = seite.KopfRechteck();
            var rx = Rechteck(x);
            var rh = Rechteck(handy);
            var ri = Rechteck(ich);
            var leiste = seite.LeisteRechteck();
            var (xFlaeche, xSymbol) = seite.XFarben;
            var kontrast = xSymbol.Length > 0 ? Farbwelt.Kontrast(xSymbol, xFlaeche) : 0;
            var rand = SpielSeite.Rand + 0.5;
            Pruefe("PC X in der Ecke", rx is { } a && Name(x) == StadtTexte.Verlassen && a.Right >= seite.Bounds.Width - rand - 1 && a.Right <= seite.Bounds.Width + 0.5
                && a.Top <= rand + 1 && !(kopf is { } k0 && k0.Intersects(a)) && kontrast >= Farbwelt.MindestKontrast && xSymbol == Symbolfarbe.Auf(xFlaeche),
                string.Create(inv, $"X {rx}, Seite {seite.Bounds.Width:0}, Name \"{Name(x)}\", Kopf {kopf}, Flaeche {xFlaeche}, Symbol {xSymbol}, Kontrast {kontrast:0.00}"));
            Pruefe("PC Ich-Sicht unten links", ri is { } r1 && r1.Left < 40 && r1.Bottom > seite.Bounds.Height - 120 && !(leiste is { } l0 && l0.Intersects(r1))
                && Name(ich) == SpielProbeTexte.IchSichtKnopf && ich.Symbol == DaumenSymbol.Auge,
                $"Ich {ri}, Leiste {leiste}, Name \"{Name(ich)}\", Symbol {ich.Symbol}");
            Pruefe("PC Handy neben der Ich-Sicht", rh is { } r2 && ri is { } r3 && r2.Left >= r3.Right && r2.Left - r3.Right < 20 && Math.Abs(r2.Center.Y - r3.Center.Y) < 1
                && !(leiste is { } l1 && l1.Intersects(r2)) && handy.Symbol == DaumenSymbol.Telefon && Name(handy).StartsWith(SpielProbeTexte.KnopfTelefon + ", ", StringComparison.Ordinal),
                $"Handy {rh}, Ich {ri}, Leiste {leiste}, Name \"{Name(handy)}\", Zahl \"{seite.HandyZahl}\", Symbol {handy.Symbol}");
            var offenHandy = seite.HandyOffen;
            Tippen(handy);
            await ansicht.BildFertigAsync();
            var aufgegangen = seite.HandyOffen && Dialog.Offen is not null;
            Dialog.AlleSchliessen();
            await Warten();
            Pruefe("PC Handy antippen oeffnet", !offenHandy && aufgegangen && !seite.HandyOffen, $"vorher {offenHandy}, offen {aufgegangen}, danach {seite.HandyOffen}");
            var alt = new Control?[] { seite.VerlassenKnopf, seite.IchKnopf, seite.QuestKnopf };
            Pruefe("PC alte Knoepfe weg", alt.All(c => c is not { IsEffectivelyVisible: true }) && !seite.LeisteKnoepfe.Any(n => spiel.Knoepfe.Any(kn => kn.Name == n)),
                $"Verlassen {seite.VerlassenKnopf.IsEffectivelyVisible}, Ich {seite.IchKnopf.IsEffectivelyVisible}, Handy unten {seite.QuestKnopf?.IsEffectivelyVisible}, Leiste: {string.Join(", ", seite.LeisteKnoepfe)}");
            var layout = Layoutpruefung.Pruefe(seite);
            Pruefe("PC Layout", layout.Count == 0, layout.Count == 0 ? "keine Funde" : string.Join(" | ", layout.Take(5)));

            // vor einer Raumtuer: F oeffnet sie, danach „F: Tür schließen“; F halten zeigt die Auswahl, Esc schliesst sie
            var tuer = innen.RaumTueren.OrderByDescending(r => Vector2.Distance(r.Mitte, new Vector2(innen.StartX, innen.StartZ))).First();
            var vor = tuer.Punkt((tuer.Von + tuer.Bis) / 2, tuer.Linie - (tuer.Innen * 0.8f));
            spiel.Ansehen(innen, vor.X, vor.Y, 0f);
            await Warten();
            var zuText = FText();
            Pruefe("PC vor der Tuer", spiel.TuerZu(tuer) && zuText == string.Format(inv, SpielProbeTexte.HandlungTaste, taste, SpielProbeTexte.KnopfTuerOeffnen),
                $"zu {spiel.TuerZu(tuer)}, Anzeige \"{zuText}\"");
            Taste(Key.F, true);
            Taste(Key.F, false);
            await Warten();
            Pruefe("PC F oeffnet", !spiel.TuerZu(tuer) && FText() == string.Format(inv, SpielProbeTexte.HandlungTaste, taste, SpielProbeTexte.KnopfTuerSchliessen),
                $"zu {spiel.TuerZu(tuer)}, Anzeige \"{FText()}\"");
            Taste(Key.F, true);
            await Task.Delay(TimeSpan.FromSeconds(HandlungKnopf.LangAb + 0.3));
            await Warten();
            var auswahl = seite.AuswahlKnoepfe;
            Taste(Key.F, false);
            await Warten();
            Pruefe("PC F halten zeigt Auswahl", auswahl.Count == spiel.Handlungen.Count && auswahl.Count > 0 && seite.AuswahlKnoepfe.Count == auswahl.Count && !spiel.TuerZu(tuer),
                $"{auswahl.Count} Knoepfe ({string.Join(", ", auswahl.Select(b => b.Content))}), {spiel.Handlungen.Count} Handlungen, nach dem Loslassen offen {seite.AuswahlKnoepfe.Count}");
            var layoutAuswahl = Layoutpruefung.Pruefe(seite);
            Pruefe("PC Layout mit Auswahl", layoutAuswahl.Count == 0, layoutAuswahl.Count == 0 ? "keine Funde" : string.Join(" | ", layoutAuswahl.Take(5)));
            Taste(Key.Escape, true);
            await Warten();
            Pruefe("PC Esc schliesst Auswahl", seite.AuswahlKnoepfe.Count == 0 && ansicht.AktuelleSeite == Seiten.Spiel && spiel.ImInnenraum,
                $"Auswahl {seite.AuswahlKnoepfe.Count}, Seite {ansicht.AktuelleSeite}, drinnen {spiel.ImInnenraum}");
            // aus der Auswahl: „Tür schließen“ waehlen
            seite.AuswahlZeigen(true);
            await Warten();
            var schliessen = seite.AuswahlKnoepfe.FirstOrDefault(b => b.Content is string s0 && s0 == SpielProbeTexte.KnopfTuerSchliessen
                || b.Content is TextBlock { Text: SpielProbeTexte.KnopfTuerSchliessen });
            schliessen?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Warten();
            Pruefe("PC Auswahl schliesst Tuer", schliessen is not null && spiel.TuerZu(tuer) && seite.AuswahlKnoepfe.Count == 0, $"Knopf {schliessen is not null}, zu {spiel.TuerZu(tuer)}");
            // Ich-Sicht ueber das Symbol an und wieder aus
            Tippen(ich);
            await Warten();
            var an = seite.Flaeche.IchAn && Name(ich) == SpielProbeTexte.IchSichtBeenden && ich.Leuchtet;
            Tippen(ich);
            await Warten();
            Pruefe("PC Ich-Sicht Symbol", an && !seite.Flaeche.IchAn && Name(ich) == SpielProbeTexte.IchSichtKnopf, $"an {an}, danach {seite.Flaeche.IchAn}");

            // Handy quer und Tablet quer: runder Knopf ueber dem linken Stick, Ich-Sicht rechts daneben
            seite.DaumenKnoepfeFuerTests = true;
            foreach (var (geraet, w, h) in new[] { ("Handy quer (S24 Ultra)", 832.0, 384.0), ("Tablet quer (Tab A9+)", 1280.0, 800.0) })
            {
                fenster.Width = Math.Min(w, hoechstB);
                fenster.Height = Math.Min(h, hoechstH);
                spiel.Ansehen(innen, vor.X, vor.Y, 0f);
                if (!spiel.TuerZu(tuer))
                {
                    spiel.Handeln(StadtSpiel.AktionTuerZu);
                }
                await Warten();
                var punkt = string.Create(inv, $"{geraet} {fenster.Width:0}x{fenster.Height:0}");
                var fl = seite.Flaeche;
                var bereich = fl.StickBereich;
                if (seite.HandlungRund is not { } rund || seite.DaumenRechteck(rund) is not { } rr || seite.DaumenRechteck(ich) is not { } ri2 || fl.TranslatePoint(default, seite) is not { } f0)
                {
                    Pruefe(punkt + " sichtbar", false, $"Handlung {seite.HandlungRund?.IsVisible}, Ich {ich.IsVisible}");
                    continue;
                }
                var (sx, sy, sr) = Sticks.Lage(0, bereich.Hoehe, Sticks.Seite.Links, bereich with { Sperre = default, SperreLinks = default });
                bool ImKreis(Rect r)
                {
                    double dx = Math.Clamp(sx, r.Left, r.Right) - sx, dy = Math.Clamp(sy, r.Top, r.Bottom) - sy;
                    return (dx * dx) + (dy * dy) < sr * sr;
                }
                bool Trifft(DaumenKnopf knopf, Rect r) => seite.InputHitTest(r.Center + f0) as Visual is { } v && (ReferenceEquals(v, knopf) || knopf.IsVisualAncestorOf(v));
                var l2 = seite.LeisteRechteck();
                var aufLeiste = l2 is { } lr && (lr.Intersects(rr.Translate(f0)) || lr.Intersects(ri2.Translate(f0)));
                Pruefe(punkt + " Lage", !ImKreis(rr) && !ImKreis(ri2) && !rr.Intersects(ri2) && rr.Right <= bereich.Breite / 2 && ri2.Right <= bereich.Breite / 2
                    && ri2.Left > rr.Left && Trifft(rund, rr) && Trifft(ich, ri2) && !aufLeiste,
                    string.Create(inv, $"Handlung ({rr.Center.X:0}, {rr.Center.Y:0}), Ich ({ri2.Center.X:0}, {ri2.Center.Y:0}), Stick ({sx:0}, {sy:0}) Radius {sr:0}, im Kreis {ImKreis(rr)}/{ImKreis(ri2)}, Treffer {Trifft(rund, rr)}/{Trifft(ich, ri2)}, auf der Leiste {aufLeiste}"));
                Pruefe(punkt + " Symbol und Name", rund.Symbol == DaumenSymbol.Tuer && Name(rund) == SpielProbeTexte.KnopfTuerOeffnen && seite.HandlungNameText == SpielProbeTexte.KnopfTuerOeffnen
                    && seite.FAnzeige is not { IsEffectivelyVisible: true },
                    $"Symbol {rund.Symbol}, Name \"{Name(rund)}\", darueber \"{seite.HandlungNameText}\", F-Anzeige {seite.FAnzeige?.IsEffectivelyVisible}");
                Tippen(rund);
                await Warten();
                Pruefe(punkt + " Antippen oeffnet", !spiel.TuerZu(tuer) && Name(rund) == SpielProbeTexte.KnopfTuerSchliessen, $"zu {spiel.TuerZu(tuer)}, Name \"{Name(rund)}\"");
                rund.LangFuerTests();
                await Warten();
                var zahl = seite.AuswahlKnoepfe.Count;
                var layoutHandy = Layoutpruefung.Pruefe(seite);
                Pruefe(punkt + " lang druecken zeigt Auswahl", zahl == spiel.Handlungen.Count && zahl > 0, $"{zahl} Knoepfe, {spiel.Handlungen.Count} Handlungen");
                Pruefe(punkt + " Layout", layoutHandy.Count == 0, layoutHandy.Count == 0 ? "keine Funde" : string.Join(" | ", layoutHandy.Take(5)));
                seite.AuswahlZeigen(false);
                // ein Daumen auf dem Handlungsknopf setzt keinen Stick
                Pruefe(punkt + " kein Stick auf dem Knopf", Sticks.SeiteFuer(rr.Center.X, rr.Center.Y, bereich) is null, $"Seite {Sticks.SeiteFuer(rr.Center.X, rr.Center.Y, bereich)}");
                // Handy-Symbol rechts neben dem linken Stick (Nico 1a, Weisung P12-362), frei von allem
                {
                    var bh = fl.StickBereich;
                    var (hx, hy, hr) = Sticks.Lage(0, bh.Hoehe, Sticks.Seite.Links, bh with { Sperre = default, SperreLinks = default, SperreTelefon = default });
                    var p2 = $"{punkt} Handy";
                    if (seite.DaumenRechteck(handy) is not { } rhy)
                    {
                        Pruefe(p2 + " sichtbar", false, $"Handy {handy.IsVisible}");
                    }
                    else
                    {
                        var andere = new[] { seite.DaumenRechteck(rund), seite.DaumenRechteck(ich), seite.DaumenRechteck(seite.DaumenKnoepfe.Springen), seite.DaumenRechteck(seite.DaumenKnoepfe.Haltung) }
                            .OfType<Rect>().ToList();
                        double dx = Math.Clamp(hx, rhy.Left, rhy.Right) - hx, dy = Math.Clamp(hy, rhy.Top, rhy.Bottom) - hy;
                        var imKreis = (dx * dx) + (dy * dy) < hr * hr;
                        var neben = rhy.Left >= hx + hr && rhy.Right <= bh.Breite / 2;
                        var aufLeisteH = seite.LeisteRechteck() is { } lh && lh.Intersects(rhy.Translate(f0));
                        var stickAuf = Sticks.SeiteFuer(rhy.Center.X, rhy.Center.Y, bh);
                        Pruefe(p2 + " Lage", !imKreis && neben && !andere.Any(o => o.Intersects(rhy)) && !aufLeisteH && Trifft(handy, rhy) && stickAuf is null
                            && handy.Symbol == DaumenSymbol.Telefon && Name(handy).StartsWith(SpielProbeTexte.KnopfTelefon + ", ", StringComparison.Ordinal),
                            string.Create(inv, $"Handy ({rhy.Center.X:0}, {rhy.Center.Y:0}), Stick ({hx:0}, {hy:0}) Radius {hr:0}, im Kreis {imKreis}, neben {neben}, auf anderen {andere.Count(o => o.Intersects(rhy))}, Leiste {aufLeisteH}, Treffer {Trifft(handy, rhy)}, Stick darauf {stickAuf}, Name \"{Name(handy)}\""));
                    }
                }
            }

            // Gegenprobe: Menue wie bis A 1.5.0
            seite.DaumenKnoepfeFuerTests = false;
            seite.MenueAltFuerTests = true;
            seite.Beschriften();
            await Warten();
            Pruefe("Gegenprobe Menue alt", seite.FAnzeige is not { IsEffectivelyVisible: true } && !x.IsEffectivelyVisible && !ich.IsVisible && !handy.IsVisible
                && seite.VerlassenKnopf.IsEffectivelyVisible,
                $"F-Anzeige {seite.FAnzeige?.IsEffectivelyVisible}, X {x.IsEffectivelyVisible}, Ich-Symbol {ich.IsVisible}, Handy-Symbol {handy.IsVisible}, Spiel verlassen unten {seite.VerlassenKnopf.IsEffectivelyVisible}");
        }
        finally
        {
            seite.DaumenKnoepfeFuerTests = null;
            seite.MenueAltFuerTests = true;
            seite.TuerenSelbstFuerTests = true;
            seite.AuswahlZeigen(false);
            fenster.Width = breiteVorher;
            fenster.Height = hoeheVorher;
            if (seite.HinweisZu != zuVorher)
            {
                seite.HinweisUmschalten();
            }
            seite.SpielSetzen(vorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }
}
