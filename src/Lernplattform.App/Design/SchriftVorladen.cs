using Avalonia.Media;
using Lernplattform.Core.Diagnose;

namespace Lernplattform.Design;

/// <summary>
/// Welle „Ablauf stabil“ (Weisung P12-338 D1, Sammelliste 418): voller Lauf 1150 Versuch 1 brach am Intel-Mac im Start-Test
/// mit „Could not create glyphTypeface. Font family: $Default … Weight: Bold“ ab (einmal in 158 Laeufen). Die Schriften der
/// Oberflaeche (Roboto, Inter und die Systemschrift $Default, je normal und fett) werden darum einmal beim Start auf dem
/// Oberflaechen-Faden angelegt; danach liegen sie im Speicher der Schriftverwaltung und werden nicht mehr mitten in einem
/// Zeichenschritt zum ersten Mal gesucht. Laesst sich eine nicht anlegen, steht eine Zeile in fehler.log (nie still, nie ein
/// Absturz beim Start). Aendert keine Schrift und kein Aussehen.
/// </summary>
public static class SchriftVorladen
{
    /// <summary>Die Familien der Oberflaeche (gleich ContentControlThemeFontFamily in App.axaml) und die Systemschrift.</summary>
    public static readonly IReadOnlyList<string> Familien =
        ["avares://Lernplattform.App/Assets/Fonts#Roboto", "fonts:Inter#Inter", FontFamily.DefaultFontFamilyName];

    /// <summary>Legt jede Familie normal und fett an; Rueckgabe: die Schriften, die sich nicht anlegen liessen.</summary>
    public static IReadOnlyList<string> Laden()
    {
        var fehlt = new List<string>();
        foreach (var name in Familien)
        {
            foreach (var gewicht in new[] { FontWeight.Normal, FontWeight.Bold })
            {
                try
                {
                    if (!FontManager.Current.TryGetGlyphTypeface(new Typeface(new FontFamily(name), FontStyle.Normal, gewicht), out _))
                    {
                        fehlt.Add(name + " " + gewicht);
                    }
                }
                catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException)
                {
                    fehlt.Add(name + " " + gewicht + " (" + e.GetType().Name + ": " + e.Message + ")");
                }
            }
        }
        foreach (var f in fehlt)
        {
            Fehlerprotokoll.Schreiben("Schrift beim Start nicht ladbar: " + f);
        }
        return fehlt;
    }
}
