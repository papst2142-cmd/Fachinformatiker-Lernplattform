using System.Text;
using System.Text.Json.Nodes;

namespace Lernplattform.Core.Pruefung.Echt;

/// <summary>
/// A 1.20.0 (W2, Weisung P12-446, Nico 4a): Ausgabe und Datenauszug gibt es noch in keinem echten Satz, nur als Probesatz fuer
/// Tests und den Start-Test. Grundlage ist die neueste Fassung von alle_ap1_03 (mit dem Schreibtischtest 4 b); angehaengt werden
/// die Beispiele der Entwuerfe P12-267 (Bilder 05, 07 und 10): Anlage 7 Ausgabe, Anlage 8 CSV, Anlage 9 JSON. Wird nie angeboten
/// und nie gespeichert.
/// </summary>
public static class EchtProbesatz
{
    public const string Kennung = "alle_ap1_03";

    /// <summary>Aufgabe und Teil, die auf die neuen Anlagen verweisen: Ausgabe, CSV, JSON.</summary>
    public static readonly IReadOnlyList<(int Aufgabe, string Teil, string Anlage)> Verweise = [(1, "f", "7"), (3, "a", "8"), (2, "d", "9")];

    public static readonly IReadOnlyList<string> Ausgabe =
    [
        @"C:\> ipconfig", "Ethernet-Adapter Ethernet:", "   IPv4-Adresse  . . . . . . . . . . : 10.20.30.57",
        "   Subnetzmaske  . . . . . . . . . . : 255.255.255.0", "   Standardgateway . . . . . . . . . : 10.20.31.1", @"C:\> ping 10.20.30.1",
        "Antwort von 10.20.30.1: Bytes=32 Zeit<1ms TTL=64", "Antwort von 10.20.30.1: Bytes=32 Zeit<1ms TTL=64", @"C:\> ping 10.99.0.10",
        "PING: Fehler bei der Übertragung. Allgemeiner Fehler.", "PING: Fehler bei der Übertragung. Allgemeiner Fehler.",
    ];

    public static readonly IReadOnlyList<string> Csv =
    [
        "kundennr;name;plz;ort;letzte_bestellung;umsatz_2025", "K-1001;Becker GmbH;34117;Kassel;2026-03-14;12480,50",
        "K-1002;Brunnhof KG;34119;;2025-11-02;", "K-1003;becker gmbh;34117;Kassel;14.03.2026;980,00",
        "K-1004;Lindner AG;3412;Kassel;2026-02-30;4310,25", "K-1005;Ostwerk eG;34121;Kassel;2026-01-09;7055,00",
    ];

    public static readonly IReadOnlyList<string> Json =
    [
        "{", "  \"artikel\": \"R-2207\",", "  \"name\": \"Regenjacke Föhr\",", "  \"preis\": \"79,90\",", "  \"waehrung\": \"EUR\",",
        "  \"groessen\": [\"S\", \"M\", \"L\", \"XXL\", \"M\"],", "  \"farbe\": null,", "  \"lager\": { \"kassel\": 14, \"hamm\": -3 },",
        "  \"geaendert\": \"2026-13-02T08:15:00\"", "}",
    ];

    /// <summary>Datei im Format 6 mit genau dem Probesatz, gebaut aus der echten Datei <paramref name="echteDatei"/>.</summary>
    public static byte[] Datei(byte[] echteDatei)
    {
        var wurzel = JsonNode.Parse(echteDatei)!.AsObject();
        var satz = wurzel["saetze"]!.AsArray().Where(s => (string?)s!["kennung"] == Kennung).Last()!.DeepClone().AsObject();
        var anlagen = satz["anlagen"]!.AsArray();
        static JsonArray Liste(IEnumerable<string> zeilen) => new([.. zeilen.Select(z => (JsonNode?)JsonValue.Create(z))]);
        anlagen.Add(new JsonObject
        {
            ["nr"] = "7", ["titel"] = "Ausgaben am PC WS-07", ["art"] = EchtDaten.Ausgabe, ["zeilen"] = Liste(Ausgabe),
            ["eingaben"] = new JsonArray(1, 6, 9),
        });
        anlagen.Add(new JsonObject
        {
            ["nr"] = "8", ["titel"] = "Auszug kunden.csv (Trennzeichen ;)", ["art"] = EchtDaten.Daten, ["format"] = EchtDaten.Csv,
            ["trennzeichen"] = ";", ["zeilen"] = Liste(Csv),
        });
        anlagen.Add(new JsonObject
        {
            ["nr"] = "9", ["titel"] = "Datensatz im Produkt-Feed (JSON)", ["art"] = EchtDaten.Daten, ["format"] = EchtDaten.Json,
            ["zeilen"] = Liste(Json),
        });
        foreach (var (aufgabe, teil, anlage) in Verweise)
        {
            var t = satz["aufgaben"]![aufgabe - 1]!["teile"]!.AsArray().First(x => (string?)x!["nr"] == teil)!.AsObject();
            if (t["anlagen"] is not JsonArray liste)
            {
                liste = [];
                t["anlagen"] = liste;
            }
            liste.Add(anlage);
        }
        var datei = new JsonObject { ["format"] = EchtSaetze.Format, ["saetze"] = new JsonArray(satz) };
        return Encoding.UTF8.GetBytes(datei.ToJsonString());
    }
}
