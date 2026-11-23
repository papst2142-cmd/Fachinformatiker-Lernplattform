namespace Lernplattform.Core.Pakete;

/// <summary>
/// Welle A 1.5.0 (Weisung P12-358, Lauf 1193: „Inhalte laden AE“ ueber 400 ms): merkt sich Fachrichtungs-Pakete, die mit
/// genau dieser App schon ganz geprueft und gut waren (Aufbau, Ladepruefung der Saetze, „nur angehaengt“). Der Schluessel
/// nennt Fachrichtung, Stand und die gepruefte SHA-256 beider Inhaltsdateien; die Kennung der App
/// (<see cref="PaketAblage.PruefKennung"/>) nennt den Programmstand (Code und Inhalte der App). Das Ergebnis dieser Pruefungen
/// haengt nur davon ab; ein gemerktes Paket bekommt darum dasselbe Ergebnis ohne erneutes Rechnen. Kopf, Pruefsummen, Groesse
/// und Aufbau werden bei jedem Lesen geprueft wie bisher.
/// </summary>
public interface IPaketPruefMerker
{
    /// <summary>War das Paket mit diesem Schluessel mit dieser App schon ganz gut?</summary>
    bool Bekannt(string schluessel);

    /// <summary>Nach einer ganzen, guten Pruefung merken (nie ein Absturz).</summary>
    void Merken(string schluessel);
}
