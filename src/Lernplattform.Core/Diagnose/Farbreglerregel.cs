using System.Globalization;

namespace Lernplattform.Core.Diagnose;

/// <summary>
/// Weisung P2-17 (Nico 21.10.2026): Zeitgrenzen fuer das Neuzeichnen des Farbreglers im Start
/// ohne Bedienung. Der erste Schritt nach dem Oeffnen von Eigene Farben traegt das erste echte
/// Zeichnen und hat 150 ms; ab dem zweiten Schritt gilt 100 ms. Gilt nur fuer diese Messung,
/// alle anderen Grenzen (400 ms Ansichtswechsel, Abbruch ab 1000 ms) bleiben unberuehrt.
/// </summary>
public static class Farbreglerregel
{
    /// <summary>Grenze fuer Schritt 1 nach dem Oeffnen.</summary>
    public const double GrenzeErsterSchritt = 150;

    /// <summary>Grenze fuer die Schritte 2 bis 360.</summary>
    public const double GrenzeWeitereSchritte = 100;

    /// <summary>Ein Fund je Schritt ueber seiner Grenze (Schritte ab 1 gezaehlt); leer = gruen.</summary>
    public static List<string> Funde(IReadOnlyList<double> zeiten)
    {
        var funde = new List<string>();
        for (var i = 0; i < zeiten.Count; i++)
        {
            var grenze = i == 0 ? GrenzeErsterSchritt : GrenzeWeitereSchritte;
            if (zeiten[i] > grenze)
            {
                funde.Add(string.Create(CultureInfo.InvariantCulture, $"Schritt {i + 1}: {zeiten[i]:F1} ms ueber {grenze:F0} ms"));
            }
        }
        return funde;
    }
}
