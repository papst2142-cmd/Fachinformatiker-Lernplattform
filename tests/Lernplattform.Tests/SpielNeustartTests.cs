using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W6 (Weisung P7-11, Datenverlust R3): das Spiel schreibt ueber die Spielverwaltung
/// (SpielDatenbank, Sperre der Vorgaenge) nur angehaengte Zeilen. Nach jeder Aktion sind alle
/// frueheren Zeilen jeder Tabelle unveraendert, und ausser den Spieltabellen aendert sich nichts.
/// Nach einem Neustart (neue Verwaltung, neue Verbindung zur Datei) ist der Spielstand gleich
/// (Spielstand.Unterschiede leer, wie _same). Das Laden beim Start schreibt nichts.
/// </summary>
public class SpielNeustartTests
{
    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static PyObjekt Aussehen() => new()
    {
        { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" },
    };

    /// <summary>Alle Zeilen aller Tabellen der Datei, je Tabelle nach rowid.</summary>
    private static Dictionary<string, List<string>> Alles(Lernstand lernstand)
    {
        var tabellen = lernstand.Abfragen("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name")
            .Select(z => (string)z[0]!).ToList();
        var alles = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var tabelle in tabellen)
        {
            alles[tabelle] = [.. lernstand.Abfragen("SELECT * FROM \"" + tabelle + "\" ORDER BY rowid")
                .Select(z => string.Join("\u001f", z.Select(w => w is DBNull or null ? "<null>" : Convert.ToString(w, System.Globalization.CultureInfo.InvariantCulture))))];
        }
        return alles;
    }

    /// <summary>Nur angehaengt: jede alte Zeile steht unveraendert am selben Platz; neue Zeilen nur in Spieltabellen.</summary>
    private static void NurAngehaengt(Dictionary<string, List<string>> vorher, Dictionary<string, List<string>> nachher, string schritt)
    {
        Assert.Equal(vorher.Keys.Order(StringComparer.Ordinal), nachher.Keys.Order(StringComparer.Ordinal));
        foreach (var (tabelle, alt) in vorher)
        {
            var neu = nachher[tabelle];
            Assert.True(neu.Count >= alt.Count, schritt + ": Zeilen in " + tabelle + " verschwunden");
            for (var i = 0; i < alt.Count; i++)
            {
                Assert.True(alt[i] == neu[i], schritt + ": Zeile " + i + " in " + tabelle + " geaendert");
            }
            if (neu.Count > alt.Count)
            {
                Assert.True(tabelle is "spiel_ereignisse" or "spiel_bestenliste", schritt + ": neue Zeile in " + tabelle);
            }
        }
    }

    [Fact]
    public void SpielstandNachNeustartGleichUndNurAngehaengt()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var lernstand = new Lernstand(pfad);
        var schreiber = new LernstandSchreiber(lernstand);
        var spiel = new Spielverwaltung(schreiber, "PC", Inhalt, sperre: Vorgaenge.Sperren);
        spiel.NeuerLauf(1);
        spiel.ZumSchreiben().Profil("Nico", Aussehen(), Ereignisse.Normal);

        var schritte = 0;
        void Tun(string name, Action<Spielablauf> aktion)
        {
            var vorher = Alles(lernstand);
            try
            {
                aktion(spiel.ZumSchreiben());
            }
            catch (SpielFehler)
            {
                // abgelehnt: dann darf auch nichts geschrieben sein
                Assert.Equal(vorher.Select(p => (p.Key, p.Value.Count)), Alles(lernstand).Select(p => (p.Key, p.Value.Count)));
            }
            NurAngehaengt(vorher, Alles(lernstand), name);
            schritte++;
        }

        for (var tag = 1; tag <= 4; tag++)
        {
            for (var n = 0; n < 40 && spiel.Stand.OffeneTickets().Count > 0; n++)
            {
                var auftrag = spiel.Stand.OffeneTickets()[0];
                var vorbereitet = spiel.Stand.Vorbereitet(auftrag) ?? auftrag;
                if (n == 1)
                {
                    Tun("Verschieben", a => a.Verschieben(auftrag["id"]));
                    continue;
                }
                var antwort = Bewertung.Loesung(vorbereitet, spiel.Stand.VerfuegbareTeile(vorbereitet), Inhalt) ?? new PyObjekt();
                Tun("Loesen Tag " + tag, a => a.Loesen(auftrag["id"], antwort, tag % 2 == 0));
            }
            Tun("Feierabend Tag " + tag, a => a.Feierabend());
        }
        Tun("Gruenden ohne Voraussetzung", a => a.Gruenden("Zu frueh"));

        // W7 (Weisung P7-12): Einkauf im Moebelhaus und (ohne Firma abgelehnt) im Elektromarkt
        var stadt = new Stadt(Inhalt, SpielStadtTests.Daten.Value);
        var geschaefte = new Geschaefte(Inhalt, SpielGeschaefteTests.Daten.Value);
        var ware = geschaefte.Waren("moebelhaus").Where(w => w.Art == Geschaefte.ArtMoebel).MinBy(w => w.Preis)!;
        var teil = geschaefte.Waren("elektromarkt").First(w => w.Art == Geschaefte.ArtTeil);
        Tun("Kaufen zu Hause", a => a.MoebelKaufen(geschaefte, stadt, "moebelhaus", ware.Id));
        Tun("Gehen Moebelhaus", a => stadt.Gehen(a, "moebelhaus"));
        Tun("Warten Moebelhaus", a => stadt.Warten(a));
        Assert.Equal(StadtGrund.Ok, stadt.Betreten(spiel.Stand));
        Assert.True(spiel.Stand.Geld >= ware.Preis, "nach vier Tagen reicht das Geld fuer " + ware.Id);
        string? stueck = null;
        Tun("Moebel kaufen", a => stueck = a.MoebelKaufen(geschaefte, stadt, "moebelhaus", ware.Id));
        Assert.True(stueck is not null && spiel.Stand.Moebel.ContainsKey(stueck));
        Tun("Gehen Elektromarkt", a => stadt.Gehen(a, "elektromarkt"));
        var vorTeil = spiel.Stand.Verlauf.Count;
        Tun("Teil ohne Firma", a => a.TeilKaufen(geschaefte, stadt, "elektromarkt", teil.Id));
        Assert.Equal(vorTeil, spiel.Stand.Verlauf.Count);
        Tun("Gehen heim", a => stadt.Gehen(a, "zuhause"));
        Assert.True(schritte > 8);
        var stand = spiel.Stand;
        Assert.True(stand.Tag >= 5);

        // Neustart: neue Verbindung, neue Verwaltung; das Laden schreibt nichts
        var vorNeustart = Alles(lernstand);
        var lernstand2 = new Lernstand(pfad);
        var neu = new Spielverwaltung(new LernstandSchreiber(lernstand2), "PC", Inhalt, sperre: Vorgaenge.Sperren);
        neu.Neuladen();
        Assert.Equal(spiel.Lauf, neu.Lauf);
        Assert.Empty(stand.Unterschiede(neu.Stand));
        Assert.True(Spielstand.Gleich(stand.Profil, neu.Stand.Profil));
        Assert.Equal(stand.Geld, neu.Stand.Geld);
        Assert.Equal(stand.Tag, neu.Stand.Tag);
        var nachNeustart = Alles(lernstand2);
        Assert.Equal(vorNeustart.Keys, nachNeustart.Keys);
        foreach (var (tabelle, zeilen) in vorNeustart)
        {
            Assert.Equal(zeilen, nachNeustart[tabelle]);
        }
    }

    /// <summary>
    /// W7b (Weisung P7-13): Bank und Amt mit Firma (Szenario 045 in der Datei). Kredit zu Hause
    /// (abgelehnt), in der Bank aufnehmen und abloesen, Gruenden auf dem Amt (abgelehnt, schon Firma);
    /// nach jedem Schritt nur angehaengt, nach dem Neustart derselbe Spielstand.
    /// </summary>
    [Fact]
    public void BankUndAmtNurAngehaengt()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var lernstand = new Lernstand(pfad);
        var schreiber = new LernstandSchreiber(lernstand);
        var spiel = new Spielverwaltung(schreiber, "PC", Inhalt, sperre: Vorgaenge.Sperren);
        spiel.NeuerLauf(1);
        var speicher = new SpielDatenbank(schreiber, "PC", spiel.Lauf, Vorgaenge.Sperren);
        using (speicher.Sitzung())
        {
            foreach (var e in SpielFingerabdruckTests.Gespielt.Value.Stand.Verlauf)
        {
                Assert.NotNull(speicher.Schreiben(e.Art, PythonJson.Standard(e.Daten)));
            }
        }
        spiel.Neuladen();
        Assert.True(Py.Wahr(spiel.Stand.Firma));

        void Tun(string name, Action<Spielablauf> aktion, bool abgelehnt = false)
        {
            var vorher = Alles(lernstand);
            var fehler = false;
            try
            {
                aktion(spiel.ZumSchreiben());
            }
            catch (SpielFehler)
            {
                fehler = true;
                Assert.Equal(vorher.Select(p => (p.Key, p.Value.Count)), Alles(lernstand).Select(p => (p.Key, p.Value.Count)));
            }
            Assert.True(abgelehnt == fehler, name + (abgelehnt ? ": nicht abgelehnt" : ": abgelehnt"));
            NurAngehaengt(vorher, Alles(lernstand), name);
        }

        var stadt = new Stadt(Inhalt, SpielStadtTests.Daten.Value);
        var paket = Wirtschaft.KreditPakete(spiel.Stand, Inhalt).First(a => !Py.Wahr(a["problem"]));
        Tun("Kredit zu Hause", a => a.BankKreditAufnehmen(stadt, 0L, 0L, paket["paket"]), abgelehnt: true);
        Tun("Gehen Bank", a => stadt.Gehen(a, Schalter.Bank));
        Tun("Kredit vor dem Oeffnen", a => a.BankKreditAufnehmen(stadt, 0L, 0L, paket["paket"]), abgelehnt: true);
        Tun("Warten Bank", a => stadt.Warten(a));
        Tun("Kredit aufnehmen", a => a.BankKreditAufnehmen(stadt, 0L, 0L, paket["paket"]));
        var kredit = spiel.Stand.Verlauf.Last(e => e.Art == Ereignisse.Kredit).Daten["kredit"];
        Tun("Kredit abloesen", a => a.BankKreditAbloesen(stadt, kredit));
        Assert.Equal(kredit, spiel.Stand.Verlauf.Last(e => e.Art == Ereignisse.KreditAbgeloest).Daten["kredit"]);
        Tun("Gehen Amt", a => stadt.Gehen(a, Schalter.Amt));
        Tun("Gruenden mit Firma", a => a.AmtGruenden(stadt, "Noch eine"), abgelehnt: true);
        Tun("Gehen heim", a => stadt.Gehen(a, "zuhause"));
        var stand = spiel.Stand;

        var vorNeustart = Alles(lernstand);
        var lernstand2 = new Lernstand(pfad);
        var neu = new Spielverwaltung(new LernstandSchreiber(lernstand2), "PC", Inhalt, sperre: Vorgaenge.Sperren);
        neu.Neuladen();
        Assert.Equal(spiel.Lauf, neu.Lauf);
        Assert.Empty(stand.Unterschiede(neu.Stand));
        Assert.Equal(stand.Geld, neu.Stand.Geld);
        Assert.Equal(stand.Kredite.Count, neu.Stand.Kredite.Count);
        var nachNeustart = Alles(lernstand2);
        Assert.Equal(vorNeustart.Keys, nachNeustart.Keys);
        foreach (var (tabelle, zeilen) in vorNeustart)
        {
            Assert.Equal(zeilen, nachNeustart[tabelle]);
        }
    }
}
