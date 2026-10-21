using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Rechner;
using Lernplattform.Design;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Views;

/// <summary>
/// Phase 3 W4a, D5 (Weisung P3-17): Karten "Datenmengen umrechnen" und "Übertragungsdauer" der
/// Rechner-Seite. Gerechnet und geschrieben wird im Core (<see cref="DatenBerichte"/>,
/// Vergleichswerte G28), hier wird nur angezeigt. Fehleingabe als roter Hinweis, Ergebniskasten
/// leer, kein Fenster (wie R6). Der Rechenweg nutzt die Zahlen der letzten gueltigen Berechnung
/// (vorher die Vorgabewerte, F1a). Wechselt der Aufbau, wird das Ergebnis neu geschrieben (am PC
/// zwei Spalten nur, wenn sie passen; schmal untereinander mit kurzen Beschriftungen).
/// W4b (Weisung P3-21): Uebernahme aus "Bild und Video" mit Hinweiszeile ueber dem Ergebnis
/// (<see cref="UebertragungKarte"/>); bei einem Eingabefehler steht nur der rote Hinweis.
/// </summary>
internal sealed class DatenKarten
{
    private readonly Func<bool> _schmal;

    // Datenmengen
    private readonly TextBox _wert;
    private readonly Pillen _einheit;
    private readonly Ergebniskasten _datenKasten = new(NochKeineBerechnung);
    private readonly Fehlerhinweis _datenFehler = new();
    private DatenmengeEingabe? _daten;

    // Uebertragung
    private readonly TextBox _groesse;
    private readonly Pillen _groesseEinheit;
    private readonly TextBox _rate;
    private readonly Pillen _rateEinheit;
    private readonly TextBox _prozent;
    private readonly Ergebniskasten _dauerKasten = new(NochKeineBerechnung);
    private readonly Fehlerhinweis _dauerFehler = new();
    private readonly TextBlock _uebernahmeHinweis = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.text_dim");
    private UebertragungEingabe? _dauer;

    public DatenKarten(Func<bool> schmal)
    {
        _schmal = schmal;

        _wert = RechnerTeile.Feld(DataDefault, DataField, zahl: true, breite: 160);
        _einheit = new Pillen(Datenmenge.Einheiten.Select(e => (e, e)), DataDefaultUnit, _ => { });
        DatenWeg = new Rechenwegteil(() => DatenBerichte.DatenmengeRechenweg(_daten ?? Vorgabe()));
        var daten = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                RechnerSeite.Felder(RechnerTeile.Zeile(DataField, _wert)),
                RechnerTeile.Beschriftung(DataUnit), _einheit,
                RechnerSeite.Knopf(Berechnen, DatenRechnen), _datenFehler, _datenKasten,
                DatenWeg.Knopf, DatenWeg.Kasten,
            },
        };
        RechnerSeite.EnterRechnet(DatenRechnen, _wert);
        DatenKarte = RechnerTeile.Karte(DataTitle, "Pinsel.accent", RechnerTeile.Unterschrift(DataSubtitle), daten);

        _groesse = RechnerTeile.Feld(TransferDefaultSize, TransferSize, zahl: true, breite: 160);
        _groesseEinheit = new Pillen(Uebertragung.Einheiten.Select(e => (e, e)), DataDefaultUnit, _ => { });
        _rate = RechnerTeile.Feld(TransferDefaultRate, TransferRate, zahl: true, breite: 160);
        _rateEinheit = new Pillen(Uebertragung.Raten.Select(e => (e, e)), Uebertragung.VorgabeRate, _ => { });
        _prozent = RechnerTeile.Feld(TransferDefaultPercent, TransferPercent, zahl: true, breite: 110);
        DauerWeg = new Rechenwegteil(() => DatenBerichte.UebertragungRechenweg(_dauer ?? VorgabeDauer()));
        var dauer = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                RechnerSeite.Felder(RechnerTeile.Zeile(TransferSize, _groesse)),
                RechnerTeile.Beschriftung(DataUnit), _groesseEinheit,
                RechnerSeite.Felder(RechnerTeile.Zeile(TransferRate, _rate)),
                RechnerTeile.Beschriftung(DataUnit), _rateEinheit,
                RechnerSeite.Felder(RechnerTeile.Zeile(TransferPercent, _prozent)),
                RechnerSeite.Knopf(Berechnen, DauerRechnen), _dauerFehler, _uebernahmeHinweis, _dauerKasten,
                DauerWeg.Knopf, DauerWeg.Kasten,
            },
        };
        _uebernahmeHinweis.TextWrapping = TextWrapping.Wrap;
        _uebernahmeHinweis.IsVisible = false;
        RechnerSeite.EnterRechnet(DauerRechnen, _groesse, _rate, _prozent);
        DauerKarte = RechnerTeile.Karte(TransferTitle, "Pinsel.cyan", RechnerTeile.Unterschrift(TransferSubtitle), dauer);
    }

    public Border DatenKarte { get; }

    public Border DauerKarte { get; }

    public Rechenwegteil DatenWeg { get; }

    public Rechenwegteil DauerWeg { get; }

    internal Ergebniskasten DatenKasten => _datenKasten;

    internal Ergebniskasten DauerKasten => _dauerKasten;

    internal string DatenFehler => _datenFehler.Text ?? "";

    internal string DauerFehler => _dauerFehler.Text ?? "";

    /// <summary>Hinweiszeile der Uebernahme ("" = keine).</summary>
    internal string UebernahmeHinweis => _uebernahmeHinweis.IsVisible ? _uebernahmeHinweis.Text ?? "" : "";

    /// <summary>Dateigroesse und Einheit der Uebertragung (fuer den Start ohne Bedienung).</summary>
    internal (string Groesse, string Einheit, string Rate, string RateEinheit, string Prozent) DauerFelder =>
        (_groesse.Text ?? "", _groesseEinheit.Wert, _rate.Text ?? "", _rateEinheit.Wert, _prozent.Text ?? "");

    private static DatenmengeEingabe Vorgabe() => DatenBerichte.DatenmengeLesen(DataDefault, DataDefaultUnit);

    private static UebertragungEingabe VorgabeDauer() =>
        DatenBerichte.UebertragungLesen(TransferDefaultSize, DataDefaultUnit, TransferDefaultRate, Uebertragung.VorgabeRate, TransferDefaultPercent);

    internal void DatenRechnen()
    {
        try
        {
            _daten = DatenBerichte.DatenmengeLesen(_wert.Text, _einheit.Wert);
            _datenKasten.Setzen(DatenBerichte.DatenmengeBericht(_daten, _schmal()));
            _datenFehler.Zeigen(null);
        }
        catch (RechnerFehler f)
        {
            _datenKasten.Setzen("");
            _datenFehler.Zeigen(f.Message);
        }
        DatenWeg.Fassung(_schmal());
    }

    internal void DauerRechnen() => DauerRechnen(null);

    private void DauerRechnen(string? hinweis)
    {
        var a = UebertragungKarte.Rechnen(_groesse.Text, _groesseEinheit.Wert, _rate.Text, _rateEinheit.Wert, _prozent.Text, _schmal(), hinweis);
        if (a.Eingabe is not null)
        {
            _dauer = a.Eingabe;
        }
        _dauerKasten.Setzen(a.Text);
        _dauerFehler.Zeigen(a.Fehler.Length > 0 ? a.Fehler : null);
        _uebernahmeHinweis.Text = a.Hinweis;
        _uebernahmeHinweis.IsVisible = a.Hinweis.Length > 0;
        DauerWeg.Fassung(_schmal());
    }

    /// <summary>
    /// Uebernahme aus "Bild und Video" (V2): Dateigroesse und Einheit setzen, Datenrate und
    /// Leistung bleiben, sofort rechnen, Hinweiszeile ueber dem Ergebnis.
    /// </summary>
    internal void Uebernehmen(string groesse, string einheit, string hinweis)
    {
        _groesse.Text = groesse;
        _groesseEinheit.Waehle(einheit);
        DauerRechnen(hinweis);
    }

    /// <summary>Felder setzen (fuer den Start ohne Bedienung).</summary>
    internal void DatenEingabe(string wert, string einheit)
    {
        _wert.Text = wert;
        _einheit.Waehle(einheit);
    }

    /// <summary>Felder setzen (fuer den Start ohne Bedienung).</summary>
    internal void DauerEingabe(string groesse, string einheit, string rate, string rateEinheit, string prozent)
    {
        _groesse.Text = groesse;
        _groesseEinheit.Waehle(einheit);
        _rate.Text = rate;
        _rateEinheit.Waehle(rateEinheit);
        _prozent.Text = prozent;
    }

    /// <summary>Aufbau gewechselt: sichtbare Ergebnisse in der passenden Fassung neu schreiben.</summary>
    public void AufbauGeaendert()
    {
        if (_daten is not null && _datenKasten.Roh.Length > 0)
        {
            _datenKasten.Setzen(DatenBerichte.DatenmengeBericht(_daten, _schmal()));
        }
        if (_dauer is not null && _dauerKasten.Roh.Length > 0)
        {
            _dauerKasten.Setzen(DatenBerichte.UebertragungBericht(_dauer, _schmal()));
        }
    }
}
