namespace Lernplattform.Spiel;

/// <summary>
/// Phase 7, W4 (Weisung P7-9 Punkt 2, Wunsch Nico): das Spiel laeuft im Vollbild, am Handy im
/// Querformat. Die Seite selbst blendet Kopfleiste und Navigation aus (MainView); was nur das Geraet
/// kann (Android: Querformat und Systemleisten ausblenden), haengt die Plattform hier ein.
/// true = Spiel betreten, false = Spiel verlassen (zurueck zur Einstellung des Geraets, also
/// Hochformat, wenn das Handy hochgehalten wird oder das automatische Drehen aus ist).
/// </summary>
public static class Vollbild
{
    /// <summary>Plattform-Teil (Android: MainActivity); am PC null.</summary>
    public static Action<bool>? Geraet { get; set; }

    /// <summary>Zuletzt gesetzter Zustand (Start-Test und Rueckweg).</summary>
    public static bool An { get; private set; }

    public static void Setzen(bool an)
    {
        if (An == an)
        {
            return;
        }
        An = an;
        Geraet?.Invoke(an);
    }
}
