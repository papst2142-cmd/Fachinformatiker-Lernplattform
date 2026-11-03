using Avalonia.Controls;
using Avalonia.Media;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Texte;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// Phase 11 (Weisungen P11-29, P11-30, Sammelliste 202): Ablauf von "Bericht senden" unter "Problem melden".
/// Ohne eingebauten Schluessel nur der Hinweis "nicht verfuegbar". Sonst Rueckfrage mit freiem Feld "Was ist passiert?"
/// (hoechstens 2000 Zeichen), Senden im Hintergrund hinter "Bitte warten" (nichts darunter bedienbar), dann
/// Bestaetigung mit Dateiname oder Fehlertext. Nie von allein, nie ein Systemtext in einem Fenster.
/// BEFRISTET bis 1.0 (Weisung P11-29): siehe Core/Diagnose/BerichtSchluessel.cs.
/// </summary>
public static class BerichtSenden
{
    /// <summary>Inhalt der Rueckfrage: Text, Beschriftung (Name des Felds fuer den Screenreader), Feld, Hinweis.</summary>
    public static (Control Inhalt, TextBox Feld) Rueckfrage()
    {
        var feld = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 96,
            MaxHeight = 260,
            MaxLength = BerichtSendenTexte.FeldHoechst,
        }.Farbe(TextBox.ForegroundProperty, "Pinsel.text").Farbe(TextBox.BackgroundProperty, "Pinsel.card_alt").Groesse();
        var beschriftung = Schrift.Text(BerichtSendenTexte.Feld, "Normal", FontWeight.Bold).Fuer(feld);
        var inhalt = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Schrift.Text(BerichtSendenTexte.Rueckfrage),
                beschriftung,
                new BisZumRand(feld),
                Schrift.Text(BerichtSendenTexte.FeldHinweis, "Klein", FontWeight.Normal, "Pinsel.muted"),
            },
        };
        return (inhalt, feld);
    }

    /// <summary>Knopf gedrueckt: der ganze Ablauf. bericht baut den Text (im Hintergrund).</summary>
    public static async Task AusfuehrenAsync(Func<string> bericht, string geraet, string version)
    {
        if (!BerichtVersand.Verfuegbar)
        {
            await Dialog.Zeigen(BerichtSendenTexte.RueckfrageTitel, BerichtSendenTexte.NichtVerfuegbar, Dialog.Ok);
            return;
        }
        var (inhalt, feld) = Rueckfrage();
        if (await Dialog.Zeigen(BerichtSendenTexte.RueckfrageTitel, inhalt, BerichtSendenTexte.Senden, BerichtSendenTexte.Abbrechen) != 0)
        {
            return;
        }
        var beschreibung = feld.Text;
        var zeit = DateTime.Now;
        VersandErgebnis ergebnis;
        using (Dialog.Warten(BerichtSendenTexte.Sendet))
        {
            ergebnis = await Task.Run(() => BerichtVersand.Senden(BerichtVersand.Inhalt(bericht(), beschreibung), zeit, geraet, version));
        }
        var (titel, text) = BerichtVersand.Rueckmeldung(ergebnis);
        await Dialog.Zeigen(titel, text, Dialog.Ok);
    }
}
