using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 1.10.0 (Weisung P12-381, Nico 3c mit Hinweis 1a, Text aus Bild 08 P12-245): nach einem Umbau der Wohnung im
/// Werkzeug stehen Moebel, deren Platz nicht mehr passt, im Karton (<see cref="Wohnung.Umgebaut"/>). Beim ersten Betreten
/// danach erscheint einmal ein Hinweis, mehrere Moebel in einem Hinweis. „Einmal“ merkt sich das Programm in
/// <c>einstellungen.json</c> (Schluessel <see cref="Merker"/>, nur ueber <c>Einstellungen.Aendern</c>), nicht im Spielstand.
/// </summary>
public static partial class Wohnung
{
    /// <summary>Schluessel in einstellungen.json: Kennung des zuletzt gezeigten Umbau-Hinweises.</summary>
    public const string Merker = "wohnung_umbau_gesehen";

    public const string UmbauTitel = "Deine Wohnung wurde umgebaut.";

    /// <summary>Geschlecht der Moebel fuer den Satz (f, m, n); unbekannt = „Das Möbelstück …“.</summary>
    private static readonly Dictionary<string, char> Geschlecht = new(StringComparer.Ordinal)
    {
        ["matratze"] = 'f', ["bett_einzel"] = 'n', ["bett_doppel"] = 'n', ["sofa"] = 'n', ["sessel"] = 'm', ["sitzsack"] = 'm',
        ["couchtisch"] = 'm', ["fernseher"] = 'm', ["spielkonsole"] = 'f', ["esstisch"] = 'm', ["schreibtisch"] = 'm', ["regal"] = 'n',
        ["kleiderschrank"] = 'm', ["kommode"] = 'f', ["pflanze"] = 'f', ["teppich"] = 'm', ["stehlampe"] = 'f', ["aquarium"] = 'n',
        ["waschmaschine"] = 'f', ["liegestuhl"] = 'm', ["grill"] = 'm', ["sonnenschirm"] = 'm', ["hochbeet"] = 'n', ["bild"] = 'n',
    };

    private static string MitArtikel(object? kennung, PyObjekt inhalt, bool gross)
    {
        var name = MoebelStueck(kennung, inhalt) is { } m ? Py.Str(m["name"]) : Py.Str(kennung);
        var artikel = Geschlecht.TryGetValue(Py.Str(kennung), out var g) ? g switch { 'f' => "die", 'm' => "der", _ => "das" } : null;
        var text = artikel is null ? "das Möbelstück „" + name + "“" : artikel + " " + name;
        return gross ? char.ToUpperInvariant(text[0]) + text[1..] : text;
    }

    /// <summary>
    /// Text des Hinweises (null = nichts umgebaut). Ein Moebel: „Die Matratze passte nicht mehr an ihren Platz. Sie steht jetzt im
    /// Karton: tippe „Aufstellen“ und stell sie neu auf.“ (Wortlaut Bild 08); mehrere: „… und … passten …, Sie stehen …“.
    /// </summary>
    public static string? UmbauText(Spielstand stand)
    {
        var stuecke = Umgebaut(stand);
        if (stuecke.Count == 0)
        {
            return null;
        }
        if (stuecke.Count == 1)
        {
            var k = Py.Str(stuecke[0].Moebel);
            var g = Geschlecht.TryGetValue(k, out var x) ? x : 'n';
            var (sein, er, ihn) = g switch { 'f' => ("ihren", "Sie", "sie"), 'm' => ("seinen", "Er", "ihn"), _ => ("seinen", "Es", "es") };
            return MitArtikel(stuecke[0].Moebel, stand.Inhalt, true) + " passte nicht mehr an " + sein + " Platz. " + er
                + " steht jetzt im Karton: tippe „Aufstellen“ und stell " + ihn + " neu auf.";
        }
        var namen = stuecke.Select((s, i) => MitArtikel(s.Moebel, stand.Inhalt, i == 0)).ToList();
        var liste = string.Join(", ", namen.Take(namen.Count - 1)) + " und " + namen[^1];
        return liste + " passten nicht mehr an ihren Platz. Sie stehen jetzt im Karton: tippe „Aufstellen“ und stell sie neu auf.";
    }

    /// <summary>Kennung dieses Umbaus (Wohnung, Stuecke, Grundriss); gleich = Hinweis schon gezeigt. Null = nichts umgebaut.</summary>
    public static string? UmbauKennung(Spielstand stand)
    {
        var stuecke = Umgebaut(stand);
        if (stuecke.Count == 0)
        {
            return null;
        }
        var bleibe = Bleibe(stand.Wohnung, stand.Inhalt)!;
        var text = Py.Str(stand.Wohnung) + "|" + string.Join(",", stuecke.Select(s => s.Stueck)) + "|" + PythonJson.Standard(bleibe["gebaeude"]);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    /// <summary>Soll der Hinweis jetzt erscheinen (Umbau da und noch nicht gezeigt)?</summary>
    public static bool UmbauHinweisOffen(Spielstand stand, JsonObject werte) =>
        UmbauKennung(stand) is { } kennung && !(werte[Merker] is JsonValue v && v.TryGetValue<string>(out var alt) && alt == kennung);

    /// <summary>Den Hinweis als gezeigt merken (nur ueber Einstellungen.Aendern aufrufen).</summary>
    public static void UmbauGesehen(JsonObject werte, string kennung) => werte[Merker] = kennung;
}
