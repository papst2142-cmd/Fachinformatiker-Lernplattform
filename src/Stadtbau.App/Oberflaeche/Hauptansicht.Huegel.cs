using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Gelaende (S 0.42.0, Weisung P12-192): Knopf „Hügel“ und Pinsel (Heben, Senken, Glätten, Größe) in „Natur und Licht“;
/// unter „Ausgewähltes Stück“ Radius, Höhe, Form und Länge des gewaehlten Huegels (jede Aenderung ein Schritt).
/// </summary>
public sealed partial class Hauptansicht
{
    /// <summary>Knopf „Hügel“ (Start-Test).</summary>
    internal Button? HuegelKnopf { get; private set; }

    /// <summary>S 0.51.0: Grund neben dem grauen Knopf „Hügel“ (nur sichtbar bei der Hoechstzahl).</summary>
    internal TextBlock? HuegelGrundZeile { get; private set; }

    private Control HuegelGrundBauen()
    {
        var t = Schrift.Text("", "Klein", farbe: "Pinsel.muted");
        t.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        t.IsVisible = false;
        HuegelGrundZeile = t;
        return t;
    }

    /// <summary>
    /// S 0.51.0 (Weisung P12-241): bei <see cref="Gelaende.HuegelHoechstens"/> Huegeln ist der Knopf grau und nennt den Grund
    /// (Tipp, Vorlesen und eine Zeile darunter). Ist gerade „Hügel“ zum Setzen gewaehlt, endet das Setzen mit dem Grund.
    /// </summary>
    private void HuegelKnopfAktualisieren()
    {
        if (HuegelKnopf is not { } knopf)
        {
            return;
        }
        var voll = _projekt is not null && AussenBearbeitung.HuegelVoll(_projekt.AussenStand);
        var grund = Texte.HuegelGenug(Gelaende.HuegelHoechstens);
        knopf.IsEnabled = !voll;
        var tipp = voll ? grund : Texte.HuegelTipp + " " + Texte.SetzenKnopfTipp;
        ToolTip.SetTip(knopf, new TextBlock { Text = tipp, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 340 });
        Avalonia.Automation.AutomationProperties.SetHelpText(knopf, tipp);
        Avalonia.Automation.AutomationProperties.SetName(knopf, voll ? Texte.HuegelSetzen + ", " + Texte.HuegelGenugKurz : Texte.HuegelSetzen);
        if (HuegelGrundZeile is { } zeile)
        {
            zeile.Text = grund;
            zeile.IsVisible = voll;
        }
        if (voll && Karte.SetzenArt == AussenArt.Huegel)
        {
            SetzenBeenden();
            Melden(grund);
        }
    }

    /// <summary>Start-Test: Pinsel-Knoepfe Heben, Senken, Glaetten.</summary>
    internal Dictionary<PinselArt, Button> PinselKnoepfe { get; } = [];

    private TextBlock? _pinselGroesseText;

    /// <summary>Start-Test: Knoepfe der drei Staerken (schwach, mittel, stark).</summary>
    internal List<Button> PinselStaerkeKnoepfe { get; } = [];

    private static string PinselName(PinselArt art) => art switch
    {
        PinselArt.Heben => Texte.PinselHeben,
        PinselArt.Senken => Texte.PinselSenken,
        _ => Texte.PinselGlaetten,
    };

    private static string Zahl(double wert) => wert.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));

    /// <summary>Baustein „Gelände malen“: drei Pinsel und die Größe (− und +).</summary>
    private Control PinselBaustein()
    {
        var reihe = new WrapPanel();
        foreach (var art in new[] { PinselArt.Heben, PinselArt.Senken, PinselArt.Glaetten })
        {
            var k = new Button { Content = Schrift.Text(PinselName(art), "Klein"), Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(8, 4) }
                .Vorlese(Texte.Pinsel + " " + PinselName(art)).Mit(Texte.PinselTipp);
            k.Click += (_, _) => PinselWaehlen(art);
            PinselKnoepfe[art] = k;
            reihe.Children.Add(k);
        }
        _pinselGroesseText = Schrift.Text(Zahl(Karte.PinselGroesse), "Normal");
        var kleiner = Schrift.Knopf("−").Vorlese(Texte.PinselGroesse + " kleiner").Mit(Texte.PinselTipp);
        var groesser = Schrift.Knopf("+").Vorlese(Texte.PinselGroesse + " größer").Mit(Texte.PinselTipp);
        kleiner.Click += (_, _) => PinselGroesseSchritt(-1);
        groesser.Click += (_, _) => PinselGroesseSchritt(1);
        Karte.PinselGeaendert += PinselMarkieren;
        var staerken = new WrapPanel();
        PinselStaerkeKnoepfe.Clear();
        for (var i = 0; i < AussenBearbeitung.PinselStaerken.Count; i++)
        {
            var wert = AussenBearbeitung.PinselStaerken[i];
            var name = Texte.PinselStaerkeName(i, wert);
            var k = new Button { Content = Schrift.Text(name, "Klein"), Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(8, 4) }
                .Vorlese(Texte.PinselStaerkeTitel + " " + name).Mit(Texte.PinselStaerkeTipp);
            k.Click += (_, _) => PinselStaerkeWaehlen(wert);
            PinselStaerkeKnoepfe.Add(k);
            staerken.Children.Add(k);
        }
        StaerkeMarkieren();
        return new StackPanel
        {
            Children =
            {
                Gruppe(Schrift.Text(Texte.Pinsel, "Klein", Avalonia.Media.FontWeight.Bold), Texte.PinselTipp),
                reihe,
                Schrift.Text(Texte.PinselGroesse, "Klein"),
                new WrapPanel { Children = { kleiner, _pinselGroesseText, groesser } },
                Schrift.Text(Texte.PinselStaerkeTitel, "Klein"),
                staerken,
            },
        };
    }

    /// <summary>Pinsel waehlen; derselbe Knopf noch einmal beendet den Pinsel (auch fuer den Start-Test).</summary>
    internal void PinselWaehlen(PinselArt art)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return;
        }
        if (Karte.GelaendePinsel == art)
        {
            Karte.GelaendePinsel = null;
            return;
        }
        KnopfMarkieren(false);
        _setzenKnopf = null;
        EbeneFuerSetzenAn(AussenArt.Huegel);
        Karte.GelaendePinsel = art;
    }

    /// <summary>Staerke des Pinsels waehlen (auch fuer den Start-Test); der Knopf der gewaehlten Staerke ist markiert.</summary>
    internal void PinselStaerkeWaehlen(float wert)
    {
        Karte.PinselStaerke = wert;
        StaerkeMarkieren();
    }

    private void StaerkeMarkieren()
    {
        for (var i = 0; i < PinselStaerkeKnoepfe.Count; i++)
        {
            var k = PinselStaerkeKnoepfe[i];
            var wert = AussenBearbeitung.PinselStaerken[i];
            var name = Texte.PinselStaerkeTitel + " " + Texte.PinselStaerkeName(i, wert);
            if (Math.Abs(Karte.PinselStaerke - wert) < 1e-4f)
            {
                k.FarbeNeu(Button.BackgroundProperty, "VerlaufW.primary").FarbeNeu(Button.ForegroundProperty, "Pinsel.on_accent");
                (k.Content as TextBlock)?.FarbeNeu(TextBlock.ForegroundProperty, "Pinsel.on_accent");
                k.Vorlese(name + ", " + Texte.SetzenGewaehlt);
            }
            else
            {
                Markierung.Loesen(k, Button.BackgroundProperty);
                Markierung.Loesen(k, Button.ForegroundProperty);
                (k.Content as TextBlock)?.FarbeNeu(TextBlock.ForegroundProperty, "Pinsel.text");
                k.Vorlese(name);
            }
        }
    }

    private void PinselGroesseSchritt(int richtung)
    {
        var liste = AussenBearbeitung.PinselGroessen;
        var i = liste.ToList().IndexOf(Karte.PinselGroesse);
        i = Math.Clamp((i < 0 ? 1 : i) + richtung, 0, liste.Count - 1);
        Karte.PinselGroesse = liste[i];
        _pinselGroesseText?.Text = Zahl(liste[i]);
    }

    /// <summary>Gewaehlten Pinsel markieren wie einen gewaehlten Setzen-Knopf (Name mit „, gewählt“).</summary>
    private void PinselMarkieren()
    {
        foreach (var (art, k) in PinselKnoepfe)
        {
            var an = Karte.GelaendePinsel == art;
            var name = Texte.Pinsel + " " + PinselName(art);
            if (an)
            {
                k.FarbeNeu(Button.BackgroundProperty, "VerlaufW.primary").FarbeNeu(Button.ForegroundProperty, "Pinsel.on_accent");
                (k.Content as TextBlock)?.FarbeNeu(TextBlock.ForegroundProperty, "Pinsel.on_accent");
                k.Vorlese(name + ", " + Texte.SetzenGewaehlt);
            }
            else
            {
                Markierung.Loesen(k, Button.BackgroundProperty);
                Markierung.Loesen(k, Button.ForegroundProperty);
                (k.Content as TextBlock)?.FarbeNeu(TextBlock.ForegroundProperty, "Pinsel.text");
                k.Vorlese(name);
            }
        }
    }

    /// <summary>Start-Test: Knoepfe des gewaehlten Huegels (null = kein Huegel gewaehlt).</summary>
    internal (Button RadiusWeniger, Button RadiusMehr, Button HoeheWeniger, Button HoeheMehr, Button Rund, Button Laenglich)? HuegelKnoepfe { get; private set; }

    private void HuegelTeil(AussenStelle s)
    {
        HuegelKnoepfe = null;
        if (_projekt is null || s.Art != AussenArt.Huegel || AussenBearbeitung.HuegelVon(_projekt.AussenStand, s) is null)
        {
            return;
        }
        var (radius, hoehe, laenglich, laenge, _) = AussenBearbeitung.HuegelWerte(_projekt.AussenStand, s);
        var an = !_projekt.NurLesen;
        Button Klein(string text, string name, bool erlaubt, Action klick)
        {
            var k = Schrift.Knopf(text).Vorlese(name).Mit(Texte.HuegelWerteTipp);
            k.IsEnabled = an && erlaubt;
            k.Click += (_, _) => klick();
            return k;
        }
        var rW = Klein("−", Texte.HuegelRadius + " kleiner", radius > Gelaende.RadiusMin, () => HuegelAendern(s, radius - 0.5, hoehe, laenglich, laenge));
        var rM = Klein("+", Texte.HuegelRadius + " größer", radius < Gelaende.RadiusMax, () => HuegelAendern(s, radius + 0.5, hoehe, laenglich, laenge));
        var hW = Klein("−", Texte.HuegelHoehe + " niedriger", hoehe > Gelaende.HoeheMin, () => HuegelAendern(s, radius, hoehe - 0.1, laenglich, laenge));
        var hM = Klein("+", Texte.HuegelHoehe + " höher", hoehe < Gelaende.HoeheMax, () => HuegelAendern(s, radius, hoehe + 0.1, laenglich, laenge));
        var rund = Schrift.Knopf(Texte.HuegelRund, betont: !laenglich).Vorlese(Texte.HuegelForm + " " + Texte.HuegelRund).Mit(Texte.HuegelWerteTipp);
        var lang = Schrift.Knopf(Texte.HuegelLaenglich, betont: laenglich).Vorlese(Texte.HuegelForm + " " + Texte.HuegelLaenglich).Mit(Texte.HuegelWerteTipp);
        rund.IsEnabled = lang.IsEnabled = an;
        rund.Click += (_, _) => HuegelAendern(s, radius, hoehe, false, laenge);
        lang.Click += (_, _) => HuegelAendern(s, radius, hoehe, true, Math.Max(laenge, 2 * radius + 1));
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.HuegelRadius, "Klein"));
        _aussenAuswahl.Children.Add(new WrapPanel { Children = { rW, Schrift.Text(Zahl(radius), "Normal"), rM } });
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.HuegelHoehe, "Klein"));
        _aussenAuswahl.Children.Add(new WrapPanel { Children = { hW, Schrift.Text(Zahl(hoehe), "Normal"), hM } });
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.HuegelForm, "Klein"));
        _aussenAuswahl.Children.Add(new WrapPanel { Children = { rund, lang } });
        if (laenglich)
        {
            var lW = Klein("−", Texte.HuegelLaenge + " kürzer", laenge > 2 * radius, () => HuegelAendern(s, radius, hoehe, true, laenge - 0.5));
            var lM = Klein("+", Texte.HuegelLaenge + " länger", laenge < Gelaende.LaengeMax, () => HuegelAendern(s, radius, hoehe, true, laenge + 0.5));
            _aussenAuswahl.Children.Add(Schrift.Text(Texte.HuegelLaenge, "Klein"));
            _aussenAuswahl.Children.Add(new WrapPanel { Children = { lW, Schrift.Text(Zahl(laenge), "Normal"), lM } });
        }
        HuegelKnoepfe = (rW, rM, hW, hM, rund, lang);
    }

    /// <summary>Werte des Huegels setzen (ein Schritt; auch fuer den Start-Test). false = ungueltig oder kein Projekt.</summary>
    internal bool HuegelAendern(AussenStelle s, double radius, double hoehe, bool laenglich, double laenge)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return false;
        }
        var vorher = AussenBearbeitung.HuegelWerte(_projekt.AussenStand, s);
        if (!_projekt.AussenAendern(Texte.HuegelGeaendert, a =>
        {
            AussenBearbeitung.HuegelWerteSetzen(a, s, radius, hoehe, laenglich, laenge);
            return AussenBearbeitung.HuegelWerte(a, s) != vorher;
        }))
        {
            return false;
        }
        Melden(Texte.HuegelGeaendert);
        Karte.Waehlen(s);
        return true;
    }
}
