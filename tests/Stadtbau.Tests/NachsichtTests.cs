using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle A 0.67.0 / S 0.43.0 (Weisung P12-204, E2 und E3 aus der Liste P12-119): EIN Schalter „nachsichtig“ fuer alle
/// Ansichten des Werkzeugs (<see cref="Nachsicht"/>) statt einzelner Schalter fuer Questebene und Laterne; Spiel und Pruefung
/// bleiben streng. Fehler der 3D-Ansicht stehen nur einmal je Ursache in fehler.log (<see cref="Werkzeugordner.FehlerEinmal"/>).
/// </summary>
public class NachsichtTests
{
    private static Spieldaten D => Daten.Spiel;

    [Fact]
    public async Task Schalter_GiltNurImBlock_UndNurImEigenenAblauf()
    {
        Assert.False(Nachsicht.An);
        var los = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // nebenher laufende Arbeit, die vor dem Block begonnen hat, bleibt streng
        var nebenher = Task.Run(async () =>
        {
            await los.Task.ConfigureAwait(false);
            return Nachsicht.An;
        }, TestContext.Current.CancellationToken);
        using (Nachsicht.Werkzeugansicht())
        {
            Assert.True(Nachsicht.An);
            using (Nachsicht.Werkzeugansicht())
            {
                Assert.True(Nachsicht.An);
            }
            Assert.True(Nachsicht.An);
            los.SetResult();
            Assert.False(await nebenher.ConfigureAwait(true));
        }
        Assert.False(Nachsicht.An);
    }

    /// <summary>
    /// Halb fertige Stadt mit zwei Fehlern zugleich: eine Quest mit unbekannter Kennung und eine Laterne ohne freien Platz.
    /// Der eine Schalter laesst die 3D-Ansicht fuer beide bauen; die Pruefung meldet beide rot, das Spiel laedt so nicht.
    /// </summary>
    [Fact]
    public void EinSchalter_QuestUndLaterne_AnsichtBaut_PruefungUndSpielStreng()
    {
        var datei = File.ReadAllBytes(Path.Combine(Daten.Wurzel, "tests", "Stadtbau.Tests", "Testdaten", "nico", "teststadt_S0.40.0.fistadt"));
        var p = Projekt.AusStadtDatei(datei, D, Daten.NeueAblage(), "Nachsicht");
        p.AussenAendern("Schieben", a => AussenBearbeitung.Verschieben(a, new AussenStelle(AussenArt.Laterne, 10), 1, 0.5));
        p.AussenAendern("unbekannt", a =>
        {
            if (QuestDaten.Liste(a).Count == 0)
            {
                Py.Setzen(Aussen.Stadtdaten(a), QuestDaten.Abschnitt, new List<object?>());
            }
            QuestDaten.Liste(a).Add(new PyObjekt { { "id", "gibt-es-nicht" }, { "questgeber", "ulla" } });
        });
        var stand = p.Stand();
        var funde = Projekt.PruefenAussen(stand, D);
        Assert.Contains(funde, f => f.Rot && f.Text.Contains("findet an ihrer Straße keinen freien Platz", StringComparison.Ordinal));
        // das Spiel laedt so nicht (nie still weg)
        Assert.Throws<StadtbauFehler>(() => stand.StadtFuer(stand.MitKarte(D.Inhalt)));
        // die Ansicht baut mit dem einen Schalter (ohne die Laterne, ohne die kaputte Quest)
        var spiel = Ansicht3D.Bauen(stand, D, Ansicht3D.Einstellung.Standard);
        Assert.Equal(stand.Laternen.Count - 1, spiel.Szene.Laternen.Count);
        Assert.False(Nachsicht.An);
        // Gegenprobe: dieselbe Stadt ohne den Schalter baut die Szene nicht (Laterne)
        Stadt stadt;
        using (Nachsicht.Werkzeugansicht())
        {
            stadt = stand.StadtFuer(stand.MitKarte(D.Inhalt));
        }
        Assert.Throws<InvalidOperationException>(() => StadtSzene.Bauen(stadt, StadtbauSpiel.Probestand(stadt.Inhalt).Stand, D.Grafik).Laternen.Count);
    }

    [Fact]
    public void FehlerEinmal_JeUrsache_Gegenprobe()
    {
        var ordner = Daten.NeueAblage();
        static Exception Werfen(string text)
        {
            try
            {
                throw new InvalidOperationException(text);
            }
            catch (InvalidOperationException e)
            {
                return e;
            }
        }
        var a = Werfen("Laterne 10 findet keinen Platz");
        Assert.True(ordner.FehlerEinmal("3D-Ansicht", new AggregateException(a)));
        // dieselbe Ursache bei jeder weiteren Aenderung: nichts mehr geschrieben
        for (var i = 0; i < 5; i++)
        {
            Assert.False(ordner.FehlerEinmal("3D-Ansicht", new AggregateException(Werfen("Laterne 10 findet keinen Platz"))));
        }
        // andere Ursache (Text, Bereich): wieder einmal
        Assert.True(ordner.FehlerEinmal("3D-Ansicht", Werfen("Laterne 11 findet keinen Platz")));
        Assert.True(ordner.FehlerEinmal("Grenzen live", a));
        var zeilen = File.ReadAllText(ordner.FehlerLog);
        Assert.Equal(3, zeilen.Split("InvalidOperationException", StringSplitOptions.None).Length - 1);
        Assert.Contains("Laterne 10 findet keinen Platz", zeilen, StringComparison.Ordinal);
        // Gegenprobe: so wie bis A 0.66.1 stand jede Wiederholung erneut im Log
        Werkzeugordner.JedesMalAltFuerTests = true;
        try
        {
            Assert.True(ordner.FehlerEinmal("3D-Ansicht", a));
            Assert.True(ordner.FehlerEinmal("3D-Ansicht", a));
        }
        finally
        {
            Werkzeugordner.JedesMalAltFuerTests = false;
        }
        Assert.Equal(5, File.ReadAllText(ordner.FehlerLog).Split("InvalidOperationException", StringSplitOptions.None).Length - 1);
    }
}
