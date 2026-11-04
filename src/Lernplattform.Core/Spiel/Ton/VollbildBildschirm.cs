using System.Globalization;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Spiel.Ton;

/// <summary>Ein Bildschirm des PCs: Lage und Groesse in Bildpunkten, Hauptbildschirm, Name des Systems (oft leer).</summary>
public sealed record Bildschirm(int X, int Y, int Breite, int Hoehe, bool Haupt = false, string Name = "");

/// <summary>
/// Patch A 0.37.0 (Sammelliste 205, Wunsch Nico, Weisungen P11-33/P11-34): Bildschirm fuer das Vollbild am PC fest
/// waehlen. Ohne Merker (Vorgabe "Bildschirm des Fensters") wie bisher der Bildschirm, auf dem das Fenster liegt.
/// Gemerkt wird Lage, Groesse und Name unter <see cref="Schluessel"/> (nur ueber <see cref="Einstellungen.Aendern"/>).
/// Gefunden wird zuerst nach Lage und Groesse, sonst nach dem Namen (nur wenn genau einer so heisst); fehlt der
/// Bildschirm, gilt der des Fensters und der Merker bleibt stehen (der Bildschirm kann wieder angesteckt werden).
/// </summary>
public static class VollbildBildschirm
{
    public const string Schluessel = "vollbild_bildschirm";

    /// <summary>Gemerkter Bildschirm oder null (Vorgabe, auch bei kaputtem Eintrag).</summary>
    public static Bildschirm? Lesen(JsonObject werte)
    {
        if (werte[Schluessel] is not JsonObject o)
        {
            return null;
        }
        static int? Zahl(JsonNode? n) => n is JsonValue v && v.TryGetValue<int>(out var z) ? z : null;
        if (Zahl(o["x"]) is not { } x || Zahl(o["y"]) is not { } y || Zahl(o["breite"]) is not { } b || Zahl(o["hoehe"]) is not { } h
            || b <= 0 || h <= 0)
        {
            return null;
        }
        var name = o["name"] is JsonValue nv && nv.TryGetValue<string>(out var t) ? t : "";
        return new Bildschirm(x, y, b, h, false, name);
    }

    /// <summary>Merker setzen (null = Vorgabe "Bildschirm des Fensters", Schluessel weg).</summary>
    public static void Wert(JsonObject werte, Bildschirm? bildschirm)
    {
        if (bildschirm is null)
        {
            werte.Remove(Schluessel);
            return;
        }
        werte[Schluessel] = new JsonObject
        {
            ["x"] = bildschirm.X,
            ["y"] = bildschirm.Y,
            ["breite"] = bildschirm.Breite,
            ["hoehe"] = bildschirm.Hoehe,
            ["name"] = bildschirm.Name,
        };
    }

    /// <summary>Stelle des gemerkten Bildschirms in der Liste, sonst null (dann gilt der Bildschirm des Fensters).</summary>
    public static int? Finden(IReadOnlyList<Bildschirm> alle, Bildschirm? gemerkt)
    {
        if (gemerkt is null)
        {
            return null;
        }
        for (var i = 0; i < alle.Count; i++)
        {
            var b = alle[i];
            if (b.X == gemerkt.X && b.Y == gemerkt.Y && b.Breite == gemerkt.Breite && b.Hoehe == gemerkt.Hoehe)
            {
                return i;
            }
        }
        if (gemerkt.Name.Length == 0)
        {
            return null;
        }
        var gleich = Enumerable.Range(0, alle.Count).Where(i => alle[i].Name == gemerkt.Name).ToList();
        return gleich.Count == 1 ? gleich[0] : null;
    }

    /// <summary>Eintraege der Auswahl: zuerst "Bildschirm des Fensters", dann "Bildschirm 1 (1920 × 1080, Hauptbildschirm)" …</summary>
    public static IReadOnlyList<string> Eintraege(IReadOnlyList<Bildschirm> alle)
    {
        var liste = new List<string> { TonTexte.VollbildFenster };
        for (var i = 0; i < alle.Count; i++)
        {
            var b = alle[i];
            liste.Add(TonTexte.VollbildNummer.Replace("%d", (i + 1).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                + " (" + b.Breite.ToString(CultureInfo.InvariantCulture) + " × " + b.Hoehe.ToString(CultureInfo.InvariantCulture)
                + (b.Haupt ? TonTexte.VollbildHaupt : "") + ")");
        }
        return liste;
    }
}
