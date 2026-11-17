using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.56.0 (Sammelliste 362, Weisung P12-287): „Plätze“ mit Maus, Finger, Tastatur und Screenreader.
/// <list type="bullet">
/// <item>Knopf mit der Maus an; Karte vergroessern ueber die Auswahl (Breite 128, heutige Stadt auf Platz 1) und den Knopf
/// per Automation; gelber Rand-Hinweis fuer die Strasse, die an der Grenze endet.</item>
/// <item>Stadtteil einsetzen: Projekt waehlen, Finger tippt Platz 1 (belegt: Rahmen rot, Grund, „Einsetzen“ gesperrt),
/// Maus tippt Platz 2 (gruen), Pfeiltasten wechseln den Platz, Einsetzen per Automation: die Strasse drueben schliesst an,
/// der gelbe Hinweis ist weg, ein Schritt (Rueckgaengig bringt ihn zurueck).</item>
/// <item>Karte aus vier Plaetzen (Stadt 1 plus dreimal eingesetzt, 128 × 128): Grenzen live je Stadtteil ohne „zu teuer“,
/// 3D laedt nach ohne Loch, alle Groessen.</item>
/// </list>
/// </summary>
public static partial class StadtbauMessung
{
    private static void Waehlen(Hauptansicht a, string name, object? tag)
    {
        // Wie ein Mensch erst aufklappen: lange Listen (ueber 30 Projekte) fuellen sich erst dabei
        if (a.PlaetzeElement(name) is ComboBox auf)
        {
            auf.IsDropDownOpen = true;
            auf.IsDropDownOpen = false;
        }
        if (a.PlaetzeElement(name) is not ComboBox c || c.Items.OfType<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, tag)) is not { } item)
        {
            Fund("Plätze: Auswahl „" + name + "“ ohne Eintrag " + tag);
            return;
        }
        c.SelectedItem = item;
    }

    private static bool Aufrufen(Hauptansicht a, string name)
    {
        if (a.PlaetzeElement(name) is Button { IsEnabled: true } k && ControlAutomationPeer.CreatePeerForElement(k).GetProvider<IInvokeProvider>() is { } aufruf)
        {
            aufruf.Invoke();
            return true;
        }
        return false;
    }

    private static async Task PlaetzeAsync(Hauptansicht a, Window? f)
    {
        // Quelle: leere Stadt 64 × 64 mit einer Strasse vom linken Rand und einem Baum
        a.NeuAnlegen("Starttest Teil", null, (64, 64));
        await a.AussenGeprueft;
        var quelle = a.Projekt!;
        quelle.AussenAendern("Strasse", x => Aussen.Landschaft(x).Add(PlatzLinie("strasse", (0, 20), (20, 20))));
        PlatzSetzen(quelle, AussenArt.Baum, 10, 40);
        quelle.Speichern(true);

        a.NeuAnlegen("Starttest Plätze", null, (64, 64));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var karte = a.Karte;
        p.AussenAendern("Strasse", x => Aussen.Landschaft(x).Add(PlatzLinie("strasse", (40, 20), (64, 20))));
        await BildAsync(a);

        // Maus: Knopf an
        var knopf = a.PlaetzeKnopf;
        var zeigerMaus = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var mitte = new Point(knopf.Bounds.Width / 2, knopf.Bounds.Height / 2);
        Maus(knopf, zeigerMaus, mitte, RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        Maus(knopf, zeigerMaus, mitte, RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased, los: true);
        await BildAsync(a);
        if (!karte.PlaetzeAn || AutomationProperties.GetName(knopf) != Texte.Gewaehlt(Texte.Plaetze) || karte.PlaetzeGezeichnet.Plaetze != 1)
        {
            Fund("Plätze: Knopf mit der Maus nicht an oder Raster nicht gezeichnet (" + karte.PlaetzeGezeichnet + ")");
        }

        // Vergroessern: Auswahl wie mit der Tastatur, Knopf wie ein Screenreader
        Waehlen(a, Texte.NeueBreite, 128);
        await BildAsync(a);
        Waehlen(a, Texte.HeutigeStadtAuf, 1);
        await BildAsync(a);
        var schritte = p.SchritteZurueck;
        if (!Aufrufen(a, Texte.KarteVergroessernKnopf))
        {
            Fund("Plätze: Knopf „Vergrößern“ für den Screenreader fehlt oder gesperrt");
        }
        await BildAsync(a);
        Messwerte.Notiere(string.Create(Inv, $"Plätze: nach Vergrößern {Aussen.Groesse(p.AussenStand)}, Plätze gezeichnet {karte.PlaetzeGezeichnet.Plaetze}"));
        if (Aussen.Groesse(p.AussenStand) != (128, 64) || p.SchritteZurueck != schritte + 1 || karte.PlaetzeGezeichnet.Plaetze != 2)
        {
            Fund("Plätze: Vergrößern auf 128 × 64 nicht als ein Schritt");
        }
        int Rand() => p.PruefenAussen().Count(x => !x.Rot && x.Tipp == Texte.RandOffen);
        // dieselbe Zahl muss in der Fundliste stehen, die der Mensch sieht (Pruefung im Hintergrund)
        async Task<int> RandListe()
        {
            await a.AussenGeprueft;
            return a.AussenFunde.Count(x => !x.Rot && x.Tipp == Texte.RandOffen);
        }
        if (Rand() != 1 || await RandListe() != 1)
        {
            Fund("Plätze: Straße am Rand ohne gelben Hinweis (" + Rand() + ", Liste " + await RandListe() + ")");
        }

        // Stadtteil einsetzen: Projekt waehlen
        Waehlen(a, Texte.EinsetzenProjekt, quelle.Datei);
        await BildAsync(a);
        // Finger tippt Platz 1 (belegt): rot, Grund, Einsetzen gesperrt
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var punkt1 = karte.BildPunkt(32, 50);
        Druck(karte, finger, punkt1);
        Los(karte, finger, punkt1);
        await BildAsync(a);
        if (karte.PlatzGewaehlt != 1 || karte.PlaetzeGezeichnet.Rahmen != "rot" || a.EinsetzenVorschau is not { Geht: false }
            || a.PlaetzeElement(Texte.Einsetzen) is not Button { IsEnabled: false })
        {
            Fund("Plätze: Finger auf belegtem Platz 1 nicht rot oder Einsetzen nicht gesperrt (" + karte.PlaetzeGezeichnet + ")");
        }
        // Maus tippt Platz 2 (frei): gruen
        var maus = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var punkt2 = karte.BildPunkt(96, 50);
        Maus(karte, maus, punkt2, RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        Maus(karte, maus, punkt2, RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased, los: true);
        await BildAsync(a);
        if (karte.PlatzGewaehlt != 2 || karte.PlaetzeGezeichnet.Rahmen != "gruen")
        {
            Fund("Plätze: Maus auf freiem Platz 2 nicht grün (" + karte.PlatzGewaehlt + ", " + karte.PlaetzeGezeichnet + ")");
        }
        // Tastatur: Pfeil links (Platz 1, rot), Pfeil rechts (Platz 2, gruen)
        karte.Focus();
        a.TasteFuerTests(Key.Left);
        await BildAsync(a);
        var links = (karte.PlatzGewaehlt, karte.PlaetzeGezeichnet.Rahmen);
        a.TasteFuerTests(Key.Right);
        await BildAsync(a);
        if (links != (1, "rot") || karte.PlatzGewaehlt != 2 || karte.PlaetzeGezeichnet.Rahmen != "gruen")
        {
            Fund("Plätze: Pfeiltasten wählen den Platz nicht (" + links + ", " + karte.PlatzGewaehlt + ")");
        }
        // Einsetzen per Automation: ein Schritt, die Strasse schliesst an, kein gelber Hinweis mehr
        schritte = p.SchritteZurueck;
        if (!Aufrufen(a, Texte.Einsetzen))
        {
            Fund("Plätze: Knopf „Einsetzen“ für den Screenreader fehlt oder gesperrt");
        }
        await BildAsync(a);
        var baum = AussenBearbeitung.Alle(p.AussenStand).Any(t => t.Stelle.Art == AussenArt.Baum && Math.Abs(t.X - 74) < 0.01 && Math.Abs(t.Y - 40) < 0.01);
        Messwerte.Notiere(string.Create(Inv, $"Plätze: eingesetzt auf Platz 2, Schritte {schritte} -> {p.SchritteZurueck}, Baum bei 74/40: {baum}, gelbe Rand-Hinweise {Rand()}"));
        if (p.SchritteZurueck != schritte + 1 || !baum || Rand() != 0 || await RandListe() != 0)
        {
            Fund("Plätze: Einsetzen nicht als ein Schritt, nicht genau um 64 verschoben oder Rand-Hinweis bleibt");
        }
        // Weisung P12-291 Punkt 2: nach dem Einsetzen ist kein Platz mehr gewaehlt (kein roter Rahmen, keine rote Vorschau)
        if (karte.PlatzGewaehlt is not null || karte.PlaetzeGezeichnet.Rahmen is not null || a.EinsetzenVorschau is not null)
        {
            Fund("Plätze: nach dem Einsetzen bleibt der Platz gewählt (" + karte.PlatzGewaehlt + ", " + karte.PlaetzeGezeichnet.Rahmen + ")");
        }
        a.Rueckgaengig();
        await BildAsync(a);
        if (Rand() != 1 || await RandListe() != 1)
        {
            Fund("Plätze: Rückgängig bringt den Stand vor dem Einsetzen nicht zurück");
        }
        a.TasteFuerTests(Key.P);
        await BildAsync(a);
        if (karte.PlaetzeAn)
        {
            Fund("Plätze: Taste P beendet nicht");
        }

        await VierPlaetzeAsync(a, f);
    }

    private static PyObjekt PlatzLinie(string typ, params (double X, double Y)[] punkte) => new()
    {
        { "typ", typ },
        { "punkte", new List<object?>(punkte.Select(q => (object?)new List<object?> { Bearbeitung.Zahl(q.X), Bearbeitung.Zahl(q.Y) })) },
    };

    private static void PlatzSetzen(Projekt p, AussenArt art, double x, double y) =>
        p.AussenAendern("Setzen", aa => AussenBearbeitung.Setzen(aa, art, x, y));

    /// <summary>Karte aus vier Plaetzen: Stadt 1, vergroessert auf 128 × 128, und dreimal eine zweite Stadt 1 eingesetzt.</summary>
    private static async Task VierPlaetzeAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Stadt Teil", null);
        await a.AussenGeprueft;
        var quelle = a.Projekt!;
        quelle.Speichern(true);
        a.NeuAnlegen("Starttest Vier Plätze", null);
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        var p = a.Projekt!;
        var gruende = new List<string>();
        if (p.KarteVergroessern(128, 128, 1) is { } g0)
        {
            gruende.Add(g0);
        }
        foreach (var platz in new[] { 2, 3, 4 })
        {
            if (p.StadtteilEinsetzen(quelle, platz).Grund is { } g)
            {
                gruende.Add(platz + ": " + g);
            }
        }
        foreach (var g in gruende)
        {
            Fund("Plätze: Karte aus vier Plätzen: " + g);
        }
        a.ProjektSetzen(p, null, innen: false);
        await a.AussenGeprueft;
        await a.GrenzenLiveFertig;
        var stand = a.GrenzenLiveStand;
        Messwerte.Notiere("Plätze: Karte aus vier Plätzen 128 x 128, Grenzen live: " + stand + ", rote Funde " + a.AussenFunde.Count(x => x.Rot));
        if (stand?.Kacheln is not { } kk || kk.Count != Stadtteile.Alle(128, 128).Count() || kk.Any(k => k.Ueber) || stand.Ueber)
        {
            Fund("Plätze: Karte aus vier Plätzen über den Grenzen je Stadtteil (" + stand + ")");
        }
        a.DreiZeigen();
        await a.DreiFertig;
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        for (var i = 0; i < 600 && a.Drei.GlBereit && !a.Drei.NachladenAktiv; i++)
        {
            await a.Drei.NaechstesBildAsync();
        }
        if (a.Drei.GlBereit)
        {
            if (!a.Drei.NachladenAktiv)
            {
                Fund("Plätze: die 3D-Ansicht der Karte aus vier Plätzen lädt nicht nach");
            }
            if (a.Drei.KachelnLoch != 0)
            {
                Fund(string.Create(Inv, $"Plätze: {a.Drei.KachelnLoch} Stadtteile im Bild weder nah noch fern gezeichnet (Loch)"));
            }
        }
        else
        {
            Messwerte.Notiere("Plätze: 3D nicht geprüft (kein OpenGL auf diesem Rechner)");
        }
        await AlleGroessenAsync(a, f, "Vier Plätze 3D");
        a.ReiterZeigen(innen: false);
    }
}
