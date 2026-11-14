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
/// </summary>
public sealed class EchtFormatTeil
{
    private const string Ausgang = "ausgang";
    private const string Zeit = "zeit";

    private readonly StackPanel _koerper;
    private readonly Action _leeren;
    private readonly Action _zurAuswahl;
    private readonly Func<bool> _andereLaeuft;
    private readonly EchtAblauf _a;
    private readonly DispatcherTimer _takt = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _speichern = new() { Interval = TimeSpan.FromMilliseconds(1200) };
    private readonly Dictionary<string, TextBox> _felder = new(StringComparer.Ordinal);
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

    private async Task StartenAsync(IReadOnlyList<EchtSatz> saetze, string art, Pruefungsbereich bereich)
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
        var satz = EchtAblauf.NaechsterSatz(saetze, art, Gesamtstand.Verlauf(Zustand.Lernstand))!;
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
        _umschalter = new Pillen([("a", EchtTexte.AufgabeUmschalter), ("n", Beschriftungen.Einsetzen(EchtTexte.Anlagen, satz.Anlagen.Count))],
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
        _umschalter!.IsVisible = _reiter is not (Ausgang or Zeit);
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
            if (_anlagen)
            {
                foreach (var anlage in AnlagenVon(satz, aufgabe))
                {
                    _inhalt.Children.Add(AnlageKarte(anlage));
                }
            }
            else
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
                _inhalt.Children.Add(Schrift.KopfKarte(Beschriftungen.Einsetzen(EchtTexte.AufgabeTitel, n, aufgabe.Titel),
                    EchtTexte.PunkteText(aufgabe.Punkte), inhalt));
            }
        }
        FortschrittZeigen();
    }

    /// <summary>Anlagen, auf die die Aufgabe verweist (alle, wenn sie auf keine verweist).</summary>
    private static IEnumerable<EchtAnlage> AnlagenVon(EchtSatz satz, EchtAufgabe aufgabe)
    {
        var nr = aufgabe.Teile.SelectMany(t => t.Anlagen).ToHashSet(StringComparer.Ordinal);
        return nr.Count == 0 ? satz.Anlagen : satz.Anlagen.Where(a => nr.Contains(a.Nr));
    }

    private static Border AnlageKarte(EchtAnlage anlage)
    {
        var inhalt = new StackPanel { Spacing = 6 };
        switch (anlage.Art)
        {
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
        }
        return feld;
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
        if (_a.Festhalten(_felder.Select(f => (f.Key, f.Value.Text))))
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
            inhalt.Children.Add(EigeneAntwort(n, teil, zustand, wertung));
            if (teil.Punkte - teil.SelbstPunkte > 0)
            {
                inhalt.Children.Add(Schrift.Text(Beschriftungen.Einsetzen(EchtTexte.ProgrammPunkte, wertung.Programm, teil.Punkte - teil.SelbstPunkte),
                    "Klein", FontWeight.Bold, wertung.Programm == teil.Punkte - teil.SelbstPunkte ? "Pinsel.green" : "Pinsel.red"));
            }
            inhalt.Children.Add(Abstand(LernTeile.Label(EchtTexte.Loesung), 8));
            foreach (var zeile in teil.Loesung)
            {
                inhalt.Children.Add(new TabellenText(zeile, "Klein", FontWeight.Normal, "Pinsel.text_soft"));
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
    ];

    /// <summary>Probezustand: Probesatz, 754 s gelaufen, Antworten teils richtig, teils falsch, teils leer.</summary>
    internal JsonObject ProbeZustand(bool bewertet)
    {
        _probeBestand = null;
        var satz = Zustand.EchtBestand.Saetze.First(s => s.Bereich == EchtSaetze.Ap1);
        var zustand = EchtAblauf.Neu(satz, EchtArten.Praefix + EchtSaetze.Ap1, DateTimeOffset.Now.AddSeconds(-754), TimeZoneInfo.Local);
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
            default:
                var zustand = ProbeZustand(bewertet: true);
                var ergebnis = EchtBewertung.Auswerten(_a.Bestand.Satz(Json.Text(zustand["satz"]))!, zustand);
                _a.ProbeSetzen(zustand, ergebnis);
                ErgebnisZeigen(Json.Text(zustand["art"])!, ergebnis);
                break;
        }
        _takt.Stop();
    }

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

    internal StackPanel? ProbeInhalt => _inhalt;

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
