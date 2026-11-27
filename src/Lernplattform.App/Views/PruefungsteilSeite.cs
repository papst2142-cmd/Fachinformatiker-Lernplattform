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

/// <summary>Wohin die Knoepfe der Pruefungsteile fuehren (MainView). Der erste Wert ist immer der Teil (teil1, b1, b2, wiso).</summary>
public sealed record PruefungsteilWege(
    Action<string, IReadOnlyList<string>> FragenUeben,
    Action<string, string, IReadOnlyList<Lerninhalt>> SzenarienUeben,
    Action<string, string> KlausurStarten,
    Action<string, EchtSatz?> EchtStarten,
    Action<string, string?> Oeffnen);

/// <summary>
/// Seiten AP1 und AP2 der Pruefung.
/// <para>A 1.19.0 (Weisung P12-440, Nico 1a–5a, „genau wie der Entwurf“ aus Meldung P12-278): AP1 und AP2 gleich aufgebaut.
/// Oben immer die Segment-Leiste AP1 | AP2, darunter immer die Pillen Üben | Prüfen; alles, was ein Knopf oeffnet, erscheint in
/// dieser Seite unter beiden Leisten mit der Zeile „← Zurück · &lt;Teil&gt; · &lt;Ziel&gt;“ (<see cref="ZurueckZeile"/>):</para>
/// <list type="bullet">
/// <item>AP1: Üben mit Prüfungsfragen, Szenarien, einzelner Aufgabe im echten Format und DEIN STAND; Prüfen mit PROBEKLAUSUR und
/// ECHTES FORMAT (wie seit A 1.15.0).</item>
/// <item>AP2: je Bereich (Bereich 1, Bereich 2, WiSo) eine Karte „BEREICH n · Gewicht“ mit dem Namen; unter Üben die Zeilen
/// Prüfungsfragen, Szenarien, Einzelne Aufgabe und klein der Stand, unter Prüfen Probeklausur und Echtes Format; darunter die Karte
/// „PROJEKT · 50 %“ als Hinweis ohne Knoepfe (Nico 2a; Testprojekte und Mein Projekt nur unter „Mehr“, Nico 3).</item>
/// </list>
/// Prüfungsfragen, Szenarien und Probeklausur laufen in der alten Seite (Pruefungstrainer bzw. Szenarien), die MainView unter
/// <see cref="RahmenKopf"/> einbettet. Breit (ab <see cref="BreitAb"/>) wie das PC-Bild, schmal wie das Handy-Bild.
/// </summary>
public sealed class PruefungsteilSeite : UserControl
{
    /// <summary>Ab dieser Breite der Seite stehen Text und Knoepfe nebeneinander (PC-Bild), darunter wie am Handy.</summary>
    public const double BreitAb = 600;

    /// <summary>Bereiche von AP2 in der Reihenfolge der Karten.</summary>
    public static readonly IReadOnlyList<string> Bereiche = [Pruefungsteile.B1, Pruefungsteile.B2, Pruefungsteile.Wiso];

    private readonly string _seite;
    private readonly PruefungsteilWege _wege;
    private readonly StackPanel _inhalt = new() { Spacing = 12, Margin = new Thickness(16, 4, 16, 24) };
    private readonly ScrollViewer _rollen;
    private readonly Dictionary<string, Control> _bereichKarten = new(StringComparer.Ordinal);
    private PruefungsteilDaten.Daten? _daten;
    private bool? _breit;
    private string _reiter = Pruefungsteile.Ueben;
    // Unteransicht im Reiter: null = Karten, sonst Satz- bzw. Aufgabenliste oder eine Aufgabe
    private Func<IEnumerable<Control>>? _unter;
    // Teil (teil1, b1, b2, wiso), zu dem die Unteransicht gehoert
    private string _teil;
    // Bereich, zu dem nach dem Malen gerollt wird (Unterpunkt der PC-Seitenleiste, Nico 4a)
    private string? _sprung;

    /// <param name="seite"><see cref="Pruefungsteile.Teil1"/> oder <see cref="Pruefungsteile.Teil2"/></param>
    public PruefungsteilSeite(string seite, PruefungsteilWege wege)
    {
        _seite = seite;
        _teil = seite == Pruefungsteile.Teil1 ? seite : Pruefungsteile.B1;
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

    /// <summary>Kennung der Seite (teil1 oder teil2).</summary>
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

    /// <summary>Beim erneuten Zeigen: Stand neu lesen, zurueck zu den Karten; <paramref name="bereich"/>: zu dieser Bereich-Karte rollen.</summary>
    public void Gezeigt(string? reiter = null, string? bereich = null)
    {
        if (reiter is not null)
        {
            _reiter = reiter;
        }
        _unter = null;
        _sprung = bereich;
        Malen();
    }

    private bool Breit => _breit ?? true;

    /// <summary>A 1.17.0: Segment-Leiste AP1 | AP2.</summary>
    public SegmentLeiste? Segment { get; private set; }

    /// <summary>A 1.19.0: Pillen Üben | Prüfen (immer unter der Segment-Leiste).</summary>
    public Pillen? ReiterPillen { get; private set; }

    /// <summary>A 1.19.0: Zeile „← Zurück · …“ der offenen Unteransicht (null = Karten).</summary>
    public Control? Zurueck { get; private set; }

    private void Malen()
    {
        if (_daten is not { } d)
        {
            return;
        }
        _inhalt.Children.Clear();
        _bereichKarten.Clear();
        Zurueck = null;
        // A 1.17.0 (Weisung P12-430 B1, Reiter B): oben die Segment-Leiste AP1 | AP2
        Segment = new SegmentLeiste(_seite, Gewichte(d.Fachrichtung), Breit, ziel => _wege.Oeffnen(ziel, null));
        _inhalt.Children.Add(Segment);
        // A 1.19.0 (Nico 1): darunter immer Üben | Prüfen, auch auf AP2
        ReiterPillen = ReiterLeiste(_reiter, w =>
        {
            _reiter = w;
            _unter = null;
            Malen();
        });
        _inhalt.Children.Add(ReiterPillen);
        if (_unter is { } unter)
        {
            foreach (var teil in unter())
            {
                _inhalt.Children.Add(teil);
            }
        }
        else if (_seite == Pruefungsteile.Teil1)
        {
            if (_reiter == Pruefungsteile.Ueben)
            {
                UebenKarten(d);
            }
            else
            {
                PruefenKarten(d);
            }
        }
        else
        {
            Ap2Karten(d);
        }
        _rollen.Offset = default;
        if (_sprung is { } sprung && _bereichKarten.TryGetValue(sprung, out var karte))
        {
            _sprung = null;
            Dispatcher.UIThread.Post(() => karte.BringIntoView(), DispatcherPriority.Loaded);
        }
    }

    private static (int Ap1, int Ap2) Gewichte(Fachrichtung fr) => (fr.Bereich("ap1").Gewicht, Fachrichtung.Teil2.Sum(r => fr.Bereich(r).Gewicht));

    private static Pillen ReiterLeiste(string reiter, Action<string> waehlen) =>
        new([(Pruefungsteile.Ueben, Pruefungsteile.Ueben), (Pruefungsteile.Pruefen, Pruefungsteile.Pruefen)], reiter, waehlen);

    // -- AP2: je Bereich eine Karte, dazu der Hinweis Projekt --------------------------------------------------

    private void Ap2Karten(PruefungsteilDaten.Daten d)
    {
        var fr = d.Fachrichtung;
        foreach (var b in Bereiche)
        {
            var titel = Beschriftungen.Einsetzen(Pruefungsteile.KarteBereich, Pruefungsteile.BereichKurz(b), fr.Bereich(b).Gewicht);
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new LernText(Pruefungsteile.Deckblattname(fr, b), "Mittel", FontWeight.Bold, "Pinsel.text"));
            foreach (var zeile in _reiter == Pruefungsteile.Ueben ? UebenZeilen(d, b, mitStand: true) : PruefenZeilen(d, b))
            {
                panel.Children.Add(zeile);
            }
            var karte = Schrift.KopfKarte(titel, null, panel, "Pinsel.pink");
            _bereichKarten[b] = karte;
            _inhalt.Children.Add(karte);
        }
        // Nico 2a: Karte bleibt als Hinweis ohne Knoepfe
        var projekt = fr.Bereich("projekt");
        _inhalt.Children.Add(Schrift.KopfKarte(Beschriftungen.Einsetzen(Pruefungsteile.KarteBereich, Pruefungsteile.Projekt, projekt.Gewicht), null,
            new LernText(Pruefungsteile.ProjektName, "Mittel", FontWeight.Bold, "Pinsel.text"), "Pinsel.green"));
    }

    /// <summary>Zeilen „Üben“ eines Teils: Prüfungsfragen, Szenarien (falls es welche gibt), einzelne Aufgabe (falls es Saetze gibt).</summary>
    private List<Control> UebenZeilen(PruefungsteilDaten.Daten d, string t, bool mitStand)
    {
        var teil = d.Teile[t];
        var name = Pruefungsteile.BereichKurz(t);
        var zeilen = new List<Control>();
        var fragen = Knopf(Pruefungsteile.Ueben, true, () => _wege.FragenUeben(t, teil.Fragen), Pruefungsteile.Pruefungsfragen + " " + name);
        fragen.IsEnabled = teil.Fragen.Count > 0;
        zeilen.Add(UebenZeile(Pruefungsteile.Pruefungsfragen,
            teil.Fragen.Count > 0 ? Beschriftungen.Einsetzen(Pruefungsteile.FragenAngabe, teil.Fragen.Count) : Pruefungsteile.KeineFragen, fragen));
        if (teil.Szenarien.Count > 0)
        {
            var szenarien = Knopf(Pruefungsteile.Ueben, true, () => _wege.SzenarienUeben(t, teil.SzenarienQuelle, teil.Szenarien),
                Pruefungsteile.Szenarien + " " + name);
            zeilen.Add(UebenZeile(Pruefungsteile.Szenarien, Beschriftungen.Einsetzen(Pruefungsteile.SzenarienAngabe, teil.Szenarien.Count), szenarien));
        }
        if (d.Saetze[t].Count > 0)
        {
            var waehlen = Knopf(Pruefungsteile.AufgabeWaehlen, false, () => Unter(t, () => SatzListe(d, einzeln: true)), name);
            zeilen.Add(UebenZeile(Pruefungsteile.Einzelaufgabe, Pruefungsteile.EinzelaufgabeAngabe, waehlen));
        }
        if (mitStand)
        {
            zeilen.Add(new LernText(StandZeile(teil), "Klein", FontWeight.Normal, "Pinsel.muted"));
        }
        return zeilen;
    }

    private static string StandZeile(TeilInhalte teil)
    {
        var stand = TeilUeben.Stand(teil, Zustand.BuchLesen());
        return Beschriftungen.Einsetzen(Pruefungsteile.Stand, stand.Fragen, stand.Bearbeitet, stand.Richtig)
            + (stand.Szenarien > 0 ? Beschriftungen.Einsetzen(Pruefungsteile.StandSzenarien, stand.SzenarienBearbeitet, stand.Szenarien) : "");
    }

    /// <summary>Zeilen „Prüfen“ eines Bereichs von AP2: Probeklausur, Echtes Format (falls es Saetze gibt).</summary>
    private List<Control> PruefenZeilen(PruefungsteilDaten.Daten d, string t)
    {
        var zeilen = new List<Control>();
        var bereich = d.Fachrichtung.Bereich(t);
        var name = Pruefungsteile.BereichKurz(t);
        var starten = Knopf(Pruefungsteile.Starten, true, () => _wege.KlausurStarten(t, bereich.Art), Pruefungsteile.ProbeklausurZeile + " " + name);
        starten.IsEnabled = Klausur.Art(bereich.Art) is not null && bereich.Quelle != "keine";
        zeilen.Add(UebenZeile(Pruefungsteile.ProbeklausurZeile,
            Beschriftungen.Einsetzen(Pruefungsteile.ProbeAngabe, bereich.Minuten, Pruefungsteile.Aufgaben(t)), starten));
        var saetze = d.Saetze[t];
        if (saetze.Count > 0)
        {
            var bearbeitet = PruefungsteilDaten.Bearbeitet(d.Fachrichtung, t, Gesamtstand.Verlauf(Zustand.Lernstand));
            var naechster = Knopf(Pruefungsteile.NaechsterSatz, true, () => _wege.EchtStarten(t, null), name);
            var waehlen = Knopf(Pruefungsteile.SatzWaehlen, false, () => Unter(t, () => SatzListe(d, einzeln: false)), name);
            var text = ZeilenText(Pruefungsteile.EchtesFormatZeile,
                Beschriftungen.Einsetzen(Pruefungsteile.EchtAngabe, saetze.Count, saetze.Count(s => bearbeitet.Contains(s.Kennung))));
            zeilen.Add(Breit ? Zeile(text, [naechster, waehlen]) : Untereinander(text, [naechster, waehlen]));
        }
        return zeilen;
    }

    // -- Bausteine ----------------------------------------------------------------------------------------

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

    private static StackPanel ZeilenText(string name, string angabe) => new()
    {
        Spacing = 2,
        VerticalAlignment = VerticalAlignment.Center,
        Children =
        {
            new LernText(name, "Mittel", FontWeight.Bold, "Pinsel.text"),
            new LernText(angabe, "Klein", FontWeight.Normal, "Pinsel.muted"),
        },
    };

    private Control UebenZeile(string name, string angabe, Button knopf) =>
        Breit ? Zeile(ZeilenText(name, angabe), [knopf]) : Untereinander(ZeilenText(name, angabe), [knopf]);

    // -- AP1 ------------------------------------------------------------------------------------------------

    private void UebenKarten(PruefungsteilDaten.Daten d)
    {
        var zeilen = new StackPanel { Spacing = 8 };
        foreach (var zeile in UebenZeilen(d, _seite, mitStand: false))
        {
            zeilen.Children.Add(zeile);
        }
        _inhalt.Children.Add(Schrift.KopfKarte(Pruefungsteile.UebenTitel, null, zeilen, "Pinsel.green"));
        _inhalt.Children.Add(Schrift.KopfKarte(Pruefungsteile.StandTitel, null,
            new LernText(StandZeile(d.Teile[_seite]), "Klein", FontWeight.Normal, "Pinsel.text_soft"), "Pinsel.yellow"));
    }

    private void PruefenKarten(PruefungsteilDaten.Daten d)
    {
        var rolle = Pruefungsteile.Rolle(_seite);
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
            var waehlen = Knopf(Pruefungsteile.SatzWaehlen, false, () => Unter(_seite, () => SatzListe(d, einzeln: false)));
            _inhalt.Children.Add(PruefKarte(Pruefungsteile.EchtesFormat, "Pinsel.pink",
                Beschriftungen.Einsetzen(Pruefungsteile.EchtAngabe, saetze.Count, saetze.Count(s => bearbeitet.Contains(s.Kennung))), naechster, waehlen));
        }
    }

    /// <summary>Karte ohne Namen (Prüfen-Reiter AP1): Kopf, Angabe, Knoepfe; breit in einer Zeile, schmal untereinander.</summary>
    private Border PruefKarte(string titel, string akzent, string angabe, params Button[] knoepfe)
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new LernText(angabe, "Klein", FontWeight.Normal, "Pinsel.muted"));
        Control inhalt = Breit ? new NebenOderUnter(220, 14) { Children = { text, Knopfreihe(knoepfe) } } : Untereinander(text, knoepfe);
        return Schrift.KopfKarte(titel, null, inhalt, akzent);
    }

    // -- Zurück-Zeile und Rahmen fuer eingebettete Seiten ---------------------------------------------------

    /// <summary>
    /// A 1.19.0 (Nico 1a, „Neu gestaltet“ in P12-278): Kopf ueber jeder geoeffneten Ansicht: Knopf „← Zurück“, daneben fett, wohin
    /// man gerade schaut (<see cref="Pruefungsteile.ZurueckTitel"/>).
    /// </summary>
    public static Control ZurueckZeile(string titel, Action zurueck)
    {
        var knopf = LernTeile.Knopf("← " + Pruefungsteile.Zurueck, false);
        Avalonia.Automation.AutomationProperties.SetName(knopf, Pruefungsteile.Zurueck);
        knopf.VerticalAlignment = VerticalAlignment.Center;
        knopf.Margin = new Thickness(0, 0, 12, 0);
        knopf.Click += (_, _) => zurueck();
        DockPanel.SetDock(knopf, Dock.Left);
        var text = new LernText(titel, "Normal", FontWeight.Bold, "Pinsel.text_soft") { VerticalAlignment = VerticalAlignment.Center };
        return new DockPanel { LastChildFill = true, Children = { knopf, text } };
    }

    /// <summary>
    /// A 1.19.0 (Nico 1): Rahmen oben ueber einer eingebetteten alten Seite (Pruefungstrainer, Szenarien): Segment-Leiste AP1 | AP2,
    /// Pillen Üben | Prüfen und die Zurück-Zeile. <paramref name="zeigen"/> oeffnet eine Haelfte mit Reiter (null = Üben). Breit
    /// wie die Seite selbst (ab <see cref="BreitAb"/>), bei einer Aenderung der Breite neu gebaut.
    /// </summary>
    public static Control RahmenKopf(string haelfte, string reiter, string titel, bool breit, Action<string, string?> zeigen, Action zurueck)
    {
        var huelle = new ContentControl();
        void Bauen(bool b)
        {
            var kopf = new StackPanel { Spacing = 12, Margin = new Thickness(16, 4, 16, 4) };
            kopf.Children.Add(new SegmentLeiste(haelfte, Gewichte(Fachrichtung.Nach(Zustand.Sicht)), b, ziel => zeigen(ziel, null)));
            kopf.Children.Add(ReiterLeiste(reiter, w => zeigen(haelfte, w)));
            kopf.Children.Add(ZurueckZeile(titel, zurueck));
            huelle.Content = kopf;
        }
        var jetzt = breit;
        Bauen(jetzt);
        huelle.SizeChanged += (_, e) =>
        {
            var b = e.NewSize.Width >= BreitAb;
            if (b != jetzt)
            {
                jetzt = b;
                Bauen(b);
            }
        };
        return huelle;
    }

    // -- Satz wählen, Aufgabe wählen, einzelne Aufgabe ---------------------------------------------------------

    private void Unter(string teil, Func<IEnumerable<Control>> unter)
    {
        _teil = teil;
        _unter = unter;
        Malen();
    }

    private Control ZurueckZu(string ziel, Action zurueck)
    {
        var zeile = ZurueckZeile(Beschriftungen.Einsetzen(Pruefungsteile.ZurueckTitel, Pruefungsteile.BereichKurz(_teil), ziel), zurueck);
        Zurueck = zeile;
        return zeile;
    }

    /// <summary>Liste der Saetze: im Prüfen-Reiter startet ein Klick den Satz, beim Üben folgt die Liste seiner Aufgaben.</summary>
    private IEnumerable<Control> SatzListe(PruefungsteilDaten.Daten d, bool einzeln)
    {
        yield return ZurueckZu(einzeln ? Pruefungsteile.EinzelaufgabeKurz : Pruefungsteile.SatzWaehlen, () =>
        {
            _unter = null;
            Malen();
        });
        var bearbeitet = PruefungsteilDaten.Bearbeitet(d.Fachrichtung, _teil, Gesamtstand.Verlauf(Zustand.Lernstand));
        var liste = new StackPanel { Spacing = 8 };
        var saetze = d.Saetze[_teil];
        var teil = _teil;
        for (var i = 0; i < saetze.Count; i++)
        {
            var satz = saetze[i];
            var text = Beschriftungen.Einsetzen(Pruefungsteile.SatzZeile, i + 1, satz.Firma) + (bearbeitet.Contains(satz.Kennung) ? Pruefungsteile.SatzBearbeitet : "");
            var knopf = Knopf(einzeln ? Pruefungsteile.AufgabeWaehlen : Pruefungsteile.Starten, !einzeln,
                () =>
                {
                    if (einzeln)
                    {
                        Unter(teil, () => AufgabenListe(d, satz));
                    }
                    else
                    {
                        _wege.EchtStarten(teil, satz);
                    }
                }, text);
            liste.Children.Add(UebenZeile(text, Beschriftungen.Einsetzen(Pruefungsteile.SatzAngabe, satz.Minuten, satz.Aufgaben.Count), knopf));
        }
        yield return Schrift.KopfKarte(einzeln ? Pruefungsteile.AufgabeWaehlenTitel : Pruefungsteile.SatzWaehlenTitel, null, liste, "Pinsel.pink");
    }

    private IEnumerable<Control> AufgabenListe(PruefungsteilDaten.Daten d, EchtSatz satz)
    {
        var teil = _teil;
        yield return ZurueckZu(satz.Firma, () => Unter(teil, () => SatzListe(d, einzeln: true)));
        var liste = new StackPanel { Spacing = 8 };
        for (var i = 0; i < satz.Aufgaben.Count; i++)
        {
            var n = i + 1;
            var aufgabe = satz.Aufgaben[i];
            var kopf = Beschriftungen.Einsetzen(Pruefungsteile.AufgabeZeile, n, aufgabe.Punkte);
            var knopf = Knopf(Pruefungsteile.Ueben, true, () => Unter(teil, () => Einzelaufgabe(d, satz, n, false)), kopf);
            liste.Children.Add(UebenZeile(kopf, aufgabe.Titel, knopf));
        }
        yield return Schrift.KopfKarte(Pruefungsteile.AufgabeWaehlenTitel, satz.Firma, liste, "Pinsel.pink");
    }

    /// <summary>Eine Aufgabe im echten Format zum Lesen (ohne Uhr, ohne Note), mit Anlagen; die Musterloesung auf Knopfdruck.</summary>
    private IEnumerable<Control> Einzelaufgabe(PruefungsteilDaten.Daten d, EchtSatz satz, int n, bool loesung)
    {
        var teil = _teil;
        yield return ZurueckZu(Beschriftungen.Einsetzen(Pruefungsteile.AufgabeKurz, n), () => Unter(teil, () => AufgabenListe(d, satz)));
        foreach (var stueck in EchtFormatTeil.Einzelaufgabe(satz, n, loesung))
        {
            yield return stueck;
        }
        if (!loesung)
        {
            var zeigen = Knopf(Pruefungsteile.Musterloesung, true, () => Unter(teil, () => Einzelaufgabe(d, satz, n, true)));
            zeigen.HorizontalAlignment = HorizontalAlignment.Left;
            yield return zeigen;
        }
    }

    // -- Start-Test ------------------------------------------------------------------------------------------

    /// <summary>Start-Test: hat dieser Teil (AP1 bzw. Bereich 1 von AP2) Saetze im echten Format?</summary>
    internal bool HatSaetze => _daten is { } d && d.Saetze[TeilFuerTests].Count > 0;

    private string TeilFuerTests => _seite == Pruefungsteile.Teil1 ? _seite : Pruefungsteile.B1;

    /// <summary>Start-Test: Unteransicht „Satz wählen“ (einzeln = false) bzw. „Aufgabe wählen“ zeigen (AP2: Bereich 1 oder <paramref name="teil"/>).</summary>
    internal void ListeFuerTests(bool einzeln, string? teil = null)
    {
        if (_daten is { } d)
        {
            _reiter = einzeln ? Pruefungsteile.Ueben : Pruefungsteile.Pruefen;
            Unter(teil ?? TeilFuerTests, () => SatzListe(d, einzeln));
        }
    }

    /// <summary>Start-Test: Aufgabe n des ersten Satzes zeigen (mit oder ohne Musterloesung).</summary>
    internal void AufgabeFuerTests(int n, bool loesung)
    {
        var teil = TeilFuerTests;
        if (_daten is { } d && d.Saetze[teil].Count > 0)
        {
            _reiter = Pruefungsteile.Ueben;
            Unter(teil, () => Einzelaufgabe(d, d.Saetze[teil][0], n, loesung));
        }
    }

    /// <summary>Start-Test: Reiter wechseln wie mit der Maus.</summary>
    internal void ReiterFuerTests(string reiter)
    {
        _reiter = reiter;
        _unter = null;
        Malen();
    }

    /// <summary>Start-Test und Fotos: ganz nach unten rollen (AP2: Karte Projekt).</summary>
    internal void NachUnten() => _rollen.ScrollToEnd();

    /// <summary>Start-Test: Karte eines Bereichs von AP2 (null = nicht gemalt).</summary>
    internal Control? BereichKarte(string bereich) => _bereichKarten.GetValueOrDefault(bereich);
}
