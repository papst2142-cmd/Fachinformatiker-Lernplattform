using Avalonia.Controls;
using Avalonia.Layout;
using Lernplattform.Core.Rechner;
using static Lernplattform.Core.Texte.RechnerTexte;

namespace Lernplattform.Views;

/// <summary>
/// Phase 3 W4b, V2/V3 (Weisung P3-21): Karte "Bild und Video" der Rechner-Seite. Die bisherigen
/// Bildschirm-Felder bleiben, dazu "Dauer (Minuten, optional)" (leer) und "Kompression (y-fach)"
/// ("1"). Ohne Dauer bleibt die Ausgabe genau wie bisher (G26). Gerechnet und geschrieben wird im
/// Core (<see cref="VideoBerichte"/>, <see cref="VideoKartenZustand"/>), hier wird nur angezeigt.
/// Der Knopf "In Übertragungsdauer übernehmen" ist nur mit Video-Ergebnis aktiv und gibt Wert,
/// Einheit und Hinweiszeile an die Rechner-Seite weiter.
/// </summary>
internal sealed class VideoKarte
{
    private readonly Func<bool> _schmal;
    private readonly VideoKartenZustand _zustand = new();
    private readonly TextBox _breite;
    private readonly TextBox _hoehe;
    private readonly TextBox _fps;
    private readonly TextBox _minuten;
    private readonly TextBox _kompression;
    private readonly Pillen _tiefe;
    private readonly Ergebniskasten _kasten = new(NochKeineBerechnung);
    private readonly Fehlerhinweis _fehler = new();
    private readonly Button _uebernehmen;
    private readonly Action<string, string, string> _weitergeben;

    public VideoKarte(Func<bool> schmal, Action<string, string, string> uebernehmen)
    {
        _schmal = schmal;
        _breite = RechnerTeile.Feld("1920", SCREEN_FIELD_WIDTH, zahl: true, breite: 110);
        _hoehe = RechnerTeile.Feld("1080", SCREEN_FIELD_HEIGHT, zahl: true, breite: 110);
        _fps = RechnerTeile.Feld("30", SCREEN_FIELD_FPS, zahl: true, breite: 110);
        _minuten = RechnerTeile.Feld("", VideoFieldMinutes, zahl: true, breite: 110);
        _kompression = RechnerTeile.Feld(VideoDefaultCompression, VideoFieldCompression, zahl: true, breite: 110);
        _tiefe = new Pillen(COLOR_DEPTHS, COLOR_DEPTHS[2].Item1, _ => Geaendert());
        _weitergeben = uebernehmen;
        _uebernehmen = RechnerSeite.Knopf(VideoTakeOver, Uebernehmen);
        _uebernehmen.IsEnabled = _zustand.KnopfAktiv;
        Weg = new Rechenwegteil(() => VideoBerichte.Rechenweg(_zustand.Weg, _schmal()));
        var inhalt = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                RechnerSeite.Felder(RechnerTeile.Zeile(SCREEN_FIELD_WIDTH, _breite), RechnerTeile.Zeile(SCREEN_FIELD_HEIGHT, _hoehe),
                    RechnerTeile.Zeile(SCREEN_FIELD_FPS, _fps)),
                RechnerTeile.Beschriftung(SCREEN_FIELD_DEPTH), _tiefe,
                RechnerSeite.Felder(RechnerTeile.Zeile(VideoFieldMinutes, _minuten), RechnerTeile.Zeile(VideoFieldCompression, _kompression)),
                RechnerSeite.Knopf(Berechnen, Rechnen), _fehler, _kasten,
                _uebernehmen,
                Weg.Knopf, Weg.Kasten,
            },
        };
        foreach (var feld in new[] { _breite, _hoehe, _fps, _minuten, _kompression })
        {
            feld.TextChanged += (_, _) => Geaendert();
        }
        RechnerSeite.EnterRechnet(Rechnen, _breite, _hoehe, _fps, _minuten, _kompression);
        Karte = RechnerTeile.Karte(VideoTitle, "Pinsel.green", RechnerTeile.Unterschrift(VideoSubtitle), inhalt);
    }

    public Border Karte { get; }

    public Rechenwegteil Weg { get; }

    internal Ergebniskasten Kasten => _kasten;

    internal string Fehler => _fehler.Text ?? "";

    /// <summary>Uebernahme-Knopf aktiv (fuer den Start ohne Bedienung).</summary>
    internal bool KnopfAktiv => _uebernehmen.IsEnabled;

    internal void Rechnen()
    {
        try
        {
            _kasten.Setzen(_zustand.Rechnen(_breite.Text, _hoehe.Text, _tiefe.Wert, _fps.Text ?? "", _minuten.Text, _kompression.Text));
            _fehler.Zeigen(null);
        }
        catch (RechnerFehler f)
        {
            // B-D-04 (D-4): Kasten leer, dann die Meldung (roter Hinweis, Weisung P3-4)
            _kasten.Setzen("");
            _fehler.Zeigen(f.Message);
        }
        _uebernehmen.IsEnabled = _zustand.KnopfAktiv;
        Weg.Fassung(_schmal());
    }

    /// <summary>Uebernahme-Knopf druecken (fuer den Start ohne Bedienung).</summary>
    internal void Uebernehmen()
    {
        Geaendert();                     // falls ein TextChanged noch aussteht: Felder jetzt vergleichen
        if (_uebernehmen.IsEnabled && _zustand.Video is { } v)
        {
            var (text, einheit, hinweis) = VideoBerichte.Uebernahme(v);
            _weitergeben(text, einheit, hinweis);
        }
    }

    /// <summary>Felder setzen (fuer den Start ohne Bedienung); Farbtiefe als Wert wie "24".</summary>
    internal void Eingabe(string breite, string hoehe, string fps, string tiefe, string minuten, string kompression)
    {
        _breite.Text = breite;
        _hoehe.Text = hoehe;
        _fps.Text = fps;
        _tiefe.Waehle(tiefe);
        _minuten.Text = minuten;
        _kompression.Text = kompression;
        Geaendert();
    }

    /// <summary>Nur das Feld Dauer setzen, ohne selbst zu vergleichen (Start ohne Bedienung: TextChanged-Weg).</summary>
    internal void NurDauerSetzen(string minuten) => _minuten.Text = minuten;

    private void Geaendert()
    {
        _zustand.Geaendert(_breite.Text, _hoehe.Text, _tiefe.Wert, _fps.Text ?? "", _minuten.Text, _kompression.Text);
        _uebernehmen.IsEnabled = _zustand.KnopfAktiv;
    }
}
