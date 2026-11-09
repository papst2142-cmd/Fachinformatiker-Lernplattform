using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lernplattform.Core.Spiel.Ton;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// S 0.25.1 (Weisung P12-109, Sammelliste 252, Wunsch Nico): Menue „Einstellungen“ > „Bildschirm für Vollbild“ (nur am PC
/// ab zwei Bildschirmen). Fenster mit „Bildschirm des Fensters“ und je Bildschirm einem Eintrag (Groesse, Hauptbildschirm),
/// der gewaehlte ist markiert. Die Wahl steht in einstellungen.json des Werkzeugordners; ist das Vollbild an, wechselt es
/// sofort auf den gewaehlten Bildschirm, sonst beim naechsten Vollbild (auch nach einem Neustart).
/// </summary>
public sealed partial class Hauptansicht
{
    /// <summary>Menue-Eintrag „Bildschirm für Vollbild“ (Start-Test).</summary>
    public Button? BildschirmEintrag { get; private set; }

    /// <summary>Knoepfe im offenen Fenster der Wahl (Start-Test), sonst leer.</summary>
    public List<Button> BildschirmKnoepfe { get; } = [];

    internal async Task BildschirmWaehlenAsync()
    {
        var alle = WerkzeugVollbild.Liste();
        var eintraege = VollbildBildschirm.Eintraege(alle);
        var jetzt = VollbildBildschirm.Finden(alle, WerkzeugEinstellungen.VollbildBildschirm(_ordner)) is { } s ? s + 1 : 0;
        int? wahl = null;
        var liste = new StackPanel { Spacing = 6, Children = { Schrift.Text(Texte.VollbildBildschirmText, "Klein", farbe: "Pinsel.muted") } };
        BildschirmKnoepfe.Clear();
        for (var i = 0; i < eintraege.Count; i++)
        {
            var nummer = i;
            var text = i == jetzt ? "✓ " + eintraege[i] : eintraege[i];
            var k = Schrift.Knopf(text, betont: i == jetzt).Vorlese(i == jetzt ? Texte.Gewaehlt(eintraege[i]) : eintraege[i]);
            k.HorizontalAlignment = HorizontalAlignment.Stretch;
            k.Margin = new Thickness(0);
            k.Click += (_, _) =>
            {
                wahl = nummer;
                Dialog.AlleSchliessen();
            };
            liste.Children.Add(k);
            BildschirmKnoepfe.Add(k);
        }
        await Dialog.Zeigen(Texte.VollbildBildschirm, new ScrollViewer { Content = liste, MaxHeight = 420 }, Texte.Abbrechen);
        BildschirmKnoepfe.Clear();
        if (wahl is not { } w || w == jetzt)
        {
            return;
        }
        try
        {
            WerkzeugEinstellungen.VollbildBildschirmSetzen(_ordner, w == 0 ? null : alle[w - 1]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _ordner.Fehler("Vollbild-Bildschirm merken: " + e.Message);
            Melden(Texte.VollbildBildschirmFehler);
            return;
        }
        if (WerkzeugVollbild.An)
        {
            WerkzeugVollbild.Anwenden();
        }
    }
}
