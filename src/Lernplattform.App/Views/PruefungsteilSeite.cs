using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>Wohin die Knoepfe der Pruefungsteile fuehren (MainView).</summary>
public sealed record PruefungsteilWege(
    Action<string, IReadOnlyList<string>> FragenUeben,
    Action<string, string, IReadOnlyList<Lerninhalt>> SzenarienUeben,
    Action<string, string> KlausurStarten,
    Action<string, EchtSatz?> EchtStarten,
    Action<string, string?> Oeffnen);

/// <summary>
/// Welle A 1.15.0 (Weisungen P12-406/P12-407, „genau wie die Entwurfsbilder“ Variante B aus Meldung P12-261):
/// <list type="bullet">
/// <item>Teil 2: Hinweis und je Bereich eine Karte (BEREICH n · Gewicht, Name, Angabe, Üben und Prüfen), dazu PROJEKT mit
/// Testprojekte und Mein Projekt.</item>
/// <item>Teil 1 und die drei schriftlichen Bereiche: Reiter Üben/Prüfen. Üben: Prüfungsfragen, Szenarien, einzelne Aufgabe im
/// echten Format, darunter DEIN STAND. Prüfen: PROBEKLAUSUR und ECHTES FORMAT (Nächster Satz, Satz wählen).</item>
/// </list>
/// Breit (ab <see cref="BreitAb"/>) wie das PC-Bild, schmal wie das Handy-Bild (Name als Ueberschrift, Knoepfe darunter).
/// </summary>
public sealed class PruefungsteilSeite : UserControl
{
    /// <summary>Ab dieser Breite der Seite stehen Text und Knoepfe nebeneinander (PC-Bild), darunter wie am Handy.</summary>
    public const double BreitAb = 600;

    private readonly string _seite;
    private readonly PruefungsteilWege _wege;
    private readonly StackPanel _inhalt = new() { Spacing = 12, Margin = new Thickness(16, 4, 16, 24) };
    private readonly ScrollViewer _rollen;
    private PruefungsteilDaten.Daten? _daten;
    private bool? _breit;
    private string _reiter = Pruefungsteile.Ueben;
    // Unteransicht im Reiter: null = Karten, sonst Satz- bzw. Aufgabenliste oder eine Aufgabe
    private Func<IEnumerable<Control>>? _unter;

    public PruefungsteilSeite(string seite, PruefungsteilWege wege)
    {
        _seite = seite;
        _wege = wege;
        _rollen = new ScrollViewer { Content = _inhalt, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Content = _rollen;
        _inhalt.Children.Add(Schrift.Text(FarbenTexte.Laden, "Normal", FontWeight.Normal, "Pinsel.text_dim"));
        SizeChanged += (_, e) =>
        {
            var breit = e.NewSize.Width >= BreitAb;
            if (breit != _breit)
            {
                _breit = breit;
                Malen();
            }
        };
        Geladen = LadenAsync();
    }

    /// <summary>Daten der Sicht stehen, Seite ist gemalt.</summary>
    public Task Geladen { get; }

    /// <summary>Kennung der Seite (teil1, teil2, b1, b2, wiso).</summary>
    public string Kennung => _seite;

    /// <summary>Gewaehlter Reiter (Üben oder Prüfen).</summary>
    public string Reiter => _reiter;

    private async Task LadenAsync()
    {
        var daten = await PruefungsteilDaten.Fuer(Zustand.Sicht, Zustand.Inhalte);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _daten = daten;
            Malen();
        });
    }

    /// <summary>Beim erneuten Zeigen: Stand neu lesen, zurueck zu den Karten.</summary>
    public void Gezeigt(string? reiter = null)
    {
        if (reiter is not null)
        {
            _reiter = reiter;
        }
        _unter = null;
        Malen();
    }

    private bool Breit => _breit ?? true;

    private void Malen()
    {
        if (_daten is not { } d)
        {
            return;
        }
        _inhalt.Children.Clear();
        if (_seite == Pruefungsteile.Teil2)
        {
            Teil2(d);
        }
        else
        {
            ReiterSeite(d);
        }
        _rollen.Offset = default;
    }

    // -- Teil 2 --------------------------------------------------------------------------------------------

    private void Teil2(PruefungsteilDaten.Daten d)
    {
        var fr = d.Fachrichtung;
        var hinweis = Breit ? Pruefungsteile.Teil2Hinweis
            : Beschriftungen.Einsetzen(Pruefungsteile.Teil2HinweisKurz, FachrichtungTexte.KuerzelFuer(fr.Id));
        _inhalt.Children.Add(new LernText(hinweis, "Klein", FontWeight.Normal, "Pinsel.text_soft"));
        foreach (var rolle in (string[])[Pruefungsteile.B1, Pruefungsteile.B2, Pruefungsteile.Wiso])
        {
            var bereich = fr.Bereich(rolle);
            var teil = d.Teile[rolle];
            var saetze = d.Saetze[rolle].Count;
            var angabe = rolle == Pruefungsteile.Wiso
                ? Beschriftungen.Einsetzen(Pruefungsteile.AngabeWiso, teil.Fragen.Count, Pruefungsteile.Aufgaben(rolle), bereich.Minuten)
                : saetze > 0 ? Beschriftungen.Einsetzen(Pruefungsteile.AngabeTeil, teil.Fragen.Count, teil.Szenarien.Count, saetze)
                : Beschriftungen.Einsetzen(Pruefungsteile.AngabeTeilOhneSaetze, teil.Fragen.Count, teil.Szenarien.Count);
            var titel = Beschriftungen.Einsetzen(Pruefungsteile.KarteBereich, Pruefungsteile.BereichKurz(rolle), bereich.Gewicht);
            var ueben = Knopf(Pruefungsteile.Ueben, false, () => _wege.Oeffnen(rolle, Pruefungsteile.Ueben), Pruefungsteile.BereichKurz(rolle));
            var pruefen = Knopf(Pruefungsteile.Pruefen, true, () => _wege.Oeffnen(rolle, Pruefungsteile.Pruefen), Pruefungsteile.BereichKurz(rolle));
            _inhalt.Children.Add(Karte(titel, "Pinsel.pink", Pruefungsteile.Deckblattname(fr, rolle), Breit ? angabe : null, ueben, pruefen));
        }
        var projekt = fr.Bereich("projekt");
        var testprojekte = Knopf(Pruefungsteile.Testprojekte, false, () => _wege.Oeffnen(Seiten.Testprojekt, null));
        var mein = Knopf(Pruefungsteile.MeinProjekt, true, () => _wege.Oeffnen(Seiten.Abschluss, null));
        _inhalt.Children.Add(Karte(Beschriftungen.Einsetzen(Pruefungsteile.KarteBereich, Pruefungsteile.Projekt, projekt.Gewicht),
            "Pinsel.green", Pruefungsteile.ProjektName, Breit ? Beschriftungen.Einsetzen(Pruefungsteile.ProjektAngabe, d.Testprojekte) : null,
            testprojekte, mein));
    }

    /// <summary>Karte mit Kopf, Name (fett), Angabe und Knoepfen; breit in einer Zeile wie im PC-Bild, schmal untereinander.</summary>
    private Border Karte(string titel, string akzent, string name, string? angabe, params Button[] knoepfe)
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new LernText(name, "Mittel", FontWeight.Bold, "Pinsel.text"));
        if (angabe is not null)
        {
            text.Children.Add(new LernText(angabe, "Klein", FontWeight.Normal, "Pinsel.muted"));
        }
        return Schrift.KopfKarte(titel, null, Breit ? Zeile(text, knoepfe) : Untereinander(text, knoepfe), akzent);
    }

    private static Control Untereinander(Control text, Button[] knoepfe)
    {
        var panel = new StackPanel { Spacing = 10, Children = { text } };
        if (knoepfe.Length > 0)
        {
            panel.Children.Add(Knopfreihe(knoepfe));
        }
        return panel;
    }

    private static WrapPanel Knopfreihe(Button[] knoepfe)
    {
        var reihe = new WrapPanel();
        foreach (var k in knoepfe)
        {
            reihe.Children.Add(k);
        }
        return reihe;
    }

    /// <summary>Zeile wie im PC-Bild: Flaeche card_alt mit Rand, links Text, rechts die Knoepfe (zu wenig Platz: darunter).</summary>
    private static Border Zeile(Control text, Button[] knoepfe)
    {
        Control inhalt = knoepfe.Length == 0 ? text : new NebenOderUnter(220, 14) { Children = { text, Knopfreihe(knoepfe) } };
        foreach (var k in knoepfe)
        {
            k.Margin = new Thickness(8, 0, 0, 0);
        }
        return new Border
        {
            Padding = new Thickness(14, 10),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Child = inhalt,
        }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border");
    }

    private static Button Knopf(string text, bool betont, Action klick, string? zusatz = null)
    {
        var knopf = LernTeile.Knopf(text, betont);
        knopf.VerticalAlignment = VerticalAlignment.Center;
        // Bildschirmleser: gleiche Knoepfe je Karte mit dem Namen der Karte unterscheiden
        Avalonia.Automation.AutomationProperties.SetName(knopf, zusatz is null ? text : text + " " + zusatz);
        knopf.Click += (_, _) => klick();
        return knopf;
    }

    // -- Teil 1 und die schriftlichen Bereiche ----------------------------------------------------------------

    private void ReiterSeite(PruefungsteilDaten.Daten d)
    {
        var fr = d.Fachrichtung;
        var rolle = Pruefungsteile.Rolle(_seite);
        if (!Breit && _seite != Pruefungsteile.Teil1)
        {
            // Handy-Bild: der Name steht als Ueberschrift, die Brotkrumen nennen nur „BEREICH n“
            _inhalt.Children.Add(new LernText(Pruefungsteile.Deckblattname(fr, rolle), "Gross", FontWeight.Bold, "Pinsel.text"));
        }
        var pillen = new Pillen([(Pruefungsteile.Ueben, Pruefungsteile.Ueben), (Pruefungsteile.Pruefen, Pruefungsteile.Pruefen)], _reiter, w =>
        {
            _reiter = w;
            _unter = null;
            Malen();
        });
        _inhalt.Children.Add(pillen);
        if (_unter is { } unter)
        {
            foreach (var teil in unter())
            {
                _inhalt.Children.Add(teil);
            }
            return;
        }
        if (_reiter == Pruefungsteile.Ueben)
        {
            UebenKarten(d, rolle);
        }
        else
        {
            PruefenKarten(d, rolle);
        }
    }

    private void UebenKarten(PruefungsteilDaten.Daten d, string rolle)
    {
        var teil = d.Teile[_seite];
        var zeilen = new StackPanel { Spacing = 8 };
        var fragen = Knopf(Pruefungsteile.Ueben, true, () => _wege.FragenUeben(_seite, teil.Fragen), Pruefungsteile.Pruefungsfragen);
        fragen.IsEnabled = teil.Fragen.Count > 0;
        zeilen.Children.Add(UebenZeile(Pruefungsteile.Pruefungsfragen,
            teil.Fragen.Count > 0 ? Beschriftungen.Einsetzen(Pruefungsteile.FragenAngabe, teil.Fragen.Count) : Pruefungsteile.KeineFragen, fragen));
        if (teil.Szenarien.Count > 0)
        {
            var name = teil.SzenarienQuelle == Core.Lernen.Quellen.Ap1 ? Pruefungsteile.Ap1Szenarien : Pruefungsteile.Ap2Szenarien;
            var szenarien = Knopf(Pruefungsteile.Ueben, true, () => _wege.SzenarienUeben(_seite, teil.SzenarienQuelle, teil.Szenarien), name);
            zeilen.Children.Add(UebenZeile(name, Beschriftungen.Einsetzen(Pruefungsteile.SzenarienAngabe, teil.Szenarien.Count), szenarien));
        }
        if (d.Saetze[_seite].Count > 0)
        {
            var waehlen = Knopf(Pruefungsteile.AufgabeWaehlen, false, () => Unter(() => SatzListe(d, einzeln: true)));
            zeilen.Children.Add(UebenZeile(Pruefungsteile.Einzelaufgabe, Pruefungsteile.EinzelaufgabeAngabe, waehlen));
        }
        _inhalt.Children.Add(Schrift.KopfKarte(Pruefungsteile.UebenTitel, null, zeilen, "Pinsel.green"));
        var stand = TeilUeben.Stand(teil, Zustand.BuchLesen());
        var zeile = Beschriftungen.Einsetzen(Pruefungsteile.Stand, stand.Fragen, stand.Bearbeitet, stand.Richtig)
            + (stand.Szenarien > 0 ? Beschriftungen.Einsetzen(Pruefungsteile.StandSzenarien, stand.SzenarienBearbeitet, stand.Szenarien) : "");
        _inhalt.Children.Add(Schrift.KopfKarte(Pruefungsteile.StandTitel, null, new LernText(zeile, "Klein", FontWeight.Normal, "Pinsel.text_soft"),
            "Pinsel.yellow"));
    }

    private Control UebenZeile(string name, string angabe, Button knopf)
    {
        var text = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new LernText(name, "Mittel", FontWeight.Bold, "Pinsel.text"),
                new LernText(angabe, "Klein", FontWeight.Normal, "Pinsel.muted"),
            },
        };
        return Breit ? Zeile(text, [knopf]) : Untereinander(text, [knopf]);
    }

    private void PruefenKarten(PruefungsteilDaten.Daten d, string rolle)
    {
        var bereich = d.Fachrichtung.Bereich(rolle);
        var art = Klausur.Art(bereich.Art);
        var starten = Knopf(Pruefungsteile.Starten, true, () => _wege.KlausurStarten(_seite, bereich.Art), Pruefungsteile.Probeklausur);
        starten.IsEnabled = art is not null && bereich.Quelle != "keine";
        _inhalt.Children.Add(PruefKarte(Pruefungsteile.Probeklausur, "Pinsel.pink",
            Beschriftungen.Einsetzen(Pruefungsteile.ProbeAngabe, bereich.Minuten, Pruefungsteile.Aufgaben(rolle)), starten));
        var saetze = d.Saetze[_seite];
        if (saetze.Count > 0)
        {
            var bearbeitet = PruefungsteilDaten.Bearbeitet(d.Fachrichtung, _seite, Gesamtstand.Verlauf(Zustand.Lernstand));
            var naechster = Knopf(Pruefungsteile.NaechsterSatz, true, () => _wege.EchtStarten(_seite, null));
            var waehlen = Knopf(Pruefungsteile.SatzWaehlen, false, () => Unter(() => SatzListe(d, einzeln: false)));
            _inhalt.Children.Add(PruefKarte(Pruefungsteile.EchtesFormat, "Pinsel.pink",
                Beschriftungen.Einsetzen(Pruefungsteile.EchtAngabe, saetze.Count, saetze.Count(s => bearbeitet.Contains(s.Kennung))), naechster, waehlen));
        }
    }

    /// <summary>Karte ohne Namen (Prüfen-Reiter): Kopf, Angabe, Knoepfe; breit in einer Zeile, schmal untereinander.</summary>
    private Border PruefKarte(string titel, string akzent, string angabe, params Button[] knoepfe)
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new LernText(angabe, "Klein", FontWeight.Normal, "Pinsel.muted"));
        Control inhalt = Breit ? new NebenOderUnter(220, 14) { Children = { text, Knopfreihe(knoepfe) } } : Untereinander(text, knoepfe);
        return Schrift.KopfKarte(titel, null, inhalt, akzent);
    }

    // -- Satz wählen, Aufgabe wählen, einzelne Aufgabe ---------------------------------------------------------

    private void Unter(Func<IEnumerable<Control>> unter)
    {
        _unter = unter;
        Malen();
    }

    private Button ZurueckKnopf(Action zurueck)
    {
        var knopf = Knopf("← " + Pruefungsteile.Zurueck, false, zurueck);
        Avalonia.Automation.AutomationProperties.SetName(knopf, Pruefungsteile.Zurueck);
        knopf.HorizontalAlignment = HorizontalAlignment.Left;
        return knopf;
    }

    /// <summary>Liste der Saetze: im Prüfen-Reiter startet ein Klick den Satz, beim Üben folgt die Liste seiner Aufgaben.</summary>
    private IEnumerable<Control> SatzListe(PruefungsteilDaten.Daten d, bool einzeln)
    {
        yield return ZurueckKnopf(() =>
        {
            _unter = null;
            Malen();
        });
        var bearbeitet = PruefungsteilDaten.Bearbeitet(d.Fachrichtung, _seite, Gesamtstand.Verlauf(Zustand.Lernstand));
        var liste = new StackPanel { Spacing = 8 };
        var saetze = d.Saetze[_seite];
        for (var i = 0; i < saetze.Count; i++)
        {
            var satz = saetze[i];
            var text = Beschriftungen.Einsetzen(Pruefungsteile.SatzZeile, i + 1, satz.Firma) + (bearbeitet.Contains(satz.Kennung) ? Pruefungsteile.SatzBearbeitet : "");
            var knopf = Knopf(einzeln ? Pruefungsteile.AufgabeWaehlen : Pruefungsteile.Starten, !einzeln,
                () =>
                {
                    if (einzeln)
                    {
                        Unter(() => AufgabenListe(d, satz));
                    }
                    else
                    {
                        _wege.EchtStarten(_seite, satz);
                    }
                }, text);
            liste.Children.Add(UebenZeile(text, Beschriftungen.Einsetzen(Pruefungsteile.SatzAngabe, satz.Minuten, satz.Aufgaben.Count), knopf));
        }
        yield return Schrift.KopfKarte(einzeln ? Pruefungsteile.AufgabeWaehlenTitel : Pruefungsteile.SatzWaehlenTitel, null, liste, "Pinsel.pink");
    }

    private IEnumerable<Control> AufgabenListe(PruefungsteilDaten.Daten d, EchtSatz satz)
    {
        yield return ZurueckKnopf(() => Unter(() => SatzListe(d, einzeln: true)));
        var liste = new StackPanel { Spacing = 8 };
        for (var i = 0; i < satz.Aufgaben.Count; i++)
        {
            var n = i + 1;
            var aufgabe = satz.Aufgaben[i];
            var kopf = Beschriftungen.Einsetzen(Pruefungsteile.AufgabeZeile, n, aufgabe.Punkte);
            var knopf = Knopf(Pruefungsteile.Ueben, true, () => Unter(() => Einzelaufgabe(d, satz, n, false)), kopf);
            liste.Children.Add(UebenZeile(kopf, aufgabe.Titel, knopf));
        }
        yield return Schrift.KopfKarte(Pruefungsteile.AufgabeWaehlenTitel, satz.Firma, liste, "Pinsel.pink");
    }

    /// <summary>Eine Aufgabe im echten Format zum Lesen (ohne Uhr, ohne Note), mit Anlagen; die Musterloesung auf Knopfdruck.</summary>
    private IEnumerable<Control> Einzelaufgabe(PruefungsteilDaten.Daten d, EchtSatz satz, int n, bool loesung)
    {
        yield return ZurueckKnopf(() => Unter(() => AufgabenListe(d, satz)));
        foreach (var teil in EchtFormatTeil.Einzelaufgabe(satz, n, loesung))
        {
            yield return teil;
        }
        if (!loesung)
        {
            var zeigen = Knopf(Pruefungsteile.Musterloesung, true, () => Unter(() => Einzelaufgabe(d, satz, n, true)));
            zeigen.HorizontalAlignment = HorizontalAlignment.Left;
            yield return zeigen;
        }
    }

    /// <summary>Start-Test: hat dieser Teil Saetze im echten Format (Satz- und Aufgabenliste)?</summary>
    internal bool HatSaetze => _daten is { } d && d.Saetze[_seite].Count > 0;

    /// <summary>Start-Test: Unteransicht „Satz wählen“ (einzeln = false) bzw. „Aufgabe wählen“ zeigen.</summary>
    internal void ListeFuerTests(bool einzeln)
    {
        if (_daten is { } d)
        {
            Unter(() => SatzListe(d, einzeln));
        }
    }

    /// <summary>Start-Test: Aufgabe n des ersten Satzes zeigen (mit oder ohne Musterloesung).</summary>
    internal void AufgabeFuerTests(int n, bool loesung)
    {
        if (_daten is { } d && d.Saetze[_seite].Count > 0)
        {
            Unter(() => Einzelaufgabe(d, d.Saetze[_seite][0], n, loesung));
        }
    }

    /// <summary>Start-Test: Reiter wechseln wie mit der Maus.</summary>
    internal void ReiterFuerTests(string reiter)
    {
        _reiter = reiter;
        _unter = null;
        Malen();
    }
}
