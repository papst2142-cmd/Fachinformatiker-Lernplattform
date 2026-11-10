namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 0.62.0 / S 0.37.0 (Weisung P12-174, Sammelliste 285, Nico 1a, Chat 2a): die EINE Tabelle, welches Modell
/// gezeichnet wird. Die sechs Autos des Car Kit haben eine gesparte Fassung „&lt;Name&gt;-12“, hinten an modelle.bin
/// angehaengt (Erzeuger <c>wandle_modelle.py</c>, Regel <c>sparen_raeder12</c>: Raeder mit 12 statt 24 Ecken, keine
/// Flaechen nach unten unter dem Wagenboden, keine Dreiecke ohne Flaeche); die alten Modelle bleiben in der Datei.
/// Strecken, Ton und Parkplatz rechnen weiter mit dem Namen des Autos (<c>car-taxi</c> usw.); nur wer das Modell zum
/// Zeichnen oder Zaehlen sucht, nimmt <see cref="Zeichnen"/> (fahrende Autos nah und fern, geparkte Autos).
/// Nur Darstellung: kein Spielstand, keine Ereignisart, Fingerabdruecke unveraendert.
/// </summary>
public static class Fahrzeugmodelle
{
    /// <summary>Zusatz am Namen der gesparten Fassung.</summary>
    public const string Zusatz = "-12";

    /// <summary>Autos mit gesparter Fassung (alle sechs Autos der Strecken; der Bus ist eigens gebaut und bleibt).</summary>
    public static readonly string[] Gespart =
        ["car-taxi", "car-van", "car-sedan", "car-police", "car-ambulance", "car-garbage-truck"];

    /// <summary>Name des Modells, das fuer ein Auto gezeichnet wird.</summary>
    public static string Zeichnen(string modell) =>
        !Alt.Value && Array.IndexOf(Gespart, modell) >= 0 ? modell + Zusatz : modell;

    /// <summary>Nur Gegenproben: die alten Modelle wie bis A 0.61.0 zeichnen (gilt im laufenden Ablauf, AsyncLocal).</summary>
    public static bool AltFuerTests { get => Alt.Value; set => Alt.Value = value; }

    private static readonly AsyncLocal<bool> Alt = new();
}
