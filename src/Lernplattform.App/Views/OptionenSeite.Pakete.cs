using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Pakete;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Welle A 0.87.0 (Weisung P12-294, Plan P12-175 Abschnitt 3, Nico 1a): Karte „Inhaltspakete“ in der Gruppe Programm. Liste der
/// geladenen und angebotenen Pakete mit Name, Groesse, Kurztext, Stand (geladen, neuer Stand, nicht geladen, rot fehlerhaft mit
/// Grund) und den Knoepfen Laden, Aktualisieren, Neu laden, Entfernen. Gesucht wird nur auf Knopfdruck, geladen nur auf Knopfdruck;
/// am Handy ohne WLAN zuerst die Rueckfrage mit der Groesse, dann der Balken mit „Abbrechen“. PC und Handy gleich beschriftet.
/// </summary>
public sealed partial class OptionenSeite
{
    private StackPanel? _paketListe;
    private TextBlock? _paketStatus;
    private Button? _paketSuchen;
    private CancellationTokenSource? _paketAbbruch;
    private readonly Dictionary<string, Dictionary<PaketKnopf, Button>> _paketKnoepfe = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Control> _paketKarten = new(StringComparer.Ordinal);

    /// <summary>Start-Test: Knopf „Nach Paketen suchen“.</summary>
    internal Button? PaketSuchenKnopf => _paketSuchen;

    /// <summary>Start-Test: Statuszeile der Karte.</summary>
    internal TextBlock? PaketStatus => _paketStatus;

    /// <summary>Start-Test: Knoepfe je Paket (Kennung) und Art.</summary>
    internal IReadOnlyDictionary<string, Dictionary<PaketKnopf, Button>> PaketKnoepfe => _paketKnoepfe;

    /// <summary>Start-Test, Fotos: Karte je Paket (Kennung).</summary>
    internal IReadOnlyDictionary<string, Control> PaketKarten => _paketKarten;

    /// <summary>Start-Test: Balken des gerade ladenden Pakets (null = keiner).</summary>
    internal ProgressBar? PaketBalken { get; private set; }

    /// <summary>Start-Test: laeuft gerade ein Laden?</summary>
    internal bool PaketLaedt => _paketAbbruch is not null;

    private Control PaketeKarte()
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new LernText(PaketTexte.Satz, "Klein", FontWeight.Normal, "Pinsel.text_dim"));
        _paketSuchen = Schrift.Knopf(PaketTexte.Suchen);
        _paketSuchen.HorizontalAlignment = HorizontalAlignment.Left;
        _paketSuchen.Click += async (_, _) => await PaketeSuchenAsync();
        panel.Children.Add(_paketSuchen);
        _paketStatus = new LernText("", "Klein", FontWeight.Normal, "Pinsel.muted") { IsVisible = false };
        panel.Children.Add(_paketStatus);
        _paketListe = new StackPanel { Spacing = 6 };
        panel.Children.Add(_paketListe);
        PaketeNeu();
        return panel;
    }

    private void PaketStatusSetzen(string text, string farbe)
    {
        if (_paketStatus is null)
        {
            return;
        }
        _paketStatus.Text = text;
        _paketStatus.FarbeNeu(TextBlock.ForegroundProperty, farbe);   // wechselt: nur ueber Markierung (Sammelliste 341)
        _paketStatus.IsVisible = text.Length > 0;
    }

    /// <summary>Sucht bei der Quelle (nur auf Knopfdruck); ohne Netz „Keine Verbindung“, Geladenes bleibt in der Liste.</summary>
    /// <summary>Start-Test und Layout-Ansichten: Statuszeile wie nach einer Suche setzen.</summary>
    internal void PaketStatusZeigen(string text, bool rot) => PaketStatusSetzen(text, rot ? "Pinsel.red" : "Pinsel.muted");

    internal async Task PaketeSuchenAsync()
    {
        if (_paketSuchen is null || PaketLaedt)
        {
            return;
        }
        _paketSuchen.IsEnabled = false;
        PaketStatusSetzen(PaketTexte.Sucht, "Pinsel.muted");
        var v = PaketDienst.Verwaltung;
        try
        {
            await Task.Run(() => v.SuchenAsync());
        }
        finally
        {
            _paketSuchen.IsEnabled = true;
        }
        if (v.SuchFehler is { } fehler)
        {
            PaketStatusSetzen(fehler, "Pinsel.red");
        }
        else
        {
            PaketStatusSetzen(v.Angebote.Count == 0 ? PaketTexte.KeineAngebote
                : PaketTexte.Format(PaketTexte.Gesucht, v.Gesucht!.Value.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)), "Pinsel.muted");
        }
        PaketeNeu();
    }

    /// <summary>Baut die Zeilen neu (liest nur den Datenordner und das letzte Angebot, geht ohne Netz).</summary>
    internal void PaketeNeu()
    {
        if (_paketListe is null)
        {
            return;
        }
        _paketListe.Children.Clear();
        _paketKnoepfe.Clear();
        _paketKarten.Clear();
        PaketBalken = null;
        var zeilen = PaketDienst.Verwaltung.Zeilen();
        if (zeilen.Count == 0)
        {
            _paketListe.Children.Add(new LernText(PaketTexte.Keine, "Klein", FontWeight.Normal, "Pinsel.muted"));
            return;
        }
        foreach (var zeile in zeilen)
        {
            var karte = PaketKarte(zeile, null);
            _paketKarten[zeile.Kennung] = karte;
            _paketListe.Children.Add(karte);
        }
    }

    private static string ZustandFarbe(PaketZustand z) => z switch
    {
        PaketZustand.Geladen => "Pinsel.green",
        PaketZustand.NeuerStand => "Pinsel.accent",
        PaketZustand.Fehlerhaft => "Pinsel.red",
        _ => "Pinsel.muted",
    };

    /// <summary>Karte eines Pakets; mit <paramref name="laden"/> (geladen, gesamt) statt der Knoepfe der Balken mit „Abbrechen“.</summary>
    private Control PaketKarte(PaketZeile zeile, (long Geladen, long Gesamt)? laden)
    {
        var inhalt = new StackPanel { Spacing = 2 };
        inhalt.Children.Add(new LernText(zeile.Name + " · " + PaketTexte.Mb(zeile.Groesse) + " MB", "Normal", FontWeight.SemiBold, "Pinsel.text"));
        if (zeile.Text.Length > 0)
        {
            inhalt.Children.Add(new LernText(zeile.Text, "Klein", FontWeight.Normal, "Pinsel.text_dim"));
        }
        if (laden is { } l)
        {
            inhalt.Children.Add(new LernText(PaketTexte.Format(PaketTexte.LaedtMb, PaketTexte.Mb(l.Geladen), PaketTexte.Mb(l.Gesamt)), "Klein", FontWeight.Normal, "Pinsel.text_dim"));
            var balken = new ProgressBar { Minimum = 0, Maximum = 1, Height = 10, Value = l.Gesamt > 0 ? (double)l.Geladen / l.Gesamt : 0, Margin = new Thickness(0, 4, 0, 4) }
                .Farbe(ProgressBar.ForegroundProperty, "Pinsel.purple").Farbe(ProgressBar.BackgroundProperty, "Pinsel.ring_bg");
            AutomationProperties.SetName(balken, PaketTexte.Format(PaketTexte.LaedtMb, PaketTexte.Mb(l.Geladen), PaketTexte.Mb(l.Gesamt)));
            PaketBalken = balken;
            inhalt.Children.Add(balken);
            var abbrechen = Schrift.Knopf(PaketTexte.Abbrechen);
            abbrechen.HorizontalAlignment = HorizontalAlignment.Left;
            AutomationProperties.SetName(abbrechen, PaketTexte.Abbrechen + ": " + zeile.Name);
            abbrechen.Click += (_, _) => _paketAbbruch?.Cancel();
            inhalt.Children.Add(abbrechen);
        }
        else
        {
            inhalt.Children.Add(new LernText(zeile.ZustandText, "Klein", zeile.Zustand == PaketZustand.Fehlerhaft ? FontWeight.SemiBold : FontWeight.Normal,
                ZustandFarbe(zeile.Zustand)));
            var reihe = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            var knoepfe = new Dictionary<PaketKnopf, Button>();
            foreach (var art in zeile.Knoepfe)
            {
                var knopf = Schrift.Knopf(PaketZeile.KnopfText(art), art != PaketKnopf.Entfernen);
                knopf.Margin = new Thickness(0, 0, 6, 4);
                AutomationProperties.SetName(knopf, PaketZeile.KnopfText(art) + ": " + zeile.Name);
                knopf.IsEnabled = !PaketLaedt;
                knopf.Click += async (_, _) =>
                {
                    if (art == PaketKnopf.Entfernen)
                    {
                        await PaketEntfernenAsync(zeile);
                    }
                    else
                    {
                        await PaketLadenAsync(zeile);
                    }
                };
                knoepfe[art] = knopf;
                reihe.Children.Add(knopf);
            }
            _paketKnoepfe[zeile.Kennung] = knoepfe;
            inhalt.Children.Add(reihe);
        }
        var rahmen = Schrift.Karte(inhalt, "Pinsel.card_alt");
        rahmen.Padding = new Thickness(12, 10);
        return rahmen;
    }

    /// <summary>
    /// Laedt ein Paket auf Knopfdruck: am Handy ohne WLAN zuerst die Rueckfrage mit der Groesse (vor dem Laden), dann Balken mit
    /// „Abbrechen“; danach steht das Ergebnis in der Statuszeile und rot an der Zeile, wenn es nicht ging.
    /// </summary>
    internal async Task PaketLadenAsync(PaketZeile zeile)
    {
        if (zeile.Angebot is not { } angebot || PaketLaedt)
        {
            return;
        }
        if (PaketMobilFrage(zeile) is { } frage && !await frage)
        {
            return;
        }
        using var abbruch = new CancellationTokenSource();
        _paketAbbruch = abbruch;
        foreach (var k in _paketKnoepfe.Values.SelectMany(k => k.Values))
        {
            k.IsEnabled = false;
        }
        if (_paketSuchen is not null)
        {
            _paketSuchen.IsEnabled = false;
        }
        void Zeigen(long geladen)
        {
            if (_paketListe is null || !_paketKarten.TryGetValue(zeile.Kennung, out var alt))
            {
                return;
            }
            var neu = PaketKarte(zeile, (geladen, Math.Max(angebot.Groesse, geladen)));
            var i = _paketListe.Children.IndexOf(alt);
            if (i >= 0)
            {
                _paketListe.Children[i] = neu;
                _paketKarten[zeile.Kennung] = neu;
            }
        }
        Zeigen(0);
        PaketStatusSetzen("", "Pinsel.muted");
        var fortschritt = new Progress<long>(Zeigen);
        string? grund;
        try
        {
            grund = await Task.Run(() => PaketDienst.Verwaltung.LadenAsync(angebot, fortschritt, abbruch.Token));
        }
        finally
        {
            _paketAbbruch = null;
            if (_paketSuchen is not null)
            {
                _paketSuchen.IsEnabled = true;
            }
        }
        PaketStatusSetzen(grund ?? PaketTexte.Format(PaketTexte.Geladen, zeile.Name), grund is null || grund == PaketTexte.Abgebrochen ? "Pinsel.muted" : "Pinsel.red");
        PaketeNeu();
    }

    /// <summary>Rueckfrage vor dem Laden ueber mobile Daten (nur am Handy ohne WLAN, sonst null); true = laden.</summary>
    private static Task<bool>? PaketMobilFrage(PaketZeile zeile)
    {
        if (!PaketMobilFuerTests && Aktualisierung.Aktualisierung.Handy?.ImWlan != false)
        {
            return null;
        }
        return Frage();
        async Task<bool> Frage() => await Dialog.Zeigen(PaketTexte.MobilTitel,
            PaketTexte.Format(PaketTexte.MobilFrage, zeile.Name, PaketTexte.Mb(zeile.Angebot?.Groesse ?? zeile.Groesse)),
            PaketTexte.MobilJa, PaketTexte.MobilNein) == 0;
    }

    /// <summary>Start-Test und Fotos: verhaelt sich wie am Handy ohne WLAN (Rueckfrage vor dem Laden).</summary>
    internal static bool PaketMobilFuerTests { get; set; }

    /// <summary>Entfernt ein Paket nach Rueckfrage.</summary>
    internal async Task PaketEntfernenAsync(PaketZeile zeile)
    {
        if (PaketLaedt || await Dialog.Zeigen(PaketTexte.EntfernenTitel, PaketTexte.Format(PaketTexte.EntfernenFrage, zeile.Name), TrainerTexte.Ja, TrainerTexte.Nein) != 0)
        {
            return;
        }
        var ok = PaketDienst.Verwaltung.Entfernen(zeile.Kennung);
        PaketStatusSetzen(ok ? PaketTexte.Format(PaketTexte.Entfernt, zeile.Name) : PaketTexte.Format(PaketTexte.Speichern, zeile.Name), ok ? "Pinsel.muted" : "Pinsel.red");
        PaketeNeu();
    }

    /// <summary>Fotos und Layoutpruefung: zeigt eine Zeile im Zustand „laedt“ (ohne zu laden).</summary>
    internal void PaketLaedtVorschau(string kennung, long geladen)
    {
        if (_paketListe is null || !_paketKarten.TryGetValue(kennung, out var alt)
            || PaketDienst.Verwaltung.Zeilen().FirstOrDefault(z => z.Kennung == kennung) is not { } zeile)
        {
            return;
        }
        var neu = PaketKarte(zeile, (geladen, Math.Max(zeile.Groesse, geladen)));
        _paketListe.Children[_paketListe.Children.IndexOf(alt)] = neu;
        _paketKarten[kennung] = neu;
    }
}
