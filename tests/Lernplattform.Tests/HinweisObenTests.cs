using System.Reflection;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.13.0 (Sammelliste 464, Weisung P12-394, Nico 1a 2a 3a 5a): Ablauf des Hinweises oben (<see cref="HinweisOben"/>).
/// Abnahmesaetze: neuer Text 6 s ganz, danach nur der Titel blass, beim Weitergehen weg (2a); Tippen oeffnet den ganzen Text,
/// noch einmal Tippen zurueck zum Titel, Weitergehen schliesst (3a); B-3 kein Text geht verloren: fuer jeden festen Text des
/// Spiels (alle Konstanten der Spieltexte) ist der aufgeklappte Text (Titel und Rest) derselbe Text; Taste H (Standard) ohne
/// Doppelbelegung. Gegenproben: ohne Weitergehen bleibt der Titel stehen, kleine Wackler (unter <see cref="HinweisOben.WeiterAb"/>)
/// schliessen nicht.
/// </summary>
public sealed class HinweisObenTests
{
    private const string Matratze = "Matratze\nHier schläfst du.\nAntippen: hinlegen.";

    [Fact]
    public void NeuerText_SechsSekundenGanz_DannTitel_BeimWeitergehenWeg()
    {
        var h = new HinweisOben();
        Assert.True(h.Takt(Matratze, TimeSpan.Zero, 1, 1));
        Assert.Equal(HinweisOben.Stufe.Voll, h.Jetzt);
        // innerhalb der 6 s bleibt er ganz, auch wenn die Figur geht (2a: ganz fuer 6 s)
        Assert.False(h.Takt(Matratze, TimeSpan.FromSeconds(5.9), 3, 3));
        Assert.Equal(HinweisOben.Stufe.Voll, h.Jetzt);
        Assert.False(h.Takt(Matratze, TimeSpan.FromSeconds(6), 3, 3));
        Assert.Equal(HinweisOben.Stufe.Titel, h.Jetzt);
        // Gegenprobe: ohne Weitergehen bleibt der Titel, kleine Wackler auch
        Assert.False(h.Takt(Matratze, TimeSpan.FromSeconds(60), 3 + (HinweisOben.WeiterAb / 2), 3));
        Assert.Equal(HinweisOben.Stufe.Titel, h.Jetzt);
        Assert.False(h.Takt(Matratze, TimeSpan.FromSeconds(61), 3 + (HinweisOben.WeiterAb * 1.5f), 3));
        Assert.Equal(HinweisOben.Stufe.Aus, h.Jetzt);
        // derselbe Text bleibt weg, ein neuer kommt wieder ganz
        Assert.False(h.Takt(Matratze, TimeSpan.FromSeconds(62), 5, 5));
        Assert.Equal(HinweisOben.Stufe.Aus, h.Jetzt);
        Assert.True(h.Takt("Tür\nAntippen: hinausgehen.", TimeSpan.FromSeconds(63), 5, 5));
        Assert.Equal(HinweisOben.Stufe.Voll, h.Jetzt);
        Assert.Equal(0.5, HinweisOben.TitelDeckkraft);
        Assert.Equal(TimeSpan.FromSeconds(6), HinweisOben.VollDauer);
    }

    [Fact]
    public void Tippen_OeffnetGanz_NochmalZumTitel_WeitergehenSchliesst()
    {
        var h = new HinweisOben();
        Assert.False(h.Tippen(0, 0));   // ohne Text nichts
        h.Takt(Matratze, TimeSpan.Zero, 1, 1);
        Assert.True(h.Tippen(1, 1));
        Assert.Equal(HinweisOben.Stufe.Offen, h.Jetzt);
        // offen bleibt offen, auch nach den 6 s
        h.Takt(Matratze, TimeSpan.FromSeconds(30), 1, 1);
        Assert.Equal(HinweisOben.Stufe.Offen, h.Jetzt);
        Assert.True(h.Tippen(1, 1));
        Assert.Equal(HinweisOben.Stufe.Titel, h.Jetzt);
        Assert.True(h.Tippen(1, 1));
        Assert.Equal(HinweisOben.Stufe.Offen, h.Jetzt);
        h.Takt(Matratze, TimeSpan.FromSeconds(31), 1.5f, 1);
        Assert.Equal(HinweisOben.Stufe.Aus, h.Jetzt);
        // weg: Tippen (Taste H) holt ihn ganz zurueck
        Assert.True(h.Tippen(1.5f, 1));
        Assert.Equal(HinweisOben.Stufe.Offen, h.Jetzt);
        // leerer Text: aus
        Assert.False(h.Takt("", TimeSpan.FromSeconds(32), 1.5f, 1));
        Assert.Equal(HinweisOben.Stufe.Aus, h.Jetzt);
        Assert.False(h.Tippen(1.5f, 1));
    }

    [Fact]
    public void Teile_TitelUndRest_KurzUndEineZeile()
    {
        Assert.Equal(("Matratze", "Hier schläfst du.\nAntippen: hinlegen."), HinweisOben.Teile(Matratze));
        Assert.Equal("Matratze", HinweisOben.Kurz(Matratze));
        Assert.Equal("Hier schläfst du. Antippen: hinlegen.", HinweisOben.RestInEinerZeile(Matratze));
        // ohne Titel: der erste Satz
        Assert.Equal(("", "Du bist im Freien. Tippe auf ein Ziel."), HinweisOben.Teile("Du bist im Freien. Tippe auf ein Ziel."));
        Assert.Equal("Du bist im Freien.", HinweisOben.Kurz("Du bist im Freien. Tippe auf ein Ziel."));
        Assert.Equal("Ein Satz", HinweisOben.Kurz("Ein Satz"));
    }

    /// <summary>B-3: jeder feste Text der Spieltexte ist aufgeklappt ganz zu lesen (Titel und Rest ergeben denselben Text).</summary>
    [Fact]
    public void JederSpieltext_AufgeklapptGanz()
    {
        var texte = new[] { typeof(SpielProbeTexte), typeof(StadtTexte) }
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => f.FieldType == typeof(string) && (f.IsLiteral || f.IsInitOnly))
            .Select(f => (string?)f.GetValue(null) ?? "")
            .Where(s => s.Trim().Length > 0)
            .Append(Matratze)
            .ToList();
        Assert.True(texte.Count > 100, $"nur {texte.Count} Texte");
        string Ohne(string s) => string.Concat(s.Where(c => !char.IsWhiteSpace(c)));
        foreach (var text in texte)
        {
            var h = new HinweisOben();
            h.Takt(text, TimeSpan.Zero, 0, 0);
            h.Tippen(0, 0);
            Assert.Equal(HinweisOben.Stufe.Offen, h.Jetzt);
            var (titel, inhalt) = HinweisOben.Teile(h.Text);
            Assert.Equal(Ohne(text), Ohne(titel + inhalt));
            Assert.False(string.IsNullOrWhiteSpace(HinweisOben.Kurz(text)) && text.Trim().Length > 0 && inhalt.Length == 0 && titel.Length == 0, text);
        }
    }

    [Fact]
    public void TasteH_Standard_OhneDoppelbelegung()
    {
        var b = new Tastenbelegung();
        Assert.Equal(["H"], b.Tasten(Tastenbelegung.Hinweis));
        Assert.Contains(Tastenbelegung.Hinweis, Tastenbelegung.NeuAb080);
        Assert.Contains(Tastenbelegung.Aufgaben, a => a.Id == Tastenbelegung.Hinweis);
        var doppelt = Tastenbelegung.Aufgaben.SelectMany(a => b.Tasten(a.Id).Select(t => (t, a.Id))).GroupBy(x => x.t).Where(g => g.Count() > 1 && g.Key == "H").ToList();
        Assert.Empty(doppelt);
        // wer H schon fuer etwas anderes gewaehlt hat, behaelt es; der Hinweis hat dann keine Taste (wie bei F und R, A 0.80.0)
        var alt = Tastenbelegung.Lesen(System.Text.Json.Nodes.JsonNode.Parse("{\"tasten\":{\"vollbild\":\"H\"}}")!.AsObject());
        Assert.Equal(Tastenbelegung.Vollbild, alt.AufgabeFuer("H"));
        Assert.Equal("keine Taste", alt.AnzeigeFuer(Tastenbelegung.Hinweis));
    }
}
