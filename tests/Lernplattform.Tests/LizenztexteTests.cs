using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8 W2 (Weisung P8-3, Sammelliste 74, Nico 5a): Knopf "Lizenzen" zeigt THIRD_PARTY_LICENSES.txt
/// (im Programm eingebettet, auch in der APK). Die Datei im Repo prueft release.py zusammen gegen die
/// beim Bau erzeugten Texte; hier: Zerlegen ist verlustfrei, jeder Bestandteil der Uebersicht hat
/// seinen Text, kein Block ist zu gross fuer die Anzeige.
/// </summary>
public sealed class LizenztexteTests
{
    private static string Datei(string name) =>
        File.ReadAllText(Path.Combine(Pfade.Wurzel, name)).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void ZerlegenIstVerlustfrei()
    {
        var text = Datei("THIRD_PARTY_LICENSES.txt");
        var (vorspann, teile) = Lizenztexte.Zerlegen(text);
        Assert.StartsWith("Fachinformatiker Lernplattform - Lizenztexte der Fremdbestandteile", vorspann, StringComparison.Ordinal);
        var zeilen = new List<string> { vorspann };
        foreach (var teil in teile)
        {
            zeilen.Add(Lizenztexte.Trenner);
            zeilen.Add(teil.Titel);
            zeilen.AddRange(teil.Bloecke);
        }
        Assert.Equal(text, string.Join('\n', zeilen));
    }

    [Fact]
    public void JederBestandteilDerUebersichtHatSeinenText()
    {
        var (_, teile) = Lizenztexte.Zerlegen(Datei("THIRD_PARTY_LICENSES.txt"));
        var uebersicht = Datei("THIRD_PARTY_NOTICES.txt").Split('\n')
            .Where(z => z.StartsWith("- ", StringComparison.Ordinal))
            .Select(z => z[2..z.IndexOf(" | ", StringComparison.Ordinal)])
            .ToList();
        Assert.True(uebersicht.Count > 100, "Uebersicht mit " + uebersicht.Count + " Bestandteilen");
        Assert.Equal(uebersicht, teile.Select(t => t.Titel[..t.Titel.LastIndexOf(", Lizenz ", StringComparison.Ordinal)]));
        Assert.All(teile, t => Assert.NotEmpty(string.Concat(t.Bloecke).Trim()));
    }

    [Fact]
    public void BloeckeHoechstensBlockGroesse()
    {
        var (_, teile) = Lizenztexte.Zerlegen(Datei("THIRD_PARTY_LICENSES.txt"));
        var bloecke = teile.SelectMany(t => t.Bloecke).ToList();
        Assert.All(bloecke, b => Assert.True(b.Length <= Lizenztexte.BlockGroesse || !b.Contains('\n', StringComparison.Ordinal),
            "Block mit " + b.Length + " Zeichen"));
        // der groesste Bestandteil (Velopack, fast 1 MB) ist in viele Bloecke geteilt
        Assert.True(teile.Max(t => t.Bloecke.Count) > 400);
    }

    [Fact]
    public void LizenzzeileImTextZaehltNichtAlsBestandteil()
    {
        var text = "Kopf\n\n" + Lizenztexte.Trenner + "\nA (1.0), Lizenz MIT\n\n--- LICENSE ---\n" + Lizenztexte.Trenner
            + "\nkein Titel\n" + Lizenztexte.Trenner + "\nB (2.0), Lizenz Apache-2.0\nText\n";
        var (vorspann, teile) = Lizenztexte.Zerlegen(text);
        Assert.Equal("Kopf\n", vorspann);
        Assert.Equal(["A (1.0), Lizenz MIT", "B (2.0), Lizenz Apache-2.0"], teile.Select(t => t.Titel));
        Assert.Contains("kein Titel", string.Join('\n', teile[0].Bloecke), StringComparison.Ordinal);
    }

    [Fact]
    public void TitelBrechenNachPunktUndStrich()
    {
        Assert.Equal("Xamarin.\u200BAndroid\u200BX.\u200BCore (1.\u200B0), Lizenz MIT-\u200B0",
            Lizenztexte.Brechbar("Xamarin.AndroidX.Core (1.0), Lizenz MIT-0"));
        Assert.Equal("View\u200BModel\u200BSaved\u200BState NETCore", Lizenztexte.Brechbar("ViewModelSavedState NETCore"));
        Assert.Equal("a/\u200Bb_\u200Bc", Lizenztexte.Brechbar("a/b_c"));
        Assert.Equal("Ohne", Lizenztexte.Brechbar("Ohne"));
    }

    /// <summary>Eingebettet wie LICENSE.txt und THIRD_PARTY_NOTICES.txt; release.py prueft die Datei gegen den Bau.</summary>
    [Fact]
    public void EingebettetUndGeprueft()
    {
        var projekt = Datei(Path.Combine("src", "Lernplattform.App", "Lernplattform.App.csproj"));
        Assert.Contains(@"..\..\THIRD_PARTY_NOTICES.txt;..\..\THIRD_PARTY_LICENSES.txt"" LogicalName=""texte/", projekt, StringComparison.Ordinal);
        var release = Datei(Path.Combine("werkzeuge", "release", "release.py"));
        Assert.Contains(@"os.path.join(WURZEL, ""THIRD_PARTY_LICENSES.txt""))", release, StringComparison.Ordinal);
        var optionen = Datei(Path.Combine("src", "Lernplattform.App", "Views", "OptionenSeite.cs"));
        Assert.Contains("Lizenztexte.Zerlegen(Eingebettet(\"THIRD_PARTY_LICENSES.txt\"))", optionen, StringComparison.Ordinal);
        Assert.Equal("Lizenzen", UeberTexte.KnopfLizenzen);
    }
}
