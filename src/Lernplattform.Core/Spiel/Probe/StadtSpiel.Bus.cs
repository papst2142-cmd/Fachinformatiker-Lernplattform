using System.Globalization;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 0.67.0 (Weisung P12-204, Sammelliste 302, Nico 4a 5a): mit dem Bus von A nach B. Gibt es Haltestellen (Fassung 0:
/// keine), bietet die Ortsliste neben „Hingehen“ den Knopf „Mit dem Bus“, wenn die Fahrt weniger Minuten kostet als zu Fuss
/// (<see cref="Busfahrt"/>). Die Fahrt ist kostenlos und ein einziges Ereignis <see cref="Stadt.Gegangen"/> mit
/// <c>"mit": "bus"</c> (<see cref="Stadt.Bus"/>); danach geht die Figur zur Haltestelle, faehrt im Zeitraffer (hoechstens
/// <see cref="Busfahrt.ZeitrafferSekunden"/>, Antippen ueberspringt) und geht von der Ausstiegs-Haltestelle zum Ziel. Waehrend
/// der Fahrt ist die Figur im Bus (<see cref="ImBus"/>): die Darstellung zeichnet den Bus der Linie an ihrer Stelle.
/// </summary>
public sealed partial class StadtSpiel
{
    /// <summary>Punkte des Wegs vor der Fahrt (zu Fuss zur Haltestelle), danach <see cref="_busPunkte"/> Punkte im Bus.</summary>
    private int _vorBus;

    private int _busPunkte;

    private double _busTempo = Tempo;

    private Buslinie.Linie? _linie;

    private StadtSzene? _linieSzene;

    /// <summary>Die Figur sitzt im Bus (Zeitraffer).</summary>
    public bool ImBus => _vorBus == 0 && _busPunkte > 0;

    /// <summary>Die Figur ist auf dem Weg mit dem Bus (zur Haltestelle, im Bus).</summary>
    public bool MitBusUnterwegs => _busPunkte > 0;

    /// <summary>Die Buslinie der Szene (je Szene einmal gerechnet; ohne Haltestelle null).</summary>
    public Buslinie.Linie? Linie
    {
        get
        {
            if (_linieSzene != Szene)
            {
                _linieSzene = Szene;
                _linie = Szene.Haltestellen.Count == 0 ? null : Buslinie.Anlegen(Szene.Strassen, Szene.Haltestellen);
            }
            return _linie;
        }
    }

    private void WegLeeren()
    {
        _weg.Clear();
        _vorBus = 0;
        _busPunkte = 0;
    }

    /// <summary>Startpunkt fuer Wege draussen: frei die Figur, sonst der Stehplatz vor dem Ort, an dem sie ist.</summary>
    private (double X, double Y)? StartDraussen()
    {
        if (ImFreien && !ImInnenraum)
        {
            return (FigurX, FigurZ);
        }
        return Szene.Ort(Jetzt.Ort) is { } o ? (o.StehX, o.StehZ) : null;
    }

    /// <summary>Minuten zu Fuss zum Ort (wie <see cref="GeheZu(string)"/> rechnet), null = kein Weg oder schon da.</summary>
    public long? MinutenZuFuss(string ziel)
    {
        if (ImFreien && !ImInnenraum && Szene.Ort(ziel) is { } o)
        {
            var weg = Szene.Laufraster.Weg((FigurX, FigurZ), (o.StehX, o.StehZ));
            return weg.Count == 0 ? null : Schritte(weg) * Stadt.MinutenJeSchritt;
        }
        return Jetzt.Ort != ziel && Stadt.Weg(Jetzt.Ort, ziel) is { } w ? w.Schritte * Stadt.MinutenJeSchritt : null;
    }

    /// <summary>Die beste Busfahrt zum Ort oder null (keine Linie, kein Weg, schon da).</summary>
    public Busfahrt.Plan? BusPlan(string ziel)
    {
        if (Linie is not { } linie || Szene.Ort(ziel) is not { } o || StartDraussen() is not { } start || Jetzt.Ort == ziel && !ImFreien)
        {
            return null;
        }
        return Busfahrt.Planen(linie, Szene.Haltestellen, Szene.Laufraster, start, (o.StehX, o.StehZ), Stadt.Zelle,
            Stadt.MinutenJeSchritt, Jetzt.Uhr, Stadt.Beginn);
    }

    /// <summary>Zeiten fuer die Ortsliste: zu Fuss und mit dem Bus, nur wenn der Bus weniger Minuten braucht, sonst null.</summary>
    public (long ZuFuss, long Bus)? BusLohnt(string ziel)
    {
        if (!(ImFreien && !ImInnenraum))
        {
            // an einem Ort (vor der Tuer oder drinnen): dieselbe Rechnung, die das Werkzeug fuer jeden Weg nutzt
            return BusLohntVon(Jetzt.Ort, ziel, Jetzt.Uhr);
        }
        if (Linie is null || MinutenZuFuss(ziel) is not { } fuss || BusPlan(ziel) is not { } plan || plan.Minuten >= fuss)
        {
            return null;
        }
        return (fuss, plan.Minuten);
    }

    /// <summary>
    /// Welle A 0.70.0 (Weisung P12-221): Zeiten von Ort <paramref name="von"/> (Tuer) nach Ort <paramref name="ziel"/> um
    /// <paramref name="uhr"/>: zu Fuss (<see cref="Stadt.Weg"/>) und mit dem Bus (<see cref="Busfahrt.Planen"/> ab dem Stehplatz
    /// vor <paramref name="von"/>); Bus nur, wenn er weniger Minuten braucht, sonst null. Die Ortsliste rechnet genau so
    /// (<see cref="BusLohnt"/>, Figur an einem Ort), die Karte „Laufzeiten“ des Werkzeugs auch.
    /// </summary>
    public (long ZuFuss, long Bus)? BusLohntVon(string von, string ziel, long uhr)
    {
        if (Linie is not { } linie || MinutenVon(von, ziel) is not { } fuss || Szene.Ort(von) is not { } a || Szene.Ort(ziel) is not { } b
            || Busfahrt.Planen(linie, Szene.Haltestellen, Szene.Laufraster, (a.StehX, a.StehZ), (b.StehX, b.StehZ), Stadt.Zelle,
                Stadt.MinutenJeSchritt, uhr, Stadt.Beginn) is not { } plan || plan.Minuten >= fuss)
        {
            return null;
        }
        return (fuss, plan.Minuten);
    }

    /// <summary>Minuten zu Fuss von Tuer zu Tuer (<see cref="Stadt.Weg"/>), null = kein Weg oder derselbe Ort.</summary>
    public long? MinutenVon(string von, string ziel) =>
        von != ziel && Stadt.Weg(von, ziel) is { } w ? w.Schritte * Stadt.MinutenJeSchritt : null;

    /// <summary>Mit dem Bus zum Ort: Ereignis schreiben (Uhr springt), dann zur Haltestelle, fahren, zum Ziel gehen.</summary>
    public StadtGrund MitBusZu(string ziel) => Gemessen(() => MitBusZuOrt(ziel));

    private StadtGrund MitBusZuOrt(string ziel)
    {
        if (Etage > 0)
        {
            Zeile = Etagen.NurUnten;
            Geaendert?.Invoke();
            return StadtGrund.KeinWeg;
        }
        if (Geht)
        {
            Zeile = SpielProbeTexte.BusErstAnkommen;
            Geaendert?.Invoke();
            return StadtGrund.KeinWeg;
        }
        if (BusLohnt(ziel) is null || BusPlan(ziel) is not { } plan)
        {
            Zeile = SpielProbeTexte.BusLohntNicht;
            Geaendert?.Invoke();
            return StadtGrund.KeinWeg;
        }
        if (ImInnenraum)
        {
            Verlassen();
        }
        var ergebnis = Schreiben(() => Stadt.Bus(Ablauf, ziel, plan.Schritte, plan.Minuten));
        if (ergebnis is null)
        {
            return StadtGrund.KeinProfil;
        }
        if (!ergebnis.Ok)
        {
            Zeile = StadtTexte.Grund(ergebnis.Grund);
            Geaendert?.Invoke();
            return ergebnis.Grund;
        }
        WegLeeren();
        Knoepfe = [];
        if (StartDraussen() is { } start && !ImFreien)
        {
            // vom Stehplatz vor dem Ort aus (die Figur steht dort nach dem Verlassen)
            (FigurX, FigurZ) = ((float)start.X, (float)start.Y);
        }
        _weg.AddRange(plan.ZurHaltestelle);
        _vorBus = plan.ZurHaltestelle.Count;
        _busPunkte = plan.Fahrt.Count;
        var einstieg = plan.ZurHaltestelle.Count > 0 ? plan.ZurHaltestelle[^1] : (FigurX, FigurZ);
        _busTempo = Busfahrt.TempoImBus(plan.Fahrt, einstieg, Tempo);
        _weg.AddRange(plan.Fahrt);
        _weg.AddRange(plan.ZumZiel);
        if (Szene.Ort(ziel) is { } o)
        {
            _weg.Add((o.StehX, o.StehZ));
        }
        ImFreien = false;
        _zielOrt = ziel;
        Vorbereiten(ziel);
        Zeile = string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.BusUnterwegs, Name(ziel), plan.MinutenZuFuss, plan.Warten,
            plan.MinutenFahrt, ergebnis.Daten!["minuten"]);
        Geaendert?.Invoke();
        return StadtGrund.Ok;
    }

    /// <summary>Ein Punkt des Wegs erreicht (zu Fuss): true, wenn jetzt die Fahrt beginnt.</summary>
    private bool BusPunktErreicht()
    {
        if (_vorBus > 0)
        {
            _vorBus--;
            return _vorBus == 0 && _busPunkte > 0;
        }
        return false;
    }

    /// <summary>Im Bus weiter (Zeitraffer, ohne Schritte und Schritt-Toene).</summary>
    private bool BusWeiter(double sekunden)
    {
        var rest = sekunden * _busTempo;
        while (rest > 0 && _busPunkte > 0 && _weg.Count > 0)
        {
            var (zx, zz) = _weg[0];
            var dx = zx - FigurX;
            var dz = zz - FigurZ;
            var weite = Math.Sqrt((dx * dx) + (dz * dz));
            if (weite > 1e-6)
            {
                FigurRichtung = (float)(Math.Atan2(dx, dz) * 180 / Math.PI);
            }
            if (weite <= rest)
            {
                (FigurX, FigurZ) = ((float)zx, (float)zz);
                rest -= weite;
                _weg.RemoveAt(0);
                _busPunkte--;
            }
            else
            {
                FigurX += (float)(dx / weite * rest);
                FigurZ += (float)(dz / weite * rest);
                rest = 0;
            }
        }
        if (_weg.Count == 0)
        {
            _busPunkte = 0;
            Ankommen();
        }
        return _weg.Count > 0;
    }

    /// <summary>Antippen waehrend der Fahrt: sofort an der Ausstiegs-Haltestelle (die Uhr steht schon im Ereignis).</summary>
    public void BusUeberspringen()
    {
        while (ImBus && _weg.Count > 0)
        {
            var (zx, zz) = _weg[0];
            var dx = zx - FigurX;
            var dz = zz - FigurZ;
            if ((dx * dx) + (dz * dz) > 1e-12)
            {
                FigurRichtung = (float)(Math.Atan2(dx, dz) * 180 / Math.PI);
            }
            (FigurX, FigurZ) = ((float)zx, (float)zz);
            _weg.RemoveAt(0);
            _busPunkte--;
        }
        if (_weg.Count == 0)
        {
            _busPunkte = 0;
            Ankommen();
        }
        Geaendert?.Invoke();
    }
}
