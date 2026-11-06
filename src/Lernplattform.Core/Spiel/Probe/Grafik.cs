using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Ein Modell aus den CC0-Paketen: Dreiecke (je drei Ecken) mit einer Farbe je Dreieck.</summary>
public sealed class GrafikModell(string name, float[] ecken, byte[] farben)
{
    public string Name { get; } = name;

    /// <summary>Je Dreieck 9 Zahlen (drei Ecken x, y, z), Koordinaten wie im Paket (Y oben, Vorderseite +Z).</summary>
    public float[] Ecken { get; } = ecken;

    /// <summary>Je Dreieck R, G, B.</summary>
    public byte[] Farben { get; } = farben;

    public int Dreiecke => Farben.Length / 3;

    public Vector3 Min { get; } = Grenze(ecken, MathF.Min, float.MaxValue);

    public Vector3 Max { get; } = Grenze(ecken, MathF.Max, float.MinValue);

    private static Vector3 Grenze(float[] e, Func<float, float, float> f, float start)
    {
        float x = start, y = start, z = start;
        for (var i = 0; i + 2 < e.Length; i += 3)
        {
            x = f(x, e[i]);
            y = f(y, e[i + 1]);
            z = f(z, e[i + 2]);
        }
        return new Vector3(x, y, z);
    }
}

/// <summary>Ein platziertes Modell der Szene (Lage = Welt-Matrix, System.Numerics: Zeilenvektoren).</summary>
public readonly record struct ModellStueck(string Name, Matrix4x4 Lage);

/// <summary>
/// Phase 7 W4 (Weisung P7-9 Nachtrag 3/4): die gewandelten CC0-Modelle aus
/// <c>inhalte_csharp/grafik/modelle.bin</c> (Erzeuger <c>werkzeuge/grafik/wandle_modelle.py</c>, Aufbau dort)
/// und die Zuordnung Modellteil -> Grafik aus <c>zuordnung.json</c>. Die Pruefsumme aus
/// <c>modelle.json</c> muss stimmen, sonst wird nichts geladen (die Stadt zeichnet dann eigene Geometrie).
/// </summary>
public sealed class GrafikBibliothek
{
    private readonly Dictionary<string, GrafikModell> _modelle;

    private GrafikBibliothek(Dictionary<string, GrafikModell> modelle, Dictionary<string, string> bloecke)
    {
        _modelle = modelle;
        Bloecke = bloecke;
    }

    public IReadOnlyCollection<GrafikModell> Modelle => _modelle.Values;

    /// <summary>"modell:teilnr" (Teilnummer wie in orte.json, ab 0) -> Name der Grafik fuer diesen Block.</summary>
    public IReadOnlyDictionary<string, string> Bloecke { get; }

    public GrafikModell? Modell(string name) => _modelle.GetValueOrDefault(name);

    /// <summary>Liest modelle.bin, prueft die SHA-256 aus modelle.json und liest die Zuordnung.</summary>
    public static GrafikBibliothek Laden(byte[] bin, string indexJson, string zuordnungJson)
    {
        using var index = JsonDocument.Parse(indexJson);
        var soll = index.RootElement.GetProperty("sha256").GetString();
        var ist = Convert.ToHexStringLower(SHA256.HashData(bin));
        if (ist != soll)
        {
            throw new InvalidDataException("modelle.bin: Pruefsumme " + ist + " statt " + soll);
        }
        var modelle = Lesen(bin);
        using var zuordnung = JsonDocument.Parse(zuordnungJson);
        var bloecke = new Dictionary<string, string>();
        foreach (var e in zuordnung.RootElement.GetProperty("bloecke").EnumerateObject())
        {
            var name = e.Value.GetString()!;
            if (!modelle.ContainsKey(name))
            {
                throw new InvalidDataException("zuordnung.json: Grafik " + name + " fehlt in modelle.bin");
            }
            bloecke[e.Name] = name;
        }
        return new GrafikBibliothek(modelle, bloecke);
    }

    /// <summary>Phase 13 Welle 4a: eine Bibliothek nur aus diesen Modellen (ohne Zuordnung), z. B. fuer die Ladepruefung.</summary>
    public static GrafikBibliothek Aus(IEnumerable<GrafikModell> modelle) =>
        new(modelle.ToDictionary(m => m.Name, StringComparer.Ordinal), []);

    /// <summary>
    /// Phase 13 Welle 4a (Weisung P12-28): diese Bibliothek mit weiteren Modellen (Stuecke aus dem Katalog des Stadtbau-
    /// Werkzeugs, Name "cc0:paket/name"); ein Name, den es schon gibt, ist ein Fehler. Die Zuordnung bleibt.
    /// </summary>
    public GrafikBibliothek Mit(IEnumerable<GrafikModell> zusatz)
    {
        var alle = new Dictionary<string, GrafikModell>(_modelle);
        foreach (var m in zusatz)
        {
            if (!alle.TryAdd(m.Name, m))
            {
                throw new InvalidDataException("Grafik " + m.Name + " gibt es schon");
            }
        }
        return new GrafikBibliothek(alle, new Dictionary<string, string>(Bloecke));
    }

    /// <summary>
    /// Phase 13 Welle 4a: die mitgelieferten Stuecke aus dem Katalog (<c>inhalte_csharp/grafik/katalog_verbaut.bin</c>, Aufbau
    /// wie modelle.bin, Erzeuger <c>werkzeuge/stadtbau/verbaut.py</c>) mit der SHA-256 aus <c>katalog_verbaut.json</c>; jeder
    /// Name beginnt mit "cc0:" und steht genau so in der Liste "modelle" der json-Datei.
    /// </summary>
    public static List<GrafikModell> KatalogLaden(byte[] bin, string indexJson)
    {
        using var index = JsonDocument.Parse(indexJson);
        var soll = index.RootElement.GetProperty("sha256").GetString();
        var ist = Convert.ToHexStringLower(SHA256.HashData(bin));
        if (ist != soll)
        {
            throw new InvalidDataException("katalog_verbaut.bin: Pruefsumme " + ist + " statt " + soll);
        }
        var modelle = Lesen(bin);
        var namen = index.RootElement.GetProperty("modelle").EnumerateArray().Select(e => e.GetString()!).ToList();
        if (!namen.SequenceEqual(modelle.Keys) || namen.Any(n => !Katalogstueck.IstTyp(n)))
        {
            throw new InvalidDataException("katalog_verbaut.bin: Liste der Modelle passt nicht zu katalog_verbaut.json");
        }
        return [.. modelle.Values];
    }

    private static Dictionary<string, GrafikModell> Lesen(byte[] bin)
    {
        using var leser = new BinaryReader(new MemoryStream(bin));
        if (Encoding.ASCII.GetString(leser.ReadBytes(4)) != "FLGM" || leser.ReadUInt32() != 1)
        {
            throw new InvalidDataException("modelle.bin: unbekannter Aufbau");
        }
        var anzahl = leser.ReadUInt32();
        var modelle = new Dictionary<string, GrafikModell>();
        for (var m = 0; m < anzahl; m++)
        {
            var name = Encoding.UTF8.GetString(leser.ReadBytes(leser.ReadUInt16()));
            var dreiecke = (int)leser.ReadUInt32();
            var ecken = new float[dreiecke * 9];
            var farben = new byte[dreiecke * 3];
            for (var d = 0; d < dreiecke; d++)
            {
                for (var i = 0; i < 9; i++)
                {
                    ecken[(d * 9) + i] = leser.ReadSingle();
                }
                var rgba = leser.ReadBytes(4);
                farben[d * 3] = rgba[0];
                farben[(d * 3) + 1] = rgba[1];
                farben[(d * 3) + 2] = rgba[2];
            }
            modelle[name] = new GrafikModell(name, ecken, farben);
        }
        return modelle;
    }
}
