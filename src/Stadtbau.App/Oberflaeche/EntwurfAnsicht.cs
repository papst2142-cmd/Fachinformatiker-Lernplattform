using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Messung;
using Lernplattform.Spiel;
using Lernplattform.Views;
using Stadtbau.Core;

using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// S 0.9.0 (Weisung P12-48, Plan P12-26b Abschnitt 1): der Editor „Gebäude entwerfen“. Links die Bauteile in Gruppen mit
/// Vorschaubild und deutschem Namen, in der Mitte die 3D-Ansicht mit Bauraster (eigene Spiel-Flaeche mit der kleinen Stadt
/// aus <see cref="EntwurfAnsicht3D"/>, darueber die Ebene <see cref="EntwurfBedienung"/>), oben die Wahl der Ebene, rechts
/// das ausgewaehlte Teil (Drehen, Kopieren, Loeschen, Ebene, Pfeile, Eingang), die Grenzen des Gebaeudes, die Pruefungen
/// und Tag/Nacht. Jede Aenderung baut die Vorschau im Hintergrund neu (der neueste Stand gewinnt).
/// </summary>
public sealed class EntwurfAnsicht : Grid
{
    private readonly Spieldaten _daten;
    private readonly Action<string> _melden;
    private readonly StadtSzene.Vorrat _vorrat = new();
    private readonly StackPanel _auswahl = new() { Spacing = 6 };
    private readonly StackPanel _pruefungen = new() { Spacing = 6 };
    private readonly TextBlock _grenzText = Schrift.Text("", "Klein");
    private readonly ProgressBar _grenzBalken = new() { Minimum = 0, Maximum = 100, Height = 10, MinHeight = 10 };
    private readonly WrapPanel _ebenen = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _teile = new() { Spacing = 4 };
    private readonly TextBlock _titel = Schrift.Text("", "Mittel", FontWeight.Bold);
    private readonly TextBlock _status = Ansage.Lebend(Schrift.Text("", "Klein", farbe: "Pinsel.muted"));
    private readonly CheckBox _nacht = new() { Content = Schrift.Text(Texte.EntwurfNacht, "Klein") };

    /// <summary>Weisung P12-52: Raster zeigen (Vorgabe an, gemerkt in einstellungen.json des Werkzeugordners).</summary>
    private readonly CheckBox _raster = new() { Content = Schrift.Text(Texte.EntwurfRaster, "Klein") };

    private readonly Werkzeugordner? _ordner;

    /// <summary>Rasterlinien in der 3D-Ansicht zeigen? (Setzen und Waehlen gehen immer.)</summary>
    public bool RasterZeigen => _raster.IsChecked != false;

    /// <summary>Raster ein- oder ausblenden (Schalter und Taste G).</summary>
    public void RasterUmschalten() => _raster.IsChecked = !RasterZeigen;
    private readonly Dictionary<string, ToggleButton> _teilKnoepfe = new(StringComparer.Ordinal);
    private int _stand;
    private int _gebaut = -1;
    private bool _baut;
    private long _aenderung;
    private bool _wartetAufBild;
    private TaskCompletionSource _fertig = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Entwurf Entwurf { get; }

    public StadtFlaeche Drei { get; } = new();

    public EntwurfBedienung Bedienung { get; }

    /// <summary>Gewaehlte Ebene (0 = EG); Ebenen darueber sind ausgeblendet.</summary>
    public int Ebene { get; private set; }

    /// <summary>Teil, das beim naechsten Tippen gesetzt wird (null = waehlen).</summary>
    public string? SetzenTeil { get; private set; }

    /// <summary>Drehung des naechsten gesetzten Teils.</summary>
    public int SetzenDreh { get; set; }

    /// <summary>Ausgewaehltes Teil (Stelle in <see cref="Entwurf.Teile"/>) oder -1.</summary>
    public int Auswahl { get; private set; } = -1;

    /// <summary>Seit dem letzten Speichern geaendert?</summary>
    public bool Ungespeichert { get; private set; }

    /// <summary>Letzte Funde der Pruefung.</summary>
    public List<StadtbauFund> Funde { get; private set; } = [];

    /// <summary>Dreiecke des ganzen Gebaeudes.</summary>
    public int Dreiecke { get; private set; }

    /// <summary>Vorschau mit dem aktuellen Stand gezeichnet (Start-Test).</summary>
    public Task Fertig => _fertig.Task;

    /// <summary>Zeit von der Aenderung (oder dem Oeffnen) bis zum ersten Bild der neuen Vorschau in ms.</summary>
    public double ZeitMs { get; private set; }

    /// <summary>Speichern als Blueprint angefordert (Hauptansicht fragt nach dem Namen).</summary>
    public event Action? SpeichernGewuenscht;

    /// <summary>Zurueck zur Stadt angefordert.</summary>
    public event Action? ZurueckGewuenscht;

    /// <summary>Verlauf des Entwurfs geaendert (Kopfleiste: Rueckgaengig/Wiederholen).</summary>
    public event Action? VerlaufGeaendert;

    public EntwurfAnsicht(Entwurf entwurf, Spieldaten daten, Action<string> melden, Werkzeugordner? ordner = null)
    {
        Entwurf = entwurf;
        _daten = daten;
        _melden = melden;
        _ordner = ordner;
        _raster.IsChecked = ordner is null || WerkzeugEinstellungen.RasterZeigen(ordner);
        Bedienung = new EntwurfBedienung(this);
        ColumnDefinitions = new ColumnDefinitions("260,*,340");
        Entwurf.Geaendert += () =>
        {
            Ungespeichert = true;
            if (Auswahl >= Entwurf.Teile.Count)
            {
                Auswahl = -1;
            }
            Neu();
            VerlaufGeaendert?.Invoke();
        };
        Drei.BildGezeichnet += BildGezeichnet;
        Drei.GlGestartet += () => Dispatcher.UIThread.Post(GanzZeigen);
        _nacht.Mit(Texte.TippEntwurfNacht);
        _nacht.Vorlese(Texte.EntwurfNacht);
        _nacht.IsCheckedChanged += (_, _) =>
        {
            _gebaut = -1;
            Aktualisieren();
        };
        _raster.Mit(Texte.TippEntwurfRaster);
        _raster.Vorlese(Texte.EntwurfRaster);
        _raster.IsCheckedChanged += (_, _) =>
        {
            Bedienung.InvalidateVisual();
            if (_ordner is null)
            {
                return;
            }
            try
            {
                WerkzeugEinstellungen.RasterZeigenSetzen(_ordner, RasterZeigen);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _ordner.Fehler("Raster merken: " + e.Message);
                _melden(Texte.RasterFehler);
            }
        };

        Children.Add(Links());
        var mitte = Mitte();
        SetColumn(mitte, 1);
        Children.Add(mitte);
        var rechts = new ScrollViewer { Content = new Border { Child = Rechts(), Padding = new Thickness(0, 0, Dialog.RandFuerLeiste, 0) }, Margin = new Thickness(0, 12, 12, 12) };
        SetColumn(rechts, 2);
        Children.Add(rechts);
        TitelSetzen();
        EbenenZeigen();
        Neu();
    }

    private void TitelSetzen() => _titel.Text = Texte.EntwurfTitel(Entwurf.Name ?? Texte.EntwurfTitelNeu);

    /// <summary>Nach dem Speichern: Name im Titel, nichts mehr ungespeichert.</summary>
    public void Gespeichert(string name)
    {
        Ungespeichert = false;
        _titel.Text = Texte.EntwurfTitel(name);
    }

    // ---- Aufbau -----------------------------------------------------------------------------------

    private static Button Knopf(string text, string name, string tipp, Action a, bool betont = false)
    {
        var k = Schrift.Knopf(text, betont).Vorlese(name).Mit(tipp);
        k.Click += (_, _) => a();
        return k;
    }

    /// <summary>Gruppen der Teile links (Reihenfolge der Anzeige).</summary>
    public static readonly (string Name, string Tipp, Func<Bauteile.Ausrichtung, bool> Passt)[] Gruppen =
    [
        (Texte.EntwurfGruppeWaende, Texte.TippEntwurfWaende, a => a.Art is Bauteile.Art.Wand or Bauteile.Art.Block),
        (Texte.EntwurfGruppeEcken, Texte.TippEntwurfEcken, a => a.Art == Bauteile.Art.Ecke),
        (Texte.EntwurfGruppeTueren, Texte.TippEntwurfTueren, a => a.Art == Bauteile.Art.Tuer),
        (Texte.EntwurfGruppeDaecher, Texte.TippEntwurfDaecher, a => a.Art == Bauteile.Art.Dach),
        (Texte.EntwurfGruppeDetails, Texte.TippEntwurfDetails, a => a.Art is Bauteile.Art.Detail or Bauteile.Art.DachDetail),
    ];

    private Control Links()
    {
        var gruppen = new StackPanel { Spacing = 8 };
        foreach (var (name, tipp, passt) in Gruppen)
        {
            var liste = new StackPanel { Spacing = 4 };
            foreach (var (teil, a) in Bauteile.Tabelle.Where(p => passt(p.Value)).OrderBy(p => p.Value.Anzeige, StringComparer.CurrentCulture))
            {
                liste.Children.Add(TeilKnopf(Bauteile.Praefix + teil, a));
            }
            var kopf = Schrift.Text(name, gewicht: FontWeight.Bold);
            var exp = new Expander { Header = kopf, Content = liste, IsExpanded = name == Texte.EntwurfGruppeWaende, HorizontalAlignment = HorizontalAlignment.Stretch }.Vorlese(name).Mit(tipp);
            gruppen.Children.Add(exp);
        }
        _teile.Children.Add(Schrift.Text(Texte.EntwurfTeile, "Mittel", FontWeight.Bold));
        _teile.Children.Add(gruppen);
        return new ScrollViewer { Content = new Border { Child = _teile, Padding = new Thickness(0, 0, Dialog.RandFuerLeiste, 0) }, Margin = new Thickness(12, 12, 0, 12) };
    }

    private ToggleButton TeilKnopf(string id, Bauteile.Ausrichtung a)
    {
        var reihe = new DockPanel { LastChildFill = true };
        if (Katalog.Aktiv?.Eintrag(id) is { } eintrag && KatalogBilder.Bild(eintrag) is { } quelle)
        {
            var bild = new Image { Source = quelle, Width = 40, Height = 40, Margin = new Thickness(0, 0, 8, 0) };
            DockPanel.SetDock(bild, Dock.Left);
            reihe.Children.Add(bild);
        }
        var name = Schrift.Text(a.Anzeige, "Klein");
        name.VerticalAlignment = VerticalAlignment.Center;
        reihe.Children.Add(name);
        var k = new ToggleButton { Content = reihe, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(6, 4) }
            .Vorlese(a.Anzeige);
        k.Click += (_, _) => TeilWaehlen(k.IsChecked == true ? id : null);
        _teilKnoepfe[id] = k;
        return k;
    }

    /// <summary>Teil zum Setzen waehlen (null = wieder waehlen statt setzen).</summary>
    public void TeilWaehlen(string? id)
    {
        SetzenTeil = id;
        SetzenDreh = 0;
        foreach (var (k, b) in _teilKnoepfe)
        {
            b.IsChecked = k == id;
        }
        if (id is not null)
        {
            Waehlen(-1);
        }
        Bedienung.SchattenWeg();
    }

    private Control Mitte()
    {
        var ebene = new Grid();
        ebene.Children.Add(Drei);
        ebene.Children.Add(Bedienung.Vorlese(Texte.GebaeudeEntwerfen));
        var flaeche = new Border { Child = ebene, CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderThickness = new Thickness(1) }
            .Farbe(Border.BorderBrushProperty, "Pinsel.border");
        var oben = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 8),
            Children =
            {
                _titel,
                Schrift.Text(Texte.EntwurfHinweis, "Klein", farbe: "Pinsel.muted"),
                new WrapPanel
                {
                    Children =
                    {
                        Knopf(Texte.EntwurfSpeichern, Texte.EntwurfSpeichern, Texte.TippEntwurfSpeichern, () => SpeichernGewuenscht?.Invoke(), true),
                        Knopf(Texte.EntwurfZurueck, Texte.EntwurfZurueck, Texte.TippEntwurfZurueck, () => ZurueckGewuenscht?.Invoke()),
                    },
                },
                new WrapPanel { Children = { Schrift.Text(Texte.EntwurfEbene + ":", "Klein", FontWeight.Bold).Mit(Texte.TippEntwurfEbene), _ebenen } },
                _status,
            },
        };
        var knoepfe = new WrapPanel
        {
            Margin = new Thickness(0, 8, 0, 0),
            Children =
            {
                Knopf("+", Texte.Vergroessern, Texte.ZoomTipp, () => Bedienung.Zoomen(1.25f)),
                Knopf("−", Texte.Verkleinern, Texte.ZoomTipp, () => Bedienung.Zoomen(0.8f)),
                Knopf(Texte.EntwurfKameraDrehen, Texte.EntwurfKameraDrehen, Texte.TippEntwurfKamera, () => Bedienung.Drehen(45)),
                Knopf(Texte.Steiler, Texte.Steiler, Texte.KippenTipp, () => Bedienung.Kippen(8)),
                Knopf(Texte.Flacher, Texte.Flacher, Texte.KippenTipp, () => Bedienung.Kippen(-8)),
                Knopf(Texte.EntwurfGanzZeigen, Texte.EntwurfGanzZeigen, Texte.GanzeStadt3DTipp, GanzZeigen),
            },
        };
        var mitte = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(12) };
        mitte.Children.Add(oben);
        SetRow(flaeche, 1);
        mitte.Children.Add(flaeche);
        SetRow(knoepfe, 2);
        mitte.Children.Add(knoepfe);
        return mitte;
    }

    private void EbenenZeigen()
    {
        _ebenen.Children.Clear();
        for (var e = 0; e < Bauteile.Ebenen; e++)
        {
            var nr = e;
            var k = new ToggleButton { Content = Schrift.Text(Entwurf.EbeneName(e), "Klein"), IsChecked = e == Ebene, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(8, 4) }
                .Vorlese(Texte.EntwurfEbene + " " + Entwurf.EbeneName(e)).Mit(Texte.TippEntwurfEbene);
            k.Click += (_, _) => EbeneSetzen(nr);
            _ebenen.Children.Add(k);
        }
    }

    /// <summary>Ebene waehlen (Ebenen darueber werden ausgeblendet).</summary>
    public void EbeneSetzen(int e)
    {
        Ebene = Math.Clamp(e, 0, Bauteile.Ebenen - 1);
        foreach (var (k, i) in _ebenen.Children.OfType<ToggleButton>().Select((k, i) => (k, i)))
        {
            k.IsChecked = i == Ebene;
        }
        _gebaut = -1;
        Bedienung.SchattenWeg();
        Aktualisieren();
    }

    private Control Rechts()
    {
        var grenzen = Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.EntwurfGrenzen, "Mittel", FontWeight.Bold), _grenzBalken, _grenzText } });
        var pruefung = Schrift.Karte(new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.EntwurfPruefungen, "Mittel", FontWeight.Bold), _pruefungen } });
        var licht = Schrift.Karte(new StackPanel { Spacing = 6, Children = { _nacht, _raster } });
        return new StackPanel { Spacing = 12, Children = { Schrift.Karte(_auswahl), grenzen, pruefung, licht } };
    }

    // ---- Auswahl ----------------------------------------------------------------------------------

    /// <summary>Teil waehlen (-1 = keines); die Karte rechts zeigt es.</summary>
    public void Waehlen(int i)
    {
        Auswahl = i >= 0 && i < Entwurf.Teile.Count ? i : -1;
        AuswahlZeigen();
        Bedienung.InvalidateVisual();
    }

    private void AuswahlZeigen()
    {
        _auswahl.Children.Clear();
        _auswahl.Children.Add(Schrift.Text(Texte.EntwurfAuswahl, "Mittel", FontWeight.Bold));
        if (Auswahl < 0)
        {
            _auswahl.Children.Add(Schrift.Text(Texte.EntwurfNichtsGewaehlt, "Klein", farbe: "Pinsel.muted"));
            return;
        }
        var t = Entwurf.Teile[Auswahl];
        _auswahl.Children.Add(Schrift.Text(t.Art?.Anzeige ?? t.Name, gewicht: FontWeight.Bold));
        _auswahl.Children.Add(Schrift.Text(Texte.EntwurfTeilStelle(t.Stockwerk, t.X, t.Y, Texte.SeiteName(t.Fassade)), "Klein", farbe: "Pinsel.muted"));
        var nr = Auswahl;
        _auswahl.Children.Add(new WrapPanel
        {
            Children =
            {
                Knopf(Texte.EntwurfDrehen, Texte.EntwurfDrehen, Texte.TippEntwurfDrehen, () => Entwurf.Drehen(nr)),
                Knopf(Texte.EntwurfKopieren, Texte.EntwurfKopieren, Texte.TippEntwurfKopieren, () =>
                {
                    if (Entwurf.Kopieren(nr) is var neu and >= 0)
                    {
                        Waehlen(neu);
                    }
                }),
                Knopf(Texte.EntwurfLoeschen, Texte.EntwurfLoeschen, Texte.TippEntwurfLoeschen, () =>
                {
                    Entwurf.Loeschen(nr);
                    Waehlen(-1);
                }),
            },
        });
        _auswahl.Children.Add(new WrapPanel
        {
            Children =
            {
                Knopf(Texte.EntwurfHoeher, Texte.EntwurfHoeher, Texte.TippEntwurfEbeneWechseln, () => Entwurf.EbeneWechseln(nr, 1)),
                Knopf(Texte.EntwurfTiefer, Texte.EntwurfTiefer, Texte.TippEntwurfEbeneWechseln, () => Entwurf.EbeneWechseln(nr, -1)),
            },
        });
        _auswahl.Children.Add(new WrapPanel
        {
            Children =
            {
                Knopf(Texte.EntwurfWest, Texte.EntwurfNachWest, Texte.TippEntwurfPfeile, () => Entwurf.Verschieben(nr, -1, 0)),
                Knopf(Texte.EntwurfNord, Texte.EntwurfNachNord, Texte.TippEntwurfPfeile, () => Entwurf.Verschieben(nr, 0, -1)),
                Knopf(Texte.EntwurfSued, Texte.EntwurfNachSued, Texte.TippEntwurfPfeile, () => Entwurf.Verschieben(nr, 0, 1)),
                Knopf(Texte.EntwurfOst, Texte.EntwurfNachOst, Texte.TippEntwurfPfeile, () => Entwurf.Verschieben(nr, 1, 0)),
            },
        });
        if (Entwurf.Eingaenge.Any(e => e.X == t.X && e.Y == t.Y && e.Seite == t.Fassade && Entwurf.EingangMoeglich(t)))
        {
            _auswahl.Children.Add(Schrift.Text(Texte.EntwurfIstEingang, "Klein", FontWeight.Bold, "Pinsel.green"));
        }
        else if (Entwurf.EingangMoeglich(t))
        {
            _auswahl.Children.Add(Knopf(Texte.EntwurfAlsEingang, Texte.EntwurfAlsEingang, Texte.TippEntwurfEingang, () => Entwurf.EingangFestlegen(nr)));
        }
    }

    // ---- Vorschau bauen ---------------------------------------------------------------------------

    /// <summary>Nach jeder Aenderung: Pruefung, Grenzen, Auswahl und die Vorschau neu.</summary>
    private void Neu()
    {
        _stand++;
        _aenderung = Stopwatch.GetTimestamp();
        Dreiecke = Bauteile.Dreiecke(Entwurf.Teile, Katalog.Bauteil);
        Funde = Entwurf.Pruefen(Katalog.Bauteil);
        GrenzenZeigen();
        PruefungZeigen();
        AuswahlZeigen();
        Bedienung.StandGeaendert();
        if (_fertig.Task.IsCompleted)
        {
            _fertig = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        Aktualisieren();
    }

    private void GrenzenZeigen()
    {
        _grenzBalken.Value = Math.Min(100, Dreiecke * 100.0 / Bauteile.GrenzeDreiecke);
        var anteil = Dreiecke / (double)Bauteile.GrenzeDreiecke;
        _grenzBalken.Farbe(ProgressBar.ForegroundProperty, anteil > 1 ? "Pinsel.red" : anteil >= StadtbauPruefung.GelbAb ? "Pinsel.yellow" : "Pinsel.green");
        _grenzText.Text = Texte.EntwurfGrenzText(Dreiecke);
    }

    private void PruefungZeigen()
    {
        _pruefungen.Children.Clear();
        if (Funde.Count == 0)
        {
            _pruefungen.Children.Add(Schrift.Text("✓ " + Texte.EntwurfAllesGut, "Klein", farbe: "Pinsel.green"));
            return;
        }
        foreach (var f in Funde.OrderByDescending(f => f.Rot))
        {
            _pruefungen.Children.Add(new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    Schrift.Text((f.Rot ? "Rot: " : "Gelb: ") + f.Text, "Klein", FontWeight.Bold, f.Rot ? "Pinsel.red" : "Pinsel.orange"),
                    Schrift.Text(f.Tipp, "Klein", farbe: "Pinsel.muted"),
                },
            });
        }
    }

    private Ansicht3D.Einstellung Einstellung => _nacht.IsChecked == true ? new Ansicht3D.Einstellung(true, Lernplattform.Core.Spiel.Jahreszeit.Sommer, null) : Ansicht3D.Einstellung.Standard;

    private void Aktualisieren()
    {
        if (_baut || (_gebaut == _stand && Drei.Spiel is not null))
        {
            return;
        }
        var stand = _stand;
        var entwurf = new Entwurf();
        entwurf.Teile.AddRange(Entwurf.Teile);
        entwurf.Eingaenge.AddRange(Entwurf.Eingaenge);
        var ebene = Ebene;
        var einstellung = Einstellung;
        var daten = _daten;
        _baut = true;
        _ = Task.Run(() => EntwurfAnsicht3D.Bauen(entwurf, ebene, daten, einstellung, _vorrat)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            _baut = false;
            if (t.IsFaulted)
            {
                _status.Text = Texte.Ansicht3DFehler;
                _gebaut = stand;
                _fertig.TrySetResult();
                return;
            }
            var alt = Drei.AktuelleKamera;
            var (ax, az, ab, ad, an) = alt is null ? (float.NaN, 0f, 0f, 0f, 0f) : (alt.ZielX, alt.ZielZ, alt.Abstand, alt.Drehung, alt.Neigung);
            Tageslicht.FesteUhr = einstellung.Uhr;
            Drei.Spiel = t.Result;
            if (Drei.AktuelleKamera is { } neu)
            {
                if (!float.IsNaN(ax))
                {
                    // die Kamera bleibt, wo sie war
                    neu.Setzen(ax, az);
                    neu.Zoomen(neu.Abstand / ab);
                    neu.Drehung = ad;
                    neu.Neigung = an;
                }
                else
                {
                    GanzZeigen();
                }
            }
            _gebaut = stand;
            _status.Text = "";
            _wartetAufBild = true;
            DispatcherTimer.RunOnce(OhneGrafikPruefen, TimeSpan.FromSeconds(1));
            Drei.AnfordernBild();
            if (_gebaut != _stand)
            {
                Aktualisieren();
            }
        }), TaskScheduler.Default);
    }

    private void OhneGrafikPruefen()
    {
        if (_wartetAufBild && !Drei.GlBereit)
        {
            _wartetAufBild = false;
            _status.Text = Texte.Ansicht3DOhneGrafik;
            _fertig.TrySetResult();
        }
    }

    private void BildGezeichnet() => Dispatcher.UIThread.Post(() =>
    {
        if (!_wartetAufBild)
        {
            return;
        }
        _wartetAufBild = false;
        ZeitMs = Stopwatch.GetElapsedTime(_aenderung).TotalMilliseconds;
        Messwerte.Reihe("Editor bis zum Bild (Angabe)", ZeitMs);
        if (_gebaut == _stand)
        {
            _fertig.TrySetResult();
        }
    });

    /// <summary>Zeitpunkt fuer die naechste Messung „bis zum Bild“ neu setzen (Start-Test: Oeffnen).</summary>
    public void MessungAb(long zeit) => _aenderung = zeit;

    public void GanzZeigen()
    {
        if (Drei.AktuelleKamera is { } k)
        {
            var (x, z, abstand) = EntwurfAnsicht3D.GanzZeigen(Entwurf);
            k.Setzen(x, z);
            k.Zoomen(k.Abstand / abstand);
            k.Neigung = 48;
            Drei.AnfordernBild();
        }
    }

    // ---- Schritte ueber die Ansicht ---------------------------------------------------------------

    /// <summary>Das gewaehlte Teil an das Feld der aktuellen Ebene setzen; Rueckgabe: neue Stelle oder -1.</summary>
    public int SetzenAn(int x, int y)
    {
        if (SetzenTeil is not { } id)
        {
            return -1;
        }
        var i = Entwurf.Setzen(id, x, y, Ebene, SetzenDreh);
        if (i >= 0)
        {
            Auswahl = i;
            AuswahlZeigen();
        }
        return i;
    }

    /// <summary>Tasten des Editors (Hauptansicht gibt sie weiter): R dreht, Entf loescht, Pfeile schieben, Esc hoert auf zu setzen,
    /// G blendet das Raster ein oder aus.</summary>
    public bool Taste(Avalonia.Input.Key taste)
    {
        switch (taste)
        {
            case Avalonia.Input.Key.Escape when SetzenTeil is not null:
                TeilWaehlen(null);
                return true;
            case Avalonia.Input.Key.R when SetzenTeil is not null:
                SetzenDreh = (SetzenDreh + 1) % 4;
                Bedienung.StandGeaendert();
                return true;
            case Avalonia.Input.Key.R when Auswahl >= 0:
                Entwurf.Drehen(Auswahl);
                return true;
            case Avalonia.Input.Key.Delete or Avalonia.Input.Key.Back when Auswahl >= 0:
                Entwurf.Loeschen(Auswahl);
                Waehlen(-1);
                return true;
            case Avalonia.Input.Key.Left when Auswahl >= 0:
                return Entwurf.Verschieben(Auswahl, -1, 0);
            case Avalonia.Input.Key.Right when Auswahl >= 0:
                return Entwurf.Verschieben(Auswahl, 1, 0);
            case Avalonia.Input.Key.Up when Auswahl >= 0:
                return Entwurf.Verschieben(Auswahl, 0, -1);
            case Avalonia.Input.Key.Down when Auswahl >= 0:
                return Entwurf.Verschieben(Auswahl, 0, 1);
            case Avalonia.Input.Key.G:
                RasterUmschalten();
                return true;
            case Avalonia.Input.Key.PageUp:
                Bedienung.Kippen(8);
                return true;
            case Avalonia.Input.Key.PageDown:
                Bedienung.Kippen(-8);
                return true;
            default:
                return false;
        }
    }

    public void Melden(string text) => _melden(text);
}
