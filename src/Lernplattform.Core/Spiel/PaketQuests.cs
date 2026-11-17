using System.Runtime.CompilerServices;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 0.89.0 (Inhaltspakete W3, Weisung P12-297 B, Nico b und 5a): Auftraege („Quests“) in Staedten aus Paketen.
/// <para><b>Regel</b>, damit zwei Geraete gleich rechnen (wie FW8):</para>
/// <list type="number">
/// <item>Eine Paket-Stadt bringt nur ihre <b>neuen</b> Quests mit (<c>stadtdaten.quests</c> mit Typ, Merkmal der Quest-Ebene);
/// umgehaengte Auftraege von Stadt 1 gelten dort nicht (sie bleiben in Stadt 1).</item>
/// <item>Im Spielstand traegt jede ihre Kennung mit dem Vorsatz der Stadt (<c>kennung:qn-1</c>, ebenso <c>nach</c>); kein
/// Paket ueberdeckt einen Auftrag von Stadt 1 oder eines anderen Pakets.</item>
/// <item>Offen ist eine Quest, wenn ihr Tag erreicht ist (<c>ab_tag</c>), sie nicht geloest ist, heute noch nicht beantwortet
/// wurde und ihr Vorgaenger (<c>nach</c>) geloest ist. Sie gehoert nicht zu den Tickets des Tages: keine Tagesgrenze, sie
/// nimmt der Bitweiche keinen Platz, haelt keinen Feierabend auf und gilt auch mit eigener Firma.</item>
/// <item>Loesen schreibt das vorhandene Ereignis <see cref="Ereignisse.Erledigt"/> (keine neue Ereignisart) mit allen Werten
/// der Bewertung (Geld, Ruf, richtig). Der Spielstand wendet es ohne die Daten des Pakets an: ein Geraet ohne das Paket
/// rechnet Geld, Ruf, geloeste Auftraege und Tickets genau gleich.</item>
/// <item>Fehlt das Paket (entfernt oder auf einem anderen Geraet), ruhen seine Quests: sie sind nicht zu sehen, nichts geht
/// verloren; nach dem erneuten Laden sind geloeste weiter geloest und offene wieder da (Nico 5a).</item>
/// </list>
/// Stadt 1, die Fingerabdruecke und alte Spielstaende bleiben bitgleich (in Stadt 1 traegt kein Auftrag einen Vorsatz).
/// </summary>
public static class PaketQuests
{
    private static readonly ConditionalWeakTable<Stadt, IReadOnlyList<PyObjekt>> Cache = [];

    /// <summary>Vorsatz der falsch gelieferten Ware (kein Paket: Kennung <c>falschlieferung</c> ist darum reserviert).</summary>
    private static readonly string FalschVorsatz = Falschlieferung[..^1];

    /// <summary>Kennung eines Auftrags aus einem Paket (Vorsatz <c>kennung:</c>, nie die falsch gelieferte Ware)?</summary>
    public static bool IstPaketAufgabe(object? aufgabe) =>
        aufgabe is string s && s.IndexOf(':', StringComparison.Ordinal) is var i and > 0 && s[..i] != FalschVorsatz;

    /// <summary>
    /// Zaehlt ein heute bearbeiteter Auftrag NICHT zu den Tickets des Tages (Quest einer Paket-Stadt)? Regel 3; Gegenprobe
    /// <see cref="ImTagAltFuerTests"/>: wie ohne diese Regel (die Quest naehme der Bitweiche einen Platz).
    /// </summary>
    public static bool NichtImTag(object? aufgabe) => !ImTagAlt.Value && IstPaketAufgabe(aufgabe);

    private static readonly AsyncLocal<bool> ImTagAlt = new();

    /// <summary>Nur Gegenprobe Regel 3 (Weisung P12-297 B).</summary>
    public static bool ImTagAltFuerTests
    {
        get => ImTagAlt.Value;
        set => ImTagAlt.Value = value;
    }

    /// <summary>Kennung des Pakets eines Auftrags (null = Stadt 1).</summary>
    public static string? PaketVon(object? aufgabe) => IstPaketAufgabe(aufgabe) ? ((string)aufgabe!)[..((string)aufgabe!).IndexOf(':', StringComparison.Ordinal)] : null;

    /// <summary>
    /// Alle Quests der Paket-Stadt mit Vorsatz (Kopien; Questgeber und Ort bleiben die der Paket-Stadt). Stadt 1: keine.
    /// </summary>
    public static IReadOnlyList<PyObjekt> Alle(Stadt stadt)
    {
        if (stadt.Vorsatz is not { } vorsatz || stadt.Haupt is not { } haupt)
        {
            return [];
        }
        return Cache.GetValue(stadt, s =>
        {
            var bestand = Liste(haupt.Inhalt["aufgaben"]).Select(a => Py.Str(Objekt(a)["id"])).ToHashSet(StringComparer.Ordinal);
            var ergebnis = new List<PyObjekt>();
            foreach (var a in Liste(s.Inhalt["aufgaben"]).Select(Objekt))
            {
                if (!Py.Wahr(Get(a, Questebene.Merkmal)) || bestand.Contains(Py.Str(a["id"])) || !Questebene.Typen.Contains(Py.Str(a["typ"])))
                {
                    continue;
                }
                var kopie = Py.Flach(a);
                Py.Setzen(kopie, "id", vorsatz + Py.Str(a["id"]));
                if (Py.Wahr(Get(a, "nach")))
                {
                    Py.Setzen(kopie, "nach", vorsatz + Py.Str(a["nach"]));
                }
                ergebnis.Add(kopie);
            }
            return ergebnis;
        });
    }

    /// <summary>Die heute offenen Quests der Paket-Stadt (Reihenfolge der Datei).</summary>
    public static List<PyObjekt> Offen(Stadt stadt, Spielstand stand) =>
        [.. Alle(stadt).Where(a => Py.F(Get(a, "ab_tag", 1L)) <= stand.Tag
            && !stand.Geloest.Contains(a["id"])
            && !stand.Bearbeitet.ContainsKey(a["id"])
            && (!Py.Wahr(Get(a, "nach")) || stand.Geloest.Contains(a["nach"])))];

    /// <summary>Ein Auftrag der Paket-Stadt nach Kennung (mit Vorsatz), auch geloest; sonst null.</summary>
    public static PyObjekt? Auftrag(Stadt stadt, object? aufgabe) => Alle(stadt).FirstOrDefault(a => Py.Gleich(a["id"], aufgabe));

    /// <summary>Offene Quests je Questgeber (mit Ort nur die an diesem Ort), wie <see cref="Spielstand.Auftraege"/>.</summary>
    public static PyDict Auftraege(Stadt stadt, Spielstand stand, object? ort = null)
    {
        var ergebnis = new PyDict();
        foreach (var auftrag in Offen(stadt, stand))
        {
            if (ort is not null && !Py.Gleich(AuftragOrt(auftrag, stadt.Inhalt), ort))
            {
                continue;
            }
            ((List<object?>)ergebnis.SetDefault(auftrag["auftraggeber"], new List<object?>())!).Add(auftrag);
        }
        return ergebnis;
    }

    /// <summary>
    /// Ereignisse zum Loesen einer offenen Quest der Paket-Stadt; die Figur muss in dieser Stadt sein. Sonst
    /// <see cref="SpielFehler"/> mit Grund (nichts geschrieben).
    /// </summary>
    public static List<(string Art, PyObjekt Daten)> Loesen(Stadt stadt, Spielstand stand, object? aufgabe, object? antwort, bool hilfe, PyObjekt wissen)
    {
        if (stadt.Vorsatz is null || stadt.Fremd(stadt.Stand(stand).Ort))
        {
            throw new SpielFehler(Texte.ZugTexte.QuestAndereStadt);
        }
        if (Offen(stadt, stand).FirstOrDefault(a => Py.Gleich(a["id"], aufgabe)) is not { } auftrag)
        {
            throw new SpielFehler("Dieses Ticket ist heute nicht (mehr) offen.");
        }
        var daten = Bewertung.Auswerten(auftrag, antwort, hilfe, wissen, stand.Tag, Bewertung.EinfachBalancing(stand), null, stadt.Inhalt, null);
        return [(Ereignisse.Erledigt, daten)];
    }

    /// <summary>Text und Knopf eines Questgebers der Paket-Stadt (wie eine Person im Buero, „Auftrag annehmen“).</summary>
    public static (string Titel, string Text, List<(string Aktion, string Name)> Knoepfe) Nachricht(Stadt stadt, Spielstand stand, PyObjekt person, object? ort)
    {
        var auftraege = Auftraege(stadt, stand, ort);
        var (titel, text) = SpielOrte.BueroNachricht(0, 0, person, auftraege, new PyObjekt { { "raeume", new List<object?>() } }, stand);
        var knoepfe = new List<(string, string)>();
        if (auftraege.Get(person["id"]) is List<object?> { Count: > 0 } aufgaben)
        {
            knoepfe.Add(("auftrag:" + Py.Str(Objekt(aufgaben[0])["id"]), "Auftrag annehmen"));
        }
        return (titel, text, knoepfe);
    }
}
