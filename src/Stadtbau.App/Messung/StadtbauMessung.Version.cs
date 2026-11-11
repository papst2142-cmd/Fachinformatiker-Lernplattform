using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle G1 (Weisung P12-183, Sammelliste 288): die Version „S x.y.z“ steht in jeder Groesse und auf jeder Seite sichtbar in
/// der Titelzeile, ganz im Fenster und von keinem anderen Element der Titelzeile verdeckt. Gegenprobe: ohne Anzeige (wie bis
/// S 0.38.0) meldet die Pruefung einen Fund.
/// </summary>
public static partial class StadtbauMessung
{
    /// <summary>Gruende, warum die Versionsanzeige fehlt oder verdeckt ist (leer = in Ordnung).</summary>
    private static List<string> VersionGruende(Hauptansicht a)
    {
        var v = a.VersionKopf;
        var gruende = new List<string>();
        if (!v.IsEffectivelyVisible)
        {
            gruende.Add("nicht sichtbar");
            return gruende;
        }
        if (v.Text != Werkzeug.Anzeige)
        {
            gruende.Add("Text \"" + v.Text + "\" statt \"" + Werkzeug.Anzeige + "\"");
        }
        if (v.TranslatePoint(new Point(0, 0), a) is not { } p)
        {
            gruende.Add("Lage unbekannt");
            return gruende;
        }
        var r = new Rect(p, v.Bounds.Size);
        if (r.Width < 1 || r.Height < 1 || r.X < 0 || r.Y < 0 || r.Right > a.Bounds.Width + 0.5 || r.Bottom > a.Bounds.Height + 0.5)
        {
            gruende.Add(string.Create(Inv, $"nicht ganz im Fenster ({r.X:F0},{r.Y:F0} {r.Width:F0}x{r.Height:F0})"));
        }
        if (v.GetVisualParent() is Panel eltern)
        {
            foreach (var nachbar in eltern.Children.Where(c => c != v && c.IsEffectivelyVisible))
            {
                if (nachbar.TranslatePoint(new Point(0, 0), a) is { } q && new Rect(q, nachbar.Bounds.Size).Intersects(r))
                {
                    gruende.Add("verdeckt von " + nachbar.GetType().Name);
                }
            }
        }
        return gruende;
    }

    private static void VersionPruefen(Hauptansicht a, string wo)
    {
        foreach (var g in VersionGruende(a))
        {
            Fund("Versionsanzeige " + wo + ": " + g);
        }
    }

    /// <summary>Gegenprobe: ohne Anzeige meldet die Pruefung einen Fund.</summary>
    private static async Task VersionGegenprobeAsync(Hauptansicht a)
    {
        Hauptansicht.VersionAltFuerTests = true;
        try
        {
            a.TitelSetzen();
            await BildAsync(a);
            var gruende = VersionGruende(a);
            Messwerte.Notiere("Versionsanzeige Gegenprobe ohne Anzeige: " + (gruende.Count > 0 ? string.Join(", ", gruende) : "kein Fund"));
            if (gruende.Count == 0)
            {
                Fund("Versionsanzeige Gegenprobe: ohne Anzeige kein Fund");
            }
        }
        finally
        {
            Hauptansicht.VersionAltFuerTests = false;
            a.TitelSetzen();
            await BildAsync(a);
        }
        Messwerte.Notiere("Versionsanzeige: \"" + a.VersionKopf.Text + "\" in der Titelzeile");
    }
}
