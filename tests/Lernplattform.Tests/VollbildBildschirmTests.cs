using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Ton;
using Lernplattform.Core.Texte;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>Patch A 0.37.0 (Sammelliste 205): Bildschirm fuer das Vollbild merken und wiederfinden.</summary>
public sealed class VollbildBildschirmTests
{
    private static readonly Bildschirm Links = new(0, 0, 1920, 1080, true, "DELL U2419H");
    private static readonly Bildschirm Rechts = new(1920, 0, 2560, 1440, false, "LG 27GL850");

    [Fact]
    public void OhneMerkerBildschirmDesFensters()
    {
        Assert.Null(VollbildBildschirm.Lesen(new JsonObject()));
        Assert.Null(VollbildBildschirm.Finden([Links, Rechts], null));
        // kaputter Eintrag zaehlt als Vorgabe
        Assert.Null(VollbildBildschirm.Lesen(new JsonObject { [VollbildBildschirm.Schluessel] = "Bildschirm 2" }));
        Assert.Null(VollbildBildschirm.Lesen(JsonNode.Parse("""{"vollbild_bildschirm":{"x":0,"y":0,"breite":0,"hoehe":1080}}""")!.AsObject()));
    }

    [Fact]
    public void MerkenUndWiederfinden()
    {
        var werte = new JsonObject();
        VollbildBildschirm.Wert(werte, Rechts);
        Assert.Equal("""{"vollbild_bildschirm":{"x":1920,"y":0,"breite":2560,"hoehe":1440,"name":"LG 27GL850"}}""", werte.ToJsonString());
        var gemerkt = VollbildBildschirm.Lesen(werte)!;
        Assert.Equal(1, VollbildBildschirm.Finden([Links, Rechts], gemerkt));
        // andere Reihenfolge: nach Lage und Groesse
        Assert.Equal(0, VollbildBildschirm.Finden([Rechts, Links], gemerkt));
        // verschoben (anders angeordnet): nach dem Namen
        Assert.Equal(0, VollbildBildschirm.Finden([Rechts with { X = -2560 }, Links], gemerkt));
        // fehlt: Bildschirm des Fensters, der Merker bleibt
        Assert.Null(VollbildBildschirm.Finden([Links], gemerkt));
        Assert.NotNull(werte[VollbildBildschirm.Schluessel]);
        // ohne Namen oder mit zwei gleichen Namen nur nach Lage und Groesse
        Assert.Null(VollbildBildschirm.Finden([Rechts with { X = -2560, Name = "" }], gemerkt with { Name = "" }));
        Assert.Null(VollbildBildschirm.Finden([Rechts with { X = -2560 }, Rechts with { X = 5000 }], gemerkt));
        VollbildBildschirm.Wert(werte, null);
        Assert.Equal("{}", werte.ToJsonString());
    }

    [Fact]
    public void EintraegeDerAuswahl()
    {
        Assert.Equal([TonTexte.VollbildFenster, "Bildschirm 1 (1920 × 1080, Hauptbildschirm)", "Bildschirm 2 (2560 × 1440)"],
            VollbildBildschirm.Eintraege([Links, Rechts]));
        Assert.Equal([TonTexte.VollbildFenster], VollbildBildschirm.Eintraege([]));
    }

    [Fact]
    public void GespeichertNurUeberEinstellungenAendern()
    {
        using var temp = new TempOrdner();
        var einstellungen = new Einstellungen(temp.Pfad);
        Assert.True(einstellungen.Aendern(w => VollbildBildschirm.Wert(w, Links)));
        Assert.Equal(Links with { Haupt = false }, VollbildBildschirm.Lesen(new Einstellungen(temp.Pfad).Laden()));
    }
}
