using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Pruefung;

namespace Lernplattform.Core.Texte;

/// <summary>Art einer Zeile der Seitenleiste: Eintrag (fett), Unterpunkt (eingerueckt) oder Gruppenkopf (klein, gross geschrieben).</summary>
public enum LeistenArt { Eintrag, Unter, Gruppe }

/// <summary>Eine Zeile der Seitenleiste (Gruppenkopf ohne Kennung).</summary>
public sealed record LeistenZeile(LeistenArt Art, string? Id, string Titel);

/// <summary>
/// Welle A 1.15.0 (Weisungen P12-406/P12-407, Nico 26.11.2026 Antworten 1a-5a, „genau wie die Entwurfsbilder“ Variante B
/// aus Meldung P12-261): Navigation nach Pruefungsteilen. Seitenleiste wie das Deckblatt (ABSCHLUSSPRÜFUNG &lt;Kürzel&gt; mit
/// Teil 1 · AP1 und Teil 2 · AP2 und ihren Bereichen, LERNEN mit Karteikarten, Notizblock, Rechner, SONSTIGES mit Spiel und
/// Fortschritt, unten Hilfe und Optionen), untere Leiste am Handy Start, Teil 1, Teil 2, Lernen, Spiel, Mehr. Die Namen der
/// Teile folgen der gewaehlten Fachrichtung (Deckblatt, <see cref="Fachrichtung"/>). Sollwert-Aenderung Nr. 26.
/// </summary>
public static class Pruefungsteile
{
    public const string Teil1 = "teil1";
    public const string Teil2 = "teil2";
    public const string B1 = "b1";
    public const string B2 = "b2";
    public const string Wiso = "wiso";
    public const string Mehr = "mehr";

    /// <summary>
    /// A 1.17.0 (Weisung P12-430, Reiter B aus Meldung P12-268): ein Eintrag „Prüfung“ unten am Handy und als Hauptpunkt am PC;
    /// er oeffnet die zuletzt gewaehlte Haelfte AP1 oder AP2 (zuerst AP1), oben auf der Seite die Segment-Leiste „AP1 | AP2“.
    /// </summary>
    public const string Pruefung = "pruefung";

    /// <summary>Seiten eines Pruefungsteils mit den Reitern „Üben“ und „Prüfen“ (Teil 1 und die drei schriftlichen Bereiche).</summary>
    public static readonly IReadOnlyList<string> ReiterSeiten = [Teil1, B1, B2, Wiso];

    /// <summary>Rolle in <see cref="Fachrichtung.Bereiche"/> zu einer Seite.</summary>
    public static string Rolle(string seite) => seite == Teil1 ? "ap1" : seite;

    // -- Beschriftungen der Leisten --------------------------------------------------------------
    public const string GruppeAbschluss = "ABSCHLUSSPRÜFUNG %s";
    public const string PruefungTitel = "Prüfung";
    public const string LernenTitel = "Lernen";
    /// <summary>A 1.17.0 (Reiter B, P12-420/P12-423): nirgends mehr „Teil 1/2“ in den Leisten, nur noch AP1 und AP2.</summary>
    public const string Teil1Titel = "AP1";
    public const string Teil2Titel = "AP2";
    public const string Teil1Kurz = "AP1";
    public const string Teil2Kurz = "AP2";
    /// <summary>Zweite Zeile einer Haelfte der Segment-Leiste: Gewicht in der Pruefung („20 %“).</summary>
    public const string SegmentGewicht = "%d %%";
    public const string MehrTitel = "Mehr";
    public const string Abschlussprojekt = "Abschlussprojekt";

    /// <summary>
    /// Name im Entwurf der Seitenleiste: der Name vom Deckblatt; nur FISI Bereich 1 kuerzer wie im Entwurfsbild
    /// („Konzeption und Administration“), damit er in zwei Zeilen passt.
    /// </summary>
    public static string Seitenleistenname(Fachrichtung fachrichtung, string rolle) =>
        fachrichtung.Id == Fachrichtungen.Systemintegration && rolle == B1 ? "Konzeption und Administration" : Deckblattname(fachrichtung, rolle);

    /// <summary>Name wie auf dem Deckblatt der IHK (ohne „AP1 · “/„AP2 · “ davor).</summary>
    public static string Deckblattname(Fachrichtung fachrichtung, string rolle)
    {
        var name = fachrichtung.Bereich(rolle).Name;
        foreach (var vorn in (string[])["AP1 · ", "AP2 · "])
        {
            if (name.StartsWith(vorn, StringComparison.Ordinal))
            {
                return name[vorn.Length..];
            }
        }
        return name;
    }

    /// <summary>Zeilen der Seitenleiste (oben, im Bildlauf) fuer eine Fachrichtung.</summary>
    /// <remarks>
    /// A 1.17.0 (Weisung P12-430, Entwurf P12-271 Satz 3): Hauptpunkte mit Symbol (<see cref="Symbole"/>), Unterpunkte ohne;
    /// „Prüfung“ mit AP1, AP2 und den Bereichen darunter (P12-429), „Lernen“ mit Karteikarten, Notizblock, Rechner; die
    /// Gruppenkoepfe LERNEN und SONSTIGES entfallen wie im Entwurf, die Fachrichtung bleibt als Kopf über „Prüfung“ (P12-425).
    /// </remarks>
    public static IReadOnlyList<LeistenZeile> Seitenleiste(Fachrichtung fachrichtung) =>
    [
        new(LeistenArt.Eintrag, "dashboard", "Start"),
        new(LeistenArt.Gruppe, null, Beschriftungen.Einsetzen(GruppeAbschluss, FachrichtungTexte.KuerzelFuer(fachrichtung.Id))),
        new(LeistenArt.Eintrag, Pruefung, PruefungTitel),
        new(LeistenArt.Unter, Teil1, Teil1Titel),
        new(LeistenArt.Unter, Teil2, Teil2Titel),
        new(LeistenArt.Unter, B1, Seitenleistenname(fachrichtung, B1)),
        new(LeistenArt.Unter, B2, Seitenleistenname(fachrichtung, B2)),
        new(LeistenArt.Unter, Wiso, Deckblattname(fachrichtung, Wiso)),
        new(LeistenArt.Unter, "abschluss", Abschlussprojekt),
        new(LeistenArt.Eintrag, Navigation.LernenId, LernenTitel),
        new(LeistenArt.Unter, "cards", "Karteikarten"),
        new(LeistenArt.Unter, "notebook", "Notizblock"),
        new(LeistenArt.Unter, "calc", "Rechner"),
        new(LeistenArt.Eintrag, "game", "Spiel"),
        new(LeistenArt.Eintrag, "progress", "Fortschritt"),
    ];

    /// <summary>Kennung des Symbols „Programm beenden“ in <see cref="Symbole"/>.</summary>
    public const string Beenden = "beenden";

    /// <summary>
    /// A 1.17.0 (Weisung P12-430, Satz 3 mit Start = Sonnenaufgang): Symbol je Hauptpunkt der Seitenleiste, der unteren Leiste
    /// und der Kopfleiste (Hilfe). Gleicher Satz an PC und Handy; der Bildschirmleser liest die Woerter, nie das Symbol.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Symbole = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["dashboard"] = "START",
        [Pruefung] = "PRUEFUNG",
        [Navigation.LernenId] = "LERNEN",
        ["game"] = "SPIEL",
        ["progress"] = "FORTSCHRITT",
        ["help"] = "HILFE",
        ["settings"] = "OPTIONEN",
        [Mehr] = "MEHR",
        [Beenden] = "BEENDEN",
    };

    /// <summary>Hauptpunkt der Seitenleiste zu einem Unterpunkt (null = kein Unterpunkt).</summary>
    public static string? Oberpunkt(string id) => id switch
    {
        Teil1 or Teil2 or B1 or B2 or Wiso or "abschluss" => Pruefung,
        "cards" or "notebook" or "calc" => Navigation.LernenId,
        _ => null,
    };

    /// <summary>Unten angeheftet (ausserhalb des Bildlaufs): Hilfe und Optionen, darunter wie bisher „Programm beenden“ (der Kasten LERNSTATUS entfiel mit Weisung P12-409, Nico 2a).</summary>
    public static readonly IReadOnlyList<LeistenZeile> SeitenleisteUnten =
    [
        new(LeistenArt.Eintrag, "help", Navigation.Hilfe),
        new(LeistenArt.Eintrag, "settings", "Optionen"),
    ];

    /// <summary>Sammelseite „Mehr“ am Handy: Fortschritt, Optionen, Hilfe.</summary>
    public static readonly IReadOnlyList<LernenEintrag> MehrEintraege =
    [
        new("progress", "INSIGHTS_ROUNDED", "Fortschritt", "Rahmenplan, Prüfungen und Lerntage", ["#7C3AED", "#22D3EE"]),
        new("settings", "SETTINGS_ROUNDED", "Optionen", "Aussehen, Fachrichtung, Sicherung und Updates", ["primary"]),
        new("help", "HELP_OUTLINE_ROUNDED", Navigation.Hilfe, "Erklärungen und Rundgang", ["success"]),
        // die ganze Liste der AP2-Szenarien (in den Bereichen gefiltert), damit sie am Handy in zwei Klicks erreichbar bleibt
        new("scenarios", "DIAMOND_ROUNDED", "AP2-Szenarien", "%d Aufgaben aus allen Bereichen von AP2", ["#DB2777", "#FB923C"]),
    ];

    public static readonly Kopftitel MehrKopf = new(Mehr, "MEHR", "ÜBERSICHT");

    /// <summary>Welche Seite der Seitenleiste bzw. der unteren Leiste zu einer Seite gehoert, die sie selbst nicht zeigt.</summary>
    public static string? Eltern(string id, bool unten) => id switch
    {
        Teil1 or Teil2 or B1 or B2 or Wiso or "abschluss" when unten => Pruefung,
        "ap1scenarios" => unten ? Pruefung : Teil1,
        "scenarios" or "testproject" => unten ? Pruefung : Teil2,
        "cards" or "notebook" or "calc" when unten => Navigation.LernenId,
        "progress" or "settings" or "help" when unten => Mehr,
        _ when Fachbereiche.Alle.Contains(id) => unten ? Navigation.LernenId : "cards",
        _ => null,
    };

    /// <summary>
    /// Seiten, die eine Seite mit einem Knopf oeffnet (Weisung P12-406, Abnahmesatz „jede bisherige Seite in hoechstens zwei
    /// Klicks“): Teil 1 und die Bereiche den Pruefungstrainer (Üben, Probeklausur, echtes Format) und ihre Szenarien, Teil 2 die
    /// Bereiche und das Projekt, die Sammelseiten ihre Eintraege, Karteikarten die Fachbereiche. <paramref name="mitSzenarien"/>:
    /// hat der Teil Szenarien (sonst fehlt die Zeile).
    /// </summary>
    public static IReadOnlyList<string> Ziele(string seite, bool mitSzenarien = true) => seite switch
    {
        // A 1.17.0: „Prüfung“ zeigt AP1 (zuerst) mit der Segment-Leiste; die andere Haelfte ist ein Klick auf das Segment
        Pruefung => [Teil1, Teil2],
        Teil1 => mitSzenarien ? [Teil2, "quiz", "ap1scenarios"] : [Teil2, "quiz"],
        Teil2 => [Teil1, B1, B2, Wiso, "testproject", "abschluss"],
        B1 or B2 or Wiso => mitSzenarien ? ["quiz", "scenarios"] : ["quiz"],
        Navigation.LernenId => [.. Navigation.Lernen.Select(e => e.Id)],
        Mehr => [.. MehrEintraege.Select(e => e.Id)],
        "cards" => Fachbereiche.Alle,
        _ => [],
    };

    // -- Brotkrumen der neuen Seiten (die bisherigen Seiten behalten ihre Kopfzeile, G9 kopfleiste_pc/_handy) ------------
    /// <summary>A 1.17.0 (Entwurf P12-271): Brotkrumen „PRÜFUNG / AP1“, unter AP2 „AP2 / BEREICH 1“.</summary>
    public const string KopfAbschluss = "PRÜFUNG";
    public const string KopfTeil2 = "AP2";

    /// <summary>Brotkrumen einer neuen Seite (null = keine neue Seite). PC mit vollem Namen, Handy kurz (der Name steht dort als Überschrift).</summary>
    public static Kopftitel? Kopf(string id, Fachrichtung fachrichtung, bool pc) => id switch
    {
        Teil1 => new(id, KopfAbschluss, Teil1Titel),
        Teil2 => new(id, KopfAbschluss, Teil2Titel),
        B1 or B2 or Wiso => new(id, KopfTeil2, pc ? Beschriftungen.Gross(Deckblattname(fachrichtung, id)) : Beschriftungen.Gross(BereichKurz(id))),
        Mehr => MehrKopf,
        _ => null,
    };

    /// <summary>Kurzer Name eines Bereichs wie auf der Karte: „Bereich 1“, „Bereich 2“, „WiSo“.</summary>
    public static string BereichKurz(string rolle) => rolle switch
    {
        B1 => "Bereich 1",
        B2 => "Bereich 2",
        Wiso => "WiSo",
        _ => Teil1Kurz,
    };

    // -- Texte der Seiten -----------------------------------------------------------------------
    public const string Ueben = "Üben";
    public const string Pruefen = "Prüfen";
    public const string KarteBereich = "%s · %d %%";
    public const string Projekt = "PROJEKT";
    public const string ProjektName = "Betriebliche Projektarbeit";
    public const string ProjektAngabe = "Antrag, Dokumentation, Präsentation, Fachgespräch · %d Testprojekte zum Üben";
    public const string Testprojekte = "Testprojekte";
    public const string MeinProjekt = "Mein Projekt";
    public const string AngabeTeil = "Üben: %d Fragen, %d Szenarien · Prüfen: Probeklausur, %d Sätze im echten Format";
    public const string AngabeTeilOhneSaetze = "Üben: %d Fragen, %d Szenarien · Prüfen: Probeklausur";
    public const string AngabeWiso = "Üben: %d Fragen · Prüfen: %d Aufgaben in %d Minuten";

    public const string UebenTitel = "ÜBEN · OHNE UHR, OHNE NOTE";
    public const string Pruefungsfragen = "Prüfungsfragen";
    public const string FragenAngabe = "%d Fragen zu den Themen dieses Bereichs";
    public const string Ap1Szenarien = "AP1-Szenarien";
    public const string Ap2Szenarien = "AP2-Szenarien";
    public const string SzenarienAngabe = "%d Fallaufgaben";
    public const string Einzelaufgabe = "Einzelne Aufgabe im echten Format";
    public const string EinzelaufgabeAngabe = "eine Aufgabe aus einem Satz, mit Musterlösung";
    public const string AufgabeWaehlen = "Aufgabe wählen";
    public const string StandTitel = "DEIN STAND";
    public const string Stand = "%d Fragen: %d bearbeitet, %d %% richtig";
    public const string StandSzenarien = " · Szenarien %d von %d";
    public const string AlleZeigen = "Alle zeigen";
    public const string NurTeil = "Nur %s";
    public const string KeineFragen = "Zu diesem Bereich gibt es noch keine Fragen.";

    public const string Probeklausur = "PROBEKLAUSUR";
    public const string ProbeAngabe = "%d min · %d Aufgaben · Note nach IHK-Schlüssel";
    public const string Starten = "Starten";
    public const string EchtesFormat = "ECHTES FORMAT";
    public const string EchtAngabe = "Sätze: %d, davon bearbeitet: %d";
    public const string NaechsterSatz = "Nächster Satz";
    public const string SatzWaehlen = "Satz wählen";
    public const string SatzWaehlenTitel = "Satz wählen";
    public const string SatzZeile = "Satz %d · %s";
    public const string SatzBearbeitet = " · bearbeitet";
    public const string SatzAngabe = "%d min · %d Aufgaben";
    public const string AufgabeWaehlenTitel = "Aufgabe wählen";
    public const string AufgabeZeile = "Aufgabe %d · %s Punkte";
    public const string Musterloesung = "Musterlösung zeigen";
    public const string MusterloesungTitel = "MUSTERLÖSUNG";
    public const string Zurueck = "Zurück";

    /// <summary>Aufgaben einer Probeklausur: WiSo 30, sonst 4.</summary>
    public static int Aufgaben(string rolle) => rolle == Wiso ? Klausur.WisoAnzahl : 4;

    // -- Start: Feld „Deine Abschlussprüfung“ (Nico 4a) ---------------------------------------------
    public const string StartTitel = "Deine Abschlussprüfung";
    public const string StartUntertitel = "%s · aufgebaut wie die echte Prüfung";
    public const string StartTeil1 = "TEIL 1 · AP1 · 20 %";
    public const string StartTeil2 = "TEIL 2 · AP2 · 80 %";
    public const string StartTeil2Name = "%s, %s, Wirtschafts- und Sozialkunde und Projekt";
    public const string StartOeffnen = "Öffnen";
}
