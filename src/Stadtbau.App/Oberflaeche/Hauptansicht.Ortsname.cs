using Avalonia.Controls;
using Avalonia.Layout;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.21.0 (Weisung P12-89, Nico 08.11.2026 14:06 MEZ): Feld „Name des Orts“ unter dem ausgewaehlten Stueck. Ein
/// Schritt im Verlauf (Rueckgaengig geht); leer, doppelt oder zu lang geht nicht (gleiche Regel wie die Pruefung,
/// <see cref="StadtbauPruefung.OrtNameNicht"/>). Das Spiel liest den Namen aus der Karte der Fassung.
/// </summary>
public sealed partial class Hauptansicht
{
    private void OrtNameTeil(AussenStelle s)
    {
        if (_projekt is null || s.Art != AussenArt.Haus || AussenBearbeitung.Ort(_projekt.AussenStand, s.Ort) is null)
        {
            return;
        }
        var feld = new TextBox
        {
            Text = _projekt.OrtName(s.Ort),
            MaxLength = StadtbauPruefung.OrtNameHoechstens + 10,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = !_projekt.NurLesen,
        };
        var fehler = Schrift.Text("", "Klein", farbe: "Pinsel.red");
        fehler.IsVisible = false;
        var knopf = Schrift.Knopf(Texte.OrtNameUebernehmen).Mit(Texte.OrtNameTipp);
        knopf.IsEnabled = !_projekt.NurLesen;
        knopf.Click += (_, _) =>
        {
            if (OrtUmbenennen(s.Ort, feld.Text ?? "") is { } grund)
            {
                fehler.Text = grund;
                fehler.IsVisible = true;
                Melden(grund);
            }
        };
        _aussenAuswahl.Children.Add(Schrift.Text(Texte.OrtNameFeld, "Klein").Fuer(feld));
        _aussenAuswahl.Children.Add(feld);
        _aussenAuswahl.Children.Add(knopf);
        _aussenAuswahl.Children.Add(fehler);
    }

    /// <summary>Ort umbenennen (auch fuer den Start-Test): null = umbenannt (ein Schritt), sonst der Grund.</summary>
    public string? OrtUmbenennen(string ort, string name)
    {
        if (_projekt is null || _projekt.NurLesen)
        {
            return Texte.OrtNichtGefunden;
        }
        var alt = _projekt.OrtName(ort);
        if (string.Equals(alt, name.Trim(), StringComparison.Ordinal))
        {
            return null;
        }
        // ein abgelehnter Name aendert nichts, also entsteht auch kein Schritt im Verlauf
        if (_projekt.AussenAendern(Texte.OrtUmbenannt, a => AussenBearbeitung.OrtUmbenennen(a, ort, name)) is { } grund)
        {
            return grund;
        }
        Melden(Texte.OrtUmbenannt + ": „" + alt + "“ → „" + name.Trim() + "“");
        return null;
    }
}
