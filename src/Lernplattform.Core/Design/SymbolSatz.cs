namespace Lernplattform.Core.Design;

/// <summary>Art eines Teils eines zweifarbigen Symbols (<see cref="SymbolSatz.Satz3"/>).</summary>
public enum SymbolTeilArt { Form, Akzent, Grund, Linie }

/// <summary>Ein Teil eines zweifarbigen Symbols: Art, Umriss (SVG-Pfad im Raster 24 x 24), bei Linie die Breite.</summary>
public sealed record SymbolTeil(SymbolTeilArt Art, string Pfad, double Breite = 0);

/// <summary>
/// A 1.17.0 (Weisung P12-430, Nico: Satz 3 „geometrisch zweifarbig“ aus Meldung P12-271, Start = Sonnenaufgang aus P12-272):
/// die Symbole der Leisten als Daten. Gezeichnet werden sie in der Oberflaeche (Views/SatzSymbol.cs).
/// </summary>
public static class SymbolSatz
{
    // -- A 1.17.0 (Weisung P12-430, Nico: Satz 3 aus Meldung P12-271, Start = Sonnenaufgang aus P12-272) ----------------
    // Selbst gezeichnet (CC, 27.11.2026), Raster 24 x 24, keine fremde Vorlage, darum kein Eintrag in der Lizenzliste.
    // Quelle: Fi-Austausch anlagen/P12-272_Symbol_Quellen/ (satz3_symbole.py, entwurf_start_farbwelt.py STARTS['3']);
    // Kreise und Rechtecke dort sind hier als Pfade ausgeschrieben. Die Material-Symbole (Views/Symbole.Zeichen) bleiben unveraendert.

    /// <summary>
    /// Satz 3 „geometrisch zweifarbig“: je Ziel die Teile in Zeichenreihenfolge. Form = Grundfarbe (muted bzw. accent, wenn
    /// gewaehlt), Akzent = zweiter Akzent der Farbwelt (accent2) mit einem Spalt 1,5 in der Grundflaeche, Grund = ausgespart
    /// (Farbe der Flaeche darunter), Linie = ausgesparte Linie (Breite in Punkten des Rasters, runde Enden).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, SymbolTeil[]> Satz3 = new Dictionary<string, SymbolTeil[]>(StringComparer.Ordinal)
    {
        // Sonnenaufgang
        ["START"] = [
            new(SymbolTeilArt.Akzent, "M5.5 16a6.5 6.5 0 0 1 13 0z"),
            new(SymbolTeilArt.Form, "M11.1,2.5 L12.9,2.5 L12.9,6.5 L11.1,6.5 Z"),
            new(SymbolTeilArt.Form, "M19.136,5.191 L20.409,6.464 L17.864,9.009 L16.591,7.736 Z"),
            new(SymbolTeilArt.Form, "M3.591,6.464 L4.864,5.191 L7.409,7.736 L6.136,9.009 Z"),
            new(SymbolTeilArt.Form, "M19,11.6 L22.5,11.6 L22.5,13.4 L19,13.4 Z"),
            new(SymbolTeilArt.Form, "M1.5,11.6 L5,11.6 L5,13.4 L1.5,13.4 Z"),
            new(SymbolTeilArt.Form, "M1.5,16.6 L22.5,16.6 L22.5,18.6 L1.5,18.6 Z"),
            new(SymbolTeilArt.Form, "M5,20 L19,20 L19,21.6 L5,21.6 Z"),
        ],
        // Stoppuhr
        ["PRUEFUNG"] = [
            new(SymbolTeilArt.Form, "M10.5,1.8 L13.5,1.8 L13.5,4.4 L10.5,4.4 Z"),
            new(SymbolTeilArt.Form, "M18.5,4.103 L20.197,5.8 L18.5,7.497 L16.803,5.8 Z"),
            new(SymbolTeilArt.Form, "M3.5,13.5 A8.5,8.5 0 1 0 20.5,13.5 A8.5,8.5 0 1 0 3.5,13.5 Z"),
            new(SymbolTeilArt.Akzent, "M12 13.5V7a6.5 6.5 0 0 1 6.5 6.5z"),
            new(SymbolTeilArt.Grund, "M10.8,13.5 A1.2,1.2 0 1 0 13.2,13.5 A1.2,1.2 0 1 0 10.8,13.5 Z"),
        ],
        // Chip (Schaltkreis)
        ["LERNEN"] = [
            new(SymbolTeilArt.Form, "M8.5 2.5h1.6v3H8.5zM11.2 2.5h1.6v3h-1.6zM13.9 2.5h1.6v3h-1.6zM8.5 18.5h1.6v3H8.5zM11.2 18.5h1.6v3h-1.6zM13.9 18.5h1.6v3h-1.6zM2.5 8.5h3v1.6h-3zM2.5 11.2h3v1.6h-3zM2.5 13.9h3v1.6h-3zM18.5 8.5h3v1.6h-3zM18.5 11.2h3v1.6h-3zM18.5 13.9h3v1.6h-3z"),
            new(SymbolTeilArt.Form, "M6.5,5.5 L17.5,5.5 A1,1 0 0 1 18.5,6.5 L18.5,17.5 A1,1 0 0 1 17.5,18.5 L6.5,18.5 A1,1 0 0 1 5.5,17.5 L5.5,6.5 A1,1 0 0 1 6.5,5.5 Z"),
            new(SymbolTeilArt.Akzent, "M9,9 L15,9 L15,15 L9,15 Z"),
        ],
        // Stadt-Silhouette
        ["SPIEL"] = [
            new(SymbolTeilArt.Akzent, "M8.5 5.5 11.5 2l3 3.5z"),
            new(SymbolTeilArt.Form, "M2.5 11h5v10h-5zM8.5 5.5h6V21h-6zM15.5 9h6v12h-6z"),
            new(SymbolTeilArt.Grund, "M3.8 13h1.4v1.4H3.8zM3.8 16.2h1.4v1.4H3.8zM10 8h1.3v1.4H10zM11.8 8h1.3v1.4h-1.3zM10 11.4h1.3v1.4H10zM11.8 11.4h1.3v1.4h-1.3zM10 14.8h1.3v1.4H10zM11.8 14.8h1.3v1.4h-1.3zM17 11h1.3v1.4H17zM18.7 11H20v1.4h-1.3zM17 14.4h1.3v1.4H17zM18.7 14.4H20v1.4h-1.3z"),
            new(SymbolTeilArt.Akzent, "M1.5,20.5 L22.5,20.5 L22.5,22.2 L1.5,22.2 Z"),
        ],
        // Berg mit Flagge
        ["FORTSCHRITT"] = [
            new(SymbolTeilArt.Form, "M2 21 9.5 8.5l3.6 6 2.6-3.7L22 21z"),
            new(SymbolTeilArt.Form, "M8.9,2.5 L10.2,2.5 L10.2,9 L8.9,9 Z"),
            new(SymbolTeilArt.Akzent, "M10.2 2.5h5.6l-1.4 1.8 1.4 1.8h-5.6z"),
        ],
        // Wegweiser
        ["HILFE"] = [
            new(SymbolTeilArt.Form, "M11,2.5 L13,2.5 L13,22 L11,22 Z"),
            new(SymbolTeilArt.Akzent, "M13 4.5h6l2.6 2.7L19 9.9h-6z"),
            new(SymbolTeilArt.Form, "M11 11.5H5l-2.6 2.7L5 16.9h6z"),
            new(SymbolTeilArt.Form, "M8,20.5 L16,20.5 L16,22 L8,22 Z"),
        ],
        // Drehregler
        ["OPTIONEN"] = [
            new(SymbolTeilArt.Form, "M4.8,13 A7.2,7.2 0 1 0 19.2,13 A7.2,7.2 0 1 0 4.8,13 Z"),
            new(SymbolTeilArt.Linie, "M12 13l3.6-3.6", 2),
            new(SymbolTeilArt.Akzent, "M10.9,2.8 A1.1,1.1 0 1 0 13.1,2.8 A1.1,1.1 0 1 0 10.9,2.8 Z"),
            new(SymbolTeilArt.Akzent, "M3.7,5.8 A1.1,1.1 0 1 0 5.9,5.8 A1.1,1.1 0 1 0 3.7,5.8 Z"),
            new(SymbolTeilArt.Akzent, "M18.1,5.8 A1.1,1.1 0 1 0 20.3,5.8 A1.1,1.1 0 1 0 18.1,5.8 Z"),
            new(SymbolTeilArt.Akzent, "M1.2,13 A1.1,1.1 0 1 0 3.4,13 A1.1,1.1 0 1 0 1.2,13 Z"),
            new(SymbolTeilArt.Akzent, "M20.6,13 A1.1,1.1 0 1 0 22.8,13 A1.1,1.1 0 1 0 20.6,13 Z"),
        ],
        // Plus in Kachel
        ["MEHR"] = [
            new(SymbolTeilArt.Form, "M5,3 L19,3 A2,2 0 0 1 21,5 L21,19 A2,2 0 0 1 19,21 L5,21 A2,2 0 0 1 3,19 L3,5 A2,2 0 0 1 5,3 Z"),
            new(SymbolTeilArt.Akzent, "M10.8 6.8h2.4v10.4h-2.4zM6.8 10.8h10.4v2.4H6.8z"),
        ],
        // Stecker
        ["BEENDEN"] = [
            new(SymbolTeilArt.Form, "M8.6,2.5 L10.5,2.5 L10.5,8.5 L8.6,8.5 Z"),
            new(SymbolTeilArt.Form, "M13.5,2.5 L15.4,2.5 L15.4,8.5 L13.5,8.5 Z"),
            new(SymbolTeilArt.Form, "M6.5 8.5h11v4a5.5 5.5 0 0 1-11 0z"),
            new(SymbolTeilArt.Akzent, "M11,17.5 L13,17.5 L13,22 L11,22 Z"),
        ],
    };

    /// <summary>Breite des Spalts um Akzent-Teile (Strich in der Farbe der Flaeche, unter dem Akzent gezeichnet).</summary>
    public const double AkzentSpalt = 1.5;

    /// <summary>
    /// Weisung P12-434: Fuellregel aller Teile wie im SVG des Entwurfs (nonzero; in der Pfad-Schreibweise von Avalonia „F1“, ohne sie
    /// gilt dort evenodd). Mit evenodd wurde die Ueberlagerung der zwei Balken im Kreuz von „Mehr“ zum hellen Fleck.
    /// </summary>
    public const string Fuellregel = "F1 ";
}
