using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 1.13.0 (Sammelliste 464, Weisung P12-394, Nico 1a 2a 3a 4a 5a): der Hinweis steht oben neben der Uhr. Abnahmesaetze:
/// B-1 der Text (ganz, nur Titel, aufgeklappt) ueberschneidet weder die Figur noch den Weg vor ihr (Spalte unter der Figur bis zum
/// unteren Rand) noch die Steuer-Knoepfe und den Bereich der Sticks, je Geraet (Handy quer und hoch, Tablet quer, PC) und Schrift
/// normal und riesig; Gegenprobe mit dem Hinweisfeld unten (<see cref="SpielSeite.HinweisfeldAltFuerTests"/>) schlaegt an.
/// B-2 kein Dreieck (Symbol und Name „Hinweis ein-/ausklappen“) in der Seite, auch nicht beim Einrichten.
/// B-3 ein dreizeiliger Text (Matratze) ist aufgeklappt ganz lesbar; Maus, Finger, Taste H und Screenreader oeffnen ihn,
/// Tippen schliesst ihn (zurueck zum Titel), Weitergehen schliesst ihn ganz. B-4 jeder neue Text wird vorgelesen.
/// B-6 der Handy-Knopf steht unten in der Mitte ohne Dreieck und ueberschneidet weder Figur noch Steuer-Knoepfe.
/// Ablauf 2a: neuer Text ganz, nach 6 s nur der Titel (Deckkraft 0,5), beim Weitergehen weg.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> Hinweis464Async(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Hinweis 464 " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Hinweis 464 " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null || TopLevel.GetTopLevel(ansicht) is not Window fenster)
        {
            Messwerte.Notiere("  FEHLER: Spiel Hinweis 464: Spielseite oder Fenster fehlt");
            return 1;
        }
        if (!await GlAbwartenAsync(seite))
        {
            Messwerte.Notiere("Spiel: Hinweis 464: ohne 3D keine Handlung, der Hinweis bleibt in der Karte unten (nicht gemessen)");
            return 0;
        }
        var vorher = seite.Spiel;
        var (breiteVorher, hoeheVorher) = (fenster.Width, fenster.Height);
        var schriftVorher = Zustand.SchriftFaktor(Einstellungen.Schriftgroesse(Zustand.Werte));
        var tafelVorher = seite.Tafel?.IsVisible ?? false;
        var fl = seite.Flaeche;
        async Task Warten()
        {
            await ansicht.BildFertigAsync();
            await Task.Delay(300);
            await fl.NaechstesBildAsync();
            await ansicht.BildFertigAsync();
        }
        string R(Rect? r) => r is { } q ? string.Create(inv, $"{q.X:0}/{q.Y:0} {q.Width:0}x{q.Height:0}") : "-";
        // Figur auf dem Bild (Kasten des Modells mit Lage und Sicht des letzten Bilds), in Punkten der Seite
        Rect? Figur() => fl.FigurAufBild() is { } r && fl.TranslatePoint(default, seite) is { } f0 ? r.Translate(new Avalonia.Vector(f0.X, f0.Y)) : null;
        // Weg vor der Figur: die Spalte unter ihr bis zum unteren Rand der Flaeche
        Rect Weg(Rect figur) => fl.TranslatePoint(new Point(0, fl.Bounds.Height), seite) is { } unten && unten.Y > figur.Bottom
            ? new Rect(figur.X, figur.Bottom, figur.Width, unten.Y - figur.Bottom) : default;
        // Steuer-Knoepfe (rund, in Punkten der Seite) und der Bereich, in dem die Sticks aufsetzen
        List<(string Name, Rect Lage)> Knoepfe()
        {
            var liste = new List<(string, Rect)>();
            if (fl.TranslatePoint(default, seite) is not { } f0)
            {
                return liste;
            }
            var (springen, knien, liegen) = seite.DaumenKnoepfe;
            foreach (var (name, k) in new (string, DaumenKnopf?)[] { ("Springen", springen), ("Hinknien", knien), ("Hinlegen", liegen), ("Handlung", seite.HandlungRund), ("Ich-Sicht", seite.IchRund), ("Handy rund", seite.HandyRund) })
            {
                if (k is { IsEffectivelyVisible: true } && seite.DaumenRechteck(k) is { } r)
                {
                    liste.Add((name, r.Translate(new Avalonia.Vector(f0.X, f0.Y))));
                }
            }
            if (seite.QuestKnopf is { IsEffectivelyVisible: true } q && q.TranslatePoint(default, seite) is { } qp)
            {
                liste.Add(("Handy-Knopf", new Rect(qp, q.Bounds.Size)));
            }
            if (seite.XKnopf is { IsEffectivelyVisible: true } x && x.TranslatePoint(default, seite) is { } xp)
            {
                liste.Add(("X", new Rect(xp, x.Bounds.Size)));
            }
            if (fl.SticksMoeglich)
            {
                var b = fl.StickBereich;
                var oben = Sticks.ZoneOben(b);
                liste.Add(("Sticks", new Rect(f0.X, f0.Y + oben, b.Breite, Math.Max(0, b.Hoehe - oben))));
            }
            return liste;
        }
        static bool Schneidet(Rect a, Rect b) => a.Width > 0 && b.Width > 0 && a.Intersect(b) is { Width: > 0.5, Height: > 0.5 };
        // B-2: kein Dreieck in der Seite (Symbol und Name)
        string Dreieck()
        {
            var symbol = Symbole.Text("arrow_drop_down");
            var treffer = seite.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible
                && ((c is TextBlock t && t.Text == symbol)
                    || Avalonia.Automation.AutomationProperties.GetName(c) is SpielProbeTexte.HinweisEinklappen or SpielProbeTexte.HinweisAusklappen)).ToList();
            return string.Join(", ", treffer.Select(c => c.GetType().Name));
        }
        void Tippen(Control c) => (ControlAutomationPeer.CreatePeerForElement(c) as IInvokeProvider)?.Invoke();
        void Taste(Key taste) => seite.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = taste, Source = fl });
        try
        {
            seite.MenueAltFuerTests = false;
            seite.HinweisZeitFuerTests = TimeSpan.Zero;
            var spiel = HaltungenWohnung(seite);
            if (spiel?.Innen is not { } innen)
            {
                Pruefe("Wohnung", false, "Wohnung nicht betretbar");
                return funde;
            }
            if (seite.Tafel is { } t0)
            {
                t0.IsVisible = false;
            }
            // die Stelle an der Matratze (Nicos Fund): Titel „Matratze“ und mindestens zwei Zeilen Text darunter
            System.Numerics.Vector2? matratze = null;
            for (var z = 0.2f; z < innen.Tiefe && matratze is null; z += 0.3f)
            {
                for (var x = 0.2f; x < innen.Breite && matratze is null; x += 0.3f)
                {
                    spiel.Ansehen(innen, x, z, 0f);
                    var teile = spiel.Zeile.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (teile.Length >= 3 && teile[0].StartsWith("Matratze", StringComparison.Ordinal))
                    {
                        matratze = new System.Numerics.Vector2(x, z);
                    }
                }
            }
            if (matratze is not { } stelle)
            {
                Pruefe("Matratze", false, "keine Stelle mit dreizeiligem Text an der Matratze gefunden");
                return funde;
            }
            // Blick zur Kamera: die Figur steht vor der Matratze, das Bild zeigt sie in der Mitte
            async Task NeuerText()
            {
                spiel.Ansehen(innen, innen.StartX, innen.StartZ, 0f);
                await ansicht.BildFertigAsync();
                spiel.Ansehen(innen, stelle.X, stelle.Y, 0f);
                fl.KameraAufFigur();
                // die Tafel (Spielmenue) baut sich bei neuer Schrift neu auf und deckt die Ebene ab: fuer die Messung weg damit
                if (seite.Tafel is { IsVisible: true } tafel)
                {
                    tafel.IsVisible = false;
                }
                await Warten();
            }
            await NeuerText();
            var zeile = SpielProbeTexte.Tafelzeile(spiel.Zeile, seite.AntippenGiltJetzt).Trim();
            Pruefe("Matratze", true, string.Create(inv, $"an ({stelle.X:0.0}, {stelle.Y:0.0}): \"{zeile.Replace('\n', '|')}\""));

            var (hoechstB, hoechstH) = Hoechstgroesse(fenster);
            var geraete = new (string Name, double B, double H, bool Daumen)[]
            {
                ("Handy quer", 832, 384, true), ("Handy hoch", 384, 832, true), ("Tablet quer", 1280, 800, true), ("PC", PcBreite, PcHoehe, false),
            };
            foreach (var faktor in new[] { 1.0, 2.0 })
            {
                Schrift.Anwenden(faktor);
                foreach (var (name, b, h, daumen) in geraete)
                {
                    seite.DaumenKnoepfeFuerTests = daumen;
                    fenster.Width = Math.Min(b, hoechstB);
                    fenster.Height = Math.Min(h, hoechstH);
                    seite.HinweisZeitFuerTests = TimeSpan.Zero;
                    await NeuerText();
                    if (seite.Tafel is { IsVisible: true } t)
                    {
                        t.IsVisible = false;
                        await Warten();
                    }
                    var punkt = string.Create(inv, $"{name} {fenster.Width:0}x{fenster.Height:0} Schrift {faktor * 100:0} %");
                    var figur = Figur();
                    var knoepfe = Knoepfe();
                    if (figur is not { } fig)
                    {
                        Pruefe(punkt + " Figur", false, "Figur nicht auf dem Bild");
                        continue;
                    }
                    var weg = Weg(fig);
                    string Ueberschneidung(Rect text)
                    {
                        var funde2 = new List<string>();
                        if (Schneidet(text, fig))
                        {
                            funde2.Add("FIGUR " + R(fig));
                        }
                        if (Schneidet(text, weg))
                        {
                            funde2.Add("WEG " + R(weg));
                        }
                        funde2.AddRange(knoepfe.Where(k => Schneidet(text, k.Lage)).Select(k => k.Name.ToUpperInvariant() + " " + R(k.Lage)));
                        return string.Join(", ", funde2);
                    }
                    // drei Stufen: sofort ganz, nach 6 s nur der Titel, aufgeklappt
                    foreach (var stufe in new[] { HinweisOben.Stufe.Voll, HinweisOben.Stufe.Titel, HinweisOben.Stufe.Offen })
                    {
                        if (stufe == HinweisOben.Stufe.Titel)
                        {
                            seite.HinweisZeitFuerTests = HinweisOben.VollDauer + TimeSpan.FromSeconds(1);
                            await Warten();
                        }
                        else if (stufe == HinweisOben.Stufe.Offen)
                        {
                            seite.HinweisObenTippen();
                            await Warten();
                        }
                        var text = seite.HinweisObenRechteck();
                        var ist = seite.HinweisObenStufe;
                        var ueber = text is { } tr ? Ueberschneidung(tr) : "Zeile nicht zu sehen";
                        var deckOk = stufe != HinweisOben.Stufe.Titel || Math.Abs((seite.HinweisObenFeld?.Opacity ?? 1) - HinweisOben.TitelDeckkraft) < 0.01;
                        var inhaltOk = stufe switch
                        {
                            HinweisOben.Stufe.Titel => seite.HinweisObenText == HinweisOben.Kurz(zeile),
                            HinweisOben.Stufe.Offen => seite.HinweisObenText.Replace("\n", "", StringComparison.Ordinal) == zeile.Replace("\n", "", StringComparison.Ordinal),
                            _ => seite.HinweisObenText.StartsWith(HinweisOben.Teile(zeile).Titel, StringComparison.Ordinal),
                        };
                        Pruefe(punkt + " " + stufe, ist == stufe && ueber.Length == 0 && deckOk && inhaltOk && !seite.HinweisUntenSichtbar,
                            $"Stufe {ist}, Zeile {R(text)} {(seite.HinweisObenDaneben ? "neben der Uhr" : "unter der Uhr")}, Figur {R(fig)}, Weg {R(weg)}, "
                            + $"Knoepfe {knoepfe.Count}{(ueber.Length > 0 ? ", UEBERSCHNEIDET " + ueber : ", keine Ueberschneidung")}"
                            + (deckOk ? "" : ", DECKKRAFT falsch") + (inhaltOk ? "" : $", TEXT \"{seite.HinweisObenText.Replace('\n', '|')}\"")
                            + (seite.HinweisUntenSichtbar ? ", HINWEIS UNTEN zu sehen" : "") + (seite.LeisteRechteck() is { } lr ? ", Karte unten " + R(lr) : ""));
                    }
                    // B-6: Handy-Knopf unten in der Mitte (Handy, Tablet: der runde am Daumen; PC: in der Karte unten), ohne Dreieck
                    var handy = knoepfe.Where(k => k.Name is "Handy rund" or "Handy-Knopf").ToList();
                    var andere = knoepfe.Where(k => k.Name is not ("Handy rund" or "Handy-Knopf" or "Sticks")).ToList();
                    var handyUeber = handy.SelectMany(hk => andere.Where(a => Schneidet(hk.Lage, a.Lage)).Select(a => hk.Name + "/" + a.Name)
                        .Concat(Schneidet(hk.Lage, fig) ? [hk.Name + "/FIGUR"] : [])).ToList();
                    Pruefe(punkt + " Handy-Knopf (B-6)", handy.Count > 0 && handyUeber.Count == 0 && Dreieck().Length == 0,
                        (handy.Count == 0 ? "KEIN Handy-Knopf" : string.Join(", ", handy.Select(hk => hk.Name + " " + R(hk.Lage))))
                        + (handyUeber.Count > 0 ? ", UEBERSCHNEIDET " + string.Join(", ", handyUeber) : ", frei") + (Dreieck() is { Length: > 0 } d ? ", DREIECK " + d : ", kein Dreieck"));
                    // Weitergehen schliesst
                    spiel.HinstellenFuerTests(stelle.X + 0.1f, stelle.Y);
                    await Warten();
                    Pruefe(punkt + " Weitergehen schliesst", seite.HinweisObenStufe == HinweisOben.Stufe.Aus, $"Stufe {seite.HinweisObenStufe}");
                    spiel.HinstellenFuerTests(stelle.X, stelle.Y);
                }
            }
            Schrift.Anwenden(1.0);

            // Gegenprobe B-1: das Hinweisfeld unten wie bis A 1.12.0 liegt am Handy quer auf dem Weg vor der Figur
            seite.DaumenKnoepfeFuerTests = true;
            fenster.Width = Math.Min(832, hoechstB);
            fenster.Height = Math.Min(384, hoechstH);
            seite.HinweisfeldAltFuerTests = true;
            seite.Beschriften();
            await NeuerText();
            await Warten();
            var altKarte = seite.LeisteRechteck();
            var altZustand = seite.UntenZustand;
            var altFigur = Figur();
            var altRot = altKarte is { } ak && altFigur is { } af && (Schneidet(ak, af) || Schneidet(ak, Weg(af)));
            seite.HinweisfeldAltFuerTests = false;
            seite.Beschriften();
            await NeuerText();
            Pruefe("Gegenprobe Hinweisfeld unten (alt)", altRot, $"alt: Karte {R(altKarte)}, Figur {R(altFigur)} -> {(altRot ? "auf Figur oder Weg (erkannt)" : "FREI (Gegenprobe wirkt nicht)")}, Seite {seite.Bounds.Width:0}x{seite.Bounds.Height:0}, Text unten {seite.HinweisUntenSichtbar} ({altZustand})");

            // B-3: oeffnen mit Maus, Finger, Taste H und Screenreader; Tippen schliesst zurueck zum Titel
            foreach (var art in new[] { "Maus", "Finger", "Taste H", "Screenreader" })
            {
                seite.DaumenKnoepfeFuerTests = art == "Finger";
                seite.HinweisZeitFuerTests = TimeSpan.Zero;
                await NeuerText();
                if (seite.Tafel is { IsVisible: true } tb)
                {
                    tb.IsVisible = false;
                    await Warten();
                }
                if (seite.HinweisObenFeld is not { } feld)
                {
                    Pruefe("Oeffnen " + art, false, "keine Zeile");
                    continue;
                }
                async Task Ausloesen()
                {
                    switch (art)
                    {
                        case "Maus":
                            await TippenAsync(ansicht, feld, PointerType.Mouse);
                            break;
                        case "Finger":
                            await TippenAsync(ansicht, feld, PointerType.Touch);
                            break;
                        case "Taste H":
                            Taste(Key.H);
                            break;
                        default:
                            Tippen(feld);
                            break;
                    }
                    await Warten();
                }
                await Ausloesen();
                var offen = seite.HinweisObenStufe;
                var ganz = seite.HinweisObenText;
                var nameOffen = Avalonia.Automation.AutomationProperties.GetName(feld) ?? "";
                await Ausloesen();
                var zu = seite.HinweisObenStufe;
                Pruefe("Oeffnen und Schliessen " + art, offen == HinweisOben.Stufe.Offen && ganz.Split('\n').Length >= 3 && zu == HinweisOben.Stufe.Titel
                    && nameOffen.StartsWith("Hinweis: ", StringComparison.Ordinal),
                    $"offen {offen} ({ganz.Split('\n').Length} Zeilen), Name \"{nameOffen}\", danach {zu}");
            }

            // B-4: ein neuer Text wird vorgelesen (ganz)
            seite.DaumenKnoepfeFuerTests = false;
            await NeuerText();
            Pruefe("Vorlesen (B-4)", Ansage.Zuletzt == zeile, $"angesagt \"{Ansage.Zuletzt.Replace('\n', '|')}\"");
            // Taste H in der Tastenbelegung
            var belegt = Tastenbelegung.Aktuell.AnzeigeFuer(Tastenbelegung.Hinweis);
            Pruefe("Taste in der Belegung", belegt == "H" && Tastenbelegung.Aufgaben.Any(a => a.Id == Tastenbelegung.Hinweis), $"Taste \"{belegt}\"");

            // B-2 beim Einrichten (Nico 4a): Tafel unten ohne Dreieck, keine Zeile oben
            spiel.Ansehen(innen, innen.StartX, innen.StartZ, 0f);
            spiel.EinrichtenUmschalten();
            if (spiel.Einrichten)
            {
                await Warten();
                Pruefe("Einrichten ohne Dreieck (B-2)", Dreieck().Length == 0 && seite.HinweisObenStufe == HinweisOben.Stufe.Aus && seite.UntenSichtbar,
                    $"Dreieck \"{Dreieck()}\", Zeile oben {seite.HinweisObenStufe}, Karte unten {(seite.UntenSichtbar ? "da" : "WEG")}, Tafel {seite.Tafel?.IsVisible}, Fertig-Knopf {seite.EinrichtenKnopf.IsEffectivelyVisible}");
                spiel.EinrichtenUmschalten();
                await Warten();
            }
            else
            {
                Pruefe("Einrichten ohne Dreieck (B-2)", false, "Einrichten nicht gestartet");
            }
        }
        finally
        {
            seite.DaumenKnoepfeFuerTests = null;
            seite.HinweisfeldAltFuerTests = false;
            seite.HinweisZeitFuerTests = null;
            seite.MenueAltFuerTests = true;
            Schrift.Anwenden(schriftVorher);
            fenster.Width = breiteVorher;
            fenster.Height = hoeheVorher;
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
