using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Engine festigen 3 (A 1.16.0, Weisung P12-395/P12-399): Teil von <see cref="Innenraum"/> mit den eigenen Moebelformen
/// (<c>Eigen</c>). Reiner Umzug aus Innenraum.cs, nichts geaendert.
/// </summary>
public sealed partial class Innenraum
{
    /// <summary>Einrichtung ohne passendes CC0-Modell: eigene Geometrie in den Farben der Python-Zeichnung.</summary>
    private void Eigen(string typ, Rahmen r, float b, float t)
    {
        const float M = Meter;
        var wandOben = WandHoehe - 0.2f;
        switch (typ)
        {
            case "rack" when !WerkstattAltFuerTests.Value:
                RackBauen(KastenIn(r), b, t);
                _glastueren.Add((r, b, t));
                break;
            case "usv" when !WerkstattAltFuerTests.Value:
                UsvBauen(KastenIn(r), b, t);
                break;
            case "pc" when !WerkstattAltFuerTests.Value:
                PcBauen(KastenIn(r), b * 0.3f, t * 0.2f, b * 0.4f, t * 0.6f, 0, 0.5f * M);
                break;
            case "rack":
            {
                Kasten(r, 0, 0, b, t, 0, 2.0f * M, Farben.Geraet);
                // Einschuebe mit Leuchten auf der Vorderseite
                for (var h = 0.25f; h < 1.9f * M; h += 0.28f)
                {
                    Kasten(r, 0.08f, t - 0.02f, b - 0.16f, 0.04f, h, h + 0.18f, Farben.GeraetHell);
                    Kasten(r, b - 0.2f, t, 0.08f, 0.03f, h + 0.06f, h + 0.12f, h % 0.84f < 0.28f ? Farben.Bildschirm : Farben.Gruen);
                }
                break;
            }
            case "usv":
                Kasten(r, 0, 0, b, t, 0, 1.0f * M, Farben.MetallDunkel);
                Kasten(r, 0.15f, t - 0.02f, b * 0.45f, 0.04f, 0.6f * M, 0.8f * M, Netz.Farbe("#0B3B2E"));
                Kasten(r, b - 0.35f, t - 0.02f, 0.18f, 0.04f, 0.7f * M, 0.8f * M, Farben.Gruen);
                break;
            case "klima":
                Kasten(r, 0, 0, b, t, 0, 1.9f * M, Farben.Hell);
                for (var h = 0.3f * M; h < 1.8f * M; h += 0.18f)
                {
                    Kasten(r, 0.05f, t - 0.01f, b - 0.1f, 0.02f, h, h + 0.05f, Farben.Metall);
                }
                break;
            case "drucker" when !DruckerAltFuerTests.Value:
                DruckerBauen(KastenIn(r), b, t);
                break;
            case "drucker":
                Kasten(r, 0, 0, b, t, 0, 0.75f * M, Farben.Hell);
                Kasten(r, 0.05f, 0.05f, b - 0.1f, t - 0.1f, 0.75f * M, 0.95f * M, Farben.Weiss);
                break;
            case "pc":
                Kasten(r, b * 0.3f, t * 0.2f, b * 0.4f, t * 0.6f, 0, 0.5f * M, Farben.GeraetHell);
                break;
            case "feuerloescher":
                Kasten(r, b * 0.25f, t * 0.25f, b * 0.5f, t * 0.5f, 0, 0.6f * M, Farben.Rot);
                Kasten(r, b * 0.38f, t * 0.38f, b * 0.24f, t * 0.24f, 0.6f * M, 0.68f * M, Farben.Geraet);
                break;
            case "fussmatte":
                Kasten(r, 0, 0, b, t, 0.03f, 0.05f, Farben.HolzDunkel);
                break;
            case "markierung":
                Kasten(r, 0, 0, b, t, 0.03f, 0.05f, Farben.Gelb);
                break;
            case "whiteboard":
            case "anzeigetafel":
            case "monitorwand":
            {
                // an der Wand: flache Tafel in Augenhoehe
                var unten = typ == "anzeigetafel" ? 1.1f * M : 0.8f * M;
                var farbe = typ == "whiteboard" ? Farben.Weiss : Farben.Geraet;
                Kasten(r, 0, 0, b, Math.Min(t, 0.12f), unten, wandOben, Farben.Rahmen);
                Kasten(r, 0.06f, 0.0f, b - 0.12f, Math.Min(t, 0.12f) + 0.02f, unten + 0.06f, wandOben - 0.06f, farbe);
                if (typ == "monitorwand")
                {
                    var n = Math.Max(2, (int)(b / 1.3f));
                    for (var i = 0; i < n; i++)
                    {
                        var bx = (b * i / n) + 0.12f;
                        Kasten(r, bx, 0.0f, (b / n) - 0.24f, Math.Min(t, 0.12f) + 0.03f, unten + 0.15f, wandOben - 0.15f, i % 2 == 0 ? Farben.Bildschirm : Netz.Farbe("#0E7490"));
                    }
                }
                else if (typ == "anzeigetafel")
                {
                    Kasten(r, 0.15f, 0.0f, b - 0.3f, Math.Min(t, 0.12f) + 0.03f, unten + 0.2f, unten + 0.3f, Farben.Gelb);
                }
                break;
            }
            case "bild":
            case "wimpel":
            case "kamera":
            {
                var farbe = typ == "bild" ? Netz.Farbe("#F472B6") : typ == "wimpel" ? Farben.Gelb : Farben.Geraet;
                if (typ == "bild" && !BildAltFuerTests.Value && !InWand(r.Rechteck(0, -BildWandAbstand, b, BildWandAbstand)))
                {
                    // Welle A 1.3.1 (Weisung P12-348, Sammelliste 432, Nico 2a „Staffelei“): ein Bild, hinter dem keine Wand ist,
                    // schwebte in der Luft; es steht jetzt auf einer Staffelei (zwei Beine vorn, ein Bein hinten mit Strebe, Ablage)
                    Kasten(r, b * 0.2f, t * 0.6f, 0.05f, 0.05f, 0, 1.6f * M, Farben.Holz);
                    Kasten(r, (b * 0.8f) - 0.05f, t * 0.6f, 0.05f, 0.05f, 0, 1.6f * M, Farben.Holz);
                    Kasten(r, (b * 0.5f) - 0.025f, 0, 0.05f, 0.05f, 0, 1.45f * M, Farben.Holz);
                    Kasten(r, (b * 0.5f) - 0.025f, 0.05f, 0.05f, (t * 0.6f) - 0.05f, 0.9f * M, 0.95f * M, Farben.Holz);
                    Kasten(r, b * 0.15f, t * 0.65f, b * 0.7f, 0.1f, 0.62f * M, 0.67f * M, Farben.Holz);
                    Kasten(r, b * 0.15f, t * 0.55f, b * 0.7f, 0.06f, 0.67f * M, 1.4f * M, farbe);
                    break;
                }
                Kasten(r, b * 0.1f, 0, b * 0.8f, Math.Min(t, 0.1f), 1.2f * M, Math.Min(wandOben, 1.6f * M), farbe);
                break;
            }
            case "wasserspender":
                Kasten(r, 0, 0, b, t, 0, 1.0f * M, Farben.Hell);
                Kasten(r, b * 0.15f, t * 0.15f, b * 0.7f, t * 0.7f, 1.0f * M, 1.35f * M, Farben.Wasser);
                break;
            case "palette":
                Kasten(r, 0, 0, b, t, 0, 0.15f * M, Farben.Holz);
                Modell(r, "cardboardBoxClosed", 0.08f, 0.08f, b - 0.16f, t - 0.16f, 0.7f * M, 0.15f * M, Farben.Karton);
                break;
            case "hubwagen":
                Kasten(r, 0, 0, b, t, 0, 0.12f * M, Farben.MetallDunkel);
                Kasten(r, 0, 0, b * 0.25f, t, 0.12f * M, 0.5f * M, Farben.Gelb);
                break;
            case "gleis":
            {
                // Gleisbett mit zwei Schienen laengs der langen Seite
                Kasten(r, 0, 0, b, t, 0.03f, 0.06f, Farben.Metall);
                var laengs = b >= t;
                for (var s = -1; s <= 1; s += 2)
                {
                    if (laengs)
                    {
                        Kasten(r, 0, (t / 2) + (s * 0.3f) - 0.04f, b, 0.08f, 0.06f, 0.12f, Farben.Geraet);
                    }
                    else
                    {
                        Kasten(r, (b / 2) + (s * 0.3f) - 0.04f, 0, 0.08f, t, 0.06f, 0.12f, Farben.Geraet);
                    }
                }
                break;
            }
            case "zug":
            {
                // Zug laengs: das Modell steht laengs z, also um eine Vierteldrehung, wenn das Rechteck breit ist
                var (x, z, w, tt) = r.Rechteck(0, 0, b, t);
                Modell("train-electric-city-a", x, z, w, tt, Math.Min(wandOben, 1.9f * M), w >= tt ? 1 : 0, 0.06f, Farben.Rahmen);
                break;
            }
            case "automat":
                Kasten(r, 0, 0, b, t, 0, 1.6f * M, Farben.MetallDunkel);
                Kasten(r, 0.08f, t - 0.02f, b - 0.16f, 0.04f, 0.9f * M, 1.4f * M, Farben.Bildschirm);
                break;
            case "absperrband":
            case "bauzaun":
            {
                var laengs = b >= t;
                var farbe = typ == "absperrband" ? Farben.Rot : Farben.Metall;
                // Sammelliste 347: hochkant (laenger als breit) laeuft das Band laengs der Tiefe wie im Grundriss; bis A 0.79.0 lag
                // es quer (Breite = Tiefe des Rechtecks) und ragte in Talheim-Nord durch die Wand
                if (laengs || WandAltFuerTests.Value)
                {
                    Kasten(r, 0, 0, laengs ? b : t, laengs ? Math.Min(t, 0.08f) : 0.08f, 0.85f * M, 0.95f * M, farbe);
                }
                else
                {
                    Kasten(r, 0, 0, Math.Min(b, 0.08f), t, 0.85f * M, 0.95f * M, farbe);
                }
                Kasten(r, 0, 0, 0.08f, 0.08f, 0, 1.0f * M, Farben.Geraet);
                Kasten(r, b - 0.08f, t - 0.08f, 0.08f, 0.08f, 0, 1.0f * M, Farben.Geraet);
                break;
            }
            case "aquarium":
                Kasten(r, 0, 0, b, t, 0, 0.7f * M, Farben.MetallDunkel);
                Kasten(r, 0.05f, 0.05f, b - 0.1f, t - 0.1f, 0.7f * M, 1.2f * M, Farben.Wasser);
                break;
            case "grill":
                Kasten(r, b * 0.15f, t * 0.15f, b * 0.7f, t * 0.7f, 0, 0.9f * M, Farben.Geraet);
                break;
            case "sonnenschirm":
                Kasten(r, (b / 2) - 0.05f, (t / 2) - 0.05f, 0.1f, 0.1f, 0, 2.1f * M, Farben.Metall);
                Kasten(r, 0, 0, b, t, 2.1f * M, 2.2f * M, Farben.Gelb);
                break;
            case "hochbeet":
                Kasten(r, 0, 0, b, t, 0, 0.6f * M, Farben.Holz);
                Kasten(r, 0.08f, 0.08f, b - 0.16f, t - 0.16f, 0.6f * M, 0.7f * M, Farben.Pflanze);
                break;
            case "spielkonsole":
                Kasten(r, 0, 0, b, t, 0, 0.12f * M, Farben.MetallDunkel);
                break;
            case "glaswand":
            {
                // Glas auf dem Tresen (P7-15): nur Rahmen und Glaskante, damit die Person dahinter sichtbar bleibt
                float unten = 1.05f * M, oben = 1.6f * M;
                Kasten(r, 0, 0, b, t, unten, unten + 0.04f, Farben.Glas);
                Kasten(r, 0, 0, b, t, oben - 0.05f, oben, Farben.Rahmen);
                var n = Math.Max(2, (int)MathF.Round(b / 1.1f));
                for (var i = 0; i <= n; i++)
                {
                    Kasten(r, (b - 0.06f) * i / n, 0, 0.06f, t, unten, oben, Farben.Rahmen);
                }
                // Durchreiche in der Mitte
                Kasten(r, (b / 2) - 0.4f, 0, 0.8f, t + 0.06f, unten, unten + 0.02f, Farben.MetallDunkel);
                break;
            }
            case "schliessfaecher":
            {
                var koerper = Netz.Farbe("#94A3B8");
                var tuer = Netz.Farbe("#CBD5E1");
                Kasten(r, 0, 0, b, t, 0, 1.7f * M, koerper);
                var spalten = Math.Max(2, (int)(b / 0.45f));
                var breite = b / spalten;
                var hoehe = (1.6f * M - 0.1f) / 5;
                for (var i = 0; i < spalten; i++)
                {
                    for (var j = 0; j < 5; j++)
                    {
                        var lx = i * breite;
                        var unten = 0.1f + (j * hoehe);
                        Kasten(r, lx + 0.03f, t - 0.01f, breite - 0.06f, 0.03f, unten + 0.03f, unten + hoehe - 0.03f, tuer);
                        Kasten(r, lx + breite - 0.12f, t, 0.05f, 0.03f, unten + (hoehe / 2) - 0.03f, unten + (hoehe / 2) + 0.03f, Farben.Gelb);
                    }
                }
                break;
            }
            case "tresor":
            {
                var stahl = Netz.Farbe("#475569");
                var hell = Netz.Farbe("#E2E8F0");
                Kasten(r, 0, 0, b, t, 0, 1.75f * M, stahl);
                Kasten(r, 0.15f, t - 0.01f, b - 0.3f, 0.04f, 0.15f * M, 1.6f * M, Netz.Farbe("#64748B"));
                // Rad in der Mitte (Kreuz mit Nabe) und Scharniere links
                var mitte = 0.9f * M;
                Kasten(r, (b / 2) - 0.35f, t + 0.02f, 0.7f, 0.04f, mitte - 0.03f, mitte + 0.03f, hell);
                Kasten(r, (b / 2) - 0.03f, t + 0.02f, 0.06f, 0.04f, mitte - 0.35f, mitte + 0.35f, hell);
                Kasten(r, (b / 2) - 0.1f, t + 0.03f, 0.2f, 0.05f, mitte - 0.1f, mitte + 0.1f, Farben.Gelb);
                Kasten(r, 0.12f, t, 0.06f, 0.06f, 0.4f * M, 0.6f * M, hell);
                Kasten(r, 0.12f, t, 0.06f, 0.06f, 1.15f * M, 1.35f * M, hell);
                break;
            }
            case "geldautomat":
                Kasten(r, 0, 0, b, t, 0, 1.5f * M, Netz.Farbe("#334155"));
                Kasten(r, 0, 0, b, t, 1.5f * M, 1.7f * M, Netz.Farbe("#2563EB"));
                Kasten(r, 0.12f, t - 0.02f, b - 0.24f, 0.04f, 1.0f * M, 1.35f * M, Farben.Bildschirm);
                Kasten(r, 0.1f, t - 0.02f, b - 0.2f, 0.14f, 0.85f * M, 0.92f * M, Netz.Farbe("#94A3B8"));
                Kasten(r, 0.25f, t - 0.01f, b - 0.5f, 0.03f, 0.7f * M, 0.75f * M, Farben.Geraet);
                break;
            case "wartemarken":
                Kasten(r, 0.15f, 0.1f, b - 0.3f, t - 0.2f, 0, 1.0f * M, Netz.Farbe("#B45309"));
                Kasten(r, 0, 0, b, t, 1.0f * M, 1.5f * M, Netz.Farbe("#F59E0B"));
                Kasten(r, 0.1f, t - 0.01f, b - 0.2f, 0.03f, 1.2f * M, 1.42f * M, Farben.Bildschirm);
                Kasten(r, 0.2f, t - 0.01f, b - 0.4f, 0.04f, 1.06f * M, 1.1f * M, Farben.Weiss);
                break;
            case "wappen":
            {
                var gold = Netz.Farbe("#FBBF24");
                var tief = Math.Min(t, 0.1f);
                Kasten(r, b * 0.15f, 0, b * 0.7f, tief, 1.0f * M, 1.5f * M, gold);
                Kasten(r, b * 0.32f, 0, b * 0.36f, tief, 0.9f * M, 1.0f * M, gold);
                Kasten(r, b * 0.25f, 0, b * 0.5f, tief + 0.02f, 1.08f * M, 1.42f * M, Netz.Farbe("#B91C1C"));
                Kasten(r, b * 0.45f, 0, b * 0.1f, tief + 0.03f, 1.12f * M, 1.38f * M, Farben.Geraet);
                break;
            }
            case "fahne":
            {
                // Fahne im Staender: Fuss, Mast, Tuch in drei Streifen
                Kasten(r, 0, 0, b, t, 0, 0.06f * M, Netz.Farbe("#475569"));
                Kasten(r, 0.06f, (t / 2) - 0.03f, 0.06f, 0.06f, 0, 1.75f * M, Farben.Metall);
                var streifen = new[] { Farben.Geraet, Netz.Farbe("#DC2626"), Netz.Farbe("#FACC15") };
                for (var i = 0; i < 3; i++)
                {
                    var oben = (1.68f - (i * 0.22f)) * M;
                    Kasten(r, 0.12f, (t / 2) - 0.01f, b - 0.14f, 0.02f, oben - (0.22f * M), oben, streifen[i]);
                }
                break;
            }
            default:
                Kasten(r, 0, 0, b, t, 0, 0.6f * M, Farben.Karton);
                break;
        }
    }
}
