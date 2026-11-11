using System.Runtime.InteropServices;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Tests;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Phase 13 Welle 3 (Weisung P12-21, Frage 1 = a): der Teilbau der 3D-Ansicht. Nach jeder Art von Aenderung (setzen,
/// schieben, drehen, loeschen, Rueckgaengig) ist die Szene mit Vorrat (Unveraendertes aus dem letzten Bau) gleich der
/// ganz neu gebauten: gleiche Pruefsumme der Szene (alle Netze Bit fuer Bit, Tueren, Orte, Laternen, Laufraster) und
/// gleiche Listen (Teile, Modelle, Haeuser, Fensterlicht, umgefaerbte Modelle). Gegenprobe: ein absichtlich falsch
/// uebernommener Ort ergibt eine andere Pruefsumme.
/// </summary>
public class TeilbauTests
{
    private static Spieldaten D => Daten.Spiel;

    private static void Gleich(StadtSzene voll, StadtSzene teil)
    {
        Assert.Equal(StadtSzeneHash.Stadt(voll), StadtSzeneHash.Stadt(teil));
        Assert.True(MemoryMarshal.AsBytes(voll.Teile.ToArray().AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(teil.Teile.ToArray().AsSpan())), "Teile");
        Assert.Equal(voll.Stuecke.Select(s => s.Name), teil.Stuecke.Select(s => s.Name));
        Assert.True(voll.Stuecke.Select(s => s.Lage).SequenceEqual(teil.Stuecke.Select(s => s.Lage)), "Lage der Modelle");
        Assert.Equal(voll.Haeuser, teil.Haeuser);
        Assert.Equal(voll.LichtTeile.Order(), teil.LichtTeile.Order());
        Assert.Equal(voll.Umgefaerbt.Keys.Order(), teil.Umgefaerbt.Keys.Order());
        Assert.Equal(voll.Baeume, teil.Baeume);
        Assert.Equal(voll.Signatur, teil.Signatur);
        Assert.Equal(voll.Orte, teil.Orte);
        Assert.Equal(voll.Tueren, teil.Tueren);
        Assert.Equal(voll.Laufraster.AnzahlFrei, teil.Laufraster.AnzahlFrei);
    }

    private static int Pruefen(Projekt p, Ansicht3D.Einstellung e, StadtSzene.Vorrat vorrat)
    {
        var teil = Ansicht3D.Bauen(p.Stand(), D, e, vorrat).Szene;
        var voll = Ansicht3D.Bauen(p.Stand(), D, e).Szene;
        Gleich(voll, teil);
        return vorrat.Uebernommen;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JedeAenderung_TeilbauGleichVollbau(bool grenzen)
    {
        var e = grenzen ? Ansicht3D.Einstellung.Grenzen : Ansicht3D.Einstellung.Standard;
        var p = Projekt.Neu("Teilbau", D, Daten.NeueAblage());
        var vorrat = new StadtSzene.Vorrat();
        Assert.Equal(0, Pruefen(p, e, vorrat));
        // ohne Aenderung: alles uebernommen ausser dem Netz des Stellwerks (Haus ohne Ort: seine Fenster an der Tuer
        // bekommen bei jedem Bau neu umgefaerbte Modelle, die nicht dieselben Objekte sind)
        Assert.True(Pruefen(p, e, vorrat) > 10);
        Assert.Equal(1, vorrat.NeuGebaut);
        var a = p.AussenStand;
        var baum = AussenBearbeitung.Alle(a).First(x => x.Stelle.Art == AussenArt.Baum).Stelle;
        var laterne = AussenBearbeitung.Alle(a).First(x => x.Stelle.Art == AussenArt.Laterne).Stelle;
        var park = AussenBearbeitung.Alle(a).First(x => x.Stelle.Art == AussenArt.Park).Stelle;
        var strasse = AussenBearbeitung.Alle(a).First(x => x.Stelle.Art == AussenArt.Strasse).Stelle;
        var haus = AussenBearbeitung.Alle(a).First(x => x.Stelle.Art == AussenArt.Haus).Stelle;
        var schritte = new List<(string Was, Action<PyObjekt> Tun)>
        {
            ("Baum schieben", x => AussenBearbeitung.Verschieben(x, baum, 0.5, 0)),
            ("Laterne schieben", x => AussenBearbeitung.Verschieben(x, laterne, 0, 0.5)),
            ("Haus schieben", x => AussenBearbeitung.Verschieben(x, haus, 0.5, 0)),
            ("Park drehen", x => AussenBearbeitung.Drehen(x, park)),
            ("Baum setzen", x => AussenBearbeitung.Setzen(x, AussenArt.Baum, 3, 3)),
            ("Laterne setzen", x => AussenBearbeitung.Setzen(x, AussenArt.Laterne, 12, 20.3)),
            ("Strasse drehen", x => AussenBearbeitung.Drehen(x, strasse)),
            ("Haus drehen", x => AussenBearbeitung.Drehen(x, haus)),
            ("Strasse fein drehen", x => AussenBearbeitung.Drehen(x, strasse, 15)),
            ("Baum loeschen", x => AussenBearbeitung.Loeschen(x, baum)),
        };
        foreach (var (was, tun) in schritte)
        {
            p.AussenAendern(was, tun);
            Pruefen(p, e, vorrat);
        }
        // Rueckgaengig bis zum Anfang: am Ende wieder alles wie vorher und alles uebernommen
        while (p.Rueckgaengig())
        {
            Pruefen(p, e, vorrat);
        }
        Assert.True(Pruefen(p, e, vorrat) > 10);
        Assert.Equal(1, vorrat.NeuGebaut);
    }

    [Fact]
    public void QuestgeberDraussen_TeilbauGleichVollbau_NurSeinHausNeu()
    {
        // Welle Q2: ein Questgeber draussen steht im Netz des Hauses seines Orts; setzen und schieben bauen dieses Haus neu
        var p = QuestTests.Stadt1();
        var vorrat = new StadtSzene.Vorrat();
        var e = Ansicht3D.Einstellung.Standard;
        Pruefen(p, e, vorrat);
        var qg = p.QuestgeberNeu();
        p.QuestgeberPlatz(qg, QuestTests.DraussenPlatz(p));
        Pruefen(p, e, vorrat);
        var szene = Ansicht3D.Bauen(p.Stand(), D, e).Szene;
        Assert.Single(szene.QuestgeberJeHaus.Values.SelectMany(l => l));
        Pruefen(p, e, vorrat);
        p.QuestgeberPlatz(qg, QuestTests.DraussenPlatz(p, 0.5));
        var neu = vorrat.NeuGebaut;
        Pruefen(p, e, vorrat);
        // nur das Haus seines Orts (und das Stellwerk, siehe oben) kommt neu
        Assert.True(vorrat.NeuGebaut <= 2, $"neu gebaut {vorrat.NeuGebaut} (vorher {neu})");
        while (p.Rueckgaengig())
        {
            Pruefen(p, e, vorrat);
        }
    }

    [Fact]
    public void Baum_Schieben_BautNurWenigNeu()
    {
        var p = Projekt.Neu("Teilbau Baum", D, Daten.NeueAblage());
        var vorrat = new StadtSzene.Vorrat();
        Pruefen(p, Ansicht3D.Einstellung.Standard, vorrat);
        var baum = AussenBearbeitung.Alle(p.AussenStand).First(x => x.Stelle.Art == AussenArt.Baum).Stelle;
        p.AussenAendern("Baum schieben", x => AussenBearbeitung.Verschieben(x, baum, 0.5, 0));
        var uebernommen = Pruefen(p, Ansicht3D.Einstellung.Standard, vorrat);
        // die Orte und ihre Hausnetze kommen aus dem Vorrat; neu sind nur wenige Teile
        Assert.True(uebernommen > 2 * vorrat.NeuGebaut, $"uebernommen {uebernommen}, neu {vorrat.NeuGebaut}");
    }

    [Fact]
    public void Gegenprobe_FalschUebernommenerOrt_IstAnders()
    {
        var p = Projekt.Neu("Teilbau Gegenprobe", D, Daten.NeueAblage());
        var vorrat = new StadtSzene.Vorrat();
        var e = Ansicht3D.Einstellung.Standard;
        var erster = Ansicht3D.Bauen(p.Stand(), D, e, vorrat).Szene.Orte[0].Id;
        p.AussenAendern("Haus schieben", x => AussenBearbeitung.Verschieben(x, new AussenStelle(AussenArt.Haus, -1, 0, erster), 1, 0));
        vorrat.FalschUebernehmenFuerTests = true;
        var falsch = Ansicht3D.Bauen(p.Stand(), D, e, vorrat).Szene;
        var voll = Ansicht3D.Bauen(p.Stand(), D, e).Szene;
        Assert.NotEqual(StadtSzeneHash.Stadt(voll), StadtSzeneHash.Stadt(falsch));
        // und ohne den Fehler wieder gleich
        vorrat.FalschUebernehmenFuerTests = false;
        Gleich(voll, Ansicht3D.Bauen(p.Stand(), D, e, vorrat).Szene);
    }
}
