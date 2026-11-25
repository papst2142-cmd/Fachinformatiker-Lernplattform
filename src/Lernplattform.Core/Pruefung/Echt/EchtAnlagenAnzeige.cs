namespace Lernplattform.Core.Pruefung.Echt;

/// <summary>
/// Patch A 0.84.1 (Sammelliste 363, Weisung P12-286): welche Anlagen neben einer Aufgabe stehen und welche Zahl der
/// Umschalter "Anlagen (n)" nennt. Beides kommt aus derselben Rechnung <see cref="Von"/>; bis A 0.84.0 nannte der
/// Umschalter die Zahl aller Anlagen des Satzes, gezeigt wurden aber nur die der Aufgabe.
/// </summary>
public static class EchtAnlagenAnzeige
{
    private static readonly AsyncLocal<bool> ZahlAlt = new();

    /// <summary>Gegenprobe 363: der Umschalter nennt die Zahl aller Anlagen des Satzes wie bis A 0.84.0 (nur Tests, je Ablauf).</summary>
    public static bool ZahlAltFuerTests { get => ZahlAlt.Value; set => ZahlAlt.Value = value; }

    private static readonly AsyncLocal<bool> VolleBreiteAlt = new();

    /// <summary>Gegenprobe 456: Aufgaben mit Baukasten stehen wie bis A 1.10.0 neben ihren Anlagen (nur Tests, je Ablauf).</summary>
    public static bool VolleBreiteAltFuerTests { get => VolleBreiteAlt.Value; set => VolleBreiteAlt.Value = value; }

    /// <summary>
    /// A 1.11.0 (Sammelliste 456, Weisungen P12-385/P12-386): eine Aufgabe mit mindestens einer Zeichenaufgabe (im Lauf mit
    /// Baukasten) steht im breiten Fenster in voller Breite, ihre Anlagen darunter; der Umschalter "Aufgabe / Anlagen (n)"
    /// bleibt und zeigt unter "Anlagen" die Anlagen daneben. Aufgaben ohne Zeichenaufgabe bleiben in zwei Spalten.
    /// </summary>
    public static bool VolleBreite(EchtAufgabe aufgabe) =>
        !VolleBreiteAlt.Value && aufgabe.Teile.Any(t => t.Antwort.Form == EchtSaetze.FormZeichnen);

    /// <summary>Anlagen, auf die die Aufgabe verweist (alle, wenn sie auf keine verweist), in der Reihenfolge des Satzes.</summary>
    public static IReadOnlyList<EchtAnlage> Von(EchtSatz satz, EchtAufgabe aufgabe)
    {
        var nr = aufgabe.Teile.SelectMany(t => t.Anlagen).ToHashSet(StringComparer.Ordinal);
        return nr.Count == 0 ? satz.Anlagen : [.. satz.Anlagen.Where(a => nr.Contains(a.Nr))];
    }

    /// <summary>Zahl im Umschalter "Anlagen (n)" fuer diese Aufgabe: so viele Anlagen, wie <see cref="Von"/> zeigt.</summary>
    public static int UmschalterZahl(EchtSatz satz, EchtAufgabe aufgabe) => ZahlAlt.Value ? satz.Anlagen.Count : Von(satz, aufgabe).Count;
}
