namespace Lernplattform.Core.Design;

/// <summary>
/// Weisung P12-434 (Regel D „Entwurf gilt woertlich“, Frage 4 = a): Farbe der Pille hinter dem gewaehlten Knopf der unteren
/// Leiste. Im hellen Aussehen hebt sie sich so deutlich von der Leiste ab wie im freigegebenen Entwurf P12-271 (Pille
/// #E3D9F5 auf Leiste #EBE4F7); dazu wird die Leistenfarbe (sidebar) der Farbwelt schrittweise zur Randfarbe (border)
/// gemischt, bis der Kontrast mindestens so gross ist wie im Entwurf. Im dunklen Aussehen bleibt card_hi (wie im Entwurf).
/// </summary>
public static class Leistenpille
{
    /// <summary>Leiste und Pille im freigegebenen Entwurf P12-271, helles Aussehen.</summary>
    public const string EntwurfLeiste = "#EBE4F7", EntwurfPille = "#E3D9F5";

    /// <summary>Kontrast Pille zu Leiste im Entwurf (etwa 1,09).</summary>
    public static double EntwurfKontrast => Farbwelt.Kontrast(EntwurfLeiste, EntwurfPille);

    /// <summary>Farbe der Pille in dieser Farbwelt.</summary>
    public static string Farbe(Palette palette)
    {
        if (!palette.Hell)
        {
            return palette.Farben["card_hi"];
        }
        var leiste = palette.Farben["sidebar"];
        var rand = palette.Farben["border"];
        for (var schritt = 1; schritt <= 100; schritt++)
        {
            var ton = Farbwelt.Mischen(leiste, rand, schritt / 100.0);
            if (Farbwelt.Kontrast(leiste, ton) >= EntwurfKontrast)
            {
                return ton;
            }
        }
        // Rand und Leiste liegen naeher beieinander als im Entwurf: weiter Richtung Schwarz
        for (var schritt = 1; schritt <= 100; schritt++)
        {
            var ton = Farbwelt.Mischen(rand, "#000000", schritt / 100.0);
            if (Farbwelt.Kontrast(leiste, ton) >= EntwurfKontrast)
            {
                return ton;
            }
        }
        return "#000000";
    }
}
