using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// A 0.76.0, Welle 1 "Prüfung im echten Format" (Weisung P12-262, Plan P12-152): Karte in der Pruefungsauswahl mit
/// eigener Ergebnisliste (Antwort 2a: zaehlt nie ins Gesamtergebnis), Lauf mit Uhr ohne Pause, Reitern fuer
/// Ausgangssituation, Aufgaben 1 bis 4 und Zeitfrage, Umschalter Aufgabe/Anlagen, Antwortformen Zeilen, Angaben,
/// Rechnen, Tabelle, Lueckenfeld und Auswahl, Bewertungsseite (gruen/rot vom Programm, Haekchen selbst) und Ergebnis
/// "Nach Aufgaben". Ein Aufbau fuer PC und Handy (Pillen umbrechen, Tabellen als Zeilenbloecke). Zeichnet in den
/// Koerper der Pruefungsseite (<see cref="KlausurTeil"/>), der Ablauf ohne Oberflaeche ist <see cref="EchtAblauf"/>.
/// A 0.77.0 (Welle 2, Weisung P12-265): gezeichnete Anlagen (Netzplan, Monitor-Bild) mit Textfassung
/// (<see cref="EchtBilder"/>), Zeichenaufgaben Stufe 1 (auf Papier, Vorgabe sichtbar, Musterbild mit Haekchen) und am
/// breiten PC zwei Spalten im Lauf (links die Aufgabe, rechts ihre Anlagen; Umschalter nur, wenn es schmal ist).
/// </summary>
public sealed class EchtFormatTeil
{
    private const string Ausgang = "ausgang";
    private const string Zeit = "zeit";

    /// <summary>Ab dieser Breite des Koerpers (mal Schriftfaktor) stehen Aufgabe und Anlagen nebeneinander.</summary>
    internal const double ZweiSpaltenAb = 1000;

    /// <summary>Schluessel der Antwort "Auf Papier gezeichnet" einer Zeichenaufgabe.</summary>
    internal const string Papier = "papier";

    private readonly StackPanel _koerper;
    private readonly Action _leeren;
    private readonly Action _zurAuswahl;
    private readonly Func<bool> _andereLaeuft;
    private readonly EchtAblauf _a;
    private readonly DispatcherTimer _takt = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _speichern = new() { Interval = TimeSpan.FromMilliseconds(1200) };
    private readonly Dictionary<string, TextBox> _felder = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EchtBaukastenTeil> _baukaesten = new(StringComparer.Ordinal);
    // A 1.12.0 (Weisung P12-390): Struktogramm-Baukaesten im Lauf (gleiche Antwort <aufgabe>.<nr>/bau)
    private readonly Dictionary<string, EchtStruktogrammTeil> _struktogramme = new(StringComparer.Ordinal);
    private readonly HashSet<string> _bauOffen = new(StringComparer.Ordinal);
    // A 1.20.0 (W2, Weisung P12-446): Schreibtischtests im Lauf, ihre Code-Anlagen (Markierung) und der CSV-Umschalter je Anlage
    private readonly Dictionary<string, EchtSchreibtischTeil> _schreibtische = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CodeBlatt> _codeBlaetter = new(StringComparer.Ordinal);

    /// <summary>A 1.20.0: zuletzt gewaehlte Code-Zeile je Code-Anlage (Zeile, Schritt ab 0); bleibt beim Wechsel zu "Anlagen" erhalten.</summary>
    private readonly Dictionary<string, (int Zeile, int Schritt)> _codeMarke = new(StringComparer.Ordinal);

    /// <summary>Lauf, zu dem <see cref="_codeMarke"/> gehoert (ein anderer Lauf beginnt ohne Markierung).</summary>
    private JsonObject? _codeMarkeLauf;
    private readonly Dictionary<string, bool> _alsTabelle = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _bauGewaehlt = new(StringComparer.Ordinal);
    private JsonObject? _bauZustand;
    private EchtBestand? _probeBestand;
    private string _reiter = Ausgang;
    private bool _anlagen;
    private bool _abgeben;
    private TextBlock? _uhr;
    private TextBlock? _fortschritt;
    private TextBlock? _summe;
    private StackPanel? _inhalt;
    private Pillen? _reiterPillen;
    private Pillen? _umschalter;
    private bool _zweiSpalten;
    private bool _alsText;

    public EchtFormatTeil(StackPanel koerper, Action leeren, Action zurAuswahl, Func<bool> andereLaeuft)
    {
        _koerper = koerper;
        _leeren = leeren;
        _zurAuswahl = zurAuswahl;
        _andereLaeuft = andereLaeuft;
        _a = new EchtAblauf(() => _probeBestand ?? Zustand.EchtBestand,
            new EchtAblage(() => Zustand.Einstellungen.Laden(), Zustand.Aendere), () => Zustand.Schreiber,
            () => DateTimeOffset.Now, TimeZoneInfo.Local);
        _takt.Tick += (_, _) => Ticken();
        _speichern.Tick += (_, _) => Festhalten();
        _koerper.SizeChanged += (_, _) =>
        {
            // Breite geaendert (Fenster, Drehen): zwei Spalten an oder aus, nur im Lauf
            if (Aktiv && !Bewertet && _inhalt is not null && _umschalter is not null && _a.Satz is not null && ZweiSpalten != _zweiSpalten)
            {
                Festhalten();
                LaufMalen();
            }
        };
        Zustand.EchtVorab();
    }

    /// <summary>Gespeicherte Pruefung im echten Format (auch in der Bewertung)?</summary>
    public bool Laeuft => _a.Laeuft;

    /// <summary>Wird gerade eine Pruefung im echten Format gezeigt (Lauf, Bewertung, Ergebnis)?</summary>
    public bool Aktiv { get; private set; }

    /// <summary>Bewertung wird gezeigt.</summary>
    public bool Bewertet { get; private set; }

    /// <summary>Vom Koerper geleert (KlausurTeil.Leeren): Uhr und Felder los.</summary>
    public void Geleert()
    {
        Aktiv = false;
        Bewertet = false;
        _takt.Stop();
        _speichern.Stop();
        _felder.Clear();
        _baukaesten.Clear();
        _struktogramme.Clear();
        _schreibtische.Clear();
        _codeBlaetter.Clear();
        AnlageGross.Schliessen();
        _uhr = _fortschritt = _summe = null;
        _inhalt = null;
        _reiterPillen = _umschalter = null;
    }

    /// <summary>Gespeicherte Pruefung fortsetzen; false = keine (die Auswahl zeigt KlausurTeil).</summary>
    public bool Zeigen()
    {
        _probeBestand = null;
        if (!_a.Laeuft)
        {
            return false;
        }
        _ = SchrittAsync(_a.Zeigen());
        return true;
    }

    public void Verbergen()
    {
        AnlageGross.Schliessen();
        Festhalten();
        _takt.Stop();
    }

    private async Task SchrittAsync(EchtSchritt schritt)
    {
        if (schritt.Hinweis == TrainerTexte.NichtGespeichert && Bewertet)
        {
            await Dialog.Zeigen(schritt.HinweisTitel!, schritt.Hinweis, Dialog.Ok);
            return;
        }
        if (schritt.Ansicht == EchtAnsicht.Bewertung && schritt.Hinweis is { } vorher)
        {
            await Dialog.Zeigen(schritt.HinweisTitel!, vorher, Dialog.Ok);
        }
        switch (schritt.Ansicht)
        {
            case EchtAnsicht.Auswahl:
                _zurAuswahl();
                break;
            case EchtAnsicht.Lauf:
                _reiter = Ausgang;
                _anlagen = false;
                LaufZeigen();
                break;
            case EchtAnsicht.Bewertung:
                _reiter = "1";
                BewertungZeigen();
                break;
            case EchtAnsicht.Ergebnis:
                if (!_a.Probe)
                {
                    AbgleichDienst.NachDemLernen();
                }
                ErgebnisZeigen(_a.ErgebnisArt!, _a.Ergebnis!);
                break;
        }
        if (schritt.Ansicht != EchtAnsicht.Bewertung && schritt.Hinweis is { } nachher)
        {
            await Dialog.Zeigen(schritt.HinweisTitel!, nachher, Dialog.Ok);
        }
    }

    private Pruefungsbereich Bereich => EchtArten.Bereich(_a.Art)!;

    /// <summary>Aufgabe und Anlagen nebeneinander: breiter Koerper und Schrift unter der Stufe "untereinander".</summary>
    private bool ZweiSpalten => Schrift.Faktor < Schrift.UntereinanderAb && _koerper.Bounds.Width >= ZweiSpaltenAb * Schrift.Faktor;

    private string BereichName => Bereich.Name + EchtTexte.NameZusatz;

    // -- Auswahl (Karte in der Pruefungsauswahl) -------------------------------------------------

    /// <summary>
    /// Karten "Prüfung im echten Format" und "Ergebnisse im echten Format" fuer die Auswahl: je Bereich (AP1 fuer alle,
    /// b1/b2 der Fachrichtung) eine Startzeile oder der Platzhalter, jede Meldung der Ladepruefung sichtbar.
    /// </summary>
    public IEnumerable<Control> AuswahlKarten(Fachrichtung fachrichtung, List<KlausurEintrag> verlauf)
    {
        var bestand = _probeBestand ?? Zustand.EchtBestand;
        var inhalt = new StackPanel { Spacing = 6 };
        inhalt.Children.Add(new LernText(EchtTexte.Einleitung, "Klein", FontWeight.Normal, "Pinsel.text_soft"));
        var eigene = EchtArten.Nur(verlauf);
        foreach (var rolle in new[] { EchtSaetze.Ap1, EchtSaetze.B1, EchtSaetze.B2 })
        {
            inhalt.Children.Add(Startzeile(fachrichtung, rolle, bestand, eigene));
        }
        yield return Schrift.KopfKarte(EchtTexte.KarteTitel, EchtTexte.KarteUntertitel, inhalt);
        yield return ErgebnisKarte(eigene);
    }

    private Border Startzeile(Fachrichtung fachrichtung, string rolle, EchtBestand bestand, List<KlausurEintrag> eigene)
    {
        var bereich = fachrichtung.Bereich(rolle);
        var art = EchtArten.Art(fachrichtung, rolle);
        var saetze = bestand.Fuer(rolle, fachrichtung.Id);
        var text = new StackPanel { Spacing = 2, Children = { new LernText(bereich.Name, "Normal", FontWeight.Bold, "Pinsel.text") } };
        foreach (var fehler in bestand.FehlerFuer(rolle, fachrichtung.Id))
        {
            text.Children.Add(new LernText(Beschriftungen.Einsetzen(EchtTexte.FehlerHinweis, fehler.Kennung + ": " + fehler.Meldung),
                "Klein", FontWeight.Normal, "Pinsel.red"));
        }
        if (saetze.Count == 0)
        {
            text.Children.Add(new LernText(EchtTexte.KeinSatz, "Klein", FontWeight.Normal, "Pinsel.text_dim"));
            return Zeile(text);
        }
        var bearbeitet = saetze.Count(s => eigene.Any(e => e.Art == art && e.Daten is JsonObject d && Json.Text(d["satz"]) == s.Kennung));
        text.Children.Add(Schrift.Text(Beschriftungen.Einsetzen(EchtTexte.Angabe, saetze[0].Minuten, saetze.Count, bearbeitet), "Klein",
            FontWeight.Normal, "Pinsel.muted"));
        var knopf = LernTeile.Knopf(Beschriftungen.Einsetzen(EchtTexte.Starten, bereich.Kurz), true);
        knopf.VerticalAlignment = VerticalAlignment.Center;
        knopf.Click += async (_, _) => await StartenAsync(saetze, art, bereich);
        return Zeile(new NebenOderUnter(220, 14) { Children = { text, knopf } });
    }

    private static Border ErgebnisKarte(List<KlausurEintrag> eigene)
    {
        var inhalt = new StackPanel { Spacing = 2 };
        if (eigene.Count == 0)
        {
            inhalt.Children.Add(Schrift.Text(EchtTexte.KeinErgebnis, "Klein", FontWeight.Normal, "Pinsel.muted"));
        }
        foreach (var e in eigene.Take(8))
        {
            var punkte = e.Punkte ?? 0;
            var name = EchtArten.Namen(e.Art)?.Kurz ?? e.Art ?? "";
            inhalt.Children.Add(new WrapPanel
            {
                Children =
                {
                    Schrift.Text(Gesamtstand.Datum(e.Zeit), "Klein", FontWeight.Normal, "Pinsel.muted").Breite(150),
                    Schrift.Text(name, "Klein", FontWeight.Bold).Breite(160),
                    Schrift.Text(Beschriftungen.Einsetzen(PruefungTexte.PunkteMuster, Core.Mathe.PythonRunden.PunkteText(punkte)), "Klein",
                        FontWeight.Normal, "Pinsel.text_soft").Breite(110),
                    Schrift.Text(e.Note ?? "", "Klein", FontWeight.Bold, LernTeile.Pinsel(Gesamtstand.Notenfarbe(punkte))),
                },
            });
        }
        return Schrift.KopfKarte(EchtTexte.Ergebnisse, Beschriftungen.Einsetzen(PruefungTexte.Insgesamt, eigene.Count), inhalt);
    }

    /// <summary>
    /// Welle A 1.15.0 (Entwurf Variante B, Reiter „Prüfen“): Satz eines Teils starten, <paramref name="satz"/> = null der naechste
    /// wie „Starten“ hier, sonst der gewaehlte („Satz wählen“). Gleiche Rueckfrage und gleicher Ablauf wie bisher.
    /// </summary>
    internal Task SatzStartenAsync(Fachrichtung fachrichtung, string rolle, EchtSatz? satz)
    {
        var saetze = (_probeBestand ?? Zustand.EchtBestand).Fuer(rolle, fachrichtung.Id);
        return saetze.Count == 0 ? Task.CompletedTask : StartenAsync(saetze, EchtArten.Art(fachrichtung, rolle), fachrichtung.Bereich(rolle), satz);
    }

    private async Task StartenAsync(IReadOnlyList<EchtSatz> saetze, string art, Pruefungsbereich bereich, EchtSatz? gewaehlt = null)
    {
        if (_andereLaeuft())
        {
            await Dialog.Zeigen(KartenTexte.HinweisTitel, EchtTexte.AndereLaeuft, Dialog.Ok);
            return;
        }
        if (_a.Laeuft)
        {
            Zeigen();
            return;
        }
        var satz = gewaehlt ?? EchtAblauf.NaechsterSatz(saetze, art, Gesamtstand.Verlauf(Zustand.Lernstand))!;
        var frage = Beschriftungen.Einsetzen(EchtTexte.StartFrage, bereich.Name, satz.Firma, satz.Minuten);
        if (await Dialog.Zeigen(PruefungTexte.StartTitel, frage, TrainerTexte.Ja, TrainerTexte.Nein) != 0 || _andereLaeuft())
        {
            return;
        }
        await SchrittAsync(_a.Starten(satz, art));
    }

    // -- Lauf -------------------------------------------------------------------------------------

    private void LaufZeigen()
    {
        _leeren();
        Aktiv = true;
        var satz = _a.Satz!;
        _uhr = Schrift.Text("", "Zahl", FontWeight.Bold, "Pinsel.accent");
        var verbleibend = Schrift.Text(PruefungTexte.Verbleibend, "Klein", FontWeight.Normal, "Pinsel.muted");
        verbleibend.VerticalAlignment = VerticalAlignment.Bottom;
        verbleibend.Margin = new Thickness(10, 0, 0, 6);
        var abgeben = LernTeile.Knopf(PruefungTexte.Abgeben, true);
        abgeben.Click += async (_, _) => await AbgebenAsync(auto: false);
        var abbrechen = LernTeile.Knopf(PruefungTexte.Abbrechen);
        abbrechen.Click += async (_, _) => await AbbrechenAsync();
        _fortschritt = Schrift.Text("", "Klein", FontWeight.Normal, "Pinsel.muted");
        var kopf = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new WrapPanel { Children = { new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 16, 8),
                    Children = { _uhr, verbleibend } }, abgeben, abbrechen } },
                _fortschritt,
            },
        };
        _koerper.Children.Add(Schrift.KopfKarte(BereichName, satz.Firma, kopf));
        _reiterPillen = new Pillen(Reiter(satz, mitZeitfrage: true), _reiter, w =>
        {
            Festhalten();
            _reiter = w;
            LaufMalen();
            _koerper.Children[0].BringIntoView();
        });
        _koerper.Children.Add(_reiterPillen);
        _umschalter = new Pillen([("a", EchtTexte.AufgabeUmschalter), ("n", UmschalterText(satz, satz.Aufgaben[0]))],
            _anlagen ? "n" : "a", w =>
            {
                Festhalten();
                _anlagen = w == "n";
                LaufMalen();
            });
        _koerper.Children.Add(_umschalter);
        _inhalt = new StackPanel { Spacing = 14 };
        _koerper.Children.Add(_inhalt);
        LaufMalen();
        Ticken();
        if (_a.ImLauf && !_a.Probe)
        {
            _takt.Start();
        }
    }

    private static IEnumerable<(string, string)> Reiter(EchtSatz satz, bool mitZeitfrage)
    {
        if (mitZeitfrage)
        {
            yield return (Ausgang, EchtTexte.Ausgangssituation);
        }
        for (var n = 0; n < satz.Aufgaben.Count; n++)
        {
            yield return (Nummer(n + 1), Beschriftungen.Einsetzen(EchtTexte.AufgabeKurz, n + 1, satz.Aufgaben[n].Punkte));
        }
        if (mitZeitfrage)
        {
            yield return (Zeit, EchtTexte.Zeitfrage);
        }
    }

    private static string Nummer(int n) => n.ToString(CultureInfo.InvariantCulture);

    private void LaufMalen()
    {
        var satz = _a.Satz!;
        var zustand = _a.Zustand!;
        _inhalt!.Children.Clear();
        _felder.Clear();
        _baukaesten.Clear();
        _struktogramme.Clear();
        _schreibtische.Clear();
        _codeBlaetter.Clear();
        _zweiSpalten = ZweiSpalten;
        var aufgabeReiter = _reiter is not (Ausgang or Zeit);
        var mitAnlagen = aufgabeReiter && satz.Aufgaben[int.Parse(_reiter, CultureInfo.InvariantCulture) - 1].Teile.Any(t => t.Anlagen.Count > 0);
        // Sammelliste 456: eine Aufgabe mit Baukasten nutzt im breiten Fenster die volle Breite, ihre Anlagen stehen darunter
        var mitBaukasten = aufgabeReiter && _a.ImLauf && EchtAnlagenAnzeige.VolleBreite(satz.Aufgaben[int.Parse(_reiter, CultureInfo.InvariantCulture) - 1]);
        _umschalter!.IsVisible = aufgabeReiter && !(_zweiSpalten && mitAnlagen && !mitBaukasten);
        if (_reiter == Ausgang)
        {
            var text = new StackPanel { Spacing = 8 };
            foreach (var zeile in satz.Ausgangssituation)
            {
                text.Children.Add(new LernText(zeile, "Normal", FontWeight.Normal, "Pinsel.text_soft"));
            }
            text.Children.Add(Abstand(new LernText(EchtTexte.Regeln, "Klein", FontWeight.Normal, "Pinsel.muted"), 6));
            _inhalt.Children.Add(Schrift.KopfKarte(EchtTexte.Ausgangssituation, satz.Firma, text));
        }
        else if (_reiter == Zeit)
        {
            var liste = new StackPanel { Spacing = 6 };
            liste.Children.Add(new LernText(satz.Zeitfrage.Text, "Normal", FontWeight.Bold, "Pinsel.text"));
            for (var i = 0; i < satz.Zeitfrage.Optionen.Count; i++)
            {
                var nr = i;
                liste.Children.Add(Option(satz.Zeitfrage.Optionen[i], "zeitfrage", _a.ZeitfrageWahl == i, an =>
                {
                    if (an)
                    {
                        _a.Zeitfrage(nr);
                    }
                }, radio: true));
            }
            _inhalt.Children.Add(Schrift.KopfKarte(EchtTexte.Zeitfrage, EchtTexte.ZeitfrageUntertitel, liste));
        }
        else
        {
            var n = int.Parse(_reiter, CultureInfo.InvariantCulture);
            var aufgabe = satz.Aufgaben[n - 1];
            // Sammelliste 363: die Zahl im Umschalter geht mit der Aufgabe mit
            _umschalter.Beschriften("n", UmschalterText(satz, aufgabe));
            Border AufgabeKarte()
            {
                var inhalt = new StackPanel { Spacing = 6 };
                foreach (var zeile in aufgabe.Einleitung)
                {
                    inhalt.Children.Add(new TabellenText(zeile, "Normal", FontWeight.Normal, "Pinsel.text_soft"));
                }
                foreach (var teil in aufgabe.Teile)
                {
                    inhalt.Children.Add(Abstand(Teilkopf(teil), 14));
                    inhalt.Children.Add(Antwortfelder(n, teil, zustand));
                }
                return Schrift.KopfKarte(Beschriftungen.Einsetzen(EchtTexte.AufgabeTitel, n, aufgabe.Titel), EchtTexte.PunkteText(aufgabe.Punkte), inhalt);
            }
            if (_zweiSpalten && mitAnlagen && mitBaukasten && !_anlagen)
            {
                // Sammelliste 456 (Nico 2b): "Aufgabe" = volle Breite, darunter ihre Anlagen; "Anlagen" = Anlagen daneben wie bisher
                _inhalt.Children.Add(AufgabeKarte());
                foreach (var anlage in AnlagenVon(satz, aufgabe))
                {
                    _inhalt.Children.Add(LaufAnlage(anlage, aufgabe, n));
                }
            }
            else if (_zweiSpalten && mitAnlagen)
            {
                // breiter PC: links die Aufgabe, rechts ihre Anlagen (Plan P12-152 Abschnitt 3)
                var rechts = new StackPanel { Spacing = 14 };
                foreach (var anlage in AnlagenVon(satz, aufgabe))
                {
                    rechts.Children.Add(LaufAnlage(anlage, aufgabe, n));
                }
                var spalten = new Grid { ColumnDefinitions = new ColumnDefinitions("11*,14,9*") };
                var links = AufgabeKarte();
                Grid.SetColumn(rechts, 2);
                spalten.Children.Add(links);
                spalten.Children.Add(rechts);
                _inhalt.Children.Add(spalten);
            }
            else if (_anlagen)
            {
                foreach (var anlage in AnlagenVon(satz, aufgabe))
                {
                    _inhalt.Children.Add(LaufAnlage(anlage, aufgabe, n));
                }
            }
            else
            {
                _inhalt.Children.Add(AufgabeKarte());
            }
        }
        FortschrittZeigen();
    }

    /// <summary>
    /// Welle A 1.15.0 (Entwurf Variante B, „Einzelne Aufgabe im echten Format“): Aufgabe n eines Satzes zum Lesen, ohne Uhr
    /// und ohne Note: Ausgangssituation, die Aufgabe mit ihren Teilen, mit <paramref name="loesung"/> unter jedem Teil die
    /// Musterloesung (Text und Musterbild), danach die Anlagen der Aufgabe. Nichts wird gespeichert.
    /// </summary>
    internal static IEnumerable<Control> Einzelaufgabe(EchtSatz satz, int n, bool loesung)
    {
        var ausgang = new StackPanel { Spacing = 8 };
        foreach (var zeile in satz.Ausgangssituation)
        {
            ausgang.Children.Add(new LernText(zeile, "Normal", FontWeight.Normal, "Pinsel.text_soft"));
        }
        yield return Schrift.KopfKarte(EchtTexte.Ausgangssituation, satz.Firma, ausgang);
        var aufgabe = satz.Aufgaben[n - 1];
        var inhalt = new StackPanel { Spacing = 6 };
        foreach (var zeile in aufgabe.Einleitung)
        {
            inhalt.Children.Add(new TabellenText(zeile, "Normal", FontWeight.Normal, "Pinsel.text_soft"));
        }
        foreach (var teil in aufgabe.Teile)
        {
            inhalt.Children.Add(Abstand(Teilkopf(teil), 14));
            if (!loesung)
            {
                continue;
            }
            inhalt.Children.Add(Abstand(LernTeile.Label(EchtTexte.Loesung), 8));
            foreach (var zeile in teil.Loesung)
            {
                inhalt.Children.Add(new TabellenText(zeile, "Klein", FontWeight.Normal, "Pinsel.text_soft"));
            }
            if (teil.Antwort.Struktogramm is { } struktur)
            {
                inhalt.Children.Add(Abstand(LernTeile.Label(EchtTexte.Musterbild), 8));
                inhalt.Children.Add(EchtBilder.Struktogramm(struktur, EchtTexte.Musterbild + ": " + EchtTexte.StruktogrammName));
            }
            if (teil.Antwort.Muster is { } muster)
            {
                inhalt.Children.Add(Abstand(LernTeile.Label(EchtTexte.Musterbild), 8));
                inhalt.Children.Add(EchtBilder.Diagramm(muster, EchtTexte.Musterbild + ": " + EchtZeichnung.Name(muster.Art)));
            }
        }
        yield return Schrift.KopfKarte(Beschriftungen.Einsetzen(EchtTexte.AufgabeTitel, n, aufgabe.Titel), EchtTexte.PunkteText(aufgabe.Punkte), inhalt);
        foreach (var anlage in AnlagenVon(satz, aufgabe))
        {
            yield return AnlageKarte(anlage, false);
        }
    }

    /// <summary>Anlagen, auf die die Aufgabe verweist (alle, wenn sie auf keine verweist); Rechnung in <see cref="EchtAnlagenAnzeige.Von"/>.</summary>
    private static IReadOnlyList<EchtAnlage> AnlagenVon(EchtSatz satz, EchtAufgabe aufgabe) => EchtAnlagenAnzeige.Von(satz, aufgabe);

    /// <summary>Beschriftung des Umschalters "Anlagen (n)" fuer eine Aufgabe (Sammelliste 363: Zahl der gezeigten Anlagen).</summary>
    internal static string UmschalterText(EchtSatz satz, EchtAufgabe aufgabe) =>
        Beschriftungen.Einsetzen(EchtTexte.Anlagen, EchtAnlagenAnzeige.UmschalterZahl(satz, aufgabe));

    /// <summary>
    /// A 1.20.0: Anlage im Lauf. Code eines Schreibtischtests dieser Aufgabe mit Zeilennummern am Rand und Markierung, Ausgabe
    /// und Datenauszug mit "Groß öffnen" (Nico 3a: nur diese beiden), der CSV-Umschalter bleibt je Anlage gemerkt.
    /// </summary>
    private Border LaufAnlage(EchtAnlage anlage, EchtAufgabe aufgabe, int n)
    {
        var titel = Beschriftungen.Einsetzen(EchtTexte.Anlage, anlage.Nr, anlage.Titel);
        if (anlage.Art == "code" && aufgabe.Teile.Any(t => t.Antwort.Form == EchtSaetze.FormSchreibtischtest && t.Antwort.Code == anlage.Nr))
        {
            var blatt = EchtAnlagenBlatt.Code(anlage);
            _codeBlaetter[anlage.Nr] = blatt;
            if (ReferenceEquals(_codeMarkeLauf, _a.Zustand) && _codeMarke.TryGetValue(anlage.Nr, out var marke))
            {
                blatt.Markieren(marke.Zeile, marke.Schritt);
            }
            return Schrift.KopfKarte(titel, null, blatt);
        }
        return AnlageKarte(anlage, _alsText, a => GrossOeffnen(a, n), _alsTabelle);
    }

    /// <summary>Grosses Blatt einer Ausgabe oder eines Datenauszugs (eigener Inhalt, die Karte bleibt stehen).</summary>
    private void GrossOeffnen(EchtAnlage anlage, int aufgabe)
    {
        var titel = Beschriftungen.Einsetzen(EchtTexte.Anlage, anlage.Nr, anlage.Titel);
        if (anlage.Art == EchtDaten.Ausgabe)
        {
            AnlageGross.Oeffnen(titel, EchtAnlagenBlatt.AusgabeKasten(anlage), null, aufgabe);
            return;
        }
        var inhalt = new ContentControl { Content = EchtAnlagenBlatt.DatenInhalt(anlage, _alsTabelle.GetValueOrDefault(anlage.Nr)) };
        var umschalter = anlage.DatenFormat == EchtDaten.Csv
            ? EchtAnlagenBlatt.Umschalter(_alsTabelle.GetValueOrDefault(anlage.Nr), an => inhalt.Content = EchtAnlagenBlatt.DatenInhalt(anlage, an))
            : null;
        AnlageGross.Oeffnen(titel, inhalt, umschalter, aufgabe);
    }

    private static Border AnlageKarte(EchtAnlage anlage, bool alsText, Action<EchtAnlage>? gross = null, Dictionary<string, bool>? alsTabelle = null)
    {
        var inhalt = new StackPanel { Spacing = 6 };
        if (EchtBilder.Anlage(anlage, alsText) is { } bild)
        {
            return Schrift.KopfKarte(Beschriftungen.Einsetzen(EchtTexte.Anlage, anlage.Nr, anlage.Titel), null, bild);
        }
        switch (anlage.Art)
        {
            case EchtDaten.Ausgabe:
                inhalt.Children.Add(EchtAnlagenBlatt.Ausgabe(anlage, gross));
                break;
            case EchtDaten.Daten:
                inhalt.Children.Add(EchtAnlagenBlatt.Daten(anlage, alsTabelle?.GetValueOrDefault(anlage.Nr) ?? false, an =>
                {
                    if (alsTabelle is not null)
                    {
                        alsTabelle[anlage.Nr] = an;
                    }
                }, gross));
                break;
            case "code":
                inhalt.Children.Add(new Tabellenkasten(string.Join("\n", anlage.Zeilen), "Klein", FontWeight.Normal, "Pinsel.text"));
                break;
            case "tabelle":
                inhalt.Children.Add(new Tabellenkasten(Tabelle(anlage.Spalten, anlage.Tabelle), "Klein", FontWeight.Normal, "Pinsel.text"));
                break;
            default:
                foreach (var zeile in anlage.Zeilen)
                {
                    inhalt.Children.Add(new LernText(zeile, "Normal", FontWeight.Normal, "Pinsel.text_soft"));
                }
                break;
        }
        return Schrift.KopfKarte(Beschriftungen.Einsetzen(EchtTexte.Anlage, anlage.Nr, anlage.Titel), null, inhalt);
    }

    /// <summary>Tabelle in fester Zeichenbreite: Spalten mit " | " getrennt, Kopf unterstrichen.</summary>
    internal static string Tabelle(IReadOnlyList<string> spalten, IReadOnlyList<IReadOnlyList<string>> zeilen)
    {
        var breite = spalten.Select((s, i) => Math.Max(s.Length, zeilen.Max(z => z[i].Length))).ToList();
        string Zeile(IReadOnlyList<string> zellen) => string.Join(" | ", zellen.Select((z, i) => z.PadRight(breite[i]))).TrimEnd();
        var strich = string.Join("-+-", breite.Select(b => new string('-', b)));
        return string.Join("\n", new[] { Zeile(spalten), strich }.Concat(zeilen.Select(Zeile)));
    }

    private static Control Teilkopf(EchtTeil teil)
    {
        var kopf = new StackPanel { Spacing = 4 };
        kopf.Children.Add(Schrift.Text(Beschriftungen.Einsetzen(EchtTexte.HinweisPunkte, Beschriftungen.Einsetzen(EchtTexte.Teil, teil.Nr),
            EchtTexte.PunkteText(teil.Punkte)), "Normal", FontWeight.Bold, "Pinsel.accent"));
        foreach (var zeile in teil.Text)
        {
            kopf.Children.Add(new TabellenText(zeile, "Normal", FontWeight.Normal, "Pinsel.text"));
        }
        if (teil.ErsatzText is { } ersatz)
        {
            kopf.Children.Add(new LernText(ersatz, "Klein", FontWeight.Normal, "Pinsel.muted"));
        }
        if (teil.Englisch)
        {
            kopf.Children.Add(new LernText(EchtTexte.Englisch, "Klein", FontWeight.Normal, "Pinsel.muted"));
        }
        if (teil.Antwort.Form == EchtSaetze.FormAngaben && teil.Operator == "nennen")
        {
            kopf.Children.Add(new LernText(Beschriftungen.Einsetzen(EchtTexte.NurErste, Nummer(teil.Angaben)), "Klein", FontWeight.Normal,
                "Pinsel.muted"));
        }
        if (teil.Antwort.Form == EchtSaetze.FormAuswahl)
        {
            kopf.Children.Add(new LernText(EchtTexte.Mehrfach, "Klein", FontWeight.Normal, "Pinsel.muted"));
        }
        return kopf;
    }

    private Control Antwortfelder(int n, EchtTeil teil, JsonObject zustand)
    {
        var a = teil.Antwort;
        var feld = new StackPanel { Spacing = 6 };
        switch (a.Form)
        {
            case EchtSaetze.FormZeilen:
                feld.Children.Add(Eingabe(EchtBewertung.Feld(n, teil.Nr, EchtBewertung.Text), EchtTexte.Antwort, zustand, a.Zeilen));
                break;
            case EchtSaetze.FormAngaben:
                for (var i = 1; i <= teil.Angaben; i++)
                {
                    var name = Beschriftungen.Einsetzen(EchtTexte.AngabeFeld, i);
                    feld.Children.Add(LernTeile.Label(name));
                    feld.Children.Add(Eingabe(EchtBewertung.Feld(n, teil.Nr, EchtBewertung.Angabe(i)), name, zustand, teil.Operator == "nennen" ? 1 : 2));
                }
                break;
            case EchtSaetze.FormRechnen:
                feld.Children.Add(LernTeile.Label(EchtTexte.Rechenweg));
                feld.Children.Add(Eingabe(EchtBewertung.Feld(n, teil.Nr, EchtBewertung.Weg), EchtTexte.Rechenweg, zustand, 4));
                var ergebnis = Kurzfeld(EchtBewertung.Feld(n, teil.Nr, EchtBewertung.Ergebnis), ErgebnisName(a), zustand, 180);
                feld.Children.Add(new FeldZeile(Schrift.Text(ErgebnisName(a), "Normal", FontWeight.Bold, "Pinsel.text"), ergebnis));
                break;
            case EchtSaetze.FormTabelle:
                for (var z = 0; z < a.Tabelle.Count; z++)
                {
                    var block = new StackPanel { Spacing = 4 };
                    for (var s = 0; s < a.Tabelle[z].Count; s++)
                    {
                        var zelle = a.Tabelle[z][s];
                        var spalte = Schrift.Text(a.Spalten[s], "Klein", FontWeight.Bold, "Pinsel.muted");
                        if (zelle.Offen)
                        {
                            var name = a.Spalten[s] + " " + Nummer(z + 1);
                            block.Children.Add(new FeldZeile(spalte, Kurzfeld(EchtBewertung.Feld(n, teil.Nr, EchtBewertung.Zelle(z, s)), name, zustand, 200)));
                        }
                        else
                        {
                            block.Children.Add(new FeldZeile(spalte, Schrift.Text(zelle.Fest!, "Normal", FontWeight.Normal, "Pinsel.text_soft")));
                        }
                    }
                    feld.Children.Add(Zeile(block));
                }
                break;
            case EchtSaetze.FormLuecke:
                for (var f = 0; f < a.Felder.Count; f++)
                {
                    var name = a.Felder[f].Name;
                    feld.Children.Add(new FeldZeile(Schrift.Text(name, "Normal", FontWeight.Bold, "Pinsel.text"),
                        Kurzfeld(EchtBewertung.Feld(n, teil.Nr, EchtBewertung.Luecke(f)), name, zustand, 200)));
                }
                break;
            case EchtSaetze.FormAuswahl:
                var gewaehlt = EchtBewertung.Gewaehlt(zustand, n, teil.Nr);
                for (var i = 0; i < a.Optionen.Count; i++)
                {
                    var nr = i;
                    feld.Children.Add(Option(a.Optionen[i], "", gewaehlt.Contains(i), an =>
                    {
                        var neu = EchtBewertung.Gewaehlt(_a.Zustand!, n, teil.Nr);
                        neu.Remove(nr);
                        if (an)
                        {
                            neu.Add(nr);
                        }
                        _a.Festhalten([(EchtBewertung.Feld(n, teil.Nr, EchtBewertung.Wahl), string.Join(",", neu.Order()))]);
                        FortschrittZeigen();
                    }, radio: false));
                }
                break;
            case EchtSaetze.FormZeichnen:
                feld.Children.Add(new LernText(EchtTexte.ZeichnenHinweis, "Klein", FontWeight.Normal, "Pinsel.muted"));
                if (a.Vorgabe is { } vorgabe)
                {
                    feld.Children.Add(LernTeile.Label(EchtTexte.Vorgabe));
                    feld.Children.Add(EchtBilder.Diagramm(vorgabe, EchtTexte.Vorgabe + ": " + EchtZeichnung.Name(a.Diagramm), _alsText));
                }
                var schluessel = EchtBewertung.Feld(n, teil.Nr, Papier);
                feld.Children.Add(Option(EchtTexte.Gezeichnet, "", EchtBewertung.Antwort(zustand, schluessel).Length > 0, an =>
                {
                    _a.Festhalten([(schluessel, an ? TrainerTexte.Ja : "")]);
                    FortschrittZeigen();
                }, radio: false));
                if (_a.ImLauf)
                {
                    feld.Children.Add(Baukasten(n, teil, zustand));
                }
                break;
            case EchtSaetze.FormSchreibtischtest:
                feld.Children.Add(Schreibtisch(n, teil, zustand));
                break;
        }
        return feld;
    }

    /// <summary>
    /// A 1.20.0 (W2, Entwurf P12-267 Bilder 01/02): Raster des Schreibtischtests im Lauf mit Hinweis; jede offene Zelle ist ein
    /// Feld <c>&lt;aufgabe&gt;.&lt;nr&gt;/z{schritt}s{spalte}</c>, der gewaehlte Schritt markiert seine Zeile in der Code-Anlage.
    /// </summary>
    private StackPanel Schreibtisch(int n, EchtTeil teil, JsonObject zustand)
    {
        var a = teil.Antwort;
        var raster = new EchtSchreibtischTeil(a, (z, s) => EchtBewertung.Antwort(zustand, EchtBewertung.Feld(n, teil.Nr, EchtBewertung.Zelle(z, s))),
            (box, z, s) => Merken(box, EchtBewertung.Feld(n, teil.Nr, EchtBewertung.Zelle(z, s)), Avalonia.Automation.AutomationProperties.GetName(box) ?? ""));
        raster.ZeileGewaehlt += (schritt, zeile) =>
        {
            if (!ReferenceEquals(_codeMarkeLauf, _a.Zustand))
            {
                _codeMarke.Clear();
                _codeMarkeLauf = _a.Zustand;
            }
            _codeMarke[a.Code] = (zeile, schritt);
            if (_codeBlaetter.TryGetValue(a.Code, out var blatt))
            {
                blatt.Markieren(zeile, schritt);
            }
        };
        _schreibtische[EchtBewertung.Feld(n, teil.Nr, "")] = raster;
        var hinweis = !a.LeerUnveraendert ? EchtTexte.SchreibtischHinweisVoll
            : _zweiSpalten && !_anlagen ? EchtTexte.SchreibtischHinweisDaneben : EchtTexte.SchreibtischHinweisUnter;
        return new StackPanel { Spacing = 6, Children = { raster, new LernText(hinweis, "Klein", FontWeight.Normal, "Pinsel.muted") } };
    }

    /// <summary>
    /// A 0.78.0: Knopf "Im Programm zeichnen" und der Baukasten der Zeichenaufgabe (offen, wenn schon etwas gezeichnet
    /// ist). Die Zeichnung steht in der Antwort <c>&lt;aufgabe&gt;.&lt;nr&gt;/bau</c>, gespeichert wie jedes Feld.
    /// </summary>
    private StackPanel Baukasten(int n, EchtTeil teil, JsonObject zustand)
    {
        if (!ReferenceEquals(_bauZustand, zustand))
        {
            // andere Pruefung: offene Baukaesten und Wahl gelten nur fuer die Pruefung, in der sie entstanden
            _bauZustand = zustand;
            _bauOffen.Clear();
            _bauGewaehlt.Clear();
        }
        var a = teil.Antwort;
        var schluessel = EchtBewertung.Feld(n, teil.Nr, EchtBaukasten.Feld);
        void Geaendert(bool sofort)
        {
            if (sofort)
            {
                Festhalten();
            }
            else
            {
                _speichern.Stop();
                _speichern.Start();
            }
        }
        Control teilBau;
        bool offen;
        if (a.Struktogramm is not null)
        {
            // A 1.12.0 (Weisung P12-390): Struktogramm-Baukasten
            var strukto = EchtStruktogramm.Laden(EchtBewertung.Antwort(zustand, schluessel));
            var st = new EchtStruktogrammTeil(strukto, Geaendert, _bauGewaehlt.GetValueOrDefault(schluessel), id => _bauGewaehlt[schluessel] = id);
            _struktogramme[schluessel] = st;
            teilBau = st;
            offen = strukto.Eigenes || strukto.Unlesbar || _bauOffen.Contains(schluessel);
        }
        else
        {
            var bau = EchtBaukasten.Laden(a.Diagramm, a.Vorgabe, EchtBewertung.Antwort(zustand, schluessel));
            var bt = new EchtBaukastenTeil(bau, Geaendert, _bauGewaehlt.GetValueOrDefault(schluessel), id => _bauGewaehlt[schluessel] = id);
            _baukaesten[schluessel] = bt;
            teilBau = bt;
            offen = bau.Eigenes || bau.Unlesbar || _bauOffen.Contains(schluessel);
        }
        teilBau.IsVisible = offen;
        var knopf = LernTeile.Knopf(offen ? EchtTexte.BaukastenSchliessen : EchtTexte.BaukastenOeffnen);
        Avalonia.Automation.AutomationProperties.SetName(knopf, offen ? EchtTexte.BaukastenSchliessen : EchtTexte.BaukastenOeffnen);
        knopf.HorizontalAlignment = HorizontalAlignment.Left;
        knopf.Click += (_, _) =>
        {
            teilBau.IsVisible = !teilBau.IsVisible;
            if (teilBau.IsVisible)
            {
                _bauOffen.Add(schluessel);
            }
            else
            {
                _bauOffen.Remove(schluessel);
            }
            var name = teilBau.IsVisible ? EchtTexte.BaukastenSchliessen : EchtTexte.BaukastenOeffnen;
            ((TextBlock)knopf.Content!).Text = name;
            Avalonia.Automation.AutomationProperties.SetName(knopf, name);
        };
        return new StackPanel { Spacing = 8, Margin = new Thickness(0, 6, 0, 0), Children = { knopf, teilBau } };
    }

    private static string ErgebnisName(EchtAntwort a) => a.Einheit.Length > 0 ? EchtTexte.ErgebnisFeld + " (" + a.Einheit + ")" : EchtTexte.ErgebnisFeld;

    private TextBox Eingabe(string schluessel, string name, JsonObject zustand, int zeilen)
    {
        var feld = new TextBox
        {
            AcceptsReturn = zeilen > 1,
            NewLine = "\n",
            TextWrapping = TextWrapping.Wrap,
            MinHeight = zeilen > 1 ? 28 + 24 * zeilen : 0,
            MaxHeight = 420,
            Text = EchtBewertung.Antwort(zustand, schluessel),
        }.Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt").Groesse();
        return Merken(feld, schluessel, name);
    }

    private TextBox Kurzfeld(string schluessel, string name, JsonObject zustand, double breite)
    {
        var feld = new TextBox { Text = EchtBewertung.Antwort(zustand, schluessel), TextWrapping = TextWrapping.NoWrap }
            .Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt").Groesse().Breite(breite);
        return Merken(feld, schluessel, name);
    }

    private TextBox Merken(TextBox feld, string schluessel, string name)
    {
        Avalonia.Automation.AutomationProperties.SetName(feld, name);
        feld.IsReadOnly = !_a.ImLauf;
        feld.TextChanged += (_, _) =>
        {
            _speichern.Stop();
            _speichern.Start();
        };
        _felder[schluessel] = feld;
        return feld;
    }

    private static Border Option(string text, string gruppe, bool gewaehlt, Action<bool> gewechselt, bool radio)
    {
        Avalonia.Controls.Primitives.ToggleButton wahl = radio
            ? new RadioButton { GroupName = gruppe }
            : new CheckBox();
        wahl.Content = new LernText(text, "Normal", FontWeight.Normal, "Pinsel.text_soft");
        wahl.IsChecked = gewaehlt;
        wahl.HorizontalAlignment = HorizontalAlignment.Stretch;
        Avalonia.Automation.AutomationProperties.SetName(wahl, text);
        wahl.IsCheckedChanged += (_, _) => gewechselt(wahl.IsChecked == true);
        return new Border
        {
            Child = wahl,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 4),
        }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border");
    }

    private void Ticken()
    {
        if (_a.Zustand is null || _uhr is null)
        {
            _takt.Stop();
            return;
        }
        var rest = _a.Rest;
        _uhr.Text = Klausur.Zeittext(rest);
        _uhr.Farbe(TextBlock.ForegroundProperty, rest < 300 ? "Pinsel.red" : "Pinsel.accent");
        if (rest <= 0 && _a.ImLauf && !_a.Probe && !_abgeben)
        {
            _takt.Stop();
            _ = AbgebenAsync(auto: true);
        }
    }

    private void FortschrittZeigen()
    {
        if (_fortschritt is not null)
        {
            _fortschritt.Text = _a.Fortschritt();
        }
    }

    private void Festhalten()
    {
        _speichern.Stop();
        if (_a.Festhalten(_felder.Select(f => (f.Key, f.Value.Text)).Concat(_baukaesten.Select(b => (b.Key, (string?)b.Value.Json)))
            .Concat(_struktogramme.Select(b => (b.Key, (string?)b.Value.Json)))))
        {
            FortschrittZeigen();
        }
    }

    private async Task AbbrechenAsync()
    {
        var zustand = _a.Zustand;
        if (await Dialog.Zeigen(PruefungTexte.AbbrechenTitel, PruefungTexte.AbbrechenFrage, TrainerTexte.Ja, TrainerTexte.Nein) != 0
            || _a.Zustand != zustand || !_a.ImLauf)
        {
            return;
        }
        _speichern.Stop();
        await SchrittAsync(_a.Abbrechen());
    }

    private async Task AbgebenAsync(bool auto)
    {
        if (_abgeben || _a.Zustand is not { } zustand || !_a.ImLauf)
        {
            return;
        }
        _abgeben = true;
        try
        {
            var nein = !auto && await Dialog.Zeigen(PruefungTexte.AbgebenTitel, PruefungTexte.AbgebenFrage, TrainerTexte.Ja, TrainerTexte.Nein) != 0;
            if (_a.Zustand != zustand || !_a.ImLauf)
            {
                return;
            }
            auto = auto || _a.Rest <= 0;    // keine Pause: lief die Zeit waehrend der Rueckfrage ab, wird abgegeben
            if (nein && !auto)
            {
                return;
            }
            Festhalten();
            _takt.Stop();
            await SchrittAsync(_a.Abgeben(auto));
        }
        finally
        {
            _abgeben = false;
        }
    }

    // -- Bewertung --------------------------------------------------------------------------------

    private void BewertungZeigen()
    {
        _leeren();
        Aktiv = true;
        Bewertet = true;
        var satz = _a.Satz!;
        var abschliessen = LernTeile.Knopf(PruefungTexte.BewertungAbschliessen, true);
        abschliessen.Click += async (_, _) => await AbschliessenAsync();
        _summe = Schrift.Text("", "Normal", FontWeight.Bold);
        _summe.VerticalAlignment = VerticalAlignment.Center;
        var kopf = new StackPanel { Spacing = 10 };
        kopf.Children.Add(new LernText(EchtTexte.BewertungAnleitung, "Klein", FontWeight.Normal, "Pinsel.text_soft"));
        kopf.Children.Add(new NebenOderUnter(160, 14) { Children = { _summe, abschliessen } });
        _koerper.Children.Add(Schrift.KopfKarte(PruefungTexte.BewertungTitel, BereichName, kopf));
        if (_reiter is Ausgang or Zeit)
        {
            _reiter = "1";
        }
        _reiterPillen = new Pillen(Reiter(satz, mitZeitfrage: false), _reiter, w =>
        {
            _reiter = w;
            BewertungMalen();
            _koerper.Children[0].BringIntoView();
        });
        _koerper.Children.Add(_reiterPillen);
        _inhalt = new StackPanel { Spacing = 14 };
        _koerper.Children.Add(_inhalt);
        BewertungMalen();
    }

    private void BewertungMalen()
    {
        var satz = _a.Satz!;
        var zustand = _a.Zustand!;
        _inhalt!.Children.Clear();
        var n = int.Parse(_reiter, CultureInfo.InvariantCulture);
        var aufgabe = satz.Aufgaben[n - 1];
        var inhalt = new StackPanel { Spacing = 4 };
        foreach (var teil in aufgabe.Teile)
        {
            var wertung = EchtBewertung.Teil(zustand, n, teil);
            inhalt.Children.Add(Abstand(Teilkopf(teil), 16));
            inhalt.Children.Add(Abstand(LernTeile.Label(PruefungTexte.DeineAntwort), 6));
            var punkteZeile = teil.Punkte - teil.SelbstPunkte > 0
                ? Schrift.Text(Beschriftungen.Einsetzen(EchtTexte.ProgrammPunkte, wertung.Programm, teil.Punkte - teil.SelbstPunkte),
                    "Klein", FontWeight.Bold, wertung.Programm == teil.Punkte - teil.SelbstPunkte ? "Pinsel.green" : "Pinsel.red")
                : null;
            if (teil.Antwort.Form == EchtSaetze.FormSchreibtischtest && ZweiSpalten)
            {
                // A 1.20.0 (Entwurf P12-267 Bild 04): breit Raster links, Musterloesung rechts
                var links = new StackPanel { Spacing = 4, Children = { EigeneAntwort(n, teil, zustand, wertung) } };
                if (punkteZeile is not null)
                {
                    links.Children.Add(punkteZeile);
                }
                var rechts = new StackPanel { Spacing = 4 };
                rechts.Children.Add(LernTeile.Label(EchtTexte.Musterloesung));
                foreach (var zeile in teil.Loesung)
                {
                    rechts.Children.Add(new TabellenText(zeile, "Klein", FontWeight.Normal, "Pinsel.text_soft"));
                }
                var spalten = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,14,2*") };
                spalten.Children.Add(links);
                Grid.SetColumn(rechts, 2);
                spalten.Children.Add(rechts);
                inhalt.Children.Add(spalten);
                continue;
            }
            inhalt.Children.Add(EigeneAntwort(n, teil, zustand, wertung));
            if (punkteZeile is not null)
            {
                inhalt.Children.Add(punkteZeile);
            }
            inhalt.Children.Add(Abstand(LernTeile.Label(teil.Antwort.Form == EchtSaetze.FormSchreibtischtest ? EchtTexte.Musterloesung : EchtTexte.Loesung), 8));
            foreach (var zeile in teil.Loesung)
            {
                inhalt.Children.Add(new TabellenText(zeile, "Klein", FontWeight.Normal, "Pinsel.text_soft"));
            }
            if (teil.Antwort.Struktogramm is { } strukturMuster)
            {
                // A 1.12.0 (Weisung P12-390, Auflage F): eigene Zeichnung und Musterbild beschriftet untereinander
                var eigen = EchtStruktogramm.Laden(EchtBewertung.Antwort(zustand, EchtBewertung.Feld(n, teil.Nr, EchtBaukasten.Feld)));
                if (eigen.Eigenes)
                {
                    inhalt.Children.Add(Abstand(LernTeile.Label(EchtTexte.EigeneZeichnungTitel), 8));
                    inhalt.Children.Add(EchtBilder.Struktogramm(eigen.Bloecke, EchtTexte.EigeneZeichnung + ": " + EchtTexte.StruktogrammName, _alsText));
                    inhalt.Children.Add(Abstand(Zaehlung(EchtStruktogramm.Zaehlen(eigen.Bloecke, strukturMuster)), 8));
                }
                inhalt.Children.Add(Abstand(LernTeile.Label(EchtTexte.Musterbild), 8));
                inhalt.Children.Add(EchtBilder.Struktogramm(strukturMuster, EchtTexte.Musterbild + ": " + EchtTexte.StruktogrammName, _alsText));
            }
            if (teil.Antwort.Muster is { } muster)
            {
                var bau = EchtBaukasten.Laden(muster.Art, teil.Antwort.Vorgabe,
                    EchtBewertung.Antwort(zustand, EchtBewertung.Feld(n, teil.Nr, EchtBaukasten.Feld)));
                if (bau.Eigenes)
                {
                    inhalt.Children.Add(Abstand(LernTeile.Label(EchtTexte.EigeneZeichnungTitel), 8));
                    inhalt.Children.Add(EchtBilder.Diagramm(bau.Diagramm, EchtTexte.EigeneZeichnung + ": " + EchtZeichnung.Name(muster.Art), _alsText));
                    inhalt.Children.Add(Abstand(Zaehlung(EchtBaukasten.Zaehlen(bau.Diagramm, muster)), 8));
                }
                inhalt.Children.Add(Abstand(LernTeile.Label(EchtTexte.Musterbild), 8));
                inhalt.Children.Add(EchtBilder.Diagramm(muster, EchtTexte.Musterbild + ": " + EchtZeichnung.Name(muster.Art), _alsText));
            }
            if (EchtBewertung.HakenAnzahl(teil) > 0)
            {
                var selbst = Schrift.Text("", "Klein", FontWeight.Bold, "Pinsel.text_soft");
                void SelbstZeigen() => selbst.Text = Beschriftungen.Einsetzen(EchtTexte.SelbstPunkte,
                    EchtBewertung.Teil(_a.Zustand!, n, teil).Erreicht - EchtBewertung.Teil(_a.Zustand!, n, teil).Programm, teil.SelbstPunkte);
                var haken = EchtBewertung.Haken(zustand, EchtBewertung.Schluessel(n, teil.Nr));
                for (var i = 0; i < EchtBewertung.HakenAnzahl(teil); i++)
                {
                    var index = i;
                    var text = teil.Antwort.Form == EchtSaetze.FormAngaben
                        ? Beschriftungen.Einsetzen(EchtTexte.AngabeRichtig, i + 1, EchtTexte.PunkteText(EchtBewertung.HakenPunkte(teil, i)))
                        : Beschriftungen.Einsetzen(EchtTexte.HinweisPunkte, teil.Hinweise[i].Text, EchtTexte.PunkteText(teil.Hinweise[i].Punkte));
                    var wahl = new CheckBox
                    {
                        Content = new LernText(text, "Klein", FontWeight.Normal, "Pinsel.text_soft"),
                        IsChecked = i < haken.Count && haken[i],
                        Margin = new Thickness(0, 2, 0, 0),
                    };
                    Avalonia.Automation.AutomationProperties.SetName(wahl, text);
                    wahl.IsCheckedChanged += (_, _) =>
                    {
                        _a.Haken(n, teil, index, wahl.IsChecked == true);
                        SelbstZeigen();
                        SummeZeigen();
                    };
                    inhalt.Children.Add(wahl);
                }
                SelbstZeigen();
                inhalt.Children.Add(selbst);
            }
        }
        _inhalt.Children.Add(Schrift.KopfKarte(Beschriftungen.Einsetzen(EchtTexte.AufgabeTitel, n, aufgabe.Titel),
            EchtTexte.PunkteText(aufgabe.Punkte), inhalt));
        SummeZeigen();
    }

    /// <summary>Eigene Antwort; was das Programm bewertet, gruen oder rot mit dem erwarteten Wert.</summary>
    /// <summary>A 0.78.0: was das Programm an der eigenen Zeichnung gezaehlt hat (gruen/rot, mit Zeichen fuer Farbenblinde).</summary>
    private static Border Zaehlung(IReadOnlyList<EchtZaehlung> zeilen)
    {
        var liste = new StackPanel { Spacing = 4 };
        liste.Children.Add(LernTeile.Label(EchtTexte.ProgrammZaehlt));
        foreach (var z in zeilen)
        {
            liste.Children.Add(new LernText((z.Ok ? "✓ " : "✗ ") + z.Text, "Klein", FontWeight.Bold, z.Ok ? "Pinsel.green" : "Pinsel.red"));
        }
        liste.Children.Add(new LernText(EchtTexte.ZaehlHinweis, "Klein", FontWeight.Normal, "Pinsel.muted"));
        return new Border
        {
            Child = liste,
            Padding = new Thickness(12, 10),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
        }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border");
    }

    private static Control EigeneAntwort(int n, EchtTeil teil, JsonObject zustand, EchtTeilwertung wertung)
    {
        var a = teil.Antwort;
        var liste = new StackPanel { Spacing = 4 };
        void Text(string wert) => liste.Children.Add(new LernText(wert.Length > 0 ? wert : PruefungTexte.KeineAntwort, "Normal",
            FontWeight.Normal, "Pinsel.text_soft"));
        void Bewertet(string name, string wert, bool richtig, string erwartet)
        {
            var zeile = new StackPanel { Spacing = 2 };
            zeile.Children.Add(new LernText(name + ": " + (wert.Length > 0 ? wert : EchtTexte.Leer) + " · "
                + (richtig ? EchtTexte.Richtig : EchtTexte.Falsch), "Normal", FontWeight.Bold, richtig ? "Pinsel.green" : "Pinsel.red"));
            if (!richtig)
            {
                zeile.Children.Add(new LernText(Beschriftungen.Einsetzen(EchtTexte.ErwarteteWerte, erwartet), "Klein", FontWeight.Normal,
                    "Pinsel.muted"));
            }
            liste.Children.Add(zeile);
        }
        string Wert(string feld) => EchtBewertung.Antwort(zustand, EchtBewertung.Feld(n, teil.Nr, feld));
        if (a.Form == EchtSaetze.FormSchreibtischtest)
        {
            // A 1.20.0 (Entwurf P12-267 Bild 04): das Raster selbst, jedes Kaestchen gruen oder rot, darunter die Legende
            var raster = new StackPanel { Spacing = 6 };
            raster.Children.Add(new EchtSchreibtischTeil(a, (z, s) => Wert(EchtBewertung.Zelle(z, s)), null, wertung.Felder, "Pinsel.card_alt"));
            var art = a.Schritte.Any(s => s.Punkte is not null)
                ? EchtTexte.SchreibtischJeSchritt
                : Beschriftungen.Einsetzen(EchtTexte.SchreibtischJeZelle,
                    EchtTexte.PunkteText(a.Schritte.SelectMany(s => s.Werte).FirstOrDefault(c => c.Offen)?.Punkte ?? 1));
            raster.Children.Add(new LernText(Beschriftungen.Einsetzen(EchtTexte.SchreibtischLegende, art), "Klein", FontWeight.Normal, "Pinsel.muted"));
            return raster;
        }
        switch (a.Form)
        {
            case EchtSaetze.FormZeilen:
                Text(Wert(EchtBewertung.Text));
                break;
            case EchtSaetze.FormAngaben:
                for (var i = 1; i <= teil.Angaben; i++)
                {
                    Text(Beschriftungen.Einsetzen(EchtTexte.AngabeFeld, i) + ": " + Wert(EchtBewertung.Angabe(i)));
                }
                break;
            case EchtSaetze.FormRechnen:
                Text(Wert(EchtBewertung.Weg));
                var erwartet = Zahl(a.Ergebnis) + (a.ErsatzErgebnis is { } ersatz ? " / " + Zahl(ersatz) : "");
                Bewertet(ErgebnisName(a), Wert(EchtBewertung.Ergebnis), wertung.Felder[0], erwartet);
                break;
            case EchtSaetze.FormTabelle:
                var k = 0;
                for (var z = 0; z < a.Tabelle.Count; z++)
                {
                    for (var s = 0; s < a.Tabelle[z].Count; s++)
                    {
                        if (a.Tabelle[z][s] is { Offen: true } zelle)
                        {
                            var name = (a.Tabelle[z][0].Fest ?? Nummer(z + 1)) + ", " + a.Spalten[s];
                            Bewertet(name, Wert(EchtBewertung.Zelle(z, s)), wertung.Felder[k++], zelle.Loesung[0]);
                        }
                    }
                }
                break;
            case EchtSaetze.FormLuecke:
                for (var f = 0; f < a.Felder.Count; f++)
                {
                    Bewertet(a.Felder[f].Name, Wert(EchtBewertung.Luecke(f)), wertung.Felder[f], a.Felder[f].Loesung[0]);
                }
                break;
            case EchtSaetze.FormZeichnen:
                Text(Wert(Papier).Length > 0 ? EchtTexte.Gezeichnet : EchtTexte.NichtGezeichnet);
                if (Wert(EchtBaukasten.Feld).Length > 0)
                {
                    Text(EchtTexte.ImBaukasten);
                }
                break;
            case EchtSaetze.FormAuswahl:
                var gewaehlt = EchtBewertung.Gewaehlt(zustand, n, teil.Nr);
                foreach (var i in gewaehlt.Order().Where(i => i < a.Optionen.Count))
                {
                    Text("✓ " + a.Optionen[i]);
                }
                Bewertet(EchtTexte.Antwort, gewaehlt.Count.ToString(CultureInfo.InvariantCulture), wertung.Felder[0],
                    string.Join("; ", a.Richtig.Select(i => a.Optionen[i])));
                break;
        }
        return new Border
        {
            Child = liste,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8),
            Margin = new Thickness(0, 4, 0, 4),
        }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border");
    }

    private static string Zahl(double wert) => wert.ToString("0.####", CultureInfo.GetCultureInfo("de-DE"));

    private void SummeZeigen()
    {
        if (_summe is not null)
        {
            _summe.Text = _a.Zwischenstand();
        }
    }

    private async Task AbschliessenAsync()
    {
        if (_a.Zustand is not { } zustand
            || await Dialog.Zeigen(PruefungTexte.BewertungAbschliessen, PruefungTexte.AbschliessenFrage, TrainerTexte.Ja, TrainerTexte.Nein) != 0)
        {
            return;
        }
        if (_a.Zustand == zustand)
        {
            await SchrittAsync(_a.Abschliessen());
        }
    }

    // -- Ergebnis ---------------------------------------------------------------------------------

    private void ErgebnisZeigen(string art, EchtErgebnis ergebnis)
    {
        _leeren();
        Aktiv = true;
        var bereich = EchtArten.Bereich(art)!;
        var punkte = Schrift.Text(Beschriftungen.Einsetzen(PruefungTexte.PunkteMuster, Nummer(ergebnis.Punkte)), "Zahl", FontWeight.Bold,
            LernTeile.Pinsel(Gesamtstand.Notenfarbe(ergebnis.Punkte)));
        punkte.Margin = new Thickness(0, 0, 18, 0);
        var note = Schrift.Text(Beschriftungen.Einsetzen(PruefungTexte.NoteDauer, Navigation.NoteKurz, ergebnis.Note, Klausur.Zeittext(ergebnis.Dauer)),
            "Normal", FontWeight.Bold);
        note.VerticalAlignment = VerticalAlignment.Bottom;
        note.Margin = new Thickness(0, 0, 0, 6);
        _koerper.Children.Add(Schrift.KopfKarte(PruefungTexte.ErgebnisTitel, bereich.Name + EchtTexte.NameZusatz,
            new WrapPanel { Children = { punkte, note } }));
        var balken = new StackPanel { Spacing = 8 };
        for (var i = 0; i < ergebnis.Aufgaben.Count; i++)
        {
            var (titel, erreicht, hoechst) = ergebnis.Aufgaben[i];
            var schwach = erreicht * 2 < hoechst;
            var farbe = schwach ? "Pinsel.red" : "Pinsel.green";
            balken.Children.Add(new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TitelUndWert(160) { Children = { new WortText(Beschriftungen.Einsetzen(EchtTexte.AufgabeTitel, i + 1, titel), "Klein",
                        FontWeight.Normal, "Pinsel.text_soft"),
                        Schrift.Text(Beschriftungen.Einsetzen(EchtTexte.AufgabePunkte, erreicht, hoechst), "Klein", FontWeight.Bold, farbe) } },
                    new ProgressBar { Minimum = 0, Maximum = hoechst, Value = erreicht, Height = 8, MinWidth = 40 }
                        .Farbe(ProgressBar.ForegroundProperty, farbe).Farbe(ProgressBar.BackgroundProperty, "Pinsel.card_hi"),
                },
            });
        }
        _koerper.Children.Add(Schrift.KopfKarte(EchtTexte.ErgebnisAufgaben, EchtTexte.ErgebnisAufgabenUntertitel, balken));
        var zurueck = LernTeile.Knopf(PruefungTexte.ZurAuswahl);
        zurueck.HorizontalAlignment = HorizontalAlignment.Left;
        zurueck.Click += (_, _) => _zurAuswahl();
        _koerper.Children.Add(zurueck);
    }

    // -- Start ohne Bedienung -----------------------------------------------------------------------

    /// <summary>Namen der Ansichten von <see cref="Probe"/> (Nummer = Index).</summary>
    internal static readonly string[] ProbeNamen =
    [
        "pruefung/echt-lauf-ausgangssituation", "pruefung/echt-lauf-aufgabe1", "pruefung/echt-lauf-aufgabe2", "pruefung/echt-lauf-aufgabe4",
        "pruefung/echt-lauf-anlagen1", "pruefung/echt-lauf-anlagen4", "pruefung/echt-lauf-zeitfrage", "pruefung/echt-bewertung-aufgabe1",
        "pruefung/echt-bewertung-aufgabe2", "pruefung/echt-ergebnis",
        // A 0.77.0 (Welle 2): Probesatz AP2 Konzeption mit Netzplan, Monitor-Bild und Zeichenaufgabe
        "pruefung/echt-b1-lauf-aufgabe2", "pruefung/echt-b1-anlagen2", "pruefung/echt-b1-anlagen1", "pruefung/echt-b1-lauf-aufgabe3",
        "pruefung/echt-b1-bewertung-aufgabe3", "pruefung/echt-b1-anlagen2-text", "pruefung/echt-diagramm-uml",
        // A 0.78.0: Zeichen-Baukasten (ER im Probesatz, UML ohne Satz, Bewertung mit Zaehlung)
        "pruefung/echt-b1-baukasten-er", "pruefung/echt-baukasten-uml", "pruefung/echt-b1-bewertung-baukasten",
        // A 0.84.0 (Weisung P12-285): erster Satz der Fachrichtung FIAE je Bereich (Lauf, Anlagen, Bewertung)
        "pruefung/echt-ae-b1-lauf-aufgabe1", "pruefung/echt-ae-b1-anlagen1", "pruefung/echt-ae-b1-bewertung-aufgabe1",
        "pruefung/echt-ae-b2-lauf-aufgabe1", "pruefung/echt-ae-b2-anlagen1", "pruefung/echt-ae-b2-bewertung-aufgabe1",
        // A 0.85.0 (Weisung P12-291): erster Satz der Fachrichtung FIDP je Bereich (Lauf, Anlagen, Bewertung)
        "pruefung/echt-dpa-b1-lauf-aufgabe1", "pruefung/echt-dpa-b1-anlagen1", "pruefung/echt-dpa-b1-bewertung-aufgabe1",
        "pruefung/echt-dpa-b2-lauf-aufgabe1", "pruefung/echt-dpa-b2-anlagen1", "pruefung/echt-dpa-b2-bewertung-aufgabe1",
        // A 0.86.0 (Weisung P12-292): erster Satz der Fachrichtung FIDV je Bereich (Lauf, Anlagen, Bewertung)
        "pruefung/echt-dv-b1-lauf-aufgabe1", "pruefung/echt-dv-b1-anlagen1", "pruefung/echt-dv-b1-bewertung-aufgabe1",
        "pruefung/echt-dv-b2-lauf-aufgabe1", "pruefung/echt-dv-b2-anlagen1", "pruefung/echt-dv-b2-bewertung-aufgabe1",
        // A 1.8.0 (Weisung P12-371): Klassen- und Use-Case-Baukasten in FIAE-Saetzen (Lauf mit gewaehltem Baustein, Bewertung)
        "pruefung/echt-ae-baukasten-klasse", "pruefung/echt-ae-baukasten-usecase", "pruefung/echt-ae-bewertung-klasse",
        "pruefung/echt-ae-bewertung-usecase",
        // A 1.12.0 (Weisung P12-390): Struktogramm im Baukasten (Lauf mit gewaehlter Verzweigung, alle sieben Bausteine, Bewertung)
        "pruefung/echt-ae-struktogramm-lauf", "pruefung/echt-ae-struktogramm-alle", "pruefung/echt-ae-struktogramm-bewertung",
        // A 1.20.0 (Weisung P12-446): Schreibtischtest alle_ap1_03 4 b (Lauf, Code markiert, Bewertung), Probesatz mit Ausgabe,
        // CSV wie im Beleg und als Tabelle, JSON, dazu "Groß öffnen" des CSV
        "pruefung/echt-schreibtisch-lauf", "pruefung/echt-schreibtisch-anlagen", "pruefung/echt-schreibtisch-bewertung",
        "pruefung/echt-ausgabe", "pruefung/echt-csv-beleg", "pruefung/echt-csv-tabelle", "pruefung/echt-json", "pruefung/echt-gross",
    ];

    /// <summary>Erste Nummer der Ansichten der Welle 2 in <see cref="ProbeNamen"/>.</summary>
    internal const int Welle2Ab = 10;

    /// <summary>Erste Nummer der Ansichten des Baukastens (A 0.78.0) in <see cref="ProbeNamen"/>.</summary>
    internal const int BaukastenAb = 17;

    /// <summary>Erste Nummer der Ansichten der Fachrichtung FIAE (A 0.84.0) in <see cref="ProbeNamen"/>: je Bereich drei.</summary>
    internal const int AeAb = 20;

    /// <summary>Erste Nummer der Ansichten der Fachrichtung FIDP (A 0.85.0) in <see cref="ProbeNamen"/>: je Bereich drei.</summary>
    internal const int DpaAb = 26;

    /// <summary>Erste Nummer der Ansichten der Fachrichtung FIDV (A 0.86.0) in <see cref="ProbeNamen"/>: je Bereich drei.</summary>
    internal const int DvAb = 32;

    /// <summary>
    /// Erste Nummer der Ansichten der Welle W1 (A 1.8.0) in <see cref="ProbeNamen"/>: Klasse im Lauf (ae_b1_07 Aufgabe 2),
    /// Use-Case im Lauf (ae_b1_06 Aufgabe 4), dann beide bewertet.
    /// </summary>
    internal const int W1Ab = 38;

    /// <summary>
    /// Erste Nummer der Ansichten der Welle W2 (A 1.12.0) in <see cref="ProbeNamen"/>: Struktogramm ae_b2_08 Aufgabe 2 im Lauf
    /// (Verzweigung gewaehlt), Probezeichnung mit allen sieben Bausteinen (ae_b2_08 Aufgabe 2), ae_b2_08 bewertet.
    /// </summary>
    internal const int W2Ab = 42;

    /// <summary>Erste Nummer der Ansichten von A 1.20.0 (Schreibtischtest, Ausgabe, Datenauszug, Groß öffnen) in <see cref="ProbeNamen"/>.</summary>
    internal const int SchreibtischAb = 45;

    /// <summary>
    /// Probezustand: Probesatz des Bereichs (AP1 oder ab A 0.77.0 AP2 Konzeption der Fachrichtung FISI), 754 s gelaufen,
    /// Antworten teils richtig, teils falsch, teils leer.
    /// </summary>
    internal JsonObject ProbeZustand(bool bewertet, string bereich = EchtSaetze.Ap1, string? fachrichtung = null, string? kennung = null,
        EchtBestand? bestand = null)
    {
        _probeBestand = bestand;
        var quelle = bestand ?? Zustand.EchtBestand;
        // A 0.94.0: ersetzte Saetze (ausschluss.json) zeigt die Probe nicht mehr; A 1.8.0: fester Satz ueber die Kennung;
        // A 1.20.0: Probesatz aus <paramref name="bestand"/>
        var satz = quelle.Saetze.First(s => s.Bereich == bereich && (fachrichtung is null || s.Fachrichtung == fachrichtung)
            && !quelle.IstAusgeschlossen(s.Kennung) && (kennung is null || s.Kennung == kennung));
        var art = bereich == EchtSaetze.Ap1 ? EchtArten.Praefix + EchtSaetze.Ap1 : EchtArten.Art(Fachrichtung.Nach(satz.Fachrichtung), bereich);
        var zustand = EchtAblauf.Neu(satz, art, DateTimeOffset.Now.AddSeconds(-754), TimeZoneInfo.Local);
        var antworten = (JsonObject)zustand["antworten"]!;
        var haken = (JsonObject)zustand["haken"]!;
        var de = CultureInfo.GetCultureInfo("de-DE");
        for (var n = 1; n <= satz.Aufgaben.Count; n++)
        {
            var teile = satz.Aufgaben[n - 1].Teile;
            for (var t = 0; t < teile.Count; t++)
            {
                var teil = teile[t];
                var a = teil.Antwort;
                var richtig = (n + t) % 3 != 0;
                string F(string feld) => EchtBewertung.Feld(n, teil.Nr, feld);
                switch (a.Form)
                {
                    case EchtSaetze.FormZeilen:
                        antworten[F(EchtBewertung.Text)] = string.Join("\n", teil.Loesung);
                        break;
                    case EchtSaetze.FormAngaben:
                        for (var i = 1; i <= teil.Angaben; i++)
                        {
                            antworten[F(EchtBewertung.Angabe(i))] = teil.Loesung[Math.Min(i - 1, teil.Loesung.Count - 1)];
                        }
                        break;
                    case EchtSaetze.FormRechnen:
                        antworten[F(EchtBewertung.Weg)] = string.Join("\n", teil.Loesung);
                        antworten[F(EchtBewertung.Ergebnis)] = (richtig ? a.Ergebnis : a.Ergebnis + 1).ToString(de);
                        break;
                    case EchtSaetze.FormTabelle:
                        for (var z = 0; z < a.Tabelle.Count; z++)
                        {
                            for (var s = 0; s < a.Tabelle[z].Count; s++)
                            {
                                if (a.Tabelle[z][s].Offen)
                                {
                                    antworten[F(EchtBewertung.Zelle(z, s))] = richtig || z % 2 == 0 ? a.Tabelle[z][s].Loesung[0] : "";
                                }
                            }
                        }
                        break;
                    case EchtSaetze.FormLuecke:
                        for (var f = 0; f < a.Felder.Count; f++)
                        {
                            antworten[F(EchtBewertung.Luecke(f))] = richtig || f > 0 ? a.Felder[f].Loesung[0] : "10.24.8.1";
                        }
                        break;
                    case EchtSaetze.FormAuswahl:
                        antworten[F(EchtBewertung.Wahl)] = richtig ? string.Join(",", a.Richtig) : "0,1";
                        break;
                    case EchtSaetze.FormZeichnen:
                        antworten[F(Papier)] = richtig ? TrainerTexte.Ja : "";
                        break;
                    case EchtSaetze.FormSchreibtischtest:
                        // A 1.20.0: Zellen der ersten Haelfte richtig, dann eine falsch, eine leer, der Rest richtig
                        var k = 0;
                        for (var z = 0; z < a.Schritte.Count; z++)
                        {
                            for (var sp = 0; sp < a.Schritte[z].Werte.Count; sp++)
                            {
                                if (a.Schritte[z].Werte[sp] is { Offen: true } zelle)
                                {
                                    antworten[F(EchtBewertung.Zelle(z, sp))] = k == ProbeLeer - 1 ? zelle.Loesung[0] + "0" : k == ProbeLeer ? "" : zelle.Loesung[0];
                                    k++;
                                }
                            }
                        }
                        break;
                }
                if (bewertet && EchtBewertung.HakenAnzahl(teil) > 0)
                {
                    haken[EchtBewertung.Schluessel(n, teil.Nr)] = new JsonArray([.. Enumerable.Range(0, EchtBewertung.HakenAnzahl(teil))
                        .Select(i => (JsonNode?)JsonValue.Create(richtig || i == 0))]);
                }
            }
        }
        zustand["zeitfrage"] = 1;
        if (bewertet)
        {
            Klausur.Abgeben(zustand, Klausur.Sekunden(DateTimeOffset.Now), TimeZoneInfo.Local);
        }
        return zustand;
    }

    /// <summary>Nur fuer den Start ohne Bedienung: Ansicht <paramref name="art"/> (siehe <see cref="ProbeNamen"/>) ohne Speichern.</summary>
    internal void Probe(int art)
    {
        _speichern.Stop();
        _takt.Stop();
        switch (art)
        {
            case <= 6:
                _a.ProbeSetzen(ProbeZustand(bewertet: false));
                (_reiter, _anlagen) = art switch
                {
                    0 => (Ausgang, false),
                    1 => ("1", false),
                    2 => ("2", false),
                    3 => ("4", false),
                    4 => ("1", true),
                    5 => ("4", true),
                    _ => (Zeit, false),
                };
                LaufZeigen();
                break;
            case 7:
            case 8:
                _a.ProbeSetzen(ProbeZustand(bewertet: true));
                _reiter = art == 7 ? "1" : "2";
                BewertungZeigen();
                break;
            case 9:
                var zustand = ProbeZustand(bewertet: true);
                var ergebnis = EchtBewertung.Auswerten(_a.Bestand.Satz(Json.Text(zustand["satz"]))!, zustand);
                _a.ProbeSetzen(zustand, ergebnis);
                ErgebnisZeigen(Json.Text(zustand["art"])!, ergebnis);
                break;
            case Welle2Ab + 4:
                _a.ProbeSetzen(ProbeZustand(bewertet: true, EchtSaetze.B1));
                _reiter = "3";
                BewertungZeigen();
                break;
            case Welle2Ab + 6:
                UmlZeigen();
                break;
            case BaukastenAb:
            case BaukastenAb + 2:
                var probe = ProbeZustand(bewertet: art == BaukastenAb + 2, EchtSaetze.B1);
                var satzB1 = _a.Bestand.Satz(Json.Text(probe["satz"]))!;
                var erTeil = satzB1.Aufgaben[2].Teile.First(t => t.Antwort.Form == EchtSaetze.FormZeichnen);
                ((JsonObject)probe["antworten"]!)[EchtBewertung.Feld(3, erTeil.Nr, EchtBaukasten.Feld)] = ProbeEr(erTeil).Json();
                _a.ProbeSetzen(probe);
                (_reiter, _anlagen) = ("3", false);
                if (art == BaukastenAb)
                {
                    LaufZeigen();
                    var er = _baukaesten.Values.FirstOrDefault();
                    er?.ProbeWaehlen(er.ProbeBau.Diagramm.Elemente.FirstOrDefault(e => e.Text == "besucht")?.Id);
                }
                else
                {
                    BewertungZeigen();
                }
                break;
            case BaukastenAb + 1:
                UmlBaukastenZeigen();
                break;
            case >= SchreibtischAb:
                SchreibtischZeigen(art - SchreibtischAb);
                break;
            case >= W2Ab:
                W2Zeigen(art - W2Ab);
                break;
            case >= W1Ab:
                W1Zeigen(art - W1Ab);
                break;
            case >= AeAb:
                // A 0.84.0: erster FIAE-Satz des Bereichs (0-2 Bereich 1, 3-5 Bereich 2), Aufgabe 1 im Lauf, mit Anlagen, bewertet;
                // A 0.85.0: ab DpaAb dasselbe fuer FIDP, A 0.86.0: ab DvAb fuer FIDV
                var (fr, erste) = art >= DvAb ? ("dv", DvAb) : art >= DpaAb ? ("dpa", DpaAb) : ("ae", AeAb);
                var aeArt = (art - erste) % 3;
                _a.ProbeSetzen(ProbeZustand(bewertet: aeArt == 2, art - erste < 3 ? EchtSaetze.B1 : EchtSaetze.B2, fr));
                (_reiter, _anlagen) = ("1", aeArt == 1);
                if (aeArt == 2)
                {
                    BewertungZeigen();
                }
                else
                {
                    LaufZeigen();
                }
                break;
            default:
                _a.ProbeSetzen(ProbeZustand(bewertet: false, EchtSaetze.B1));
                _alsText = art == Welle2Ab + 5;
                (_reiter, _anlagen) = (art - Welle2Ab) switch
                {
                    0 => ("2", false),
                    1 or 5 => ("2", true),
                    2 => ("1", true),
                    _ => ("3", false),
                };
                LaufZeigen();
                _alsText = false;
                break;
        }
        _takt.Stop();
    }

    /// <summary>
    /// Nur Start ohne Bedienung: ein UML-Aktivitaetsdiagramm aus <see cref="ProbeUml"/> (der Probesatz der Welle 2 hat eine
    /// ER-Aufgabe; UML kommt mit den Inhalten), damit Bilder und Layoutpruefung beide Diagrammarten sehen.
    /// </summary>
    private void UmlZeigen()
    {
        _leeren();
        Aktiv = true;
        var diagramm = EchtZeichnung.Diagramm(JsonNode.Parse(ProbeUml), EchtZeichnung.Uml, vollstaendig: true, "Probe");
        _inhalt = new StackPanel { Spacing = 14 };
        _koerper.Children.Add(_inhalt);
        _inhalt.Children.Add(Schrift.KopfKarte(EchtTexte.Musterbild, EchtTexte.UmlName, EchtBilder.Diagramm(diagramm, EchtTexte.UmlName)));
    }

    /// <summary>
    /// Nur Start ohne Bedienung (A 0.78.0): Baukasten fuer ein UML-Aktivitaetsdiagramm ohne Satz; Vorgabe = Start und erste
    /// Aktion aus <see cref="ProbeUml"/>, dazu eine Verzweigung mit zwei Aktionen, eine davon noch ohne Bedingung. Gewaehlt ist
    /// die Verzweigung (Karte mit Bedingungen). Aenderungen werden nirgends gespeichert.
    /// </summary>
    private void UmlBaukastenZeigen()
    {
        _leeren();
        Aktiv = true;
        var muster = EchtZeichnung.Diagramm(JsonNode.Parse(ProbeUml), EchtZeichnung.Uml, vollstaendig: true, "Probe");
        var vorgabe = muster with
        {
            Elemente = muster.Elemente.Where(e => e.Id is "s" or "a1").ToList(),
            Verbindungen = muster.Verbindungen.Where(v => v.Von == "s").ToList(),
        };
        var bau = EchtBaukasten.Neu(EchtZeichnung.Uml, vorgabe);
        var v = bau.Setzen("verzweigung", "a1")!;
        bau.Verbinden("a1", v);
        var a2 = bau.Setzen("aktion", v)!;
        bau.Beschriften(a2, "Bereitschaft anrufen");
        var a3 = bau.Setzen("aktion", v)!;
        bau.Beschriften(a3, "Ticket anlegen");
        bau.Verbinden(v, a2);
        bau.Verbinden(v, a3);
        bau.Bedingung(v, a2, "[dringend]");
        var teil = new EchtBaukastenTeil(bau, _ => { });
        _inhalt = new StackPanel { Spacing = 14 };
        _koerper.Children.Add(_inhalt);
        _inhalt.Children.Add(Schrift.KopfKarte(EchtTexte.Zeichenflaeche, EchtTexte.UmlName, teil));
        teil.ProbeWaehlen(v);
        ProbeUmlBaukasten = teil;
    }

    /// <summary>Probe: der Baukasten der Ansicht "echt-baukasten-uml" (Bedienprobe).</summary>
    internal EchtBaukastenTeil? ProbeUmlBaukasten { get; private set; }

    /// <summary>Probe: der Baukasten im Lauf (erste Zeichenaufgabe des Reiters).</summary>
    internal EchtBaukastenTeil? ProbeBaukasten => _baukaesten.Values.FirstOrDefault();

    /// <summary>
    /// Probe-Zeichnung zur ER-Aufgabe des Probesatzes: Patient und „besucht“ an der Stelle des Musterbilds, Kardinalitaeten
    /// 1:n und n:m, Schluessel Patientennummer; es fehlen Qualifikation, Datum, Startzeit, geplante Uhrzeit, Name, Adresse
    /// (die Zaehlung zeigt dann Gruenes und Rotes).
    /// </summary>
    /// <summary>
    /// Nur Start ohne Bedienung (A 1.8.0, Weisung P12-371): <paramref name="nr"/> 0 = Klassendiagramm im Lauf (ae_b1_07 Aufgabe 2,
    /// eigene Klasse "Erinnerung" gewaehlt), 1 = Use-Case im Lauf (ae_b1_06 Aufgabe 4, "Abokunde" gewaehlt), 2 und 3 = dieselben
    /// Zeichnungen bewertet (Zaehlung gruen und rot). Die Zeichnung ist teils richtig, teils falsch (siehe <see cref="ProbeKlasse"/>).
    /// </summary>
    private void W1Zeigen(int nr)
    {
        var klasse = nr % 2 == 0;
        var (kennung, aufgabe) = klasse ? ("ae_b1_07", 2) : ("ae_b1_06", 4);
        var probe = ProbeZustand(bewertet: nr >= 2, EchtSaetze.B1, "ae", kennung);
        var satz = _a.Bestand.Satz(Json.Text(probe["satz"]))!;
        var teil = satz.Aufgaben[aufgabe - 1].Teile.First(t => t.Antwort.Diagramm == (klasse ? EchtZeichnung.Klasse : EchtZeichnung.UseCase));
        var bau = klasse ? ProbeKlasse(teil) : ProbeUseCase(teil);
        ((JsonObject)probe["antworten"]!)[EchtBewertung.Feld(aufgabe, teil.Nr, EchtBaukasten.Feld)] = bau.Json();
        _a.ProbeSetzen(probe);
        (_reiter, _anlagen) = (aufgabe.ToString(CultureInfo.InvariantCulture), false);
        if (nr >= 2)
        {
            BewertungZeigen();
            return;
        }
        LaufZeigen();
        var b = _baukaesten.Values.FirstOrDefault();
        b?.ProbeWaehlen(b.ProbeBau.Diagramm.Elemente.FirstOrDefault(e => e.Text == (klasse ? "Erinnerung" : "Abokunde"))?.Id);
    }

    private void W2Zeigen(int nr)
    {
        // ae_b2_08 2b: ae_b2_02 ist seit A 0.94.0 ausgeschlossen (ausschluss.json), die Probe zeigt nur angebotene Saetze
        var alle = nr == 1;
        var probe = ProbeZustand(bewertet: nr == 2, EchtSaetze.B2, "ae", "ae_b2_08");
        var satz = _a.Bestand.Satz(Json.Text(probe["satz"]))!;
        var teil = satz.Aufgaben[1].Teile.First(t => t.Antwort.Struktogramm is not null);
        var (bau, wahl) = alle ? ProbeStruktogrammAlle() : ProbeStruktogramm();
        ((JsonObject)probe["antworten"]!)[EchtBewertung.Feld(2, teil.Nr, EchtBaukasten.Feld)] = bau.Json();
        _a.ProbeSetzen(probe);
        (_reiter, _anlagen) = ("2", false);
        if (nr == 2)
        {
            BewertungZeigen();
            return;
        }
        LaufZeigen();
        var b = _struktogramme.Values.FirstOrDefault();
        b?.ProbeWaehlen(wahl);
        b?.ProbeZiel(alle ? 0 : 3);
    }

    /// <summary>
    /// Nur Start ohne Bedienung (A 1.20.0): 0 = Schreibtischtest alle_ap1_03 Aufgabe 4 im Lauf (zweiter offener Schritt gewaehlt),
    /// 1 = dieselbe Aufgabe unter "Anlagen" mit markierter Code-Zeile, 2 = bewertet; ab 3 der Probesatz (<see cref="EchtProbesatz"/>):
    /// 3 Ausgabe (Aufgabe 1), 4 CSV wie im Beleg, 5 CSV als Tabelle (Aufgabe 3), 6 JSON (Aufgabe 2), 7 CSV gross geoeffnet.
    /// </summary>
    private void SchreibtischZeigen(int nr)
    {
        AnlageGross.Schliessen();
        _alsTabelle.Clear();
        if (nr <= 2)
        {
            _codeMarke.Clear();
            _a.ProbeSetzen(ProbeZustand(bewertet: nr == 2, EchtSaetze.Ap1, "alle", EchtProbesatz.Kennung));
            (_reiter, _anlagen) = ("4", nr == 1);
            if (nr == 2)
            {
                BewertungZeigen();
                return;
            }
            if (nr == 1)
            {
                // Code unter "Anlagen": erst im Raster den Schritt waehlen, dann zu "Anlagen" wechseln (die Markierung bleibt)
                _anlagen = false;
                LaufZeigen();
                ProbeSchreibtisch?.Waehlen(ProbeSchritt.Index);
                _anlagen = true;
                LaufZeigen();
                return;
            }
            LaufZeigen();
            if (ProbeSchreibtisch is { } raster)
            {
                // wie im Entwurf: der Schritt mit der leeren Zelle hat den Fokus
                raster.Waehlen(ProbeSchritt.Index);
                raster.Felder[ProbeLeer].Focus();
                // vor dem ersten Layout greift der Fokus nicht immer: sobald das Raster steht, noch einmal (Weisung P12-448 Fund A)
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (ReferenceEquals(ProbeSchreibtisch, raster) && TopLevel.GetTopLevel(raster.Felder[ProbeLeer]) is not null && !raster.Felder[ProbeLeer].IsFocused)
                    {
                        raster.Felder[ProbeLeer].Focus();
                    }
                }, Avalonia.Threading.DispatcherPriority.Loaded);
            }
            return;
        }
        var bestand = EchtSaetze.Laden(EchtProbesatz.Datei(Zustand.Aktuell.Saetze()!));
        _a.ProbeSetzen(ProbeZustand(bewertet: false, EchtSaetze.Ap1, "alle", EchtProbesatz.Kennung, bestand));
        var (aufgabe, _, anlage) = EchtProbesatz.Verweise[nr == 3 ? 0 : nr == 6 ? 2 : 1];
        if (nr == 5)
        {
            _alsTabelle[anlage] = true;
        }
        (_reiter, _anlagen) = (aufgabe.ToString(CultureInfo.InvariantCulture), !ZweiSpalten);
        LaufZeigen();
        if (nr == 7)
        {
            GrossOeffnen(_a.Satz!.Anlagen.First(x => x.Nr == anlage), aufgabe);
        }
    }

    /// <summary>Probe: Nummer der offenen Zelle, die <see cref="ProbeZustand"/> leer laesst.</summary>
    internal const int ProbeLeer = 3;

    /// <summary>Probe: Schritt des Schreibtischtests alle_ap1_03 4 b mit der leeren Zelle (Index ab 0 und Schritt).</summary>
    private (int Index, EchtSchreibSchritt Schritt) ProbeSchritt
    {
        get
        {
            var a = _a.Satz!.Aufgaben[3].Teile.First(t => t.Antwort.Form == EchtSaetze.FormSchreibtischtest).Antwort;
            var k = 0;
            for (var z = 0; z < a.Schritte.Count; z++)
            {
                k += a.Schritte[z].Werte.Count(c => c.Offen);
                if (k > ProbeLeer)
                {
                    return (z, a.Schritte[z]);
                }
            }
            return (0, a.Schritte[0]);
        }
    }

    /// <summary>Probe: das Raster des Schreibtischtests im Lauf (erster des Reiters).</summary>
    internal EchtSchreibtischTeil? ProbeSchreibtisch => _schreibtische.Values.FirstOrDefault();

    /// <summary>Probe: Code-Anlage eines Schreibtischtests im Lauf.</summary>
    internal CodeBlatt? ProbeCodeBlatt => _codeBlaetter.Values.FirstOrDefault();

    /// <summary>Probe: der Struktogramm-Baukasten im Lauf (erste Struktogramm-Aufgabe des Reiters).</summary>
    internal EchtStruktogrammTeil? ProbeStruktogrammTeil => _struktogramme.Values.FirstOrDefault();

    /// <summary>
    /// A 1.12.0: Probezeichnung zu ae_b2_08 2b wie im Entwurf P12-250 Bild 01 (dort am Beispiel ae_b2_02): alles bis auf die
    /// innere Verzweigung im Ja-Zweig (die Bewertung zeigt sie rot). Gewaehlt ist die aeussere Verzweigung; ihr Nein-Zweig ist
    /// leer (∅) und kuerzer als der Ja-Zweig.
    /// </summary>
    /// <summary>Text der gewaehlten Verzweigung in <see cref="ProbeStruktogramm"/> (Start-Test: Text unveraendert, Auflage C).</summary>
    internal const string ProbeBedingung = "b.getAbholtag() = tag und b.getStatus() ≠ \"storniert\" ?";

    internal static (EchtStruktogramm Bau, string Gewaehlt) ProbeStruktogramm()
    {
        var bau = EchtStruktogramm.Neu();
        string Neu(string typ, string? wo, int ziel, string text)
        {
            var p = bau.Einfuegen(typ, wo, ziel)!;
            bau.Beschriften(p, text);
            return p;
        }
        var start = Neu(EchtStruktogramm.Anweisung, null, 0, "anzahl ← 0; rueckfragen ← 0");
        var schleife = Neu(EchtStruktogramm.Zaehlschleife, start, 0, "für i von 0 bis länge(bestellungen) − 1");
        var b = Neu(EchtStruktogramm.Anweisung, schleife, 2, "b ← bestellungen[i]");
        var wenn = Neu(EchtStruktogramm.Verzweigung, b, 0, ProbeBedingung);
        var ja = Neu(EchtStruktogramm.Anweisung, wenn, 2, "Ausgabe b.getGericht().getBezeichnung(), b.getGast().getPersonalNr()");
        Neu(EchtStruktogramm.Anweisung, ja, 0, "anzahl ← anzahl + 1");
        Neu(EchtStruktogramm.Anweisung, schleife, 0, "Ausgabe anzahl, rueckfragen");
        return (bau, wenn);
    }

    /// <summary>
    /// A 1.12.0 (Auflage G): eigene Probezeichnung mit allen sieben Bausteinen (Fallauswahl mit drei Faellen und sonst, ein
    /// leerer Zweig, ein kuerzerer Zweig mit Fuellraum). Gewaehlt ist der zweite Fall.
    /// </summary>
    internal static (EchtStruktogramm Bau, string Gewaehlt) ProbeStruktogrammAlle()
    {
        var bau = EchtStruktogramm.Neu();
        string Neu(string typ, string? wo, int ziel, string text)
        {
            var p = bau.Einfuegen(typ, wo, ziel)!;
            bau.Beschriften(p, text);
            return p;
        }
        var start = Neu(EchtStruktogramm.Anweisung, null, 0, "summe ← 0; i ← 0");
        var aufruf = Neu(EchtStruktogramm.Aufruf, start, 0, "ladeBestellungen(tag)");
        var solange = Neu(EchtStruktogramm.Solange, aufruf, 0, "solange i < länge(bestellungen)");
        var fall = Neu(EchtStruktogramm.Fallauswahl, solange, 2, "bestellungen[i].getStatus()");
        bau.Beschriften(fall + ".0", "\"offen\"");
        bau.Beschriften(fall + ".1", "\"abgeholt\"");
        var dritter = bau.FallNeu(fall)!;
        bau.Beschriften(dritter, "\"storniert\"");
        Neu(EchtStruktogramm.Anweisung, fall, 2, "Ausgabe „offen“");
        var abgeholt = Neu(EchtStruktogramm.Anweisung, fall, 3, "summe ← summe + 1");
        Neu(EchtStruktogramm.Anweisung, abgeholt, 0, "Ausgabe „abgeholt“");
        Neu(EchtStruktogramm.Anweisung, fall, 5, "Ausgabe „unbekannt“");
        Neu(EchtStruktogramm.Anweisung, fall, 0, "i ← i + 1");
        var zaehl = Neu(EchtStruktogramm.Zaehlschleife, solange, 0, "für k von 1 bis 3");
        var wiederhole = Neu(EchtStruktogramm.Wiederhole, zaehl, 2, "solange antwort = \"nein\"");
        Neu(EchtStruktogramm.Anweisung, wiederhole, 2, "antwort ← frage(k)");
        var wenn = Neu(EchtStruktogramm.Verzweigung, zaehl, 0, "summe > 0");
        Neu(EchtStruktogramm.Aufruf, wenn, 2, "druckeListe()");
        return (bau, fall + ".1");
    }

    /// <summary>
    /// A 1.8.0: Probezeichnung zu ae_b1_07 2c auf der Vorgabe (Termin, Tier): Termin–Tier richtig (0..* zu 1), Klasse
    /// "Erinnerung" mit zwei von drei Attributen und der Methode, ihre Beziehung zum Termin mit falscher Multiplizitaet (1 statt 0..*).
    /// </summary>
    internal static EchtBaukasten ProbeKlasse(EchtTeil teil)
    {
        var bau = EchtBaukasten.Neu(EchtZeichnung.Klasse, teil.Antwort.Vorgabe);
        bau.Verbinden("termin", "tier", "assoziation");
        bau.MultSetzen("termin", "tier", amVon: true, "0..*");
        bau.MultSetzen("termin", "tier", amVon: false, "1");
        var er = bau.Setzen("klasse", "termin")!;
        bau.Verschieben(er, 0, 2);
        bau.Beschriften(er, "Erinnerung");
        foreach (var (zeile, methode) in new[] { ("- kanal: String", false), ("- zeitpunkt: int", false), ("+ senden(): boolean", true) })
        {
            var n = bau.ZeileHinzufuegen(er, methode)!.Value;
            bau.ZeileSetzen(er, methode, n, zeile);
        }
        bau.Verbinden(er, "termin", "assoziation");
        bau.MultSetzen(er, "termin", amVon: true, "1");
        bau.MultSetzen(er, "termin", amVon: false, "1");
        return bau;
    }

    /// <summary>
    /// A 1.8.0: Probezeichnung zu ae_b1_06 4a auf der Vorgabe (Grenze, Kunde, "Rad ausleihen"): «include» zu "Anmelden" richtig,
    /// «extend» in falscher Richtung (Rueckgabe zu Schaden), Abokunde mit Generalisierung richtig; Kundendienst fehlt.
    /// </summary>
    internal static EchtBaukasten ProbeUseCase(EchtTeil teil)
    {
        var bau = EchtBaukasten.Neu(EchtZeichnung.UseCase, teil.Antwort.Vorgabe);
        string Fall(string typ, string text, int x, int y)
        {
            var id = bau.Setzen(typ)!;
            bau.Verschieben(id, x, y);
            bau.Beschriften(id, text);
            return id;
        }
        var anmelden = Fall("anwendungsfall", "Anmelden", 4, 0);
        var rueckgabe = Fall("anwendungsfall", "Rad zurückgeben", 2, 2);
        var schaden = Fall("anwendungsfall", "Schaden melden", 4, 2);
        var abo = Fall("akteur", "Abokunde", 0, 4);
        var verwalten = Fall("anwendungsfall", "Abo verwalten", 2, 4);
        bau.Verbinden("ausleihen", anmelden, "include");
        bau.Verbinden("kunde", rueckgabe, "assoziation");
        bau.Verbinden(rueckgabe, schaden, "extend");
        bau.Verbinden(abo, "kunde", "generalisierung");
        bau.Verbinden(abo, verwalten, "assoziation");
        return bau;
    }

    internal static EchtBaukasten ProbeEr(EchtTeil teil)
    {
        var bau = EchtBaukasten.Neu(EchtZeichnung.Er, teil.Antwort.Vorgabe);
        var besucht = bau.Setzen("beziehung", "tour")!;
        bau.Verschieben(besucht, 5, 2);
        bau.Beschriften(besucht, "besucht");
        var pat = bau.Setzen("entitaet", besucht)!;
        bau.Verschieben(pat, 5, 3);
        bau.Beschriften(pat, "Patient");
        var nr = bau.Setzen("attribut", pat)!;
        bau.Verschieben(nr, 4, 4);
        bau.Beschriften(nr, "Patientennummer");
        bau.Schluessel(nr, true);
        bau.Verbinden(nr, pat);
        bau.Verbinden(besucht, "tour");
        bau.Verbinden(besucht, pat);
        bau.Kardinalitaet(besucht, "tour", "n");
        bau.Kardinalitaet(besucht, pat, "m");
        bau.Kardinalitaet("faehrt", "pk", "1");
        bau.Kardinalitaet("faehrt", "tour", "n");
        return bau;
    }

    /// <summary>Kleines erfundenes UML-Aktivitaetsdiagramm (Stoerungsannahme) nur fuer den Start ohne Bedienung.</summary>
    internal const string ProbeUml = """
        {"art": "uml", "elemente": [
          {"id": "s", "typ": "start", "x": 1, "y": 0},
          {"id": "a1", "typ": "aktion", "text": "Störungsmeldung aufnehmen", "x": 1, "y": 1},
          {"id": "v", "typ": "verzweigung", "x": 1, "y": 2},
          {"id": "a2", "typ": "aktion", "text": "Bereitschaft anrufen", "x": 0, "y": 3},
          {"id": "a3", "typ": "aktion", "text": "Ticket mit Priorität „normal“ anlegen", "x": 2, "y": 3},
          {"id": "z", "typ": "zusammenfuehrung", "x": 1, "y": 4},
          {"id": "a4", "typ": "aktion", "text": "Rückmeldung an die Station", "x": 1, "y": 5},
          {"id": "e", "typ": "ende", "x": 1, "y": 6}],
         "verbindungen": [
          {"von": "s", "nach": "a1"}, {"von": "a1", "nach": "v"},
          {"von": "v", "nach": "a2", "text": "[dringend]"}, {"von": "v", "nach": "a3", "text": "[sonst]"},
          {"von": "a2", "nach": "z"}, {"von": "a3", "nach": "z"}, {"von": "z", "nach": "a4"}, {"von": "a4", "nach": "e"}]}
        """;

    /// <summary>Nur Start ohne Bedienung: Bestand mit einer Meldung der Ladepruefung (Platzhalter mit Meldung zeigen).</summary>
    internal void ProbeBestand(EchtBestand? bestand) => _probeBestand = bestand;

    /// <summary>Probe: Reiter oder Umschalter wechseln (Messpunkte), wie ein Klick.</summary>
    internal void ProbeReiter(string reiter, bool anlagen)
    {
        _reiter = reiter;
        _anlagen = anlagen;
        _reiterPillen?.Waehle(reiter);
        _umschalter?.Waehle(anlagen ? "n" : "a");
        if (Bewertet)
        {
            BewertungMalen();
        }
        else
        {
            LaufMalen();
        }
    }

    /// <summary>Probe: aktueller Reiter, Umschalter und Zwischenstand (Bedienprobe mit Maus und Finger).</summary>
    internal (string Reiter, bool Anlagen, string Summe) ProbeStand => (_reiter, _anlagen, _summe?.Text ?? "");

    internal Pillen? ProbeReiterPillen => _reiterPillen;

    internal Pillen? ProbeUmschalter => _umschalter;

    /// <summary>Probe 363: Beschriftung des Umschalters und Zahl der gezeigten Anlagen-Karten (nur mit Umschalter auf "Anlagen").</summary>
    internal (string Text, int Karten) ProbeAnlagenZahl => (_umschalter?.Text("n") ?? "",
        _inhalt?.Children.OfType<Grid>().FirstOrDefault() is { } spalten
            ? spalten.Children.OfType<StackPanel>().SelectMany(p => p.Children.OfType<Border>()).Count()
            : _inhalt?.Children.OfType<Border>().Count() ?? 0);

    /// <summary>Probe: der gezeigte Satz (null ohne Lauf).</summary>
    internal EchtSatz? ProbeSatz => _a.Satz;
    internal void ProbeFesthalten() => Festhalten();
    internal System.Text.Json.Nodes.JsonObject? ProbeZustandJetzt => _a.Zustand;

    internal StackPanel? ProbeInhalt => _inhalt;

    /// <summary>Probe: Text "x von y Teilaufgaben bearbeitet" (Bedienprobe "Auf Papier gezeichnet").</summary>
    internal string ProbeFortschritt => _fortschritt?.Text ?? "";

    /// <summary>Probe: stehen Aufgabe und Anlagen gerade nebeneinander, und sollten sie es bei dieser Breite?</summary>
    internal (bool Gezeigt, bool Soll) ProbeZweiSpalten => (_zweiSpalten, ZweiSpalten);

    // -- Bausteine ----------------------------------------------------------------------------------

    private static Border Zeile(Control inhalt) => new Border
    {
        Child = inhalt,
        Padding = new Thickness(14, 10),
        Margin = new Thickness(0, 10, 0, 0),
        CornerRadius = new CornerRadius(12),
        BorderThickness = new Thickness(1),
    }.Farbe(Border.BackgroundProperty, "Pinsel.card_alt").Farbe(Border.BorderBrushProperty, "Pinsel.border");

    private static T Abstand<T>(T element, double oben) where T : Control
    {
        element.Margin = new Thickness(0, oben, 0, 0);
        return element;
    }
}
