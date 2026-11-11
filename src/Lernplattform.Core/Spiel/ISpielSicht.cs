using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Weisung P12-179 Schritt 2: was die eigene Engine vom Spielstand lesen darf (nur lesen). Der Spielstand liefert die Sicht
/// selbst (<c>Spielstand.Sicht.cs</c>, jedes Feld unveraendert weitergereicht); die Engine kennt <see cref="Spielstand"/>
/// nicht mehr. Gehoert zu den Grundlagen (nutzt nur Python-Werte).
/// </summary>
public interface ISpielSicht
{
    /// <summary>Spielinhalt (content).</summary>
    PyObjekt Inhalt { get; }

    /// <summary>Heutiger Spieltag (days_done + 1).</summary>
    long Tag { get; }

    /// <summary>Fertige Tage (days_done).</summary>
    long TageFertig { get; }

    /// <summary>Profil (profile), null vor dem Anlegen.</summary>
    PyObjekt? Profil { get; }

    /// <summary>Geld (money).</summary>
    long Geld { get; }

    /// <summary>Eigene Firma (firm), sonst null.</summary>
    object? Firma { get; }

    /// <summary>Zweigstelle (branch), sonst null.</summary>
    object? Zweigstelle { get; }

    /// <summary>Gemietete Raeume (rooms).</summary>
    PyDict Raeume { get; }

    /// <summary>Kennung der Wohnung (home_id).</summary>
    object? Wohnung { get; }

    /// <summary>Geloeste Aufgaben (solved).</summary>
    PySet Geloest { get; }

    /// <summary>Serverfarm frei (farm_unlocked).</summary>
    object? FarmFrei { get; }

    /// <summary>Serverfarm (farm).</summary>
    object? Farm { get; }

    /// <summary>Offene Tickets je Auftraggeber (quests), mit Ort nur die an diesem Ort.</summary>
    PyDict Auftraege(object? ort = null);
}
