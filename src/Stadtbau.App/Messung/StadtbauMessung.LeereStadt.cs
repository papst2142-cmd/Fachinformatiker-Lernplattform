using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Sammelliste 257 (Weisung P12-116): eine leere Stadt ueber „Projekt öffnen“ geoeffnet hat keinen offenen Innenraum; eine
/// Aenderung draussen laeuft danach ohne Fehler (vorher suchte das Werkzeug den Grundriss des leeren Orts "" und schrieb bei
/// jeder Aenderung eine Zeile in fehler.log). Gegenprobe mit dem alten Verhalten: dieselbe Aenderung scheitert.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task LeereStadtOeffnenAsync(Hauptansicht a)
    {
        a.NeuAnlegen("Starttest leere Stadt oeffnen", null, (20, 16));
        await a.AussenGeprueft;
        var datei = a.Projekt!.Datei;
        bool Aendern(double x)
        {
            try
            {
                a.Projekt!.AussenAendern("Baum", aa => AussenBearbeitung.Setzen(aa, AussenArt.Baum, x, 4));
                return true;
            }
            catch (Exception e) when (e is InvalidDataException or KeyNotFoundException)
            {
                return false;
            }
        }

        // Gegenprobe: altes Verhalten (leerer Ort gilt als offener Innenraum)
        Hauptansicht.OrtLeerAltFuerTests = true;
        try
        {
            a.ProjektLadenFuerTests(datei);
            a.ReiterZeigen(innen: false);
            if (a.OffenerOrt is not "" || Aendern(4))
            {
                Fund("Leere Stadt oeffnen: Gegenprobe (altes Verhalten) ohne Fehler, Probe taugt nicht");
            }
        }
        finally
        {
            Hauptansicht.OrtLeerAltFuerTests = false;
        }
        await a.AussenGeprueft;

        var zeilen = File.Exists(a.Ablage.FehlerLog) ? File.ReadAllLines(a.Ablage.FehlerLog).Length : 0;
        a.ProjektLadenFuerTests(datei);
        a.ReiterZeigen(innen: false);
        await a.AussenGeprueft;
        if (a.OffenerOrt is not null)
        {
            Fund("Leere Stadt oeffnen: offener Innenraum \"" + a.OffenerOrt + "\" statt keiner");
        }
        if (!Aendern(8))
        {
            Fund("Leere Stadt oeffnen: eine Aenderung draussen scheitert");
        }
        await a.AussenGeprueft;
        var danach = File.Exists(a.Ablage.FehlerLog) ? File.ReadAllLines(a.Ablage.FehlerLog).Length : 0;
        if (danach != zeilen)
        {
            Fund("Leere Stadt oeffnen: fehler.log hat " + (danach - zeilen) + " neue Zeilen");
        }
    }
}
