using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel.Ton;

/// <summary>Regler und Wahl der Karte "Musik und Geräusche" (Prozent 0 bis 100).</summary>
public sealed record TonWerte(int Gesamt, int Musik, int Umgebung, string MusikOrte)
{
    public float GesamtFaktor => Gesamt / 100f;

    public float MusikFaktor => Musik / 100f;

    public float UmgebungFaktor => Umgebung / 100f;
}

/// <summary>
/// Phase 8 W8 (Weisungen P8-16 E2 Punkt 2, P8-17 Nico 5a, P8-17b): Schluessel der Toene in
/// einstellungen.json, geschrieben nur ueber <see cref="Einstellungen.Aendern"/> (wie UpdateMerker),
/// Einstellungen.cs bleibt unveraendert. Falscher Wert = Vorgabe, ausserhalb 0 bis 100 geklemmt.
/// </summary>
public static class TonEinstellungen
{
    public const string Gesamt = "ton_gesamt";
    public const string Musik = "ton_musik";
    public const string Umgebung = "ton_umgebung";
    public const string MusikOrte = "ton_musik_orte";

    public const int GesamtVorgabe = 80;
    public const int MusikVorgabe = 50;
    public const int UmgebungVorgabe = 80;

    /// <summary>Wo eigene Lieder laufen (Nico P8-17b): nur in den Raeumen (Vorgabe), nur in der Stadt, ueberall.</summary>
    public const string Innen = "innen";
    public const string Stadt = "stadt";
    public const string Ueberall = "ueberall";

    public static readonly IReadOnlyList<string> Orte = [Innen, Stadt, Ueberall];

    public static TonWerte Lesen(JsonObject werte) => new(
        Zahl(werte, Gesamt, GesamtVorgabe),
        Zahl(werte, Musik, MusikVorgabe),
        Zahl(werte, Umgebung, UmgebungVorgabe),
        werte[MusikOrte] is JsonValue v && v.TryGetValue<string>(out var orte) && Orte.Contains(orte) ? orte : Innen);

    /// <summary>Ganze Zahl 0 bis 100 (geklemmt); kein Zahlwert (Text, Wahrheitswert, fehlt) = Vorgabe.</summary>
    public static int Zahl(JsonObject werte, string schluessel, int vorgabe)
    {
        if (werte[schluessel] is not JsonValue wert)
        {
            return vorgabe;
        }
        if (wert.TryGetValue<int>(out var ganz))
        {
            return Math.Clamp(ganz, 0, 100);
        }
        if (wert.TryGetValue<long>(out var lang))
        {
            return (int)Math.Clamp(lang, 0, 100);
        }
        if (wert.TryGetValue<double>(out var zahl) && double.IsFinite(zahl))
        {
            return (int)Math.Clamp(Math.Round(zahl), 0, 100);
        }
        return vorgabe;
    }

    public static bool Setzen(Einstellungen einstellungen, string schluessel, int prozent) =>
        einstellungen.Aendern(werte => Wert(werte, schluessel, prozent));

    public static bool OrteSetzen(Einstellungen einstellungen, string orte) => einstellungen.Aendern(werte => OrteWert(werte, orte));

    /// <summary>In einer Aenderung (Einstellungen.Aendern) setzen.</summary>
    public static void Wert(JsonObject werte, string schluessel, int prozent) => werte[schluessel] = Math.Clamp(prozent, 0, 100);

    public static void OrteWert(JsonObject werte, string orte) => werte[MusikOrte] = Orte.Contains(orte) ? orte : Innen;

    /// <summary>Laufen eigene Lieder hier (innen = in einem Raum)?</summary>
    public static bool MusikHier(string orte, bool innen) => orte switch
    {
        Ueberall => true,
        Stadt => !innen,
        _ => innen,
    };
}

/// <summary>Phase 8 W8 (Sammelliste 131, Nico 3a): Schalter "Vollbild", PC und Handy gleich.</summary>
public static class VollbildWahl
{
    public const string Schluessel = "vollbild";

    public static bool Lesen(JsonObject werte) => werte[Schluessel] is JsonValue v && v.TryGetValue<bool>(out var an) && an;

    public static bool Setzen(Einstellungen einstellungen, bool an) => einstellungen.Aendern(werte => Wert(werte, an));

    public static void Wert(JsonObject werte, bool an) => werte[Schluessel] = an;
}
