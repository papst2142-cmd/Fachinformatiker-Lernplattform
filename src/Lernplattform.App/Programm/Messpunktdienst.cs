using Lernplattform.Core.Diagnose;
using Update = Lernplattform.Aktualisierung.Aktualisierung;

namespace Lernplattform.Programm;

/// <summary>
/// Phase 11 W3 (Weisung P11-21): schreibt die Messpunkte des Programms (<see cref="Messpunkte"/>) im Hintergrund in
/// den Datenordner; "Problem melden" zeigt sie mit. Nie auf dem Oberflaechen-Thread, Fehler werden geschluckt.
/// </summary>
public static class Messpunktdienst
{
    public static void Notieren(Messpunkte.Punkt punkt, double ms, string? fachrichtung = null)
    {
        var jetzt = DateTime.Now;
        _ = Task.Run(() =>
        {
            try
            {
                Messpunkte.Notieren(Core.Daten.Ablage.Datenordner(), jetzt, Update.Anzeige, punkt, ms, fachrichtung);
            }
            catch (Exception)
            {
                // Messpunkte sind nur Diagnose: nie das Programm stoeren
            }
        });
    }
}
