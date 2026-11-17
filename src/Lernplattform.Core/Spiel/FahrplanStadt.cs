namespace Lernplattform.Core.Spiel;

/// <summary>
/// Eine Stadt im Fahrplan am Bahnhof (Welle A 0.88.0). Kennung null = Stadt 1. Liegt im Spiel-Kern, nicht im Namensraum der
/// Engine (der Fahrplan gehoert zur Spielanbindung, <c>EngineGrenzenTests</c>).
/// </summary>
/// <param name="Fahrbar">Paket geladen und in Ordnung: der Zug faehrt hin.</param>
/// <param name="Knopf">Zusaetzlicher Knopf (Aktion, Beschriftung), z. B. „Paket fehlt (0,51 MB) – Laden“; null = keiner.</param>
/// <param name="Hinweis">Zeile unter dem Fahrplan (z. B. Paket fehlerhaft); null = keine.</param>
public sealed record FahrplanStadt(string? Kennung, string Name, bool Fahrbar, (string Aktion, string Name)? Knopf = null, string? Hinweis = null);
