using System.Globalization;
using Lernplattform.Core.Daten;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle Fehlerpruefung Teil 2, Werkzeug S 0.61.0 (Weisung P12-345): Sammelliste 428 (sicheres Speichern mit
/// <c>Flush(true)</c> vor dem Umbenennen) und 427 (Namensvergleiche ordinal ohne Gross/Klein statt Systemsprache).
/// Jede Pruefung hat eine Gegenprobe mit dem alten Weg.
/// </summary>
public class FehlerpruefungTeil2Tests
{
    private static readonly DateTime Zeit = new(2026, 11, 22, 11, 0, 0);

    private static List<string> SchritteBeimSchreiben(Action schreiben, bool ohneFlush = false)
    {
        var schritte = new List<string>();
        Werkzeugordner.SchrittFuerTests.Value = schritte.Add;
        Werkzeugordner.OhneFlushFuerTests.Value = ohneFlush;
        try
        {
            schreiben();
        }
        finally
        {
            Werkzeugordner.SchrittFuerTests.Value = null;
            Werkzeugordner.OhneFlushFuerTests.Value = false;
        }
        return schritte;
    }

    /// <summary>Flush(true) auf die Zwischendatei laeuft vor dem Umbenennen (das neue Wort steht vor „move“).</summary>
    private static bool FlushVorMove(List<string> schritte) =>
        schritte.IndexOf("move") > 0 && schritte[schritte.IndexOf("move") - 1] == "flush";

    [Fact]
    public void Sammelliste428_SicherSchreiben_FlushVorDemUmbenennen_InhaltVollstaendig()
    {
        var ablage = Daten.NeueAblage();
        var pfad = Path.Combine(ablage.Projekte, "x.fisprojekt");
        File.WriteAllBytes(pfad, [1, 2, 3]);
        var daten = Enumerable.Range(0, 70_000).Select(i => (byte)(i % 251)).ToArray();
        var schritte = SchritteBeimSchreiben(() => Werkzeugordner.SicherSchreiben(pfad, daten));
        Assert.Equal(["flush", "move"], schritte);
        Assert.True(FlushVorMove(schritte));
        Assert.Equal(daten, File.ReadAllBytes(pfad));
        Assert.False(File.Exists(pfad + ".neu"));
        // Gegenprobe: der alte Weg (WriteAllBytes ohne Flush) erfuellt die Pruefung nicht
        var alt = SchritteBeimSchreiben(() => Werkzeugordner.SicherSchreiben(pfad, daten), ohneFlush: true);
        Assert.False(FlushVorMove(alt));
    }

    [Fact]
    public void Sammelliste428_ProjektSpeichern_LaeuftUeberFlush()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Flush Probe", Daten.Spiel, ablage);
        var schritte = SchritteBeimSchreiben(() => p.Speichern(vonHand: true));
        Assert.True(FlushVorMove(schritte), string.Join(",", schritte));
        Assert.True(new FileInfo(p.Datei).Length > 0);
    }

    [Fact]
    public void Sammelliste428_BlueprintNurSchreibweiseUmbenennen_FlushVorDemLoeschen()
    {
        var ablage = Daten.NeueAblage();
        var pfad = Neu("haus probe").Speichern(ablage, Zeit);
        var schritte = SchritteBeimSchreiben(() => Blueprint.Umbenennen(ablage, pfad, "Haus Probe", Zeit));
        Assert.True(FlushVorMove(schritte), string.Join(",", schritte));
        var alle = Blueprint.Alle(ablage);
        Assert.Equal("Haus Probe", Assert.Single(alle).Name);
        var gegen = SchritteBeimSchreiben(() => Blueprint.Umbenennen(ablage, alle[0].Datei, "HAUS PROBE", Zeit), ohneFlush: true);
        Assert.False(FlushVorMove(gegen));
    }

    private static Blueprint Neu(string name) => new()
    {
        Name = name,
        Herkunft = new PyObjekt { { "stadt", "stadt1" }, { "ort", "" }, { "projekt", "Probe" } },
        Modell = "probe_haus",
        Teile = [new PyObjekt { { "art", "block" }, { "x", 0L }, { "y", 0L }, { "w", 1L }, { "t", 1L }, { "h", 1L } }],
    };

    private static T MitKultur<T>(string kultur, Func<T> f)
    {
        var alt = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(kultur);
        try
        {
            return f();
        }
        finally
        {
            CultureInfo.CurrentCulture = alt;
        }
    }

    /// <summary>
    /// Unter tuerkischer Systemsprache sind „Fiat“ und „FIAT“ fuer den alten Vergleich verschieden (I gehoert dort zu ı),
    /// „Fiat“ und „FİAT“ dagegen gleich. Der neue Vergleich ist auf jedem System gleich: Fiat = FIAT, Fiat ≠ FİAT.
    /// </summary>
    [Fact]
    public void Sammelliste427_Gegenprobe_AlterVergleichHaengtAnDerSystemsprache()
    {
        Assert.True(MitKultur("de-DE", () => string.Equals("Fiat", "FIAT", StringComparison.CurrentCultureIgnoreCase)));
        Assert.False(MitKultur("tr-TR", () => string.Equals("Fiat", "FIAT", StringComparison.CurrentCultureIgnoreCase)));
        Assert.True(MitKultur("tr-TR", () => string.Equals("Fiat", "FIAT", StringComparison.OrdinalIgnoreCase)));
        Assert.False(MitKultur("tr-TR", () => string.Equals("Fiat", "FİAT", StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    [InlineData("")]
    public void Sammelliste427_BlueprintNameGibtEs_GleichAufJedemSystem(string kultur)
    {
        var ablage = Daten.NeueAblage();
        MitKultur(kultur, () => Neu("Fiat Haus").Speichern(ablage, Zeit));
        // Name nur in Gross/Klein anders: ein Fehler, auch unter tuerkischer Systemsprache
        Assert.Throws<IOException>(() => MitKultur(kultur, () => Neu("FIAT HAUS").Speichern(ablage, Zeit)));
        Assert.Throws<IOException>(() => MitKultur(kultur, () => Neu("fiat haus").Speichern(ablage, Zeit)));
        var zweiter = MitKultur(kultur, () => Neu("Zweites Haus").Speichern(ablage, Zeit));
        Assert.Throws<IOException>(() => MitKultur(kultur, () => Blueprint.Umbenennen(ablage, zweiter, "FIAT haus", Zeit)));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    public void Sammelliste427_StrassenzugFestUndVorhanden_GleichAufJedemSystem(string kultur)
    {
        Assert.True(MitKultur(kultur, () => Strassenzug.NameFest("WOHNSTRAßE")));
        Assert.True(MitKultur(kultur, () => Strassenzug.NameFest("hauptstraße")));
        Assert.True(MitKultur(kultur, () => Strassenzug.NameFest("LANDSTRAßE")));
        var ablage = Daten.NeueAblage();
        MitKultur(kultur, () => new Strassenzug { Name = "Gasse Mit I" }.Speichern(ablage, Zeit));
        Assert.Throws<IOException>(() => MitKultur(kultur, () => new Strassenzug { Name = "GASSE MIT I" }.Speichern(ablage, Zeit)));
        Assert.Throws<IOException>(() => MitKultur(kultur, () => new Strassenzug { Name = "gasse mit i" }.Speichern(ablage, Zeit)));
    }
}
