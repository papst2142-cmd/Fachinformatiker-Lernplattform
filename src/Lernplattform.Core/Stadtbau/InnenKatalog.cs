namespace Lernplattform.Core.Stadtbau;

/// <summary>Ein Teil des Katalogs: Typ wie in innenraeume.json, Name fuer die Anzeige, Vorgabegroesse, Gruppe der Leiste.</summary>
public sealed record KatalogTeil(string Typ, string Name, double Breite, double Tiefe, string Gruppe);

/// <summary>
/// Phase 13 Welle 2 (Weisung P12-17, Plan Abschnitt 1): der Katalog der Einrichtung fuer Innenraeume. Nur Teile, die das
/// Spiel schon zeichnet (Typen aus der Ladepruefung des Spiels und aus Fassung 0, mit CC0-Modell oder eigener Form);
/// neue Modelle kommen nur ueber CC mit der naechsten Werkzeug-Version. Vorgabegroesse = haeufigste Groesse in Fassung 0.
/// </summary>
public static class InnenKatalog
{
    public static readonly IReadOnlyList<KatalogTeil> Teile =
    [
        new("absperrband", "Absperrband", 3.0, 0.1, "Sonstiges"),
        new("aktenregal", "Aktenregal", 0.6, 2.7, "Regale"),
        new("anzeigetafel", "Anzeigetafel", 4.0, 0.1, "Wand"),
        new("aquarium", "Aquarium", 1.8, 0.7, "Deko"),
        new("auslage", "Auslagetisch", 2.4, 0.9, "Regale"),
        new("automat", "Automat", 1.0, 0.6, "Geräte"),
        new("badewanne", "Badewanne", 1.6, 3.0, "Wohnen"),
        new("bahnhofsuhr", "Bahnhofsuhr", 1.2, 1.2, "Wand"),
        new("bank", "Bank", 2.4, 0.5, "Sitzen"),
        new("bauzaun", "Bauzaun", 3.0, 0.2, "Sonstiges"),
        new("besprechungstisch", "Besprechungstisch", 3.4, 1.1, "Tische"),
        new("bett", "Bett", 3.0, 3.6, "Wohnen"),
        new("bild", "Bild", 1.4, 0.3, "Wand"),
        new("couchtisch", "Couchtisch", 1.8, 1.0, "Tische"),
        new("drucker", "Drucker", 0.8, 0.6, "Geräte"),
        new("dusche", "Dusche", 1.6, 1.6, "Wohnen"),
        new("esstisch", "Esstisch", 2.4, 1.2, "Tische"),
        new("fahne", "Fahne", 0.6, 0.6, "Deko"),
        new("fernseher", "Fernseher", 3.0, 0.8, "Geräte"),
        new("feuerloescher", "Feuerlöscher", 0.4, 0.4, "Deko"),
        new("fussmatte", "Fußmatte", 0.6, 1.2, "Boden"),
        new("geldautomat", "Geldautomat", 1.0, 0.6, "Geräte"),
        new("gemaelde", "Gemälde", 1.6, 0.1, "Wand"),
        new("glaswand", "Glaswand", 4.4, 0.12, "Wand"),
        new("gleis", "Gleis", 6.0, 1.0, "Sonstiges"),
        new("grill", "Grill", 1.0, 1.0, "Wohnen"),
        new("hochbeet", "Hochbeet", 2.2, 1.0, "Deko"),
        new("hochregal", "Hochregal", 2.6, 1.0, "Regale"),
        new("hubwagen", "Hubwagen", 0.8, 1.6, "Sonstiges"),
        new("kaffeemaschine", "Kaffeemaschine", 0.55, 0.6, "Geräte"),
        new("kamera", "Kamera", 0.4, 0.4, "Wand"),
        new("kartons", "Kartons", 1.3, 1.0, "Sonstiges"),
        new("kasse", "Kasse", 0.6, 0.5, "Geräte"),
        new("kassentisch", "Kassentisch", 1.6, 0.8, "Tische"),
        new("kleiderregal", "Kleiderregal", 2.6, 0.6, "Regale"),
        new("kleiderschrank", "Kleiderschrank", 2.8, 1.0, "Regale"),
        new("kleiderstaender", "Kleiderständer", 2.8, 0.6, "Regale"),
        new("klima", "Klimagerät", 0.55, 1.9, "Geräte"),
        new("kommode", "Kommode", 1.8, 0.8, "Wohnen"),
        new("kuechenzeile", "Küchenzeile", 3.0, 0.8, "Wohnen"),
        new("kuehlschrank", "Kühlschrank", 0.8, 0.8, "Wohnen"),
        new("leitstand", "Leitstand", 4.6, 1.0, "Tische"),
        new("liegestuhl", "Liegestuhl", 1.0, 2.0, "Sitzen"),
        new("markierung", "Bodenmarkierung", 3.0, 0.1, "Boden"),
        new("matratze", "Matratze", 2.0, 3.0, "Wohnen"),
        new("monitorwand", "Monitorwand", 7.2, 0.4, "Wand"),
        new("palette", "Palette", 1.2, 0.8, "Sonstiges"),
        new("pc", "PC", 0.6, 0.6, "Geräte"),
        new("pflanze", "Pflanze", 0.8, 0.8, "Deko"),
        new("platznummer", "Platznummer", 0.4, 0.1, "Wand"),
        new("puppe", "Schaufensterpuppe", 0.8, 0.8, "Deko"),
        new("rack", "Serverschrank", 1.3, 2.6, "Geräte"),
        new("regal", "Regal", 2.4, 0.6, "Regale"),
        new("schliessfaecher", "Schließfächer", 0.5, 2.6, "Regale"),
        new("schreibtisch", "Schreibtisch", 2.4, 1.0, "Tische"),
        new("sessel", "Sessel", 1.1, 1.1, "Sitzen"),
        new("sitzsack", "Sitzsack", 1.2, 1.2, "Sitzen"),
        new("sofa", "Sofa", 3.6, 1.4, "Sitzen"),
        new("sonnenschirm", "Sonnenschirm", 1.6, 1.6, "Deko"),
        new("spiegel", "Spiegel", 0.12, 1.4, "Wand"),
        new("spielkonsole", "Spielkonsole", 0.8, 0.5, "Geräte"),
        new("stehlampe", "Stehlampe", 0.7, 0.7, "Deko"),
        new("stuhlreihe", "Stuhlreihe", 2.6, 0.7, "Sitzen"),
        new("teppich", "Teppich", 3.0, 2.2, "Boden"),
        new("tisch", "Tisch", 1.6, 1.0, "Tische"),
        new("tresen", "Tresen", 3.6, 0.9, "Tische"),
        new("tresor", "Tresor", 0.6, 2.0, "Sonstiges"),
        new("umkleide", "Umkleide", 1.6, 1.6, "Sonstiges"),
        new("usv", "USV", 1.4, 1.0, "Geräte"),
        new("wappen", "Wappen", 1.2, 0.1, "Wand"),
        new("wartemarken", "Wartemarken", 0.7, 0.5, "Geräte"),
        new("wartesessel", "Wartesessel", 1.2, 1.0, "Sitzen"),
        new("waschbecken", "Waschbecken", 0.8, 0.6, "Wohnen"),
        new("waschmaschine", "Waschmaschine", 1.0, 1.0, "Geräte"),
        new("wasserspender", "Wasserspender", 0.6, 0.6, "Geräte"),
        new("wc", "WC", 0.8, 0.8, "Wohnen"),
        new("werkbank", "Werkbank", 5.0, 1.0, "Tische"),
        new("whiteboard", "Whiteboard", 0.22, 2.4, "Wand"),
        new("wimpel", "Wimpelkette", 3.0, 0.1, "Wand"),
        new("zug", "Zug", 6.0, 1.2, "Sonstiges"),
    ];

    /// <summary>Gruppen der Leiste in fester Reihenfolge.</summary>
    public static readonly IReadOnlyList<string> Gruppen = ["Regale", "Tische", "Sitzen", "Geräte", "Wohnen", "Deko", "Wand", "Boden", "Sonstiges"];

    private static readonly Dictionary<string, KatalogTeil> NachTyp = Teile.ToDictionary(t => t.Typ, StringComparer.Ordinal);

    public static bool Bekannt(string typ) => NachTyp.ContainsKey(typ);

    public static KatalogTeil? Teil(string typ) => NachTyp.GetValueOrDefault(typ);

    /// <summary>Name fuer die Anzeige (unbekannter Typ: der Typ selbst).</summary>
    public static string Name(string typ) => NachTyp.TryGetValue(typ, out var t) ? t.Name : typ;

    /// <summary>
    /// Stuecke, die im Spiel aufeinander oder ineinander stehen duerfen (Kasse auf dem Tresen, Geraete auf Tischen,
    /// Puppen in der Auslage). Gilt in beide Richtungen.
    /// </summary>
    private static readonly HashSet<(string, string)> Aufliegen = [.. new[] { "tresen", "schreibtisch", "tisch", "kassentisch", "werkbank" }
        .SelectMany(unten => new[] { "kasse", "pc", "drucker", "kaffeemaschine", "platznummer", "wartemarken" }.Select(oben => (unten, oben)))
        .Append(("tresen", "glaswand"))];

    public static bool DarfAufliegen(string a, string b) => Aufliegen.Contains((a, b)) || Aufliegen.Contains((b, a));
}
