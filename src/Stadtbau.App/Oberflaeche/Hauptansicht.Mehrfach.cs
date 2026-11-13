using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle W-B S 0.30.0 (Weisung P12-136): Karte „Ausgewähltes Stück“ rechts bei mehreren gewaehlten Stuecken (Alle drehen,
/// Alle kopieren, Alle löschen, Auswahl aufheben) und beim Setzen eines Baums oder Katalog-Stuecks der Teil „Als Reihe
/// setzen“ (Abstand, beide Seiten, zufaellig drehen, Infozeile mit „+n Dreiecke“).
/// </summary>
public sealed partial class Hauptansicht
{
    private readonly TextBlock _reiheInfo = Schrift.Text("", "Klein");
    private bool _reiheVerbunden;

    /// <summary>Start-Test: Infozeile der Reihe und die Knoepfe der Gruppe.</summary>
    internal string ReiheInfoText => _reiheInfo.Text ?? "";

    internal Button? GruppeDrehenKnopf { get; private set; }

    internal Button? GruppeKopierenKnopf { get; private set; }

    internal Button? GruppeLoeschenKnopf { get; private set; }

    internal CheckBox? ReiheSchalter { get; private set; }

    /// <summary>Zahl neben − und + der Reihe (Weisung P12-139 Frage 1 = b: folgt jeder Aenderung des Abstands).</summary>
    internal TextBlock? ReiheAbstandZahl { get; private set; }

    /// <summary>Gegenprobe P12-139: die Zahl folgt wie in S 0.30.0 nur den Knoepfen − und +.</summary>
    internal static bool ReiheZahlAltFuerTests { get; set; }

    /// <summary>Einmal: Infozeile und Hinweis folgen der Vorschau (ohne die ganze Karte neu zu bauen).</summary>
    private void ReiheVerbinden()
    {
        if (_reiheVerbunden)
        {
            return;
        }
        _reiheVerbunden = true;
        Karte.ReiheGeaendert += ReiheInfoZeigen;
    }

    private void ReiheInfoZeigen()
    {
        _reiheInfo.Text = Karte.ReiheVorschau is { } plan
            ? Texte.ReiheInfo(plan.Frei, plan.Ausgelassen, plan.Dreiecke)
            : Karte.ReiheAnfang is null ? Texte.ReiheAnfang : Texte.ReiheEnde;
        _reiheInfo.Vorlese(_reiheInfo.Text);
        if (ReiheAbstandZahl is { } zahl && !ReiheZahlAltFuerTests)
        {
            zahl.Text = Texte.ReiheAbstandText(Karte.ReiheWahl.Abstand);
        }
        _aussenHinweis.Text = SetzenHinweis();
    }

    /// <summary>Mehrere Stuecke gewaehlt: Karte rechts mit den gemeinsamen Aktionen (true = gezeigt).</summary>
    private bool GruppeTeil()
    {
        GruppeDrehenKnopf = GruppeKopierenKnopf = GruppeLoeschenKnopf = null;
        if (!Karte.GruppeAktiv || _projekt is null)
        {
            return false;
        }
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.GruppeGewaehlt(Karte.Gruppe.Count), "Normal", Avalonia.Media.FontWeight.Bold));
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.GruppeText, "Klein", farbe: "Pinsel.muted"));
        if (Karte.Gruppe.Count(s => s.Art == AussenArt.Hoehe) is var gemalt and > 0)
        {
            // S 0.51.0: wie viele gemalte Hoehenpunkte mitgehen (sie ruecken in Schritten 0,5)
            _aussenAuswahl.Children.Add(Schrift.Text(Texte.GemaltePunkte(gemalt) + ". " + Texte.GemaltRaster, "Klein", farbe: "Pinsel.muted"));
        }
        if (!_projekt.NurLesen)
        {
            GruppeDrehenKnopf = Knopf(Texte.GruppeDrehen, Texte.GruppeDrehenTipp, () => Karte.GruppeDrehen(), betont: true);
            GruppeKopierenKnopf = Knopf(Texte.GruppeKopieren, Texte.GruppeKopierenTipp, Karte.GruppeKopieren);
            GruppeLoeschenKnopf = Knopf(Texte.GruppeLoeschen, Texte.GruppeLoeschenTipp, Karte.GruppeLoeschen);
            _aussenAuswahl.Children.Add(new WrapPanel { Children = { GruppeDrehenKnopf, GruppeKopierenKnopf, GruppeLoeschenKnopf } });
        }
        // S 0.47.0 (Weisung P12-226 W1): auch fuer ein Projekt, das nur angesehen wird
        _aussenAuswahl.Children.Add(KopierenKnopf([.. Karte.Gruppe]));
        _aussenAuswahl.Children.Add(Knopf(Texte.AuswahlAufheben, Texte.AuswahlAufheben, () => Karte.Waehlen(null)));
        return true;

        static Button Knopf(string text, string tipp, Action aktion, bool betont = false)
        {
            var k = Schrift.Knopf(text, betont: betont).Vorlese(text).Mit(tipp);
            k.Click += (_, _) => aktion();
            return k;
        }
    }

    /// <summary>Beim Setzen eines Baums oder Katalog-Stuecks: Schalter „Als Reihe setzen“ und seine Einstellungen.</summary>
    private void ReiheTeil()
    {
        ReiheSchalter = null;
        ReiheAbstandZahl = null;
        if (!AussenBearbeitung.ReiheMoeglich(Karte.SetzenArt) || _projekt is null || _projekt.NurLesen)
        {
            return;
        }
        ReiheVerbinden();
        var schalter = new CheckBox { Content = Schrift.Text(Texte.ReiheSetzen), IsChecked = Karte.ReiheAn, MinHeight = 36 }
            .Vorlese(Texte.ReiheSetzen).Mit(Texte.ReiheSetzenTipp);
        schalter.IsCheckedChanged += (_, _) =>
        {
            Karte.ReiheAn = schalter.IsChecked == true;
            AussenAuswahlZeigen();
        };
        ReiheSchalter = schalter;
        _aussenAuswahl.Children.Add(schalter);
        if (!Karte.ReiheAn)
        {
            return;
        }
        var wahl = Karte.ReiheWahl;
        var abstand = Schrift.Text(Texte.ReiheAbstandText(wahl.Abstand), "Normal");
        abstand.VerticalAlignment = VerticalAlignment.Center;
        abstand.Margin = new Thickness(8, 0);
        ReiheAbstandZahl = abstand;
        var enger = Schrift.Knopf("−").Vorlese(Texte.ReiheEnger).Mit(Texte.ReiheEnger);
        var weiter = Schrift.Knopf("+").Vorlese(Texte.ReiheWeiter).Mit(Texte.ReiheWeiter);
        enger.Click += (_, _) => AbstandUm(-AussenBearbeitung.ReiheAbstandSchritt * (Karte.ReiheWahl.Abstand > 2 ? 5 : 1));
        weiter.Click += (_, _) => AbstandUm(AussenBearbeitung.ReiheAbstandSchritt * (Karte.ReiheWahl.Abstand >= 2 ? 5 : 1));
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.ReiheAbstand, "Klein", farbe: "Pinsel.muted"));
        _aussenAuswahl.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { enger, abstand, weiter } });
        var beide = new CheckBox { Content = Schrift.Text(Texte.ReiheBeideSeiten), IsChecked = wahl.BeideSeiten, MinHeight = 36 }
            .Vorlese(Texte.ReiheBeideSeiten).Mit(Texte.ReiheBeideSeitenTipp);
        beide.IsCheckedChanged += (_, _) => Karte.ReiheWahl = Karte.ReiheWahl with { BeideSeiten = beide.IsChecked == true };
        _aussenAuswahl.Children.Add(beide);
        if (Karte.SetzenArt == AussenArt.Katalog)
        {
            var zufall = new CheckBox { Content = Schrift.Text(Texte.ReiheZufaellig), IsChecked = wahl.Zufaellig, MinHeight = 36 }
                .Vorlese(Texte.ReiheZufaellig).Mit(Texte.ReiheZufaelligTipp);
            zufall.IsCheckedChanged += (_, _) => Karte.ReiheWahl = Karte.ReiheWahl with { Zufaellig = zufall.IsChecked == true };
            _aussenAuswahl.Children.Add(zufall);
        }
        ReiheInfoZeigen();
        _aussenAuswahl.Children.Add(_reiheInfo);

        void AbstandUm(double d)
        {
            Karte.ReiheWahl = Karte.ReiheWahl with { Abstand = Karte.ReiheWahl.Abstand + d };
            abstand.Text = Texte.ReiheAbstandText(Karte.ReiheWahl.Abstand);
        }
    }

    /// <summary>Vorgabe-Abstand, wenn ein anderes Stueck zum Setzen gewaehlt wird (Katalog: Stuecke stossen aneinander).</summary>
    private void ReiheVorgabeSetzen(AussenArt art, string? modell)
    {
        if (AussenBearbeitung.ReiheMoeglich(art))
        {
            Karte.ReiheWahl = Karte.ReiheWahl with { Abstand = AussenBearbeitung.ReiheVorgabe(art, modell) };
        }
    }
}
