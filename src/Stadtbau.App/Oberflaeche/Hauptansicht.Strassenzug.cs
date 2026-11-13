using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.51.0 (Weisung P12-241, Nico 5c): beim Werkzeug „Straße ziehen“ rechts die Wahl „Ausstattung der Straße“: nur die
/// Straße, die drei festen Vorlagen und die eigenen aus dem Ordner <c>strassenzuege/</c> (eine Datei je Vorlage; eine
/// unlesbare steht mit Grund in der Liste und ist nicht waehlbar). Darunter „Eigene Vorlage zusammenstellen“: Name, Gehweg,
/// Baeume mit Abstand, Zebrastreifen, Haltestelle, „Vorlage speichern“. Eine eigene Vorlage laesst sich nach Rueckfrage loeschen.
/// </summary>
public sealed partial class Hauptansicht
{
    private readonly TextBlock _zugInfo = Schrift.Text("", "Klein");
    private bool _zugVerbunden;
    private bool _zugEigeneOffen;
    private ZugEntwurf _zugEntwurf = new();

    /// <summary>Was im Teil „Eigene Vorlage zusammenstellen“ gerade eingestellt ist (noch nicht gespeichert).</summary>
    private sealed class ZugEntwurf
    {
        public string Name { get; set; } = "";
        public bool Gehweg { get; set; } = true;
        public bool Baeume { get; set; } = true;
        public double BaumAbstand { get; set; } = Strassenzug.BaumAbstandVorgabe;
        public bool Zebrastreifen { get; set; }
        public bool Haltestelle { get; set; }
    }

    /// <summary>Start-Test: die Knoepfe der Wahl (in der Reihenfolge der Liste, erster = nur die Straße).</summary>
    internal List<RadioButton> ZugWahlKnoepfe { get; } = [];

    /// <summary>Start-Test: Zeilen der unlesbaren Dateien (mit Grund).</summary>
    internal List<TextBlock> ZugKaputtZeilen { get; } = [];

    internal Button? ZugEigeneKnopf { get; private set; }

    internal TextBox? ZugNameFeld { get; private set; }

    internal CheckBox? ZugGehwegSchalter { get; private set; }

    internal CheckBox? ZugBaeumeSchalter { get; private set; }

    internal CheckBox? ZugZebraSchalter { get; private set; }

    internal CheckBox? ZugHaltSchalter { get; private set; }

    internal Button? ZugSpeichernKnopf { get; private set; }

    internal Button? ZugLoeschenKnopf { get; private set; }

    internal TextBlock? ZugFehlerZeile { get; private set; }

    /// <summary>Infozeile unter der Wahl (Zahl der Stuecke, rote, geprueft).</summary>
    internal string ZugInfoText => _zugInfo.Text ?? "";

    private void ZugVerbinden()
    {
        if (_zugVerbunden)
        {
            return;
        }
        _zugVerbunden = true;
        Karte.ZiehenGeaendert += ZugInfoZeigen;
    }

    private void ZugInfoZeigen()
    {
        if (!Karte.ZugAktiv)
        {
            _zugInfo.Text = Karte.ZiehArtAktiv == ZiehArt.Strasse ? Texte.ZugLaternenHinweis : "";
            return;
        }
        _zugInfo.Text = Karte.ZugVorschau is { Gefunden: true } plan
            ? Texte.ZugInfo(plan.Frei, plan.Ausgelassen, plan.Nachgeprueft)
            : Texte.ZugGewaehlt(Karte.ZugVorlage!.Name) + ". " + Texte.ZugLaternenHinweis;
        _zugInfo.Vorlese(_zugInfo.Text);
    }

    /// <summary>Alle Vorlagen: feste zuerst, dann die eigenen und die unlesbaren Dateien.</summary>
    private List<Strassenzug.Eintrag> ZugEigene()
    {
        try
        {
            return Strassenzug.Alle(_ordner);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _ordner.Fehler("Straßenzug-Vorlagen lesen: " + e.GetType().Name + ": " + e.Message);
            return [];
        }
    }

    /// <summary>Teil rechts bei „Straße ziehen“.</summary>
    private void StrassenzugTeil()
    {
        ZugWahlKnoepfe.Clear();
        ZugKaputtZeilen.Clear();
        ZugEigeneKnopf = ZugSpeichernKnopf = ZugLoeschenKnopf = null;
        if (ZugNameFeld is { } altesFeld)
        {
            // der Name bleibt beim Neuaufbau (TextChanged kommt erst spaeter an)
            _zugEntwurf.Name = altesFeld.Text ?? "";
        }
        ZugNameFeld = null;
        ZugGehwegSchalter = ZugBaeumeSchalter = ZugZebraSchalter = ZugHaltSchalter = null;
        ZugFehlerZeile = null;
        if (Karte.ZiehArtAktiv != ZiehArt.Strasse || _projekt is null || _projekt.NurLesen)
        {
            return;
        }
        ZugVerbinden();
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.ZugAusstattung, "Normal", Avalonia.Media.FontWeight.Bold).Mit(Texte.ZugAusstattungTipp));
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.ZugAusstattungTipp, "Klein", farbe: "Pinsel.muted"));
        var eigene = ZugEigene();
        // die gewaehlte eigene Vorlage gibt es nicht mehr (geloescht, kaputt): zurueck auf nur die Straße
        if (Karte.ZugVorlage is { Fest: false } gewaehlt && !eigene.Any(e => e.Vorlage is { } v && v.Name == gewaehlt.Name && v.GleicheAusstattung(gewaehlt)))
        {
            Karte.ZugVorlage = null;
        }
        Wahl(null, Texte.ZugOhne, Texte.ZugOhneBeschreibung);
        foreach (var f in Strassenzug.Feste)
        {
            Wahl(f, f.Name, f.Beschreibung() + " · " + Texte.ZugFest);
        }
        foreach (var e in eigene)
        {
            if (e.Vorlage is { } v)
            {
                Wahl(v, v.Name, v.Beschreibung());
            }
            else
            {
                var zeile = Schrift.Text(Texte.ZugDateiFehler(e.Name, e.Fehler ?? ""), "Klein", farbe: "Pinsel.red");
                zeile.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
                ZugKaputtZeilen.Add(zeile);
                _aussenAuswahl.Children.Add(zeile);
            }
        }
        if (Karte.ZugVorlage is { Fest: false, Datei: { } datei } eigen)
        {
            var loeschen = Schrift.Knopf(Texte.ZugLoeschen).Vorlese(Texte.ZugLoeschen).Mit(Texte.ZugLoeschenFrage(eigen.Name));
            loeschen.Click += async (_, _) =>
            {
                if (await Dialog.Rueckfrage(Texte.ZugLoeschenTitel, Texte.ZugLoeschenFrage(eigen.Name), 1, Texte.Loeschen, Texte.Abbrechen) == 0)
                {
                    ZugLoeschen(datei, eigen.Name);
                }
            };
            ZugLoeschenKnopf = loeschen;
            _aussenAuswahl.Children.Add(loeschen);
        }
        ZugInfoZeigen();
        _zugInfo.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        _aussenAuswahl.Children.Add(_zugInfo);
        var auf = Schrift.Knopf(Texte.ZugEigene).Vorlese(Texte.ZugEigene).Mit(Texte.ZugEigeneTipp);
        auf.Click += (_, _) =>
        {
            _zugEigeneOffen = !_zugEigeneOffen;
            AussenAuswahlZeigen();
        };
        ZugEigeneKnopf = auf;
        _aussenAuswahl.Children.Add(auf);
        if (_zugEigeneOffen)
        {
            ZugEigeneTeil();
        }

        void Wahl(Strassenzug? vorlage, string name, string beschreibung)
        {
            var gewaehlt = vorlage is null ? Karte.ZugVorlage is null : Karte.ZugVorlage is { } k && k.Name == vorlage.Name && k.Fest == vorlage.Fest;
            var inhalt = new StackPanel
            {
                Children =
                {
                    Schrift.Text(name, "Normal"),
                    Schrift.Text(beschreibung, "Klein", farbe: "Pinsel.muted"),
                },
            };
            var knopf = new RadioButton { Content = inhalt, GroupName = "strassenzug", IsChecked = gewaehlt, MinHeight = 36 }
                .Vorlese(name + ", " + beschreibung).Mit(beschreibung);
            knopf.IsCheckedChanged += (_, _) =>
            {
                if (knopf.IsChecked == true && !ReferenceEquals(Karte.ZugVorlage, vorlage))
                {
                    Karte.ZugVorlage = vorlage;
                    Melden(Texte.ZugGewaehlt(vorlage?.Name ?? Texte.ZugOhne));
                    AussenAuswahlZeigen();
                }
            };
            ZugWahlKnoepfe.Add(knopf);
            _aussenAuswahl.Children.Add(knopf);
        }
    }

    /// <summary>Eigene Vorlage zusammenstellen und speichern.</summary>
    private void ZugEigeneTeil()
    {
        var e = _zugEntwurf;
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.ZugTitel, "Normal", Avalonia.Media.FontWeight.Bold));
        var feld = new TextBox { Text = e.Name, MaxLength = 40, HorizontalAlignment = HorizontalAlignment.Stretch };
        feld.TextChanged += (_, _) => _zugEntwurf.Name = feld.Text ?? "";
        ZugNameFeld = feld;
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.ZugName, "Klein").Fuer(feld));
        _aussenAuswahl.Children.Add(feld);
        ZugGehwegSchalter = Schalter(Texte.ZugMitGehweg, e.Gehweg, v =>
        {
            _zugEntwurf.Gehweg = v;
            if (!v)
            {
                _zugEntwurf.Zebrastreifen = false;
                _zugEntwurf.Haltestelle = false;
            }
        });
        ZugBaeumeSchalter = Schalter(Texte.ZugMitBaeumen, e.Baeume, v => _zugEntwurf.Baeume = v);
        if (e.Baeume)
        {
            var zahl = Schrift.Text(Texte.ZugAbstandText(e.BaumAbstand), "Normal");
            zahl.VerticalAlignment = VerticalAlignment.Center;
            zahl.Margin = new Thickness(8, 0);
            var enger = Schrift.Knopf("−").Vorlese(Texte.ZugBaumEnger).Mit(Texte.ZugBaumEnger);
            var weiter = Schrift.Knopf("+").Vorlese(Texte.ZugBaumWeiter).Mit(Texte.ZugBaumWeiter);
            enger.Click += (_, _) => Um(-Strassenzug.BaumAbstandSchritt);
            weiter.Click += (_, _) => Um(Strassenzug.BaumAbstandSchritt);
            _aussenAuswahl.Children.Add(Schrift.Text(Texte.ZugBaumAbstand, "Klein", farbe: "Pinsel.muted"));
            _aussenAuswahl.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { enger, zahl, weiter } });

            void Um(double d)
            {
                _zugEntwurf.BaumAbstand = Strassenzug.AbstandBegrenzt(_zugEntwurf.BaumAbstand + d);
                zahl.Text = Texte.ZugAbstandText(_zugEntwurf.BaumAbstand);
            }
        }
        ZugZebraSchalter = Schalter(Texte.ZugMitZebra, e.Zebrastreifen, v => _zugEntwurf.Zebrastreifen = v);
        ZugHaltSchalter = Schalter(Texte.ZugMitHalt, e.Haltestelle, v => _zugEntwurf.Haltestelle = v);
        if (!e.Gehweg)
        {
            ZugZebraSchalter.IsEnabled = false;
            ZugHaltSchalter.IsEnabled = false;
            _aussenAuswahl.Children.Add(Schrift.Text(Texte.ZugNurMitGehweg, "Klein", farbe: "Pinsel.muted"));
        }
        var fehler = Schrift.Text("", "Klein", farbe: "Pinsel.red");
        fehler.IsVisible = false;
        fehler.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        ZugFehlerZeile = fehler;
        var speichern = Schrift.Knopf(Texte.ZugSpeichern, betont: true).Vorlese(Texte.ZugSpeichern).Mit(Texte.ZugEigeneTipp);
        speichern.Click += (_, _) =>
        {
            if (ZugSpeichern() is { } grund)
            {
                fehler.Text = grund;
                fehler.IsVisible = true;
                Melden(grund);
            }
        };
        ZugSpeichernKnopf = speichern;
        _aussenAuswahl.Children.Add(speichern);
        _aussenAuswahl.Children.Add(fehler);

        CheckBox Schalter(string text, bool an, Action<bool> setzen)
        {
            var c = new CheckBox { Content = Schrift.Text(text), IsChecked = an, MinHeight = 36 }.Vorlese(text).Mit(text);
            c.IsCheckedChanged += (_, _) =>
            {
                setzen(c.IsChecked == true);
                AussenAuswahlZeigen();
            };
            _aussenAuswahl.Children.Add(c);
            return c;
        }
    }

    /// <summary>Den Entwurf als eigene Vorlage speichern und waehlen: null = gespeichert, sonst der Grund.</summary>
    internal string? ZugSpeichern()
    {
        if (ZugNameFeld is { } feld)
        {
            _zugEntwurf.Name = feld.Text ?? "";
        }
        var name = (_zugEntwurf.Name ?? "").Trim();
        if (!Strassenzug.NameGueltig(name))
        {
            return Texte.ZugKeinName;
        }
        if (Strassenzug.NameFest(name))
        {
            return Texte.ZugNameFest;
        }
        var neu = new Strassenzug
        {
            Name = name,
            Gehweg = _zugEntwurf.Gehweg,
            Baeume = _zugEntwurf.Baeume,
            BaumAbstand = _zugEntwurf.BaumAbstand,
            Zebrastreifen = _zugEntwurf.Gehweg && _zugEntwurf.Zebrastreifen,
            Haltestelle = _zugEntwurf.Gehweg && _zugEntwurf.Haltestelle,
        };
        try
        {
            neu.Speichern(_ordner, DateTime.Now);
        }
        catch (IOException ex)
        {
            return ex.Message == Texte.ZugGibtEs ? Texte.ZugGibtEs : Texte.ZugNichtLesbar;
        }
        catch (UnauthorizedAccessException)
        {
            return Texte.ZugNichtLesbar;
        }
        _zugEntwurf = new ZugEntwurf();
        _zugEigeneOffen = false;
        Karte.ZugVorlage = neu;
        Melden(Texte.ZugGespeichert(name));
        AussenAuswahlZeigen();
        return null;
    }

    /// <summary>Eigene Vorlage loeschen (nach der Rueckfrage; eine Kopie bleibt in den Sicherungen).</summary>
    internal void ZugLoeschen(string datei, string name)
    {
        try
        {
            Strassenzug.Loeschen(_ordner, datei, DateTime.Now);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Melden(Texte.ZugNichtLesbar);
            return;
        }
        Karte.ZugVorlage = null;
        Melden(Texte.ZugGeloescht(name));
        AussenAuswahlZeigen();
    }
}
