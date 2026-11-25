using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Spiel;
using Lernplattform.Views;
using Knopf = Lernplattform.Core.Spiel.KnopfAnordnung.Knopf;
using Platz = Lernplattform.Core.Spiel.KnopfAnordnung.Platz;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 1.9.0 (Sammelliste 450, Weisungen P12-375 bis P12-377): „Knöpfe anordnen“ mit gedachten Fingern am Handy quer, Handy hoch
/// und Tablet quer. Abnahmesaetze: A1 ohne eigene Anordnung stehen alle runden Knoepfe genau an der Stelle und in der Groesse von
/// A 1.8.0; A2 ein gezogener Knopf rastet ein und steht nach „Fertig“ im Spiel dort (gespeichert ueber Einstellungen.Aendern,
/// nach dem Neulesen wirksam); A3 an einer ungueltigen Stelle wird er rot und springt beim Loslassen zurueck, auf ihm beginnt kein
/// Stick; A4 „Abbrechen“ aendert nichts, „Standard“ stellt die Ausrichtung wieder her; A5 hoch und quer getrennt; A6 am PC bleiben
/// die Symbole mit Tasten an ihrer Stelle (Gegenprobe mit gespeicherter Anordnung); A7 Namen „…, verschieben“, Pfeile, Layout;
/// A8 Groesse 50 % mit Trefferflaeche 40, die Mitte trifft; A9 Raster aus wird gemerkt, frei gezogen, wieder ein.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> Anordnen450Async(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Anordnen 450 " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Anordnen 450 " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null || TopLevel.GetTopLevel(ansicht) is not Window fenster)
        {
            Messwerte.Notiere("  FEHLER: Spiel Anordnen 450: Spielseite oder Fenster fehlt");
            return 1;
        }
        if (!await GlAbwartenAsync(seite))
        {
            Messwerte.Notiere("Spiel: Anordnen 450: ohne 3D keine runden Knoepfe (nicht gemessen)");
            return 0;
        }
        var (breiteVorher, hoeheVorher) = (fenster.Width, fenster.Height);
        var tafelVorher = seite.Tafel?.IsVisible ?? false;
        async Task Warten()
        {
            await ansicht.BildFertigAsync();
            await Task.Delay(300);
            await ansicht.BildFertigAsync();
        }
        string Name(Control c) => Avalonia.Automation.AutomationProperties.GetName(c) ?? "";
        void Tippen(Control k) => (ControlAutomationPeer.CreatePeerForElement(k) as IInvokeProvider)?.Invoke();
        var (springen, knien, liegen) = seite.DaumenKnoepfe;
        DaumenKnopf? Echt(Knopf k) => k switch
        {
            Knopf.Springen => springen,
            Knopf.Knien => knien,
            Knopf.Liegen => liegen,
            Knopf.Handlung => seite.HandlungRund,
            Knopf.Ich => seite.IchRund,
            _ => seite.HandyRund,
        };
        string Lagen(IReadOnlyDictionary<Knopf, Platz> a) => string.Join(", ", a.Select(e => string.Create(inv, $"{e.Key} ({e.Value.X:0.#}/{e.Value.Y:0.#}) {e.Value.Prozent} %")));
        // sichtbare echte Knoepfe an Stelle und Groesse der Anordnung (Mitte auf 0,5 Punkte, Trefferflaeche = max(Bild, 40))
        string? Abweichung(IReadOnlyDictionary<Knopf, Platz> a, Sticks.Bereich b, bool eigene)
        {
            foreach (var (k, p) in a)
            {
                if (Echt(k) is not { IsVisible: true } knopf || seite.DaumenRechteck(knopf) is not { } r)
                {
                    continue;
                }
                var d = KnopfAnordnung.Durchmesser(k, b, p.Prozent);
                var t = eigene ? KnopfAnordnung.Treffer(d) : d;
                if (Math.Abs(r.Center.X - p.X) > 0.5 || Math.Abs(r.Center.Y - p.Y) > 0.5 || Math.Abs(r.Width - t) > 0.01 || Math.Abs(knopf.Sichtbar - d) > 0.01)
                {
                    return string.Create(inv, $"{k}: ({r.Center.X:0.#}/{r.Center.Y:0.#}) {r.Width:0.#} (Bild {knopf.Sichtbar:0.#}) statt ({p.X:0.#}/{p.Y:0.#}) {t:0.#} (Bild {d:0.#})");
                }
            }
            return null;
        }
        bool Trifft(DaumenKnopf knopf) => seite.DaumenRechteck(knopf) is { } r && seite.Flaeche.TranslatePoint(default, seite) is { } f0
            && seite.InputHitTest(r.Center + f0) as Visual is { } v && (ReferenceEquals(v, knopf) || knopf.IsVisualAncestorOf(v));
        void Ziehen(Platz von, Platz nach)
        {
            seite.AnordnenDruecken(91, new Point(von.X, von.Y));
            // in drei Schritten wie ein Finger
            for (var i = 1; i <= 3; i++)
            {
                seite.AnordnenZiehen(91, new Point(von.X + ((nach.X - von.X) * i / 3), von.Y + ((nach.Y - von.Y) * i / 3)));
            }
        }
        void AllesStandard()
        {
            Programm.Zustand.Aendere(w =>
            {
                KnopfAnordnung.Wert(w, "quer", null);
                KnopfAnordnung.Wert(w, "hoch", null);
                KnopfAnordnung.RasterWert(w, true);
            });
            KnopfAnordnung.Uebernehmen(Programm.Zustand.Werte);
        }
        try
        {
            AllesStandard();
            // Handlung, Ich-Sicht und Handy gibt es nur ohne das alte Menue (Vorbild Handlung444Async)
            seite.MenueAltFuerTests = false;
            seite.TuerenSelbstFuerTests = false;
            seite.DaumenKnoepfeFuerTests = true;
            if (seite.Tafel is { } t0)
            {
                t0.IsVisible = false;
            }
            var (hoechstB, hoechstH) = Hoechstgroesse(fenster);
            var erstes = true;
            foreach (var (geraet, w, h) in new[] { ("Handy quer (S24 Ultra)", 832.0, 384.0), ("Handy hoch (S24 Ultra)", 384.0, 832.0), ("Tablet quer (Tab A9+)", 1280.0, 800.0) })
            {
                fenster.Width = Math.Min(w, hoechstB);
                fenster.Height = Math.Min(h, hoechstH);
                await Warten();
                var punkt = string.Create(inv, $"{geraet} {fenster.Width:0}x{fenster.Height:0}");
                var b = seite.Flaeche.GrundBereich;
                var ausrichtung = KnopfAnordnung.Ausrichtung(b);
                var standard = KnopfAnordnung.Standard(b);
                // A1: ohne eigene Anordnung genau wie A 1.8.0
                Pruefe(punkt + " A1 Standard wie A 1.8.0", Abweichung(standard, b, false) is null && springen.IsVisible && seite.IchRund is { IsVisible: true },
                    Abweichung(standard, b, false) ?? "alle sichtbaren Knoepfe an Stelle und Groesse von A 1.8.0");

                // Bearbeiten-Modus: Spiel angehalten, eigene Knoepfe mit Namen, Leiste, Layout
                seite.AnordnenStarten();
                await Warten();
                var (knoepfe, regler, raster, leiste, hinweis) = seite.AnordnenTeile;
                var namenOk = knoepfe.All(e => e.Value.IsVisible && Name(e.Value).EndsWith(", verschieben", StringComparison.Ordinal));
                var layout = Layoutpruefung.Pruefe(seite);
                Pruefe(punkt + " A7 Modus offen", seite.AnordnenAktiv && !springen.IsVisible && namenOk && leiste is { IsEffectivelyVisible: true } && hinweis is { IsEffectivelyVisible: true }
                    && !seite.Flaeche.SticksMoeglich,
                    $"aktiv {seite.AnordnenAktiv}, echte Knoepfe {springen.IsVisible}, Namen {string.Join(" | ", knoepfe.Values.Select(Name))}, Sticks moeglich {seite.Flaeche.SticksMoeglich}");
                Pruefe(punkt + " A7 Layout", layout.Count == 0, layout.Count == 0 ? "keine Funde" : string.Join(" | ", layout.Take(5)));
                if (seite.AnordnenStand is not { } st0)
                {
                    continue;
                }
                // A2: Springen an eine freie Stelle ziehen (Raster an: eingerastet auf 8)
                var von = st0.Lagen[Knopf.Springen];
                var ziel = KnopfAnordnung.NaechsteErlaubte(st0.Lagen, Knopf.Springen, new Platz(von.X - (b.Breite * 0.2), ausrichtung == "hoch" ? b.Hoehe * 0.35 : von.Y - 40), b, ohneStick: false);
                if (ziel is not { } z)
                {
                    Pruefe(punkt + " A2 Ziel", false, string.Create(inv, $"keine freie Stelle, Bereich {b}, Stick links {KnopfAnordnung.StickPlatz(st0.Lagen, b, Sticks.Seite.Links)}, rechts {KnopfAnordnung.StickPlatz(st0.Lagen, b, Sticks.Seite.Rechts)}; {Lagen(st0.Lagen)}"));
                    seite.AnordnenEnde(false);
                    continue;
                }
                Ziehen(von, z);
                var waehrend = seite.AnordnenStand!.Value;
                seite.AnordnenLoslassen(91);
                var nach = seite.AnordnenStand!.Value.Lagen[Knopf.Springen];
                Pruefe(punkt + " A2 gezogen und eingerastet", waehrend.Gewaehlt == Knopf.Springen && nach.X % KnopfAnordnung.Raster == 0 && nach.Y % KnopfAnordnung.Raster == 0
                    && Math.Abs(nach.X - z.X) <= KnopfAnordnung.Raster && Math.Abs(nach.Y - z.Y) <= KnopfAnordnung.Raster,
                    string.Create(inv, $"von ({von.X:0.#}/{von.Y:0.#}) nach ({nach.X:0.#}/{nach.Y:0.#}), Ziel ({z.X:0.#}/{z.Y:0.#}), gewaehlt {waehrend.Gewaehlt}"));
                // A3: Ich-Sicht auf die Handlung ziehen: rot mit Satz, beim Loslassen zurueck
                var ichVor = seite.AnordnenStand!.Value.Lagen[Knopf.Ich];
                Ziehen(ichVor, seite.AnordnenStand!.Value.Lagen[Knopf.Handlung]);
                await ansicht.BildFertigAsync();
                var rot = seite.AnordnenStand!.Value.Fehler;
                var satz = (hinweis?.Child as TextBlock)?.Text ?? "";
                // der lange Satz bleibt ganz im Bild (Lehre aus den Nahbildern: am Handy hoch ragte er ueber den rechten Rand)
                var hb = hinweis?.Bounds;
                Pruefe(punkt + " A3 Hinweis im Bild", hb is { } hr && hr.X >= 0 && hr.Right <= seite.Bounds.Width + 0.5 && hr.Width > 0,
                    string.Create(inv, $"Hinweis {hb?.X:0.#} bis {hb?.Right:0.#}, Bild {seite.Bounds.Width:0.#}"));
                seite.AnordnenLoslassen(91);
                var ichNach = seite.AnordnenStand!.Value.Lagen[Knopf.Ich];
                Pruefe(punkt + " A3 ungueltig rot und zurueck", rot == KnopfAnordnung.Fehler.Ueberlappt && satz.StartsWith(SpielProbeTexte.AnordnenUeberlappt, StringComparison.Ordinal)
                    && ichNach == ichVor, string.Create(inv, $"Fehler {rot}, Satz \"{satz}\", vorher ({ichVor.X:0.#}/{ichVor.Y:0.#}), nachher ({ichNach.X:0.#}/{ichNach.Y:0.#})"));
                // A7: Screenreader waehlt den Knopf, ein Pfeil verschiebt um einen Rasterschritt
                Tippen(knoepfe[Knopf.Telefon]);
                var handyVor = seite.AnordnenStand!.Value.Lagen[Knopf.Telefon];
                var pfeil = seite.AnordnenStand!.Value.Gewaehlt == Knopf.Telefon && (seite.AnordnenSchritt(0, -1) || seite.AnordnenSchritt(0, 1));
                var handyNach = seite.AnordnenStand!.Value.Lagen[Knopf.Telefon];
                Pruefe(punkt + " A7 Screenreader und Pfeil", pfeil && Math.Abs(Math.Abs(handyNach.Y - handyVor.Y) - KnopfAnordnung.Raster) < 8.01 && handyNach.Y != handyVor.Y,
                    string.Create(inv, $"gewaehlt {seite.AnordnenStand!.Value.Gewaehlt}, Handy ({handyVor.X:0.#}/{handyVor.Y:0.#}) -> ({handyNach.X:0.#}/{handyNach.Y:0.#})"));
                // A8: Ich-Sicht 50 %: Bild halb, Trefferflaeche 40
                seite.AnordnenWaehlen(Knopf.Ich);
                var klein = seite.AnordnenGroesse(50);
                await Warten();
                var ichKnopf = knoepfe[Knopf.Ich];
                Pruefe(punkt + " A8 Groesse 50 %", klein && Math.Abs(ichKnopf.Sichtbar - (KnopfAnordnung.Durchmesser(Knopf.Ich, b, 100) / 2)) < 0.01 && ichKnopf.Width == KnopfAnordnung.TrefferMin
                    && regler is { Value: 50 },
                    string.Create(inv, $"gesetzt {klein}, Bild {ichKnopf.Sichtbar:0.#}, Trefferflaeche {ichKnopf.Width:0.#}, Regler {regler?.Value}"));
                if (erstes)
                {
                    // A4: „Abbrechen“ aendert nichts (nichts gespeichert, im Spiel der Standard)
                    var vorherJson = Programm.Zustand.Werte.ToJsonString();
                    seite.AnordnenEnde(false);
                    await Warten();
                    var bA = seite.Flaeche.GrundBereich;
                    var abwA = Abweichung(KnopfAnordnung.Standard(bA), bA, false);
                    Pruefe(punkt + " A4 Abbrechen aendert nichts", !seite.AnordnenAktiv && Programm.Zustand.Werte.ToJsonString() == vorherJson && KnopfAnordnung.Gespeichert.Count == 0
                        && abwA is null, abwA ?? $"gespeichert {KnopfAnordnung.Gespeichert.Count}");
                    seite.AnordnenStarten();
                    await Warten();
                    var s1 = seite.AnordnenStand!.Value;
                    Ziehen(s1.Lagen[Knopf.Springen], nach);
                    seite.AnordnenLoslassen(91);
                    seite.AnordnenWaehlen(Knopf.Ich);
                    seite.AnordnenGroesse(50);
                    // A9: Raster aus wird gemerkt, der Knopf geht frei (nicht auf 8 gerundet), dann wieder ein
                    var rasterAus = seite.AnordnenRasterSetzen(false);
                    var hl = seite.AnordnenStand!.Value.Lagen[Knopf.Liegen];
                    var frei = KnopfAnordnung.NaechsteErlaubte(seite.AnordnenStand!.Value.Lagen, Knopf.Liegen, hl with { X = hl.X - 13.3, Y = hl.Y - 5.7 }, b, ohneStick: false);
                    if (frei is { } fz)
                    {
                        Ziehen(hl, fz);
                        seite.AnordnenLoslassen(91);
                    }
                    var hl2 = seite.AnordnenStand!.Value.Lagen[Knopf.Liegen];
                    var gemerktAus = Programm.Zustand.Werte[KnopfAnordnung.SchluesselRaster]?.ToJsonString();
                    Pruefe(punkt + " A9 Raster aus", rasterAus && !KnopfAnordnung.RasterAn && gemerktAus == "false" && raster is { IsChecked: false } && frei is not null
                        && (hl2.X % KnopfAnordnung.Raster != 0 || hl2.Y % KnopfAnordnung.Raster != 0),
                        string.Create(inv, $"gespeichert {rasterAus} ({gemerktAus}), Schalter {raster?.IsChecked}, Hinlegen ({hl2.X:0.#}/{hl2.Y:0.#})"));
                    seite.AnordnenRasterSetzen(true);
                    Pruefe(punkt + " A9 Raster wieder ein", KnopfAnordnung.RasterAn && Programm.Zustand.Werte[KnopfAnordnung.SchluesselRaster] is null && raster is { IsChecked: true },
                        $"an {KnopfAnordnung.RasterAn}, Eintrag {Programm.Zustand.Werte[KnopfAnordnung.SchluesselRaster]?.ToJsonString() ?? "-"}");
                }
                // A8 groesser: Springen bis 120 % (eine Groesse ueber 100 % aendert die Breite des Knopfs im Spiel; Lehre aus dem Bau:
                // zweimal Setzen mit verschiedenen Breiten je Durchgang hielt das Layout ohne Ende in Bewegung)
                seite.AnordnenWaehlen(Knopf.Springen);
                // (wo 120 % am Platz des Knopfs keinen Raum hat, die naechste kleinere Stufe ueber 100 %)
                var gross = false;
                foreach (var stufe in new[] { 120, 115, 110, 105 })
                {
                    if (seite.AnordnenGroesse(stufe))
                    {
                        gross = true;
                        break;
                    }
                }
                Pruefe(punkt + " A8 Groesse ueber 100 %", gross && seite.AnordnenStand!.Value.Lagen[Knopf.Springen].Prozent > 100,
                    $"gesetzt {gross}, Springen {seite.AnordnenStand!.Value.Lagen[Knopf.Springen].Prozent} %");
                // A2: „Fertig“: gespeichert, im Spiel an der neuen Stelle, Mitte trifft, auf ihm kein Stick
                var entwurf = new Dictionary<Knopf, Platz>(seite.AnordnenStand!.Value.Lagen);
                var fertig = seite.AnordnenEnde(true);
                await Warten();
                // das Layout kommt zur Ruhe: in einer Sekunde ohne Eingabe hoechstens wenige Aufrufe (eine Schleife machte Hunderte)
                var zahlVor = seite.DaumenSetzenZahl;
                await Task.Delay(1000);
                await ansicht.BildFertigAsync();
                var ruhe = seite.DaumenSetzenZahl - zahlVor;
                Console.WriteLine(string.Create(inv, $"INFO Anordnen450 {punkt}: DaumenSetzen in 1 s nach Fertig {ruhe}"));
                Pruefe(punkt + " A2 Layout ruhig", ruhe <= 30, string.Create(inv, $"DaumenSetzen in 1 s: {ruhe} (Grenze 30)"));
                var bNeu = seite.Flaeche.GrundBereich;
                var gespeichert = KnopfAnordnung.Lesen(Programm.Zustand.Werte);
                var abw = Abweichung(entwurf, bNeu, true);
                var sp = entwurf[Knopf.Springen];
                var stickAuf = Sticks.SeiteFuer(sp.X, sp.Y, seite.Flaeche.StickBereich);
                Pruefe(punkt + " A2 Fertig gespeichert und im Spiel", fertig && !seite.AnordnenAktiv && gespeichert.ContainsKey(ausrichtung) && abw is null
                    && Trifft(springen) && seite.IchRund is { } ich && Trifft(ich) && ich.Width == KnopfAnordnung.TrefferMin && stickAuf is null,
                    abw ?? $"gespeichert {string.Join("/", gespeichert.Keys)}, Treffer Springen {Trifft(springen)}, Ich {seite.IchRund?.Width}, Stick auf Springen {stickAuf}; {Lagen(entwurf)}");
                if (erstes)
                {
                    // A5: nur diese Ausrichtung ist gespeichert, gedreht zeigt das Spiel den Standard
                    fenster.Width = Math.Min(h, hoechstB);
                    fenster.Height = Math.Min(w, hoechstH);
                    await Warten();
                    var bD = seite.Flaeche.GrundBereich;
                    var abwD = Abweichung(KnopfAnordnung.Standard(bD), bD, false);
                    Pruefe(punkt + " A5 andere Ausrichtung Standard", gespeichert.Count == 1 && KnopfAnordnung.Ausrichtung(bD) != ausrichtung && abwD is null,
                        abwD ?? $"gespeichert {string.Join("/", gespeichert.Keys)}, gedreht {KnopfAnordnung.Ausrichtung(bD)}");
                    fenster.Width = Math.Min(w, hoechstB);
                    fenster.Height = Math.Min(h, hoechstH);
                    await Warten();
                    // A6 Gegenprobe: am PC (ohne runde Knoepfe) bleiben die Symbole mit Tasten an ihrer Stelle von A 1.8.0
                    seite.DaumenKnoepfeFuerTests = false;
                    seite.Beschriften();
                    await Warten();
                    var tasten = HaltungKnopf.LageTasten(seite.Flaeche.StickBereich);
                    var r0 = seite.DaumenRechteck(springen);
                    Pruefe(punkt + " A6 PC unveraendert", tasten is { } tl && r0 is { } rs && Math.Abs(rs.Center.X - tl.Springen.X) < 0.5 && Math.Abs(rs.Center.Y - tl.Springen.Y) < 0.5,
                        string.Create(inv, $"Symbol ({r0?.Center.X:0.#}/{r0?.Center.Y:0.#}), Taste ({tasten?.Springen.X:0.#}/{tasten?.Springen.Y:0.#})"));
                    seite.DaumenKnoepfeFuerTests = true;
                    seite.Beschriften();
                    await Warten();
                }
                // A4: „Standard“ stellt diese Ausrichtung wieder her (Eintrag weg, Knoepfe wie A 1.8.0)
                seite.AnordnenStarten();
                await Warten();
                seite.AnordnenStandardSetzen();
                seite.AnordnenEnde(true);
                await Warten();
                var nachStandard = KnopfAnordnung.Lesen(Programm.Zustand.Werte);
                var bS = seite.Flaeche.GrundBereich;
                Pruefe(punkt + " A4 Standard", !nachStandard.ContainsKey(ausrichtung) && Abweichung(KnopfAnordnung.Standard(bS), bS, false) is null,
                    Abweichung(KnopfAnordnung.Standard(bS), bS, false) ?? $"gespeichert {string.Join("/", nachStandard.Keys)}");
                erstes = false;
            }
            // Optionen > Steuerung > „Knöpfe anordnen“ oeffnet das Spiel im Modus, „Abbrechen“ fuehrt zu den Optionen zurueck
            await ansicht.KnoepfeAnordnenAsync();
            await Warten();
            var offen = seite.AnordnenAktiv;
            seite.AnordnenEnde(false);
            await Warten();
            Pruefe("Weg ueber die Optionen", offen && ansicht.AktuelleSeite == Seiten.Optionen, $"Modus {offen}, Seite {ansicht.AktuelleSeite}");
            await ansicht.ZeigeAsync(Seiten.Spiel);
            await Warten();
        }
        finally
        {
            if (seite.AnordnenAktiv)
            {
                seite.AnordnenEnde(false);
            }
            AllesStandard();
            seite.DaumenKnoepfeFuerTests = null;
            seite.MenueAltFuerTests = true;
            seite.TuerenSelbstFuerTests = true;
            fenster.Width = breiteVorher;
            fenster.Height = hoeheVorher;
            if (seite.Tafel is { } t1)
            {
                t1.IsVisible = tafelVorher;
            }
            await ansicht.BildFertigAsync();
        }
        return funde;
    }
}
