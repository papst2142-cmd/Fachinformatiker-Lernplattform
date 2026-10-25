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
