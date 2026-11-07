using System.Globalization;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Patch S 0.12.1 (Weisung P12-62, Sammelliste 233, Fund Nico am Tablet): eine Kurve 90 Grad Radius 2 mit dem Finger in der
/// 3D-Ansicht bis knapp an das Ende einer geraden Strasse ziehen (ein Rasterfeld schraeg daneben, 0,71). Sie rastet mit
/// Lage und Richtung ein: Anfang genau auf dem Strassenende, Anschluss wie im Spiel (eine Kette der Mittellinie), kein
/// roter Fund, ein Schritt im Verlauf. Danach die Gerade selbst an die Kurve ziehen (Nicos Foto: vorher ragte sie in den
/// Bogen).
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task EinrastenAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Einrasten", null, (32, 20));
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        AussenStelle? gerade = null, kurve = null;
        p.AussenAendern("Starttest", aa =>
        {
            gerade = AussenBearbeitung.Setzen(aa, AussenArt.Strasse, 15, 12);
            kurve = AussenBearbeitung.Setzen(aa, AussenArt.Kurve, 24, 15, modell: "bogen:90:2");
            return true;
        });
        await a.AussenGeprueft;
        if (gerade is not { } gs || kurve is not { } ks)
        {
            Fund("Einrasten: Gerade oder Kurve nicht gesetzt");
            return;
        }
        a.DreiZeigen();
        await a.DreiFertig;
        var b3 = a.Bedienung3D;
        if (!a.Drei.GlBereit || a.Drei.AktuelleKamera is null)
        {
            Messwerte.Notiere("Einrasten 3D: kein OpenGL auf diesem Rechner, nicht geprueft");
            a.ReiterZeigen(innen: false);
            return;
        }
        b3.GanzeStadt();
        await BildAsync(b3);

        bool Angeschlossen()
        {
            var strassen = AussenBearbeitung.Strassen(p.AussenStand);
            return StadtSzene.MittellinienZuege(strassen).Any(z => z.Strassen.Count == 2);
        }
        Vector2 Anfang(AussenStelle s) => AussenBearbeitung.Strassen(p.AussenStand)[s.Nummer][0];
        Vector2 Ende(AussenStelle s) => AussenBearbeitung.Strassen(p.AussenStand)[s.Nummer][^1];

        // 1: Kurve (Anfang 23; 14) mit dem Finger bis 0,5 / 0,5 neben das Strassenende (17; 12) ziehen
        var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var vorSchritte = p.SchritteZurueck;
        var start = Bild(a, 23.3, 14.05);
        Druck(b3, finger, start);
        if (a.Karte.Auswahl != ks)
        {
            Fund("Einrasten 3D: Antippen waehlt die Kurve nicht (" + a.Karte.Auswahl + ")");
        }
        var ziel = Bild(a, 23.3 - 5.5, 14.05 - 1.5);
        for (var i = 1; i <= 8; i++)
        {
            Bewegung(b3, finger, start + ((ziel - start) * (i / 8.0)));
        }
        Los(b3, finger, ziel);
        await a.DreiFertig;
        await a.AussenGeprueft;
        var anfang = Anfang(ks);
        Messwerte.Notiere(string.Create(CultureInfo.InvariantCulture, $"Einrasten 3D: Kurve gezogen, Anfang {anfang.X:F3}/{anfang.Y:F3}, Schritte +{p.SchritteZurueck - vorSchritte}"));
        if (Vector2.Distance(anfang, new Vector2(17, 12)) > 0.011f)
        {
            Fund(string.Create(CultureInfo.InvariantCulture, $"Einrasten 3D: Kurve rastet nicht am Strassenende ein (Anfang {anfang.X:F2}/{anfang.Y:F2})"));
        }
        if (!Angeschlossen())
        {
            Fund("Einrasten 3D: Kurve und Gerade bilden keinen Anschluss (Mittellinie bricht ab)");
        }
        if (p.SchritteZurueck != vorSchritte + 1)
        {
            Fund("Einrasten 3D: Ziehen der Kurve ist nicht genau ein Schritt");
        }
        if (a.AussenFunde.FirstOrDefault(x => x.Rot) is { } rot)
        {
            Fund("Einrasten 3D: roter Fund nach dem Einrasten: " + rot.Text);
        }

        // Gegenprobe: dasselbe Ziehen mit dem Einrasten vor S 0.12.1 (0,5 ohne Richtung) rastet nicht ein, kein Anschluss
        a.Rueckgaengig();
        await a.DreiFertig;
        AussenBearbeitung.EinrastenAltFuerTests = true;
        try
        {
            Druck(b3, finger, start);
            for (var i = 1; i <= 8; i++)
            {
                Bewegung(b3, finger, start + ((ziel - start) * (i / 8.0)));
            }
            Los(b3, finger, ziel);
            await a.DreiFertig;
            if (Angeschlossen() || Vector2.Distance(Anfang(ks), new Vector2(17, 12)) < 0.011f)
            {
                Fund("Einrasten 3D: Gegenprobe mit dem alten Einrasten rastet auch ein (Probe prueft nichts)");
            }
        }
        finally
        {
            AussenBearbeitung.EinrastenAltFuerTests = false;
        }
        a.Rueckgaengig();
        await a.DreiFertig;
        p.AussenAendern("Starttest", aa =>
        {
            AussenBearbeitung.Verschieben(aa, ks, -5.5, -1.5);
            return true;
        });
        await a.DreiFertig;

        // 2: Nicos Foto: die Gerade von links bis 0,5 in den Bogen ziehen; sie rastet am Kurvenende ein, ragt nicht hinein
        var g0 = Anfang(gs);
        var g1 = Ende(gs);
        p.AussenAendern("Starttest", aa =>
        {
            AussenBearbeitung.Verschieben(aa, gs, -3, 0);
            return true;
        });
        await a.DreiFertig;
        var gStart = Bild(a, ((g0.X + g1.X) / 2) - 3, g0.Y);
        Druck(b3, finger, gStart);
        var gZiel = Bild(a, ((g0.X + g1.X) / 2) + 0.5, g0.Y);
        for (var i = 1; i <= 8; i++)
        {
            Bewegung(b3, finger, gStart + ((gZiel - gStart) * (i / 8.0)));
        }
        Los(b3, finger, gZiel);
        await a.DreiFertig;
        await a.AussenGeprueft;
        var ende = Ende(gs);
        Messwerte.Notiere(string.Create(CultureInfo.InvariantCulture, $"Einrasten 3D: Gerade gezogen, Ende {ende.X:F3}/{ende.Y:F3}"));
        if (Vector2.Distance(ende, Anfang(ks)) > 0.011f || !Angeschlossen())
        {
            Fund(string.Create(CultureInfo.InvariantCulture, $"Einrasten 3D: Gerade ragt in die Kurve (Ende {ende.X:F2}/{ende.Y:F2}, Kurve beginnt {Anfang(ks).X:F2}/{Anfang(ks).Y:F2})"));
        }
        if (a.AussenFunde.FirstOrDefault(x => x.Rot) is { } rot2)
        {
            Fund("Einrasten 3D: roter Fund nach dem Ziehen der Geraden: " + rot2.Text);
        }
        await AlleGroessenAsync(a, f, "Einrasten 3D");
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }
}
