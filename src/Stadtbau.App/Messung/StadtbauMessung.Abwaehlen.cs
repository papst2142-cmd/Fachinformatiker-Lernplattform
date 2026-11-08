using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Daten;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// S 0.9.1 (Weisung P12-53 Teil A, Nico: "Man kann Strassen oder Fluesse nicht wieder abwaehlen"): Setzen abwaehlen in
/// der Draufsicht und in der 3D-Ansicht, je Art Strasse, Fluss, Teich, Katalog-Stueck und Blueprint. Zweimal den Knopf
/// links antippen und der Knopf "Setzen beenden" beenden das Setzen, ohne etwas zu setzen und ohne Schritt im Verlauf;
/// der gewaehlte Knopf ist sichtbar markiert (Sammelliste 228). Zwei Finger und die rechte Maustaste bewegen im
/// Setzen-Zustand die Ansicht und setzen nichts. Gegenprobe: mit dem Verhalten vor S 0.9.1 waehlt das zweite Antippen
/// erneut, das Setzen bleibt.
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task AbwaehlenAsync(Hauptansicht a, Window? f)
    {
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        a.NeuAnlegen("Starttest Abwaehlen", "moebelhaus");
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        a.BlueprintsZeigen();
        await BildAsync(a);
        var p = a.Projekt!;
        var blueprint = a.Blueprints.FirstOrDefault(e => e.Vorlage is not null)?.Name;
        var katalog = Katalog.Aktiv?.Eintraege.FirstOrDefault(e => e.Art != KatalogArt.Effekt)?.Id;
        string Stand() => PythonJson.Text(p.AussenStand);

        foreach (var drei in new[] { false, true })
        {
            var ansicht = drei ? "3D" : "Draufsicht";
            if (drei)
            {
                a.DreiZeigen();
                await a.DreiFertig;
            }
            await BildAsync(a);
            var arten = new List<(string Name, AussenArt Art, Button? Knopf, Action? Waehlen)>
            {
                (Texte.Strasse, AussenArt.Strasse, Knopf(a, Texte.Strasse), null),
                (Texte.Fluss, AussenArt.Fluss, Knopf(a, Texte.Fluss), null),
                (Texte.Teich, AussenArt.Teich, Knopf(a, Texte.Teich), null),
                (Texte.VollerKatalog, AussenArt.Katalog, null, () => a.AussenSetzenWaehlenFuerTests(AussenArt.Katalog, katalog)),
                ("Blueprint", AussenArt.Blueprint, blueprint is null ? null : Knopf(a, blueprint), null),
            };
            var altFaelle = 0;
            foreach (var (name, art, knopf, waehlen) in arten)
            {
                var fall = "Abwaehlen " + ansicht + " " + name;
                if (knopf is null && waehlen is null)
                {
                    Fund(fall + ": Knopf links fehlt");
                    continue;
                }
                var stand = Stand();
                var schritte = p.SchritteZurueck;
                void NichtsGesetzt(string wie)
                {
                    if (a.Karte.SetzenArt is not null)
                    {
                        Fund(fall + " (" + wie + "): Setzen bleibt gewaehlt");
                    }
                    if (a.SetzenKnopfMarkiert is not null)
                    {
                        Fund(fall + " (" + wie + "): ein Knopf links bleibt markiert");
                    }
                    if (a.SetzenBeendenKnopf.IsVisible)
                    {
                        Fund(fall + " (" + wie + "): Knopf \"" + Texte.SetzenBeenden + "\" bleibt sichtbar");
                    }
                    if (Stand() != stand || p.SchritteZurueck != schritte)
                    {
                        Fund(fall + " (" + wie + "): es wurde etwas gesetzt oder ein Schritt im Verlauf angelegt");
                    }
                }
                void Gewaehlt(string wie)
                {
                    if (a.Karte.SetzenArt != art)
                    {
                        Fund(fall + " (" + wie + "): Setzen nicht gewaehlt");
                    }
                    if (!a.SetzenBeendenKnopf.IsVisible)
                    {
                        Fund(fall + " (" + wie + "): Knopf \"" + Texte.SetzenBeenden + "\" fehlt beim Setzen");
                    }
                    if (!a.AussenAuswahlKnoepfe.Contains(Texte.SetzenBeenden))
                    {
                        Fund(fall + " (" + wie + "): Knopf \"" + Texte.SetzenBeenden + "\" fehlt rechts bei der Auswahl");
                    }
                }

                // 1) zweimal den Knopf links antippen (Katalog-Stuecke kommen aus dem vollen Katalog, ohne Knopf links)
                if (knopf is not null)
                {
                    var flaeche = knopf.Background;
                    a.SetzenKnopfDrueckenFuerTests(knopf);
                    await BildAsync(a);
                    Gewaehlt("zweimal antippen");
                    if (!ReferenceEquals(a.SetzenKnopfMarkiert, knopf) || Equals(knopf.Background, flaeche))
                    {
                        Fund(fall + ": der gewaehlte Knopf ist nicht sichtbar markiert");
                    }
                    if (!drei)
                    {
                        Layout(a, "Setzen gewaehlt " + name);
                    }
                    a.SetzenKnopfDrueckenFuerTests(knopf);
                    await BildAsync(a);
                    NichtsGesetzt("zweimal antippen");
                    if (!Equals(knopf.Background, flaeche))
                    {
                        Fund(fall + ": die Markierung bleibt nach dem Abwaehlen");
                    }
                }

                // 2) Knopf "Setzen beenden"
                if (knopf is not null)
                {
                    a.SetzenKnopfDrueckenFuerTests(knopf);
                }
                else
                {
                    waehlen!();
                }
                await BildAsync(a);
                Gewaehlt("Setzen beenden");
                a.SetzenKnopfDrueckenFuerTests(a.SetzenBeendenKnopf);
                await BildAsync(a);
                NichtsGesetzt("Setzen beenden");

                // Gegenprobe: Verhalten vor S 0.9.1, das zweite Antippen waehlt erneut
                if (knopf is not null)
                {
                    Hauptansicht.AbwaehlenAltFuerTests = true;
                    a.SetzenKnopfDrueckenFuerTests(knopf);
                    a.SetzenKnopfDrueckenFuerTests(knopf);
                    Hauptansicht.AbwaehlenAltFuerTests = false;
                    if (a.Karte.SetzenArt == art)
                    {
                        altFaelle++;
                    }
                    a.SetzenBeenden();
                    await BildAsync(a);
                    NichtsGesetzt("nach der Gegenprobe");
                }
            }
            Messwerte.Notiere(ansicht + " Abwaehlen Gegenprobe (vor S 0.9.1): " + altFaelle.ToString(Inv) + " von " + arten.Count(x => x.Knopf is not null).ToString(Inv) + " Knoepfen bleiben gewaehlt");
            if (altFaelle != arten.Count(x => x.Knopf is not null))
            {
                Fund(ansicht + " Abwaehlen Gegenprobe: die alte Bedienung waehlte ab (Probe taugt nicht)");
            }

            // 3) im Setzen-Zustand bewegen zwei Finger und die rechte Maustaste die Ansicht und setzen nichts
            Control flaecheC = drei ? a.Bedienung3D : a.Karte;
            if (drei && (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null))
            {
                Messwerte.Notiere("Abwaehlen 3D: kein OpenGL auf diesem Rechner, Finger nicht geprueft");
                continue;
            }
            double Zoom() => drei ? 1 / a.Drei.AktuelleKamera!.Abstand : a.Karte.Zoom;
            var stand2 = Stand();
            var schritte2 = p.SchritteZurueck;
            a.SetzenKnopfDrueckenFuerTests(Knopf(a, Texte.Strasse)!);
            await BildAsync(a);
            var vorher = Zoom();
            var start = new Point(flaecheC.Bounds.Width / 2 - 50, flaecheC.Bounds.Height / 2);
            await SpreizenAsync(flaecheC, start, spaeter: true);
            var faktor = Zoom() / vorher;
            Messwerte.Notiere(string.Create(Inv, $"{ansicht} Setzen-Zustand zwei Finger: Zoom x {faktor:F2}"));
            if (faktor < 1.3)
            {
                Fund(string.Create(Inv, $"{ansicht} Setzen-Zustand: zwei Finger bewegen die Ansicht nicht (Zoom x {faktor:F2})"));
            }
            await EinpassenAsync(flaecheC, Zoom, vorher);
            await MausZiehenAsync(flaecheC, start, 60, 20, "rechts");
            await MausZiehenAsync(flaecheC, start, 0, 0, "rechts");
            if (a.Karte.SetzenArt != AussenArt.Strasse)
            {
                Fund(ansicht + " Setzen-Zustand: zwei Finger oder rechte Maustaste beenden das Setzen");
            }
            if (Stand() != stand2 || p.SchritteZurueck != schritte2)
            {
                Fund(ansicht + " Setzen-Zustand: zwei Finger oder rechte Maustaste haben etwas gesetzt");
            }
            a.SetzenBeenden();
            await BildAsync(a);
        }
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }

    /// <summary>Der sichtbare Setzen-Knopf links mit genau diesem Text (Katalog draussen, Wasser, Blueprints).</summary>
    private static Button? Knopf(Hauptansicht a, string text)
    {
        // Welle Q: die Knoepfe stehen in klappbaren Gruppen; erreichbar mit hoechstens einem Tippen, dann Gruppe auf
        var k = a.KnoepfeMit(text).FirstOrDefault(k => a.KlicksBis(k) is 0 or 1 && k.IsEffectivelyEnabled);
        if (k is not null)
        {
            a.GruppeOeffnenFuer(k);
        }
        return k;
    }
}
