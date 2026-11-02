using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 11 FW2 "Spiel wie Python" (Sammelliste 79, 80, 82). Die Sollwerte sind mit python3.12 -I
/// gerechnet (Python-Linie v0.63.2): parse_prefix, str.capitalize() und Rechnen ohne stillen Ueberlauf.
/// </summary>
public class SpielWiePythonTests
{
    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    // 79: text.isdigit() nimmt hochgestellte und eingekreiste Ziffern an, int() wirft dann ValueError
    [Theory]
    [InlineData("²")]
    [InlineData("/²")]
    [InlineData("²6")]
    [InlineData("①")]
    public void PraefixMitHochgestellterZifferWirftWiePython(string eingabe)
    {
        Assert.Throws<FormatException>(() => Bewertung.PraefixLesen(eingabe));
    }

    // Dezimalziffern ausserhalb der Grundebene (zwei UTF-16-Zeichen) liest Python wie andere Ziffern
    [Theory]
    [InlineData("𝟐𝟔", 26L)]
    [InlineData("/𝟐𝟔", 26L)]
    [InlineData("/𝟑𝟑", null)]
    [InlineData("٢٦", 26L)]
    [InlineData("/24", 24L)]
    [InlineData("abc", null)]
    public void PraefixWiePython(string eingabe, long? soll)
    {
        Assert.Equal(soll, Bewertung.PraefixLesen(eingabe));
    }

    // 80: capitalize() mit Titelschreibung (volle Zuordnung) und lower() fuer den Rest, Schluss-Sigma im ganzen Text
    [Theory]
    [InlineData("ß", "Ss")]
    [InlineData("ßa", "Ssa")]
    [InlineData("ﬁx", "Fix")]
    [InlineData("ǆa", "ǅa")]
    [InlineData("ǳ", "ǲ")]
    [InlineData("ŉ", "ʼN")]
    [InlineData("ᾳ", "ᾼ")]
    [InlineData("ΑΣ", "Ας")]
    [InlineData("ΣΑΣ", "Σας")]
    [InlineData("İX", "İx")]
    [InlineData("aİ", "Ai̇")]
    [InlineData("𐐨ab", "𐐀ab")]
    [InlineData("nORMAL", "Normal")]
    [InlineData("x", "X")]
    [InlineData("", "")]
    public void CapitalizeWiePython(string text, string soll)
    {
        Assert.Equal(soll, PyTitel.Capitalize(text));
    }

    [Fact]
    public void TitelTabelleHatDieBekanntenFaelle()
    {
        // Gegenprobe zur erzeugten Tabelle (die CI erzeugt sie bytegleich neu)
        Assert.Equal("Ss", PyTitel.Tabelle['ß']);
        Assert.Equal("ǅ", PyTitel.Tabelle['ǆ']);
        Assert.Equal("ǅ", PyTitel.Tabelle['Ǆ']);
        Assert.False(PyTitel.Tabelle.ContainsKey('A'));
    }

    [Fact]
    public void UnbekannteSchwierigkeitMitScharfemS()
    {
        // difficulty_name: (level or DIFF_NORMAL).capitalize() fuer eine Stufe ohne Namen
        Assert.Equal("Ssonder", Spielstaende.SchwierigkeitName("ßonder", Inhalt));
        Assert.Equal("Normal", Spielstaende.SchwierigkeitName(null, Inhalt));
    }

    // 82: Python-int ist unbegrenzt; C# bricht ab, statt still ueberzulaufen
    [Fact]
    public void RechnenLaeuftNichtStillUeber()
    {
        Assert.Throws<OverflowException>(() => Py.Plus(long.MaxValue, 1L));
        Assert.Throws<OverflowException>(() => Py.Minus(long.MinValue, 1L));
        Assert.Throws<OverflowException>(() => Py.Mal(long.MaxValue, 2L));
        Assert.Equal(long.MaxValue, Py.Plus(long.MaxValue - 1, 1L));
        Assert.Equal(3.5, Py.Plus(2.5, 1L));
    }

    [Fact]
    public void GeldImSpielstandLaeuftNichtStillUeber()
    {
        static SpielEreignis Umzug(long geld, string zeit) => new(zeit, Ereignisse.Umzug, new PyObjekt
        {
            { "geld", geld }, { "wohnung", "wg" }, { "miete", 0L }, { "kaution", 0L }, { "tag", 1L },
        });
        var stand = new Spielstand([Umzug(long.MaxValue, "2026-06-15T08:00:00")], Inhalt);
        Assert.Equal(long.MaxValue, stand.Geld);
        Assert.Throws<OverflowException>(() => new Spielstand([Umzug(long.MaxValue, "2026-06-15T08:00:00"), Umzug(1, "2026-06-15T08:01:00")], Inhalt));
    }
}
