namespace Lernplattform.Spiel;

/// <summary>
/// Phase 7, W4 (Weisung P7-9 Punkt 2, Wunsch Nico): das Spiel laeuft im Vollbild, am Handy im
/// Querformat. Die Seite selbst blendet Kopfleiste und Navigation aus (MainView); was nur das Geraet
/// kann (Android: Querformat und Systemleisten ausblenden), haengt die Plattform hier ein.
/// true = Spiel betreten, false = Spiel verlassen (zurueck zur Einstellung des Geraets, also
/// Hochformat, wenn das Handy hochgehalten wird oder das automatische Drehen aus ist).
/// Phase 8 W8 (Sammelliste 131, Nico 3a): Schalter "Vollbild" in den Optionen (<see cref="Dauerhaft"/>):
/// am PC das ganze Fenster (<see cref="Fenster"/>), am Handy die Systemleisten auch ausserhalb des Spiels.
/// </summary>
public static class Vollbild
{
    /// <summary>Plattform-Teil (Android: MainActivity): (im Spiel, Leisten ausblenden); am PC null.</summary>
    public static Action<bool, bool>? Geraet { get; set; }

    /// <summary>PC: Fenster in den Vollbildmodus (MainWindow); am Handy null.</summary>
    public static Action<bool>? Fenster { get; set; }

    /// <summary>
    /// Patch A 0.37.0 (Sammelliste 205): Bildschirme des PCs (MainWindow, Reihenfolge von Avalonia); am Handy null.
    /// Der Start-Test setzt <see cref="BildschirmeFuerTests"/>, um zwei Bildschirme vorzutaeuschen.
    /// </summary>
    public static Func<IReadOnlyList<Core.Spiel.Ton.Bildschirm>>? Bildschirme { get; set; }

    public static IReadOnlyList<Core.Spiel.Ton.Bildschirm>? BildschirmeFuerTests { get; set; }

    /// <summary>Bildschirme fuer die Auswahl (leer am Handy oder ohne Fenster).</summary>
    public static IReadOnlyList<Core.Spiel.Ton.Bildschirm> AlleBildschirme()
    {
        if (BildschirmeFuerTests is { } test)
        {
            return test;
        }
        try
        {
            return Bildschirme?.Invoke() ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>Die Wahl "Bildschirm für Vollbild" hat gewechselt: laeuft das Vollbild, auf den neuen Bildschirm setzen.</summary>
    public static void BildschirmNeu()
    {
        if (Dauerhaft)
        {
            Fenster?.Invoke(true);
        }
        Geaendert?.Invoke();
    }

    /// <summary>Zuletzt gesetzter Zustand des Spiels (Start-Test und Rueckweg).</summary>
    public static bool An { get; private set; }

    /// <summary>Schalter "Vollbild" der Optionen (auch F11).</summary>
    public static bool Dauerhaft { get; private set; }

    public static void Setzen(bool an)
    {
        if (An == an)
        {
            return;
        }
        An = an;
        Geraet?.Invoke(an, an || Dauerhaft);
    }

    public static void DauerhaftSetzen(bool an)
    {
        if (Dauerhaft == an)
        {
            return;
        }
        Dauerhaft = an;
        Fenster?.Invoke(an);
        Geraet?.Invoke(An, An || an);
        Geaendert?.Invoke();
    }

    /// <summary>Plattform-Teil neu anwenden (Android nach OnCreate, wenn der Schalter schon an ist).</summary>
    public static void GeraetAnwenden()
    {
        if (An || Dauerhaft)
        {
            Geraet?.Invoke(An, true);
        }
    }

    /// <summary>Schalter oder Taste haben gewechselt (Karte "Vollbild" frischt sich auf).</summary>
    public static event Action? Geaendert;

    /// <summary>Die Karte "Tastenbelegung" wartet auf eine Taste: F11 schaltet dann nicht um.</summary>
    public static bool TasteWartet { get; set; }

    /// <summary>Taste F11 (oder eigene Taste): umschalten und speichern wie der Schalter.</summary>
    public static void Umschalten()
    {
        var neu = !Dauerhaft;
        if (Programm.Zustand.Aendere(w => Core.Spiel.Ton.VollbildWahl.Wert(w, neu)))
        {
            DauerhaftSetzen(neu);
        }
    }
}
