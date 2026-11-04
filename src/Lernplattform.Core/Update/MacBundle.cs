namespace Lernplattform.Core.Update;

/// <summary>
/// Phase 11 Patch A 0.37.0 (Sammelliste 203, Weisungen P11-32b/P11-34, Antwort 1c): am Mac ersetzt der
/// Velopack-Updater das ganze Programm-Bundle. Liegt es so, dass der Benutzer es nicht aendern darf (z. B.
/// mit dem Installer "fuer alle Benutzer" nach /Applications gelegt, Dateien gehoeren root), darf der Updater
/// nicht still laufen: er muss nach dem Passwort fragen, sonst startet die alte Version wieder.
/// </summary>
public static class MacBundle
{
    /// <summary>Name der Probedatei (wird sofort wieder geloescht).</summary>
    public const string Probedatei = ".lernplattform_schreibprobe";

    /// <summary>Das .app-Bundle, in dem die Programmdatei liegt (…/X.app/Contents/MacOS), sonst null.</summary>
    public static string? Von(string programmOrdner)
    {
        var ordner = new DirectoryInfo(programmOrdner);
        if (ordner.Name != "MacOS" || ordner.Parent is not { Name: "Contents" } contents || contents.Parent is not { } bundle
            || !bundle.Name.EndsWith(".app", StringComparison.Ordinal))
        {
            return null;
        }
        return bundle.FullName;
    }

    /// <summary>
    /// true, wenn der Benutzer im Bundle und in Contents/MacOS schreiben darf (Probedatei anlegen und loeschen).
    /// Jeder Fehler zaehlt als nicht beschreibbar (dann fragt der Updater lieber einmal zu viel nach dem Passwort).
    /// </summary>
    public static bool Beschreibbar(string bundle)
    {
        foreach (var ordner in new[] { bundle, Path.Combine(bundle, "Contents"), Path.Combine(bundle, "Contents", "MacOS") })
        {
            var pfad = Path.Combine(ordner, Probedatei);
            try
            {
                using (new FileStream(pfad, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
                {
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
        return true;
    }
}
