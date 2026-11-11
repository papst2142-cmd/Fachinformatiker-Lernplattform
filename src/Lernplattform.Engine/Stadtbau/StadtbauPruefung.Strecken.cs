using System.Numerics;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// Welle K3b (A 0.46.0 / S 0.12.0, Weisungen P12-59/P12-61): die Regeln fuer die automatischen Autostrecken
/// (<see cref="Autostrecken"/>), beide rot:
/// <list type="number">
/// <item>eine Sackgasse ist zu kurz zum Wenden (Stueck zwischen zwei Wendestellen kuerzer als <see cref="Autostrecken.WendeMindest"/>);</item>
/// <item>ein Auto bleibt stecken: in <see cref="Autostrecken.Stecken"/> (90 Sekunden Fahrt, Figur weit weg) steht es
/// laenger als 20 Sekunden am Stueck.</item>
/// </list>
/// Gelb gibt es nicht (E1): jede Sackgasse hat einen Wendeweg, eine Strasse ohne Auto ist kein Fehler.
/// Fassung 0 faehrt auf den festen Strecken (<see cref="Autostrecken.Automatisch"/> ist falsch): hier kein Fund.
/// </summary>
public static partial class StadtbauPruefung
{
    private static void StreckenRegeln(StadtSzene szene, List<StadtbauFund> funde)
    {
        if (!Autostrecken.Automatisch(szene))
        {
            return;
        }
        var ergebnis = Autostrecken.Anlegen(szene.Strassen, tag: Autostrecken.WerkzeugTag);
        foreach (var p in ergebnis.ZuKurz)
        {
            funde.Add(new StadtbauFund("Diese Sackgasse ist zu kurz: ein Auto kann darauf nicht wenden.",
                $"Die Straße auf mindestens {Zf(Autostrecken.WendeMindest)} verlängern oder an eine andere Straße anschließen.", true, p.X, p.Y));
        }
        foreach (var p in Autostrecken.Stecken(Autostrecken.VerkehrAus(ergebnis, null, Verkehr.Abstand)))
        {
            funde.Add(new StadtbauFund("Hier bleibt ein Auto stecken: es wartet länger als 20 Sekunden.",
                "Kreuzungen weiter auseinander bauen oder eine Straße mehr anschließen.", true, p.X, p.Y));
        }
    }
}
