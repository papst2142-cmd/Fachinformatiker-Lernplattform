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
