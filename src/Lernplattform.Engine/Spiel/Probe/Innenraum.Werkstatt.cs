using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 0.99.0 (Weisung P12-317, Sammelliste 355/356, Nico F1 = c): Werkstatt-Teile genauer als der Entwurf P12-194
/// (Serverschrank, PC-Tower, USV; Mainboard, RAM und CPU nur auf der Werkbank). Alles aus Kaesten im Rahmen des Stuecks
/// (Rueckseite lz = 0, Vorderseite lz = t), nichts ragt aus dem Rechteck. Nur Darstellung: Grundriss, Laufraster, Spielstand
/// und Bewertung bleiben gleich. Obergrenzen der Dreiecke je Teil in <see cref="WerkstattGrenzen"/>.
/// </summary>
public sealed partial class Innenraum
{
    /// <summary>Gegenprobe und alte Sollwerte: Serverschrank, USV, PC und Werkbank wie bis A 0.98.0 (ein Kasten mit Streifen).</summary>
    internal static readonly AsyncLocal<bool> WerkstattAltFuerTests = new();

    /// <summary>
    /// Obergrenzen der Dreiecke je Teil (Kaesten, ohne das Tischmodell der Werkbank). Grund: acht Schraenke und vier USV in
    /// der Serverfarm bleiben zusammen unter 45 000 Dreiecken, die ganze Serverfarm unter 60 000, also unter der Haelfte der kleinsten Grenze 125 000 (Niedrig).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> WerkstattGrenzen = new Dictionary<string, int>
    {
        ["rack"] = 5000, ["usv"] = 640, ["pc"] = 720, ["werkbank"] = 2800,
        ["mainboard"] = 900, ["ram"] = 480, ["cpu"] = 240,
        // Welle A 1.0.0 (Sammelliste 410): Etagendrucker; Grund wie beim PC (ein Geraet je Buero, mehrere je Haus)
        ["drucker"] = 720,
    };

    /// <summary>Gegenprobe Welle A 1.0.0 (Sammelliste 410): Drucker wie bis A 0.99.0 (zwei graue Kaesten).</summary>
    internal static readonly AsyncLocal<bool> DruckerAltFuerTests = new();

    /// <summary>Ein Kasten im Rahmen eines Stuecks (Masse in Spiel-Einheiten): lx, lz, lw, lt, unten, oben, Farbe.</summary>
    internal delegate void KastenSetzen(float lx, float lz, float lw, float lt, float unten, float oben, Vector3 farbe);

    private static class WFarbe
    {
        public static readonly Vector3 Rahmen = Netz.Farbe("#1B2230");
        public static readonly Vector3 Blech = Netz.Farbe("#273142");
        public static readonly Vector3 BlechHell = Netz.Farbe("#3A4658");
        public static readonly Vector3 Schiene = Netz.Farbe("#9AA4B2");
        public static readonly Vector3 Loch = Netz.Farbe("#0B0F17");
        public static readonly Vector3 Server = Netz.Farbe("#C9D1DC");
        public static readonly Vector3 ServerDunkel = Netz.Farbe("#5B6676");
        public static readonly Vector3 Schacht = Netz.Farbe("#3F4856");
        public static readonly Vector3 Schwarz = Netz.Farbe("#10141C");
        public static readonly Vector3 Schraube = Netz.Farbe("#B8BEC8");
        public static readonly Vector3 LedGruen = Netz.Farbe("#22C55E");
        public static readonly Vector3 LedBlau = Netz.Farbe("#38BDF8");
        public static readonly Vector3 LedGelb = Netz.Farbe("#F59E0B");
        public static readonly Vector3 Anzeige = Netz.Farbe("#0B3B2E");
        public static readonly Vector3 AnzeigeHell = Netz.Farbe("#4ADE80");
        public static readonly Vector3 Platine = Netz.Farbe("#14532D");
        public static readonly Vector3 PlatineRam = Netz.Farbe("#166534");
        public static readonly Vector3 Chip = Netz.Farbe("#111827");
        public static readonly Vector3 Gold = Netz.Farbe("#EAB308");
        public static readonly Vector3 Kuehler = Netz.Farbe("#A3ACB9");
        public static readonly Vector3 Esd = Netz.Farbe("#2563EB");
        public static readonly Vector3 Glas = Netz.Farbe("#93C5FD");
        public static readonly Vector3 Rot = Netz.Farbe("#DC2626");
        public static readonly Vector3 Weiss = Netz.Farbe("#E5E7EB");
        public static readonly Vector3 Druck = Netz.Farbe("#D9DDE3");
        public static readonly Vector3 DruckGrau = Netz.Farbe("#9CA3AF");
        public static readonly Vector3[] Kabel = [Netz.Farbe("#2563EB"), Netz.Farbe("#F59E0B"), Netz.Farbe("#22C55E"), Netz.Farbe("#DC2626")];
    }

    /// <summary>Hilfe: Kaesten in Metern (die Teile sind nach echten Massen gebaut), umgerechnet in Spiel-Einheiten.</summary>
    private readonly struct InMetern(KastenSetzen k)
    {
        public void K(float x, float z, float w, float t, float y0, float y1, Vector3 f) =>
            k(x * Meter, z * Meter, w * Meter, t * Meter, y0 * Meter, y1 * Meter, f);
    }

    /// <summary>Masse des Serverschranks im Rahmen (Meter): Breite, Front-Ebene, Rueckwand, Hoehe.</summary>
    internal static (float X0, float X1, float Front, float Hinten, float Hoehe) RackMasse(float b, float t)
    {
        var bm = b / Meter;
        var tm = t / Meter;
        var front = tm - 0.036f;
        return (0.012f, bm - 0.012f, front, Math.Max(0.012f, front - 1.2f), 2.0f);
    }

    /// <summary>Serverschrank 19 Zoll, 40 Hoeheneinheiten, Vorderseite nach lz = t (die Glastuer zeichnet <see cref="GlastuerTeile"/>).</summary>
    internal static void RackBauen(KastenSetzen kasten, float b, float t)
    {
        var m = new InMetern(kasten);
        var (x0, x1, f, h0, hoehe) = RackMasse(b, t);
        var mitte = (x0 + x1) / 2;
        // Sockel mit Stellfuessen und Lueftungsschlitzen
        m.K(x0 + 0.03f, h0 + 0.03f, x1 - x0 - 0.06f, f - h0 - 0.06f, 0.025f, 0.1f, WFarbe.Rahmen);
        foreach (var fx in new[] { x0 + 0.02f, x1 - 0.07f })
        {
            foreach (var fz in new[] { h0 + 0.02f, f - 0.07f })
            {
                m.K(fx, fz, 0.05f, 0.05f, 0f, 0.012f, WFarbe.Schwarz);
                m.K(fx + 0.017f, fz + 0.017f, 0.016f, 0.016f, 0.012f, 0.025f, WFarbe.Schraube);
            }
        }
        for (var i = 0; i < 3; i++)
        {
            m.K(mitte - 0.2f + (i * 0.14f), f - 0.031f, 0.12f, 0.002f, 0.045f, 0.08f, WFarbe.Loch);
        }
        // Rahmen: vier Ecksaeulen, oben und unten je vier Holme
        foreach (var px in new[] { x0, x1 - 0.035f })
        {
            foreach (var pz in new[] { h0, f - 0.035f })
            {
                m.K(px, pz, 0.035f, 0.035f, 0.1f, hoehe, WFarbe.Rahmen);
            }
        }
        foreach (var (y0, y1) in new[] { (0.1f, 0.135f), (hoehe - 0.035f, hoehe) })
        {
            m.K(x0 + 0.035f, h0, x1 - x0 - 0.07f, 0.035f, y0, y1, WFarbe.Rahmen);
            m.K(x0 + 0.035f, f - 0.035f, x1 - x0 - 0.07f, 0.035f, y0, y1, WFarbe.Rahmen);
            m.K(x0, h0 + 0.035f, 0.035f, f - h0 - 0.07f, y0, y1, WFarbe.Rahmen);
            m.K(x1 - 0.035f, h0 + 0.035f, 0.035f, f - h0 - 0.07f, y0, y1, WFarbe.Rahmen);
        }
        // Seitenwaende mit Griffmulde und Lueftungsschlitzen
        foreach (var (sx, aussen) in new[] { (x0 + 0.004f, x0 + 0.001f), (x1 - 0.016f, x1 - 0.004f) })
        {
            m.K(sx, h0 + 0.035f, 0.012f, f - h0 - 0.07f, 0.135f, hoehe - 0.035f, WFarbe.Blech);
            m.K(aussen, ((h0 + f) / 2) - 0.1f, 0.003f, 0.2f, 1.1f, 1.14f, WFarbe.Schwarz);
            for (var i = 0; i < 3; i++)
            {
                m.K(aussen, h0 + 0.12f + (i * 0.3f), 0.003f, 0.22f, 0.2f, 0.215f, WFarbe.Loch);
            }
        }
        // Dach mit zwei Luefteroeffnungen und Kabeleinfuehrung (Buerste)
        m.K(x0, h0, x1 - x0, f - h0, hoehe, hoehe + 0.012f, WFarbe.Blech);
        foreach (var lz in new[] { h0 + 0.3f, h0 + 0.75f })
        {
            const float g = 0.17f;
            var lx = mitte - (g / 2);
            m.K(lx, lz, g, 0.012f, hoehe + 0.012f, hoehe + 0.02f, WFarbe.Schwarz);
            m.K(lx, lz + g - 0.012f, g, 0.012f, hoehe + 0.012f, hoehe + 0.02f, WFarbe.Schwarz);
            m.K(lx, lz + 0.012f, 0.012f, g - 0.024f, hoehe + 0.012f, hoehe + 0.02f, WFarbe.Schwarz);
            m.K(lx + g - 0.012f, lz + 0.012f, 0.012f, g - 0.024f, hoehe + 0.012f, hoehe + 0.02f, WFarbe.Schwarz);
            m.K(lx + 0.012f, lz + (g / 2) - 0.004f, g - 0.024f, 0.008f, hoehe + 0.012f, hoehe + 0.018f, WFarbe.Schraube);
            m.K(lx + (g / 2) - 0.004f, lz + 0.012f, 0.008f, g - 0.024f, hoehe + 0.012f, hoehe + 0.018f, WFarbe.Schraube);
            m.K(lx + (g / 2) - 0.025f, lz + (g / 2) - 0.025f, 0.05f, 0.05f, hoehe + 0.018f, hoehe + 0.024f, WFarbe.Schwarz);
        }
        m.K(mitte - 0.15f, h0 + 0.05f, 0.3f, 0.06f, hoehe + 0.012f, hoehe + 0.022f, WFarbe.Schwarz);
        // Rueckwand (gelochte Tuer) mit Scharnieren und Griff
        m.K(x0 + 0.035f, h0 + 0.004f, x1 - x0 - 0.07f, 0.012f, 0.135f, hoehe - 0.035f, WFarbe.Blech);
        for (var i = 0; i < 10; i++)
        {
            m.K(x0 + 0.08f, h0 + 0.001f, x1 - x0 - 0.16f, 0.003f, 0.25f + (i * 0.16f), 0.31f + (i * 0.16f), WFarbe.Loch);
        }
        foreach (var y in new[] { 0.3f, 1.0f, 1.7f })
        {
            m.K(x0 + 0.04f, h0 + 0.001f, 0.02f, 0.003f, y, y + 0.06f, WFarbe.Schraube);
        }
        m.K(x1 - 0.07f, h0 + 0.001f, 0.02f, 0.003f, 0.9f, 1.15f, WFarbe.Schwarz);
        // 19-Zoll-Schienen vorn und hinten; Kabelbuendel links im Kanal
        const float halb = 0.2415f;
        var schieneVorn = f - 0.1f;
        foreach (var sz in new[] { schieneVorn, h0 + 0.15f })
        {
            m.K(mitte - halb, sz, 0.0165f, 0.02f, 0.135f, hoehe - 0.035f, WFarbe.Schiene);
            m.K(mitte + halb - 0.0165f, sz, 0.0165f, 0.02f, 0.135f, hoehe - 0.035f, WFarbe.Schiene);
        }
        m.K(x0 + 0.05f, f - 0.2f, 0.04f, 0.04f, 0.2f, hoehe - 0.05f, WFarbe.Kabel[0]);
        m.K(x0 + 0.05f, f - 0.15f, 0.03f, 0.03f, 0.4f, hoehe - 0.05f, WFarbe.Kabel[1]);
        // Geraete von unten nach oben (Hoeheneinheit 44,45 mm, unten ab 0,15 m)
        const float he = 0.04445f;
        var vorne = schieneVorn + 0.02f;   // Vorderkante der Schienen = Frontblende der Geraete
        var belegt = 0;
        void Geraet(int anzahl, Action<float, float> bauen)
        {
            bauen(0.15f + (belegt * he), (anzahl * he) - 0.002f);
            belegt += anzahl;
        }
        void Ohren(float y, float hh, Vector3 farbe)
        {
            foreach (var ox in new[] { mitte - halb, mitte + halb - 0.0165f })
            {
                m.K(ox, vorne, 0.0165f, 0.004f, y + 0.002f, y + hh - 0.002f, farbe);
                m.K(ox + 0.004f, vorne + 0.004f, 0.008f, 0.003f, y + (hh / 2) - 0.004f, y + (hh / 2) + 0.004f, WFarbe.Schraube);
            }
        }
        void Gehaeuse(float y, float hh, float tiefe, Vector3 farbe) =>
            m.K(mitte - halb + 0.0165f, vorne - tiefe, (2 * halb) - 0.033f, tiefe, y, y + hh, farbe);
        void Blind(int anzahl) => Geraet(anzahl, (y, hh) =>
        {
            m.K(mitte - halb, vorne, 2 * halb, 0.003f, y, y + hh, WFarbe.Rahmen);
            m.K(mitte - halb + 0.004f, vorne + 0.003f, 0.008f, 0.003f, y + (hh / 2) - 0.004f, y + (hh / 2) + 0.004f, WFarbe.Schraube);
            m.K(mitte + halb - 0.012f, vorne + 0.003f, 0.008f, 0.003f, y + (hh / 2) - 0.004f, y + (hh / 2) + 0.004f, WFarbe.Schraube);
        });
        void Server(int anzahl) => Geraet(anzahl, (y, hh) =>
        {
            Gehaeuse(y, hh, 0.7f, WFarbe.Server);
            Ohren(y, hh, WFarbe.ServerDunkel);
            var schaechte = anzahl * 4;
            var reihen = anzahl;
            var breite = 0.3f / (schaechte / reihen);
            for (var i = 0; i < schaechte; i++)
            {
                var sx = mitte - halb + 0.03f + ((i % (schaechte / reihen)) * breite);
                var sy = y + 0.004f + ((i / (schaechte / reihen)) * he);
                m.K(sx, vorne, breite - 0.004f, 0.004f, sy, sy + he - 0.01f, WFarbe.Schacht);
                m.K(sx + 0.004f, vorne + 0.004f, 0.006f, 0.003f, sy + he - 0.02f, sy + he - 0.014f, i % 5 == 3 ? WFarbe.LedGelb : WFarbe.LedGruen);
                m.K(sx + breite - 0.016f, vorne + 0.004f, 0.008f, 0.004f, sy + 0.006f, sy + he - 0.016f, WFarbe.ServerDunkel);
            }
            m.K(mitte + halb - 0.07f, vorne, 0.012f, 0.004f, y + hh - 0.02f, y + hh - 0.008f, WFarbe.LedBlau);
            m.K(mitte + halb - 0.05f, vorne, 0.008f, 0.003f, y + hh - 0.018f, y + hh - 0.01f, WFarbe.LedGruen);
            m.K(mitte + halb - 0.11f, vorne, 0.08f, 0.002f, y + 0.008f, y + 0.014f, WFarbe.ServerDunkel);
            m.K(mitte + halb - 0.11f, vorne, 0.08f, 0.002f, y + 0.02f, y + 0.026f, WFarbe.ServerDunkel);
        });
        Geraet(3, (y, hh) =>
        {
            // USV im Schrank: Anzeige, drei Tasten, Leuchten, Lueftungsgitter
            Gehaeuse(y, hh, 0.6f, WFarbe.Schwarz);
            Ohren(y, hh, WFarbe.Rahmen);
            m.K(mitte - 0.12f, vorne, 0.1f, 0.003f, y + 0.07f, y + 0.115f, WFarbe.Rahmen);
            m.K(mitte - 0.113f, vorne + 0.003f, 0.086f, 0.002f, y + 0.076f, y + 0.109f, WFarbe.AnzeigeHell);
            for (var i = 0; i < 3; i++)
            {
                m.K(mitte + 0.0f + (i * 0.025f), vorne, 0.016f, 0.006f, y + 0.085f, y + 0.1f, WFarbe.ServerDunkel);
                m.K(mitte - 0.12f + (i * 0.02f), vorne, 0.008f, 0.003f, y + 0.05f, y + 0.058f, i == 2 ? WFarbe.LedGelb : WFarbe.LedGruen);
            }
            for (var i = 0; i < 4; i++)
            {
                m.K(mitte - 0.2f, vorne, 0.06f, 0.002f, y + 0.02f + (i * 0.025f), y + 0.032f + (i * 0.025f), WFarbe.Loch);
            }
        });
        Blind(1);
        Server(2);
        Server(2);
        Blind(1);
        Server(1);
        Server(1);
        void Kabelfuehrung() => Geraet(1, (y, hh) =>
        {
            m.K(mitte - halb, vorne, 2 * halb, 0.004f, y, y + hh, WFarbe.Rahmen);
            for (var i = 0; i < 5; i++)
            {
                m.K(mitte - halb + 0.05f + (i * 0.09f), vorne + 0.004f, 0.012f, 0.05f, y + 0.006f, y + hh - 0.006f, WFarbe.Schwarz);
            }
        });
        Kabelfuehrung();
        var switchY = 0.15f + (belegt * he);
        Geraet(1, (y, hh) =>
        {
            // Switch: 24 Anschluesse in zwei Reihen, vier SFP-Schaechte (zwei belegt), Leuchten, Konsole
            Gehaeuse(y, hh, 0.4f, WFarbe.BlechHell);
            Ohren(y, hh, WFarbe.BlechHell);
            for (var i = 0; i < 24; i++)
            {
                var px = mitte - 0.2f + ((i / 2) * 0.024f);
                var py = y + 0.004f + ((i % 2) * 0.019f);
                m.K(px, vorne - 0.002f, 0.018f, 0.004f, py, py + 0.015f, WFarbe.Loch);
            }
            for (var i = 0; i < 4; i++)
            {
                m.K(mitte + 0.1f + (i * 0.026f), vorne - 0.002f, 0.02f, 0.004f, y + 0.012f, y + 0.026f, WFarbe.Loch);
                m.K(mitte - 0.215f + (i * 0.006f), vorne, 0.004f, 0.003f, y + 0.03f, y + 0.034f, i == 3 ? WFarbe.LedGelb : WFarbe.LedGruen);
            }
            m.K(mitte + 0.102f, vorne, 0.016f, 0.012f, y + 0.014f, y + 0.024f, WFarbe.Schraube);
            m.K(mitte + 0.128f, vorne, 0.016f, 0.012f, y + 0.014f, y + 0.024f, WFarbe.Schraube);
            m.K(mitte + 0.21f, vorne - 0.002f, 0.012f, 0.004f, y + 0.012f, y + 0.024f, WFarbe.LedBlau);
        });
        Geraet(1, (y, hh) =>
        {
            // Patchfeld: zwoelf Buchsen, jede mit Kabel hinunter zum Switch, Beschriftung
            m.K(mitte - halb, vorne, 2 * halb, 0.004f, y, y + hh, WFarbe.Rahmen);
            m.K(mitte - halb + 0.004f, vorne + 0.004f, 0.008f, 0.003f, y + (hh / 2) - 0.004f, y + (hh / 2) + 0.004f, WFarbe.Schraube);
            m.K(mitte + halb - 0.012f, vorne + 0.004f, 0.008f, 0.003f, y + (hh / 2) - 0.004f, y + (hh / 2) + 0.004f, WFarbe.Schraube);
            m.K(mitte - 0.2f, vorne + 0.004f, 0.29f, 0.002f, y + hh - 0.01f, y + hh - 0.004f, WFarbe.Server);
            for (var i = 0; i < 12; i++)
            {
                var px = mitte - 0.2f + (i * 0.024f);
                var farbe = WFarbe.Kabel[i % 4];
                m.K(px, vorne + 0.004f, 0.018f, 0.002f, y + 0.008f, y + 0.026f, WFarbe.Loch);
                m.K(px + 0.003f, vorne + 0.006f, 0.012f, 0.02f, y + 0.01f, y + 0.024f, farbe);
                m.K(px + 0.005f, vorne + 0.016f, 0.008f, 0.008f, switchY + 0.006f + ((i % 2) * 0.019f), y + 0.012f, farbe);
            }
        });
        Kabelfuehrung();
        Blind(2);
        Geraet(2, (y, hh) =>
        {
            // NAS mit zwoelf Plattenschaechten, Anzeige und zwei Tasten
            Gehaeuse(y, hh, 0.65f, WFarbe.Server);
            Ohren(y, hh, WFarbe.ServerDunkel);
            for (var i = 0; i < 12; i++)
            {
                var sx = mitte - 0.2f + ((i % 6) * 0.055f);
                var sy = y + 0.004f + ((i / 6) * 0.042f);
                m.K(sx, vorne, 0.05f, 0.004f, sy, sy + 0.038f, WFarbe.Schacht);
                m.K(sx + 0.004f, vorne + 0.004f, 0.006f, 0.003f, sy + 0.028f, sy + 0.034f, i % 7 == 4 ? WFarbe.LedBlau : WFarbe.LedGruen);
            }
            m.K(mitte + 0.14f, vorne, 0.06f, 0.003f, y + 0.05f, y + 0.075f, WFarbe.AnzeigeHell);
            m.K(mitte + 0.14f, vorne, 0.014f, 0.006f, y + 0.02f, y + 0.034f, WFarbe.ServerDunkel);
            m.K(mitte + 0.17f, vorne, 0.014f, 0.006f, y + 0.02f, y + 0.034f, WFarbe.ServerDunkel);
        });
        Server(2);
        Geraet(1, (y, hh) =>
        {
            // Konsolenschublade (KVM) mit Griff
            Gehaeuse(y, hh, 0.55f, WFarbe.Rahmen);
            Ohren(y, hh, WFarbe.Rahmen);
            m.K(mitte - 0.1f, vorne, 0.2f, 0.012f, y + 0.012f, y + 0.026f, WFarbe.Schraube);
            m.K(mitte + 0.18f, vorne, 0.008f, 0.003f, y + 0.018f, y + 0.026f, WFarbe.LedGruen);
        });
        Blind(3);
        Blind(3);
        Blind(3);
        // oben frei: Lochreihe der Schienen je Hoeheneinheit
        for (var y = 0.15f + (belegt * he); y + he <= hoehe - 0.04f; y += he)
        {
            m.K(mitte - halb + 0.004f, vorne, 0.009f, 0.002f, y + 0.018f, y + 0.027f, WFarbe.Loch);
            m.K(mitte + halb - 0.013f, vorne, 0.009f, 0.002f, y + 0.018f, y + 0.027f, WFarbe.Loch);
        }
    }

    /// <summary>USV im Raum (Standgeraet): Anzeige, Tasten, Leuchten, Gitter, Rollen, Steckdosen hinten, Griffe.</summary>
    internal static void UsvBauen(KastenSetzen kasten, float b, float t)
    {
        var m = new InMetern(kasten);
        float bm = b / Meter, tm = t / Meter;
        const float h = 1.0f;
        float x0 = 0.02f, x1 = bm - 0.02f, z0 = 0.06f, z1 = tm - 0.03f;
        var mitte = (x0 + x1) / 2;
        m.K(x0, z0, x1 - x0, z1 - z0 - 0.02f, 0.06f, h, WFarbe.Schwarz);
        m.K(x0 + 0.01f, z1 - 0.02f, x1 - x0 - 0.02f, 0.02f, 0.07f, h - 0.01f, WFarbe.Rahmen);
        foreach (var fx in new[] { x0 + 0.03f, x1 - 0.08f })
        {
            foreach (var fz in new[] { z0 + 0.03f, z1 - 0.1f })
            {
                m.K(fx, fz, 0.05f, 0.05f, 0.0f, 0.04f, WFarbe.Loch);
                m.K(fx + 0.015f, fz + 0.015f, 0.02f, 0.02f, 0.04f, 0.06f, WFarbe.Schraube);
            }
        }
        // Anzeige mit Rahmen, vier Tasten, vier Leuchten
        m.K(mitte - 0.12f, z1, 0.24f, 0.006f, h - 0.25f, h - 0.1f, WFarbe.ServerDunkel);
        m.K(mitte - 0.11f, z1 + 0.006f, 0.22f, 0.004f, h - 0.24f, h - 0.11f, WFarbe.Anzeige);
        m.K(mitte - 0.09f, z1 + 0.01f, 0.12f, 0.002f, h - 0.16f, h - 0.13f, WFarbe.AnzeigeHell);
        m.K(mitte - 0.09f, z1 + 0.01f, 0.08f, 0.002f, h - 0.21f, h - 0.19f, WFarbe.AnzeigeHell);
        for (var i = 0; i < 4; i++)
        {
            m.K(mitte - 0.1f + (i * 0.055f), z1, 0.035f, 0.012f, h - 0.32f, h - 0.29f, WFarbe.ServerDunkel);
            m.K(mitte - 0.09f + (i * 0.055f), z1, 0.012f, 0.008f, h - 0.36f, h - 0.348f, i == 3 ? WFarbe.LedGelb : WFarbe.LedGruen);
        }
        // Lueftungsgitter unten, Schriftzug oben
        for (var i = 0; i < 8; i++)
        {
            m.K(x0 + 0.05f, z1, x1 - x0 - 0.1f, 0.004f, 0.12f + (i * 0.045f), 0.14f + (i * 0.045f), WFarbe.Loch);
        }
        m.K(mitte - 0.08f, z1, 0.16f, 0.004f, h - 0.06f, h - 0.035f, WFarbe.Schraube);
        // oben Lueftung, an den Seiten Griffmulden
        m.K(mitte - 0.15f, z0 + 0.1f, 0.3f, 0.2f, h, h + 0.004f, WFarbe.Loch);
        m.K(x0 - 0.008f, ((z0 + z1) / 2) - 0.08f, 0.008f, 0.16f, h - 0.15f, h - 0.11f, WFarbe.Rahmen);
        m.K(x1, ((z0 + z1) / 2) - 0.08f, 0.008f, 0.16f, h - 0.15f, h - 0.11f, WFarbe.Rahmen);
        // hinten: sechs Steckdosen, Sicherung, Luefter, Kabel zum Boden
        for (var i = 0; i < 6; i++)
        {
            m.K(x0 + 0.06f + (i * 0.08f), z0 - 0.008f, 0.05f, 0.008f, 0.3f, 0.36f, WFarbe.Loch);
        }
        m.K(x1 - 0.12f, z0 - 0.01f, 0.06f, 0.01f, 0.5f, 0.56f, WFarbe.ServerDunkel);
        m.K(x1 - 0.1f, z0 - 0.014f, 0.02f, 0.004f, 0.515f, 0.545f, WFarbe.Rot);
        m.K(x0 + 0.06f, z0 - 0.004f, 0.14f, 0.004f, 0.6f, 0.74f, WFarbe.Loch);
        m.K(x0 + 0.12f, z0 - 0.008f, 0.02f, 0.004f, 0.6f, 0.74f, WFarbe.Schraube);
        m.K(x0 + 0.06f, z0 - 0.008f, 0.14f, 0.004f, 0.66f, 0.68f, WFarbe.Schraube);
        m.K(x0 + 0.1f, z0 - 0.04f, 0.02f, 0.032f, 0.0f, 0.33f, WFarbe.Schwarz);
        m.K(x0 + 0.1f, 0.0f, 0.02f, 0.02f, 0.0f, 0.02f, WFarbe.Schwarz);
    }

    /// <summary>
    /// PC-Tower im Rechteck (lx, lz, lw, lt) des Stuecks, Vorderseite nach +lz, Hoehe <paramref name="hoehe"/> (Einheiten):
    /// Gehaeuse mit Seitenfenster rechts, Front mit Tasten und Anschluessen, Rueckseite mit Netzteil, innen Mainboard,
    /// Kuehler, RAM, Grafikkarte, Luefter.
    /// </summary>
    internal static void PcBauen(KastenSetzen kasten, float lx, float lz, float lw, float lt, float unten, float hoehe)
    {
        // eigene Einheit: Bruchteile von Breite (a), Tiefe (c) und Hoehe (h)
        void K(float x, float z, float w, float t, float y0, float y1, Vector3 f) =>
            kasten(lx + (x * lw), lz + (z * lt), w * lw, t * lt, unten + (y0 * hoehe), unten + (y1 * hoehe), f);
        // Fuesse
        foreach (var fx in new[] { 0.06f, 0.76f })
        {
            foreach (var fz in new[] { 0.06f, 0.82f })
            {
                K(fx, fz, 0.18f, 0.12f, 0f, 0.04f, WFarbe.Loch);
            }
        }
        // Gehaeuse: Boden, Deckel, Rueckwand, Front, linke Wand, rechts Fensterrahmen mit Glas
        K(0f, 0f, 1f, 1f, 0.04f, 0.08f, WFarbe.Rahmen);
        K(0f, 0f, 1f, 1f, 0.96f, 1f, WFarbe.Rahmen);
        K(0f, 0f, 1f, 0.03f, 0.08f, 0.96f, WFarbe.Rahmen);
        K(0f, 0.94f, 1f, 0.06f, 0.08f, 0.96f, WFarbe.Rahmen);
        K(0f, 0.03f, 0.05f, 0.91f, 0.08f, 0.96f, WFarbe.Blech);
        K(0.95f, 0.03f, 0.05f, 0.08f, 0.08f, 0.96f, WFarbe.Blech);
        K(0.95f, 0.86f, 0.05f, 0.08f, 0.08f, 0.96f, WFarbe.Blech);
        K(0.95f, 0.11f, 0.05f, 0.75f, 0.08f, 0.14f, WFarbe.Blech);
        K(0.95f, 0.11f, 0.05f, 0.75f, 0.9f, 0.96f, WFarbe.Blech);
        K(0.97f, 0.11f, 0.02f, 0.75f, 0.14f, 0.9f, WFarbe.Glas);
        // Front: Gitterstreifen, Einschalter mit Ring, Leuchte, zwei USB, Kopfhoerer, Mikrofon
        for (var i = 0; i < 3; i++)
        {
            K(0.12f, 1f - 0.012f, 0.76f, 0.012f, 0.18f + (i * 0.16f), 0.24f + (i * 0.16f), WFarbe.Loch);
        }
        K(0.38f, 0.988f, 0.24f, 0.012f, 0.84f, 0.9f, WFarbe.LedBlau);
        K(0.42f, 0.98f, 0.16f, 0.02f, 0.85f, 0.89f, WFarbe.Schwarz);
        K(0.72f, 0.988f, 0.08f, 0.012f, 0.86f, 0.88f, WFarbe.LedGruen);
        K(0.12f, 0.988f, 0.14f, 0.012f, 0.76f, 0.79f, WFarbe.Loch);
        K(0.3f, 0.988f, 0.14f, 0.012f, 0.76f, 0.79f, WFarbe.Loch);
        K(0.56f, 0.988f, 0.07f, 0.012f, 0.76f, 0.79f, WFarbe.LedGruen);
        K(0.7f, 0.988f, 0.07f, 0.012f, 0.76f, 0.79f, Netz.Farbe("#F472B6"));
        // Deckel: Lueftungsgitter
        K(0.15f, 0.2f, 0.7f, 0.05f, 1f, 1.004f, WFarbe.Loch);
        K(0.15f, 0.4f, 0.7f, 0.05f, 1f, 1.004f, WFarbe.Loch);
        // Rueckseite: Anschlussblende mit Buchsen, Netzteil mit Gitter und Schalter, drei Slotblenden, Luefter
        K(0.1f, 0f, 0.3f, 0.012f, 0.62f, 0.9f, WFarbe.Schraube);
        foreach (var y in new[] { 0.66f, 0.73f, 0.8f })
        {
            K(0.14f, -0.004f, 0.22f, 0.012f, y, y + 0.04f, WFarbe.Loch);
        }
        K(0.1f, 0f, 0.8f, 0.012f, 0.1f, 0.3f, WFarbe.Schwarz);
        K(0.5f, -0.004f, 0.3f, 0.012f, 0.14f, 0.26f, WFarbe.Loch);
        K(0.2f, -0.004f, 0.12f, 0.012f, 0.16f, 0.24f, WFarbe.Loch);
        K(0.35f, -0.004f, 0.06f, 0.012f, 0.18f, 0.22f, WFarbe.Rot);
        for (var i = 0; i < 3; i++)
        {
            K(0.1f + (i * 0.1f), 0f, 0.06f, 0.012f, 0.36f, 0.58f, WFarbe.Schraube);
        }
        K(0.5f, -0.004f, 0.35f, 0.012f, 0.6f, 0.86f, WFarbe.Loch);
        // innen (durch das Fenster): Mainboard an der linken Wand, Kuehler, zwei RAM, Grafikkarte, Netzteilabdeckung,
        // zwei Frontluefter, SSD, Kabel
        K(0.05f, 0.08f, 0.05f, 0.6f, 0.36f, 0.9f, WFarbe.Platine);
        K(0.1f, 0.3f, 0.12f, 0.16f, 0.66f, 0.8f, WFarbe.Kuehler);
        for (var i = 0; i < 3; i++)
        {
            K(0.22f + (i * 0.12f), 0.3f, 0.06f, 0.16f, 0.64f, 0.82f, WFarbe.Kuehler);
        }
        K(0.58f, 0.3f, 0.06f, 0.16f, 0.64f, 0.82f, WFarbe.Schwarz);
        K(0.1f, 0.5f, 0.3f, 0.02f, 0.66f, 0.84f, WFarbe.Rot);
        K(0.1f, 0.54f, 0.3f, 0.02f, 0.66f, 0.84f, WFarbe.Rot);
        K(0.1f, 0.04f, 0.6f, 0.62f, 0.44f, 0.5f, WFarbe.Schwarz);
        K(0.1f, 0.04f, 0.6f, 0.62f, 0.5f, 0.54f, WFarbe.BlechHell);
        K(0.25f, 0.12f, 0.4f, 0.16f, 0.42f, 0.44f, WFarbe.ServerDunkel);
        K(0.25f, 0.4f, 0.4f, 0.16f, 0.42f, 0.44f, WFarbe.ServerDunkel);
        K(0.05f, 0.03f, 0.9f, 0.6f, 0.08f, 0.32f, WFarbe.Blech);
        foreach (var y in new[] { 0.4f, 0.66f })
        {
            K(0.15f, 0.88f, 0.7f, 0.05f, y, y + 0.22f, WFarbe.Schwarz);
            K(0.35f, 0.875f, 0.3f, 0.005f, y + 0.06f, y + 0.16f, WFarbe.ServerDunkel);
        }
        K(0.6f, 0.7f, 0.3f, 0.12f, 0.32f, 0.34f, WFarbe.Schraube);
        K(0.12f, 0.7f, 0.06f, 0.06f, 0.32f, 0.66f, WFarbe.Schwarz);
        K(0.12f, 0.76f, 0.06f, 0.1f, 0.6f, 0.64f, WFarbe.Schwarz);
    }

    /// <summary>
    /// Welle A 1.0.0 (Sammelliste 410, Nico: „Drucker neu im realistischen Stil wie Server und PC“): Etagendrucker
    /// (Multifunktionsgeraet) im Rechteck des Stuecks, Vorderseite nach lz = t: Rollen, Unterschrank mit zwei Papierfaechern,
    /// Druckwerk mit Frontklappe und Bypass-Einzug rechts, Ausgabefach mit Papier, Scanner mit Vorlageneinzug und Papier oben,
    /// Bedienfeld mit Anzeige und Leuchten, Lueftung hinten. Masse in Metern nach echten Geraeten (gut 1 m hoch).
    /// </summary>
    internal static void DruckerBauen(KastenSetzen kasten, float b, float t)
    {
        var m = new InMetern(kasten);
        float bm = b / Meter, tm = t / Meter;
        float x0 = 0.01f, x1 = bm - 0.01f, z0 = 0.02f, z1 = tm - 0.012f;
        var breite = x1 - x0;
        var mitte = (x0 + x1) / 2;
        // Rollen
        foreach (var rx in new[] { x0 + 0.02f, x1 - 0.06f })
        {
            foreach (var rz in new[] { z0 + 0.02f, z1 - 0.06f })
            {
                m.K(rx, rz, 0.04f, 0.04f, 0f, 0.03f, WFarbe.Schwarz);
            }
        }
        // Unterschrank mit zwei Papierfaechern (Fronten leicht vorstehend, Griffmulden, Fuellanzeige)
        m.K(x0, z0, breite, z1 - z0, 0.03f, 0.42f, WFarbe.Druck);
        foreach (var (u, o) in new[] { (0.04f, 0.22f), (0.23f, 0.41f) })
        {
            m.K(x0 + 0.01f, z1, breite - 0.02f, 0.012f, u, o, WFarbe.Druck);
            m.K(mitte - 0.07f, z1 + 0.004f, 0.14f, 0.008f, o - 0.05f, o - 0.03f, WFarbe.Schwarz);
            m.K(x0 + 0.04f, z1 + 0.004f, 0.03f, 0.008f, u + 0.03f, o - 0.03f, WFarbe.DruckGrau);
            m.K(x0 + 0.045f, z1 + 0.006f, 0.02f, 0.006f, u + 0.04f, u + 0.09f, WFarbe.Weiss);
        }
        // Druckwerk: Gehaeuse, Frontklappe mit Fugen, Bypass rechts, Lueftung hinten
        m.K(x0, z0, breite - 0.025f, z1 - z0, 0.42f, 0.78f, WFarbe.Druck);
        m.K(x0 + 0.01f, z1, breite - 0.045f, 0.01f, 0.45f, 0.74f, WFarbe.Druck);
        m.K(x0 + 0.01f, z1 + 0.004f, breite - 0.045f, 0.008f, 0.6f, 0.605f, WFarbe.DruckGrau);
        m.K(mitte - 0.1f, z1 + 0.004f, 0.2f, 0.008f, 0.7f, 0.72f, WFarbe.Schwarz);
        m.K(x1 - 0.025f, z0 + 0.05f, 0.025f, z1 - z0 - 0.1f, 0.5f, 0.7f, WFarbe.DruckGrau);
        m.K(x1 - 0.02f, z0 + 0.08f, 0.02f, z1 - z0 - 0.16f, 0.69f, 0.7f, WFarbe.Weiss);
        for (var i = 0; i < 5; i++)
        {
            m.K(x0 + 0.06f, z0 - 0.004f, breite - 0.12f, 0.004f, 0.48f + (i * 0.05f), 0.495f + (i * 0.05f), WFarbe.Loch);
        }
        // Ausgabefach: hinten und links Stuetzen, dazwischen frei, Papierstapel
        m.K(x0, z0, breite, 0.1f, 0.78f, 0.86f, WFarbe.Druck);
        m.K(x0, z0, 0.06f, z1 - z0, 0.78f, 0.86f, WFarbe.Druck);
        m.K(x0 + 0.06f, z0 + 0.1f, breite - 0.06f, z1 - z0 - 0.1f, 0.78f, 0.785f, WFarbe.Loch);
        m.K(x0 + 0.1f, z0 + 0.14f, breite - 0.18f, Math.Min(0.3f, z1 - z0 - 0.18f), 0.785f, 0.805f, WFarbe.Weiss);
        // Scanner mit Deckel, Vorlageneinzug und Papier, Bedienfeld vorn
        m.K(x0, z0, breite, z1 - z0, 0.86f, 0.93f, WFarbe.Druck);
        m.K(x0 + 0.01f, z0 + 0.01f, breite - 0.02f, z1 - z0 - 0.02f, 0.93f, 0.95f, WFarbe.DruckGrau);
        m.K(x0 + 0.05f, z0 + 0.04f, breite - 0.1f, (z1 - z0) * 0.6f, 0.95f, 0.99f, WFarbe.DruckGrau);
        m.K(x0 + 0.08f, z0 + 0.07f, breite - 0.16f, (z1 - z0) * 0.45f, 0.99f, 0.996f, WFarbe.Weiss);
        m.K(mitte - 0.12f, z1 - 0.08f, 0.24f, 0.08f, 0.93f, 0.955f, WFarbe.Schwarz);
        m.K(mitte - 0.1f, z1 - 0.07f, 0.12f, 0.06f, 0.955f, 0.958f, WFarbe.Anzeige);
        m.K(mitte - 0.09f, z1 - 0.055f, 0.08f, 0.012f, 0.958f, 0.96f, WFarbe.AnzeigeHell);
        m.K(mitte + 0.04f, z1 - 0.06f, 0.025f, 0.02f, 0.955f, 0.96f, WFarbe.LedGruen);
        m.K(mitte + 0.075f, z1 - 0.06f, 0.025f, 0.02f, 0.955f, 0.96f, WFarbe.LedBlau);
        m.K(mitte + 0.04f, z1 - 0.03f, 0.06f, 0.015f, 0.955f, 0.958f, WFarbe.ServerDunkel);
        // Netzkabel hinten zum Boden
        m.K(x1 - 0.1f, z0 - 0.015f, 0.015f, 0.015f, 0f, 0.5f, WFarbe.Schwarz);
    }

    /// <summary>Mainboard (ATX, 30,5 x 24,4 cm) flach im Rechteck, Sockel nach +lz.</summary>
    internal static void MainboardBauen(KastenSetzen kasten, float lx, float lz, float unten)
    {
        var m = new InMetern((x, z, w, t, y0, y1, f) => kasten(lx + x, lz + z, w, t, unten + y0, unten + y1, f));
        m.K(0f, 0f, 0.305f, 0.244f, 0f, 0.002f, WFarbe.Platine);
        // CPU-Sockel mit Rahmen, Hebel, Platte
        m.K(0.12f, 0.06f, 0.07f, 0.07f, 0.002f, 0.007f, WFarbe.ServerDunkel);
        m.K(0.13f, 0.07f, 0.05f, 0.05f, 0.007f, 0.009f, WFarbe.Schraube);
        m.K(0.193f, 0.06f, 0.004f, 0.07f, 0.002f, 0.01f, WFarbe.Schraube);
        // Spannungswandler: zwei Kuehler, acht Spulen
        m.K(0.04f, 0.04f, 0.07f, 0.02f, 0.002f, 0.03f, WFarbe.Kuehler);
        m.K(0.12f, 0.01f, 0.07f, 0.02f, 0.002f, 0.03f, WFarbe.Kuehler);
        for (var i = 0; i < 8; i++)
        {
            m.K(0.045f + (i * 0.008f), 0.065f, 0.006f, 0.006f, 0.002f, 0.009f, WFarbe.Loch);
        }
        // vier RAM-Baenke mit Halteklammern (Bank 1 frei, siehe W2-Notiz)
        for (var i = 0; i < 4; i++)
        {
            var bx = 0.21f + (i * 0.012f);
            m.K(bx, 0.03f, 0.006f, 0.133f, 0.002f, 0.012f, i % 2 == 0 ? WFarbe.Loch : WFarbe.ServerDunkel);
            m.K(bx, 0.025f, 0.006f, 0.005f, 0.002f, 0.016f, WFarbe.Weiss);
            m.K(bx, 0.163f, 0.006f, 0.005f, 0.002f, 0.016f, WFarbe.Weiss);
        }
        // drei PCIe-Steckplaetze mit Riegel
        for (var i = 0; i < 3; i++)
        {
            m.K(0.02f, 0.16f + (i * 0.025f), 0.09f, 0.008f, 0.002f, 0.013f, i == 0 ? WFarbe.Schraube : WFarbe.Loch);
            m.K(0.112f, 0.16f + (i * 0.025f), 0.006f, 0.008f, 0.002f, 0.015f, WFarbe.Weiss);
        }
        // Chipsatz mit Kuehler, zwei M.2 mit Schraube
        m.K(0.13f, 0.17f, 0.05f, 0.04f, 0.002f, 0.006f, WFarbe.Rahmen);
        m.K(0.135f, 0.175f, 0.04f, 0.03f, 0.006f, 0.012f, WFarbe.Kuehler);
        foreach (var mz in new[] { 0.145f, 0.215f })
        {
            m.K(0.2f, mz, 0.022f, 0.08f, 0.002f, 0.004f, WFarbe.Loch);
            m.K(0.204f, mz + 0.084f, 0.006f, 0.006f, 0.002f, 0.006f, WFarbe.Schraube);
        }
        // Stecker: 24-polig, 8-polig, vier SATA, drei Luefteranschluesse, Frontpanel
        m.K(0.285f, 0.06f, 0.014f, 0.052f, 0.002f, 0.016f, WFarbe.Loch);
        m.K(0.06f, 0.008f, 0.02f, 0.01f, 0.002f, 0.014f, WFarbe.Loch);
        for (var i = 0; i < 4; i++)
        {
            m.K(0.27f + ((i % 2) * 0.014f), 0.185f + ((i / 2) * 0.016f), 0.012f, 0.012f, 0.002f, 0.01f, WFarbe.Loch);
        }
        foreach (var (fx, fz) in new[] { (0.2f, 0.01f), (0.06f, 0.14f), (0.28f, 0.14f) })
        {
            m.K(fx, fz, 0.01f, 0.004f, 0.002f, 0.009f, WFarbe.Weiss);
        }
        m.K(0.24f, 0.232f, 0.04f, 0.008f, 0.002f, 0.01f, WFarbe.Loch);
        // Anschlussblende hinten mit sechs Buchsen
        m.K(0f, 0.02f, 0.02f, 0.12f, 0.002f, 0.03f, WFarbe.Rahmen);
        for (var i = 0; i < 6; i++)
        {
            m.K(-0.002f, 0.025f + (i * 0.019f), 0.004f, 0.014f, 0.008f, 0.024f, i % 3 == 0 ? WFarbe.Esd : WFarbe.Loch);
        }
        // CMOS-Batterie, Audio-Kondensatoren, Schraubloecher
        m.K(0.03f, 0.2f, 0.02f, 0.02f, 0.002f, 0.005f, WFarbe.Schraube);
        for (var i = 0; i < 4; i++)
        {
            m.K(0.06f + (i * 0.008f), 0.226f, 0.006f, 0.006f, 0.002f, 0.012f, WFarbe.LedGelb);
        }
        foreach (var (sx, sz) in new[] { (0.006f, 0.006f), (0.29f, 0.006f), (0.006f, 0.228f), (0.29f, 0.228f), (0.15f, 0.006f), (0.15f, 0.228f) })
        {
            m.K(sx, sz, 0.008f, 0.008f, 0.002f, 0.003f, WFarbe.Schraube);
        }
    }

    /// <summary>RAM-Riegel flach (13,3 x 3,1 cm): Platine, acht Chips, SPD-Chip, 20 Kontakte mit Kerbe, Aufkleber, sechs Widerstaende.</summary>
    internal static void RamBauen(KastenSetzen kasten, float lx, float lz, float unten)
    {
        var m = new InMetern((x, z, w, t, y0, y1, f) => kasten(lx + x, lz + z, w, t, unten + y0, unten + y1, f));
        m.K(0f, 0f, 0.133f, 0.031f, 0f, 0.0015f, WFarbe.PlatineRam);
        for (var i = 0; i < 8; i++)
        {
            m.K(0.006f + (i * 0.0155f), 0.011f, 0.012f, 0.011f, 0.0015f, 0.0028f, WFarbe.Chip);
        }
        m.K(0.06f, 0.024f, 0.006f, 0.004f, 0.0015f, 0.0025f, WFarbe.Chip);
        for (var i = 0; i < 20; i++)
        {
            // Kerbe zwischen Kontakt 9 und 10 (nicht mittig, so passt er nur richtig herum)
            var px = 0.004f + (i * 0.0063f) + (i >= 9 ? 0.003f : 0f);
            m.K(px, 0.0005f, 0.0042f, 0.006f, 0.0015f, 0.0019f, WFarbe.Gold);
        }
        m.K(0.03f, 0.023f, 0.022f, 0.006f, 0.0015f, 0.0019f, WFarbe.Weiss);
        for (var i = 0; i < 6; i++)
        {
            // kleine Widerstaende zwischen Chips und Kontakten
            m.K(0.01f + (i * 0.021f), 0.0075f, 0.002f, 0.0012f, 0.0015f, 0.0021f, WFarbe.Schraube);
        }
    }

    /// <summary>CPU flach (4 x 4 cm): Traeger, Waerme-Deckel in zwei Stufen, zwei Kerben, Dreieck-Ecke, acht Bauteile.</summary>
    internal static void CpuBauen(KastenSetzen kasten, float lx, float lz, float unten)
    {
        var m = new InMetern((x, z, w, t, y0, y1, f) => kasten(lx + x, lz + z, w, t, unten + y0, unten + y1, f));
        m.K(0f, 0f, 0.04f, 0.04f, 0f, 0.0015f, WFarbe.PlatineRam);
        m.K(0.004f, 0.004f, 0.032f, 0.032f, 0.0015f, 0.003f, WFarbe.Kuehler);
        m.K(0.007f, 0.007f, 0.026f, 0.026f, 0.003f, 0.0045f, WFarbe.Schraube);
        m.K(0.012f, 0.012f, 0.016f, 0.004f, 0.0045f, 0.0048f, WFarbe.ServerDunkel);
        m.K(-0.0005f, 0.012f, 0.002f, 0.004f, 0f, 0.0015f, WFarbe.Loch);
        m.K(0.0385f, 0.012f, 0.002f, 0.004f, 0f, 0.0015f, WFarbe.Loch);
        m.K(0.001f, 0.001f, 0.004f, 0.002f, 0.0015f, 0.002f, WFarbe.Gold);
        m.K(0.001f, 0.003f, 0.002f, 0.002f, 0.0015f, 0.002f, WFarbe.Gold);
        for (var i = 0; i < 4; i++)
        {
            m.K(0.012f + (i * 0.005f), 0.0365f, 0.002f, 0.0015f, 0.0015f, 0.0025f, WFarbe.Schraube);
            m.K(0.012f + (i * 0.005f), 0.002f, 0.002f, 0.0015f, 0.0015f, 0.0025f, WFarbe.Schraube);
        }
    }

    /// <summary>
    /// Werkbank-Ausstattung auf der Platte (Hoehe <paramref name="platte"/>): ESD-Matte mit Erdung, PC-Tower, Mainboard,
    /// zwei RAM-Riegel, CPU, Schraubendreher, Erdungsband. Das Tischmodell setzt <see cref="Deko"/> wie bisher.
    /// </summary>
    internal static void WerkbankBauen(KastenSetzen kasten, float b, float t, float platte)
    {
        var m = new InMetern((x, z, w, tt, y0, y1, f) => kasten(x, z, w, tt, platte + y0, platte + y1, f));
        var bm = b / Meter;
        var tm = t / Meter;
        // PC-Tower links (20 x 45 x 45 cm)
        PcBauen(kasten, 0.25f, 0.12f, 0.2f * Meter, 0.45f * Meter, platte, 0.45f * Meter);
        // ESD-Matte mit Erdungsknopf und Kabel
        var mx = Math.Min(0.6f, bm * 0.3f);
        var mw = Math.Min(1.2f, bm - mx - 0.1f);
        m.K(mx, 0.05f, mw, tm - 0.1f, 0f, 0.003f, WFarbe.Esd);
        m.K(mx + 0.02f, 0.06f, 0.012f, 0.012f, 0.003f, 0.008f, WFarbe.Schraube);
        m.K(mx + 0.024f, 0.02f, 0.004f, 0.04f, 0.001f, 0.004f, WFarbe.LedGruen);
        // Mainboard, daneben RAM, CPU, Schraubendreher, Erdungsband
        MainboardBauen(kasten, (mx + 0.05f) * Meter, 0.12f * Meter, platte + (0.003f * Meter));
        RamBauen(kasten, (mx + 0.42f) * Meter, 0.14f * Meter, platte + (0.003f * Meter));
        RamBauen(kasten, (mx + 0.42f) * Meter, 0.19f * Meter, platte + (0.003f * Meter));
        CpuBauen(kasten, (mx + 0.6f) * Meter, 0.14f * Meter, platte + (0.003f * Meter));
        m.K(mx + 0.42f, 0.27f, 0.1f, 0.022f, 0.003f, 0.025f, WFarbe.Rot);
        m.K(mx + 0.52f, 0.278f, 0.09f, 0.006f, 0.008f, 0.014f, WFarbe.Schraube);
        m.K(mx + 0.66f, 0.24f, 0.07f, 0.008f, 0.003f, 0.012f, WFarbe.Esd);
        m.K(mx + 0.73f, 0.236f, 0.016f, 0.016f, 0.003f, 0.012f, WFarbe.Schraube);
    }

    /// <summary>Die Werkstatt-Teile aus Kaesten ohne Innenraum (fuer Tests: Dreiecke und Lage je Teil).</summary>
    internal static List<Teil> WerkstattTeile(string art, float b, float t)
    {
        var teile = new List<Teil>();
        void K(float lx, float lz, float lw, float lt, float unten, float oben, Vector3 f) =>
            teile.Add(Quader(lx, unten, lz, lx + lw, oben, lz + lt, f));
        switch (art)
        {
            case "rack": RackBauen(K, b, t); break;
            case "usv": UsvBauen(K, b, t); break;
            case "pc": PcBauen(K, b * 0.3f, t * 0.2f, b * 0.4f, t * 0.6f, 0, 0.5f * Meter); break;
            case "werkbank": WerkbankBauen(K, b, t, 0.9f * Meter); break;
            case "mainboard": MainboardBauen(K, 0, 0, 0); break;
            case "ram": RamBauen(K, 0, 0, 0); break;
            case "cpu": CpuBauen(K, 0, 0, 0); break;
            case "drucker": DruckerBauen(K, b, t); break;
            default: throw new ArgumentException(art);
        }
        return teile;
    }

    // ---- Glastuer der Serverschraenke (Weisung P12-317 F2, Nico: „Erst zu und durch Oeffnen wird sie so geoeffnet wie auf dem Bild.“)

    /// <summary>Lage der Glastuer: zu, oder 90 Grad offen mit dem Scharnier links bzw. rechts (von vorn gesehen, im Rahmen des Schranks).</summary>
    public enum GlastuerLage
    {
        Zu,
        OffenLinks,
        OffenRechts,
    }

    private readonly List<(Rahmen R, float B, float T)> _glastueren = [];

    /// <summary>Zahl der Serverschraenke mit Glastuer in diesem Raum.</summary>
    public int GlastuerZahl => _glastueren.Count;

    /// <summary>
    /// Der Schrank, vor dessen Glastuer die Figur steht: in Reichweite (<see cref="SpielKern.MoebelReichweite"/> vom Rechteck) und
    /// vor der Vorderseite; der naechste, sonst null.
    /// </summary>
    public int? GlastuerNahe(float x, float z)
    {
        int? beste = null;
        var abstand = double.MaxValue;
        for (var i = 0; i < _glastueren.Count; i++)
        {
            var (r, b, t) = _glastueren[i];
            var mitte = r.Punkt(b / 2, t);
            var normale = r.Punkt(b / 2, t + 1) - mitte;
            var a = SpielKern.Abstand(r.X, r.Z, r.W, r.T, x, z);
            if (a <= SpielKern.MoebelReichweite && Vector2.Dot(new Vector2(x, z) - mitte, normale) > 0 && a < abstand)
            {
                (beste, abstand) = (i, a);
            }
        }
        return beste;
    }

    /// <summary>Blick (Grad, wie <c>FigurRichtung</c>: 0 = +z, im Uhrzeigersinn von oben) einer Figur, die zur Front des Schranks schaut.</summary>
    public float GlastuerBlick(int nummer)
    {
        var (r, b, t) = _glastueren[nummer];
        var n = r.Punkt(b / 2, t) - r.Punkt(b / 2, t + 1);
        return MathF.Atan2(n.X, n.Y) * 180f / MathF.PI;
    }

    /// <summary>
    /// Kaesten der Glastuer Nummer <paramref name="nummer"/> (Weltlage): Rahmen, Griff und Scharniere (<paramref name="glas"/> false)
    /// oder nur die Scheibe (true, wird halb durchsichtig gezeichnet). Zu: in der Front des Schranks. Offen: um 90 Grad um das
    /// Scharnier nach vorn gedreht (wie im Entwurfsbild P12-194 „355 Serverschrank neu“).
    /// </summary>
    public List<Teil> GlastuerTeile(int nummer, GlastuerLage lage, bool glas)
    {
        var (r, b, t) = _glastueren[nummer];
        var teile = new List<Teil>();
        var (x0, x1, _, _, hoehe) = RackMasse(b, t);
        var tm = t / Meter;
        float xl = x0 + 0.004f, xr = x1 - 0.004f, zc = tm - 0.024f;
        var scharnier = lage == GlastuerLage.OffenRechts ? xr : xl;
        void K(float ax, float az, float bx, float bz, float y0, float y1, Vector3 f)
        {
            // in Metern, zu; offen um das Scharnier gedreht
            (float X, float Z) Drehen(float px, float pz)
            {
                float dx = px - scharnier, dz = pz - zc;
                return lage switch
                {
                    GlastuerLage.OffenLinks => (scharnier - dz, zc + dx),
                    GlastuerLage.OffenRechts => (scharnier + dz, zc - dx),
                    _ => (px, pz),
                };
            }
            var (p1, p2) = (Drehen(ax, az), Drehen(bx, bz));
            var (wa, wb) = (r.Punkt(MathF.Min(p1.X, p2.X) * Meter, MathF.Min(p1.Z, p2.Z) * Meter), r.Punkt(MathF.Max(p1.X, p2.X) * Meter, MathF.Max(p1.Z, p2.Z) * Meter));
            teile.Add(Quader(MathF.Min(wa.X, wb.X), y0 * Meter, MathF.Min(wa.Y, wb.Y), MathF.Max(wa.X, wb.X), y1 * Meter, MathF.Max(wa.Y, wb.Y), f));
        }
        const float dick = 0.009f;
        float unten = 0.11f, oben = hoehe - 0.01f;
        if (glas)
        {
            K(xl + 0.04f, zc - 0.003f, xr - 0.04f, zc + 0.003f, unten + 0.04f, oben - 0.04f, WFarbe.Glas);
            return teile;
        }
        K(xl, zc - dick, xl + 0.04f, zc + dick, unten, oben, WFarbe.Rahmen);
        K(xr - 0.04f, zc - dick, xr, zc + dick, unten, oben, WFarbe.Rahmen);
        K(xl + 0.04f, zc - dick, xr - 0.04f, zc + dick, oben - 0.04f, oben, WFarbe.Rahmen);
        K(xl + 0.04f, zc - dick, xr - 0.04f, zc + dick, unten, unten + 0.04f, WFarbe.Rahmen);
        // Griff und Schloss auf der Seite gegenueber dem Scharnier (zu: rechts)
        var griff = lage == GlastuerLage.OffenRechts ? xl + 0.012f : xr - 0.028f;
        K(griff, zc + dick, griff + 0.016f, zc + dick + 0.011f, 0.95f, 1.2f, WFarbe.Schraube);
        K(griff, zc + dick, griff + 0.016f, zc + dick + 0.004f, 1.24f, 1.27f, WFarbe.Schwarz);
        // drei Scharniere
        foreach (var y in new[] { 0.3f, 1.0f, 1.7f })
        {
            K(scharnier - 0.006f, zc - dick - 0.004f, scharnier + 0.006f, zc + dick, y, y + 0.06f, WFarbe.Schraube);
        }
        return teile;
    }

    /// <summary>
    /// Welche Seite die Glastuer oeffnet, ohne die Figur (Stelle, Blick in Bogenmass, Haltung) oder ein anderes Stueck zu
    /// beruehren (Spiel 0,02): zuerst die Seite weiter weg von der Figur; geht keine, bleibt sie zu.
    /// </summary>
    public GlastuerLage GlastuerWahl(int nummer, Vector2 figur, float blick, Koerperhaltung haltung)
    {
        var (r, b, t) = _glastueren[nummer];
        var links = r.Punkt(0, t);
        var rechts = r.Punkt(b, t);
        var seiten = Vector2.Distance(figur, links) >= Vector2.Distance(figur, rechts)
            ? new[] { GlastuerLage.OffenLinks, GlastuerLage.OffenRechts }
            : [GlastuerLage.OffenRechts, GlastuerLage.OffenLinks];
        foreach (var lage in seiten)
        {
            if (GlastuerFrei(nummer, lage, figur, blick, haltung))
            {
                return lage;
            }
        }
        return GlastuerLage.Zu;
    }

    /// <summary>Beruehrt die Glastuer in dieser Lage weder die Figur noch ein anderes Stueck des Raums (Spiel 0,02)?</summary>
    public bool GlastuerFrei(int nummer, GlastuerLage lage, Vector2 figur, float blick, Koerperhaltung haltung)
    {
        var tuer = GlastuerTeile(nummer, lage, false).Concat(GlastuerTeile(nummer, lage, true)).ToList();
        var quader = KoerperQuader(haltung);
        foreach (var t in tuer)
        {
            var (lo, hi) = (new Vector3(MathF.Min(t.X1, t.X2), MathF.Min(t.Y1, t.Y2), MathF.Min(t.Z1, t.Z2)), new Vector3(MathF.Max(t.X1, t.X2), MathF.Max(t.Y1, t.Y2), MathF.Max(t.Z1, t.Z2)));
            if (QuaderBeruehrt(new Vector3(figur.X, 0, figur.Y), blick, quader, FigurHoehe, lo, hi))
            {
                return false;
            }
            if (Hindernisse().Any(k => Durchdringt(lo, hi, k.Lo, k.Hi)))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Durchdringen sich zwei achsgleiche Kaesten um mehr als 0,02 in jeder Richtung?</summary>
    internal static bool Durchdringt(Vector3 alo, Vector3 ahi, Vector3 blo, Vector3 bhi)
    {
        const float spiel = 0.02f;
        return MathF.Min(ahi.X, bhi.X) - MathF.Max(alo.X, blo.X) > spiel && MathF.Min(ahi.Y, bhi.Y) - MathF.Max(alo.Y, blo.Y) > spiel
            && MathF.Min(ahi.Z, bhi.Z) - MathF.Max(alo.Z, blo.Z) > spiel;
    }

    /// <summary>Alle Glastueren in ihrer Lage als ein Netz (Rahmen) bzw. die Scheiben (<paramref name="glas"/>).</summary>
    public Netz GlastuerNetz(IReadOnlyList<GlastuerLage> lagen, bool glas) =>
        Netz.Aus(Enumerable.Range(0, _glastueren.Count).SelectMany(i => GlastuerTeile(i, i < lagen.Count ? lagen[i] : GlastuerLage.Zu, glas)).ToList());
}
