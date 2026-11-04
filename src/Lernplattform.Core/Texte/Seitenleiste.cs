namespace Lernplattform.Core.Texte;

/// <summary>
/// Welle A 0.38.0 (Sammelliste 207, Nico 04.11.2026, Antworten 1a 2a 3a): auf- und zugeklappte Gruppen der
/// Seitenleiste (PC, Tablet quer). Aufbau aus <see cref="Navigation.PcLeiste"/>, "Lernen" mit
/// <see cref="Navigation.PcLernen"/>, sonst die Unterpunkte aus <see cref="Navigation.Pc"/> (Karteikarten mit den
/// Fachbereichen, Spiel mit den Orten).
/// Regeln: beim Start alles zu; Klick auf eine Gruppe klappt sie auf bzw. zu, dabei klappt eine andere Gruppe der
/// obersten Ebene zu (nur eine offen); innerhalb einer Gruppe klappt eine Untergruppe (Karteikarten) fuer sich.
/// Wechsel auf eine Seite (Suche, Dashboard-Kachel, Klick) oeffnet deren Gruppe; eine Seite ohne Gruppe (Start,
/// Rechner, Fortschritt, Optionen, Hilfe) klappt alles zu. Der Klick auf eine Gruppe entscheidet selbst, ob sie
/// offen ist; der folgende Seitenwechsel aendert daran nichts.
/// </summary>
public sealed class Seitenleiste
{
    private readonly Dictionary<string, string> _eltern = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<string>> _kinder = new(StringComparer.Ordinal);
    private readonly HashSet<string> _offen = new(StringComparer.Ordinal);
    private string? _seite;
    private string? _klick;

    public Seitenleiste()
    {
        var pc = Navigation.Pc.ToDictionary(p => p.Id, StringComparer.Ordinal);
        void Merken(string id, IReadOnlyList<string> kinder)
        {
            if (kinder.Count == 0)
            {
                return;
            }
            _kinder[id] = kinder;
            foreach (var kind in kinder)
            {
                _eltern[kind] = id;
                if (pc.TryGetValue(kind, out var eintrag))
                {
                    Merken(kind, [.. eintrag.Unterpunkte.Select(u => u.Id)]);
                }
            }
        }
        foreach (var id in Navigation.PcLeiste)
        {
            Merken(id, id == Navigation.LernenId ? Navigation.PcLernen
                : pc.TryGetValue(id, out var eintrag) ? [.. eintrag.Unterpunkte.Select(u => u.Id)] : []);
        }
    }

    /// <summary>Hat der Eintrag Unterpunkte (Lernen, Karteikarten, Spiel)?</summary>
    public bool IstGruppe(string id) => _kinder.ContainsKey(id);

    /// <summary>Unterpunkte einer Gruppe (leer bei Eintraegen ohne Unterpunkte).</summary>
    public IReadOnlyList<string> Kinder(string id) => _kinder.TryGetValue(id, out var kinder) ? kinder : [];

    /// <summary>Gruppe, in der ein Eintrag steht (null auf der obersten Ebene).</summary>
    public string? Eltern(string id) => _eltern.TryGetValue(id, out var eltern) ? eltern : null;

    /// <summary>Sind die Unterpunkte dieser Gruppe sichtbar (sie selbst und alle Gruppen darueber offen)?</summary>
    public bool Offen(string id)
    {
        if (!_offen.Contains(id))
        {
            return false;
        }
        return Eltern(id) is not { } eltern || Offen(eltern);
    }

    /// <summary>Alle offenen Gruppen, deren Unterpunkte sichtbar sind (sortiert, fuer Tests und Messwerte).</summary>
    public IReadOnlyList<string> OffeneGruppen => [.. _kinder.Keys.Where(Offen).Order(StringComparer.Ordinal)];

    /// <summary>Klick auf einen Eintrag: eine Gruppe klappt auf bzw. zu; danach folgt <see cref="Seite"/>.</summary>
    public void Klick(string id)
    {
        Umschalten(id);
        _klick = id;
    }

    /// <summary>Gruppe auf- bzw. zuklappen, ohne Seitenwechsel (auch Start-Test).</summary>
    public void Umschalten(string id)
    {
        if (!IstGruppe(id))
        {
            return;
        }
        if (Offen(id))
        {
            Schliessen(id);
        }
        else
        {
            Oeffnen(id);
        }
    }

    /// <summary>Die Seite <paramref name="id"/> wird gezeigt; nur ein echter Wechsel aendert die Gruppen.</summary>
    public void Seite(string id)
    {
        var klick = _klick;
        _klick = null;
        if (id == _seite)
        {
            return;
        }
        _seite = id;
        if (klick == id && IstGruppe(id))
        {
            return;
        }
        if (Eltern(id) is { } eltern)
        {
            Oeffnen(eltern);
        }
        else if (IstGruppe(id))
        {
            Oeffnen(id);
        }
        else
        {
            _offen.Clear();
        }
    }

    /// <summary>Klappt eine Gruppe mit allen Untergruppen zu (beim naechsten Oeffnen ist auch Karteikarten wieder zu).</summary>
    private void Schliessen(string id)
    {
        _offen.Remove(id);
        foreach (var kind in Kinder(id))
        {
            Schliessen(kind);
        }
    }

    /// <summary>Oeffnet eine Gruppe samt allen Gruppen darueber; jede andere Gruppe der obersten Ebene klappt zu.</summary>
    private void Oeffnen(string id)
    {
        var kette = new List<string> { id };
        while (Eltern(kette[^1]) is { } eltern)
        {
            kette.Add(eltern);
        }
        var oben = kette[^1];
        foreach (var andere in _offen.Where(o => Eltern(o) is null && o != oben).ToList())
        {
            Schliessen(andere);
        }
        foreach (var gruppe in kette)
        {
            _offen.Add(gruppe);
        }
    }
}
