using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Suche;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Views;
using static Lernplattform.Spiel.Tafel.Bau;

namespace Lernplattform.Spiel.Tafel;

/// <summary>Wege aus dem Spiel in den Lernteil (Karteikarten zu einem Thema, Suchtreffer).</summary>
public sealed record SpielWege(Action<string?, string?> Karten, Action<SuchTreffer> Treffer);

/// <summary>
/// Phase 7, W6 (Weisung P7-11): die Spielansichten von mobile/src/spiel.py (GameScreen) als Tafel
/// ueber der 3D-Stadt. Wie render() in Python: ohne gewaehlten Platz die Platzwahl, ohne Figur
/// "Figur anlegen", sonst ist die Tafel zu und die Stadt zu sehen. Darueber legen sich Seiten wie
/// app.push (Uebersicht, Ticket, Firma, Reise), "Zurueck" nimmt die oberste wieder weg. Jede Seite
/// baut sich bei jeder Aenderung neu. Geschrieben wird nur ueber den Spielablauf des aktiven
/// Durchgangs (SpielDatenbank, Sperre der Vorgaenge); laeuft ein anderer Vorgang, zeigt die Tafel
/// AbgleichTexte.Belegt und schreibt nichts. Beschriftungen PC = Handy, Texte wie Python.
/// </summary>
public sealed partial class SpielTafel : Grid
{
    private const double Breite = 720;

    private readonly SpielSitzung _s;
    private readonly SpielWege? _wege;
    private readonly Action _verlassen;
    private readonly StackPanel _inhalt = new() { Spacing = 14 };
    private readonly ScrollViewer _rolle;
    private readonly TextBlock _krumen;
    private readonly Button _zurueck;
    private readonly Control _kopf;
    private TextBlock _geldWert;
    private readonly StackPanel _geld;
    private readonly StackPanel _kopfFest;
    private readonly StackPanel _spalte;
    private readonly List<Seite> _stapel = [];
    private readonly PyObjekt _entwurf = [];
    private bool _waehlen;

    /// <summary>Eine Seite wie app.push(crumbs, ...): baut ihren Inhalt bei jedem Neuzeichnen.</summary>
    /// <summary>Sammelliste 381: <paramref name="MitGeld"/> = Seite mit Einkauf (Laden, Online-Shop), das Geld steht fest im Kopf.</summary>
    private sealed record Seite(string Krumen, Func<IEnumerable<Control?>> Bauen, bool MitGeld = false);

    /// <summary>Sammelliste 381: Ueberschrift des Gelds in der Kopfzeile.</summary>
    internal const string GeldTitel = "KONTOSTAND";

    public SpielTafel(SpielSitzung sitzung, Action verlassen, SpielWege? wege)
    {
        _s = sitzung;
        _verlassen = verlassen;
        _wege = wege;
        _krumen = T("", 11, "muted", true);
        _zurueck = Knopf(KartenTexte.Zurueck, () => Zurueck(), "ghost", 38);
        // Welle A 0.81.0 (Weisung P12-273, Nico 5a): die Kopfzeile mit „Zurueck“ steht fest ueber dem Bildlauf (rollt nicht
        // mehr mit dem Inhalt weg); Abstaende wie bisher (Kopf 12 oben, 4 + 14 zum Inhalt)
        // Sammelliste 381 (Weisung P12-298, Nico): in jedem Laden steht das Geld fest oben neben „Zurueck“ (nie im Bildlauf)
        _geldWert = T("", 14, "text", true);
        _geld = new StackPanel { Spacing = 0, IsVisible = false, Children = { T(GeldTitel, 11, "muted", true), _geldWert } };
        Avalonia.Automation.AutomationProperties.SetName(_geld, GeldTitel);
        _kopf = new KopfZeile(_krumen, Zeile(_geld, _zurueck, 12), 10);
        _kopf.Margin = new Thickness(0, 0, 0, 4);
        _kopfFest = new StackPanel { MaxWidth = Breite, Margin = new Thickness(16, 12, 16, 0), Children = { _kopf } };
        _spalte = new StackPanel { Spacing = 14, MaxWidth = Breite, Margin = new Thickness(16, 14, 16, 24) };
        _spalte.Children.Add(_inhalt);
        _rolle = new ScrollViewer
        {
            Content = _spalte,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        var raster = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        raster.Children.Add(_kopfFest);
        Grid.SetRow(_rolle, 1);
        raster.Children.Add(_rolle);
        var hinten = new Border { Child = raster }.Farbe(Border.BackgroundProperty, "Pinsel.bg");
        Children.Add(hinten);
        Schrift.BeiStufe(this, Zeichnen);
    }

    private PyObjekt I => _s.Inhalt;

    private Spielstand Stand => _s.Stand;

    /// <summary>Ist die Tafel zu sehen (Platzwahl, Figur oder eine Seite)?</summary>
    public bool Offen => IsVisible;

    /// <summary>Zahl der Seiten ueber der Grundansicht (Start-Test).</summary>
    public int Tiefe => _stapel.Count;

    /// <summary>Wird gerufen, wenn sich der Spielstand geaendert hat (Stadt auffrischen).</summary>
    public event Action? Geaendert;

    // -- Aufbau --------------------------------------------------------------

    /// <summary>render(): Grundansicht bestimmen und die oberste Seite neu bauen.</summary>
    public void Zeichnen()
    {
        string krumen;
        IEnumerable<Control?> teile;
        // W7b: die Seite am Schalter (Bank, Amt) setzt den Ort beim Bauen wieder
        _schalter = null;
        if (_stapel.Count > 0)
        {
            krumen = _stapel[^1].Krumen;
            teile = _stapel[^1].Bauen();
        }
        else if (_waehlen || !_s.PlatzGewaehlt)
        {
            krumen = Krumen("PRAXIS", "SPIEL");
            teile = Platzwahl();
        }
        else if (Stand.Profil is null || Py.Hat(_entwurf, "edit"))
        {
            krumen = Krumen("PRAXIS", "SPIEL");
            teile = FigurBearbeiten(Stand);
        }
        else
        {
            IsVisible = false;
            return;
        }
        _krumen.Text = krumen;
        var mitGeld = _stapel.Count > 0 && _stapel[^1].MitGeld && !GeldImBildlauf;
        _geld.IsVisible = mitGeld;
        if (mitGeld)
        {
            _geldWert = T(Euro(Stand.Geld), 14, Stand.Geld >= 0 ? "text" : "red", true);
            _geld.Children[1] = _geldWert;
        }
        _inhalt.Children.Clear();
        if (_stapel.Count > 0 && _stapel[^1].MitGeld && GeldImBildlauf)
        {
            _inhalt.Children.Add(T(PyFormat.Prozent("Kontostand: %s", Euro(Stand.Geld)), 14, "text", true));
        }
        foreach (var teil in teile)
        {
            if (teil is not null)
            {
                _inhalt.Children.Add(teil);
            }
        }
        IsVisible = true;
    }

    /// <summary>crumbs wie am Handy oben ("SPIEL · FIRMA").</summary>
    private static string Krumen(params string[] teile) => string.Join(" · ", teile);

    /// <summary>app.push: neue Seite oben auf den Stapel.</summary>
    private void Oeffnen(string krumen, Func<IEnumerable<Control?>> bauen, bool mitGeld = false)
    {
        _stapel.Add(new Seite(krumen, bauen, mitGeld));
        Zeichnen();
        _rolle.Offset = default;
    }

    /// <summary>Oberste Seite neu bauen (gleiche Seite, neuer Stand).</summary>
    private void Neu()
    {
        var offset = _rolle.Offset;
        Zeichnen();
        _rolle.Offset = offset;
    }

    /// <summary>Alle Seiten schliessen (zur Stadt bzw. zur Grundansicht).</summary>
    public void AllesSchliessen()
    {
        _stapel.Clear();
        if (_nachAbgleich)
        {
            Gezeigt();   // Weisung P8-5 (L1): der Abgleich kam, waehrend das Ticket offen war
            return;
        }
        Zeichnen();
    }

    /// <summary>Zurueck-Taste: oberste Seite weg; true, wenn die Tafel etwas geschlossen hat.</summary>
    public bool Zurueck()
    {
        if (_stapel.Count > 0)
        {
            _stapel.RemoveAt(_stapel.Count - 1);
            if (_nachAbgleich && !TicketOffen)
            {
                Gezeigt();   // Weisung P8-5 (L1): der Abgleich kam, waehrend das Ticket offen war
            }
            else
            {
                Auffrischen();
            }
            return true;
        }
        if (Py.Hat(_entwurf, "edit") && Stand.Profil is not null)
        {
            FigurAbbrechen();
            return true;
        }
        if (_waehlen && _s.PlatzGewaehlt)
        {
            _waehlen = false;
            Zeichnen();
            return true;
        }
        if (IsVisible)
        {
            _verlassen();
            return true;
        }
        return false;
    }

    // -- Schreiben -----------------------------------------------------------

    /// <summary>
    /// Eine Aktion des Spiels ausfuehren wie in Python (try ... except ValueError: toast). Laeuft ein
    /// anderer Vorgang, AbgleichTexte.Belegt (nichts geschrieben). null = nicht ausgefuehrt.
    /// </summary>
    private PyObjekt? Tun(Func<Spielablauf, PyObjekt> aktion, string farbe = "yellow")
    {
        try
        {
            var daten = aktion(_s.Verwaltung.ZumSchreiben());
            return daten;
        }
        catch (SpielBelegtAusnahme)
        {
            Hinweisen(AbgleichTexte.Belegt, "red");
        }
        catch (SpielFehler e)
        {
            Hinweisen(e.Message, farbe);
        }
        return null;
    }

    /// <summary>Wie <see cref="Tun"/> fuer die Platzverwaltung (select_run, new_run, delete_run ...).</summary>
    private bool Verwalten(Action aktion, string farbe = "red")
    {
        try
        {
            aktion();
            return true;
        }
        catch (SpielBelegtAusnahme)
        {
            Hinweisen(AbgleichTexte.Belegt, "red");
        }
        catch (SpielFehler e)
        {
            Hinweisen(e.Message, farbe);
        }
        return false;
    }

    /// <summary>notify_progress: Abgleich vormerken, neue Abzeichen kurz danach zeigen.</summary>
    private void Fortschritt()
    {
        Programm.AbgleichDienst.NachDemLernen();
        Auffrischen();
        if (_s.Verwaltung.Ablauf is { Freigeschaltet.Count: > 0 })
        {
            DispatcherTimer.RunOnce(Freischaltungen, TimeSpan.FromMilliseconds(400));
        }
    }

    /// <summary>Stadt und Tafel nach einer Aenderung neu zeigen.</summary>
    private void Auffrischen()
    {
        _s.Abgleichen();
        Geaendert?.Invoke();
        Neu();
    }

    private bool _nachAbgleich;

    /// <summary>
    /// Weisung P8-5 (L1, refresh_after_sync): nach einem Abgleich mit neuen oder entfernten Eintraegen den
    /// Spielstand neu lesen und zeigen wie <see cref="Gezeigt"/> (ein woanders geloeschter Platz wird dabei neu
    /// gewaehlt, L2). Ist gerade ein Ticket offen, wie in Python nicht; dann holt das Schliessen des Tickets es nach.
    /// </summary>
    public void NachAbgleich()
    {
        if (TicketOffen)
        {
            _nachAbgleich = true;
            return;
        }
        Gezeigt();
    }

    /// <summary>on_show: Spielstand neu lesen, Wissen-Abzeichen pruefen, neue Abzeichen zeigen.</summary>
    public void Gezeigt()
    {
        _nachAbgleich = false;
        try
        {
            _s.Verwaltung.Neuladen();
            _s.Verwaltung.Ablauf?.WissenPruefen();
        }
        catch (SpielBelegtAusnahme)
        {
            // Lesen ohne Sperre geht weiter; das naechste Zeigen holt es nach
        }
        catch (SpielFehler e)
        {
            Hinweisen(e.Message, "red");
        }
        _s.Abgleichen();
        Geaendert?.Invoke();
        Zeichnen();
        Freischaltungen();
        UpdateHinweis();
    }

    // -- Abzeichen (show_unlocks) -------------------------------------------

    private readonly List<PyObjekt> _momente = [];
    private bool _momentOffen;

    /// <summary>show_unlocks: grosse Stufen als Meilenstein, kleinere als kurzer Hinweis unten.</summary>
    public void Freischaltungen()
    {
        if (_s.Verwaltung.Ablauf is not { } ablauf || ablauf.Freigeschaltet.Count == 0)
        {
            return;
        }
        var alle = ablauf.FreischaltungenAbholen();
        if (OhneFenster)
        {
            return;
        }
        var momente = alle.Where(i => Py.Wahr(i["moment"])).ToList();
        var andere = alle.Where(i => !Py.Wahr(i["moment"])).ToList();
        if (andere.Count > 0)
        {
            Hinweisen(string.Join("  ·  ", andere.Take(3).Select(i => Py.Str(i["hinweis"])))
                + (andere.Count > 3 ? PyFormat.Prozent("  ·  +%d", andere.Count - 3) : ""), "yellow");
        }
        if (momente.Count > 0)
        {
            _momente.AddRange(momente);
            if (!_momentOffen)
            {
                _ = MomentZeigenAsync();
            }
        }
    }

    /// <summary>_show_moment: Meilenstein-Momente nacheinander ("Naechster (2/3)", "Weiter", "Zu den Erfolgen").</summary>
    private async Task MomentZeigenAsync()
    {
        _momentOffen = true;
        try
        {
            for (var stelle = 0; stelle < _momente.Count; stelle++)
            {
                var info = _momente[stelle];
                var mehr = stelle + 1 < _momente.Count;
                var weiter = mehr ? PyFormat.Prozent("Nächster (%d/%d)", stelle + 2, _momente.Count) : "Weiter";
                var wahl = await Dialog.Zeigen("", MomentKarte(info), weiter, "Zu den Erfolgen");
                if (wahl == 1)
                {
                    ReiseOeffnen("erfolge");
                    break;
                }
                if (wahl != 0)
                {
                    break;
                }
            }
        }
        finally
        {
            _momente.Clear();
            _momentOffen = false;
        }
    }

    /// <summary>moment_card: Orden, Stufe, Name und Satz (die Szene der Weltkarte entfaellt in 3D).</summary>
    private static Control MomentKarte(PyObjekt info)
    {
        var stufe = info["tier"] as string;
        var mitte = (Control c) =>
        {
            c.HorizontalAlignment = HorizontalAlignment.Center;
            if (c is TextBlock t)
            {
                t.TextAlignment = TextAlignment.Center;
            }
            return c;
        };
        var zeilen = new List<Control?>
        {
            mitte(Formen.Abzeichen(info["bild"] as string, info["farbe"] as string, stufe, 84)),
            mitte(T("MEILENSTEIN · " + Beschriftungen.Gross(Py.Str(info["tier_name"])), 11,
                SpielBilder.StufenSchrift(FarbenAnwenden.Aktuell, stufe), true)),
            mitte(T(Py.Str(info["name"]), 22, "text", true)),
            mitte(T(Py.Str(info["text"]), 14, "text_dim")),
        };
        if (Py.Wahr(info["erstes_mal"]))
        {
            zeilen.Add(mitte(T("Zum ersten Mal erreicht!", 14, "accent", true)));
        }
        if (Py.Wahr(info["bestwert"]))
        {
            zeilen.Add(mitte(T(Py.Str(info["bestwert"]), 14, "green", true)));
        }
        var stapel = Stapel(6, zeilen);
        stapel.MaxWidth = 330;
        return stapel;
    }

    // -- Kopf der Spielseiten -----------------------------------------------

    /// <summary>slot_bar: kleine Platzanzeige mit "Platz wechseln".</summary>
    private Control? PlatzLeiste(Action? wechseln = null, bool untereinander = false)
    {
        var text = _s.Verwaltung.AktivAnzeige();
        if (text.Length == 0)
        {
            return null;
        }
        // das Symbol SAVE_OUTLINED fehlt in der Teilschrift fisi_symbole.otf (nur Text)
        var knopf = Knopf("Platz wechseln", wechseln ?? PlatzWechseln, "ghost", 30);
        if (untereinander)
        {
            // Welle A 0.81.0 (Sammelliste 350): im schmalen Spiel-Handy bei grosser Schrift untereinander
            knopf.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            return Stapel(6, [T(text, 12, "muted"), knopf]);
        }
        return Zeile(T(text, 12, "muted"), knopf, 6);
    }

    /// <summary>open_picker</summary>
    private void PlatzWechseln()
    {
        _stapel.Clear();
        _waehlen = true;
        Zeichnen();
    }
}
