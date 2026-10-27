using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Phase 8 W8 (Weisung P8-15b/P8-17, Sammelliste 133, Nico 4b): Tasten des Spiels, am PC und am Handy
/// (angesteckte Tastatur) gleich. Tasten heissen wie die Avalonia-Tasten (<c>Key.ToString()</c>), damit der
/// Kern ohne Oberflaeche auskommt. Gespeichert werden nur die Abweichungen vom Standard unter
/// <see cref="Schluessel"/> ueber <see cref="Einstellungen.Aendern"/> (Einstellungen.cs unveraendert).
/// Unbekannte, gesperrte oder doppelte Eintraege gelten beim Lesen als Standard dieser Aufgabe.
/// </summary>
public sealed class Tastenbelegung
{
    public const string Schluessel = "tasten";

    public const string Links = "links";
    public const string Rechts = "rechts";
    public const string Hoch = "hoch";
    public const string Runter = "runter";
    public const string DrehLinks = "dreh_links";
    public const string DrehRechts = "dreh_rechts";
    public const string Naeher = "naeher";
    public const string Weiter = "weiter";
    public const string Hinein = "hinein";
    public const string Vollbild = "vollbild";

    /// <summary>Alle Aufgaben in der Reihenfolge der Karte, mit Text und Standardtasten (Zoomen auch Ziffernblock).</summary>
    public static readonly IReadOnlyList<(string Id, string Text, string[] Standard)> Aufgaben =
    [
        (Links, "Ansicht nach links schieben", ["Left"]),
        (Rechts, "Ansicht nach rechts schieben", ["Right"]),
        (Hoch, "Ansicht nach oben schieben", ["Up"]),
        (Runter, "Ansicht nach unten schieben", ["Down"]),
        (DrehLinks, "Nach links drehen", ["Q"]),
        (DrehRechts, "Nach rechts drehen", ["E"]),
        (Naeher, "Näher heran", ["OemPlus", "Add"]),
        (Weiter, "Weiter weg", ["OemMinus", "Subtract"]),
        (Hinein, "Hineingehen", ["Enter"]),
        (Vollbild, "Vollbild ein und aus", ["F11"]),
    ];

    /// <summary>Bleiben dem Programm (Esc bricht ab, die anderen braucht die Bedienung).</summary>
    public static readonly IReadOnlySet<string> Gesperrt = new HashSet<string>(StringComparer.Ordinal)
    {
        "Escape", "Tab", "LeftAlt", "RightAlt", "LeftCtrl", "RightCtrl", "LeftShift", "RightShift", "LWin", "RWin",
        "System", "None", "Apps", "Capital", "NumLock", "Scroll", "ImeProcessed", "DeadCharProcessed",
    };

    public const string Abbrechen = "Escape";

    /// <summary>Die Belegung, nach der Spiel und Hinweistext gerade gehen (setzt die Oberflaeche nach Lesen und Aendern).</summary>
    public static Tastenbelegung Aktuell { get; set; } = new();

    private readonly Dictionary<string, string> _eigen = new(StringComparer.Ordinal);

    /// <summary>Nur die Abweichungen vom Standard (Aufgabe -> Taste).</summary>
    public IReadOnlyDictionary<string, string> Eigen => _eigen;

    public bool IstStandard => _eigen.Count == 0;

    /// <summary>Tasten einer Aufgabe: eigene Wahl ersetzt alle Standardtasten.</summary>
    public IReadOnlyList<string> Tasten(string aufgabe) =>
        _eigen.TryGetValue(aufgabe, out var taste) ? [taste] : Aufgaben.First(a => a.Id == aufgabe).Standard;

    /// <summary>Aufgabe zur Taste oder null.</summary>
    public string? AufgabeFuer(string taste) => Aufgaben.Select(a => a.Id).FirstOrDefault(id => Tasten(id).Contains(taste));

    public static string Text(string aufgabe) => Aufgaben.First(a => a.Id == aufgabe).Text;

    public enum Ergebnis { Ok, Gesperrt, Doppelt }

    /// <summary>Taste fuer eine Aufgabe setzen; doppelt belegt oder gesperrt: nichts geaendert, <paramref name="andere"/> nennt die Aufgabe.</summary>
    public Ergebnis Setzen(string aufgabe, string taste, out string? andere)
    {
        andere = null;
        if (Gesperrt.Contains(taste))
        {
            return Ergebnis.Gesperrt;
        }
        var belegt = AufgabeFuer(taste);
        if (belegt is not null && belegt != aufgabe)
        {
            andere = belegt;
            return Ergebnis.Doppelt;
        }
        var standard = Aufgaben.First(a => a.Id == aufgabe).Standard;
        if (standard.Length == 1 && standard[0] == taste)
        {
            _eigen.Remove(aufgabe);
        }
        else
        {
            _eigen[aufgabe] = taste;
        }
        return Ergebnis.Ok;
    }

    /// <summary>Meldung bei Doppelbelegung (Text aus Meldung P8-15, von Chat angenommen).</summary>
    public static string DoppeltText(string taste, string andere) =>
        $"„{Anzeige(taste)}“ ist schon für „{Text(andere)}“ belegt. Bitte eine andere Taste wählen.";

    public Tastenbelegung Kopie()
    {
        var kopie = new Tastenbelegung();
        foreach (var (aufgabe, taste) in _eigen)
        {
            kopie._eigen[aufgabe] = taste;
        }
        return kopie;
    }

    /// <summary>Belegung aus einstellungen.json (fehlt oder kaputt = Standard).</summary>
    public static Tastenbelegung Lesen(JsonObject werte)
    {
        var belegung = new Tastenbelegung();
        if (werte[Schluessel] is not JsonObject eigen)
        {
            return belegung;
        }
        foreach (var (aufgabe, wert) in eigen)
        {
            if (Aufgaben.Any(a => a.Id == aufgabe) && wert is JsonValue v && v.TryGetValue<string>(out var taste)
                && taste.Length > 0 && !Gesperrt.Contains(taste))
            {
                // eine Taste, die schon eine andere Aufgabe hat, gilt nicht (Standard dieser Aufgabe)
                if (belegung.Setzen(aufgabe, taste, out _) != Ergebnis.Ok)
                {
                    continue;
                }
            }
        }
        return belegung;
    }

    public JsonObject AlsJson()
    {
        var json = new JsonObject();
        foreach (var (aufgabe, taste) in _eigen.OrderBy(p => Aufgaben.ToList().FindIndex(a => a.Id == p.Key)))
        {
            json[aufgabe] = taste;
        }
        return json;
    }

    /// <summary>Speichern (Standard = Schluessel entfernen).</summary>
    public bool Speichern(Einstellungen einstellungen) => einstellungen.Aendern(Schreiben);

    /// <summary>In einer Aenderung (Einstellungen.Aendern) setzen.</summary>
    public void Schreiben(JsonObject werte)
    {
        if (IstStandard)
        {
            werte.Remove(Schluessel);
        }
        else
        {
            werte[Schluessel] = AlsJson();
        }
    }

    /// <summary>Name einer Taste in der Karte und im Hinweistext.</summary>
    public static string Anzeige(string taste) => taste switch
    {
        "Left" => "Pfeil links",
        "Right" => "Pfeil rechts",
        "Up" => "Pfeil hoch",
        "Down" => "Pfeil runter",
        "OemPlus" or "Add" => "+",
        "OemMinus" or "Subtract" => "−",
        "Enter" or "Return" => "Eingabe",
        "Space" => "Leertaste",
        "Back" => "Rücktaste",
        "Delete" => "Entf",
        "Insert" => "Einfg",
        "Home" => "Pos1",
        "End" => "Ende",
        "PageUp" => "Bild hoch",
        "PageDown" => "Bild runter",
        "Multiply" => "*",
        "Divide" => "/",
        "Decimal" => ",",
        "OemComma" => ",",
        "OemPeriod" => ".",
        _ when taste.Length == 2 && taste[0] == 'D' && char.IsAsciiDigit(taste[1]) => taste[1..],
        _ when taste.StartsWith("NumPad", StringComparison.Ordinal) => "Ziffernblock " + taste["NumPad".Length..],
        _ => taste,
    };

    /// <summary>Tasten einer Aufgabe fuer die Anzeige ("+" fuer Plus und Ziffernblock-Plus nur einmal).</summary>
    public string AnzeigeFuer(string aufgabe) => string.Join(" oder ", Tasten(aufgabe).Select(Anzeige).Distinct(StringComparer.Ordinal));
}
