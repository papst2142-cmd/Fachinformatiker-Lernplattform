using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Ein Modell aus den CC0-Paketen: Dreiecke (je drei Ecken) mit einer Farbe je Dreieck und (seit A 0.43.6, Weisung
/// P12-50) einer Farbe je Ecke. Die Farbe des Dreiecks (Pixel an der Mitte der UV-Koordinaten) bleibt fuer alles, was
/// Flaechen erkennt (Glas, Tueren, Licht, Pflanzen, Haut); gezeichnet wird mit den Eckfarben (Farbverlauf der Pakete).
/// </summary>
public sealed class GrafikModell
{
    public GrafikModell(string name, float[] ecken, byte[] farben, byte[]? eckFarben = null)
    {
        if (eckFarben is not null && eckFarben.Length != farben.Length * 3)
        {
            throw new InvalidDataException("Grafik " + name + ": Eckfarben passen nicht zu den Dreiecken");
        }
        Name = name;
        Ecken = ecken;
        Farben = farben;
        EckFarben = eckFarben;
        Min = Grenze(ecken, MathF.Min, float.MaxValue);
        Max = Grenze(ecken, MathF.Max, float.MinValue);
    }

    public string Name { get; }

    /// <summary>Je Dreieck 9 Zahlen (drei Ecken x, y, z), Koordinaten wie im Paket (Y oben, Vorderseite +Z).</summary>
    public float[] Ecken { get; }

    /// <summary>Je Dreieck R, G, B (Farbe der Flaeche, zum Erkennen).</summary>
    public byte[] Farben { get; }

    /// <summary>
    /// A 0.43.6 (Weisung P12-50): je Dreieck 9 Bytes (Ecke 1 bis 3 je R, G, B), Farbe des Pakets an der UV-Stelle der Ecke;
    /// null = jede Ecke in der Farbe des Dreiecks (eigene Formen, Figur).
    /// </summary>
    public byte[]? EckFarben { get; }

    public int Dreiecke => Farben.Length / 3;

    /// <summary>Farbe des Dreiecks <paramref name="d"/> (0..1).</summary>
    public Vector3 Farbe(int d) => new Vector3(Farben[d * 3], Farben[(d * 3) + 1], Farben[(d * 3) + 2]) / 255f;

    /// <summary>Farbe der Ecke <paramref name="k"/> (0..2) des Dreiecks <paramref name="d"/> (0..1).</summary>
    public Vector3 EckFarbe(int d, int k)
    {
        if (EckFarben is not { } e)
        {
            return Farbe(d);
        }
        var i = (d * 9) + (k * 3);
        return new Vector3(e[i], e[i + 1], e[i + 2]) / 255f;
    }

    /// <summary>
    /// Dasselbe Modell mit neuen Farben je Dreieck (Tuer zugemalt, Lack): ein Dreieck mit neuer Farbe ist an allen Ecken
    /// einfarbig, alle anderen behalten ihren Farbverlauf.
    /// </summary>
    public GrafikModell MitFarben(byte[] farben)
    {
        if (EckFarben is not { } alt)
        {
            return new GrafikModell(Name, Ecken, farben);
        }
        var neu = (byte[])alt.Clone();
        for (var d = 0; d < Dreiecke; d++)
        {
            if (farben[d * 3] == Farben[d * 3] && farben[(d * 3) + 1] == Farben[(d * 3) + 1] && farben[(d * 3) + 2] == Farben[(d * 3) + 2])
            {
                continue;
            }
            for (var k = 0; k < 3; k++)
            {
                Array.Copy(farben, d * 3, neu, (d * 9) + (k * 3), 3);
            }
        }
        return new GrafikModell(Name, Ecken, farben, neu);
    }

    public Vector3 Min { get; }

    public Vector3 Max { get; }

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
    /// S 0.9.0 (Weisung P12-48): wie <see cref="Mit(IEnumerable{GrafikModell})"/> und dazu weitere Zuordnungen
    /// "modell:teilnr" -&gt; Grafik (selbst gebaute Gebaeude, <c>Stadtbau.Bauteile.MitGebaeuden</c>). Eine Zuordnung, die es
    /// schon gibt oder deren Grafik fehlt, ist ein Fehler.
    /// </summary>
    public GrafikBibliothek Mit(IEnumerable<GrafikModell> zusatz, IReadOnlyDictionary<string, string> bloecke)
    {
        var mit = Mit(zusatz);
        var alle = new Dictionary<string, string>(Bloecke);
        foreach (var (k, v) in bloecke)
        {
            if (mit.Modell(v) is null || !alle.TryAdd(k, v))
            {
                throw new InvalidDataException("Zuordnung " + k + " gibt es schon oder Grafik " + v + " fehlt");
            }
        }
        return new GrafikBibliothek(mit._modelle, alle);
    }

    /// <summary>
    /// Weisung P12-50: dieselbe Bibliothek mit einer Farbe je Dreieck an jeder Ecke (so zeichnete das Spiel bis A 0.43.5).
    /// Nur fuer die Gegenproben der Tests: Glas, Licht, Schnee und Fest-Deko muessen mit und ohne Farbverlauf gleich sein.
    /// </summary>
    public GrafikBibliothek JeDreieck() =>
        new(_modelle.ToDictionary(p => p.Key, p => new GrafikModell(p.Value.Name, p.Value.Ecken, p.Value.Farben), StringComparer.Ordinal),
            new Dictionary<string, string>(Bloecke));

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

    private static Dictionary<string, GrafikModell> Lesen(byte[] bin) => Lesen(bin, "modelle.bin");

    /// <summary>Aufbau der Datei (Version 2 mit Eckfarben seit A 0.43.6), Erzeuger <c>werkzeuge/grafik/wandle_modelle.py</c>.</summary>
    public const uint Version = 2;

    /// <summary>
    /// Liest eine FLGM-Datei (modelle.bin, katalog_verbaut.bin, Pakete des Katalogs). Version 2: je Dreieck 9 x float32,
    /// 4 Byte Farbe (R, G, B, 0) und 12 Byte Eckfarben (je Ecke R, G, B, 0). Version 1 (ohne Eckfarben, bis A 0.43.5) bleibt
    /// lesbar, dann ist jede Ecke in der Farbe des Dreiecks. Jede andere Kennung oder Version: Fehler mit <paramref name="wer"/>.
    /// </summary>
    public static Dictionary<string, GrafikModell> Lesen(byte[] bin, string wer)
    {
        try
        {
            return LesenOhneEnde(bin, wer);
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException(wer + ": Datei endet zu frueh");
        }
    }

    private static Dictionary<string, GrafikModell> LesenOhneEnde(byte[] bin, string wer)
    {
        using var leser = new BinaryReader(new MemoryStream(bin));
        var version = bin.Length >= 8 && Encoding.ASCII.GetString(leser.ReadBytes(4)) == "FLGM" ? leser.ReadUInt32() : 0;
        if (version is not (1 or Version))
        {
            throw new InvalidDataException(wer + ": unbekannter Aufbau");
        }
        var anzahl = leser.ReadUInt32();
        var modelle = new Dictionary<string, GrafikModell>(StringComparer.Ordinal);
        for (var m = 0; m < anzahl; m++)
        {
            var name = Encoding.UTF8.GetString(leser.ReadBytes(leser.ReadUInt16()));
            var dreiecke = (int)leser.ReadUInt32();
            var ecken = new float[dreiecke * 9];
            var farben = new byte[dreiecke * 3];
            var eck = version == 1 ? null : new byte[dreiecke * 9];
            for (var d = 0; d < dreiecke; d++)
            {
                for (var i = 0; i < 9; i++)
                {
                    ecken[(d * 9) + i] = leser.ReadSingle();
                }
                var rgba = leser.ReadBytes(4);
                if (rgba.Length < 4)
                {
                    throw new EndOfStreamException();
                }
                farben[d * 3] = rgba[0];
                farben[(d * 3) + 1] = rgba[1];
                farben[(d * 3) + 2] = rgba[2];
                if (eck is not null)
                {
                    var e = leser.ReadBytes(12);
                    if (e.Length < 12)
                    {
                        throw new EndOfStreamException();
                    }
                    for (var k = 0; k < 3; k++)
                    {
                        eck[(d * 9) + (k * 3)] = e[k * 4];
                        eck[(d * 9) + (k * 3) + 1] = e[(k * 4) + 1];
                        eck[(d * 9) + (k * 3) + 2] = e[(k * 4) + 2];
                    }
                }
            }
            if (!modelle.TryAdd(name, new GrafikModell(name, ecken, farben, eck)))
            {
                throw new InvalidDataException(wer + ": Grafik " + name + " doppelt");
            }
        }
        if (leser.BaseStream.Position != bin.Length)
        {
            throw new InvalidDataException(wer + ": Daten nach dem letzten Modell");
        }
        return modelle;
    }
}
