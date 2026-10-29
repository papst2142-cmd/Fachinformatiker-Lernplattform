namespace Lernplattform.Core.Pruefung;

/// <summary>Ein Bereich des Gesamtergebnisses (Rolle ap1, projekt, b1, b2, wiso).</summary>
public sealed record Pruefungsbereich(string Rolle, string Art, string Kurz, string Name, int Minuten, int Gewicht,
    bool Ergaenzung, string Quelle);

/// <summary>
/// Phase 4, W2 (Spezifikation G32/G36 Abschnitt 1, Addendum P4-2 Tabelle 1, FIAusbV 2020):
/// Aufbau der Abschlusspruefung je Fachrichtung. SI ist Python v0.63.2 (fisi_pruefung EXAMS,
/// WEIGHTS, PART2, AREA_NAME); AE, DPA und DV haben eigene Bereiche b1/b2. Phase 9 W2: ihre
/// Aufgaben kommen aus inhalte_csharp/lernen (Quelle "ap2_ae", "ap2_dpa", "ap2_dv"); solange es
/// keine gibt, zeigt die Auswahl den Platzhalter. Gewichte und Ergaenzung sind fuer alle gleich.
/// </summary>
public sealed record Fachrichtung(string Id, string Name, string Projekt, int ProjektStunden, IReadOnlyList<Pruefungsbereich> Bereiche)
{
    public static readonly IReadOnlyList<string> Rollen = ["ap1", "projekt", "b1", "b2", "wiso"];

    /// <summary>Teil 2 (projekt, b1, b2, wiso).</summary>
    public static readonly IReadOnlyList<string> Teil2 = ["projekt", "b1", "b2", "wiso"];

    public Pruefungsbereich Bereich(string rolle) => Bereiche.First(b => b.Rolle == rolle);

    public static readonly IReadOnlyList<Fachrichtung> Alle =
    [
        Neu("si", "Systemintegration", "Planen und Umsetzen eines Projektes der Systemintegration", 40, "ap2",
            ("konzeption", "Konzeption", "AP2 · Konzeption und Administration von IT-Systemen"),
            ("netzwerke", "Netzwerke", "AP2 · Analyse und Entwicklung von Netzwerken")),
        Neu("ae", "Anwendungsentwicklung", "Planen und Umsetzen eines Softwareprojektes", 80, "ap2_ae",
            ("softwareprodukt", "Softwareprodukt", "AP2 · Planen eines Softwareproduktes"),
            ("algorithmen", "Algorithmen", "AP2 · Entwicklung und Umsetzung von Algorithmen")),
        Neu("dpa", "Daten- und Prozessanalyse", "Planen und Durchführen eines Projektes der Datenanalyse", 40, "ap2_dpa",
            ("prozessanalyse", "Prozessanalyse", "AP2 · Durchführen einer Prozessanalyse"),
            ("datenqualitaet", "Datenqualität", "AP2 · Sicherstellen der Datenqualität")),
        Neu("dv", "Digitale Vernetzung", "Planen und Umsetzen eines Projektes der digitalen Vernetzung", 40, "ap2_dv",
            ("diagnose", "Diagnose", "AP2 · Diagnose und Störungsbeseitigung in vernetzten Systemen"),
            ("betrieb", "Betrieb", "AP2 · Betrieb und Erweiterung von vernetzten Systemen")),
    ];

    /// <summary>Fachrichtung zur Kennung; unbekannt oder null ergibt SI.</summary>
    public static Fachrichtung Nach(string? id) => Alle.FirstOrDefault(f => f.Id == id) ?? Alle[0];

    private static Fachrichtung Neu(string id, string name, string projekt, int stunden, string quelle,
        (string Art, string Kurz, string Name) b1, (string Art, string Kurz, string Name) b2) =>
        new(id, name, projekt, stunden,
        [
            new("ap1", "ap1", "AP1 (Teil 1)", "AP1 · Einrichten eines IT-gestützten Arbeitsplatzes", 90, 20, false, "ap1"),
            new("projekt", "projekt", "Projekt", projekt, 0, 50, false, "keine"),
            new("b1", b1.Art, b1.Kurz, b1.Name, 90, 10, true, quelle),
            new("b2", b2.Art, b2.Kurz, b2.Name, 90, 10, true, quelle),
            new("wiso", "wiso", "WiSo", "AP2 · Wirtschafts- und Sozialkunde", 60, 10, true, "quiz"),
        ]);
}
