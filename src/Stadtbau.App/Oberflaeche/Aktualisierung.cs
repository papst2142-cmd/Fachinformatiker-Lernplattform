using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Phase 13 Welle 2: Update des Werkzeugs am PC. Das Startprogramm setzt <see cref="Pruefen"/> (Velopack, nur Releases
/// der Linie vS); nach dem Laden fragt das Werkzeug einmal nach (nur bei einer neuen Version). Seit der Nachbesserung
/// S 0.5.1 (Weisung P12-42 Punkt 1) gibt es dazu den Knopf "Nach Updates suchen", der immer antwortet
/// (<see cref="UpdateSuche.Antwort"/>). Projekte sind nach jeder Aenderung gespeichert, das Update braucht deshalb
/// keine Kopie wie in der Lernplattform.
/// </summary>
public static class Aktualisierung
{
    /// <summary>
    /// Sucht eine neue Version (null = am Geraet gibt es kein Update von selbst). PC: Velopack (Stadtbau.Desktop), Tablet
    /// seit S 0.6.0: <see cref="TabletUpdate.SuchenAsync"/> (MainActivity des Werkzeugs).
    /// </summary>
    public static Func<Task<UpdateErgebnis>>? Pruefen { get; set; }

    internal static async Task NachLadenAsync(Action<string> fehler)
    {
        if (Pruefen is not { } pruefen)
        {
            return;
        }
        var e = await Task.Run(() => UpdateSuche.SuchenAsync(pruefen));
        if (e.Art == UpdateArt.Fehler)
        {
            fehler("Update: " + e.Grund);
        }
        if (UpdateSuche.BeimStartZeigen(e))
        {
            await AnbietenAsync(e, fehler);
        }
    }

    /// <summary>Knopf "Nach Updates suchen": sucht und antwortet immer mit einem Fenster.</summary>
    internal static async Task VonHandAsync(Action<string> fehler)
    {
        if (Pruefen is not { } pruefen)
        {
            return;
        }
        UpdateErgebnis e;
        using (Dialog.Warten(Texte.UpdateSucht))
        {
            e = await Task.Run(() => UpdateSuche.SuchenAsync(pruefen));
        }
        if (e.Art == UpdateArt.Fehler)
        {
            fehler("Update: " + e.Grund);
        }
        if (e.Art == UpdateArt.Neu)
        {
            await AnbietenAsync(e, fehler);
            return;
        }
        var (titel, text) = UpdateSuche.Antwort(e);
        await Dialog.Zeigen(titel, text, Texte.Ok);
    }

    private static async Task AnbietenAsync(UpdateErgebnis e, Action<string> fehler)
    {
        if (e.Angebot is not null)
        {
            // S 0.6.0: am Tablet laedt und prueft das Werkzeug selbst (Fenster fuer Erlaubnis und Fehler)
            await TabletUpdate.AnbietenAsync(e, fehler);
            return;
        }
        var (titel, text) = UpdateSuche.Antwort(e);
        if (e.Einspielen is null || await Dialog.Zeigen(titel, text, Texte.UpdateJetzt, Texte.UpdateSpaeter) != 0)
        {
            return;
        }
        try
        {
            using (Dialog.Warten(Texte.UpdateLaedt))
            {
                await e.Einspielen();
            }
        }
        catch (Exception x) when (x is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException)
        {
            fehler("Update: " + x.GetType().Name + ": " + x.Message);
            await Dialog.Zeigen(Texte.UpdateSuchen, Texte.UpdateFehler, Texte.Ok);
        }
    }
}
