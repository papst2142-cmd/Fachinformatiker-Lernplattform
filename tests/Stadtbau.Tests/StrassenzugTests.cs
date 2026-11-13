using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.51.0 (Weisung P12-241, Kurzplan P12-131 Punkt 3, Nico P12-231 Antwort 5c): Strassenzug-Vorlagen. Abnahmesaetze:
/// (1) drei feste Vorlagen (Wohnstrasse, Hauptstrasse, Landstrasse) mit dem Inhalt des Kurzplans und eigene Vorlagen, die als
/// Datei im Werkzeugordner wie Blueprints gespeichert werden (Formatnummer, Pruefsumme, echte Testdatei Format 1, kaputte und
/// neuere Dateien stehen mit Grund in der Liste); (2) die Ausstattung kommt mit der Strasse in EINEM Schritt (einmal
/// rueckgaengig); (3) die Vorschau ist das Ergebnis; (4) rote Stellen werden ausgelassen und gezaehlt, die Ausstattung bringt
/// nie einen neuen roten Fund der ganzen Pruefung (Gegenprobe ohne Nachpruefung).
/// </summary>
public class StrassenzugTests
{
    private static Spieldaten D => Daten.Spiel;

    public const string Testdatei = TestdateiTests.Ordner + "strassenzug_S0.51.0.fisstrassenzug";

    private static Projekt Leer() => Projekt.Neu("Strassenzug", D, Daten.NeueAblage(), leer: (32, 24));

    private static List<Vector2> Baeume(PyObjekt a) =>
        [.. AussenBearbeitung.Alle(a).Where(z => z.Stelle.Art == AussenArt.Baum).Select(z => new Vector2((float)z.X, (float)z.Y))];

    private static List<Vector2> Zebras(PyObjekt a) =>
        [.. AussenBearbeitung.Alle(a).Where(z => z.Stelle.Art == AussenArt.Zebrastreifen).Select(z => new Vector2((float)z.X, (float)z.Y))];

    private static int Halte(PyObjekt a) => AussenBearbeitung.HaltListe(a, AussenArt.Haltestelle).Count;

    private static List<PyObjekt> Strassen(PyObjekt a) => [.. Aussen.Landschaft(a).OfType<PyObjekt>().Where(l => Py.Gleich(l["typ"], "strasse"))];

    /// <summary>Hauptstrasse waagerecht (2,12)–(30,12) ohne Ausstattung, damit eine Querstrasse dort einmuendet.</summary>
    private static Projekt MitHauptachse()
    {
        var p = Leer();
        p.AussenAendern("Achse", a => AussenBearbeitung.Ziehen(a, ZiehArt.Strasse, 2, 12, 30, 12, true, out _));
        return p;
    }

    // ---- feste Vorlagen ---------------------------------------------------------------------------------

    [Fact]
    public void Feste_DreiVorlagen_InhaltWieKurzplan()
    {
        Assert.Equal(["Wohnstraße", "Hauptstraße", "Landstraße"], Strassenzug.Feste.Select(f => f.Name).ToArray());
        Assert.All(Strassenzug.Feste, f => Assert.True(f.Fest));
        var (w, h, l) = (Strassenzug.Wohnstrasse, Strassenzug.Hauptstrasse, Strassenzug.Landstrasse);
        // Wohnstrasse: Gehweg, Baeume beidseitig alle 3,5 (Laternen macht die Automatik an jeder Strasse)
        Assert.True(w is { Gehweg: true, Baeume: true, Zebrastreifen: false, Haltestelle: false });
        Assert.Equal(3.5, w.BaumAbstand);
        // Hauptstrasse: wie Wohnstrasse, dazu Zebrastreifen an jeder Einmuendung und eine Haltestelle
        Assert.True(h is { Gehweg: true, Baeume: true, Zebrastreifen: true, Haltestelle: true });
        // Landstrasse: ohne Gehweg, Baeume beidseitig
        Assert.True(l is { Gehweg: false, Baeume: true, Zebrastreifen: false, Haltestelle: false });
        Assert.True(Strassenzug.NameFest("hauptstraße"));
        Assert.False(Strassenzug.NameFest("Allee"));
    }

    // ---- Datei ------------------------------------------------------------------------------------------

    private static Strassenzug Allee => new() { Name = "Allee", Gehweg = true, Baeume = true, BaumAbstand = 2.5, Zebrastreifen = true, Haltestelle = false };

    [Fact]
    public void Datei_SchreibenLesen_FormatPruefsumme()
    {
        var daten = Allee.Schreiben();
        var o = (PyObjekt)Py.Lesen(daten)!;
        Assert.Equal(Strassenzug.Felder, o.Select(e => e.Key).ToArray());
        Assert.Equal(Strassenzug.Format, o["format"]);
        Assert.Equal(1L, o["formatversion"]);
        var gelesen = Strassenzug.Lesen(daten);
        Assert.Equal("Allee", gelesen.Name);
        Assert.True(gelesen.GleicheAusstattung(Allee));
        Assert.False(gelesen.Fest);
        // je Feld eine Zeile
        Assert.Equal(Strassenzug.Felder.Length + 2, System.Text.Encoding.UTF8.GetString(daten).TrimEnd('\n').Split('\n').Length);
    }

    /// <summary>Echte Testdatei Format 1 (geschrieben mit S 0.51.0): bleibt lesbar; ohne Werkzeugzeile gleich dem heutigen Schreiben.</summary>
    [Fact]
    public void Testdatei_Format1_LesbarUndGleichDemSchreiben()
    {
        var neu = Allee.Schreiben();
        if (Environment.GetEnvironmentVariable("STADTBAU_TESTDATEI_SCHREIBEN") == "1")
        {
            File.WriteAllBytes(Path.Combine(Daten.Wurzel, Testdatei), neu);
        }
        var alt = Daten.Lesen(Testdatei) ?? throw new FileNotFoundException(Testdatei);
        var z = Strassenzug.Lesen(alt);
        Assert.Equal("Allee", z.Name);
        Assert.True(z.GleicheAusstattung(Allee));
        static PyObjekt Ohne(byte[] d)
        {
            var o = (PyObjekt)Py.Lesen(d)!;
            o.RemoveAll(e => e.Key is "werkzeug" or "pruefsumme");
            return o;
        }
        Assert.Equal(PythonJson.Text(Ohne(neu), true), PythonJson.Text(Ohne(alt), true));
        Assert.Equal(1L, ((PyObjekt)Py.Lesen(alt)!)["formatversion"]);
    }

    [Fact]
    public void Datei_KaputtNeuerVeraendert_StehenMitGrundInDerListe()
    {
        var ablage = Daten.NeueAblage();
        var zeit = new DateTime(2026, 11, 13, 22, 0, 0, DateTimeKind.Local);
        Allee.Speichern(ablage, zeit);
        File.WriteAllText(Path.Combine(ablage.Strassenzuege, "kaputt" + Strassenzug.Endung), "{ nicht json");
        // neuere Formatnummer (mit richtiger Pruefsumme)
        var neuer = (PyObjekt)Py.Lesen(Allee.Schreiben())!;
        neuer.RemoveAll(e => e.Key == "pruefsumme");
        Py.Setzen(neuer, "formatversion", Strassenzug.FormatVersion + 1);
        Py.Setzen(neuer, "name", "Zukunft");
        neuer.Add("pruefsumme", StadtFassung.Pruefsumme(neuer));
        File.WriteAllBytes(Path.Combine(ablage.Strassenzuege, "Zukunft" + Strassenzug.Endung), PythonJson.Utf8(PythonJson.Standard(neuer)));
        // von Hand veraendert (Pruefsumme passt nicht)
        var veraendert = System.Text.Encoding.UTF8.GetString(new Strassenzug { Name = "Gasse", Gehweg = true }.Schreiben()).Replace("\"gehweg\": true", "\"gehweg\": false");
        File.WriteAllText(Path.Combine(ablage.Strassenzuege, "Gasse" + Strassenzug.Endung), veraendert);
        // unbekanntes Feld
        var fremd = (PyObjekt)Py.Lesen(new Strassenzug { Name = "Fremd" }.Schreiben())!;
        fremd.RemoveAll(e => e.Key == "pruefsumme");
        fremd.Add("bank", true);
        fremd.Add("pruefsumme", StadtFassung.Pruefsumme(fremd));
        File.WriteAllBytes(Path.Combine(ablage.Strassenzuege, "Fremd" + Strassenzug.Endung), PythonJson.Utf8(PythonJson.Standard(fremd)));
        // feste Vorlage als Datei: kaputt (Name gesperrt)
        var festName = (PyObjekt)Py.Lesen(new Strassenzug { Name = "Platzhalter" }.Schreiben())!;
        festName.RemoveAll(e => e.Key == "pruefsumme");
        Py.Setzen(festName, "name", "Wohnstraße");
        festName.Add("pruefsumme", StadtFassung.Pruefsumme(festName));
        File.WriteAllBytes(Path.Combine(ablage.Strassenzuege, "Wohnstrasse" + Strassenzug.Endung), PythonJson.Utf8(PythonJson.Standard(festName)));

        var liste = Strassenzug.Alle(ablage);
        Assert.Equal(6, liste.Count);   // nichts verschwindet still
        Assert.Single(liste, e => e.Vorlage is not null);
        Assert.Equal("Allee", liste.Single(e => e.Vorlage is not null).Name);
        Assert.Equal(Texte.ZugKaputt, liste.Single(e => e.Name == "kaputt").Fehler);
        var zukunft = liste.Single(e => e.Name == "Zukunft");
        Assert.True(zukunft.Neuer);
        Assert.Equal(Texte.ZugNeuer, zukunft.Fehler);
        Assert.Equal(Texte.ZugPruefsumme, liste.Single(e => e.Name == "Gasse").Fehler);
        Assert.Contains("„bank“ ist unbekannt", liste.Single(e => e.Name == "Fremd").Fehler);
        Assert.Equal(Texte.ZugKaputt, liste.Single(e => e.Name == "Wohnstrasse").Fehler);
        Assert.All(liste.Where(e => e.Vorlage is null), e => Assert.False(string.IsNullOrEmpty(e.Fehler)));
    }

    [Fact]
    public void Speichern_NameDoppeltOderFest_IstFehler_LoeschenLegtSicherungAn()
    {
        var ablage = Daten.NeueAblage();
        var zeit = new DateTime(2026, 11, 13, 22, 0, 0, DateTimeKind.Local);
        var pfad = Allee.Speichern(ablage, zeit);
        Assert.Equal(Path.Combine(ablage.Strassenzuege, "Allee" + Strassenzug.Endung), pfad);
        Assert.Throws<IOException>(() => new Strassenzug { Name = "allee" }.Speichern(ablage, zeit));
        Assert.Throws<IOException>(() => new Strassenzug { Name = "Landstraße" }.Speichern(ablage, zeit));
        Assert.ThrowsAny<ArgumentException>(() => Strassenzug.Landstrasse.Speichern(ablage, zeit));
        Assert.Single(Strassenzug.SicherungenFuer(ablage, "Allee").Liste());
        Strassenzug.Loeschen(ablage, pfad, zeit);
        Assert.False(File.Exists(pfad));
        Assert.Equal(2, Strassenzug.SicherungenFuer(ablage, "Allee").Liste().Count);
        Assert.Empty(Strassenzug.Alle(ablage));
    }

    // ---- Ziehen mit Ausstattung ---------------------------------------------------------------------------

    [Fact]
    public void Wohnstrasse_GehwegUndBaeumeBeidseitig_EinSchritt_VorschauGleichErgebnis()
    {
        var p = Leer();
        var plan = AussenBearbeitung.StrassenzugPlanen(p.AussenStand, 2, 12, 30, 12, true, Strassenzug.Wohnstrasse, p.ZugPruefen(), TestContext.Current.CancellationToken);
        Assert.True(plan.Gefunden);
        Assert.True(plan.Nachgeprueft);
        Assert.All(plan.Stuecke, s => Assert.Equal(ZugTeil.Baum, s.Teil));
        var frei = plan.Stuecke.Where(s => s.Frei).Select(s => s.Lage).ToList();
        // 28 lang, alle 3,5 je Seite ab 1,75: 8 je Seite
        Assert.Equal(16, plan.Stuecke.Count);
        Assert.Equal(16, frei.Count);
        // beidseitig: 8 oben, 8 unten, gleich weit von der Mitte (neben dem Gehweg)
        var rand = (StadtSzene.StrassenBreite / 2) + AussenBearbeitung.NeuerGehweg + AussenBearbeitung.ZugBaumRand;
        Assert.Equal(8, frei.Count(q => q.Y < 12));
        Assert.Equal(8, frei.Count(q => q.Y > 12));
        Assert.All(frei, q => Assert.InRange(Math.Abs(q.Y - 12), rand - 0.06, rand + 0.06));
        var xs = frei.Where(q => q.Y < 12).Select(q => (double)q.X).Order().ToList();
        Assert.All(xs.Zip(xs.Skip(1)), z => Assert.Equal(3.5, z.Second - z.First, 1));

        var schritte = p.SchritteZurueck;
        var (strasse, neu, benutzt) = p.StrassenzugZiehen(2, 12, 30, 12, true, Strassenzug.Wohnstrasse, plan);
        Assert.NotNull(strasse);
        Assert.Same(plan, benutzt);
        Assert.Equal(schritte + 1, p.SchritteZurueck);   // Strasse, Ausstattung und Laternen: EIN Schritt
        Assert.Equal(16, neu.Count);
        Assert.Equal(frei.OrderBy(q => q.X).ThenBy(q => q.Y), Baeume(p.AussenStand).OrderBy(q => q.X).ThenBy(q => q.Y));
        Assert.All(Strassen(p.AussenStand), l => Assert.Equal(AussenBearbeitung.NeuerGehweg, Py.F(l["gehweg"])));
        Assert.NotEmpty(Aussen.Laternen(p.AussenStand));   // die Automatik stellt Laternen dazu
        Assert.True(p.Rueckgaengig());
        Assert.Empty(Strassen(p.AussenStand));
        Assert.Empty(Baeume(p.AussenStand));
    }

    [Fact]
    public void Landstrasse_OhneGehweg_BaeumeNebenDerFahrbahn_KeinZebra()
    {
        var p = MitHauptachse();
        var (_, neu, plan) = p.StrassenzugZiehen(16, 11.5, 16, 2, true, Strassenzug.Landstrasse);
        Assert.NotEmpty(neu);
        var neueStrasse = Strassen(p.AussenStand).Single(l => Py.F(Liste(Liste(l["punkte"])[0])[0]) == 16 && Py.F(Liste(Liste(l["punkte"])[1])[0]) == 16);
        Assert.False(Py.Hat(neueStrasse, "gehweg"));
        Assert.Empty(Zebras(p.AussenStand));
        Assert.Equal(0, Halte(p.AussenStand));
        var rand = (StadtSzene.StrassenBreite / 2) + AussenBearbeitung.ZugBaumRand;
        Assert.All(Baeume(p.AussenStand), b => Assert.InRange(Math.Abs(b.X - 16), rand - 0.06, rand + 0.06));
        // nahe der Einmuendung kein Baum (ausgelassen und gezaehlt)
        Assert.All(Baeume(p.AussenStand), b => Assert.True(Vector2.Distance(b, new Vector2(16, 12)) >= AussenBearbeitung.ZugKreuzungFrei));
        Assert.Equal(plan.Frei, neu.Count);
    }

    [Fact]
    public void Hauptstrasse_ZebraAnDerEinmuendung_HaltestelleInDerMitte()
    {
        var p = MitHauptachse();
        var (_, neu, plan) = p.StrassenzugZiehen(16, 11.5, 16, 2, true, Strassenzug.Hauptstrasse);
        Assert.True(plan.Gefunden);
        var zebras = Zebras(p.AussenStand);
        var zebra = Assert.Single(zebras);
        // auf der neuen Strasse, ZugZebraAbstand von der Einmuendung
        Assert.Equal(16, zebra.X, 2);
        Assert.Equal(12 - AussenBearbeitung.ZugZebraAbstand, zebra.Y, 2);
        Assert.Equal(1, Halte(p.AussenStand));
        var halt = AussenBearbeitung.HaltestelleVon(p.AussenStand, new AussenStelle(AussenArt.Haltestelle, 0))!;
        Assert.Equal(16, halt.Mitte.X, 2);
        Assert.InRange(halt.Mitte.Y, 6.5, 7.5);   // Mitte des 10 langen Stuecks
        Assert.Equal(plan.Frei, neu.Count);
        // Gegenprobe Laenge: ein kurzes Stueck bekommt keine Haltestelle (rot, gezaehlt)
        var q = MitHauptachse();
        var kurz = AussenBearbeitung.StrassenzugPlanen(q.AussenStand, 16, 11.5, 16, 7, true, Strassenzug.Hauptstrasse, abbruch: TestContext.Current.CancellationToken);
        var h = Assert.Single(kurz.Stuecke, s => s.Teil == ZugTeil.Haltestelle);
        Assert.Equal(Texte.ZugHaltZuKurz, h.Grund);
    }

    /// <summary>Nicos Ziel: die Ausstattung bringt nie einen neuen roten Fund (alle drei Vorlagen, mehrere Lagen).</summary>
    [Theory]
    [InlineData("Wohnstraße")]
    [InlineData("Hauptstraße")]
    [InlineData("Landstraße")]
    public void KeinNeuesRot_DurchDieAusstattung(string name)
    {
        var vorlage = Strassenzug.Feste.Single(f => f.Name == name);
        foreach (var (ax, ay, bx, by) in new[] { (16.0, 11.5, 16.0, 2.0), (2.0, 4.0, 14.0, 4.0), (20.0, 12.5, 28.0, 20.0) })
        {
            var p = MitHauptachse();
            var nurStrasse = Daten.Kopie(p.AussenStand);
            AussenBearbeitung.Ziehen(nurStrasse, ZiehArt.Strasse, ax, ay, bx, by, true, out _);
            var pruefen = p.ZugPruefen();
            var vorher = pruefen(StrasseMitGehweg(nurStrasse, vorlage));
            p.StrassenzugZiehen(ax, ay, bx, by, true, vorlage);
            var nachher = Bauen3D.Pruefen(p.AussenStand, p.Basis, p.Innenraeume(), p.Daten);
            Assert.Null(Bauen3D.NeuRot(vorher, nachher));
        }
    }

    private static PyObjekt StrasseMitGehweg(PyObjekt a, Strassenzug vorlage)
    {
        foreach (var l in Strassen(a).Skip(1))
        {
            if (vorlage.Gehweg)
            {
                Py.Setzen(l, "gehweg", AussenBearbeitung.NeuerGehweg);
            }
            else
            {
                l.RemoveAll(e => e.Key == "gehweg");
            }
        }
        return a;
    }

    /// <summary>
    /// Gegenprobe der Nachpruefung: ein Busch auf dem Gehweg der Querstrasse genau dort, wo der Zebrastreifen hinkaeme (die Regel
    /// „Stueck auf dem Zebrastreifen“ kennen die schnellen Regeln nicht). Ohne Nachpruefung bringt die Ausstattung einen neuen roten Fund, mit Nachpruefung nicht, und das
    /// betroffene Stueck steht mit dem Fund als Grund im Plan.
    /// </summary>
    [Fact]
    public void Nachpruefung_FaengtWasDieSchnellenRegelnNichtKennen()
    {
        Projekt MitBusch()
        {
            var p = MitHauptachse();
            p.AussenAendern("Busch", a => AussenBearbeitung.SetzenUndDrehen(a, AussenArt.Katalog, 16.55, 12 - AussenBearbeitung.ZugZebraAbstand, false, "nature/plant_bush", 0));
            return p;
        }
        var ohne = MitBusch();
        var pruefenOhne = ohne.ZugPruefen();
        var basis = Daten.Kopie(ohne.AussenStand);
        AussenBearbeitung.Ziehen(basis, ZiehArt.Strasse, 16, 11.5, 16, 2, true, out _);
        var vorher = pruefenOhne(StrasseMitGehweg(basis, Strassenzug.Hauptstrasse));
        AussenBearbeitung.ZugOhneNachpruefungFuerTests = true;
        try
        {
            ohne.StrassenzugZiehen(16, 11.5, 16, 2, true, Strassenzug.Hauptstrasse);
        }
        finally
        {
            AussenBearbeitung.ZugOhneNachpruefungFuerTests = false;
        }
        Assert.NotNull(Bauen3D.NeuRot(vorher, Bauen3D.Pruefen(ohne.AussenStand, ohne.Basis, ohne.Innenraeume(), ohne.Daten)));

        var mit = MitBusch();
        var (_, _, plan) = mit.StrassenzugZiehen(16, 11.5, 16, 2, true, Strassenzug.Hauptstrasse);
        Assert.Null(Bauen3D.NeuRot(vorher, Bauen3D.Pruefen(mit.AussenStand, mit.Basis, mit.Innenraeume(), mit.Daten)));
        Assert.Contains(plan.Stuecke, s => s.Grund?.StartsWith(Texte.ZugNachPruefung(""), StringComparison.Ordinal) == true);
    }

    [Fact]
    public void KeineVerbindung_AendertNichts()
    {
        var p = Leer();
        var schritte = p.SchritteZurueck;
        var (s, neu, plan) = p.StrassenzugZiehen(2, 2, 2.1, 2, true, Strassenzug.Hauptstrasse);
        Assert.Null(s);
        Assert.Empty(neu);
        Assert.False(plan.Gefunden);
        Assert.Equal(schritte, p.SchritteZurueck);
    }
}
