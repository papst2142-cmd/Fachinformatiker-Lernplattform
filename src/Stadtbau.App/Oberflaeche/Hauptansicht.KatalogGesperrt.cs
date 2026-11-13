using Avalonia.Controls;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.52.0 (Weisung P12-243 Punkt 2, Sammelliste 337): der Knopf „Hügel“ sah bei 16 Huegeln genauso aus wie frei
/// (RGB 204/204/204, gleiche Schrift), weil die Knoepfe Schrift und Flaeche selbst setzen und das graue Aussehen von Fluent
/// darum nicht greift. Jetzt tragen die Katalog-Flaechen (Draufsicht/3D links, Grundriss links, „Voller Katalog“) die
/// Klasse „katalog“; ein Stil in <c>StadtbauApp.axaml</c> macht jeden gesperrten Knopf und Schalter darin blasser
/// (Schrift und Flaeche zusammen). Gilt fuer jeden gesperrten Katalog-Knopf, auch kuenftige.
/// </summary>
public sealed partial class Hauptansicht
{
    /// <summary>Klasse der Katalog-Flaechen (Stil in StadtbauApp.axaml).</summary>
    internal const string KatalogKlasse = "katalog";

    private readonly List<Control> _katalogFlaechen = [];

    /// <summary>Eine Katalog-Flaeche anmelden: gesperrte Knoepfe darin sehen blasser aus.</summary>
    private T KatalogFlaeche<T>(T c)
        where T : Control
    {
        c.Classes.Add(KatalogKlasse);
        _katalogFlaechen.Add(c);
        return c;
    }

    /// <summary>
    /// Gegenprobe (nur Start-Test): true = wie bis S 0.51.0 ohne die Klasse (gesperrte Knoepfe sehen aus wie freie),
    /// false = wieder mit Klasse.
    /// </summary>
    internal void KatalogGesperrtAltFuerTests(bool alt)
    {
        foreach (var c in _katalogFlaechen)
        {
            c.Classes.Set(KatalogKlasse, !alt);
        }
    }
}
