namespace Lernplattform.Core.Spiel.Ton;

/// <summary>Ausgabe auf das Audiogeraet (SoundFlow in der Oberflaeche); der Mischer liefert die Proben.</summary>
public interface ITonausgabe : IDisposable
{
    string Name { get; }

    /// <summary>Geraet oeffnen (beim ersten Mal) und abspielen; wirft, wenn es kein Geraet gibt.</summary>
    void Starten(Mischer mischer);

    /// <summary>Anhalten (Spiel verlassen, Hintergrund); Starten setzt fort.</summary>
    void Anhalten();
}

/// <summary>Stiller Dienst: spielt nichts (Pruefstand, kein Geraet); die Anlaesse zaehlt der Tondienst.</summary>
public sealed class StilleAusgabe(string grund) : ITonausgabe
{
    public string Name => "still (" + grund + ")";

    public void Starten(Mischer mischer)
    {
    }

    public void Anhalten()
    {
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// Phase 8 W8 (Teilplan 4, E2 P8-16 Punkt 6): Ausgabe waehlen. <c>LERNPLATTFORM_TON=aus</c> (Pruefstand) oder
/// ein Fehler beim Oeffnen (kein Audiogeraet) ergibt den stillen Dienst: kein Absturz, kein Eintrag in
/// fehler.log, nur eine Zeile fuer die Diagnose (<see cref="Zeile"/>).
/// </summary>
public static class Tonwahl
{
    public const string Schalter = "LERNPLATTFORM_TON";

    public static bool Aus(string? wert) => string.Equals(wert?.Trim(), "aus", StringComparison.OrdinalIgnoreCase);

    /// <summary>Zeile der letzten Wahl fuer die Diagnose (leer, solange das Spiel keinen Ton brauchte).</summary>
    public static string Zeile { get; private set; } = "";

    public static ITonausgabe Oeffnen(Mischer mischer, Func<ITonausgabe> fabrik, string? schalter)
    {
        if (Aus(schalter))
        {
            Zeile = "Ton: aus (" + Schalter + "=aus)";
            return new StilleAusgabe(Schalter + "=aus");
        }
        ITonausgabe? ausgabe = null;
        try
        {
            ausgabe = fabrik();
            ausgabe.Starten(mischer);
            Zeile = "Ton: " + ausgabe.Name;
            return ausgabe;
        }
        catch (Exception fehler)
        {
            try
            {
                ausgabe?.Dispose();
            }
            catch (Exception)
            {
                // schon kaputt: still weiter
            }
            Zeile = "Ton: kein Audiogerät (" + fehler.GetType().Name + ")";
            return new StilleAusgabe("kein Gerät");
        }
    }
}
