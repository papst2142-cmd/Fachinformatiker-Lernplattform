using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle E4 „Aufgaben an Gebäuden“ (S 0.53.0, Weisung P12-247, Nico 1a 2a 3c 4b): Abschnitt „Arbeitsort“ in der Karte einer
/// NEUEN Quest (Gebäude, Etage, Raum, Stelle, Schalter „Questgeber wartet am Arbeitsort“), bei vorhandenen Quests nur der
/// Hinweis, warum es dort keinen gibt (1a). In der Draufsicht die Linie vom Questgeber zum Arbeitsort mit der Marke „A“
/// zum Ziehen (4b): Loslassen auf einem Haus mit Ort und Innenraum = ein Schritt, sonst kein Schritt und ein sichtbarer
/// Hinweis, Escape bricht ab. Danach springen Raum und Stelle auf die erste freie Stelle im ersten Raum.
/// </summary>
public sealed partial class Hauptansicht
{
    private string? _arbeitSetzen;

    /// <summary>Start-Test: laeuft gerade „Stelle wählen“ fuer diese Quest?</summary>
    internal string? ArbeitsortSetzenFuer => _arbeitSetzen;

    /// <summary>Ort des Spiels (buero, Kunde, Geschaeft, Schalter) zu einem Ort der Karte; null = keiner.</summary>
    private string? SpielOrtVon(string? karteOrt) =>
        _projekt is null || karteOrt is null || AussenBearbeitung.Ort(_projekt.AussenStand, karteOrt) is not { } o ? null
            : Innenraum.OrtFuer(SpielDaten.Get(o, "ansicht") as string);

    /// <summary>Ort der Karte zu einem Ort des Spiels (erster passender), null = keiner.</summary>
    private string? KarteOrtVon(string spielOrt) =>
        _projekt is null ? null : AussenBearbeitung.Orte(_projekt.AussenStand).Where(o => Innenraum.OrtFuer(SpielDaten.Get(o, "ansicht") as string) == spielOrt)
            .Select(o => o["id"] as string).FirstOrDefault(i => i is not null);

    /// <summary>Name eines Spiel-Orts fuer die Anzeige (Name des Hauses in der Karte).</summary>
    internal string ArbeitsortName(string spielOrt) => KarteOrtVon(spielOrt) is { } k ? _projekt!.OrtName(k) : spielOrt;

    /// <summary>Mitte des Hauses eines Orts der Karte (Weltpunkt), null = kein Haus.</summary>
    private Point? HausMitte(string? karteOrt)
    {
        if (_projekt is null || karteOrt is null)
        {
            return null;
        }
        var a = _projekt.AussenStand;
        foreach (var (s, _, _) in AussenBearbeitung.Alle(a).Where(t => t.Stelle.Art == AussenArt.Haus && t.Stelle.Ort == karteOrt).ToList())
        {
            var (x, y, w, h) = AussenBearbeitung.HausFlaeche(a, s);
            return new Point(x + (w / 2), y + (h / 2));
        }
        return null;
    }

    /// <summary>Marke „A“ und Linie in der Draufsicht zur gewaehlten Quest (bei jedem neuen Stand).</summary>
    private void ArbeitsortMarkeNeu()
    {
        Karte.Arbeitsort = null;
        if (_projekt is null || _questGewaehlt is not { } id || QuestDaten.NeueQuest(_projekt.AussenStand, id) is not { } q
            || Questebene.ArbeitsortLesen(q) is not { } a || HausMitte(KarteOrtVon(a.Ort)) is not { } ziel)
        {
            Karte.InvalidateVisual();
            return;
        }
        Point? von = null;
        if (SpielDaten.Get(q, "questgeber") is string qid && _projekt.Questgeber().FirstOrDefault(x => x.Id == qid) is { } qg)
        {
            von = qg.Platz is { Draussen: true } p ? new Point(p.X, p.Y) : QuestgeberDaten.Stelle(qg) is { } st ? HausMitte(KarteOrtVon(st)) : null;
        }
        Karte.Arbeitsort = new ArbeitsortMarke(id, ziel, von, ArbeitsortName(a.Ort));
        Karte.InvalidateVisual();
    }

    /// <summary>Marke losgelassen: Haus pruefen, Arbeitsort setzen (ein Schritt) oder sichtbar melden, warum nicht.</summary>
    internal void ArbeitsortGezogen(string quest, string? karteOrt)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return;
        }
        var spielOrt = SpielOrtVon(karteOrt);
        if (spielOrt is null || _projekt.Arbeitsorte().All(w => w.Ort != spielOrt))
        {
            Melden(Texte.QuestArbeitsortZiehenUngueltig);
            ArbeitsortMarkeNeu();
            return;
        }
        if (QuestDaten.NeueQuest(_projekt.AussenStand, quest) is { } q && Questebene.ArbeitsortLesen(q) is { } alt && alt.Ort == spielOrt)
        {
            Melden(Texte.QuestArbeitsortGleich);
            ArbeitsortMarkeNeu();
            return;
        }
        if (!_projekt.QuestArbeitsort(quest, spielOrt))
        {
            Melden(Texte.QuestArbeitsortKeineStelle);
            ArbeitsortMarkeNeu();
            return;
        }
        Melden(string.Format(CultureInfo.InvariantCulture, Texte.QuestArbeitsortGesetzt, ArbeitsortBeschreibung(quest)));
        ArbeitsortMarkeNeu();
    }

    /// <summary>„Talheim-Nord, Raum Technik, Stelle 3.5 / 2.5“ (fuer Meldung und Karte).</summary>
    internal string ArbeitsortBeschreibung(string quest)
    {
        if (_projekt is null || QuestDaten.NeueQuest(_projekt.AussenStand, quest) is not { } q || Questebene.ArbeitsortLesen(q) is not { } a)
        {
            return "";
        }
        var teile = new List<string> { ArbeitsortName(a.Ort) };
        if (a.Etage > 0)
        {
            teile.Add(Texte.QuestArbeitsortEtageName(a.Etage));
        }
        if (a.Raum.Length > 0 && _projekt.ArbeitsortGrundriss(a.Ort, a.Etage) is { } g)
        {
            teile.Add(Texte.QuestArbeitsortRaumKurz + " " + (Questebene.Raeume(g).FirstOrDefault(r => r.Id == a.Raum).Name ?? a.Raum));
        }
        teile.Add(Texte.QuestArbeitsortStelleWert(a.X, a.Y));
        return string.Join(", ", teile);
    }

    /// <summary>Abschnitt „Arbeitsort“ der Quest-Karte (neue Quest) bzw. nur der Hinweis (vorhandene Quest, 1a).</summary>
    private Control ArbeitsortBauen(QuestDaten.Quest q, bool schreiben)
    {
        var stapel = new StackPanel { Spacing = 8 };
        if (q.Bestand || _projekt is null)
        {
            stapel.Children.Add(Hinweis(Texte.QuestArbeitsortBestand));
            return Beschriftet(Texte.QuestArbeitsortTitel, stapel);
        }
        var id = q.Id;
        var a = Questebene.ArbeitsortLesen(q.Daten);
        stapel.Children.Add(Hinweis(Texte.QuestArbeitsortErklaerung));
        var wahlen = _projekt.Arbeitsorte();
        var ort = Wahl(Texte.QuestArbeitsortOrt, Texte.QuestArbeitsortErklaerung,
            [(Texte.QuestArbeitsortKeiner, ""), .. wahlen.Select(w => (ArbeitsortName(w.Ort), (object?)w.Ort))],
            a?.Ort ?? "", t =>
            {
                if (t is string { Length: > 0 } o)
                {
                    if (!_projekt.QuestArbeitsort(id, o))
                    {
                        Melden(Texte.QuestArbeitsortKeineStelle);
                    }
                }
                else
                {
                    _projekt.QuestArbeitsortEntfernen(id);
                }
            });
        ort.IsEnabled = schreiben;
        stapel.Children.Add(Beschriftet(Texte.QuestArbeitsortOrt, ort));
        if (a is null)
        {
            return Beschriftet(Texte.QuestArbeitsortTitel, stapel);
        }
        var art = wahlen.FirstOrDefault(w => w.Ort == a.Ort)?.Art;
        if (art == Questebene.ArbeitsortArt.Geschaeft)
        {
            var etagen = new List<int> { 0 };
            for (var k = 1; k < 10 && _projekt.ArbeitsortGrundriss(a.Ort, k) is not null; k++)
            {
                etagen.Add(k);
            }
            if (etagen.Count > 1)
            {
                var etage = Wahl(Texte.QuestArbeitsortEtage, Texte.QuestArbeitsortEtage, etagen.Select(k => (Texte.QuestArbeitsortEtageName(k), (object?)(long)k)), (long)a.Etage,
                    k => _projekt.QuestArbeitsort(id, a.Ort, (int)(long)k!));
                etage.IsEnabled = schreiben;
                stapel.Children.Add(Beschriftet(Texte.QuestArbeitsortEtage, etage));
            }
            stapel.Children.Add(Schrift.Text(Texte.QuestArbeitsortGeschaeft + " · " + Texte.QuestArbeitsortStelleWert(a.X, a.Y), "Klein"));
            var waehlen = Knopf(Texte.QuestArbeitsortStelleWaehlen, Texte.QuestArbeitsortStelleWaehlenTipp, () => ArbeitsortStelleWaehlen(id), betont: true);
            waehlen.IsEnabled = schreiben;
            stapel.Children.Add(waehlen);
        }
        else if (_projekt.ArbeitsortGrundriss(a.Ort, a.Etage) is { } g)
        {
            var raum = Wahl(Texte.QuestArbeitsortRaum, Texte.QuestArbeitsortRaum, Questebene.Raeume(g).Select(r => (r.Name, (object?)r.Id)), a.Raum,
                r =>
                {
                    if (r is string rid && !_projekt.QuestArbeitsortRaum(id, rid))
                    {
                        Melden(Texte.QuestArbeitsortKeineStelle);
                    }
                });
            raum.IsEnabled = schreiben;
            stapel.Children.Add(Beschriftet(Texte.QuestArbeitsortRaum, raum));
            var auto = Schrift.Text(Texte.QuestArbeitsortStelleAuto + " " + Texte.QuestArbeitsortStelleWert(a.X, a.Y), "Klein", farbe: "Pinsel.muted");
            auto.TextWrapping = TextWrapping.Wrap;
            stapel.Children.Add(auto);
        }
        var wartet = new CheckBox { Content = Schrift.Text(Texte.QuestArbeitsortWartet), IsChecked = a.Wartet, MinHeight = 36, IsEnabled = schreiben }
            .Vorlese(Texte.QuestArbeitsortWartet).Mit(Texte.QuestArbeitsortWartetTipp);
        wartet.IsCheckedChanged += (_, _) => _projekt.QuestArbeitsortWartet(id, wartet.IsChecked == true);
        stapel.Children.Add(wartet);
        stapel.Children.Add(Hinweis(Texte.QuestArbeitsortWartetTipp));
        var knoepfe = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        var zeigen = Knopf(Texte.QuestArbeitsortZeigen, Texte.QuestArbeitsortZeigenTipp, () =>
        {
            ReiterZeigen(innen: false);
            ArbeitsortMarkeNeu();
            Melden(Texte.QuestArbeitsortZiehenTipp);
        });
        var entfernen = Knopf(Texte.QuestArbeitsortEntfernen, Texte.QuestArbeitsortEntfernenTipp, () => _projekt.QuestArbeitsortEntfernen(id));
        entfernen.IsEnabled = schreiben;
        knoepfe.Children.Add(zeigen);
        knoepfe.Children.Add(entfernen);
        stapel.Children.Add(knoepfe);
        stapel.Children.Add(Hinweis(Texte.QuestArbeitsortZiehenTipp));
        return Beschriftet(Texte.QuestArbeitsortTitel, stapel);
    }

    /// <summary>„Stelle wählen“ im Geschaeft oder am Schalter: Grundriss oeffnen, der naechste Tipp setzt die Stelle (ein Schritt).</summary>
    internal void ArbeitsortStelleWaehlen(string id)
    {
        if (_projekt is null || _projekt.NurLesen || QuestDaten.NeueQuest(_projekt.AussenStand, id) is not { } q || Questebene.ArbeitsortLesen(q) is not { } a)
        {
            return;
        }
        var innen = Etagen.Schluessel(a.Ort, a.Etage);
        if (!_projekt.GibtInnenort(innen))
        {
            Melden(Texte.QuestArbeitsortKeineStelle);
            return;
        }
        QgSetzenBeenden();
        ReiterZeigen(innen: true);
        OrtOeffnen(innen);
        _arbeitSetzen = id;
        Flaeche.QuestgeberSetzen = (x, y) =>
        {
            ArbeitsortSetzenBeenden();
            if (_projekt.QuestArbeitsortStelle(id, x, y))
            {
                Melden(string.Format(CultureInfo.InvariantCulture, Texte.QuestArbeitsortGesetzt, ArbeitsortBeschreibung(id)));
            }
            else
            {
                Melden(Texte.QuestArbeitsortKeineStelle);
            }
            QuestOeffnen(id);
        };
        Melden(Texte.QuestArbeitsortStelleHinweis);
    }

    private void ArbeitsortSetzenBeenden()
    {
        if (_arbeitSetzen is null)
        {
            return;
        }
        _arbeitSetzen = null;
        Flaeche.QuestgeberSetzen = null;
    }

    /// <summary>Escape: „Stelle wählen“ oder das Ziehen der Marke abbrechen (true = es lief etwas).</summary>
    private bool ArbeitsortAbbrechen()
    {
        if (Karte.ArbeitsortAbbrechen())
        {
            return true;
        }
        if (_arbeitSetzen is not { } id)
        {
            return false;
        }
        ArbeitsortSetzenBeenden();
        QuestOeffnen(id);
        return true;
    }

    // ---- Wie Bilder 2 und 3 aus Meldung P12-147: wer arbeitet hier? ----

    private readonly StackPanel _arbeitHier = new() { Spacing = 6 };
    private Control? _arbeitHierKarte;

    /// <summary>Start-Test: Texte der Karte „Arbeitsort hier“ im Innenraum (leer = Karte nicht sichtbar).</summary>
    internal IEnumerable<string> ArbeitsortHierTexte => _arbeitHierKarte is { IsVisible: true } ? Texte_(_arbeitHier) : [];

    /// <summary>Start-Test: Texte unter „Ausgewähltes Stück“ draussen.</summary>
    internal IEnumerable<string> AuswahlDraussenTexte => Texte_(_aussenAuswahl);

    /// <summary>Neue Quests mit Arbeitsort an diesem Spiel-Ort (optional nur auf dieser Etage), in der Reihenfolge der Liste.</summary>
    private List<(QuestDaten.Quest Quest, Questebene.Arbeitsort Ort)> ArbeitsortHier(string spielOrt, int? etage = null) =>
        _projekt is null ? [] : [.. _projekt.Quests().Where(q => !q.Bestand)
            .Select(q => (Quest: q, Ort: Questebene.ArbeitsortLesen(q.Daten)))
            .Where(t => t.Ort is { } a && a.Ort == spielOrt && (etage is null || a.Etage == etage))
            .Select(t => (t.Quest, t.Ort!))];

    private string QgName(string? id) =>
        _projekt?.Questgeber().FirstOrDefault(x => x.Id == id) is { } qg ? (qg.Name.Trim().Length > 0 ? qg.Name : qg.Id) : "–";

    /// <summary>Draussen, gewaehltes Haus: „Quests mit Arbeitsort hier (n)“ mit „Quest öffnen“ (Bild 2).</summary>
    private void ArbeitsortTeil(AussenStelle? s)
    {
        if (s is not { Art: AussenArt.Haus } h || SpielOrtVon(h.Ort) is not { } spiel || ArbeitsortHier(spiel) is not { Count: > 0 } liste)
        {
            return;
        }
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.QuestArbeitsortHier(liste.Count), "Mittel", FontWeight.Bold));
        foreach (var (q, a) in liste)
        {
            var titel = Schrift.Text(q.Titel.Trim().Length > 0 ? q.Titel : q.Id, "Klein", FontWeight.Bold);
            titel.TextWrapping = TextWrapping.Wrap;
            var zeile = string.Format(CultureInfo.InvariantCulture, Texte.QuestArbeitsortHierZeile, q.AbTag, QgName(q.Questgeber));
            if (_projekt!.ArbeitsortGrundriss(a.Ort, a.Etage) is { } g && a.Raum.Length > 0)
            {
                zeile += " · " + (Questebene.Raeume(g).FirstOrDefault(r => r.Id == a.Raum).Name ?? a.Raum);
            }
            var unter = Schrift.Text(zeile, "Klein", farbe: "Pinsel.muted");
            unter.TextWrapping = TextWrapping.Wrap;
            var id = q.Id;
            var knopf = Knopf(Texte.QuestArbeitsortOeffnen, Texte.QuestArbeitsortOeffnenTipp, () =>
            {
                QuestSeiteZeigen(Texte.Quests);
                QuestOeffnen(id);
            });
            _aussenAuswahl.Children.Add(titel);
            _aussenAuswahl.Children.Add(unter);
            _aussenAuswahl.Children.Add(knopf);
        }
    }

    /// <summary>Karte „Arbeitsort hier“ rechts im Innenraum (Bild 3); unsichtbar, wenn hier keine Quest arbeitet.</summary>
    private Control ArbeitsortHierKarte() => _arbeitHierKarte = Schrift.Karte(_arbeitHier);

    /// <summary>Im offenen Innenraum: Marken der Arbeitsorte (Stelle der Quest) und die Karte rechts.</summary>
    private void ArbeitsortInnenNeu()
    {
        _arbeitHier.Children.Clear();
        var marken = new List<QuestgeberMarke>();
        var hier = _ort is { } o && _projekt is not null
            ? _projekt.Arbeitsorte().Select(w => w.Ort).Distinct()
                .SelectMany(ort => ArbeitsortHier(ort).Where(t => Etagen.Schluessel(t.Ort.Ort, t.Ort.Etage) == o)).ToList()
            : [];
        foreach (var (q, a) in hier)
        {
            var name = a.Wartet ? string.Format(CultureInfo.InvariantCulture, Texte.QuestArbeitsortMarkeQuest, QgName(q.Questgeber)) : Texte.QuestArbeitsortMarkeAuftragName;
            marken.Add(new QuestgeberMarke("arbeitsort:" + q.Id, a.X, a.Y, 0, name, q.Id == _questGewaehlt) { Arbeitsort = true });
            var titel = q.Titel.Trim().Length > 0 ? q.Titel : q.Id;
            var raum = _projekt!.ArbeitsortGrundriss(a.Ort, a.Etage) is { } g && a.Raum.Length > 0
                ? " (" + Texte.QuestArbeitsortRaumKurz + " " + (Questebene.Raeume(g).FirstOrDefault(r => r.Id == a.Raum).Name ?? a.Raum) + ")" : "";
            var satz = string.Format(CultureInfo.InvariantCulture, a.Wartet ? Texte.QuestArbeitsortMarkeWartet : Texte.QuestArbeitsortMarkeAuftrag, QgName(q.Questgeber));
            var t = Schrift.Text(titel + raum + ". " + satz, "Klein", farbe: "Pinsel.muted");
            t.TextWrapping = TextWrapping.Wrap;
            _arbeitHier.Children.Add(t);
        }
        if (hier.Count > 0)
        {
            _arbeitHier.Children.Insert(0, Schrift.Text(Texte.QuestArbeitsortInnenHier(hier.Count), "Mittel", FontWeight.Bold));
        }
        if (_arbeitHierKarte is { } k)
        {
            k.IsVisible = hier.Count > 0;
        }
        Flaeche.ArbeitsortMarken = marken;
    }
}
