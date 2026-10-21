using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;
using Lernplattform.Design;
using Lernplattform.Programm;

namespace Lernplattform.Views;

/// <summary>
/// Phase 2, L10: Bereich „Abgleich PC und Handy“ in den Optionen (Weisung P2-12), PC = Handy:
/// Repository und Zugangsschlüssel (ohne Platzhalter, Schlüssel verdeckt), „Speichern und
/// abgleichen“ mit Statuszeile, Mischbetrieb-Zeile, Schalter „Automatisch abgleichen“, Hinweis
/// mit T6. Texte aus Core.Texte.AbgleichTexte, nie ein Systemtext.
/// </summary>
public sealed class AbgleichTeil
{
    private readonly TextBox _repo;
    private readonly TextBox _token;
    private readonly Button _knopf;
    private readonly TextBlock _status;
    private readonly TextBlock _misch;
    private readonly ToggleSwitch _auto;
    private bool _setzen;

    public AbgleichTeil()
    {
        var werte = Zustand.Werte;
        _repo = Feld(Text(werte, Abgleich.RepoSchluessel), AbgleichTexte.Repository);
        _token = Feld(Text(werte, Abgleich.TokenSchluessel), AbgleichTexte.Token);
        _token.PasswordChar = '•';
        _knopf = Schrift.Knopf(AbgleichTexte.Knopf, true);
        _knopf.Click += (_, _) => JetztAbgleichen();
        _status = Schrift.Text("", "Normal", FontWeight.Normal, "Pinsel.text_dim");
        _misch = Schrift.Text("", "Normal", FontWeight.Normal, "Pinsel.text_dim");
        _auto = new ToggleSwitch
        {
            Content = Schrift.Text(AbgleichTexte.Automatisch),
            IsChecked = AutoWert(werte),
            OnContent = null,
            OffContent = null,
        };
        Avalonia.Automation.AutomationProperties.SetName(_auto, AbgleichTexte.Automatisch);
        _auto.IsCheckedChanged += (_, _) => AutoGeaendert();
        Teil = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Schrift.Text(AbgleichTexte.Repository, "Normal", FontWeight.Normal, "Pinsel.text_dim"),
                Links(_repo),
                Schrift.Text(AbgleichTexte.Token, "Normal", FontWeight.Normal, "Pinsel.text_dim"),
                Links(_token),
                new WrapPanel { Margin = new Avalonia.Thickness(0, 6, 0, 0), Children = { _knopf } },
                _status,
                _misch,
                _auto,
                Schrift.Text(AbgleichTexte.Hinweis + " " + AbgleichTexte.Zugangsschluessel, "Klein", FontWeight.Normal, "Pinsel.muted"),
            },
        };
        // nur angemeldet, solange der Bereich sichtbar im Fenster haengt (kein Abonnent bleibt zurueck)
        Teil.AttachedToVisualTree += (_, _) =>
        {
            AbgleichDienst.Fertig += Zeigen;
            Stand();        // was inzwischen (ohne Anzeige) geschah
        };
        Teil.DetachedFromVisualTree += (_, _) => AbgleichDienst.Fertig -= Zeigen;
        Stand();
    }

    private void Stand()
    {
        if (AbgleichDienst.Laeuft)
        {
            _knopf.IsEnabled = false;
            Status(AbgleichTexte.GleicheAb, "Pinsel.text_dim");
        }
        else
        {
            Zeigen(AbgleichDienst.Letztes?.Ergebnis, AbgleichDienst.Letztes?.Fehler);
        }
    }

    public Control Teil { get; }

    private static TextBox Feld(string wert, string name)
    {
        var feld = new TextBox { Text = wert, HorizontalAlignment = HorizontalAlignment.Stretch }
            .Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt").Groesse();
        Avalonia.Automation.AutomationProperties.SetName(feld, name);
        return feld;
    }

    /// <summary>Feld links, so breit wie Platz ist, höchstens 520 Punkte (am Handy ganze Breite).</summary>
    private static Grid Links(Control feld)
    {
        var raster = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star) { MaxWidth = 520 }, new ColumnDefinition(GridLength.Auto)],
        };
        raster.Children.Add(feld);
        return raster;
    }

    private static string Text(JsonObject werte, string schluessel) =>
        werte[schluessel] is JsonValue v && v.TryGetValue<string>(out var text) ? text : "";

    private static bool AutoWert(JsonObject werte)
    {
        var mitToken = new JsonObject { [Abgleich.RepoSchluessel] = "a/b", [Abgleich.TokenSchluessel] = "x" };
        if (werte.TryGetPropertyValue(Abgleich.AutoSchluessel, out var wert))
        {
            mitToken[Abgleich.AutoSchluessel] = wert?.DeepClone();
        }
        return Abgleich.AutoAn(mitToken);
    }

    private void Status(string text, string farbe)
    {
        _status.Text = text;
        _status.Farbe(TextBlock.ForegroundProperty, farbe);
    }

    /// <summary>sync_now: Eingaben speichern, prüfen, abgleichen.</summary>
    private void JetztAbgleichen()
    {
        var repo = (_repo.Text ?? "").Trim();
        var token = (_token.Text ?? "").Trim();
        if (!Zustand.Aendere(w =>
            {
                w[Abgleich.RepoSchluessel] = repo;
                w[Abgleich.TokenSchluessel] = token;
            }))
        {
            // nicht mit dem alten Repository abgleichen (B-G-04)
            Status(Optionen.SpeichernFehlgeschlagen, "Pinsel.red");
            return;
        }
        if (!Abgleich.Eingerichtet(Zustand.Werte))
        {
            Status(AbgleichTexte.BitteEintragen, "Pinsel.yellow");
            return;
        }
        _knopf.IsEnabled = false;
        Status(AbgleichTexte.GleicheAb, "Pinsel.text_dim");
        AbgleichDienst.Jetzt();
    }

    private void AutoGeaendert()
    {
        if (_setzen)
        {
            return;
        }
        if (!Zustand.Setze(Abgleich.AutoSchluessel, _auto.IsChecked == true))
        {
            _setzen = true;
            _auto.IsChecked = !_auto.IsChecked;
            _setzen = false;
            _ = Dialog.Zeigen(Optionen.SpeichernFehlgeschlagenTitel, Optionen.SpeichernFehlgeschlagen, Dialog.Ok);
        }
    }

    /// <summary>show_sync_status: nach jedem Abgleich (auch automatisch) und beim Bauen.</summary>
    private void Zeigen(AbgleichErgebnis? ergebnis, AbgleichAusnahme? fehler)
    {
        _knopf.IsEnabled = true;
        var werte = Zustand.Werte;
        var eingerichtet = Abgleich.Eingerichtet(werte);
        var zuletzt = Text(werte, Abgleich.ZuletztSchluessel);
        var misch = AbgleichTexte.Mischbetrieb(eingerichtet, zuletzt, MischWert(), Uhr.System);
        _misch.Text = misch;
        _misch.IsVisible = misch.Length > 0;
        if (fehler is not null)
        {
            Status(AbgleichTexte.Fehler(fehler, Text(werte, Abgleich.RepoSchluessel).Trim().Trim('/')), "Pinsel.red");
            return;
        }
        if (!eingerichtet)
        {
            Status(AbgleichTexte.NichtEingerichtet, "Pinsel.muted");
        }
        else if (ergebnis is not null)
        {
            Status(AbgleichTexte.Ergebnis(ergebnis), "Pinsel.green");
        }
        else
        {
            Status(AbgleichTexte.Status(true, zuletzt), zuletzt.Length > 0 ? "Pinsel.text_dim" : "Pinsel.muted");
        }
    }

    /// <summary>Wert „mischbetrieb“ aus sync_meta (nur lesen; im Zweifel null).</summary>
    private static object? MischWert() =>
        Zustand.Lernstand.Abfragen("SELECT value FROM sync_meta WHERE key = $k", ("$k", Loeschmarken.MischbetriebSchluessel)) is [var zeile, ..]
            ? zeile[0] : null;
}
