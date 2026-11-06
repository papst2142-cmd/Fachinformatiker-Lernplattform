using Lernplattform.Core.Daten;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>Spieldaten aus den Dateien des Repos (gleiche Pfade wie die eingebetteten Dateien der Lernplattform).</summary>
internal static class Daten
{
    public static string Wurzel
    {
        get
        {
            var ordner = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(ordner.FullName, "Directory.Build.props")))
            {
                ordner = ordner.Parent!;
            }
            return ordner.FullName;
        }
    }

    public static byte[]? Lesen(string pfad)
    {
        var datei = Path.Combine(Wurzel, pfad);
        return File.Exists(datei) ? File.ReadAllBytes(datei) : null;
    }

    private static readonly Lazy<Spieldaten> Geladen = new(() => Spieldaten.Laden(Lesen));

    /// <summary>Einmal geladen (nur lesen; Projekte arbeiten auf Kopien der Grundrisse).</summary>
    public static Spieldaten Spiel => Geladen.Value;

    /// <summary>Frischer leerer Datenordner unter dem Temp-Ordner.</summary>
    public static Werkzeugordner NeueAblage()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "stadtbau_test_" + Guid.NewGuid().ToString("N"));
        return new Werkzeugordner(ordner).Anlegen();
    }

    /// <summary>
    /// Weisung P12-30: Laterne 20 an die Stelle, an die sie im spaeten Spielstand rueckt (30,3; 15,16), wie Nico es im
    /// Werkzeug tut; danach steht sie in jedem Spielstand frei. <paramref name="aussen"/> = Zustand draussen.
    /// </summary>
    public static void Laterne20Frei(PyObjekt aussen) => ((List<object?>)Aussen.Laternen(aussen)[19]!)[1] = 15.16;

    public static PyObjekt Kopie(PyObjekt o) => (PyObjekt)Lernplattform.Core.Spiel.Py.Kopie(o)!;
}
