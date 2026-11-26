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

    /// <summary>Seiten eines Pruefungsteils mit den Reitern „Üben“ und „Prüfen“ (Teil 1 und die drei schriftlichen Bereiche).</summary>
    public static readonly IReadOnlyList<string> ReiterSeiten = [Teil1, B1, B2, Wiso];

    /// <summary>Rolle in <see cref="Fachrichtung.Bereiche"/> zu einer Seite.</summary>
    public static string Rolle(string seite) => seite == Teil1 ? "ap1" : seite;

    // -- Beschriftungen der Leisten --------------------------------------------------------------
    public const string GruppeAbschluss = "ABSCHLUSSPRÜFUNG %s";
    public const string GruppeLernen = "LERNEN";
    public const string GruppeSonstiges = "SONSTIGES";
    public const string Teil1Titel = "Teil 1 · AP1";
    public const string Teil2Titel = "Teil 2 · AP2";
    public const string Teil1Kurz = "Teil 1";
    public const string Teil2Kurz = "Teil 2";
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
    public static IReadOnlyList<LeistenZeile> Seitenleiste(Fachrichtung fachrichtung) =>
    [
        new(LeistenArt.Eintrag, "dashboard", "Start"),
        new(LeistenArt.Gruppe, null, Beschriftungen.Einsetzen(GruppeAbschluss, FachrichtungTexte.KuerzelFuer(fachrichtung.Id))),
        new(LeistenArt.Eintrag, Teil1, Teil1Titel),
        new(LeistenArt.Eintrag, Teil2, Teil2Titel),
        new(LeistenArt.Unter, B1, Seitenleistenname(fachrichtung, B1)),
        new(LeistenArt.Unter, B2, Seitenleistenname(fachrichtung, B2)),
        new(LeistenArt.Unter, Wiso, Deckblattname(fachrichtung, Wiso)),
        new(LeistenArt.Unter, "abschluss", Abschlussprojekt),
        new(LeistenArt.Gruppe, null, GruppeLernen),
        new(LeistenArt.Unter, "cards", "Karteikarten"),
        new(LeistenArt.Unter, "notebook", "Notizblock"),
        new(LeistenArt.Unter, "calc", "Rechner"),
        new(LeistenArt.Gruppe, null, GruppeSonstiges),
        new(LeistenArt.Eintrag, "game", "Spiel"),
        new(LeistenArt.Eintrag, "progress", "Fortschritt"),
    ];

    /// <summary>Unten angeheftet (ausserhalb des Bildlaufs): Hilfe und Optionen, darunter wie bisher „Programm beenden“, LERNSTATUS, Version.</summary>
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
    ];

    public static readonly Kopftitel MehrKopf = new(Mehr, "MEHR", "ÜBERSICHT");

    /// <summary>Welche Seite der Seitenleiste bzw. der unteren Leiste zu einer Seite gehoert, die sie selbst nicht zeigt.</summary>
    public static string? Eltern(string id, bool unten) => id switch
    {
        B1 or B2 or Wiso or "abschluss" when unten => Teil2,
        "ap1scenarios" => Teil1,
        "scenarios" or "testproject" => Teil2,
        "cards" or "notebook" or "calc" when unten => Navigation.LernenId,
        "progress" or "settings" or "help" when unten => Mehr,
        _ when Fachbereiche.Alle.Contains(id) => unten ? Navigation.LernenId : "cards",
        _ => null,
    };

    // -- Brotkrumen der neuen Seiten (die bisherigen Seiten behalten ihre Kopfzeile, G9 kopfleiste_pc/_handy) ------------
    public const string KopfAbschluss = "ABSCHLUSSPRÜFUNG";
    public const string KopfTeil2 = "TEIL 2";

    /// <summary>Brotkrumen einer neuen Seite (null = keine neue Seite). PC mit vollem Namen, Handy kurz (der Name steht dort als Überschrift).</summary>
    public static Kopftitel? Kopf(string id, Fachrichtung fachrichtung, bool pc) => id switch
    {
        Teil1 => new(id, KopfAbschluss, pc ? Beschriftungen.Gross(Teil1Titel) : Beschriftungen.Gross(Teil1Kurz)),
        Teil2 => new(id, KopfAbschluss, pc ? Beschriftungen.Gross(Teil2Titel) : Beschriftungen.Gross(Teil2Kurz)),
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
    public const string Teil2Hinweis = "Teil 2 zählt 80 %: drei schriftliche Teile am selben Tag und das Projekt. Jeder Teil hat „Üben“ und „Prüfen“.";
    public const string Teil2HinweisKurz = "%s · Teil 2 zählt 80 %%";
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
