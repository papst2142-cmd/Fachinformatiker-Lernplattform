using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 9 W5 (Weisung P9-11 Teil B, Sammelliste 179): Umziehen ueber die Oberflaeche. Je ein frisches Spiel mit
/// Kauf und mit Miete (Schalter aus Optionen &gt; Spiel) arbeitet, bis es genug Geld hat, betritt die Wohnung,
/// schaltet "Einrichten" ein, tippt in der Karte "Wohnung" auf "Umziehen" und bestaetigt die Rueckfrage mit "Ja".
/// Gemessen wird vom Tippen auf "Ja" bis zum Bild (einmalig, streng 400 ms). Rueckfrage, Karte und Ergebnis
/// muessen den Texten aus fisi_game.py entsprechen (Wohnung.UmzugTexte, WohntIn, MietText).
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> P9W5UmzugAsync(MainView ansicht, SpielSeite seite)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Fund(string text)
        {
            Messwerte.Notiere("  FEHLER: Spiel Umzug " + text);
            funde++;
        }
        foreach (var miete in new[] { false, true })
        {
            var art = miete ? "Miete" : "Kauf";
            var alt = seite.Spiel!;
            var spiel = SpielStart.Neu(alt.Stadt, alt.Grafik, alt.Innenraeume, alt.GeschaefteDaten, alt.Feste, Festzeit.Heute, Jahreszeiten.Heute);
            spiel.Ablauf.MieteSchalter = () => miete;
            var ziel = Wohnung.Bleibe("zweizimmer", spiel.Ablauf.Stand.Inhalt)!;
            for (var tag = 0; tag < 120 && Py.Wahr(Wohnung.UmzugAngebot(spiel.Ablauf.Stand, ziel, miete)["fehlt"]); tag++)
            {
                spiel.Ablauf.SchlafenGehen();
            }
            seite.SpielSetzen(spiel);
            spiel.Auffrischen();
            await seite.Flaeche.NaechstesBildAsync();
            if (!spiel.KannBetreten || !spiel.Betreten())
            {
                Fund(art + ": Wohnung nicht betretbar (" + spiel.Zeile.Replace('\n', ' ') + ")");
                continue;
            }
            spiel.EinrichtenUmschalten();
            await seite.Flaeche.NaechstesBildAsync();
            await ansicht.BildFertigAsync();
            var (preis, frage) = Wohnung.UmzugTexte(spiel.Ablauf.Stand, ziel, miete);
            var karte = SichtbareTexte(seite);
            if (!karte.Contains(Wohnung.WohnungUntertitel(miete), StringComparison.Ordinal) || !karte.Contains("Du wohnst in: Einzimmer-Apartment", StringComparison.Ordinal)
                || !karte.Contains(preis, StringComparison.Ordinal))
            {
                Fund(art + ": Karte \"Wohnung\" zeigt Untertitel, Wohnung oder Preis \"" + preis + "\" nicht");
            }
            // "Umziehen" der ersten Wohnung (zweizimmer) antippen
            if (KnopfMit(seite, Wohnung.UmziehenKnopf) is not { IsEnabled: true } umziehen)
            {
                Fund(art + ": Knopf \"Umziehen\" fehlt oder ist gesperrt");
                spiel.EinrichtenUmschalten();
                continue;
            }
            umziehen.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await ansicht.BildFertigAsync();
            if (Dialog.Offen is not { } fenster || !SichtbareTexte(fenster).Contains(frage, StringComparison.Ordinal) || KnopfMit(fenster, "Ja") is not { } ja)
            {
                Fund(art + ": Rueckfrage \"" + frage + "\" nicht zu sehen");
                spiel.EinrichtenUmschalten();
                continue;
            }
            var geld = spiel.Ablauf.Stand.Geld;
            var uhr = Stopwatch.StartNew();
            ja.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await seite.Flaeche.NaechstesBildAsync();
            await ansicht.BildFertigAsync();
            funde += Ergebnis("Spiel Umzug mit " + art + " (\"Ja\" bis zum Bild)", Messwiederholung.Einmal(uhr.Elapsed.TotalMilliseconds, Geraetegrenze));
            var stand = spiel.Ablauf.Stand;
            var ok = Py.Str(stand.Wohnung) == "zweizimmer" && stand.Geld == geld - (miete ? 375 : 2500) && stand.Miete == (miete ? 60 : 0)
                && spiel.Einrichten && spiel.ImKarton().Count > 0 && SichtbareTexte(seite).Contains("Du wohnst in: Zweizimmer-Wohnung", StringComparison.Ordinal)
                && (!miete || SichtbareTexte(seite).Contains(Wohnung.MietText(stand), StringComparison.Ordinal));
            Messwerte.Notiere(string.Create(inv, $"Spiel: Umzug mit {art}: Wohnung {Py.Str(stand.Wohnung)}, Geld {geld} -> {stand.Geld}, Miete {stand.Miete}/Tag, Kaution {stand.Kaution}, im Karton {spiel.ImKarton().Count}"));
            if (!ok)
            {
                Fund(art + ": Ergebnis weicht ab");
            }
            spiel.EinrichtenUmschalten();
            await ansicht.BildFertigAsync();
        }
        // danach wie vorher ein frisches Spiel in der Stadt (die folgenden Messungen beginnen draussen)
        Frisch(seite);
        await seite.Flaeche.NaechstesBildAsync();
        return funde;
    }

    /// <summary>Alle Texte der sichtbaren TextBlocks unter <paramref name="wurzel"/>.</summary>
    private static string SichtbareTexte(Control wurzel) => string.Join("\n",
        wurzel.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text));

    /// <summary>Sichtbarer Knopf mit genau dieser Beschriftung (erster Treffer) oder null.</summary>
    private static Button? KnopfMit(Control wurzel, string beschriftung) =>
        wurzel.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsEffectivelyVisible
            && (Equals(b.Content, beschriftung) || b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == beschriftung)));
}
