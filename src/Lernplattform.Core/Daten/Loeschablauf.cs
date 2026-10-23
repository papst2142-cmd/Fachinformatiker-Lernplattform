namespace Lernplattform.Core.Daten;

/// <summary>
/// Phase 5 BE4: _delete_checked (app_gui) ohne Oberflaeche. Waehrend der Loeschung gehen Datenbanktexte
/// nicht an das allgemeine Fenster „Datenbankfehler“, sondern in eine Liste; danach ist der vorige
/// Empfaenger wieder gesetzt (auch bei einer Ausnahme). Die Oberflaeche zeigt dann genau ein Fenster:
/// Erfolg oder „Es wurde nichts gelöscht.“ mit den gesammelten Texten (LoeschenTexte.NichtsGeloeschtMit).
/// </summary>
public static class Loeschablauf
{
    /// <param name="loeschen">Die Loeschung (z. B. LernstandSchreiber.AllesLoeschen); true = gelungen.</param>
    /// <param name="lesen">Liest den jetzigen Empfaenger der Datenbanktexte.</param>
    /// <param name="setzen">Setzt den Empfaenger der Datenbanktexte.</param>
    public static (bool Geloescht, IReadOnlyList<string> Meldungen) Ausfuehren(Func<bool> loeschen, Func<Action<string>?> lesen, Action<Action<string>?> setzen)
    {
        ArgumentNullException.ThrowIfNull(loeschen);
        ArgumentNullException.ThrowIfNull(lesen);
        ArgumentNullException.ThrowIfNull(setzen);
        var gesammelt = new List<string>();
        var vorher = lesen();
        setzen(gesammelt.Add);
        try
        {
            return (loeschen(), gesammelt);
        }
        finally
        {
            setzen(vorher);
        }
    }
}
