using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Patch A 0.29.1 (Weisung P10-3, Nico 30.10.2026 "Nach und nach, unsichtbar"): aendert sich in der Stadt mit der Uhr
/// der Satz (<see cref="FestVerkehr.Fuer"/>, z. B. Halloween ab der Daemmerung), tauschen Autos und Gestalten einzeln,
/// ohne Hausbesuch. Nur Darstellung wie <see cref="Verkehr"/> (kein Ereignis, kein Spielstand).
/// <para>
/// Der neue Satz faehrt vom Beginn an unsichtbar mit (<see cref="NeuVerkehr"/>, ab seinen festen Startplaetzen wie beim
/// Hinausgehen, untereinander mit seinen Regeln); so behaelt er die Abstaende seiner Strecken, auch nach dem Uebergang.
/// Sichtbar ist der gemischte <see cref="Verkehr"/> (dieselben Regeln, Abstand zum Vordermann das groessere der beiden
/// Saetze). Ein altes Fahrzeug geht hinaus, ein neues kommt herein:
/// </para>
/// <list type="bullet">
/// <item>ausserhalb des Sichtkegels (mit <see cref="Rand"/>), das neue nur auf einen freien Platz (<see cref="Grund.Ausserhalb"/>),</item>
/// <item>am Kartenrand: das alte am Ende seiner Strecke, wo es ohnehin hinausfaehrt (<see cref="Grund.Rundlauf"/>), das
/// neue, wenn es am Anfang seiner Strecke wieder einfaehrt und der Platz frei ist (<see cref="Grund.Einfahrt"/>),</item>
/// <item>sonst spaetestens nach <see cref="Hoechstzeit"/> Sekunden Fahrzeit (<see cref="Grund.Hoechstzeit"/>),</item>
/// <item>sofort alle, wenn niemand die Strassen sieht (Figur im Haus) oder die Uhr sich erneut aendert
/// (<see cref="Abschliessen"/>, <see cref="Grund.Sofort"/>).</item>
/// </list>
/// </summary>
public sealed class FestUebergang
{
    /// <summary>Spaetestens nach so vielen Sekunden Fahrzeit sind alle Strecken getauscht (die langsamste Gestalt, das
    /// Huhn mit 0,6, braucht fuer die laengste Strecke von rund 53 Einheiten etwa 89 s bis zum Kartenrand).</summary>
    public const double Hoechstzeit = 120;

    /// <summary>Rand um die Mitte eines Fahrzeugs (Karteneinheiten), mit dem es noch als im Bild gilt.</summary>
    public const float Rand = 1.0f;

    /// <summary>Warum ein Fahrzeug hinausging (alter Satz) bzw. hereinkam (neuer Satz).</summary>
    public enum Grund { Ausserhalb, Rundlauf, Einfahrt, Hoechstzeit, Sofort }

    /// <summary>Ein Tausch: Strecke, neu (true) oder alt (false), Grund, Fahrzeit seit Beginn.</summary>
    public sealed record Tausch(int Strecke, bool Neu, Grund Grund, double Fahrzeit);

    /// <summary>Nach dieser Strecke am Ende gilt ein altes Fahrzeug als am Kartenrand (Front 0,3 und etwas Rand).</summary>
    private const float AmEnde = 0.4f;

    private readonly Verkehr _schatten;
    private readonly List<Verkehr.Auto> _altOffen;
    private readonly List<Tausch> _tausche = [];

    public FestUebergang(FestVerkehr.Satz alt, Verkehr altVerkehr, FestVerkehr.Satz neu, Verkehr neuVerkehr)
    {
        Alt = alt;
        Neu = neu;
        AltVerkehr = altVerkehr;
        NeuVerkehr = neuVerkehr;
        Verkehr = new Verkehr { AbstandVorne = MathF.Max(altVerkehr.AbstandVorne, neuVerkehr.AbstandVorne) };
        Verkehr.Kreuzungen.AddRange(neuVerkehr.Kreuzungen);
        Verkehr.Autos.AddRange(altVerkehr.Autos.OrderBy(a => a.Nummer));
        Verkehr.Bedient(altVerkehr.FahrenBis - Verkehr.Nachlauf);
        _altOffen = [.. Verkehr.Autos];
        _schatten = new Verkehr { AbstandVorne = neuVerkehr.AbstandVorne };
        _schatten.Kreuzungen.AddRange(neuVerkehr.Kreuzungen);
        _schatten.Autos.AddRange(neuVerkehr.Autos);
    }

    /// <summary>Satz vor dem Uebergang.</summary>
    public FestVerkehr.Satz Alt { get; }

    /// <summary>Satz nach dem Uebergang.</summary>
    public FestVerkehr.Satz Neu { get; }

    /// <summary>Verkehr des alten Satzes (seine Fahrzeuge fahren im gemischten Verkehr weiter, bis sie hinausgehen).</summary>
    public Verkehr AltVerkehr { get; }

    /// <summary>Verkehr des neuen Satzes; seine Fahrzeuge fahren unsichtbar mit, bis sie hereinkommen.</summary>
    public Verkehr NeuVerkehr { get; }

    /// <summary>Der gemischte, sichtbare Verkehr waehrend des Uebergangs.</summary>
    public Verkehr Verkehr { get; }

    /// <summary>Fahrzeit seit Beginn (Sekunden, nur solange die Fahrzeuge fahren).</summary>
    public double Fahrzeit { get; private set; }

    /// <summary>Sind alle alten hinaus und alle neuen herein?</summary>
    public bool Fertig => _altOffen.Count == 0 && _schatten.Autos.Count == 0;

    /// <summary>Bisherige Tausche in ihrer Reihenfolge.</summary>
    public IReadOnlyList<Tausch> Tausche => _tausche;

    /// <summary>Ist dieses Fahrzeug (eines der beiden Saetze) gerade sichtbar unterwegs?</summary>
    public bool Aktiv(Verkehr.Auto auto) => Verkehr.Autos.Contains(auto);

    /// <summary>
    /// Alle Fahrzeuge <paramref name="sekunden"/> weiter (0 = sie stehen, nur tauschen), danach tauschen, was geht.
    /// <paramref name="imBild"/> sagt, ob ein Punkt mit Halbmesser im Sichtkegel liegt. Rueckgabe: Zahl der Tausche.
    /// </summary>
    public int Schritt(double sekunden, float fx, float fz, Func<Vector3, float, bool> imBild)
    {
        var vorher = new Dictionary<Verkehr.Auto, float>();
        if (sekunden > 0)
        {
            foreach (var a in _schatten.Autos.Concat(_altOffen))
            {
                vorher[a] = a.Weg;
            }
            Verkehr.Weiter(sekunden, fx, fz);
            // unsichtbar: die Figur haelt sie nicht auf
            _schatten.Weiter(sekunden, -1000, -1000);
            Fahrzeit += sekunden;
        }
        var zahl = 0;
        var ende = Fahrzeit >= Hoechstzeit;
        foreach (var alt in _altOffen.ToList())
        {
            Grund? grund = ende ? Grund.Hoechstzeit
                : alt.Weg >= alt.Laenge - AmEnde || (vorher.TryGetValue(alt, out var w) && alt.Weg < w) ? Grund.Rundlauf
                : !Punkte(alt, Alt, alt.Weg).Any(p => imBild(p, Rand)) ? Grund.Ausserhalb
                : null;
            if (grund is { } g)
            {
                Verkehr.Autos.Remove(alt);
                _altOffen.Remove(alt);
                _tausche.Add(new Tausch(alt.Nummer, false, g, Fahrzeit));
                zahl++;
            }
        }
        foreach (var neu in _schatten.Autos.ToList())
        {
            Grund? grund = ende ? Grund.Hoechstzeit
                : !Frei(neu) ? null
                : vorher.TryGetValue(neu, out var w2) && neu.Weg < w2 ? Grund.Einfahrt
                : !Punkte(neu, Neu, neu.Weg).Any(p => imBild(p, Rand)) ? Grund.Ausserhalb
                : null;
            if (grund is { } g)
            {
                Herein(neu, g);
                zahl++;
            }
        }
        if (Fertig)
        {
            NeuVerkehr.Bedient(Verkehr.FahrenBis - Verkehr.Nachlauf);
        }
        return zahl;
    }

    /// <summary>Alle offenen sofort tauschen (niemand sieht die Strassen oder ein neuer Uebergang folgt).</summary>
    public void Abschliessen()
    {
        foreach (var alt in _altOffen)
        {
            Verkehr.Autos.Remove(alt);
            _tausche.Add(new Tausch(alt.Nummer, false, Grund.Sofort, Fahrzeit));
        }
        _altOffen.Clear();
        foreach (var neu in _schatten.Autos.ToList())
        {
            Herein(neu, Grund.Sofort);
        }
        NeuVerkehr.Bedient(Verkehr.FahrenBis - Verkehr.Nachlauf);
    }

    private void Herein(Verkehr.Auto neu, Grund grund)
    {
        _schatten.Autos.Remove(neu);
        var stelle = Verkehr.Autos.FindIndex(a => a.Nummer > neu.Nummer);
        Verkehr.Autos.Insert(stelle < 0 ? Verkehr.Autos.Count : stelle, neu);
        _tausche.Add(new Tausch(neu.Nummer, true, grund, Fahrzeit));
    }

    /// <summary>Ist der Platz frei (kein sichtbares Fahrzeug oder Gestalt naeher als der Abstand zum Vordermann)?</summary>
    private bool Frei(Verkehr.Auto neu)
    {
        var meine = Punkte(neu, Neu, neu.Weg).ToList();
        foreach (var b in Verkehr.Autos)
        {
            foreach (var p in Punkte(b, _altOffen.Contains(b) ? Alt : Neu, b.Weg))
            {
                if (meine.Any(m => Vector2.Distance(new Vector2(m.X, m.Z), new Vector2(p.X, p.Z)) < Verkehr.AbstandVorne))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>Mitten aller Gestalten bzw. des Autos einer Strecke (Weihnachten: Mann und Geschenke dahinter).</summary>
    public static IEnumerable<Vector3> Punkte(Verkehr.Auto auto, FestVerkehr.Satz satz, float weg)
    {
        var hinten = satz == FestVerkehr.Satz.Autos ? [0f] : FestVerkehr.Plaetze(satz).Where(p => p.Strecke == auto.Nummer).Select(p => p.Hinten);
        foreach (var h in hinten)
        {
            var ort = auto.Lage(weg - h).Ort;
            yield return new Vector3(ort.X, StadtSzene.Hoehe.Strasse + 0.15f, ort.Y);
        }
    }

    /// <summary>
    /// Liegt die Kugel um <paramref name="p"/> mit Halbmesser <paramref name="halb"/> (teilweise) im Sichtkegel der Matrix
    /// <paramref name="sichtProj"/> (Sicht mal Projektion, Zeilenvektoren wie <see cref="Kamera"/>)? Geprueft werden die
    /// vier Seiten und die Ebene vor der Kamera.
    /// </summary>
    public static bool ImSichtkegel(Matrix4x4 sichtProj, Vector3 p, float halb)
    {
        var m = sichtProj;
        Vector4 s0 = new(m.M11, m.M21, m.M31, m.M41), s1 = new(m.M12, m.M22, m.M32, m.M42),
            s2 = new(m.M13, m.M23, m.M33, m.M43), s3 = new(m.M14, m.M24, m.M34, m.M44);
        foreach (var e in (Vector4[])[s3 + s0, s3 - s0, s3 + s1, s3 - s1, s2])
        {
            var n = new Vector3(e.X, e.Y, e.Z);
            var laenge = n.Length();
            if (laenge < 1e-9f)
            {
                continue;
            }
            if ((Vector3.Dot(n, p) + e.W) / laenge < -halb)
            {
                return false;
            }
        }
        return true;
    }
}
