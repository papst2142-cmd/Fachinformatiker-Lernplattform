using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Inhalte;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.18.0 (L0, Weisung P12-424 Nico 1b): Stand von inhalte_csharp/lernen VOR dem Loeschen, bytegleich wieder
/// zusammengesetzt aus der heutigen Datei und den geloeschten Textstuecken (Testdaten/l0_vorher.json, erzeugt von
/// <c>werkzeuge/inhalte/loeschliste.py vorher</c>). Die Tests der Wellen davor („nur angehaengt“, alte und neue Fassung,
/// Gegenproben ohne Ausschluss) rechnen weiter auf diesem Stand; <see cref="L0LoeschlisteTests"/> prueft, dass die heutige
/// Datei genau der alte Stand ohne die Eintraege der Loeschliste ist.
/// </summary>
internal static class L0Vorher
{
    private static readonly string Lernen = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen");

    private static readonly Lazy<JsonNode> Daten = new(() =>
        JsonNode.Parse(File.ReadAllBytes(Path.Combine(Pfade.Wurzel, "tests", "Lernplattform.Tests", "Testdaten", "l0_vorher.json")))!);

    /// <summary>Die drei Dateien, aus denen L0 geloescht hat.</summary>
    public static IEnumerable<string> Dateien => Daten.Value["dateien"]!.AsObject().Select(p => p.Key);

    /// <summary>Datei (ohne .json) wie vor L0, bytegleich (SHA-256 geprueft); andere Dateien wie heute.</summary>
    public static byte[] Datei(string name)
    {
        // A 1.20.0: pruefungssaetze.json traegt seit Format 6 neue Fassungen; der Stand nach L0 liegt bytegleich eingefroren
        // in tests/Lerninhalte/echt/pruefungssaetze_format5.json (EchtTestDatei.NachL0, Test EchtSchreibtischTests)
        var heute = name == "pruefungssaetze" ? EchtTestDatei.NachL0 : File.ReadAllBytes(Path.Combine(Lernen, name + ".json"));
        if (Daten.Value["dateien"]![name] is not JsonObject teil)
        {
            return heute;
        }
        var nachher = (string)teil["sha256_nachher"]!;
        if (Sha(heute) != nachher)
        {
            throw new InvalidOperationException(name + ".json: heutige Datei ist nicht der Stand nach L0 (neue Eintraege seitdem?)");
        }
        var puffer = new List<byte>(heute);
        foreach (var stueck in teil["stuecke"]!.AsArray())
        {
            puffer.InsertRange((int)stueck!["byte"]!, Encoding.UTF8.GetBytes((string)stueck["text"]!));
        }
        var vorher = puffer.ToArray();
        if (Sha(vorher) != (string)teil["sha256_vorher"]!)
        {
            throw new InvalidOperationException(name + ".json: Stand vor L0 nicht bytegleich");
        }
        return vorher;
    }

    /// <summary>Die Textstuecke einer Datei (Byte-Stelle in der alten Datei, Text), wie geloescht.</summary>
    public static IReadOnlyList<(int Byte, string Text)> Stuecke(string name) =>
        [.. Daten.Value["dateien"]![name]!["stuecke"]!.AsArray().Select(s => ((int)s!["byte"]!, (string)s["text"]!))];

    /// <summary>ausschluss.json vor L0 (entfaellt seit A 1.18.0).</summary>
    public static byte[] Ausschluss => Encoding.UTF8.GetBytes((string)Daten.Value["ausschluss"]!);

    /// <summary>Lesefunktion fuer <see cref="Lerninhalte.MitLernen"/>: der ganze Ordner wie vor L0 (mit ausschluss.json).</summary>
    public static Func<string, byte[]?> Ordner()
    {
        var heute = LerninhalteTests.Ordner(Lernen);
        return name => name == Lerninhalte.AusschlussDatei ? Ausschluss
            : name == Lerninhalte.GeloeschtDatei ? null
            : Dateien.Contains(name) ? Datei(name) : heute(name);
    }

    /// <summary>Stelle eines Eintrags heute aus seiner Stelle vor L0 (null: geloescht).</summary>
    public static int? StelleHeute(string datei, int stelleVorher)
    {
        var weg = LerninhalteTests.GeloeschtListe().Where(g => g.Datei == datei).Select(g => g.Stelle).ToList();
        return weg.Contains(stelleVorher) ? null : stelleVorher - weg.Count(w => w < stelleVorher);
    }

    private static string Sha(byte[] daten) => Convert.ToHexStringLower(SHA256.HashData(daten));
}
