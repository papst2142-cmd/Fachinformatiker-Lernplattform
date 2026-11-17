using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Sammelliste 375 (S 0.57.0, Weisung P12-297 C): die Wahl „Stadtteil einsetzen: Projekt“ geht nach einem gelungenen
/// Einsetzen und beim Oeffnen eines anderen Projekts auf „Kein Projekt“ zurueck; Tippen auf die Karte waehlt danach wieder den
/// Platz der heutigen Stadt. Gegenprobe: mit dem Stand S 0.56.2 (<see cref="Hauptansicht.EinsetzenWahlAltFuerTests"/>) bleibt
/// die Wahl beim Projektwechsel stehen (Lauf 1051).
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task Platz375Async(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest 375 Quelle", null, (64, 64));
        await a.AussenGeprueft;
        var quelle = a.Projekt!;
        PlatzSetzen(quelle, AussenArt.Baum, 10, 40);
        quelle.Speichern(true);

        a.NeuAnlegen("Starttest 375", null, (64, 64));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        p.AussenAendern("Strasse", x => Aussen.Landschaft(x).Add(PlatzLinie("strasse", (20, 20), (44, 20))));
        await BildAsync(a);
        a.PlaetzeUmschalten();
        await BildAsync(a);
        Waehlen(a, Texte.NeueBreite, 128);
        await BildAsync(a);
        Waehlen(a, Texte.HeutigeStadtAuf, 1);
        await BildAsync(a);
        if (!Aufrufen(a, Texte.StadtUebernehmenKnopf))
        {
            Fund("375: Knopf „Übernehmen und speichern“ fehlt oder gesperrt");
        }
        await BildAsync(a);

        // Stadtteil waehlen, Platz 2 mit der Maus, einsetzen
        Waehlen(a, Texte.EinsetzenProjekt, quelle.Datei);
        await BildAsync(a);
        var gewaehlt = a.EinsetzenDatei;
        var karte = a.Karte;
        var maus = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var punkt2 = karte.BildPunkt(96, 32);
        Maus(karte, maus, punkt2, RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        Maus(karte, maus, punkt2, RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased, los: true);
        await BildAsync(a);
        var schritte = p.SchritteZurueck;
        if (!Aufrufen(a, Texte.Einsetzen))
        {
            Fund("375: Knopf „Einsetzen“ fehlt oder gesperrt");
        }
        await BildAsync(a);
        await BildAsync(a);
        var nachEinsetzen = a.EinsetzenDatei;
        var auswahl = a.PlaetzeElement(Texte.EinsetzenProjekt) is ComboBox { SelectedItem: ComboBoxItem { Tag: var t } } ? t : "fehlt";
        // danach waehlt der Finger den Platz der heutigen Stadt (Platz 2 im Raster 128 × 64)
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        Druck(karte, finger, punkt2);
        Los(karte, finger, punkt2);
        await BildAsync(a);
        var stadtPlatz = a.StadtPlan?.Platz;
        Messwerte.Notiere("375: gewählt " + Path.GetFileName(gewaehlt) + ", Schritte " + schritte + " -> " + p.SchritteZurueck
            + ", nach dem Einsetzen " + (nachEinsetzen ?? "kein Projekt") + ", Auswahl " + (auswahl ?? "kein Projekt") + ", Tippen danach: Stadt auf Platz " + stadtPlatz);
        if (gewaehlt != quelle.Datei || p.SchritteZurueck != schritte + 1 || nachEinsetzen is not null || auswahl is not null || stadtPlatz != 2)
        {
            Fund("375: nach dem Einsetzen bleibt der Stadtteil gewählt oder Tippen wählt nicht den Platz der Stadt");
        }

        // Projektwechsel: Wahl geht zurueck (Gegenprobe S 0.56.2: sie bleibt stehen)
        foreach (var alt in new[] { false, true })
        {
            Hauptansicht.EinsetzenWahlAltFuerTests = alt;
            try
            {
                if (!karte.PlaetzeAn)
                {
                    a.PlaetzeUmschalten();
                    await BildAsync(a);
                }
                Waehlen(a, Texte.EinsetzenProjekt, quelle.Datei);
                await BildAsync(a);
                var vorher = a.EinsetzenDatei;
                a.NeuAnlegen("Starttest 375 " + (alt ? "Gegenprobe" : "Wechsel"), null, (64, 64));
                await a.AussenGeprueft;
                a.ReiterZeigen(innen: false);
                await BildAsync(a);
                if (!karte.PlaetzeAn)
                {
                    a.PlaetzeUmschalten();
                    await BildAsync(a);
                }
                var nachher = a.EinsetzenDatei;
                var anzeige = a.PlaetzeElement(Texte.EinsetzenProjekt) is ComboBox { SelectedItem: ComboBoxItem { Tag: var z } } ? z : "fehlt";
                Messwerte.Notiere("375 " + (alt ? "Gegenprobe S 0.56.2" : "Projektwechsel") + ": vorher " + Path.GetFileName(vorher)
                    + ", nach dem Wechsel " + (nachher ?? "kein Projekt") + ", Auswahl " + (anzeige is string s ? Path.GetFileName(s) : anzeige ?? "kein Projekt"));
                if (vorher != quelle.Datei)
                {
                    Fund("375: Stadtteil lässt sich nicht wählen");
                }
                else if (!alt && (nachher is not null || anzeige is not null))
                {
                    Fund("375: nach dem Öffnen eines anderen Projekts bleibt der Stadtteil gewählt");
                }
                else if (alt && nachher is null)
                {
                    Fund("375: Gegenprobe zeigt den Fund nicht (mit dem Stand S 0.56.2 bleibt die Wahl stehen)");
                }
            }
            finally
            {
                Hauptansicht.EinsetzenWahlAltFuerTests = false;
            }
        }
        // aufraeumen: wie ein Mensch „Kein Projekt“ waehlen, Plaetze aus
        Waehlen(a, Texte.EinsetzenProjekt, null);
        await BildAsync(a);
        if (karte.PlaetzeAn)
        {
            a.PlaetzeUmschalten();
            await BildAsync(a);
        }
    }
}
