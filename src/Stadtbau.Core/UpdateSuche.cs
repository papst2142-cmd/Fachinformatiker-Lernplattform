namespace Stadtbau.Core;

/// <summary>Was die Suche nach einer neuen Version des Werkzeugs ergab.</summary>
public enum UpdateArt
{
    /// <summary>Es gibt eine neuere Version der Linie vS.</summary>
    Neu,

    /// <summary>Diese Version ist die neueste.</summary>
    Aktuell,

    /// <summary>Das Werkzeug laeuft nicht aus der Installation (dann gibt es kein Update von selbst).</summary>
    NichtInstalliert,

    /// <summary>Die Suche ging schief (kein Netz, GitHub nicht erreichbar).</summary>
    Fehler,
}

/// <summary>Ergebnis einer Suche; bei <see cref="UpdateArt.Neu"/> mit Version und Schritt "laden und neu starten".</summary>
public sealed record UpdateErgebnis(UpdateArt Art, string? Version = null, Func<Task>? Einspielen = null, string? Grund = null);

/// <summary>
/// Patch S 0.5.1 Nachbesserung (Weisung P12-42 Punkt 1): Knopf "Nach Updates suchen" am PC. Er gibt immer eine Antwort:
/// neue Version gefunden, "Du hast die neueste Version" oder ein Fehler in einfachen Worten. Beim Start fragt das
/// Werkzeug wie bisher nur, wenn es eine neue Version gibt (sonst still). Gesucht wird nur in der Linie vS
/// (<c>LinienQuelle</c>, Test in W2), nie bei der Lernplattform.
/// </summary>
public static class UpdateSuche
{
    /// <summary>Suche ausfuehren; ein Fehler (Netz, GitHub) wird zum Ergebnis <see cref="UpdateArt.Fehler"/>.</summary>
    public static async Task<UpdateErgebnis> SuchenAsync(Func<Task<UpdateErgebnis>> suche)
    {
        try
        {
            return await suche();
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException)
        {
            return new UpdateErgebnis(UpdateArt.Fehler, Grund: e.GetType().Name + ": " + e.Message);
        }
    }

    /// <summary>Zeigt der Start ein Fenster? Nur bei einer neuen Version (wie vor S 0.5.1).</summary>
    public static bool BeimStartZeigen(UpdateErgebnis e) => e.Art == UpdateArt.Neu;

    /// <summary>Titel und Text des Fensters nach "Nach Updates suchen" (jedes Ergebnis bekommt eine Antwort).</summary>
    public static (string Titel, string Text) Antwort(UpdateErgebnis e) => e.Art switch
    {
        UpdateArt.Neu => (Texte.UpdateNeuTitel, Texte.UpdateNeuText(e.Version)),
        UpdateArt.Aktuell => (Texte.UpdateSuchen, Texte.UpdateAktuell(Werkzeug.Anzeige)),
        UpdateArt.NichtInstalliert => (Texte.UpdateSuchen, Texte.UpdateNichtInstalliert),
        _ => (Texte.UpdateSuchen, Texte.UpdateFehler),
    };
}
