using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Design;
using Lernplattform.Programm;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.99.0 (Weisung P12-317, Sammelliste 355/356 und Glastuer): die Serverfarm mit den genaueren Werkstatt-Teilen und offener
/// Glastuer bleibt in jeder Grafikstufe unter den Grenzen (Innenkamera und Ich-Sicht, acht Richtungen, an drei Stellen).
/// Die Serverfarm ist im neuen Spiel gesperrt und steht darum nicht in <see cref="IchGrenzenAsync"/>; sie wird hier angesehen wie
/// im Werkzeug (nur Darstellung). Dazu (Weisung P12-318, Sammelliste 399) die Lage der Leiste unten mittig.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> WerkstattGrenzenAsync(SpielSeite seite, Grafikstufe stufe)
    {
        var inv = CultureInfo.InvariantCulture;
        var (grenzeD, grenzeA) = Grafikstufen.Grenze(stufe);
        var name = "Spiel: Serverfarm (Werkstatt, Glastuer offen) " + GrafikTexte.Name(stufe);
        var fl = seite.Flaeche;
        var vorher = seite.Spiel!;
        try
        {
            var spiel = Frisch(seite);
            var innen = Innenraum.Bauen("serverfarm", spiel.Ablauf.Stand, spiel.Innenraeume, spiel.Grafik, spiel.GeschaefteDaten, null, spiel.Feste, spiel.Szene.Jahreszeit);
            if (innen is null || innen.GlastuerZahl == 0)
            {
                Messwerte.Notiere("  FEHLER: " + name + ": Serverfarm nicht gebaut oder ohne Glastuer");
                return 1;
            }
            spiel.Ansehen(innen, innen.StartX, innen.StartZ, 0f);
            await fl.NaechstesBildAsync();
            // vor dem ersten Schrank (Gang im Sueden), Glastuer oeffnen; dazu Raummitte und Werkbank-Seite
            var rack = innen.DekoUmrisse.First(u => Py.Str(u.Deko["typ"]) == "rack").Deko;
            var vor = ((float)(Py.F(rack["x"]) + (Py.F(rack["w"]) / 2)), (float)(Py.F(rack["y"]) + Py.F(rack["h"]) + 0.45));
            (long D, int A, string Wo) teuerst = (-1, 0, "");
            var aufrufe = 0;
            var offen = false;
            foreach (var (stelle, x, z) in new[] { ("vor Schrank 1", vor.Item1, vor.Item2), ("Mitte", innen.Breite / 2, innen.Tiefe / 2), ("Eingang", innen.StartX, innen.StartZ) })
            {
                if (stelle == "vor Schrank 1")
                {
                    // hingehen wie im Spiel (der Knopf gilt fuer die Stelle, an der die Figur angekommen ist)
                    spiel.GeheZu(x, z);
                    spiel.SofortAnkommen();
                    spiel.Knopf(Lernplattform.Core.Spiel.Probe.StadtSpiel.AktionGlastuer);
                    offen = spiel.GlastuerOffen is not null;
                }
                else
                {
                    spiel.HinstellenFuerTests(x, z);
                }
                await fl.NaechstesBildAsync();
                var k = fl.AktuelleKamera!;
                foreach (var ich in new[] { false, true })
                {
                    k.IchZiel = ich ? 1f : 0f;
                    k.IchAnteil = ich ? 1f : 0f;
                    k.IchNeigung = SpielKamera.IchNeigungStart;
                    foreach (var dreh in IchRichtungen)
                    {
                        k.Drehung = dreh;
                        await fl.NaechstesBildAsync();
                        var (d, a) = (fl.LetzteDreiecke, fl.LetzteZeichenaufrufe);
                        aufrufe = Math.Max(aufrufe, a);
                        if (d > teuerst.D)
                        {
                            teuerst = (d, a, string.Create(inv, $"{stelle} {(ich ? "Ich-Sicht" : "Innenkamera")} Drehung {dreh:F0}"));
                        }
                    }
                    if (stelle == "vor Schrank 1" && !ich)
                    {
                        // die Tuer blieb waehrend der Bilder der Innenkamera offen (sonst haette die Messung sie nicht gezeigt)
                        offen = offen && spiel.GlastuerOffen is not null;
                    }
                }
                k.IchZiel = 0f;
                k.IchAnteil = 0f;
            }
            var tuerZu = spiel.GlastuerOffen is null;
            Messwerte.Notiere(string.Create(inv,
                $"{name}: teuerstes Bild {teuerst.Wo}: {teuerst.D} Dreiecke, hoechstens {aufrufe} Zeichenaufrufe (Grenzen {grenzeD}, {grenzeA}); Glastuer {(offen ? "offen" : "NICHT offen")}, nach dem Gehen {(tuerZu ? "zu" : "OFFEN")}"));
            if (teuerst.D > grenzeD || aufrufe > grenzeA || teuerst.D <= 0 || !offen || !tuerZu)
            {
                Messwerte.Notiere(string.Create(inv, $"  FEHLER: {name}: {teuerst.D} Dreiecke, {aufrufe} Aufrufe (Grenzen {grenzeD}, {grenzeA}) bei {teuerst.Wo}; offen {offen}, danach zu {tuerZu}"));
                return 1;
            }
            return 0;
        }
        finally
        {
            seite.SpielSetzen(vorher);
            await fl.NaechstesBildAsync();
        }
    }

    /// <summary>
    /// Sammelliste 399: die Leiste (Spiel-Handy und Dreieck) steht unten mittig, zu- und aufgeklappt (Mitte der Symbole und der Leiste
    /// = Bildmitte ±2), die Symbole springen beim Aufklappen nicht; wo der linke Daumen liegt (40 vom linken, 20 vom unteren Rand der
    /// Spielflaeche), ist kein Symbol, sondern der linke Stick. Hochformat und Querformat (Handy, Tablet, PC), Schrift 100 und 150 %.
    /// Gegenprobe: die alte Lage links (<see cref="SpielSeite.LeisteLinksAltFuerTests"/>) liegt unter dem Daumen und ist rot.
    /// </summary>
    private static async Task<int> Leiste399Async(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Leiste 399 " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Leiste 399 " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null || TopLevel.GetTopLevel(ansicht) is not Window fenster)
        {
            Messwerte.Notiere("  FEHLER: Spiel Leiste 399: Spielseite oder Fenster fehlt");
            return 1;
        }
        await GlAbwartenAsync(seite);
        var vorher = seite.Spiel;
        var (breiteVorher, hoeheVorher) = (fenster.Width, fenster.Height);
        var schriftVorher = Zustand.SchriftFaktor(Einstellungen.Schriftgroesse(Zustand.Werte));
        var zuVorher = seite.HinweisZu;
        // die Leiste liegt in der Ebene ueber der Flaeche; solange die Tafel (Spielmenue) offen ist, ist sie verdeckt
        var tafelVorher = seite.Tafel?.IsVisible ?? false;
        // Lage messen: Mitten, Symbole unter dem Daumen, Seite des Sticks
        async Task<(double Symbole, double Leiste, double Seite, bool UnterDaumen, Sticks.Seite? Stick)?> MessenAsync()
        {
            // die neue Schriftgroesse baut die Tafel neu (Spielstand ohne Figur: Figur anlegen); fuer die Lage der Leiste weg damit
            if (seite.Tafel is { IsVisible: true } t)
            {
                t.IsVisible = false;
            }
            // nach einer neuen Fenstergroesse braucht das Layout einen Moment (wie im Foto-Werkzeug)
            await ansicht.BildFertigAsync();
            await Task.Delay(400);
            await ansicht.BildFertigAsync();
            var fl = seite.Flaeche;
            if (!seite.Ebene.IsEffectivelyVisible || seite.LeisteMitten() is not { } m || seite.LeisteRechteck() is not { } r || fl.TranslatePoint(default, seite) is not { } f0)
            {
                return null;
            }
            // ein Symbol unter dem Daumen = ein Knopf an der Stelle (Treffer wie beim Antippen)
            var daumen = new Point(40, fl.Bounds.Height - 20);
            var unter = r.Contains(daumen + f0) && seite.InputHitTest(daumen + f0) as Visual is { } treffer
                && (treffer is Button || treffer.GetVisualAncestors().OfType<Button>().Any());
            return (m.Symbole, m.Leiste, m.Seite, unter, Sticks.SeiteFuer(daumen.X, daumen.Y, fl.StickBereich));
        }
        try
        {
            Frisch(seite);
            if (seite.Tafel is { } t0)
            {
                t0.IsVisible = false;
            }
            var (hoechstB, hoechstH) = Hoechstgroesse(fenster);
            foreach (var faktor in new[] { 1.0, 1.5 })
            {
                Schrift.Anwenden(faktor);
                foreach (var (groesse, b, h) in new[] { ("Handy hoch", 384.0, 832.0), ("Handy quer", 832.0, 384.0), ("Tablet quer", 1280.0, 800.0), ("Tablet hoch", 800.0, 1280.0), ("PC", (double)PcBreite, (double)PcHoehe) })
                {
                    fenster.Width = Math.Min(b, hoechstB);
                    fenster.Height = Math.Min(h, hoechstH);
                    double? symboleZu = null;
                    foreach (var zu in new[] { true, false })
                    {
                        if (seite.HinweisZu != zu)
                        {
                            seite.HinweisUmschalten();
                        }
                        var punkt = string.Create(inv, $"{groesse} {fenster.Width:0}x{fenster.Height:0} Schrift {faktor * 100:0} % {(zu ? "zugeklappt" : "aufgeklappt")}");
                        if (await MessenAsync() is not { } m)
                        {
                            Pruefe(punkt, false, $"Leiste nicht gemessen (Ebene sichtbar {seite.Ebene.IsEffectivelyVisible}, Tafel {seite.Tafel?.IsVisible})");
                            continue;
                        }
                        symboleZu ??= m.Symbole;
                        // zugeklappt ist links unten immer der Stick; aufgeklappt darf das Menue (am schmalen Bild ueber die ganze
                        // Breite) dort liegen, aber nie ein Symbol unter dem Daumen
                        var ok = Math.Abs(m.Symbole - m.Seite) <= 2 && Math.Abs(m.Leiste - m.Seite) <= 2 && Math.Abs(m.Symbole - symboleZu.Value) <= 2
                            && !m.UnterDaumen && (!zu || m.Stick == Sticks.Seite.Links);
                        Pruefe(punkt, ok, string.Create(inv,
                            $"Mitte Symbole {m.Symbole:0.0}, Leiste {m.Leiste:0.0}, Bild {m.Seite:0.0} (±2), zugeklappt {symboleZu:0.0}; Daumen links unten: {(m.UnterDaumen ? "SYMBOL" : "kein Symbol")}, {(m.Stick is { } s ? "Stick " + s : "KEIN Stick")}"));
                        if (!zu)
                        {
                            // Welle A 1.0.0 (Sammelliste 402): „Ich-Sicht“ links und „Spiel verlassen“ rechts gleich breit und gleich weit
                            // von der Mitte der zwei Symbole (±2 Punkte), nicht uebereinander
                            var sk = seite.SeitenKnoepfe();
                            var sym = sk is { } q && Math.Abs(q.Ich.Width - q.Verlassen.Width) <= 1 && Math.Abs((q.Mitte - q.Ich.Right) - (q.Verlassen.X - q.Mitte)) <= 2
                                && Math.Abs(q.Ich.Y - q.Verlassen.Y) <= 2 && !q.Ich.Intersects(q.Verlassen) && q.Ich.Right <= q.Mitte && q.Verlassen.X >= q.Mitte;
                            Pruefe(punkt + " Ich-Sicht links (402)", sym, sk is { } q2
                                ? string.Create(inv, $"Ich-Sicht {q2.Ich.X:0.0}..{q2.Ich.Right:0.0}, Spiel verlassen {q2.Verlassen.X:0.0}..{q2.Verlassen.Right:0.0}, Mitte {q2.Mitte:0.0}, Hoehe {q2.Ich.Y:0.0}/{q2.Verlassen.Y:0.0}")
                                : "Knoepfe nicht gemessen");
                        }
                    }
                }
            }
            // Gegenprobe: alte Lage links unten (Handy quer, zugeklappt) liegt unter dem Daumen
            Schrift.Anwenden(1.0);
            fenster.Width = Math.Min(832, hoechstB);
            fenster.Height = Math.Min(384, hoechstH);
            if (!seite.HinweisZu)
            {
                seite.HinweisUmschalten();
            }
            seite.LeisteLinksAltFuerTests = true;
            seite.Beschriften();
            var alt = await MessenAsync();
            seite.LeisteLinksAltFuerTests = false;
            seite.Beschriften();
            await ansicht.BildFertigAsync();
            var altRot = alt is { } a && (a.UnterDaumen || a.Stick != Sticks.Seite.Links || Math.Abs(a.Symbole - a.Seite) > 2);
            Pruefe("Gegenprobe alte Lage links", altRot,
                alt is { } aa ? string.Create(inv, $"alt: Mitte Symbole {aa.Symbole:0.0} statt {aa.Seite:0.0}, Daumen {(aa.UnterDaumen ? "auf dem Symbol" : "frei")}, {(aa.Stick is { } st ? "Stick " + st : "kein Stick")}") : "alt nicht gemessen");
        }
        finally
        {
            seite.LeisteLinksAltFuerTests = false;
            Schrift.Anwenden(schriftVorher);
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
