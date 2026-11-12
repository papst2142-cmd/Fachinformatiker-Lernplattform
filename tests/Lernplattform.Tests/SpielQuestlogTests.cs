using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Core.Texte;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 0.67.0 (Weisung P12-204, Sammelliste 301): das Questlog im Handy. Nur Anzeige aus vorhandenen Daten: offene
/// Tickets des Tages (Zahl am Knopf), geloeste Auftraege aus dem Verlauf (zuletzt geloest zuerst), Kette ueber „nach“ mit
/// ✓ ● 🔒, Questgeber und Ort (draussen: Ort seines Platzes, zu dem „Hingehen“ fuehrt). Das Questlog schreibt nichts.
/// </summary>
public sealed class SpielQuestlogTests
{
    private const string NeuQuest = "neu-q-1";

    private const string NeuZuordnung = "neu-q-2";

    private static StadtFassung Fassung0 => StadtbauFassungTests.Fassung0.Value;

    private static StadtSpiel MitQuests(out Spielablauf ablauf)
    {
        var inhalt = SpielQuestTests.Angewendet.Value;
        var stadt = new Stadt(inhalt, SpielQuestTests.Stadtdaten()) { Fassung = Fassung0 };
        ablauf = SpielQuestTests.AllesErledigt(inhalt);
        return new StadtSpiel(stadt, ablauf, null, SpielInnenraumTests.Innenraeume.Value);
    }

    [Fact]
    public void Offen_ZahlUndEintrag_MitQuestgeberOrtUndKette()
    {
        var spiel = MitQuests(out var ablauf);
        var zeilen = ablauf.Stand.Verlauf.Count;
        var offen = Questlog.Offen(spiel);
        Assert.Equal(ablauf.Stand.OffeneTickets().Count, Questlog.ZahlOffen(spiel));
        Assert.Equal(offen.Count, Questlog.ZahlOffen(spiel));
        var e = Assert.Single(offen, x => x.Id == NeuQuest);
        Assert.Equal("Kasse startet nicht", e.Titel);
        Assert.Equal("Greta Sommer", e.Questgeber);
        Assert.Equal("Greta ruft an: die Kasse startet nicht.", e.Ticket);
        // ab Tag 1, heute ist Tag 2: nicht „neu heute“
        Assert.False(e.NeuHeute);
        // draussen: Hingehen fuehrt zum Ort ihres Platzes (Talheim)
        Assert.Equal("talheim", e.Ziel);
        Assert.Contains(spiel.Name("talheim"), e.OrtText);
        // Kette: die Zuordnung kommt erst danach
        Assert.Equal([("Kasse startet nicht", Questlog.Stufe.Jetzt), ("Kabel sortieren", Questlog.Stufe.Gesperrt)], e.Kette);
        Assert.Equal("Kette 1 von 2: ● Kasse startet nicht · 🔒 Kabel sortieren", Questlog.KetteText(e));
        Assert.DoesNotContain(offen, x => x.Id == NeuZuordnung);
        Assert.DoesNotContain(Questlog.Erledigte(spiel), x => x.Id == NeuQuest);
        // nur Anzeige: nichts geschrieben
        Assert.Equal(zeilen, ablauf.Stand.Verlauf.Count);
    }

    [Fact]
    public void NachDemLoesen_ErledigtOben_NaechsterSchrittJetzt()
    {
        var spiel = MitQuests(out var ablauf);
        var auftrag = SpielDaten.Auftrag(NeuQuest, ablauf.Stand.Inhalt)!;
        ablauf.Loesen(NeuQuest, Bewertung.Loesung(auftrag, null, ablauf.Stand.Inhalt), false);
        var erledigt = Questlog.Erledigte(spiel);
        Assert.Equal(NeuQuest, erledigt[0].Id);
        Assert.True(erledigt[0].Erledigt);
        Assert.Equal(erledigt.Count, erledigt.Select(x => x.Id).Distinct().Count());
        var naechster = Assert.Single(Questlog.Offen(spiel), x => x.Id == NeuZuordnung);
        Assert.Equal("Kette 2 von 2: ✓ Kasse startet nicht · ● Kabel sortieren", Questlog.KetteText(naechster));
        Assert.Equal("moebelhaus", naechster.Ziel);
        Assert.DoesNotContain(Questlog.Offen(spiel), x => x.Erledigt);
    }

    [Fact]
    public void FrischesSpiel_OffenGleichDenTickets_ErledigtLeer_KeinTextFehlt()
    {
        var inhalt = Fassung0.MitKarte(SpielInhaltTests.Inhalt);
        var stadt = new Stadt(inhalt, Fassung0.Stadtdaten) { Fassung = Fassung0 };
        var ablauf = new Spielablauf(inhalt, new SpielSpeicher(() => "2026-10-01 08:00:00"));
        ablauf.Profil("Probe", new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" } }, Ereignisse.Normal);
        var spiel = new StadtSpiel(stadt, ablauf, null, SpielInnenraumTests.Innenraeume.Value);
        var offen = Questlog.Offen(spiel);
        Assert.NotEmpty(offen);
        Assert.Equal(ablauf.Stand.OffeneTickets().Select(a => Py.Str(a["id"])), offen.Select(e => e.Id));
        Assert.All(offen, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Titel));
            Assert.False(string.IsNullOrWhiteSpace(e.Questgeber));
            Assert.False(string.IsNullOrWhiteSpace(e.OrtText));
            Assert.False(string.IsNullOrWhiteSpace(e.Ticket), e.Id);
            Assert.True(e.NeuHeute, e.Id);
            Assert.NotNull(e.Ziel);
        });
        Assert.Empty(Questlog.Erledigte(spiel));
        Assert.Equal(QuestlogTexte.KeineErledigten, Questlog.LeerText(spiel, erledigt: true));
        Assert.Equal(QuestlogTexte.KeineOffenen, Questlog.LeerText(spiel, erledigt: false));
    }

    [Fact]
    public void KettenDerInhalte_JederSchrittEinmal_ZeichenStimmen()
    {
        var inhalt = Fassung0.MitKarte(SpielInhaltTests.Inhalt);
        var stadt = new Stadt(inhalt, Fassung0.Stadtdaten) { Fassung = Fassung0 };
        var spiel = new StadtSpiel(stadt, SpielQuestTests.AllesErledigt(inhalt), null, SpielInnenraumTests.Innenraeume.Value);
        // alles geloest: jede Kette nur aus ✓
        var erledigt = Questlog.Erledigte(spiel);
        Assert.NotEmpty(erledigt);
        var mitKette = erledigt.Where(e => e.Kette.Count >= 2).ToList();
        Assert.NotEmpty(mitKette);
        Assert.All(mitKette, e =>
        {
            Assert.All(e.Kette, k => Assert.Equal(Questlog.Stufe.Erledigt, k.Stufe));
            Assert.StartsWith("Kette ", Questlog.KetteText(e), StringComparison.Ordinal);
            Assert.DoesNotContain("●", Questlog.KetteText(e), StringComparison.Ordinal);
        });
        Assert.Equal("Erster Satz.", Questlog.ErsterSatz("Erster Satz. Zweiter Satz.\nNoch eine Zeile."));
        Assert.Equal("Nur eine Zeile", Questlog.ErsterSatz(new List<object?> { "Nur eine Zeile", "zweite" }));
        Assert.Equal("", Questlog.ErsterSatz(null));
        Assert.Equal("✓", Questlog.Zeichen(Questlog.Stufe.Erledigt));
        Assert.Equal("●", Questlog.Zeichen(Questlog.Stufe.Jetzt));
        Assert.Equal("🔒", Questlog.Zeichen(Questlog.Stufe.Gesperrt));
    }
}
