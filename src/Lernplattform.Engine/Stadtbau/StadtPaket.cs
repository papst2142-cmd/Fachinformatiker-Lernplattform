using System.Text.RegularExpressions;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle A 0.89.0 / S 0.57.0 (Inhaltspakete W3, Weisung P12-297 A, Plan P12-175 Abschnitt 4): eine Stadt, die als Paket gebaut
/// wird, traegt in ihren Stadtdaten den Abschnitt <see cref="Abschnitt"/> mit Kennung und Name des Pakets
/// (<c>{"kennung": "lindenau", "name": "Lindenau"}</c>). Das Werkzeug setzt ihn beim Anlegen mit „Neue Stadt (Paket)“; nur eine
/// leere Stadt (Kennung <c>neu_…</c>) darf ihn tragen, Stadt 1 nie (sie bleibt geschuetzt). Eine Datei mit dem Abschnitt bekommt
/// Formatnummer 4 (<see cref="StadtFassung.FormatFuer"/>), jede andere bleibt bitgleich. Die Regeln der Kennung sind die des
/// Pakets (<c>Paket.KennungGueltig</c>, Test gleich); nur Anzeige und Herkunft, das Spiel rechnet nichts damit.
/// </summary>
public static partial class StadtPaket
{
    /// <summary>Abschnitt der Stadtdaten.</summary>
    public const string Abschnitt = "paket";

    /// <summary>Hoechstlaenge des Namens (wie im Kopf eines Pakets).</summary>
    public const int NameHoechstens = 40;

    /// <summary>Kennungen, die kein Paket tragen darf (Stadt 1 und der Vorsatz der falsch gelieferten Ware).</summary>
    public static readonly IReadOnlyList<string> Reserviert = [AktiveFassung.Stadt1, "falschlieferung"];

    [GeneratedRegex(@"^[a-z][a-z0-9_]{1,31}\z", RegexOptions.CultureInvariant)]
    private static partial Regex KennungMuster();

    /// <summary>Hat die Stadt den Abschnitt?</summary>
    public static bool Hat(PyObjekt? stadtdaten) => stadtdaten is not null && Py.Hat(stadtdaten, Abschnitt);

    /// <summary>Kennung und Name des Pakets (null = keine Paket-Stadt oder Abschnitt falsch).</summary>
    public static (string Kennung, string Name)? Von(PyObjekt? stadtdaten) =>
        Hat(stadtdaten) && stadtdaten![Abschnitt] is PyObjekt p && p["kennung"] is string k && p["name"] is string n ? (k, n) : null;

    /// <summary>Grund, warum die Kennung nicht geht (null = gut).</summary>
    public static string? KennungFehler(string? kennung) =>
        kennung is null || !KennungMuster().IsMatch(kennung)
            ? "Die Kennung des Pakets braucht 2 bis 32 Zeichen: kleine Buchstaben a bis z, Ziffern und _, am Anfang ein Buchstabe."
            : Reserviert.Contains(kennung) ? $"Die Kennung „{kennung}“ ist vergeben (Stadt 1 oder Spiel)." : null;

    /// <summary>Grund, warum der Name nicht geht (null = gut).</summary>
    public static string? NameFehler(string? name) =>
        name is not { Length: > 0 and <= NameHoechstens } || name.Trim() != name
            ? $"Der Name des Pakets braucht 1 bis {NameHoechstens} Zeichen, ohne Leerzeichen am Anfang oder Ende."
            : null;

    /// <summary>Der Abschnitt (Kopie zum Setzen in die Stadtdaten).</summary>
    public static PyObjekt Neu(string kennung, string name) => new() { { "kennung", kennung }, { "name", name } };

    /// <summary>Alle Funde zum Abschnitt (leer = in Ordnung oder kein Abschnitt).</summary>
    public static List<string> Fehler(PyObjekt? stadtdaten, string stadtId)
    {
        if (!Hat(stadtdaten))
        {
            return [];
        }
        if (stadtdaten![Abschnitt] is not PyObjekt p || p.Count != 2 || !Py.Hat(p, "kennung") || !Py.Hat(p, "name"))
        {
            return ["Der Abschnitt „paket“ braucht genau die Felder „kennung“ und „name“."];
        }
        var funde = new List<string>();
        if (!stadtId.StartsWith("neu_", StringComparison.Ordinal))
        {
            funde.Add("Nur eine neue Stadt kann ein Paket sein; Stadt 1 bleibt geschützt.");
        }
        if (KennungFehler(p["kennung"] as string) is { } k)
        {
            funde.Add(k);
        }
        if (NameFehler(p["name"] as string) is { } n)
        {
            funde.Add(n);
        }
        return funde;
    }
}
