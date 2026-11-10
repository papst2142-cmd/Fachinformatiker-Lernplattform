using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lernplattform;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Views;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.33.0 (Weisung P12-146, Sammelliste 273): ein Eintrag der Pruefliste. Ein Fund mit Stelle ist ein Knopf mit Pfeil
/// („Antippen zeigt die Stelle“), Antippen springt hin (Draufsicht: Ansicht mittig auf die Stelle und das Stueck waehlen; 3D:
/// Kamera faehrt hin, die Neigung bleibt; Grundriss: Stelle mittig; Editor: Ebene des Teils und Teil waehlen). Ein Fund ohne
/// Stelle ist sichtbar nicht antippbar (E1: grauer Kasten ohne Pfeil, kein Knopf). Beide haben einen Namen fuer den Screenreader.
/// </summary>
public static class FundListe
{
    /// <summary>Gegenprobe: Antippen springt nicht (wie bis S 0.32.0 in der Draufsicht nur waehlen; nur Start-Test).</summary>
    internal static bool SpringenAusFuerTests { get; set; }

    /// <summary>Eintrag fuer den Fund; <paramref name="springen"/> null = keine Stelle.</summary>
    public static Control Eintrag(StadtbauFund f, Action? springen)
    {
        var art = f.Rot ? "Rot: " : "Gelb: ";
        var texte = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Schrift.Text(art + f.Text, "Klein", FontWeight.Bold, f.Rot ? "Pinsel.red" : "Pinsel.orange"),
                Schrift.Text(f.Tipp, "Klein", farbe: "Pinsel.muted"),
            },
        };
        if (springen is null)
        {
            var kasten = new Border
            {
                Child = texte,
                Padding = new Thickness(8, 6),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                Tag = f,
            }.Farbe(Border.BackgroundProperty, "Pinsel.card").Farbe(Border.BorderBrushProperty, "Pinsel.border");
            return kasten.Vorlese(art + f.Text + ". " + f.Tipp + ". " + Texte.FundOhneStelle);
        }
        var pfeil = Schrift.Text("›", "Gross", FontWeight.Bold, "Pinsel.muted");
        pfeil.VerticalAlignment = VerticalAlignment.Center;
        pfeil.Margin = new Thickness(6, 0, 0, 0);
        DockPanel.SetDock(pfeil, Dock.Right);
        var k = new Button
        {
            Content = new DockPanel { Children = { pfeil, texte } },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 6),
            Tag = f,
        }.Vorlese(art + f.Text + ". " + f.Tipp + ". " + Texte.FundAntippen);
        k.Click += (_, _) =>
        {
            if (!SpringenAusFuerTests)
            {
                springen();
            }
        };
        return k;
    }
}
