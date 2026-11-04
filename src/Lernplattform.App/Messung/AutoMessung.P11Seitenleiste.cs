using Avalonia.Controls;
using Lernplattform.Core.Texte;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.38.0 (Sammelliste 207, Weisung P11-37, Antworten 1a bis 5a): Seitenleiste wie die untere Leiste am
/// Handy und zuklappbare Gruppen der Optionen, bedient wie mit der Maus. Prueft: beim Programmstart alles zu;
/// Klick auf Lernen klappt auf und zeigt die Uebersicht; Karteikarten klappt innerhalb von Lernen; erneuter Klick
/// klappt zu; nur eine Gruppe offen; Seite ohne Gruppe klappt alles zu; Sprung (Suche, Kachel) oeffnet die Gruppe;
/// Optionen: beim Oeffnen alle Gruppen zu, Kopf nennt die Karten, Sprung oeffnet die Gruppe, nur eine offen.
/// Die Regeln selbst prueft SeitenleisteTests (mit Gegenproben). Speichert nichts.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> P11SeitenleisteAsync(MainView ansicht, IReadOnlyList<string> beimStart)
    {
        var funde = 0;
        void Fehler(string text)
        {
            Messwerte.Notiere("  FEHLER: " + text);
            funde++;
        }
        var leisten = ansicht.Leisten;
        void Erwarte(string schritt, string[] offen)
        {
            var ist = leisten.OffeneGruppen;
            Messwerte.Notiere("Seitenleiste " + schritt + ": offen [" + string.Join(", ", ist) + "]");
            if (!ist.SequenceEqual(offen))
            {
                Fehler("Seitenleiste " + schritt + ": offen [" + string.Join(", ", ist) + "], erwartet [" + string.Join(", ", offen) + "]");
            }
            foreach (var gruppe in (string[])["learn", "cards", "game"])
            {
                if (leisten.Offen(gruppe) != ist.Contains(gruppe))
                {
                    Fehler("Seitenleiste " + schritt + ": Anzeige von " + gruppe + " passt nicht zum Zustand");
                }
            }
        }
        async Task Klick(string id)
        {
            leisten.KlickFuerTests(id);
            await ansicht.BildFertigAsync();
        }
        if (beimStart.Count > 0)
        {
            Fehler("Seitenleiste beim Programmstart nicht alles zu: " + string.Join(", ", beimStart));
        }
        if (leisten.Titel("dashboard") != "Start" || leisten.Titel(Navigation.LernenId) != "Lernen")
        {
            Fehler("Seitenleiste: erster Eintrag nicht \"Start\" oder Gruppe nicht \"Lernen\"");
        }
        await ansicht.ZeigeAsync(Seiten.Rechner);
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        Erwarte("Start", []);
        await Klick(Navigation.LernenId);
        Erwarte("Klick Lernen", ["learn"]);
        if (ansicht.AktuelleSeite != Seiten.Lernen)
        {
            Fehler("Seitenleiste: Klick auf Lernen zeigt nicht die Lernen-Uebersicht, sondern " + ansicht.AktuelleSeite);
        }
        await Klick("cards");
        Erwarte("Klick Karteikarten", ["cards", "learn"]);
        // Hinweis Chat (Weisung P11-37): jeder Eintrag der offenen Gruppen rollt mit dem Fokus (Tab) ganz ins Bild
        // (Avalonia rollt den Fokus selbst ins Bild; hier wird das fuer die Seitenleiste festgehalten)
        foreach (var id in Navigation.PcLernen.Concat(Core.Inhalte.Fachbereiche.Alle).Concat(["calc", "game", "progress", "settings", "dashboard"]))
        {
            if (await leisten.FokusSichtbarFuerTestsAsync(id) is { } lage && (lage.Oben < -0.5 || lage.Unten > lage.Sichtbar + 0.5))
            {
                Fehler(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"Seitenleiste: {leisten.Titel(id)} mit Fokus nicht ganz sichtbar (oben {lage.Oben:F1}, unten {lage.Unten:F1}, sichtbar {lage.Sichtbar:F1})"));
            }
        }
        ansicht.Focus();
        await Klick("quiz");
        Erwarte("Klick Pruefungstrainer", ["cards", "learn"]);
        await Klick(Navigation.LernenId);
        Erwarte("Klick Lernen (offen)", []);
        await Klick(Navigation.LernenId);
        Erwarte("Klick Lernen (zu)", ["learn"]);
        leisten.Umschalten("game");
        await ansicht.BildFertigAsync();
        Erwarte("Spiel aufklappen", ["game"]);
        await Klick(Seiten.Rechner);
        Erwarte("Klick Rechner", []);
        await ansicht.ZeigeAsync(Seiten.Notizblock);
        Erwarte("Sprung Notizblock", ["learn"]);
        await ansicht.ZeigeAsync(Seiten.Fortschritt);
        Erwarte("Sprung Fortschritt", []);
        await ansicht.ZeigeAsync(Seiten.Dashboard);

        // Optionen: beim Oeffnen alle Gruppen zu, Kopf mit Kartennamen, Sprung oeffnet die Gruppe, nur eine offen
        await ansicht.ZeigeAsync(Seiten.Optionen);
        if (ansicht.GebauteSeiten[Seiten.Optionen] is not OptionenSeite optionen)
        {
            Fehler("Optionen nicht gebaut");
            return funde;
        }
        var gruppen = optionen.GruppenKlappen;
        if (gruppen.Count != Optionen.Gruppen.Count || gruppen.Any(g => g.IsExpanded))
        {
            Fehler("Optionen: beim Oeffnen nicht alle " + Optionen.Gruppen.Count + " Gruppen zu");
        }
        foreach (var (gruppe, (titel, bereiche)) in gruppen.Zip(Optionen.Gruppieren(optionen.Angezeigt)))
        {
            var kurz = string.Join(" · ", bereiche.Select(b => b.Titel));
            var texte = gruppe.GetVisualDescendantsTexte();
            if (!texte.Contains(titel) || !texte.Contains(kurz))
            {
                Fehler("Optionen: Gruppe " + titel + " zeigt Titel oder Kartennamen nicht (" + kurz + ")");
            }
        }
        optionen.OeffneBereich(Optionen.Optik, false);
        await ansicht.BildFertigAsync();
        optionen.OeffneBereich(Optionen.Updates, false);
        await ansicht.BildFertigAsync();
        var offen = gruppen.Where(g => g.IsExpanded).Select(g => Avalonia.Automation.AutomationProperties.GetName(g)).ToList();
        Messwerte.Notiere("Optionen nach Sprung zu Aussehen, dann Updates: offen [" + string.Join(", ", offen) + "]");
        if (!offen.SequenceEqual([Optionen.GruppeProgramm]))
        {
            Fehler("Optionen: nach dem Sprung zu Updates nicht nur PROGRAMM offen: " + string.Join(", ", offen));
        }
        optionen.Klappe(Optionen.Optik)!.IsExpanded = false;
        optionen.Klappe(Optionen.Updates)!.IsExpanded = false;
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        await ansicht.ZeigeAsync(Seiten.Optionen);
        if (gruppen.Any(g => g.IsExpanded))
        {
            Fehler("Optionen: Gruppe bleibt beim erneuten Oeffnen der Seite offen");
        }
        await ansicht.ZeigeAsync(Seiten.Dashboard);
        return funde;
    }

    private static List<string> GetVisualDescendantsTexte(this Control wurzel) =>
        [.. Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(wurzel).OfType<TextBlock>().Select(t => t.Text ?? "")];
}
