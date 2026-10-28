namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, W4: feste Tabelle der Paare (Nachtrag P4-11 Abschnitt 5). Sie steht im Testprojekt, nicht in
/// Core/Hilfe/HilfeTexte.cs: die alten Python-Texte nennen das alte Repository, und das Programm darf es
/// nirgends nennen (AbgleichTests.FesteAdresseUndKeinAltesRepository).
/// </summary>
internal static class HilfePaare
{
    /// <summary>Feste Tabelle „alt → neu“ (Nachtrag P4-11 Abschnitt 5): wo und wie C# von Python abweicht.</summary>
    public static readonly IReadOnlyList<(string Nr, string Thema, string Alt, string Neu)> Paare =
    [
        ("N1", "abgleich",
            "Namen fisi-lernstand.",
            "Namen Fi-Lernstand."),
        ("N1", "abgleich",
            "deinname/fisi-lernstand",
            "deinname/Fi-Lernstand"),
        ("N2", "schutzprogramm",
            "github.com/papst2142-cmd/Fisi-Lernplattform/releases",
            "github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases"),
        ("N2", "update_schutz",
            "github.com/papst2142-cmd/Fisi-Lernplattform/releases",
            "github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases"),
        ("W1", "update",
            "\n\nUnter Linux legt „Desktop-Verknüpfung anlegen“ eine Verknüpfung auf dem Desktop an oder erneuert si"
            + "e, zum Beispiel nachdem du das AppImage verschoben hast.",
            // Phase 6 W2 (Weisung P6-7, Nachtrag Nico 23.10.2026 20:07): W2-12, Wortliste Spalte "C# a"
            "\n\nUnter Linux legt „Verknüpfung anlegen“ eine Verknüpfung im Anwendungsmenü und auf dem Desktop an "
            + "oder erneuert sie, zum Beispiel nachdem du das AppImage verschoben hast."),
        ("W2", "haenger",
            "\n\nAb Version 0.59.2 unter Linux: Hängt das Programm, öffne ein Terminal und gib diesen Befehl ein:\nki"
            + "ll -USR1 $(pgrep -i -x -o fisi-lernplattf)\n"
            + "Das Programm schreibt dann in die Datei haenger.log, an welcher Stelle es gerade steht. Es wird "
            + "dabei nicht beendet. Achtung: In Version 0.59.1 und älter beendet derselbe Befehl das Programm. In "
            + "den „Optionen“ unter „Diagnose und Werkzeuge“ kopiert „Befehl kopieren“ ihn für dich.",
            ""),
        ("W3", "haenger",
            "Unter Linux mit IBus schaltet das Programm für sich die IBus-Eingabe ab (X11 ab 0.60, bisher nur im "
            + "Test geprüft), sonst kann es einfrieren. Umlaute, ß und AltGr gehen weiter. Wer IBus braucht, "
            + "startet es mit\n"
            + "FISI_XIM=1 fisi-lernplattform\nMit FISI_XIM=1 kann das Programm wieder langsamer sein oder hängen.",
            "Hängt unter Linux das Tippen, startest du das Programm mit dem Startschalter\n"
            + "--ohne-eingabemethode\nDann nutzt es keine Eingabemethode, zum Beispiel IBus."),
        ("W4", "tastatur",
            " Die Spielkarte und die Grundrisse im Spiel brauchen noch die Maus.",
            ""),
        // Phase 7 W6 (Weisung P7-11, Hilfe-Abschnitt Spiel): Satz der Stadt (StadtTexte.HilfeSatz, Weisung P7-9)
        ("S1", "spiel",
            "Der Lernfortschritt gilt für alle gemeinsam.",
            "Der Lernfortschritt gilt für alle gemeinsam.\n\nIm Spiel vergeht nur Spielzeit: Jeder Schritt kostet Minuten, die echte Uhr spielt keine Rolle."),
        // Phase 8 W2 (Weisung P8-3, Sammelliste 72, Nico 23.10.2026): "Vorladen" streichen, die C#-Linie laedt nicht vor
        ("W5", "haenger",
            ", Vorladen)",
            ")"),
        // Phase 8 W5 (Weisung P8-7 B2, Plan Phase 8 Abschnitt 4 und 11, Nico 4a): Stadt in 3D mit Kaufhaus und Bett (H1),
        // Spielstand im Abgleich (H2), Knopf "Lizenzen" in "Über das Programm" (L1)
        ("H1", "spiel",
            "die echte Uhr spielt keine Rolle.",
            "die echte Uhr spielt keine Rolle.\n\nDu bewegst dich durch eine Stadt in 3D: Tippe oder klicke auf einen Weg oder ein Haus, dann geht deine Figur dorthin. Im Möbelhaus und im Elektromarkt kaufst du ein, im Kaufhaus Kleidung für deine Figur. In der Bank nimmst du Kredite auf, auf dem Amt gründest du deine Firma. Die Häuser haben Öffnungszeiten, ab 20 Uhr geht es nur noch nach Hause. Zum Feierabend gehst du zu Hause ins Bett. Kann dein Gerät keine 3D-Grafik zeigen, stehen die Orte in einer Liste."),
        ("H2", "spiel",
            "Der Lernfortschritt gilt für alle gemeinsam.",
            "Der Lernfortschritt gilt für alle gemeinsam. Mit eingerichtetem Abgleich spielst du denselben Spielstand auf PC und Handy weiter."),
        ("L1", "rechtliches",
            "„Lizenz“ und „Hinweise zu Fremdbestandteilen“ (Bestandteile anderer Hersteller und ihre Lizenzen).",
            "„Lizenz“, „Hinweise zu Fremdbestandteilen“ (Bestandteile anderer Hersteller und ihre Lizenzen) und „Lizenzen“ (die vollständigen Lizenztexte dieser Bestandteile)."),
        // Phase 8 W6 (Weisung P8-12, Teilplan P8-11 Abschnitt 6): Festzeiten (F1)
        ("F1", "spiel",
            "im Kaufhaus Kleidung für deine Figur.",
            "im Kaufhaus Kleidung für deine Figur. Zu Halloween, Weihnachten, Silvester und Ostern ist die Stadt geschmückt; im Kaufhaus gibt es dann passende Kleidung."),
        // Phase 8 W10 (Weisung P8-34, Nico 2c): abends weiter gehen, um 23 Uhr steht die Uhr (T1)
        ("T1", "spiel",
            "Die Häuser haben Öffnungszeiten, ab 20 Uhr geht es nur noch nach Hause.",
            "Die Häuser haben Öffnungszeiten. Abends kannst du weiter durch die Stadt gehen; um 23 Uhr bleibt die Uhr stehen, bis du zu Hause schlafen gehst."),
        ("N2", "RELEASE_PAGE_TEXT",
            "github.com/papst2142-cmd/Fisi-Lernplattform/releases",
            "github.com/papst2142-cmd/Fachinformatiker-Lernplattform/releases"),
    ];
}
