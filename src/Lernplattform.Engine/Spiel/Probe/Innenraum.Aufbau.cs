using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Engine festigen 3 (A 1.16.0, Weisung P12-395/P12-399): Teil von <see cref="Innenraum"/> mit Boeden, Waenden, Treppen und
/// Tischen. Reiner Umzug aus Innenraum.cs, nichts geaendert.
/// </summary>
public sealed partial class Innenraum
{
    private void Aufbauen(PyObjekt? boeden)
    {
        var signatur = new List<string>();
        WandFarbe = Hex(Gebaeude, "wandfarbe") ?? Farben.Wand;
        AussenFarbe = Hex(Gebaeude, "aussenfarbe") ?? Farben.Aussen;
        // Wiese um das Haus, damit die geneigte Kamera keinen leeren Himmel zeigt
        _wiese = _teile.Count;
        _teile.Add(Quader(-20, -0.08f, -20, Breite + 20, -0.02f, Tiefe + 20, Mischen(Farben.Wiese, Vector3.Zero, 0.1f)));
        var flur = Objekt(Gebaeude["flur"]);
        _teile.Add(Quader(0, -0.02f, 0, Breite, 0.02f, Tiefe, Farben.Boden));
        _bodenFarben["flur"] = Flaeche(0, (float)Py.F(flur["y"]), Breite, (float)Py.F(flur["h"]), Get(flur, "boden") as string, null, null, 0.03f, Hex(flur, "bodenfarbe"));
        foreach (var raum in Liste(Gebaeude["raeume"]).Select(Objekt))
        {
            var boden = Get(raum, "boden") as string;
            Vector3? akzent = Get(raum, "farbe") is string hex ? Netz.Farbe(hex) : null;
            Vector3? wahl = null;
            if (boeden is not null && Get(boeden, Py.Str(raum["id"])) is List<object?> eintrag && eintrag.Count > 1)
            {
                wahl = BodenAkzent(eintrag[1]);
            }
            _bodenFarben[Py.Str(raum["id"])] = Flaeche((float)Py.F(raum["x"]), (float)Py.F(raum["y"]), (float)Py.F(raum["w"]), (float)Py.F(raum["h"]), boden, akzent, wahl, 0.035f, Hex(raum, "bodenfarbe"));
        }
        // Innenwaende wie fisi_game._segments (ohne die vier Aussenwaende am Ende)
        var stuecke = Wege.Wandstuecke(Gebaeude);
        foreach (var (x1, y1, x2, y2) in stuecke.Take(stuecke.Count - 4))
        {
            Wand((float)x1, (float)y1, (float)x2, (float)y2);
        }
        // Welle A 1.6.0 (Sammelliste 443): Zarge und Sturz jeder Raumtuer (das Blatt zeichnet TuerBlattNetz)
        RaumTuerenBauen();
        // Phase 13 Welle 5: ein Obergeschoss hat keine Eingangstuer
        Aussenwaende(Get(flur, "eingang") as PyObjekt);
        var deko = Liste(Gebaeude["raeume"]).Select(Objekt).SelectMany(r => ListeOderLeer(r, "deko"))
            .Concat(ListeOderLeer(flur, "deko")).Select(Objekt);
        if (Fest is not null && Ort == SpielKern.KleidungLaden)
        {
            // Phase 8 W6: die ersten Puppen im Flur tragen die Festkleidung
            _festPuppen = [.. ListeOderLeer(flur, "deko").Select(Objekt).Where(d => Py.Gleich(d["typ"], "puppe")).Take(FestDeko.Puppen(_festDaten))];
        }
        var dekoListe = deko.ToList();
        _alleDeko = dekoListe;
        foreach (var d in dekoListe)
        {
            var (teileVorher, stueckeVorher) = (_teile.Count, _stuecke.Count);
            _dekoJetzt = d;
            Deko(d);
            // Welle A 0.81.0 (Sammelliste 349): Plaetze zum Sitzen und Liegen dieses Stuecks
            SitzeMerken(stueckeVorher);
            _dekoJetzt = null;
            _dekoUmrisse.Add(new DekoUmriss(d, _teile.Skip(teileVorher).Concat(StueckeAlsTeile(_stuecke.Skip(stueckeVorher))).ToList())
            {
                Kaesten = [.. _teile.Skip(teileVorher)],
                Modelle = [.. _stuecke.Skip(stueckeVorher)],
            });
            signatur.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{d["typ"]}@{Py.F(d["x"])},{Py.F(d["y"])},{Py.Int(Get(d, "dreh", 0L))}"));
        }
        if (_festPlan is not null && _festMuster is not null)
        {
            var (festTeile, festStuecke) = (_teile.Count, _stuecke.Count);
            FestDeko.InnenZeichnen(_festPlan, _festMuster, _teile, _stuecke, Grafik, Lauf.Basis, WandAltFuerTests.Value ? null : FeuerwerkHalbmesser);
            FestKaesten = [.. _teile.Skip(festTeile)];
            FestModelle = [.. _stuecke.Skip(festStuecke)];
        }
        // Welle A 0.81.0 (Sammelliste 349): Plaetze, an denen die Figur ein anderes Stueck beruehrte, gibt es nicht
        SitzePruefen();
        if (Fest is not null)
        {
            // Phase 8 W6: nur angehaengt, damit ein Wechsel der Festzeit neu laedt
            signatur.Add("fest:" + Fest + string.Concat(_festPlan?.Plaetze.Select(p => string.Create(System.Globalization.CultureInfo.InvariantCulture, $",{p.Platz}@{p.X}/{p.Z}")) ?? []));
        }
        Signatur = string.Join(";", signatur);
    }

    /// <summary>
    /// Boden einer Flaeche: Art (parkett, teppichboden, fliesen, beton), Raumfarbe als Hauch, Fliesen mit
    /// Fugen; <paramref name="fest"/> (bodenfarbe) ersetzt die Farbe der Art. Gibt die Bodenfarbe zurueck.
    /// </summary>
    private Vector3 Flaeche(float x, float z, float w, float t, string? boden, Vector3? raumFarbe, Vector3? wahl, float hoehe, Vector3? fest = null)
    {
        var farbe = boden switch
        {
            "parkett" => Farben.Parkett,
            "teppichboden" => raumFarbe is { } r ? Mischen(Farben.Teppich, r, 0.35f) : Farben.Teppich,
            "fliesen" => Farben.Fliesen,
            "beton" => Farben.Beton,
            _ => raumFarbe is { } r2 ? Mischen(Farben.Boden, r2, 0.18f) : Farben.Boden,
        };
        if (fest is { } eigene)
        {
            farbe = eigene;
        }
        if (wahl is { } akzent)
        {
            farbe = Mischen(farbe, akzent, 0.3f);
        }
        _teile.Add(Quader(x, 0.02f, z, x + w, hoehe, z + t, farbe));
        if (boden == "fliesen")
        {
            for (var fx = x + 0.6f; fx < x + w - 0.05f; fx += 0.6f)
            {
                _teile.Add(Quader(fx - 0.012f, hoehe, z, fx + 0.012f, hoehe + 0.002f, z + t, fest is null ? Farben.Fuge : Mischen(farbe, Vector3.Zero, 0.12f)));
            }
            for (var fz = z + 0.6f; fz < z + t - 0.05f; fz += 0.6f)
            {
                _teile.Add(Quader(x, hoehe, fz - 0.012f, x + w, hoehe + 0.002f, fz + 0.012f, fest is null ? Farben.Fuge : Mischen(farbe, Vector3.Zero, 0.12f)));
            }
        }
        else if (boden == "parkett")
        {
            for (var fz = z + 0.35f; fz < z + t - 0.05f; fz += 0.35f)
            {
                _teile.Add(Quader(x, hoehe, fz - 0.008f, x + w, hoehe + 0.002f, fz + 0.008f, Mischen(farbe, Vector3.Zero, 0.15f)));
            }
        }
        return farbe;
    }

    private void Wand(float x1, float z1, float x2, float z2)
    {
        if (x2 - x1 < 1e-4f || z2 - z1 < 1e-4f)
        {
            return;
        }
        _teile.Add(Quader(x1, 0, z1, x2, WandHoehe - 0.06f, z2, WandFarbe));
        _teile.Add(Quader(x1, WandHoehe - 0.06f, z1, x2, WandHoehe, z2, WandFarbe == Farben.Wand ? Farben.WandKante : Mischen(WandFarbe, Vector3.Zero, 0.2f)));
        _waende.Add(new InnenWand(_teile.Count - 2, _teile.Count, new Vector3(x1, 0, z1), new Vector3(x2, WandHoehe, z2), Vector2.Zero));
    }

    /// <summary>Aussenwaende mit Eingang (links im Flur), Fenstern (Glas ab Bruestung) und Rolltor.</summary>
    private void Aussenwaende(PyObjekt? eingang)
    {
        const float d = 0.15f;
        var fenster = ListeOderLeer(Gebaeude, "fenster").Select(Liste).Select(f => (X1: (float)Py.F(f[0]), Y1: (float)Py.F(f[1]), X2: (float)Py.F(f[2]), Y2: (float)Py.F(f[3]))).ToList();
        var tore = ListeOderLeer(Gebaeude, "rolltor").Select(Liste).Select(f => (X1: (float)Py.F(f[0]), Y1: (float)Py.F(f[1]), X2: (float)Py.F(f[2]), Y2: (float)Py.F(f[3]))).ToList();
        // je Seite: Laenge und Oeffnungen (von, bis, art)
        void Seite(bool waagerecht, float fest, float laenge, float innenSeite)
        {
            var oeffnungen = new List<(float Von, float Bis, char Art)>();
            foreach (var f in fenster)
            {
                if (waagerecht && Math.Abs(f.Y1 - fest) < 0.01f && Math.Abs(f.Y2 - fest) < 0.01f)
                {
                    oeffnungen.Add((Math.Min(f.X1, f.X2), Math.Max(f.X1, f.X2), 'f'));
                }
                else if (!waagerecht && Math.Abs(f.X1 - fest) < 0.01f && Math.Abs(f.X2 - fest) < 0.01f)
                {
                    oeffnungen.Add((Math.Min(f.Y1, f.Y2), Math.Max(f.Y1, f.Y2), 'f'));
                }
            }
            foreach (var r in tore)
            {
                if (!waagerecht && Math.Abs(r.X1 - fest) < 0.01f)
                {
                    oeffnungen.Add((Math.Min(r.Y1, r.Y2), Math.Max(r.Y1, r.Y2), 'r'));
                }
                else if (waagerecht && Math.Abs(r.Y1 - fest) < 0.01f)
                {
                    oeffnungen.Add((Math.Min(r.X1, r.X2), Math.Max(r.X1, r.X2), 'r'));
                }
            }
            if (!waagerecht && fest == 0 && eingang is not null)
            {
                oeffnungen.Add(((float)Py.F(eingang["von"]), (float)Py.F(eingang["bis"]), 't'));
                Ausgang = ((float)Py.F(eingang["von"]), (float)Py.F(eingang["bis"]));
            }
            oeffnungen.Sort((a, b) => a.Von.CompareTo(b.Von));
            var a0 = innenSeite < 0 ? fest - d : fest - d;
            var a1 = fest + d;
            void Stueck(float von, float bis, float unten, float oben, Vector3 farbe)
            {
                if (bis - von < 1e-3f || oben - unten < 1e-3f)
                {
                    return;
                }
                _teile.Add(waagerecht ? Quader(von, unten, a0, bis, oben, a1, farbe) : Quader(a0, unten, von, a1, oben, bis, farbe));
            }
            var pos = waagerecht ? -d : 0f;
            var ende = waagerecht ? laenge + d : laenge;
            var erstes = _teile.Count;
            foreach (var (von, bis, art) in oeffnungen)
            {
                Stueck(pos, Math.Max(pos, von), 0, WandHoehe, AussenFarbe);
                switch (art)
                {
                    case 'f':
                        Stueck(von, bis, 0, 0.9f * Meter, AussenFarbe);
                        Stueck(von, bis, 0.9f * Meter, WandHoehe - 0.12f, Farben.Glas);
                        Stueck(von, bis, WandHoehe - 0.12f, WandHoehe, Farben.Rahmen);
                        break;
                    case 't' when !OhneTueren.Value:
                        // Welle A 1.6.0 (Sammelliste 443): der Ausgang ist eine geschlossene Tuer (hinaus nur ueber die Handlung)
                        AusgangTuer(von, bis, AussenFarbe, a0, a1);
                        break;
                    case 'r':
                        Stueck(von, bis, 0, WandHoehe, Farben.Tor);
                        for (var h = 0.25f; h < WandHoehe; h += 0.25f)
                        {
                            Stueck(von, bis, h - 0.02f, h, Mischen(Farben.Tor, Vector3.Zero, 0.25f));
                        }
                        break;
                }
                pos = Math.Max(pos, bis);
            }
            Stueck(pos, ende, 0, WandHoehe, AussenFarbe);
            // die ganze Seite als ein Wandstueck; nach draussen zeigt -innenSeite (oben/links: innenSeite 1)
            var aussen = waagerecht ? new Vector2(0, -innenSeite) : new Vector2(-innenSeite, 0);
            _waende.Add(new InnenWand(erstes, _teile.Count,
                waagerecht ? new Vector3(-d, 0, a0) : new Vector3(a0, 0, 0),
                waagerecht ? new Vector3(laenge + d, WandHoehe, a1) : new Vector3(a1, WandHoehe, laenge), aussen));
        }
        Seite(true, 0, Breite, 1);
        Seite(true, Tiefe, Breite, -1);
        Seite(false, 0, Tiefe, 1);
        Seite(false, Breite, Tiefe, -1);
    }

    // ---- Einrichtung -------------------------------------------------------

    /// <summary>
    /// Lage eines Einrichtungsstuecks: Rechteck im Grundriss (x, z, w, t, wie Python schon gedreht) und
    /// Vierteldrehungen im Uhrzeigersinn (Python "dreh"; dreh 0 = Vorderseite nach +z). Rechnet Stellen
    /// im ungedrehten Stueck (lx, lz) in den Grundriss um wie fisi_game._rotate_shape.
    /// </summary>
    private readonly record struct Rahmen(float X, float Z, float W, float T, int Dreh)
    {
        public float B0 => Dreh % 2 == 1 ? T : W;

        public float T0 => Dreh % 2 == 1 ? W : T;

        public Vector2 Punkt(float lx, float lz)
        {
            float b = B0, t = T0;
            for (var i = 0; i < Dreh; i++)
            {
                (lx, lz) = (t - lz, lx);
                (b, t) = (t, b);
            }
            return new Vector2(X + lx, Z + lz);
        }

        public (float X, float Z, float W, float T) Rechteck(float lx, float lz, float lw, float lt)
        {
            var a = Punkt(lx, lz);
            var b = Punkt(lx + lw, lz + lt);
            return (Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
        }
    }

    /// <summary>Ein CC0-Modell in das Rechteck (x, z, w, t) gestreckt, <paramref name="hoehe"/> hoch, ab <paramref name="unten"/>.</summary>
    private void Modell(string name, float x, float z, float w, float t, float hoehe, int dreh, float unten, Vector3 ersatz)
    {
        dreh = ((dreh % 4) + 4) % 4;
        if (Grafik?.Modell(name) is not { } gm)
        {
            _teile.Add(Quader(x, unten, z, x + w, unten + hoehe, z + t, ersatz));
            return;
        }
        var (b0, t0) = dreh % 2 == 1 ? (t, w) : (w, t);
        var g = gm.Max - gm.Min;
        var lage = Matrix4x4.CreateTranslation(-gm.Min)
            * Matrix4x4.CreateScale(b0 / MathF.Max(g.X, 1e-4f), hoehe / MathF.Max(g.Y, 1e-4f), t0 / MathF.Max(g.Z, 1e-4f))
            * Matrix4x4.CreateTranslation(-b0 / 2, 0, -t0 / 2)
            * Matrix4x4.CreateRotationY(-MathF.PI / 2 * dreh)
            * Matrix4x4.CreateTranslation(x + (w / 2), unten, z + (t / 2));
        _stuecke.Add(new ModellStueck(name, lage));
    }

    /// <summary>Modell an einer Stelle des ungedrehten Stuecks (Rahmen r), zusaetzlich um <paramref name="mehr"/> Vierteldrehungen.</summary>
    private void Modell(Rahmen r, string name, float lx, float lz, float lw, float lt, float hoehe, float unten, Vector3 ersatz, int mehr = 0)
    {
        var (x, z, w, t) = r.Rechteck(lx, lz, lw, lt);
        Modell(name, x, z, w, t, hoehe, r.Dreh + mehr, unten, ersatz);
    }

    /// <summary>Gegenprobe (Weisung P12-74): Treppe wie in Welle W5 zuerst gebaut (Pfosten in der Stufe, oben wie unten).</summary>
    internal static readonly AsyncLocal<bool> TreppeAltFuerTests = new();

    /// <summary>
    /// Sammelliste 347: groesster Halbmesser eines Feuerwerks an (x, z), damit keine Funken durch eine Wand ragen
    /// (9/10 des waagerechten Abstands zum naechsten Wandquader; die Funken selbst sind kleine Wuerfel).
    /// </summary>
    private float FeuerwerkHalbmesser(float x, float z)
    {
        if (x <= 0 || z <= 0 || x >= Breite || z >= Tiefe)
        {
            return 0f;
        }
        var naechster = MathF.Min(MathF.Min(x, Breite - x), MathF.Min(z, Tiefe - z));
        foreach (var w in _waende)
        {
            for (var i = w.Von; i < w.Bis; i++)
            {
                var t = _teile[i];
                var dx = MathF.Max(MathF.Max(MathF.Min(t.X1, t.X2) - x, 0), x - MathF.Max(t.X1, t.X2));
                var dz = MathF.Max(MathF.Max(MathF.Min(t.Z1, t.Z2) - z, 0), z - MathF.Max(t.Z1, t.Z2));
                naechster = MathF.Min(naechster, MathF.Sqrt((dx * dx) + (dz * dz)));
            }
        }
        return naechster * 0.9f;
    }

    /// <summary>Sammelliste 347, Gegenprobe: wie bis A 0.79.0 (Stuehle am Ess- und Besprechungstisch immer auf beiden Seiten, Bauzaun und Absperrband hochkant quer, Feuerwerk innen in voller Groesse).</summary>
    internal static readonly AsyncLocal<bool> WandAltFuerTests = new();

    /// <summary>Abstand von Gelaender und Pfosten zum Rand der Treppe; die Stufen beginnen <see cref="TreppeSeite"/> innen.</summary>
    internal const float TreppeSeite = 0.1f;

    /// <summary>Hoehe des Handlaufs ueber dem Boden einer Treppe nach unten (0,9 m).</summary>
    internal const float GelaenderHoehe = 0.9f * Meter;

    /// <summary>
    /// Treppe nach oben (Phase 13 Welle 5): Stufen von vorn (unten) nach hinten (oben). Weisung P12-74: Pfosten und
    /// Gelaender stehen neben den Stufen (seitlich <see cref="TreppeSeite"/>), der Handlauf oben liegt auf den Pfosten.
    /// </summary>
    private void TreppeNachOben(Rahmen r, float b, float tt)
    {
        const int stufen = 8;
        var oben = WandHoehe * 0.95f;
        var tiefe = tt / stufen;
        if (TreppeAltFuerTests.Value)
        {
            for (var i = 0; i < stufen; i++)
            {
                Kasten(r, 0, tt - ((i + 1) * tiefe), b, tiefe, 0, (i + 1) * oben / stufen, i % 2 == 0 ? Farben.Holz : Farben.HolzDunkel);
            }
            foreach (var seite in new[] { 0f, b - 0.08f })
            {
                Kasten(r, seite, tt - 0.1f, 0.08f, 0.08f, 0, 0.9f * Meter, Farben.Metall);
                Kasten(r, seite, 0, 0.08f, tt, oben, oben + 0.08f, Farben.Metall);
            }
            return;
        }
        for (var i = 0; i < stufen; i++)
        {
            Kasten(r, TreppeSeite, tt - ((i + 1) * tiefe), b - (2 * TreppeSeite), tiefe, 0, (i + 1) * oben / stufen, i % 2 == 0 ? Farben.Holz : Farben.HolzDunkel);
        }
        foreach (var seite in new[] { 0.01f, b - 0.09f })
        {
            // Pfosten vorn und hinten bis unter den Handlauf, daneben (nie in) den Stufen
            Kasten(r, seite, tt - 0.09f, 0.08f, 0.08f, 0, oben, Farben.Metall);
            Kasten(r, seite, 0.01f, 0.08f, 0.08f, 0, oben, Farben.Metall);
            Kasten(r, seite, 0, 0.08f, tt, oben, oben + 0.08f, Farben.Metall);
        }
    }

    /// <summary>
    /// Weisung P12-74: Treppe, die nur nach unten fuehrt (oberster Stock): Treppenloch im Boden mit Einfassung, darin
    /// Stufen, die von vorn (Zugang auf der Seite der Ankunft) nach hinten immer dunkler in die Tiefe fuehren; Gelaender
    /// an den drei Seiten ohne Zugang. Die Flaeche bleibt im Laufraster gesperrt wie jedes Moebel.
    /// </summary>
    private void TreppeNachUnten(Rahmen r, float b, float tt)
    {
        const int stufen = 8;
        const float rand = 0.045f;
        const float einfassung = 0.06f;
        // Einfassung (Rand des Lochs) und dunkler Schacht
        Kasten(r, 0, 0, b, tt, 0, rand + 0.01f, Farben.Schacht);
        var innen = tt - TreppeSeite;
        var tiefe = innen / stufen;
        for (var i = 0; i < stufen; i++)
        {
            // Stufe i (0 = vorn am Zugang) wird nach hinten dunkler: so sieht man, dass es hinunter geht
            var farbe = Vector3.Lerp(i % 2 == 0 ? Farben.Holz : Farben.HolzDunkel, Vector3.Zero, 0.15f + (0.8f * i / (stufen - 1)));
            Kasten(r, TreppeSeite, tt - ((i + 1) * tiefe), b - (2 * TreppeSeite), tiefe, rand + 0.01f, rand + einfassung - (0.004f * i), farbe);
        }
        var boden = rand + 0.01f;
        foreach (var seite in new[] { 0.01f, b - 0.09f })
        {
            Kasten(r, seite, tt - 0.09f, 0.08f, 0.08f, boden, GelaenderHoehe, Farben.Metall);
            Kasten(r, seite, 0.01f, 0.08f, 0.08f, boden, GelaenderHoehe, Farben.Metall);
            Kasten(r, seite, 0.01f, 0.08f, tt - 0.02f, GelaenderHoehe, GelaenderHoehe + 0.08f, Farben.Metall);
            Kasten(r, seite, 0.01f, 0.08f, tt - 0.02f, GelaenderHoehe * 0.5f, (GelaenderHoehe * 0.5f) + 0.05f, Farben.Metall);
        }
        // hinten quer (gegenueber dem Zugang)
        Kasten(r, 0.09f, 0.01f, b - 0.18f, 0.08f, GelaenderHoehe, GelaenderHoehe + 0.08f, Farben.Metall);
        Kasten(r, 0.09f, 0.01f, b - 0.18f, 0.08f, GelaenderHoehe * 0.5f, (GelaenderHoehe * 0.5f) + 0.05f, Farben.Metall);
    }

    /// <summary>Welle A 0.99.0: Kaesten der Werkstatt-Teile im Rahmen <paramref name="r"/>.</summary>
    private KastenSetzen KastenIn(Rahmen r) => (lx, lz, lw, lt, unten, oben, farbe) => Kasten(r, lx, lz, lw, lt, unten, oben, farbe);

    private void Kasten(Rahmen r, float lx, float lz, float lw, float lt, float unten, float oben, Vector3 farbe)
    {
        var (x, z, w, t) = r.Rechteck(lx, lz, lw, lt);
        _teile.Add(Quader(x, unten, z, x + w, oben, z + t, farbe));
    }

    /// <summary>Stuecke an der Wand ohne eigene Drehung: Rueckseite zur naechsten Wand des Raums.</summary>
    private static readonly HashSet<string> AnDerWand =
    [
        "regal", "aktenregal", "hochregal", "kleiderschrank", "kuechenzeile", "kuehlschrank", "waschmaschine", "fernseher",
        "kommode", "sofa", "bett", "matratze", "tresen", "waschbecken", "wc", "badewanne", "kaffeemaschine", "usv", "rack",
        "klima", "drucker", "automat", "wasserspender", "monitorwand", "anzeigetafel", "whiteboard", "bild", "aquarium",
        "werkbank", "leitstand", "bank", "glaswand", "kleiderregal", "spiegel", "schliessfaecher", "tresor", "geldautomat", "wartemarken", "wappen", "gemaelde",
    ];

    /// <summary>Sammelliste 347: beruehrt das Rechteck (x, z, w, t) im Grundriss einen Wandquader oder liegt es ausserhalb des Hauses?</summary>
    private bool InWand((float X, float Z, float W, float T) f)
    {
        if (f.X < 0 || f.Z < 0 || f.X + f.W > Breite || f.Z + f.T > Tiefe)
        {
            return true;
        }
        foreach (var w in _waende)
        {
            for (var i = w.Von; i < w.Bis; i++)
            {
                var t = _teile[i];
                if (f.X < MathF.Max(t.X1, t.X2) && MathF.Min(t.X1, t.X2) < f.X + f.W && f.Z < MathF.Max(t.Z1, t.Z2) && MathF.Min(t.Z1, t.Z2) < f.Z + f.T)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Sammelliste 380 (Weisung P12-298), Gegenprobe: Betten und Matratzen ohne eigene Drehung wie bis A 0.89.0 (Kopfteil an
    /// der langen Seite, das Bett wirkte breiter als lang und die Figur lag quer).
    /// </summary>
    internal static readonly AsyncLocal<bool> BettQuerAltFuerTests = new();

    /// <summary>Sammelliste 380: Liegemoebel, deren Kopfteil (Rueckseite des Modells) an der kurzen Seite steht.</summary>
    private static readonly HashSet<string> Liegemoebel = ["bett", "matratze"];

    /// <summary>Welle A 1.0.0 (Sammelliste 408), Gegenprobe: Tisch ueber einem PC wie bis A 0.99.0 (Modell mit Querstreben).</summary>
    internal static readonly AsyncLocal<bool> PcTischAltFuerTests = new();

    /// <summary>Beinstaerke und Abstand der Beine vom Rand eines Tischs ohne Streben.</summary>
    internal const float TischBein = 0.07f, TischBeinRand = 0.04f;

    /// <summary>Steht ein PC (Grundriss) unter dem Rechteck (x, z, w, t)?</summary>
    private bool PcDarunter(float x, float z, float w, float t) => Darunter("pc", x, z, w, t);

    /// <summary>Steht ein Stueck vom Typ <paramref name="typ"/> (Grundriss) unter dem Rechteck (x, z, w, t)?</summary>
    private bool Darunter(string typ, float x, float z, float w, float t) => _alleDeko.Any(d => Py.Gleich(d["typ"], typ)
        && Py.F(d["x"]) < x + w && x < Py.F(d["x"]) + Py.F(d["w"]) && Py.F(d["y"]) < z + t && z < Py.F(d["y"]) + Py.F(d["h"]));

    /// <summary>
    /// Welle A 1.0.0 (Sammelliste 408): Tisch aus Platte, Zarge und vier Beinen an den Ecken, darunter ganz frei (Platz fuer
    /// PC-Tower). Hoehe und Farbe wie das Modell „table“ (#D4A76C, eine Farbe), damit der Stil bleibt.
    /// </summary>
    private static readonly Vector3 TischHolz = Netz.Farbe("#D4A76C");

    private void TischOhneStreben(Rahmen r, float b, float t, float hoehe)
    {
        const float platte = 0.05f, zarge = 0.08f;
        Kasten(r, 0, 0, b, t, hoehe - platte, hoehe, TischHolz);
        var unten = hoehe - platte - zarge;
        Kasten(r, TischBeinRand, TischBeinRand, b - (2 * TischBeinRand), 0.03f, unten, hoehe - platte, TischHolz);
        Kasten(r, TischBeinRand, t - TischBeinRand - 0.03f, b - (2 * TischBeinRand), 0.03f, unten, hoehe - platte, TischHolz);
        Kasten(r, TischBeinRand, TischBeinRand, 0.03f, t - (2 * TischBeinRand), unten, hoehe - platte, TischHolz);
        Kasten(r, b - TischBeinRand - 0.03f, TischBeinRand, 0.03f, t - (2 * TischBeinRand), unten, hoehe - platte, TischHolz);
        foreach (var (bx, bz) in new[] { (TischBeinRand, TischBeinRand), (b - TischBeinRand - TischBein, TischBeinRand), (TischBeinRand, t - TischBeinRand - TischBein), (b - TischBeinRand - TischBein, t - TischBeinRand - TischBein) })
        {
            Kasten(r, bx, bz, TischBein, TischBein, 0, hoehe - platte, TischHolz);
        }
    }
}
