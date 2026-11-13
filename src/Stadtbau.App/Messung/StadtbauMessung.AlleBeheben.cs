using Avalonia.Controls;
using Avalonia.Interactivity;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.52.0 (Weisung P12-231 Punkt 5): Knopf „Alle beheben, die sicher gehen“ oben in der Liste „Stadt – n rot“.
/// Stadt 1 mit zwei roten Funden, die sicher gehen (Laterne 20 auf der Fahrbahn, Fussweg des Amts kurz vor der Strasse
/// abgeschnitten): der Knopf steht da, antwortet sofort mit „Suche sichere Behebungen …“, danach EIN Schritt, beide Funde weg,
/// Statuszeile „2 behoben, m bleiben“; einmal Rueckgaengig = beide wieder rot (Gegenprobe). Ohne roten Fund mit sicherem
/// Vorschlag kein Knopf. Messpunkt „Alle beheben Knopf (bis zum Bild)“; die Suche im Hintergrund steht als Angabe. Layout je
/// Groesse hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    /// <summary>Weg des Amts auf die Tuer und ein kurzes Stueck davor kuerzen (endet vor der Strasse: roter Fund).</summary>
    private static void AmtWegKuerzen(PyObjekt a)
    {
        var z = AussenBearbeitung.Zugang(a, "amt")!;
        var weg = (List<object?>)z["weg"]!;
        var t = (List<object?>)weg[0]!;
        var n = (List<object?>)weg[1]!;
        var (tx, ty) = (Py.F(t[0]), Py.F(t[1]));
        var (dx, dy) = (Py.F(n[0]) - tx, Py.F(n[1]) - ty);
        var l = Math.Max(Math.Sqrt((dx * dx) + (dy * dy)), 1e-6);
        var neu = new PyObjekt
        {
            { "tuer", Py.Kopie(z["tuer"]) },
            { "weg", new List<object?> { Py.Kopie(t), new List<object?> { Math.Round(tx + (dx / l * 0.25), 3), Math.Round(ty + (dy / l * 0.25), 3) } } },
        };
        var sd = Aussen.Stadtdaten(a);
        if (sd["zugaenge_neu"] is not PyObjekt zn)
        {
            zn = [];
            Py.Setzen(sd, "zugaenge_neu", zn);
        }
        Py.Setzen(zn, "amt", neu);
    }

    private static async Task AlleBehebenAsync(Hauptansicht a, Window? f)
    {
        // ohne roten Fund mit sicherem Vorschlag: kein Knopf
        a.NeuAnlegen("Starttest Alle beheben gruen", null);
        a.ReiterZeigen(innen: false);
        await a.AussenGeprueft;
        a.Projekt!.AussenAendern("Laterne 20", aa => ((List<object?>)Aussen.Laternen(aa)[19]!)[1] = 15.16);
        await a.AussenGeprueft;
        await BildAsync(a);
        if (a.AlleBehebenKnopf is not null)
        {
            Fund("Alle beheben: Knopf steht da, obwohl kein roter Fund sicher behoben werden kann");
        }

        a.NeuAnlegen("Starttest Alle beheben", null);
        a.ReiterZeigen(innen: false);
        await a.AussenGeprueft;
        var p = a.Projekt!;
        p.AussenAendern("Weg kaputt", AmtWegKuerzen);
        await a.AussenGeprueft;
        var vorher = a.AussenFunde.Where(x => x.Rot).Select(x => x.Text).Order(StringComparer.Ordinal).ToList();
        if (!vorher.Contains(Laterne20Fund) || !vorher.Any(t => t.Contains("„Amt“ erreicht keine Straße", StringComparison.Ordinal)))
        {
            Fund("Alle beheben: die beiden roten Funde (Laterne 20, Weg des Amts) fehlen: " + string.Join(" | ", vorher));
            return;
        }
        await BildAsync(a);
        if (a.AlleBehebenKnopf is not { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } knopf)
        {
            Fund("Alle beheben: Knopf „" + Texte.AlleBeheben + "“ fehlt oben in der Liste „Stadt“");
            return;
        }
        await AlleGroessenAsync(a, f, "Alle beheben Knopf");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        var schritte = p.Verlauf.Count;
        Task<AlleBeheben.Ergebnis?>? lauf = null;
        await MessenAsync("Alle beheben Knopf (bis zum Bild)", a, async () =>
        {
            lauf = a.AlleBehebenAsync();
            await BildAsync(a);
            if (a.AlleBehebenStatus is not { IsEffectivelyVisible: true, Text: Texte.AlleBehebenSucht } && !lauf.IsCompleted)
            {
                Fund("Alle beheben: nach dem Knopf steht nicht sofort „" + Texte.AlleBehebenSucht + "“");
            }
        }, async () =>
        {
            if (lauf is not null)
            {
                await lauf;
            }
            if (p.Verlauf.Count > schritte)
            {
                p.Rueckgaengig();
            }
            await a.AussenGeprueft;
        });
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var e = lauf is null ? null : await lauf;
        await a.AussenGeprueft;
        await BildAsync(a);
        Messwerte.Notiere(string.Create(Inv, $"Alle beheben: Suche im Hintergrund bis zum Schritt (Angabe) {uhr.Elapsed.TotalMilliseconds:F0} ms nach dem Bild des Knopfs"));
        var nachher = a.AussenFunde.Where(x => x.Rot).Select(x => x.Text).ToList();
        if (e is not { Behoben: 2 } || p.Verlauf.Count != schritte + 1 || nachher.Contains(Laterne20Fund)
            || nachher.Any(t => t.Contains("„Amt“ erreicht keine Straße", StringComparison.Ordinal)))
        {
            Fund($"Alle beheben: nicht 2 behoben in einem Schritt (behoben {e?.Behoben}, Schritte {p.Verlauf.Count - schritte}): " + string.Join(" | ", nachher));
        }
        if (a.AlleBehebenStatus is not { IsEffectivelyVisible: true } st || st.Text != Texte.AlleBehebenErgebnis(2, nachher.Count))
        {
            Fund("Alle beheben: Statuszeile zeigt nicht „" + Texte.AlleBehebenErgebnis(2, nachher.Count) + "“ (" + a.AlleBehebenStatus?.Text + ")");
        }
        if (a.AlleBehebenKnopf is not null)
        {
            Fund("Alle beheben: Knopf bleibt, obwohl nichts mehr sicher behoben werden kann");
        }
        await AlleGroessenAsync(a, f, "Alle beheben Ergebnis");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);

        // Gegenprobe: einmal Rueckgaengig = beide Funde wieder rot, Knopf wieder da, alte Statuszeile weg
        p.Rueckgaengig();
        await a.AussenGeprueft;
        await BildAsync(a);
        var zurueck = a.AussenFunde.Where(x => x.Rot).Select(x => x.Text).Order(StringComparer.Ordinal).ToList();
        if (!zurueck.SequenceEqual(vorher) || a.AlleBehebenKnopf is null || a.AlleBehebenStatus is { IsVisible: true })
        {
            Fund("Alle beheben: nach einem Rueckgaengig nicht wieder beide Funde rot, kein Knopf oder alte Statuszeile");
        }
        Messwerte.Notiere("Alle beheben: Stadt 1 mit Laterne 20 und Weg des Amts, 2 behoben in einem Schritt, Rueckgaengig = beide zurueck, ohne Fund kein Knopf");
    }
}
