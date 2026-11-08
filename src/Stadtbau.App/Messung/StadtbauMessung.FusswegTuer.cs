using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Messung;

/// <summary>
/// Patch S 0.23.1 (Weisung P12-98, Sammelliste 244, Fund Nico): „Fußweg ziehen“ an die Eingangstuer der Bitweiche in
/// Stadt 1. Draufsicht: Tipp an der Tuer, Tipp am Gehweg bis zum Bild (Messpunkt „Fussweg zur Tuer ziehen (bis zum
/// Bild)“, wiederholbar), dann mit dem Finger in beide Richtungen (zwei Tipps Tuer → Gehweg, ein Zug Gehweg → Tuer), je
/// ohne roten Fund und mit dem Wegende genau an der Tuer; Gegenprobe alter Fangweg (rot „führt durch das Haus“). 3D: zwei
/// Tipps mit dem Finger, gruener Schatten der Maus. Layout PC und Tablet, hell und dunkel, mit Vorschau.
/// </summary>
public static partial class StadtbauMessung
{
    // Bitweiche: Tuer der Daten (14,9; 19,8), Gehwegkante davor bei y 20,44
    private static readonly (double X, double Y) TuerTipp = (15.0, 19.2);
    private static readonly (double X, double Y) GehwegTipp = (14.9, 20.7);

    private static async Task FusswegTuerAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Fussweg Tuer", null);
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var k = a.Karte;
        var tuer = AussenBearbeitung.Fangen(p.AussenStand, ZiehArt.Fussweg, TuerTipp.X, TuerTipp.Y, true).Lage;
        int Wege() => Fusswege.Alle(Objekt(p.AussenStand["stadtdaten"])).Count;
        bool AmTuerEnde() => Fusswege.Alle(Objekt(p.AussenStand["stadtdaten"])) is { Count: > 0 } w
            && (System.Numerics.Vector2.Distance(w[^1].Punkte[0], tuer) < 1e-3f || System.Numerics.Vector2.Distance(w[^1].Punkte[^1], tuer) < 1e-3f);
        void OhneRot(string wo)
        {
            // Stadt 1 hat schon den bekannten roten Fund Laterne 20 (Sammelliste 220); hier zaehlen nur Funde zu Fusswegen
            if (a.AussenFunde.FirstOrDefault(x => x.Rot && x.Text.Contains("Fußweg", StringComparison.Ordinal)) is { } rot)
            {
                Fund("Fussweg zur Tuer: roter Fund " + wo + ": " + rot.Text);
            }
        }

        // 1: Messpunkt, zwei Tipps in der Draufsicht (Tuer, Gehweg)
        await MessenAsync("Fussweg zur Tuer ziehen (bis zum Bild)", k, () =>
        {
            ZiehWaehlen(a, 1);
            k.ZiehAnfangSetzen(TuerTipp.X, TuerTipp.Y);
            if (k.ZiehenBis(GehwegTipp.X, GehwegTipp.Y) is null)
            {
                Fund("Fussweg zur Tuer: nicht gezogen (" + k.ZiehGrund + ")");
            }
            return Task.CompletedTask;
        }, () =>
        {
            a.Rueckgaengig();
            return Task.CompletedTask;
        });
        a.Rueckgaengig(); // die gemessene Verbindung wieder weg
        await a.AussenGeprueft;

        // 2: Finger, Tuer -> Gehweg mit zwei Tipps
        ZiehWaehlen(a, 1);
        FingerTipp(k, k.BildPunktFuerTests(TuerTipp.X, TuerTipp.Y));
        FingerTipp(k, k.BildPunktFuerTests(GehwegTipp.X, GehwegTipp.Y));
        await a.AussenGeprueft;
        if (Wege() != 1 || !AmTuerEnde())
        {
            Fund(string.Create(CultureInfo.InvariantCulture, $"Fussweg zur Tuer: zwei Tipps Tuer -> Gehweg ergeben {Wege()} Wege, Ende an der Tuer: {AmTuerEnde()}"));
        }
        OhneRot("Tuer -> Gehweg");
        a.Rueckgaengig();
        await a.AussenGeprueft;

        // 3: Finger, Gehweg -> Tuer in einem Zug
        ZiehWaehlen(a, 1);
        FingerZug(k, k.BildPunktFuerTests(GehwegTipp.X, GehwegTipp.Y), k.BildPunktFuerTests(TuerTipp.X, TuerTipp.Y));
        await a.AussenGeprueft;
        if (Wege() != 1 || !AmTuerEnde())
        {
            Fund(string.Create(CultureInfo.InvariantCulture, $"Fussweg zur Tuer: Finger-Zug Gehweg -> Tuer ergibt {Wege()} Wege, Ende an der Tuer: {AmTuerEnde()}"));
        }
        OhneRot("Gehweg -> Tuer");
        a.Rueckgaengig();
        await a.AussenGeprueft;

        // Gegenprobe: alter Fangweg ohne Tuer, der Weg beginnt im Haus und ist rot
        AussenBearbeitung.TuerFangenAltFuerTests = true;
        try
        {
            ZiehWaehlen(a, 1);
            k.ZiehAnfangSetzen(TuerTipp.X, TuerTipp.Y);
            k.ZiehenBis(GehwegTipp.X, GehwegTipp.Y);
            await a.AussenGeprueft;
            if (!a.AussenFunde.Any(x => x.Rot && x.Text.Contains("durch das Haus", StringComparison.Ordinal)))
            {
                Fund("Fussweg zur Tuer: Gegenprobe alter Fangweg ist nicht rot");
            }
            if (Wege() > 0)
            {
                a.Rueckgaengig();
                await a.AussenGeprueft;
            }
        }
        finally
        {
            AussenBearbeitung.TuerFangenAltFuerTests = false;
        }

        // Layout mit Vorschau
        ZiehWaehlen(a, 1);
        k.ZiehAnfangSetzen(TuerTipp.X, TuerTipp.Y);
        k.ZiehZielSetzen(GehwegTipp.X, GehwegTipp.Y);
        if (k.ZiehGrund is not null)
        {
            Fund("Fussweg zur Tuer: Vorschau rot (" + k.ZiehGrund + ")");
        }
        await AlleGroessenAsync(a, f, "Fussweg zur Tuer Vorschau");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        k.ZiehAbbrechen();
        a.SetzenBeenden();

        // 3D: zwei Tipps mit dem Finger, gruener Schatten der Maus
        a.DreiZeigen();
        await a.DreiFertig;
        var b3 = a.Bedienung3D;
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Fussweg zur Tuer 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
            a.ReiterZeigen(innen: false);
            return;
        }
        b3.GanzeStadt();
        await BildAsync(b3);
        // in 3D vor der Tuer tippen (die Fassade liegt sonst vor dem Boden): naeher an der Tuer als an der Gehwegkante
        ZiehWaehlen(a, 1);
        FingerTipp(b3, Bild(a, 14.9, 19.95));
        var maus = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        Bewegung(b3, maus, Bild(a, GehwegTipp.X, GehwegTipp.Y));
        var gruen = await GeprueftAsync(b3);
        if (!b3.SchattenDa || gruen != true)
        {
            Fund("Fussweg zur Tuer 3D: kein gruener Schatten von der Tuer bis zur Maus (" + b3.SchattenText + ")");
        }
        await AlleGroessenAsync(a, f, "Fussweg zur Tuer 3D Vorschau");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        FingerTipp(b3, Bild(a, GehwegTipp.X, GehwegTipp.Y));
        await a.DreiFertig;
        await a.AussenGeprueft;
        if (Wege() != 1 || !AmTuerEnde())
        {
            Fund(string.Create(CultureInfo.InvariantCulture, $"Fussweg zur Tuer 3D: zwei Tipps ergeben {Wege()} Wege, Ende an der Tuer: {AmTuerEnde()}"));
        }
        OhneRot("3D");
        a.SetzenBeenden();
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
        Messwerte.Notiere(string.Create(CultureInfo.InvariantCulture, $"Fussweg zur Tuer: fertig, {Wege()} Fussweg"));
    }
}
