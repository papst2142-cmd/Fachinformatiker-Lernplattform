using System.Text.Json;
using System.Xml.Linq;
using Lernplattform.Core.Update;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 6 W1 (Weisung P6-2, T1, Sammelliste 73): Tag- und Namensregel des Release-Ablaufs. Dieselben Faelle
/// (werkzeuge/release/tag_faelle.json) prueft werkzeuge/release/release.py im Selbsttest; der Release-Ablauf
/// bricht ab, wenn der Tag nicht zur Version passt. UpdateVersion.Lesen bleibt unveraendert (G41).
/// </summary>
public class ReleaseTests
{
    private static JsonElement Faelle()
    {
        using var datei = File.OpenRead(Path.Combine(Pfade.Wurzel, "werkzeuge", "release", "tag_faelle.json"));
        return JsonDocument.Parse(datei).RootElement.GetProperty("faelle").Clone();
    }

    [Fact]
    public void TagRegelWieTabelle()
    {
        var faelle = Faelle();
        Assert.True(faelle.GetArrayLength() >= 10);
        foreach (var fall in faelle.EnumerateArray())
        {
            var stufe = fall.GetProperty("stufe").GetString();
            var version = fall.GetProperty("version").GetString();
            var erwartet = fall.GetProperty("tag").ValueKind == JsonValueKind.Null ? null : fall.GetProperty("tag").GetString();
            Assert.True(erwartet == UpdateVersion.ReleaseTag(stufe, version), stufe + " " + version);
        }
    }

    [Fact]
    public void AngenommenerTagLiestSichWieAnzeigeUndPaket()
    {
        foreach (var fall in Faelle().EnumerateArray())
        {
            if (fall.GetProperty("tag").GetString() is not { } tag)
            {
                continue;
            }
            var stufe = fall.GetProperty("stufe").GetString()!;
            var version = fall.GetProperty("version").GetString()!;
            var ausTag = UpdateVersion.Lesen(tag);
            Assert.NotNull(ausTag);
            Assert.Equal(ausTag, UpdateVersion.Lesen((stufe + " " + version).Trim()));
            Assert.Equal(ausTag, UpdateVersion.Lesen(ausTag!.Paket));
            Assert.Equal(ausTag.Anzeige, (stufe + " " + version).Trim());
        }
    }

    [Fact]
    public void AbgelehnterStufenloserTagWaereNeuerAlsJedeAlphaVersion()
    {
        // Darum lehnt die Regel ihn ab (Sammelliste 73): "v0.12.0" laese sich als 1.0-Reihe
        Assert.Null(UpdateVersion.ReleaseTag("", "0.12.0"));
        Assert.True(UpdateVersion.IstNeuer("v0.12.0", "B 9.9.9"));
    }

    [Fact]
    public void VersionImRepoErgibtGenauEinenTag()
    {
        var props = XDocument.Load(Path.Combine(Pfade.Wurzel, "Directory.Build.props"));
        // gleich release.py props(): Werte ohne Leerraum am Rand
        var version = props.Descendants("Version").Single().Value.Trim();
        var stufe = props.Descendants("Stufe").Single().Value.Trim();
        var tag = UpdateVersion.ReleaseTag(stufe, version);
        Assert.NotNull(tag);
        // Paketversion nach der Formel in Directory.Build.props (PaketVersion)
        var paket = stufe == "" ? version : "1.0.0-" + stufe.ToLowerInvariant() + "." + version;
        Assert.Equal(UpdateVersion.Lesen(tag), UpdateVersion.Lesen(paket));
        Assert.Equal(UpdateVersion.Lesen(tag)!.Anzeige, (stufe + " " + version).Trim());
    }
}
