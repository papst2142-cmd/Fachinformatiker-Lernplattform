using Avalonia.Controls.Platform;
using Avalonia.Threading;
using Stadtbau.Core;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Patch S 0.5.1 (Weisung P12-37 Punkt 2, Nico: "Vollbildmodus"): am PC das Fenster ueber den ganzen Bildschirm
/// (<see cref="Fenster"/>, setzt StadtbauApp), am Tablet ohne Systemleisten. Unter Android geht der Wunsch wie in der
/// Lernplattform (Sammelliste 218) an Avalonias InsetsManager (<see cref="Leisten"/>): Avalonia setzt die Leisten bei
/// jedem Sichtbarwerden der Ansicht (Start, Rueckkehr aus dem Hintergrund) auf den Wert, den es sich selbst gemerkt hat.
/// <see cref="Geraet"/> (MainActivity) blendet sie zusaetzlich sofort aus und laesst sie beim Wischen vom Rand kurz sehen.
/// </summary>
public static class WerkzeugVollbild
{
    public static Action<bool>? Fenster { get; set; }

    /// <summary>S 0.25.1 (Sammelliste 252): Bildschirme des PCs (setzt StadtbauApp; am Tablet null).</summary>
    public static Func<IReadOnlyList<Lernplattform.Core.Spiel.Ton.Bildschirm>>? Bildschirme { get; set; }

    /// <summary>Nur Start-Test: diese Bildschirme statt der echten (ein Rechner der CI hat nur einen).</summary>
    public static IReadOnlyList<Lernplattform.Core.Spiel.Ton.Bildschirm>? BildschirmeFuerTests { get; set; }

    /// <summary>Bildschirme fuer die Wahl (Testliste vor den echten).</summary>
    public static IReadOnlyList<Lernplattform.Core.Spiel.Ton.Bildschirm> Liste() => BildschirmeFuerTests ?? Bildschirme?.Invoke() ?? [];

    /// <summary>Steht „Bildschirm für Vollbild“ im Menue? Nur am PC ab zwei Bildschirmen.</summary>
    public static bool WahlZeigen => VollbildZielWahl.Zeigen(Bildschirme is not null || BildschirmeFuerTests is not null, Liste().Count);

    public static Action<bool>? Geraet { get; set; }

    public static IInsetsManager? Leisten { get; set; }

    /// <summary>Nur Gegenprobe im Start-Test: false = Avalonia nichts mitteilen (wie die Lernplattform vor A 0.41.1).</summary>
    public static bool LeistenUeberAvalonia { get; set; } = true;

    public static bool An { get; private set; }

    public static event Action? Geaendert;

    /// <summary>Start-Test "Neustart": Fenster normal, Zustand vergessen, ohne die Datei zu aendern.</summary>
    public static void VergessenFuerTests()
    {
        Fenster?.Invoke(false);
        An = false;
    }

    public static void Setzen(bool an)
    {
        An = an;
        Anwenden();
        Geaendert?.Invoke();
    }

    /// <summary>Zustand neu an Fenster, Geraet und Avalonia geben (Start, Ansicht neu im Baum).</summary>
    public static void Anwenden()
    {
        Fenster?.Invoke(An);
        Geraet?.Invoke(An);
        LeistenMerken();
    }

    /// <summary>Start-Test: bekommt denselben Wunsch wie Avalonia (IInsetsManager laesst sich nicht nachbauen).</summary>
    public static Action<bool>? WunschFuerTests { get; set; }

    public static void LeistenMerken()
    {
        if (!LeistenUeberAvalonia)
        {
            return;
        }
        var sichtbar = !An;
        WunschFuerTests?.Invoke(sichtbar);
        if (Leisten is not { } rand)
        {
            return;
        }
        if (Dispatcher.UIThread.CheckAccess())
        {
            rand.IsSystemBarVisible = sichtbar;
        }
        else
        {
            Dispatcher.UIThread.Post(() => rand.IsSystemBarVisible = !An);
        }
    }
}
