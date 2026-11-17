using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Core.Texte;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.57.0 (Inhaltspakete W3, Weisung P12-297 W3-1): „Neue Stadt (Paket)“ im Fenster „Neues Projekt“ wie Nico es bedient:
/// Kopfknopf „Neues Projekt“, Art „Neue Stadt (Paket)“ (wie ein Screenreader), Name tippen, Kennung und Name des Pakets folgen
/// dem Projektnamen. Eine Kennung „stadt1“ lehnt das Fenster mit Grund ab (Stadt 1 bleibt geschuetzt, das Fenster bleibt offen);
/// danach legt es die Paket-Stadt an, und neben dem Titel steht die geschaetzte Paketgroesse. Gegenprobe: eine leere Stadt ohne
/// Paket zeigt keine Zeile.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task NeuOffenAsync(Hauptansicht a, bool offen)
    {
        for (var i = 0; i < 40 && DialogMitTitel(Texte.NeuTitel) != offen; i++)
        {
            await BildAsync(a);
        }
    }

    private static async Task PaketStadtAsync(Hauptansicht a)
    {
        Klicken(KopfKnopf(a, Texte.Neu));
        await NeuOffenAsync(a, true);
        if (!DialogMitTitel(Texte.NeuTitel) || a.NeuPaketKnopf is not { } paket || a.NeuKennungFeld is not { } kennung || a.NeuPaketNameFeld is not { } name)
        {
            Fund("Paket-Stadt: Fenster „Neues Projekt“ ohne „Neue Stadt (Paket)“");
            DialogSchliessen(Texte.Abbrechen);
            return;
        }
        var vorher = kennung.IsEnabled;
        ControlAutomationPeer.CreatePeerForElement(paket).GetProvider<ISelectionItemProvider>()?.Select();
        await BildAsync(a);
        var projektName = Lernplattform.Views.Dialog.Offen!.GetLogicalDescendants().OfType<TextBox>().First();
        projektName.Text = "Lindenau Probe";
        await BildAsync(a);
        Messwerte.Notiere("Paket-Stadt: Felder vorher " + (vorher ? "frei" : "gesperrt") + ", nach der Wahl " + (kennung.IsEnabled ? "frei" : "gesperrt")
            + ", Vorschlag Kennung „" + kennung.Text + "“, Name „" + name.Text + "“");
        if (vorher || !kennung.IsEnabled || !name.IsEnabled || kennung.Text != "lindenau_probe" || name.Text != "Lindenau Probe")
        {
            Fund("Paket-Stadt: Kennung/Name folgen nicht dem Projektnamen oder Felder nicht frei (Kennung „" + kennung.Text + "“, Name „" + name.Text + "“)");
        }
        // Stadt 1 bleibt geschuetzt: das Fenster bleibt mit dem Grund offen, nichts angelegt
        var projekt = a.Projekt;
        kennung.Text = AktiveFassung.Stadt1;
        DialogSchliessen(Texte.Anlegen);
        // Das Fenster schliesst und oeffnet mit dem Grund neu: warten, bis es wieder offen ist und den Grund zeigt
        var grund = StadtPaket.KennungFehler(AktiveFassung.Stadt1)!;
        bool Gezeigt() => Lernplattform.Views.Dialog.Offen?.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == grund) == true;
        for (var i = 0; i < 60 && !(DialogMitTitel(Texte.NeuTitel) && Gezeigt()); i++)
        {
            await BildAsync(a);
        }
        var gezeigt = DialogMitTitel(Texte.NeuTitel) && Gezeigt();
        Messwerte.Notiere("Paket-Stadt: Kennung stadt1 → " + (gezeigt ? "abgelehnt mit Grund" : "NICHT abgelehnt")
            + (gezeigt ? "" : " | Fenster " + (Lernplattform.Views.Dialog.Offen is null ? "zu" : "offen") + ", Paket gewaehlt " + paket.IsChecked
                + ", Name „" + projektName.Text + "“, Kennung „" + kennung.Text + "“, Texte: "
                + string.Join(" / ", (Lernplattform.Views.Dialog.Offen?.GetLogicalDescendants().OfType<TextBlock>() ?? []).Select(t => t.Text).Where(s => !string.IsNullOrEmpty(s)))));
        if (!gezeigt || !ReferenceEquals(projekt, a.Projekt))
        {
            Fund("Paket-Stadt: Kennung stadt1 nicht mit Grund abgelehnt");
        }
        kennung.Text = "lindenau_probe";
        DialogSchliessen(Texte.Anlegen);
        await NeuOffenAsync(a, false);
        await a.AussenGeprueft;
        for (var i = 0; i < 100 && a.PaketGroesseGeschaetzt is null; i++)
        {
            await Task.Delay(30);
            await BildAsync(a);
        }
        var p = a.Projekt;
        var zeile = a.PaketZeileText;
        var soll = a.PaketGroesseGeschaetzt is { } g ? Texte.PaketZeile("lindenau_probe", PaketTexte.Mb(g)) : null;
        Messwerte.Notiere("Paket-Stadt: angelegt " + (p?.Paket?.ToString() ?? "OHNE Paket") + ", Zeile „" + zeile + "“, geschaetzt " + a.PaketGroesseGeschaetzt + " Bytes");
        if (p?.Paket != ("lindenau_probe", "Lindenau Probe") || zeile != soll || a.PaketGroesseGeschaetzt is not > 0)
        {
            Fund("Paket-Stadt: nicht angelegt oder Zeile mit der geschaetzten Groesse fehlt („" + zeile + "“)");
        }
        // Gegenprobe: leere Stadt ohne Paket, keine Zeile
        a.NeuAnlegen("Starttest ohne Paket", null, (20, 16));
        await a.AussenGeprueft;
        await BildAsync(a);
        if (a.PaketZeileText.Length > 0 || a.Projekt?.Paket is not null)
        {
            Fund("Paket-Stadt: Gegenprobe ohne Paket zeigt „" + a.PaketZeileText + "“");
        }
    }
}
