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
/// Seit A 1.17.0 (Weisung P12-430, Reiter B): unten Start, Prüfung, Lernen, Spiel, Mehr; am PC Hauptpunkte Prüfung (AP1, AP2,
/// Bereiche, Abschlussprojekt) und Lernen (Karteikarten, Notizblock, Rechner), dann Spiel und Fortschritt. Sollwert-Aenderung Nr. 27.
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
        new(LeistenArt.Eintrag, Navigation.LernenId, LernenTitel),
        new(LeistenArt.Unter, "cards", "Karteikarten"),
        new(LeistenArt.Unter, "notebook", "Notizblock"),
        new(LeistenArt.Unter, "calc", "Rechner"),
        new(LeistenArt.Eintrag, "game", "Spiel"),
        new(LeistenArt.Eintrag, "progress", "Fortschritt"),
        // ENTWURF P12-439 (Frage 3a): am PC Hauptpunkt „Mehr“ mit Testprojekte und Mein Projekt
        new(LeistenArt.Eintrag, Mehr, MehrTitel),
        new(LeistenArt.Unter, "testproject", Testprojekte),
        new(LeistenArt.Unter, "abschluss", MeinProjekt),
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
        Teil1 or Teil2 or B1 or B2 or Wiso => Pruefung,
        "testproject" or "abschluss" => Mehr,
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
        // Weisung P12-432 (Nico 2b): Testprojekte und das eigene Projekt auch hier, damit sie am Handy wieder in zwei Klicks erreichbar sind
        new("testproject", "FLAG_ROUNDED", Testprojekte, "%d Kundenaufträge zum Üben", ["success"]),
        new("abschluss", "WORKSPACE_PREMIUM_ROUNDED", MeinProjekt, "Dein IHK-Projekt vom Antrag bis zum Fachgespräch", ["#7C3AED", "#22D3EE"]),
    ];

    public static readonly Kopftitel MehrKopf = new(Mehr, "MEHR", "ÜBERSICHT");

    /// <summary>Welche Seite der Seitenleiste bzw. der unteren Leiste zu einer Seite gehoert, die sie selbst nicht zeigt.</summary>
    public static string? Eltern(string id, bool unten) => id switch
    {
        Teil1 or Teil2 or B1 or B2 or Wiso when unten => Pruefung,
        "ap1scenarios" => unten ? Pruefung : Teil1,
        "scenarios" => unten ? Pruefung : Teil2,
        "testproject" or "abschluss" => Mehr,
        "cards" or "notebook" or "calc" when unten => Navigation.LernenId,
        "progress" or "settings" or "help" when unten => Mehr,
        _ when Fachbereiche.Alle.Contains(id) => unten ? Navigation.LernenId : "cards",
        _ => null,
    };

    /// <summary>
    /// Seiten, die eine Seite mit einem Knopf oeffnet (Weisung P12-406, Abnahmesatz „jede bisherige Seite in hoechstens zwei
    /// Klicks“): AP1 und AP2 die andere Haelfte, den Pruefungstrainer (eingebettet: Üben, Probeklausur, echtes Format) und ihre
    /// Szenarien (eingebettet), die Sammelseiten ihre Eintraege, Karteikarten die Fachbereiche. <paramref name="mitSzenarien"/>:
    /// hat der Teil Szenarien (sonst fehlt die Zeile).
    /// </summary>
    public static IReadOnlyList<string> Ziele(string seite, bool mitSzenarien = true) => seite switch
    {
        // A 1.17.0: „Prüfung“ zeigt zuerst AP1 mit der Segment-Leiste; die andere Haelfte ist ein Klick auf das Segment
        Pruefung => Ziele(Teil1, mitSzenarien),
        Teil1 => mitSzenarien ? [Teil2, "quiz", "ap1scenarios"] : [Teil2, "quiz"],
        // A 1.19.0 (Nico 1, 3): AP2 wie AP1, Pruefungstrainer und Szenarien eingebettet; Testprojekte und Mein Projekt nur unter „Mehr“
        Teil2 => mitSzenarien ? [Teil1, "quiz", "scenarios"] : [Teil1, "quiz"],
        // die Bereiche haben keine eigene Seite mehr, sie oeffnen AP2 (Nico 4a)
        B1 or B2 or Wiso => Ziele(Teil2, mitSzenarien),
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
        // am Handy wie im Entwurf mit der Fachrichtung (dort steht sonst nirgends, welche Pruefung es ist), am PC steht sie in der Seitenleiste
        Teil1 or Teil2 => new(id, pc ? KopfAbschluss : Beschriftungen.Einsetzen(GruppeAbschluss, FachrichtungTexte.KuerzelFuer(fachrichtung.Id)),
            id == Teil1 ? Teil1Titel : Teil2Titel),
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
    public const string Testprojekte = "Testprojekte";
    public const string MeinProjekt = "Mein Projekt";

    public const string UebenTitel = "ÜBEN · OHNE UHR, OHNE NOTE";
    public const string Pruefungsfragen = "Prüfungsfragen";
    public const string FragenAngabe = "%d Fragen zu den Themen dieses Bereichs";
    /// <summary>A 1.19.0 (Nico 1a): Zeile in AP1 und in jedem Bereich von AP2 (statt „AP1-/AP2-Szenarien“); Name „Übungsaufgaben“
    /// (Weisung P12-442, Nico 27.11.2026 19:34 MEZ, statt „Szenarien“ im Entwurf P12-278), auch in Zurück- und Stand-Zeile.</summary>
    public const string Szenarien = "Übungsaufgaben";
    public const string SzenarienAngabe = "%d Fallaufgaben";
    public const string Einzelaufgabe = "Einzelne Aufgabe im echten Format";
    public const string EinzelaufgabeAngabe = "eine Aufgabe aus einem Satz, mit Musterlösung";
    public const string AufgabeWaehlen = "Aufgabe wählen";
    public const string StandTitel = "DEIN STAND";
    public const string Stand = "%d Fragen: %d bearbeitet, %d %% richtig";
    public const string StandSzenarien = " · " + Szenarien + " %d von %d";
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

    // -- A 1.19.0 (Weisung P12-440, Entwurf P12-278): AP2-Zeilen, Zurück-Zeile, eingebettete Seiten -----------------
    public const string ProbeklausurZeile = "Probeklausur";
    public const string EchtesFormatZeile = "Echtes Format";
    /// <summary>Zurück-Zeile: „&lt;Teil&gt; · &lt;Ziel&gt;“, z. B. „Bereich 1 · Prüfungsfragen“.</summary>
    public const string ZurueckTitel = "%s · %s";
    public const string EinzelaufgabeKurz = "Einzelne Aufgabe";
    public const string AufgabeKurz = "Aufgabe %d";
    /// <summary>Zurück-Zeile eingebetteter Seiten, die nicht aus AP1/AP2 kommen (Weisung P12-440 5a, Anlage P12-278 Abschnitt 4).</summary>
    public const string HerkunftSuche = "Suche";
    public const string HerkunftNotizblock = "Notizblock";
    public const string HerkunftFortschritt = "Fortschritt";
    public const string HerkunftStart = "Start";
    public const string HerkunftSpiel = "Spiel";
    public const string Pruefungsfrage = "Prüfungsfrage";
    public const string Szenario = "Szenario";
    public const string Wiederholen = "Wiederholen";

    /// <summary>Aufgaben einer Probeklausur: WiSo 30, sonst 4.</summary>
    public static int Aufgaben(string rolle) => rolle == Wiso ? Klausur.WisoAnzahl : 4;

    // -- Start: Feld „Deine Abschlussprüfung“ (Nico 4a) ---------------------------------------------
    public const string StartTitel = "Deine Abschlussprüfung";
    public const string StartUntertitel = "%s · aufgebaut wie die echte Prüfung";
    public const string StartTeil1 = "AP1 · 20 %";
    public const string StartTeil2 = "AP2 · 80 %";
    public const string StartTeil2Name = "%s, %s, Wirtschafts- und Sozialkunde und Projekt";
    public const string StartOeffnen = "Öffnen";
}
