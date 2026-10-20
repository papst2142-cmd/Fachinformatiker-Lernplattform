using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, Welle 4: Vergleichswerte G21 (werkzeuge/vergleich/erzeuge_abgleich.py, Python v0.63.2).
/// Kommazahlen wie repr, Sicherung fuer die alte App zeichengleich wie create_backup, und je Fall
/// ein Versuch des Abgleichs ohne Netz (Mischbetrieb, Uhr-Nachtrag, Messung, Zukunftsmarken,
/// Zusammenfuehren, export_local, Vergleich mit der Datei) mit gleichem Stand und gleichen Zahlen.
/// </summary>
public class AbgleichVergleichTests
{
    private static readonly JsonElement G21 = VergleichsHilfe.Lade("abgleich.json");
    private static readonly IReadOnlyList<(string Tabelle, string Alt, string Neu)> Umbenennungen = Core.Daten.Umbenennungen.Laden(Pfade.Inhalte);

    private static Uhr Uhr => SpeichernTests.FesteUhr(G21.GetProperty("jetzt_utc").GetString()!);

    private static Uhrabweichung Abweichung => new(() => G21.GetProperty("lauf").GetDouble(), G21.GetProperty("start").GetString(),
        G21.GetProperty("prozess").GetString()!);

    public static TheoryData<string> Faelle()
    {
        var daten = new TheoryData<string>();
        foreach (var fall in G21.GetProperty("faelle").EnumerateArray())
        {
            daten.Add(fall.GetProperty("name").GetString()!);
        }
        return daten;
    }

    [Fact]
    public void ZahlenWiePython()
    {
        var anzahl = 0;
        foreach (var z in G21.GetProperty("zahlen").EnumerateArray())
        {
            var wert = BitConverter.Int64BitsToDouble(z[0].GetInt64());
            Assert.Equal(z[1].GetString(), PythonJson.Zahl(wert));
            anzahl++;
        }
        Assert.True(anzahl >= 30);
    }

    [Fact]
    public void SicherungWiePython()
    {
        using var ordner = new TempOrdner();
        var s = G21.GetProperty("sicherung");
        var pfad = UebernahmeTests.BaueDatenbank(s.GetProperty("stand"), Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var lernstand = new Lernstand(pfad);
        var daten = Ruecksicherung.Lesen(lernstand);
        Assert.Equal(s.GetProperty("export").GetString(), PythonJson.Text(daten));
        var einstellungen = JsonNode.Parse(s.GetProperty("einstellungen_eingabe").GetRawText())!.AsObject();
        var huelle = Ruecksicherung.Huelle(daten, Ruecksicherung.Tagesziel(einstellungen), s.GetProperty("app_version").GetString()!,
            DateTime.ParseExact(s.GetProperty("erstellt").GetString()!, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            s.GetProperty("geraet").GetString()!);
        Assert.Equal(s.GetProperty("text").GetString(), PythonJson.Text(huelle));
        Assert.Equal(s.GetProperty("pruefsumme").GetString(), (string?)huelle["pruefsumme"]);
        // Die gepackte Datei liest die eigene Pruefung wie read_backup
        Ruecksicherung.Pruefen(Ruecksicherung.Packen(huelle));
    }

    [Theory]
    [MemberData(nameof(Faelle))]
    public void VersuchWiePython(string name)
    {
        using var ordner = new TempOrdner();
        var fall = G21.GetProperty("faelle").EnumerateArray().Single(f => f.GetProperty("name").GetString() == name);
        var pfad = UebernahmeTests.BaueDatenbank(fall.GetProperty("vorher"), Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var datei = fall.GetProperty("datei").ValueKind == JsonValueKind.Null ? null : GitHubAttrappe.Packen(fall.GetProperty("datei").GetString()!);
        double? dev = fall.GetProperty("abweichung").ValueKind == JsonValueKind.Null ? null : fall.GetProperty("abweichung").GetDouble();

        var r = Abgleich.Versuch(new Lernstand(pfad), datei, dev, Uhr, Abweichung, Umbenennungen, Path.Combine(ordner.Pfad, "sicherungen"));

        var erwartet = VergleichsHilfe.Stand(fall.GetProperty("nachher"));
        var ist = VergleichsHilfe.Stand(pfad, fall.GetProperty("nachher"));
        Assert.True(erwartet.SequenceEqual(ist), VergleichsHilfe.Unterschied(erwartet, ist));
        Assert.Equal(fall.GetProperty("export").GetString(), PythonJson.Text(r.Export));
        Assert.Equal(fall.GetProperty("received").GetInt32(), r.Erhalten);
        Assert.Equal(fall.GetProperty("gone").GetInt32(), r.Weg);
        Assert.Equal(fall.GetProperty("invalid").GetInt32(), r.Ungueltig);
        Assert.Equal(fall.GetProperty("clamped").GetBoolean(), r.Gekappt);
        Assert.Equal(fall.GetProperty("mixed").GetBoolean(), r.Misch);
        Assert.Equal(fall.GetProperty("gleich").GetBoolean(), r.Gleich);
        Assert.Equal(fall.GetProperty("sent").GetInt32(), r.Gesendet);
    }
}
