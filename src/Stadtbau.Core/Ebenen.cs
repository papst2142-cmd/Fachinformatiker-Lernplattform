namespace Stadtbau.Core;

/// <summary>Welle S 0.27.0 (Weisung P12-118 Teil B, Nico-Wunsch 4): Ebenen der Draufsicht, die sich ausblenden lassen.</summary>
public enum Ebene
{
    Gruen,
    Laternen,
    Katalog,
    Beschriftungen,
    Wasser,
    Wege,
    Notizen,
    Gelaende,
    Funde,
    Aufgaben,
}

/// <summary>
/// Welle S 0.27.0 (Weisung P12-118 Teil B, Kurzplan Meldung P12-79 Wunsch 4): Liste „Ebenen ▾“ der Draufsicht. Jede Ebene
/// laesst sich aus- und wieder einblenden, nur in der Draufsicht (3D und Spiel bleiben, wie sie sind). Ausgeblendetes ist
/// nicht anwaehlbar (E1 im Plan, <see cref="AussenBearbeitung.StueckBei"/> mit <see cref="Arten"/>); gemerkt in
/// <c>einstellungen.json</c> des Werkzeugs (<see cref="WerkzeugEinstellungen.EbenenAus"/>, Schluessel <see cref="Schluessel"/>).
/// </summary>
public static class Ebenen
{
    /// <summary>Schluessel in <c>einstellungen.json</c>: Liste der Kennungen der ausgeblendeten Ebenen.</summary>
    public const string Schluessel = "ebenen_aus";

    /// <summary>Alle Ebenen in der Reihenfolge der Liste.</summary>
    public static readonly IReadOnlyList<Ebene> Alle = Enum.GetValues<Ebene>();

    /// <summary>Kennung in <c>einstellungen.json</c> (fest, nie aendern).</summary>
    public static string Kennung(Ebene e) => e switch
    {
        Ebene.Gruen => "gruen",
        Ebene.Laternen => "laternen",
        Ebene.Katalog => "katalog",
        Ebene.Beschriftungen => "beschriftungen",
        Ebene.Wasser => "wasser",
        Ebene.Wege => "wege",
        Ebene.Notizen => "notizen",
        Ebene.Gelaende => "gelaende",
        Ebene.Funde => "funde",
        _ => "aufgaben",
    };

    /// <summary>Ebene zu einer Kennung (null = unbekannt).</summary>
    public static Ebene? Aus(string kennung) => Alle.Where(e => Kennung(e) == kennung).Select(e => (Ebene?)e).FirstOrDefault();

    /// <summary>Name in der Liste.</summary>
    public static string Name(Ebene e) => e switch
    {
        Ebene.Gruen => Texte.EbeneGruen,
        Ebene.Laternen => Texte.EbeneLaternen,
        Ebene.Katalog => Texte.EbeneKatalog,
        Ebene.Beschriftungen => Texte.EbeneBeschriftungen,
        Ebene.Wasser => Texte.EbeneWasser,
        Ebene.Wege => Texte.EbeneWege,
        Ebene.Notizen => Texte.EbeneNotizen,
        Ebene.Gelaende => Texte.EbeneGelaende,
        Ebene.Funde => Texte.EbeneFunde,
        _ => Texte.EbeneAufgaben,
    };

    /// <summary>Stuecke dieser Ebene, die mit ihr nicht mehr anwaehlbar sind (Beschriftungen, Funde und Zahlen sind keine Stuecke).</summary>
    public static IReadOnlyList<AussenArt> ArtenVon(Ebene e) => e switch
    {
        Ebene.Gruen => [AussenArt.Baum, AussenArt.Park, AussenArt.Beet],
        Ebene.Laternen => [AussenArt.Laterne],
        Ebene.Katalog => [AussenArt.Katalog],
        Ebene.Wasser => [AussenArt.Fluss, AussenArt.Teich, AussenArt.Steg],
        Ebene.Wege => [AussenArt.Fussweg],
        Ebene.Notizen => [AussenArt.Notiz],
        Ebene.Gelaende => [AussenArt.Huegel, AussenArt.Hoehe],
        _ => [],
    };

    /// <summary>Alle Arten, die bei diesen ausgeblendeten Ebenen nicht anwaehlbar sind.</summary>
    public static IReadOnlySet<AussenArt> Arten(IEnumerable<Ebene> aus) => aus.SelectMany(ArtenVon).ToHashSet();

    /// <summary>Die Ebene, zu der ein Stueck gehoert (null = laesst sich nicht ausblenden, z. B. Strasse, Haus).</summary>
    public static Ebene? Von(AussenArt art) => Alle.Where(e => ArtenVon(e).Contains(art)).Select(e => (Ebene?)e).FirstOrDefault();
}
