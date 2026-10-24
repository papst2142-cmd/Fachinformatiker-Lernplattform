namespace Lernplattform.Core.Dashboard;

/// <summary>
/// A 0.12.2 (Weisung P6-16, Frage 3 A): Blaettern im Kalender "Lerntage" wie CalendarPanel am
/// Handy (_shift, Pfeile mit Tooltip). Nur lesend: die Lerntage des Monats kommen aus
/// <see cref="DashboardWerte.Monat"/>.
/// </summary>
public static class KalenderBlaettern
{
    public const string Zurueck = "Vorheriger Monat";
    public const string Weiter = "Nächster Monat";

    /// <summary>Monat um <paramref name="schritt"/> verschoben (ueber Jahresgrenzen, wie _shift).</summary>
    public static (int Jahr, int Monat) Verschieben(int jahr, int monat, int schritt)
    {
        var m = monat + schritt;
        return (jahr + (int)Math.Floor((m - 1) / 12.0), ((m - 1) % 12 + 12) % 12 + 1);
    }
}
