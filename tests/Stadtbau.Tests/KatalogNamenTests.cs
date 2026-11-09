using System.Text;
using System.Text.Json.Nodes;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Sammelliste 222 (Weisung P12-78): jedes Katalog-Stueck hat genau einen deutschen Namen (Datei
/// <c>inhalte_stadtbau/namen_de.json</c>), alle verschieden, hoechstens 28 Zeichen und hoechstens ein Bindestrich; die
/// Suche findet jedes Stueck ueber den deutschen und den englischen Namen. Gegenproben: fehlt ein Name, steht ein Name
/// ohne Eintrag in der Datei, kommt ein Name doppelt vor oder fehlt die Datei, laedt der Katalog nicht (nie still englisch).
/// </summary>
public class KatalogNamenTests
{
    private static readonly Lazy<Katalog> Geteilt = new(() => Katalog.Laden(Daten.Lesen)!);

    private static Katalog K => Geteilt.Value;

    [Fact]
    public void JederEintrag_GenauEinDeutscherName_AlleVerschieden()
    {
        Assert.Equal(2073, K.Eintraege.Count);
        Assert.All(K.Eintraege, e => Assert.False(string.IsNullOrWhiteSpace(e.Anzeige), e.Id));
        Assert.Equal(K.Eintraege.Count, K.Eintraege.Select(e => e.Anzeige).Distinct(StringComparer.Ordinal).Count());
        var namen = JsonNode.Parse(Daten.Lesen(Katalog.NamenDatei)!)!["namen"]!.AsObject();
        Assert.Equal(K.Eintraege.Count, namen.Count);
        Assert.All(K.Eintraege, e => Assert.Equal(namen[e.Id]!.GetValue<string>(), e.Anzeige));
    }

    [Fact]
    public void Namen_Hoechstens28Zeichen_HoechstensEinBindestrich()
    {
        var lang = K.Eintraege.Where(e => e.Anzeige.Length > 28 || e.Anzeige.Count(c => c == '-') > 1)
            .Select(e => e.Id + " = " + e.Anzeige).ToList();
        Assert.Empty(lang);
    }

    [Fact]
    public void Suche_FindetJedesStueck_DeutschUndEnglisch()
    {
        var fehlt = new List<string>();
        foreach (var e in K.Eintraege)
        {
            if (!K.Suchen(e.Anzeige).Contains(e))
            {
                fehlt.Add("deutsch " + e.Id);
            }
            if (!K.Suchen(e.Englisch).Contains(e))
            {
                fehlt.Add("englisch " + e.Id);
            }
        }
        Assert.Empty(fehlt);
    }

    [Fact]
    public void Kennung_BleibtWieVorher_EnglischerNameAusDemPaket()
    {
        var liste = JsonNode.Parse(Daten.Lesen(Katalog.Ordner + "katalog.json")!)!["modelle"]!.AsArray();
        Assert.Equal(liste.Count, K.Eintraege.Count);
        foreach (var m in liste)
        {
            var e = K.Eintrag(m!["id"]!.GetValue<string>());
            Assert.NotNull(e);
            Assert.Equal(m["anzeige"]!.GetValue<string>(), e.Englisch);
        }
    }

    private static Func<string, byte[]?> MitNamen(Action<JsonObject> aendern) => p =>
    {
        var d = Daten.Lesen(p);
        if (p != Katalog.NamenDatei || d is null)
        {
            return d;
        }
        var json = JsonNode.Parse(d)!.AsObject();
        aendern(json["namen"]!.AsObject());
        return Encoding.UTF8.GetBytes(json.ToJsonString());
    };

    [Fact]
    public void Gegenprobe_NameFehlt_Fehler()
    {
        var f = Assert.Throws<InvalidDataException>(() => Katalog.Laden(MitNamen(n => n.Remove("furniture/chair"))));
        Assert.Contains("furniture/chair", f.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Gegenprobe_NameOhneEintrag_Fehler()
    {
        var f = Assert.Throws<InvalidDataException>(() => Katalog.Laden(MitNamen(n => n["furniture/gibtesnicht"] = "Gibt es nicht")));
        Assert.Contains("furniture/gibtesnicht", f.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Gegenprobe_NameDoppelt_Fehler()
    {
        var f = Assert.Throws<InvalidDataException>(() => Katalog.Laden(MitNamen(n => n["furniture/chair"] = n["furniture/bathroomCabinet"]!.GetValue<string>())));
        Assert.Contains("doppelt", f.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Gegenprobe_DateiFehlt_Fehler()
    {
        Assert.Throws<FileNotFoundException>(() => Katalog.Laden(p => p == Katalog.NamenDatei ? null : Daten.Lesen(p)));
    }
}
