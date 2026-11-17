using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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

    internal Button PlaetzeKnopf => _plaetzeChip;

    /// <summary>Start-Test: Vorschau des Einsetzens (null = kein Stadtteil oder kein Platz gewaehlt) und die rote Zeile.</summary>
    internal Projekt.EinsetzenErgebnis? EinsetzenVorschau => _einsetzenVorschau;

    internal string? PlaetzeFehler => _plaetzeFehler;

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
            _vergroessernPlatz = 1;
            _einsetzenPlatz = null;
            _einsetzenVorschau = null;
        }
        Karte.PlaetzeAn = an;
        if (an)
        {
            Melden(Texte.PlaetzeErklaerung);
        }
    }

    /// <summary>Platz waehlen (Tippen, Auswahl, Pfeile; auch Start-Test). 0 = daneben getippt.</summary>
    internal void PlatzWaehlen(int nummer)
    {
        if (_projekt is null)
        {
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
        var (b, h) = Aussen.Groesse(_projekt.AussenStand);
        var spalten = Plaetze.Spalten(b);
        var jetzt = _einsetzenPlatz ?? 1;
        switch (taste)
        {
            case Key.Escape:
                Karte.PlaetzeAn = false;
                return true;
            case Key.Left or Key.Right or Key.Up or Key.Down:
            {
                var neu = jetzt + (taste == Key.Left ? -1 : taste == Key.Right ? 1 : taste == Key.Up ? -spalten : spalten);
                var gleicheReihe = taste is Key.Up or Key.Down || (neu - 1) / spalten == (jetzt - 1) / spalten;
                PlatzWaehlen(_einsetzenPlatz is null ? 1 : Plaetze.Gibt(neu, b, h) && gleicheReihe ? neu : jetzt);
                return true;
            }
            default:
                return false;
        }
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

        // Karte vergroessern
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
        var warum = _projekt.VergroessernNicht(_vergroessernBreite, _vergroessernTiefe, _vergroessernPlatz);
        if (warum is not null && !lesen)
        {
            _aussenAuswahl.Children.Add(Schrift.Text(warum, "Klein", farbe: "Pinsel.muted"));
        }
        var vergroessern = Knopf(Texte.KarteVergroessernKnopf, Texte.KarteVergroessernTipp, () => KarteVergroessernMitWahl(), betont: true).Vorlese(Texte.KarteVergroessernKnopf);
        vergroessern.IsEnabled = !lesen && warum is null;
        _aussenAuswahl.Children.Add(vergroessern);

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
