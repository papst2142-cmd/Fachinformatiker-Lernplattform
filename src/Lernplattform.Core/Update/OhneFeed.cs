namespace Lernplattform.Core.Update;

/// <summary>
/// Mac-Welle (Weisung P11-28, Sammelliste 200 = Fund M4): Findet Velopack am Mac kein Paket, kann das auch heissen,
/// dass das Release keinen Feed fuer diesen Mac hat (bis A 0.34.0 bewusst so, D7). Dann meldete das Programm
/// "neueste Version", obwohl es eine neuere gab. Am Mac fragt die Suche darum zusaetzlich die GitHub-Seite
/// (wie ein Ordner-Programm): ist dort eine neuere Version, kommt das Angebot mit der Download-Seite, ohne
/// Datei zum Einspielen (das Update-Fenster bietet dann nur die Seite an). Windows und Linux bleiben wie bisher.
/// </summary>
public static class OhneFeed
{
    /// <summary>Velopack-Angebot, sonst am Mac das der GitHub-Seite (ohne Datei), sonst null.</summary>
    public static async Task<UpdateAngebot?> SuchenAsync(Func<Task<UpdateAngebot?>> velopack, Func<Task<UpdateAngebot?>> github, bool mac)
    {
        var angebot = await velopack().ConfigureAwait(false);
        if (angebot is not null || !mac)
        {
            return angebot;
        }
        var seite = await github().ConfigureAwait(false);
        return seite is null ? null : seite with { DateiName = null, DateiUrl = null, Groesse = 0, Pruefsumme = null, Intern = null };
    }
}
