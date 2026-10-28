namespace Lernplattform.Core.Dashboard;

/// <summary>
/// Phase 8 W10 (Sammelliste 151, Nico 4a/5a): Anordnung des Dashboards nach der Breite, nicht nach dem Geraet (PC =
/// Handy). Breit wie Python 0.63.2 am PC (<c>app_gui.py</c> <c>DashboardView._fit_row1</c>): vier Ringkarten und
/// Erfolgsquote in einer Reihe, Lernverlauf (3 Teile) neben Abdeckung (2 Teile), Fachbereiche fuenf in einer Reihe,
/// Aktivitaeten neben Lerntagen, AP1- neben AP2-Themen (je fuenf Ringe in einer Reihe, Name darunter). Sonst wie
/// bisher. Ab 160 % Schrift (<c>Schrift.Gross</c>) immer wie bisher.
/// </summary>
public static class DashboardAnordnung
{
    /// <summary>Ab dieser Inhaltsbreite (Punkte bei normaler Schrift) breit: etwa 5 x 170 + 4 x 12 = 898 (schmalste Ringkarte mit lesbarem Titel), gerundet.</summary>
    public const double BreitAb = 900;

    /// <summary>Breite Anordnung bei dieser Inhaltsbreite (ohne Seitenrand), diesem Schriftfaktor und Stufe "gross"?</summary>
    public static bool Breit(double inhaltBreite, double schriftFaktor, bool gross) =>
        !gross && inhaltBreite >= BreitAb * schriftFaktor;
}
