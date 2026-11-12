using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// S 0.21.0 „Aussehen tauschen“ (Weisungen P12-87/P12-88): Talheim-Nord bekommt das Stellwerk. Solange das Deko-Stellwerk
/// steht, ist die Vorschau rot mit Grund (Gegenprobe); nach dem Loeschen gruen, „Übernehmen“ tauscht, der Bahnsteig bleibt als
/// eigenes Stueck. Gegenproben: Zuhause nicht tauschbar; Bahnsteig-Stueck aufs Gleis geschoben = rot; geloescht = gelb ohne
/// Rot. „Altes Aussehen zurück“ bringt den Bahnhof wieder. Messpunkt „Aussehen tauschen Vorschau (bis zum Bild)“
/// (wiederholbar, 400 ms). Weisung P12-89: Name des Orts (Gegenprobe doppelt, Rueckgaengig). Layout PC und Tablet, hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    private static AussenStelle OrtStelle(string ort) => new(AussenArt.Haus, -1, 0, ort);

    private static AussenStelle GebaeudeStelle(PyObjekt aussen, string modell) =>
        new(AussenArt.Gebaeude, Aussen.Landschaft(aussen).FindIndex(t => t is PyObjekt g && Py.Gleich(g["typ"], "gebaeude") && Py.Gleich(g["modell"], modell)));

    private static async Task AussehenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Aussehen", null);
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;

        // Gegenprobe: das Zuhause ist nicht tauschbar
        a.Karte.Waehlen(OrtStelle("zuhause"));
        a.AussehenAuf("zuhause");
        if (a.AussehenOffen is not null)
        {
            Fund("Aussehen: das Zuhause laesst sich tauschen (Gegenprobe)");
        }

        // solange das Stellwerk steht: rot mit Grund
        a.Karte.Waehlen(OrtStelle("talheim_nord"));
        a.AussehenAuf("talheim_nord");
        var nr = a.AussehenVorlagen.ToList().FindIndex(v => v.Blueprint.Modell == "stellwerk");
        if (a.AussehenOffen != "talheim_nord" || nr < 0)
        {
            Fund("Aussehen: Liste fuer Talheim-Nord ohne Stellwerk");
            return;
        }
        await a.AussehenWaehlen(nr);
        await BildAsync(a);
        if (a.AussehenErgebnis is not { Grund: { } grund } || grund != Texte.AussehenSchonDa("Stellwerk"))
        {
            Fund("Aussehen: mit stehendem Stellwerk kein Grund (Gegenprobe)");
        }
        await AlleGroessenAsync(a, f, "Aussehen rot");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // Stellwerk loeschen: Liste bleibt offen, das Stellwerk bleibt waehlbar
        p.AussenAendern("Stellwerk weg", aa => AussenBearbeitung.Loeschen(aa, GebaeudeStelle(aa, "stellwerk")));
        await a.AussenGeprueft;
        a.Karte.Waehlen(OrtStelle("talheim_nord"));
        if (a.AussehenOffen is null)
        {
            a.AussehenAuf("talheim_nord");
        }
        nr = a.AussehenVorlagen.ToList().FindIndex(v => v.Blueprint.Modell == "stellwerk");
        if (nr < 0)
        {
            Fund("Aussehen: nach dem Loeschen ist das Stellwerk nicht mehr waehlbar");
            return;
        }
        await MessenAsync("Aussehen tauschen Vorschau (bis zum Bild)", a, () => a.AussehenWaehlen(nr));
        if (a.AussehenErgebnis is not { Grund: null, Rot: null, Fertig: true })
        {
            Fund("Aussehen: Vorschau Stellwerk nicht gruen: " + a.AussehenErgebnis?.Grund + a.AussehenErgebnis?.Rot?.Text);
        }
        await AlleGroessenAsync(a, f, "Aussehen Vorschau");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        if (a.AussehenUebernehmen() is { } nicht)
        {
            Fund("Aussehen: Uebernehmen ging nicht: " + nicht);
            return;
        }
        await a.AussenGeprueft;
        await BildAsync(a);
        var aussen = p.AussenStand;
        if (!Py.Gleich(AussenBearbeitung.Ort(aussen, "talheim_nord")!["modell"], "stellwerk") || GebaeudeStelle(aussen, Bahnsteig.ModellName("talheim_nord")).Nummer < 0)
        {
            Fund("Aussehen: nach Uebernehmen kein Stellwerk an Talheim-Nord oder kein Bahnsteig-Stueck");
        }
        if (a.AussenFunde.Any(x => x.Rot && x.Text != Laterne20Fund))
        {
            Fund("Aussehen: nach dem Tausch rote Funde: " + string.Join(" | ", a.AussenFunde.Where(x => x.Rot && x.Text != Laterne20Fund).Select(x => x.Text)));
        }
        await AlleGroessenAsync(a, f, "Aussehen getauscht");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // Gegenprobe: Bahnsteig aufs Gleis = rot
        var name = Bahnsteig.ModellName("talheim_nord");
        p.AussenAendern("Bahnsteig zum Gleis", aa => AussenBearbeitung.Verschieben(aa, GebaeudeStelle(aa, name), 0, 0.5, raster: false));
        await a.AussenGeprueft;
        if (!a.AussenFunde.Any(x => x.Rot && x.Text.Contains("liegt auf dem Gleis", StringComparison.Ordinal)))
        {
            Fund("Aussehen: Bahnsteig auf dem Gleis ist nicht rot (Gegenprobe)");
        }
        a.Rueckgaengig();
        await a.AussenGeprueft;

        // Bahnsteig geloescht: kein neues Rot; seit Weisung P12-220 ist Talheim-Nord das Stellwerk ohne Bahnsteig, also auch kein gelber Hinweis
        a.Karte.Waehlen(GebaeudeStelle(p.AussenStand, name));
        await BildAsync(a);
        await AlleGroessenAsync(a, f, "Aussehen Bahnsteig gewaehlt");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        p.AussenAendern("Bahnsteig weg", aa => AussenBearbeitung.Loeschen(aa, GebaeudeStelle(aa, name)));
        await a.AussenGeprueft;
        if (a.AussenFunde.Any(x => x.Text.Contains("keinen Bahnsteig", StringComparison.Ordinal)) || a.AussenFunde.Any(x => x.Rot && x.Text != Laterne20Fund))
        {
            Fund("Aussehen: Stellwerk ohne Bahnsteig mit Hinweis oder rote Funde: " + string.Join(" | ", a.AussenFunde.Select(x => x.Text)));
        }
        a.Rueckgaengig();
        await a.AussenGeprueft;

        // „Altes Aussehen zurück“
        a.Karte.Waehlen(OrtStelle("talheim_nord"));
        a.AussehenAuf("talheim_nord");
        nr = a.AussehenVorlagen.ToList().FindIndex(v => v.Zurueck);
        if (nr < 0)
        {
            Fund("Aussehen: „" + Texte.AussehenAltZurueck + "“ fehlt");
            return;
        }
        await a.AussehenWaehlen(nr);
        if (a.AussehenUebernehmen() is { } nicht2)
        {
            Fund("Aussehen: Altes Aussehen zurueck ging nicht: " + nicht2);
        }
        await a.AussenGeprueft;
        if (!Py.Gleich(AussenBearbeitung.Ort(p.AussenStand, "talheim_nord")!["modell"], "bahnhof_neu") || GebaeudeStelle(p.AussenStand, name).Nummer >= 0)
        {
            Fund("Aussehen: Altes Aussehen zurueck bringt den Bahnhof nicht oder das Bahnsteig-Stueck bleibt");
        }
        await BildAsync(a);

        // Weisung P12-89: Name des Orts; Gegenprobe doppelter Name geht nicht
        a.Karte.Waehlen(OrtStelle("talheim_nord"));
        if (a.OrtUmbenennen("talheim_nord", "Bahnhof Talheim") is null)
        {
            Fund("Ortsname: doppelter Name wurde angenommen (Gegenprobe)");
        }
        if (a.OrtUmbenennen("talheim_nord", "Stellwerk Talheim-Nord") is { } nicht3 || p.OrtName("talheim_nord") != "Stellwerk Talheim-Nord")
        {
            Fund("Ortsname: Umbenennen ging nicht");
        }
        await a.AussenGeprueft;
        if (a.AussenFunde.Any(x => x.Rot && x.Text != Laterne20Fund))
        {
            Fund("Ortsname: nach dem Umbenennen rote Funde");
        }
        a.Karte.Waehlen(OrtStelle("talheim_nord"));
        await BildAsync(a);
        await AlleGroessenAsync(a, f, "Ortsname");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        a.Rueckgaengig();
        await a.AussenGeprueft;
        if (p.OrtName("talheim_nord") != "Talheim-Nord")
        {
            Fund("Ortsname: Rueckgaengig bringt den alten Namen nicht");
        }
    }
}
