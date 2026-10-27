using System.Globalization;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Core.Texte;

/// <summary>
/// Texte der begehbaren Stadt (Phase 7 W4). Die Saetze zu den Gruenden, Wegdauer, Kopfzeile und
/// Hilfe-Satz hat Chat aus Meldung P7-6 freigegeben (Weisung P7-9, Fi-Austausch c79209c,
/// Abschnitt "Neue Nutzertexte"); "Spiel verlassen" nennt Nicos Wunsch (Weisung P7-9 Punkt 2).
/// Platzhalter wie in der Meldung (%s/%d) hier als {0}/{1}.
/// </summary>
public static class StadtTexte
{
    public const string KeinProfil = "Lege zuerst deine Spielfigur an.";
    public const string Unbekannt = "Diesen Ort gibt es nicht.";
    public const string Gesperrt = "Diesen Ort kennst du noch nicht.";
    public const string SchonDa = "Du bist schon hier.";
    public const string KeinWeg = "Dorthin führt kein Weg.";
    public const string Abend = "Es ist Abend. Du kannst nur noch nach Hause gehen.";
    public const string Geschlossen = "Geschlossen. Öffnet um {0} Uhr.";
    public const string OeffnetNichtMehr = "Geschlossen. Heute öffnet der Ort nicht mehr.";
    public const string NichtGeschlossen = "Der Ort ist geöffnet.";
    public const string Wegdauer = "{0} Schritte, {1} Minuten";
    public const string Kopfzeile = "Tag {0} · {1} Uhr";
    public const string HilfeSatz = "Im Spiel vergeht nur Spielzeit: Jeder Schritt kostet Minuten, die echte Uhr spielt keine Rolle.";

    /// <summary>Phase 8 W5 (Weisung P8-7 B2, Paar H1): Absatz der Hilfe zur Stadt in 3D, mit Kaufhaus und Bett; Phase 8 W6 (Paar F1): Festzeiten.</summary>
    public const string HilfeStadt = "Du bewegst dich durch eine Stadt in 3D: Tippe oder klicke auf einen Weg oder ein Haus, dann geht deine Figur dorthin. Im Möbelhaus und im Elektromarkt kaufst du ein, im Kaufhaus Kleidung für deine Figur. Zu Halloween, Weihnachten, Silvester und Ostern ist die Stadt geschmückt; im Kaufhaus gibt es dann passende Kleidung. In der Bank nimmst du Kredite auf, auf dem Amt gründest du deine Firma. Die Häuser haben Öffnungszeiten, ab 20 Uhr geht es nur noch nach Hause. Zum Feierabend gehst du zu Hause ins Bett. Kann dein Gerät keine 3D-Grafik zeigen, stehen die Orte in einer Liste.";
    public const string Warten = "Warten";
    public const string Verlassen = "Spiel verlassen";

    /// <summary>W6 (Weisung P7-11): Knopf zur Spieluebersicht ueber der Stadt; neuer Text, Vorschlag in Meldung P7-11.</summary>
    public const string Uebersicht = "Übersicht";

    /// <summary>Text zu einem Grund, warum eine Aktion nicht geht (Ok = leer).</summary>
    public static string Grund(StadtGrund grund, string? oeffnet = null) => grund switch
    {
        StadtGrund.Ok => "",
        StadtGrund.KeinProfil => KeinProfil,
        StadtGrund.Unbekannt => Unbekannt,
        StadtGrund.Gesperrt => Gesperrt,
        StadtGrund.SchonDa => SchonDa,
        StadtGrund.KeinWeg => KeinWeg,
        StadtGrund.Abend => Abend,
        StadtGrund.Geschlossen => string.Format(CultureInfo.InvariantCulture, Geschlossen, oeffnet ?? ""),
        StadtGrund.OeffnetNichtMehr => OeffnetNichtMehr,
        StadtGrund.NichtGeschlossen => NichtGeschlossen,
        _ => "",
    };
}
