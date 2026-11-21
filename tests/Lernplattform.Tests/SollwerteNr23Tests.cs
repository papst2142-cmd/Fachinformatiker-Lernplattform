using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.2.0, Sollwert-Aenderung Nr. 23 (Weisungen P12-335/P12-336, Nico: C#-Sollwerte auf den breiten Grundrissen).
/// Gegenprobe: dieselben Pruefungen wie in <see cref="SpielInhaltTests"/>, <see cref="SpielWegeTests"/> und
/// <see cref="SpielPruefungTests"/>, aber mit den alten, schmalen Grundrissen (<see cref="SpielInhaltTests.InhaltSchmal"/>),
/// muessen gegen die neuen C#-Sollwerte rot sein; mit den breiten (<see cref="SpielInhaltTests.Inhalt"/>) sind sie gruen.
/// </summary>
public sealed class SollwerteNr23Tests
{
    [Fact]
    public void Inhalte_MitSchmalenGrundrissen_WeichenAb()
    {
        var soll = SpielInhaltTests.Soll;
        // genau die 6 Schluessel mit Grundrissen und die 27 Auftraege mit einer Stelle im Raum
        var werte = soll.GetProperty("werte").EnumerateObject()
            .Where(p => p.Value.GetProperty("sha256").GetString() != Py.Pruefsumme(SpielInhaltTests.InhaltSchmal[p.Name])).Select(p => p.Name).ToList();
        Assert.Equal(["firma", "gebaeude", "kollegen", "kunden", "wohnungen"], werte.Where(w => w != "aufgaben").Order(StringComparer.Ordinal));
        var auftraege = soll.GetProperty("aufgaben").EnumerateArray().Select((e, i) => (e, i))
            .Count(x => x.e.GetProperty("sha256").GetString() != Py.Pruefsumme(((List<object?>)SpielInhaltTests.InhaltSchmal["aufgaben"]!)[x.i]));
        Assert.Equal(27, auftraege);
        Assert.All(soll.GetProperty("werte").EnumerateObject(),
            p => Assert.Equal(p.Value.GetProperty("sha256").GetString(), Py.Pruefsumme(SpielInhaltTests.Inhalt[p.Name])));
    }

    [Fact]
    public void Wege_MitSchmalenGrundrissen_WeichenAb()
    {
        var soll = SpielWegeTests.Soll.GetProperty("grundrisse").EnumerateArray().ToList();
        static int Abweichend(List<System.Text.Json.JsonElement> soll, List<(string Name, LaufOrt Ort)> orte) =>
            soll.Select((g, i) => (g, i)).Count(x => x.g.GetProperty("raster").GetProperty("frei_anzahl").GetInt32() != orte[x.i].Ort.Frei.Count
                || x.g.GetProperty("raster").GetProperty("basis_anzahl").GetInt32() != orte[x.i].Ort.Basis.Count);
        Assert.True(Abweichend(soll, SpielWegeTests.Grundrisse(SpielInhaltTests.InhaltSchmal)) > 0);
        Assert.Equal(0, Abweichend(soll, SpielWegeTests.Grundrisse()));
    }

    [Fact]
    public void Ladepruefung_MitSchmalenGrundrissen_WeichtAb()
    {
        // Fall mit veraenderter Start-Einrichtung: im breiten Grundriss versperrt die Matratze eine Tuer, im schmalen passt sie nicht
        var fall = SpielPruefungTests.Soll.GetProperty("faelle").EnumerateArray().Single(f =>
            f.TryGetProperty("probleme", out var p) && p.EnumerateArray().Any(x => x.GetString()!.Contains("Matratze würde eine Tür", StringComparison.Ordinal)));
        var soll = PythonJson.Text(Py.Lesen(fall.GetProperty("probleme").GetRawText()));
        string Ist(PyObjekt inhalt) => PythonJson.Text(SpielPruefung.Pruefen(
            SpielPruefungTests.Anwenden((PyObjekt)Py.Kopie(inhalt)!, fall.GetProperty("aenderungen")), SpielPruefungTests.Lernen).Cast<object?>().ToList());
        Assert.Equal(soll, Ist(SpielInhaltTests.Inhalt));
        Assert.NotEqual(soll, Ist(SpielInhaltTests.InhaltSchmal));
    }
}
