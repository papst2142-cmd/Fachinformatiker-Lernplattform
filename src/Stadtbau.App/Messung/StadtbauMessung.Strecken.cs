using System.Numerics;
using Avalonia.Controls;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle K3b (S 0.12.0, Weisungen P12-59/P12-61): automatische Autostrecken in einer leeren Stadt mit Kreisverkehr.
/// Messpunkt „Strecken anlegen“ (wiederholbar, 400 ms): Netz, Rundwege und Verkehr aus den Strassen der Stadt, wie die
/// 3D-Ansicht sie nach einer Aenderung im Hintergrund baut. Geprueft: Autos da, im Ring gegen den Uhrzeigersinn (Insel
/// links), kein roter Fund; die 3D-Ansicht zeigt dieselben Autos und sie fahren; Gegenprobe: eine Sackgasse, zu kurz zum
/// Wenden, gibt rot.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task StreckenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Strecken", null, (32, 20));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        KreisWaehlen(a, 1);
        if (a.Karte.SetzenAn(12, 10) is null)
        {
            Fund("Strecken: Kreisverkehr nicht gesetzt");
            return;
        }
        a.Karte.SetzenBeenden();
        await a.AussenGeprueft;
        if (a.AussenFunde.Any(x => x.Rot))
        {
            Fund("Strecken: Kreisverkehr mit rotem Fund: " + string.Join(" | ", a.AussenFunde.Where(x => x.Rot).Select(x => x.Text)));
        }
        var (stand, daten) = (p.Stand(), p.Daten);
        var szene = await Task.Run(() => Ansicht3D.Bauen(stand, daten, Ansicht3D.Einstellung.Standard).Szene);
        Verkehr? verkehr = null;
        await MessenAsync("Strecken anlegen", a, async () =>
        {
            verkehr = await Task.Run(() => Verkehr.Fuer(szene));
        });
        if (verkehr is null || verkehr.Autos.Count == 0 || !Autostrecken.Automatisch(szene))
        {
            Fund("Strecken: keine automatischen Autos auf den Strassen");
            return;
        }
        var (m, r) = verkehr.Ringe.Count == 1 ? verkehr.Ringe[0] : (Vector2.Zero, 0f);
        var falsch = 0;
        var imRing = 0;
        foreach (var auto in verkehr.Autos)
        {
            for (var i = 1; i < auto.Strecke.Count; i++)
            {
                var o = Vector2.Lerp(auto.Strecke[i - 1], auto.Strecke[i], 0.5f);
                var d = Vector2.Normalize(auto.Strecke[i] - auto.Strecke[i - 1]);
                if (r > 0 && MathF.Abs(Vector2.Distance(o, m) - r) < 0.2f && Vector2.Distance(o, m) < r + 0.25f)
                {
                    imRing++;
                    falsch += Vector2.Dot(Autostrecken.Rechts(d), o - m) > 0 ? 0 : 1;
                }
            }
        }
        Messwerte.Notiere(string.Create(Inv, $"Strecken: {verkehr.Autos.Count} Autos, {verkehr.Kreuzungen.Count} Kreuzungsfelder, {imRing} Stuecke im Ring, {falsch} in falscher Richtung"));
        if (imRing == 0 || falsch > 0)
        {
            Fund(string.Create(Inv, $"Strecken: im Ring {imRing} Stuecke, davon {falsch} im Uhrzeigersinn"));
        }

        // 3D: dieselben Autos, sie fahren
        a.DreiZeigen();
        await a.DreiFertig;
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Strecken 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
        }
        else
        {
            await BildAsync(a.Drei);
            var v3 = a.Drei.Verkehr;
            var vorher = v3?.Autos.Select(x => x.Weg).ToList() ?? [];
            for (var i = 0; i < 15; i++)
            {
                a.Drei.AnfordernBild();
                await Task.Delay(100);
            }
            if (v3 is null || v3.Autos.Count != verkehr.Autos.Count || !v3.Rundweg)
            {
                Fund(string.Create(Inv, $"Strecken 3D: {v3?.Autos.Count ?? 0} Autos statt {verkehr.Autos.Count}"));
            }
            else if (v3.Autos.Zip(vorher).All(z => z.First.Weg == z.Second))
            {
                Fund("Strecken 3D: die Autos fahren nicht");
            }
            await AlleGroessenAsync(a, f, "Strecken 3D");
            a.ReiterZeigen(innen: false);
        }

        // Gegenprobe: Sackgasse zu kurz zum Wenden = rot
        p.AussenAendern("Starttest kurze Sackgasse", aa => Aussen.Landschaft(aa).Add(new PyObjekt
        {
            { "typ", "strasse" }, { "punkte", new List<object?> { new List<object?> { 26.0, 4.0 }, new List<object?> { 27.2, 4.0 } } },
        }));
        await a.AussenGeprueft;
        if (!a.AussenFunde.Any(x => x.Rot && x.Text.StartsWith("Diese Sackgasse ist zu kurz", StringComparison.Ordinal)))
        {
            Fund("Strecken: zu kurze Sackgasse ohne roten Fund");
        }
        a.Rueckgaengig();
        await a.AussenGeprueft;
        await BildAsync(a);
    }
}
