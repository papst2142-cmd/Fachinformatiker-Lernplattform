using System.Text.Json.Nodes;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Phase 8 W10 (Sammelliste 148, Nico 1b): Hinweisfeld der Stadt auf- oder zugeklappt, PC und Handy
/// gleich. Schluessel <c>spiel_hinweis</c> mit "offen" oder "zu"; fehlt er oder ist er unbekannt, gilt
/// offen. Geschrieben wird nur ueber <see cref="Daten.Einstellungen.Aendern"/>.
/// </summary>
public static class HinweisWahl
{
    public const string Schluessel = "spiel_hinweis";
    public const string Offen = "offen";
    public const string Zu = "zu";

    /// <summary>So lange steht eine kurze Meldung neben dem zugeklappten Pfeil.</summary>
    public static readonly TimeSpan MeldungDauer = TimeSpan.FromSeconds(5);

    public static bool Zugeklappt(JsonObject werte) =>
        werte[Schluessel] is JsonValue v && v.TryGetValue<string>(out var wert) && wert == Zu;

    public static bool Setzen(Daten.Einstellungen einstellungen, bool zu) => einstellungen.Aendern(werte => Wert(werte, zu));

    public static void Wert(JsonObject werte, bool zu) => werte[Schluessel] = zu ? Zu : Offen;

    /// <summary>
    /// Text, der zugeklappt kurz neben dem Pfeil steht: jede Zeile ausser dem langen Bedienhinweis
    /// (der steht nur offen). Leer = keine Meldung.
    /// </summary>
    public static string KurzeMeldung(string zeile, string bedienhinweis)
    {
        if (string.IsNullOrWhiteSpace(zeile) || zeile == bedienhinweis || zeile == Texte.SpielProbeTexte.ImFreien)
        {
            return "";
        }
        return zeile;
    }
}
