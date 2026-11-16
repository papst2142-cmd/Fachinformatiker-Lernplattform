namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle Nachladen A 0.82.0 (Weisung P12-280, Nico 4a: „Stadt bauen“ fuer grosse Karten): ein Raster fuer Abfragen der Art
/// „gilt eine Bedingung fuer ALLE Eintraege einer Liste?“. Jeder Eintrag steht in den Zellen seines Wirkkastens: ausserhalb
/// dieses Kastens ist seine Bedingung sicher erfuellt (der Aufrufer waehlt den Kasten so, mit Zugabe). <see cref="Alle"/>
/// prueft darum nur die Eintraege der Zelle des Punkts und ergibt genau dasselbe wie <c>liste.All(...)</c> ueber die ganze
/// Liste (die Bedingung hat keine Nebenwirkung, die Reihenfolge zaehlt nicht). Reine Beschleunigung, kein anderes Ergebnis.
/// </summary>
public sealed class NahRaster<T>
{
    /// <summary>Zugabe auf jeden Wirkkasten gegen Rundung (die Bedingungen vergleichen mit Abstaenden in float).</summary>
    public const float Zugabe = 0.05f;

    private const float Zelle = 2f;
    private readonly Dictionary<(int, int), List<T>> _zellen = [];

    /// <summary>Anzahl der Eintraege.</summary>
    public int Anzahl { get; private set; }

    /// <summary>Einen Eintrag mit seinem Wirkkasten (ausserhalb ist seine Bedingung sicher erfuellt) aufnehmen.</summary>
    public void Hinzu(T eintrag, float minX, float minZ, float maxX, float maxZ)
    {
        Anzahl++;
        var (x0, z0) = ZelleVon(minX - Zugabe, minZ - Zugabe);
        var (x1, z1) = ZelleVon(maxX + Zugabe, maxZ + Zugabe);
        for (var i = x0; i <= x1; i++)
        {
            for (var j = z0; j <= z1; j++)
            {
                if (!_zellen.TryGetValue((i, j), out var l))
                {
                    _zellen[(i, j)] = l = [];
                }
                l.Add(eintrag);
            }
        }
    }

    /// <summary>Gilt <paramref name="bedingung"/> fuer alle Eintraege, deren Wirkkasten den Punkt enthalten koennte?</summary>
    public bool Alle(float x, float z, Func<T, bool> bedingung)
    {
        if (!_zellen.TryGetValue(ZelleVon(x, z), out var l))
        {
            return true;
        }
        foreach (var e in l)
        {
            if (!bedingung(e))
            {
                return false;
            }
        }
        return true;
    }

    private static (int, int) ZelleVon(float x, float z) => ((int)MathF.Floor(x / Zelle), (int)MathF.Floor(z / Zelle));
}
