using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.56.0 (Sammelliste 362, Weisung P12-287, Nico 1a 2b 3a 4a): Knopf „Plätze“ (Taste P, nur „Stadt von oben“).
/// Auf der Karte das Raster der Plaetze (64 × 64) mit Nummern; Tippen waehlt einen Platz. Rechts zwei Teile:
/// <list type="bullet">
/// <item>„Karte vergrößern“: neue Breite und Tiefe (64 bis 256) und der Platz der heutigen Stadt; alles rueckt genau dorthin
/// (<see cref="Projekt.KarteVergroessern"/>, ein Schritt).</item>
/// <item>„Stadtteil einsetzen“: ein anderes Projekt waehlen, dann den Platz (Tippen oder Auswahl); der Rahmen ist gruen, wenn
/// es passt, sonst rot mit dem Grund (gleiche Rechnung wie das Einsetzen). Gelbe Hinweise stehen darunter.</item>
/// </list>
/// Alles geht mit Maus, Finger, Tastatur (Auswahllisten, Eingabetaste, Pfeile waehlen den Platz) und Screenreader.
/// </summary>
public sealed partial class Hauptansicht
{
    private Button _plaetzeChip = null!;
    private int _vergroessernBreite;
    private int _vergroessernTiefe;
    private int _vergroessernPlatz = 1;
    private string? _einsetzenDatei;
    private Projekt? _einsetzenQuelle;
    private int? _einsetzenPlatz;
    private Projekt.EinsetzenErgebnis? _einsetzenVorschau;
    private string? _plaetzeFehler;
    private Projekt.StadtSetzenPlan? _stadtPlan;
    private string? _stadtBestaetigt;

    internal Button PlaetzeKnopf => _plaetzeChip;

    /// <summary>Start-Test: Vorschau des Einsetzens (null = kein Stadtteil oder kein Platz gewaehlt) und die rote Zeile.</summary>
    internal Projekt.EinsetzenErgebnis? EinsetzenVorschau => _einsetzenVorschau;

    internal string? PlaetzeFehler => _plaetzeFehler;

    /// <summary>Start-Test (Sammelliste 373): die Wahl fuer „Heutige Stadt auf einen Platz setzen“ (Breite, Tiefe, Platz).</summary>
    internal (int Breite, int Tiefe, int Platz) VergroessernWahl => (_vergroessernBreite, _vergroessernTiefe, _vergroessernPlatz);

    /// <summary>Start-Test (Sammelliste 373): Vorschau (gleiche Rechnung wie der Schritt) und die gruene Bestaetigung.</summary>
    internal Projekt.StadtSetzenPlan? StadtPlan => _stadtPlan;

    internal string? StadtBestaetigt => _stadtBestaetigt;

    /// <summary>
    /// Gegenprobe Sammelliste 373: „Plätze“ wie bis S 0.56.1 (Tippen waehlt nur den Platz fuer „Stadtteil einsetzen“, keine
    /// Vorschau der Stadt, kein Speichern beim Vergroessern). Nur Start-Test.
    /// </summary>
    internal static bool PlaetzeAltFuerTests { get; set; }

    /// <summary>
    /// Welle S 0.56.2 (Fund Nico 373): Vorschau der heutigen Stadt rechnen (gleiche Rechnung wie „Übernehmen“) und auf der Karte
    /// zeigen; ohne gewaehlten Stadtteil tippt man auf dem Raster der neuen Groesse den Platz der Stadt.
    /// </summary>
    private void StadtVorschauRechnen()
    {
        if (_projekt is null || !Karte.PlaetzeAn || PlaetzeAltFuerTests)
        {
            _stadtPlan = null;
            Karte.StadtVorschauZeigen(null, false);
            Karte.PlaetzeRaster = null;
            return;
        }
        _stadtPlan = _projekt.StadtSetzenPruefen(_vergroessernBreite, _vergroessernTiefe, _vergroessernPlatz);
        Karte.PlaetzeRaster = _einsetzenQuelle is null ? (_vergroessernBreite, _vergroessernTiefe) : null;
        Karte.StadtVorschauZeigen(_einsetzenQuelle is null ? _stadtPlan.Nachher : null, _stadtPlan.Geht);
    }

    /// <summary>Nach einer Wahl rechts: erst nach dem Ereignis der Auswahl neu aufbauen (die Auswahl entfernt sich nie selbst).</summary>
    private void NachWahlNeu()
    {
        _stadtBestaetigt = null;
        _plaetzeFehler = null;
        if (PlaetzeAltFuerTests)
        {
            AussenAuswahlZeigen();
            return;
        }
        StadtVorschauRechnen();
        Dispatcher.UIThread.Post(AussenAuswahlZeigen, DispatcherPriority.Background);
    }

    /// <summary>Platz der heutigen Stadt waehlen (Tippen, Pfeile, Auswahl; auch Start-Test).</summary>
    internal void StadtPlatzWaehlen(int nummer)
    {
        if (_projekt is null)
        {
            return;
        }
        if (!Plaetze.Gibt(nummer, _vergroessernBreite, _vergroessernTiefe))
        {
            Melden(Texte.PlatzDaneben);
            return;
        }
        _vergroessernPlatz = nummer;
        _stadtBestaetigt = null;
        _plaetzeFehler = null;
        StadtVorschauRechnen();
        AussenAuswahlZeigen();
        Melden(_stadtPlan is { Grund: { } g } ? g : Texte.StadtVorschau(nummer, _vergroessernBreite, _vergroessernTiefe));
    }

    /// <summary>
    /// „Übernehmen und speichern“ (auch Start-Test): die Stadt setzen (ein Schritt), das Projekt speichern und gruen bestaetigen;
    /// geht es nicht, steht der Grund rot da. Null = uebernommen, sonst der Grund.
    /// </summary>
    internal string? StadtUebernehmenMitWahl()
    {
        if (_projekt is null)
        {
            return null;
        }
        var plan = _projekt.StadtSetzen(_vergroessernBreite, _vergroessernTiefe, _vergroessernPlatz);
        if (plan.Grund is { } grund)
        {
            _plaetzeFehler = grund;
            _stadtBestaetigt = null;
            StadtVorschauRechnen();
            AussenAuswahlZeigen();
            Melden(grund);
            return grund;
        }
        _plaetzeFehler = null;
        _einsetzenPlatz = null;
        try
        {
            _projekt.Speichern(vonHand: true);
            _stadtBestaetigt = Texte.StadtUebernommen(plan.Platz, plan.Breite, plan.Tiefe);
        }
        catch (Exception f) when (f is IOException or UnauthorizedAccessException)
        {
            _stadtBestaetigt = Texte.StadtUebernommenOhneSpeichern(plan.Platz, plan.Breite, plan.Tiefe, f.Message);
        }
        EinsetzenVorschauRechnen();
        StadtVorschauRechnen();
        _aussenStand++;
        Karte.Einpassen();
        AussenAuswahlZeigen();
        Melden(_stadtBestaetigt);
        return null;
    }

    /// <summary>Start-Test: Knopf oder Auswahl rechts mit diesem Namen fuer den Screenreader, sonst null.</summary>
    internal Control? PlaetzeElement(string name) =>
        _aussenAuswahl.Children.SelectMany(c => c is Panel reihe ? reihe.Children : [c])
            .FirstOrDefault(k => k is Button or ComboBox && AutomationProperties.GetName(k) == name);

    private void PlaetzeKnopfBauen()
    {
        _plaetzeChip = Chip(Texte.Plaetze, Texte.PlaetzeTipp, PlaetzeUmschalten);
        _werkzeugLeiste.Children.Insert(_werkzeugLeiste.Children.IndexOf(_ebenenHinweis), _plaetzeChip);
        Karte.PlaetzeGeaendert += () =>
        {
            ChipsZeigen();
            PlaetzeChipZeigen();
            AussenAuswahlZeigen();
        };
        Karte.PlatzGetippt += nummer => PlatzWaehlen(nummer);
        PlaetzeChipZeigen();
    }

    private void PlaetzeChipZeigen()
    {
        ChipMarkieren(_plaetzeChip, Karte.PlaetzeAn);
        AutomationProperties.SetName(_plaetzeChip, Karte.PlaetzeAn ? Texte.Gewaehlt(Texte.Plaetze) : Texte.Plaetze);
        _plaetzeChip.IsVisible = !_drei;
    }

    /// <summary>„Plätze“ an oder aus (Knopf und Taste P).</summary>
    internal void PlaetzeUmschalten()
    {
        if (_projekt is null || _innen || _geschichteOffen)
        {
            return;
        }
        if (_drei)
        {
            Melden(Texte.PlaetzeNurDraufsicht);
            return;
        }
        _plaetzeFehler = null;
        var an = !Karte.PlaetzeAn;
        if (an)
        {
            var (b, h) = Aussen.Groesse(_projekt.AussenStand);
            _vergroessernBreite = Plaetze.Seiten.FirstOrDefault(s => s >= b - 1e-9, Plaetze.Seiten[^1]);
            _vergroessernTiefe = Plaetze.Seiten.FirstOrDefault(s => s >= h - 1e-9, Plaetze.Seiten[^1]);
            _vergroessernPlatz = PlaetzeAltFuerTests ? 1 : _projekt.StadtPlatz();
            _einsetzenPlatz = null;
            _einsetzenVorschau = null;
            _stadtBestaetigt = null;
        }
        Karte.PlaetzeAn = an;
        StadtVorschauRechnen();
        if (an)
        {
            Melden(PlaetzeAltFuerTests ? Texte.PlaetzeErklaerung : _einsetzenQuelle is null ? Texte.PlaetzeErklaerungStadt : Texte.PlaetzeErklaerungEinsetzen);
        }
    }

    /// <summary>Platz waehlen (Tippen, Auswahl, Pfeile; auch Start-Test). 0 = daneben getippt.</summary>
    internal void PlatzWaehlen(int nummer)
    {
        if (_projekt is null)
        {
            return;
        }
        if (_einsetzenQuelle is null && !PlaetzeAltFuerTests)
        {
            // Welle S 0.56.2 (Fund Nico 373): ohne gewaehlten Stadtteil waehlt Tippen den Platz der heutigen Stadt
            StadtPlatzWaehlen(nummer);
            return;
        }
        var (b, h) = Aussen.Groesse(_projekt.AussenStand);
        if (!Plaetze.Gibt(nummer, b, h))
        {
            Melden(Texte.PlatzDaneben);
            return;
        }
        _einsetzenPlatz = nummer;
        _plaetzeFehler = null;
        EinsetzenVorschauRechnen();
        AussenAuswahlZeigen();
        Melden(_einsetzenVorschau is { } v
            ? Texte.PlatzGewaehlt(nummer) + " " + (v.Grund ?? Texte.EinsetzenVorschauFrei)
            : Texte.PlatzGewaehlt(nummer));
    }

    /// <summary>Gleiche Rechnung wie das Einsetzen (aendert nichts); zeigt den Rahmen gruen oder rot.</summary>
    private void EinsetzenVorschauRechnen()
    {
        _einsetzenVorschau = _projekt is not null && _einsetzenQuelle is { } q && _einsetzenPlatz is { } p ? _projekt.EinsetzenPruefen(q, p) : null;
        Karte.PlatzZeigen(_einsetzenPlatz, _einsetzenVorschau?.Geht);
    }

    /// <summary>Anderes Projekt fuer „Stadtteil einsetzen“ waehlen (nur lesen; auch Start-Test). Null = gelungen, sonst Grund.</summary>
    internal string? EinsetzenQuelleWaehlen(string? datei)
    {
        _einsetzenDatei = datei;
        _einsetzenQuelle = null;
        _plaetzeFehler = null;
        if (datei is not null && _daten is { } daten)
        {
            try
            {
                _einsetzenQuelle = Projekt.Oeffnen(datei, daten, _ordner);
            }
            catch (StadtbauFehler f)
            {
                _plaetzeFehler = Texte.EinsetzenNichtLesbar(Projekt.AnzeigeName(datei)) + " " + string.Join(" ", f.Funde);
            }
            catch (IOException f)
            {
                _plaetzeFehler = Texte.EinsetzenNichtLesbar(Projekt.AnzeigeName(datei)) + " " + f.Message;
            }
        }
        EinsetzenVorschauRechnen();
        StadtVorschauRechnen();
        AussenAuswahlZeigen();
        if (_plaetzeFehler is { } fehler)
        {
            Melden(fehler);
            return fehler;
        }
        return null;
    }

    /// <summary>Karte vergroessern mit der Wahl rechts (auch Start-Test); null = vergroessert, sonst der Grund.</summary>
    internal string? KarteVergroessernMitWahl()
    {
        if (_projekt is null)
        {
            return null;
        }
        if (!PlaetzeAltFuerTests)
        {
            return StadtUebernehmenMitWahl();
        }
        if (_projekt.KarteVergroessern(_vergroessernBreite, _vergroessernTiefe, _vergroessernPlatz) is { } grund)
        {
            _plaetzeFehler = grund;
            AussenAuswahlZeigen();
            Melden(grund);
            return grund;
        }
        _plaetzeFehler = null;
        _einsetzenPlatz = null;
        EinsetzenVorschauRechnen();
        _aussenStand++;
        AussenAuswahlZeigen();
        Melden(Texte.KarteVergroessernSchritt(_vergroessernBreite, _vergroessernTiefe, _vergroessernPlatz));
        return null;
    }

    /// <summary>Gewaehlten Stadtteil auf dem gewaehlten Platz einsetzen (auch Start-Test); null = eingesetzt, sonst der Grund.</summary>
    internal string? StadtteilEinsetzenMitWahl()
    {
        if (_projekt is null || _einsetzenQuelle is not { } q || _einsetzenPlatz is not { } p)
        {
            return Texte.EinsetzenWahlFehlt;
        }
        var e = _projekt.StadtteilEinsetzen(q, p);
        if (e.Grund is { } grund)
        {
            _plaetzeFehler = grund;
            EinsetzenVorschauRechnen();
            AussenAuswahlZeigen();
            Melden(grund);
            return grund;
        }
        _plaetzeFehler = null;
        // Weisung P12-291 Punkt 2 (E1, mit Sammelliste 367): nach dem Einsetzen ist der Platz belegt; die Auswahl geht zurueck,
        // damit rechts nicht „Platz … liegt schon etwas“ rot stehen bleibt, als waere es schiefgegangen
        _einsetzenPlatz = null;
        EinsetzenVorschauRechnen();
        _aussenStand++;
        AussenAuswahlZeigen();
        Melden(Texte.EinsetzenSchritt(q.Name, p) + (e.Hinweise.Count > 0 ? " " + Texte.EinsetzenHinweise(e.Hinweise.Count) : ""));
        return null;
    }

    /// <summary>Tasten bei „Plätze“: Pfeile waehlen den Nachbarplatz, Eingabetaste setzt ein, Esc beendet.</summary>
    private bool PlaetzeTaste(Key taste)
    {
        if (!Karte.PlaetzeAn || _projekt is null)
        {
            return false;
        }
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is ComboBox or TextBox)
        {
            return false;
        }
        var stadt = _einsetzenQuelle is null && !PlaetzeAltFuerTests;
        var (b, h) = stadt ? (_vergroessernBreite, _vergroessernTiefe) : Aussen.Groesse(_projekt.AussenStand);
        var spalten = Plaetze.Spalten(b);
        var jetzt = stadt ? _vergroessernPlatz : _einsetzenPlatz ?? 1;
        switch (taste)
        {
            case Key.Escape:
                Karte.PlaetzeAn = false;
                return true;
            case Key.Left or Key.Right or Key.Up or Key.Down:
            {
                var neu = jetzt + (taste == Key.Left ? -1 : taste == Key.Right ? 1 : taste == Key.Up ? -spalten : spalten);
                var gleicheReihe = taste is Key.Up or Key.Down || (neu - 1) / spalten == (jetzt - 1) / spalten;
                PlatzWaehlen(!stadt && _einsetzenPlatz is null ? 1 : Plaetze.Gibt(neu, b, h) && gleicheReihe ? neu : jetzt);
                return true;
            }
            default:
                return false;
        }
    }

    /// <summary>Gegenprobe Sammelliste 373: der Teil „Karte vergrößern“ wie bis S 0.56.1.</summary>
    private void PlaetzeVergroessernAlt(bool lesen, double b, double h)
    {
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.KarteVergroessern, "Normal", FontWeight.Bold));
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.KarteGroesseJetzt(b, h), "Klein"));
        var seiten = Plaetze.Seiten.Select(s => (Texte.Felder(s), (object?)s)).ToList();
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.NeueBreite, "Klein"));
        _aussenAuswahl.Children.Add(Wahl(Texte.NeueBreite, Texte.KarteVergroessernTipp, seiten, _vergroessernBreite, t =>
        {
            _vergroessernBreite = (int)t!;
            _vergroessernPlatz = Math.Min(_vergroessernPlatz, Math.Max(1, Plaetze.Anzahl(_vergroessernBreite, _vergroessernTiefe)));
            AussenAuswahlZeigen();
        }));
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.NeueTiefe, "Klein"));
        _aussenAuswahl.Children.Add(Wahl(Texte.NeueTiefe, Texte.KarteVergroessernTipp, seiten, _vergroessernTiefe, t =>
        {
            _vergroessernTiefe = (int)t!;
            _vergroessernPlatz = Math.Min(_vergroessernPlatz, Math.Max(1, Plaetze.Anzahl(_vergroessernBreite, _vergroessernTiefe)));
            AussenAuswahlZeigen();
        }));
        var anzahlNeu = Math.Max(1, Plaetze.Anzahl(_vergroessernBreite, _vergroessernTiefe));
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.HeutigeStadtAuf, "Klein"));
        _aussenAuswahl.Children.Add(Wahl(Texte.HeutigeStadtAuf, Texte.KarteVergroessernTipp,
            Enumerable.Range(1, anzahlNeu).Select(p => (Texte.PlatzNummer(p), (object?)p)), _vergroessernPlatz, t =>
            {
                _vergroessernPlatz = (int)t!;
                AussenAuswahlZeigen();
            }));
        var warum = _projekt!.VergroessernNicht(_vergroessernBreite, _vergroessernTiefe, _vergroessernPlatz);
        if (warum is not null && !lesen)
        {
            _aussenAuswahl.Children.Add(Schrift.Text(warum, "Klein", farbe: "Pinsel.muted"));
        }
        var vergroessern = Knopf(Texte.KarteVergroessernKnopf, Texte.KarteVergroessernTipp, () => KarteVergroessernMitWahl(), betont: true).Vorlese(Texte.KarteVergroessernKnopf);
        vergroessern.IsEnabled = !lesen && warum is null;
        _aussenAuswahl.Children.Add(vergroessern);
    }

    /// <summary>
    /// Welle S 0.56.2 (Fund Nico 373, Nicos Wunsch woertlich): „Heutige Stadt auf einen Platz setzen“: Breite, Tiefe und Platz
    /// waehlen (rechts oder Tippen auf die Karte), die Karte zeigt blau, wo die Stadt landet, die Zeile darunter sagt es in Worten;
    /// geht es nicht, steht der Grund rot da (nie grau). Erst „Übernehmen und speichern“ setzt die Stadt (ein Schritt), speichert
    /// das Projekt und bestaetigt gruen.
    /// </summary>
    private void StadtSetzenTeil(bool lesen, double b, double h)
    {
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.StadtSetzenTitel, "Normal", FontWeight.Bold));
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.KarteGroesseJetzt(b, h) + " " + Texte.StadtSetzenErklaerung, "Klein"));
        var seiten = Plaetze.Seiten.Select(s => (Texte.Felder(s), (object?)s)).ToList();
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.NeueBreite, "Klein"));
        _aussenAuswahl.Children.Add(Wahl(Texte.NeueBreite, Texte.KarteVergroessernTipp, seiten, _vergroessernBreite, t =>
        {
            _vergroessernBreite = (int)t!;
            _vergroessernPlatz = Math.Min(_vergroessernPlatz, Math.Max(1, Plaetze.Anzahl(_vergroessernBreite, _vergroessernTiefe)));
            NachWahlNeu();
        }));
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.NeueTiefe, "Klein"));
        _aussenAuswahl.Children.Add(Wahl(Texte.NeueTiefe, Texte.KarteVergroessernTipp, seiten, _vergroessernTiefe, t =>
        {
            _vergroessernTiefe = (int)t!;
            _vergroessernPlatz = Math.Min(_vergroessernPlatz, Math.Max(1, Plaetze.Anzahl(_vergroessernBreite, _vergroessernTiefe)));
            NachWahlNeu();
        }));
        var anzahlNeu = Math.Max(1, Plaetze.Anzahl(_vergroessernBreite, _vergroessernTiefe));
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.HeutigeStadtAuf, "Klein"));
        _aussenAuswahl.Children.Add(Wahl(Texte.HeutigeStadtAuf, Texte.StadtSetzenTipp,
            Enumerable.Range(1, anzahlNeu).Select(p => (Texte.PlatzNummer(p), (object?)p)), _vergroessernPlatz, t =>
            {
                _vergroessernPlatz = (int)t!;
                NachWahlNeu();
            }));
        if (_stadtBestaetigt is { } ok)
        {
            _aussenAuswahl.Children.Add(Schrift.Text(ok, "Klein", FontWeight.Bold, "Pinsel.green"));
        }
        else if (_stadtPlan is { } plan && !lesen)
        {
            _aussenAuswahl.Children.Add(Schrift.Text(plan.Grund ?? Texte.StadtVorschau(plan.Platz, plan.Breite, plan.Tiefe), "Klein", FontWeight.Bold,
                plan.Geht ? "Pinsel.accent" : "Pinsel.red"));
        }
        var uebernehmen = Knopf(Texte.StadtUebernehmenKnopf, Texte.StadtSetzenTipp, () => StadtUebernehmenMitWahl(), betont: true).Vorlese(Texte.StadtUebernehmenKnopf);
        // nur „nur lesen“ sperrt; jeder andere Grund steht rot da, sobald man drueckt (nie still, nie nur grau)
        uebernehmen.IsEnabled = !lesen;
        _aussenAuswahl.Children.Add(uebernehmen);
    }

    /// <summary>Karte rechts bei „Plätze“.</summary>
    private bool PlaetzeTeil()
    {
        if (!Karte.PlaetzeAn || _projekt is null)
        {
            return false;
        }
        var lesen = _projekt.NurLesen;
        var (b, h) = Aussen.Groesse(_projekt.AussenStand);
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.Plaetze, "Mittel", FontWeight.Bold));
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.PlatzErklaerung, "Klein"));
        if (_plaetzeFehler is { } f)
        {
            _aussenAuswahl.Children.Add(Schrift.Text(f, "Klein", FontWeight.Bold, "Pinsel.red"));
        }

        if (PlaetzeAltFuerTests)
        {
            PlaetzeVergroessernAlt(lesen, b, h);
        }
        else
        {
            StadtSetzenTeil(lesen, b, h);
        }

        // Stadtteil einsetzen
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.StadtteilEinsetzen, "Normal", FontWeight.Bold));
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.StadtteilEinsetzenTipp, "Klein"));
        var projekte = Projekt.Liste(_ordner).Where(d => !string.Equals(Path.GetFullPath(d), Path.GetFullPath(_projekt.Datei), StringComparison.Ordinal))
            .Select(d => (Projekt.AnzeigeName(d), (object?)d)).Prepend((Texte.EinsetzenKeinProjekt, null)).ToList();
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.EinsetzenProjekt, "Klein"));
        _aussenAuswahl.Children.Add(Wahl(Texte.EinsetzenProjekt, Texte.StadtteilEinsetzenTipp, projekte, _einsetzenDatei, t => EinsetzenQuelleWaehlen(t as string)));
        var anzahl = Math.Max(1, Plaetze.Anzahl(b, h));
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.PlatzWaehlen, "Klein"));
        _aussenAuswahl.Children.Add(Wahl(Texte.PlatzWaehlen, Texte.PlatzErklaerung,
            Enumerable.Range(1, anzahl).Select(p => (Texte.PlatzNummer(p), (object?)p)).Prepend((Texte.PlatzKeiner, null)), _einsetzenPlatz, t =>
            {
                if (t is int p)
                {
                    PlatzWaehlen(p);
                }
            }));
        if (_einsetzenVorschau is { } v)
        {
            _aussenAuswahl.Children.Add(Schrift.Text(v.Grund ?? Texte.EinsetzenVorschauFrei, "Klein", FontWeight.Bold, v.Geht ? "Pinsel.green" : "Pinsel.red"));
            foreach (var hinweis in v.Hinweise)
            {
                _aussenAuswahl.Children.Add(Schrift.Text(hinweis, "Klein", farbe: "Pinsel.orange"));
            }
        }
        var einsetzen = Knopf(Texte.Einsetzen, Texte.StadtteilEinsetzenTipp, () => StadtteilEinsetzenMitWahl(), betont: true).Vorlese(Texte.Einsetzen);
        einsetzen.IsEnabled = !lesen && _einsetzenVorschau is { Geht: true };
        _aussenAuswahl.Children.Add(einsetzen);
        _aussenAuswahl.Children.Add(Knopf(Texte.StadtteilFertig, Texte.PlaetzeFertigTipp, () => Karte.PlaetzeAn = false).Vorlese(Texte.PlaetzeFertig));
        return true;
    }
}
