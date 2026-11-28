using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Lernplattform.Core.Pruefung.Echt;

/// <summary>
/// Ein Schritt eines Schreibtischtests: Zeile im Code und je Spalte eine Zelle (fester Wert, offen mit Loesung oder leer =
/// "unveraendert" mit <see cref="EchtZelle.Fest"/> = ""). <paramref name="Punkte"/>: Punkte fuer den ganzen Schritt (nur, wenn alle
/// offenen Zellen des Schritts richtig sind), null = Punkte je Zelle.
/// </summary>
public sealed record EchtSchreibSchritt(int Zeile, IReadOnlyList<EchtZelle> Werte, int? Punkte);

/// <summary>
/// A 1.20.0 (W2 Schreibtischtest, Weisung P12-446, Kurzplan P12-267 mit Nicos Antworten 1a bis 5a laut P12-419): Antwortform
/// "schreibtischtest" ab Format 6. Raster wie auf Papier: Spalte "Zeile" (Zeile im Code), je Variable eine Spalte, dazu
/// "Ausgabe" oder "Rückgabe". Lesen, Ladepruefung (Code-Anlage da, jede Zeile im Code, jede Spalte im Code, mindestens eine
/// offene Zelle, Punkte) und Bewertung je Zelle oder je Schritt. Ein kaputter Satz stuerzt nie ab (<see cref="EchtLadefehler"/>).
/// </summary>
public static class EchtSchreibtisch
{
    /// <summary>Spalten, die nicht im Code als Name stehen, sondern als Schluesselwort (gross oder klein).</summary>
    public static readonly IReadOnlyList<string> Sonderspalten = ["Ausgabe", "Rückgabe"];

    /// <summary>Hoechstens so viele Schritte und Spalten (Raster bleibt lesbar).</summary>
    public const int SchritteHoechstens = 40;
    public const int SpaltenHoechstens = 9;

    private static readonly Regex Nummer = new(@"^\s*([0-9]{1,3})(\s|$)", RegexOptions.CultureInvariant);
    private static readonly Regex Ganzzahl = new(@"^-?[0-9]+$", RegexOptions.CultureInvariant);

    internal static EchtAntwort Antwort(JsonObject a, string wo)
    {
        var code = EchtSaetze.Pflicht(a, "code");
        var spalten = EchtSaetze.Zeilen(a["spalten"], wo + " spalten");
        if (spalten.Count is < 1 or > SpaltenHoechstens || spalten.Any(s => s.Trim().Length == 0)
            || spalten.Distinct(StringComparer.Ordinal).Count() != spalten.Count)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture,
                $"{wo}: Schreibtischtest braucht 1 bis {SpaltenHoechstens} verschiedene Spalten"));
        }
        var leer = a["leer_unveraendert"] is JsonValue l && l.TryGetValue<bool>(out var ja) && ja;
        var schritte = new List<EchtSchreibSchritt>();
        foreach (var s in EchtSaetze.Liste(a["schritte"], wo + " schritte"))
        {
            var nr = schritte.Count + 1;
            var hier = string.Create(CultureInfo.InvariantCulture, $"{wo} Schritt {nr}");
            var zeile = EchtSaetze.Ganz(s["zeile"]) is { } z && z >= 1 ? z : throw new EchtLadefehler(hier + ": \"zeile\" fehlt oder ist kleiner als 1");
            if (s["werte"] is not JsonArray werte || werte.Count != spalten.Count)
            {
                throw new EchtLadefehler(hier + ": \"werte\" braucht so viele Zellen wie Spalten");
            }
            int? punkte = null;
            if (s["punkte"] is not null)
            {
                punkte = EchtSaetze.Ganz(s["punkte"]) is { } p && p >= 1 ? p : throw new EchtLadefehler(hier + ": \"punkte\" muss ab 1 sein");
            }
            var zellen = werte.Select(w => EchtSaetze.Zelle(w, hier)).ToList();
            if (punkte is not null && werte.Any(w => w is JsonObject o && o["punkte"] is not null))
            {
                throw new EchtLadefehler(hier + ": Punkte stehen entweder beim Schritt oder bei den Zellen");
            }
            if (punkte is not null && !zellen.Any(c => c.Offen))
            {
                throw new EchtLadefehler(hier + ": Schritt mit Punkten ohne offene Zelle");
            }
            if (!leer && zellen.Any(c => c.Fest == ""))
            {
                throw new EchtLadefehler(hier + ": leere Zelle, aber \"leer_unveraendert\" gilt nicht");
            }
            schritte.Add(new EchtSchreibSchritt(zeile, zellen, punkte));
        }
        if (schritte.Count > SchritteHoechstens)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: {schritte.Count} Schritte, höchstens {SchritteHoechstens}"));
        }
        if (!schritte.Any(s => s.Werte.Any(c => c.Offen)))
        {
            throw new EchtLadefehler(wo + ": Schreibtischtest ohne offene Zelle");
        }
        return new EchtAntwort(EchtSaetze.FormSchreibtischtest) { Code = code, Spalten = spalten, Schritte = schritte, LeerUnveraendert = leer };
    }

    /// <summary>Punkte eines Schreibtischtests: je offene Zelle ihre Punkte, ein Schritt mit Punkten zaehlt einmal.</summary>
    public static int Punkte(EchtAntwort a) => a.Schritte.Sum(s => s.Punkte ?? s.Werte.Where(c => c.Offen).Sum(c => c.Punkte));

    /// <summary>
    /// Ladepruefung mit dem Satz: die genannte Code-Anlage gibt es (Art "code") und die Teilaufgabe verweist auf sie; jede Zahl in
    /// "Zeile" steht im Code als Zeilennummer; jeder Spaltenname kommt im Code vor ("Ausgabe"/"Rückgabe" als Schluesselwort).
    /// </summary>
    internal static void Pruefen(string wo, EchtTeil teil, IReadOnlyList<EchtAnlage> anlagen)
    {
        var a = teil.Antwort;
        var code = anlagen.FirstOrDefault(x => x.Nr == a.Code);
        if (code is null || code.Art != "code")
        {
            throw new EchtLadefehler(wo + ": Code-Anlage " + a.Code + " gibt es nicht (oder sie ist keine Code-Anlage)");
        }
        if (!teil.Anlagen.Contains(a.Code))
        {
            throw new EchtLadefehler(wo + ": der Schreibtischtest verweist nicht auf seine Code-Anlage " + a.Code);
        }
        var nummern = Zeilennummern(code);
        if (a.Schritte.FirstOrDefault(s => !nummern.ContainsKey(s.Zeile)) is { } fehlt)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: Zeile {fehlt.Zeile} gibt es im Code der Anlage {a.Code} nicht"));
        }
        var text = string.Join("\n", code.Zeilen);
        foreach (var spalte in a.Spalten)
        {
            var da = Sonderspalten.Contains(spalte)
                ? text.Contains(spalte, StringComparison.OrdinalIgnoreCase)
                : text.Contains(spalte, StringComparison.Ordinal);
            if (!da)
            {
                throw new EchtLadefehler(wo + ": Spalte „" + spalte + "“ kommt im Code der Anlage " + a.Code + " nicht vor");
            }
        }
    }

    /// <summary>Zeilennummern einer Code-Anlage (Nummer am Zeilenanfang) mit dem Index der Zeile in <see cref="EchtAnlage.Zeilen"/>.</summary>
    public static IReadOnlyDictionary<int, int> Zeilennummern(EchtAnlage code)
    {
        var nummern = new Dictionary<int, int>();
        for (var i = 0; i < code.Zeilen.Count; i++)
        {
            var treffer = Nummer.Match(code.Zeilen[i]);
            if (treffer.Success)
            {
                nummern.TryAdd(int.Parse(treffer.Groups[1].Value, CultureInfo.InvariantCulture), i);
            }
        }
        return nummern;
    }

    /// <summary>Ist eine Zelle richtig? Ganze Zahlen als Zahl (mit Tausenderpunkt-Regel), sonst als Text.</summary>
    public static bool Richtig(EchtZelle zelle, string? eingabe) =>
        EchtBewertung.Passt(zelle.Loesung.All(l => Ganzzahl.IsMatch(l.Trim())) ? "zahl" : "text", eingabe, zelle.Loesung);

    /// <summary>
    /// Bewertung: je offene Zelle richtig/falsch (Reihenfolge Schritt fuer Schritt, Spalte fuer Spalte) und die Punkte: je Zelle
    /// ihre Punkte, ein Schritt mit Punkten nur, wenn alle seine offenen Zellen richtig sind.
    /// </summary>
    public static (IReadOnlyList<bool> Felder, int Punkte) Bewerten(EchtAntwort a, Func<int, int, string> antwort)
    {
        var felder = new List<bool>();
        var punkte = 0;
        for (var z = 0; z < a.Schritte.Count; z++)
        {
            var schritt = a.Schritte[z];
            var alle = true;
            for (var s = 0; s < schritt.Werte.Count; s++)
            {
                if (schritt.Werte[s] is { Offen: true } zelle)
                {
                    var ok = Richtig(zelle, antwort(z, s));
                    felder.Add(ok);
                    alle &= ok;
                    punkte += schritt.Punkte is null && ok ? zelle.Punkte : 0;
                }
            }
            punkte += schritt.Punkte is { } p && alle ? p : 0;
        }
        return (felder, punkte);
    }

    /// <summary>Name einer Zelle fuer den Screenreader: "Schritt 9, Zeile 4, i".</summary>
    public static string ZellName(EchtAntwort a, int schritt, int spalte) => string.Create(CultureInfo.InvariantCulture,
        $"Schritt {schritt + 1}, Zeile {a.Schritte[schritt].Zeile}, {a.Spalten[spalte]}");
}
