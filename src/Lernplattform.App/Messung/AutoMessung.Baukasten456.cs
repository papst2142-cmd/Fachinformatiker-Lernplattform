using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// A 1.11.0, Sammelliste 456 (Weisungen P12-385/P12-386, Nico Bild-OK, 1a, 2b): im breiten Fenster steht eine Aufgabe mit
/// Baukasten in voller Breite, ihre Anlagen darunter; der Umschalter "Aufgabe / Anlagen (n)" bleibt, "Anlagen" zeigt die
/// bisherige Ansicht mit den Anlagen daneben. Der Start-Test prueft je Ansicht (Klasse, Use-Case, ER, Aufgabe ohne Baukasten):
/// nichts verschwindet (gezeigte Anlagen = Anlagen der Aufgabe), der Baukasten nutzt die volle Breite (A-1: Klasse und
/// Use-Case ohne Wischen ab der Breite des PC 1280), Umschalter mit Maus und Finger, Aufgaben ohne Baukasten mit demselben
/// Abdruck wie die bisherige Rechnung (A-4, Gegenprobe <see cref="EchtAnlagenAnzeige.VolleBreiteAltFuerTests"/>). Im schmalen
/// Fenster bleibt alles wie bisher (Umschalter, keine zwei Spalten).
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Breite des Inhalts am PC 1280 × 800 (Seitenleiste offen, gemessen in P12-248): ab hier passen Klasse und Use-Case.</summary>
    internal const double Baukasten456PcInhalt = 1000;

    private static async Task<int> Baukasten456Async(MainView ansicht, KlausurTeil k, EchtFormatTeil e)
    {
        var funde = 0;
        void Fund(string text)
        {
            Messwerte.Notiere("  FEHLER: Pruefung im echten Format (456 Baukasten volle Breite): " + text);
            funde++;
        }
        var ab = KlausurTeil.EchtAb + 1;
        (string Name, int Art, bool Baukasten, bool OhneWischen)[] ansichten =
        [
            ("Klasse", ab + EchtFormatTeil.W1Ab, true, true),
            ("Use-Case", ab + EchtFormatTeil.W1Ab + 1, true, true),
            ("ER", ab + EchtFormatTeil.BaukastenAb, true, false),
            ("ohne Baukasten", ab + EchtFormatTeil.AeAb, false, false),
        ];
        // wie gestartet und am PC 1280 x 800 (breit, zwei Spalten), nie groesser als der Bildschirm
        var fenster = TopLevel.GetTopLevel(ansicht) as Window;
        var (breiteVorher, hoeheVorher) = (fenster?.Width ?? 0, fenster?.Height ?? 0);
        var groessen = new List<(double, double)> { (0, 0) };
        if (fenster is not null)
        {
            var (hoechstB, hoechstH) = Hoechstgroesse(fenster);
            groessen.Add((Math.Min(1280, hoechstB), Math.Min(800, hoechstH)));
        }
        var breitGeprueft = false;
        try
        {
            foreach (var (fb, fh) in groessen)
            {
                if (fenster is not null && fb > 0)
                {
                    fenster.Width = fb;
                    fenster.Height = fh;
                    await ansicht.BildFertigAsync();
                    await ansicht.BildFertigAsync();
                }
                foreach (var (name, art, mitBaukasten, ohneWischen) in ansichten)
                {
                    k.Probe(art);
                    await ansicht.BildFertigAsync();
                    var satz = e.ProbeSatz;
                    var reiter = e.ProbeStand.Reiter;
                    if (satz is null || !int.TryParse(reiter, out var nr))
                    {
                        Fund(name + ": kein Lauf mit Aufgabe gezeigt");
                        continue;
                    }
                    var aufgabe = satz.Aufgaben[nr - 1];
                    if (EchtAnlagenAnzeige.VolleBreite(aufgabe) != mitBaukasten)
                    {
                        Fund(name + ": Aufgabe " + nr + " mit Baukasten " + EchtAnlagenAnzeige.VolleBreite(aufgabe) + ", erwartet " + mitBaukasten);
                    }
                    var soll = EchtAnlagenAnzeige.Von(satz, aufgabe).Count;
                    var (zweiSpalten, zweiSoll) = e.ProbeZweiSpalten;
                    var inhalt = e.ProbeInhalt;
                    var raster = inhalt?.Children.OfType<Grid>().Any() == true;
                    var umschalter = e.ProbeUmschalter?.IsVisible == true;
                    var breite = inhalt?.Bounds.Width ?? 0;
                    if (zweiSpalten != zweiSoll)
                    {
                        Fund(name + ": zwei Spalten gezeigt " + zweiSpalten + ", soll " + zweiSoll);
                    }
                    var bericht = $"{name} (Aufgabe {nr}): Inhalt {breite:0} breit, zwei Spalten {zweiSpalten}, Umschalter {umschalter}";
                    if (!zweiSpalten)
                    {
                        // schmal: wie bisher (Umschalter, Aufgabe allein)
                        if (!umschalter || raster)
                        {
                            Fund(name + ": schmal nicht wie bisher (Umschalter " + umschalter + ", Spalten " + raster + ")");
                        }
                    }
                    else if (!mitBaukasten)
                    {
                        // A-4: ohne Baukasten zwei Spalten wie bisher, Abdruck gleich der bisherigen Rechnung
                        if (umschalter || !raster)
                        {
                            Fund(name + ": ohne Baukasten nicht zwei Spalten (Umschalter " + umschalter + ", Spalten " + raster + ")");
                        }
                        var neu = Abdruck456(inhalt);
                        EchtAnlagenAnzeige.VolleBreiteAltFuerTests = true;
                        try
                        {
                            k.Probe(art);
                            await ansicht.BildFertigAsync();
                            var alt = Abdruck456(e.ProbeInhalt);
                            if (!neu.SequenceEqual(alt))
                            {
                                Fund(name + ": Abdruck anders als bisher (" + neu.Count + " / " + alt.Count + " Elemente)");
                            }
                            bericht += ", Abdruck gleich bisher (" + neu.Count + " Elemente)";
                        }
                        finally
                        {
                            EchtAnlagenAnzeige.VolleBreiteAltFuerTests = false;
                        }
                    }
                    else
                    {
                        // A-1/A-2: volle Breite, Anlagen darunter, Umschalter bleibt (2b)
                        var karten = inhalt?.Children.OfType<Border>().Count() ?? 0;
                        if (raster || !umschalter || karten != 1 + soll)
                        {
                            Fund(name + ": nicht volle Breite mit Anlagen darunter (Spalten " + raster + ", Umschalter " + umschalter
                                + ", Karten " + karten + ", soll 1 + " + soll + ")");
                        }
                        var bau = e.ProbeBaukasten;
                        var kasten = bau?.GetVisualDescendants().OfType<Bildkasten>().FirstOrDefault();
                        var aufgabeBreite = inhalt?.Children.OfType<Border>().FirstOrDefault()?.Bounds.Width ?? 0;
                        if (bau is null || kasten is null || aufgabeBreite < breite - 1 || kasten.Bounds.Width < breite * 0.9)
                        {
                            Fund(name + ": Baukasten nutzt nicht die volle Breite (Aufgabe " + aufgabeBreite.ToString("0") + ", Raster "
                                + (kasten?.Bounds.Width ?? 0).ToString("0") + ", Inhalt " + breite.ToString("0") + ")");
                        }
                        else
                        {
                            if (ohneWischen && breite >= Baukasten456PcInhalt && kasten.WischHinweis)
                            {
                                Fund(name + ": Raster braucht am PC noch Wischen (" + kasten.Bild.DesiredSize.Width.ToString("0") + " von "
                                    + kasten.Bounds.Width.ToString("0") + ")");
                            }
                            bericht += $", Raster {kasten.Bild.DesiredSize.Width:0} von {kasten.Bounds.Width:0}" + (kasten.WischHinweis ? " (wischen)" : " (ganz)");
                        }

                        // 2b: "Anlagen" zeigt die Anlagen daneben (alle der Aufgabe), "Aufgabe" wieder die volle Breite; Maus und Finger
                        foreach (var zeiger in new[] { PointerType.Mouse, PointerType.Touch })
                        {
                            var wie = zeiger == PointerType.Mouse ? "Maus" : "Finger";
                            if (!await TippenAsync(ansicht, e.ProbeUmschalter, EchtFormatTeil.UmschalterText(satz, aufgabe), zeiger) || !e.ProbeStand.Anlagen)
                            {
                                Fund(name + " " + wie + ": \"Anlagen\" antippen wechselt nicht" + TrefferZuletzt);
                                continue;
                            }
                            var (_, daneben) = e.ProbeAnlagenZahl;
                            if (e.ProbeInhalt?.Children.OfType<Grid>().Any() != true || daneben != soll)
                            {
                                Fund(name + " " + wie + ": \"Anlagen\" zeigt " + daneben + " Anlagen daneben, soll " + soll);
                            }
                            if (!await TippenAsync(ansicht, e.ProbeUmschalter, EchtTexte.AufgabeUmschalter, zeiger) || e.ProbeStand.Anlagen
                                || e.ProbeInhalt?.Children.OfType<Grid>().Any() == true)
                            {
                                Fund(name + " " + wie + ": \"Aufgabe\" antippen zeigt nicht die volle Breite" + TrefferZuletzt);
                            }
                        }
                        bericht += ", Umschalter Maus und Finger";

                        // Gegenprobe: bisherige Rechnung (zwei Spalten, kein Umschalter) muss hier anschlagen
                        EchtAnlagenAnzeige.VolleBreiteAltFuerTests = true;
                        try
                        {
                            k.Probe(art);
                            await ansicht.BildFertigAsync();
                            if (e.ProbeInhalt?.Children.OfType<Grid>().Any() != true || e.ProbeUmschalter?.IsVisible == true)
                            {
                                Fund(name + ": Gegenprobe bisherige Ansicht schlaegt nicht an");
                            }
                            bericht += ", Gegenprobe bisher zwei Spalten rot";
                        }
                        finally
                        {
                            EchtAnlagenAnzeige.VolleBreiteAltFuerTests = false;
                        }
                    }
                    breitGeprueft |= zweiSpalten && mitBaukasten;
                    Messwerte.Notiere("Pruefung im echten Format 456 " + bericht + ", Anlagen " + soll);
                }
            }
        }
        finally
        {
            if (fenster is not null)
            {
                fenster.Width = breiteVorher;
                fenster.Height = hoeheVorher;
                await ansicht.BildFertigAsync();
            }
        }
        // auf einem Bildschirm mit Platz fuer 1280 x 800 muss die volle Breite wirklich geprueft sein
        if (fenster is not null && groessen[^1].Item1 >= 1280 && !breitGeprueft)
        {
            Fund("volle Breite nicht geprueft (bei 1280 x 800 keine zwei Spalten)");
        }
        return funde;
    }

    /// <summary>Abdruck der sichtbaren Elemente (Art, Lage, Groesse, Text) im Inhalt, ohne die laufende Uhr.</summary>
    private static List<string> Abdruck456(Control? inhalt)
    {
        var liste = new List<string>();
        if (inhalt is null)
        {
            return liste;
        }
        foreach (var c in inhalt.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible))
        {
            var lage = c.TranslatePoint(default, inhalt) ?? default;
            var text = c switch { TextBlock t => t.Text, TextBox t => t.Text, _ => null };
            liste.Add(FormattableString.Invariant($"{c.GetType().Name} {lage.X:0.#},{lage.Y:0.#} {c.Bounds.Width:0.#}x{c.Bounds.Height:0.#} {text}"));
        }
        return liste;
    }
}
