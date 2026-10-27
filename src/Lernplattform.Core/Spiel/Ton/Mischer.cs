namespace Lernplattform.Core.Spiel.Ton;

/// <summary>Regler-Gruppe eines Tons (Karte "Musik und Geräusche").</summary>
public enum Tongruppe { Musik, Umgebung }

/// <summary>Ein ganz gelesener Ton: mono, Proben -1 bis 1, eigene Abtastrate.</summary>
public sealed class TonDaten(float[] proben, int rate)
{
    public float[] Proben { get; } = proben;

    public int Rate { get; } = rate;

    public double Sekunden => Proben.Length / (double)Rate;
}

/// <summary>Ein Ton, der beim Abspielen gelesen wird (eigene Lieder): verschraenkte Proben.</summary>
public interface ITonStrom : IDisposable
{
    int Rate { get; }

    int Kanaele { get; }

    /// <summary>Bis zu <paramref name="anzahl"/> Proben (Bilder x Kanaele) lesen; 0 = Ende.</summary>
    int Lesen(float[] puffer, int anzahl);
}

/// <summary>
/// Phase 8 W8: mischt alle laufenden Toene in die Ausgabe (Stereo, 44 100 Hz). Laeuft im Takt der
/// Ausgabe (Faden des Audiogeraets); Aufrufe von aussen (Oberflaeche) nur kurz unter einer Sperre.
/// Lautstaerke = Ton x Gruppe (Musik/Umgebung) x Gesamt, jede Aenderung weich ueber etwa 20 ms (kein Knacken).
/// Bei Gesamt 0 bleibt alles still und nichts wird gerechnet (Teilplan 1.2).
/// </summary>
public sealed class Mischer
{
    public const int Rate = 44100;

    /// <summary>Anteil je Probe, mit dem die Lautstaerke dem Ziel folgt (etwa 20 ms).</summary>
    private static readonly float Weich = 1f - MathF.Exp(-1f / (0.02f * Rate));

    private sealed class Stimme
    {
        public int Id;
        public TonDaten? Daten;
        public ITonStrom? Strom;
        public double Lage;
        public double Schritt;
        public bool Schleife;
        public Tongruppe Gruppe;
        public float Ziel;
        public float Jetzt;
        public bool Endet;
        public bool Fertig;

        // Strom: zwei Bilder fuer die lineare Umrechnung der Abtastrate
        public float[] Puffer = [];
        public int PufferLaenge;
        public int PufferLage;
        public float VorL, VorR, NachL, NachR;
        public bool Begonnen;
    }

    private readonly object _sperre = new();
    private readonly List<Stimme> _stimmen = [];
    private readonly HashSet<int> _beendet = [];
    private int _naechste = 1;
    private float _gesamt = TonEinstellungen.GesamtVorgabe / 100f;
    private float _musik = TonEinstellungen.MusikVorgabe / 100f;
    private float _umgebung = TonEinstellungen.UmgebungVorgabe / 100f;

    public void Regler(float gesamt, float musik, float umgebung)
    {
        lock (_sperre)
        {
            _gesamt = Math.Clamp(gesamt, 0, 1);
            _musik = Math.Clamp(musik, 0, 1);
            _umgebung = Math.Clamp(umgebung, 0, 1);
        }
    }

    public float Gesamt
    {
        get
        {
            lock (_sperre)
            {
                return _gesamt;
            }
        }
    }

    /// <summary>Wie laut ein Ton der Gruppe wirklich klingt (0 = gar nicht abspielen).</summary>
    public float Faktor(Tongruppe gruppe)
    {
        lock (_sperre)
        {
            return _gesamt * (gruppe == Tongruppe.Musik ? _musik : _umgebung);
        }
    }

    public int Stimmen
    {
        get
        {
            lock (_sperre)
            {
                return _stimmen.Count;
            }
        }
    }

    /// <summary>Einen gelesenen Ton starten; <paramref name="tonhoehe"/> 1 = wie aufgenommen.</summary>
    public int Starten(TonDaten daten, Tongruppe gruppe, float laut, bool schleife = false, float tonhoehe = 1f)
    {
        lock (_sperre)
        {
            var stimme = new Stimme
            {
                Id = _naechste++,
                Daten = daten,
                Schritt = daten.Rate * (double)tonhoehe / Rate,
                Schleife = schleife,
                Gruppe = gruppe,
                Ziel = laut,
                // kurze Toene beginnen sofort in voller Lautstaerke, Schleifen blenden ein
                Jetzt = schleife ? 0 : laut * Faktor0(gruppe),
            };
            _stimmen.Add(stimme);
            return stimme.Id;
        }
    }

    /// <summary>Einen Strom (eigenes Lied) starten; er gehoert ab jetzt dem Mischer (Dispose am Ende).</summary>
    public int StromStarten(ITonStrom strom, Tongruppe gruppe, float laut)
    {
        lock (_sperre)
        {
            var stimme = new Stimme
            {
                Id = _naechste++,
                Strom = strom,
                Schritt = strom.Rate / (double)Rate,
                Gruppe = gruppe,
                Ziel = laut,
                Puffer = new float[4096 * Math.Max(1, strom.Kanaele)],
            };
            _stimmen.Add(stimme);
            return stimme.Id;
        }
    }

    private float Faktor0(Tongruppe gruppe) => _gesamt * (gruppe == Tongruppe.Musik ? _musik : _umgebung);

    public void Lautstaerke(int id, float laut)
    {
        lock (_sperre)
        {
            foreach (var stimme in _stimmen)
            {
                if (stimme.Id == id)
                {
                    stimme.Ziel = laut;
                }
            }
        }
    }

    /// <summary>Ausblenden und dann entfernen.</summary>
    public void Beenden(int id)
    {
        lock (_sperre)
        {
            foreach (var stimme in _stimmen)
            {
                if (stimme.Id == id)
                {
                    stimme.Endet = true;
                    stimme.Ziel = 0;
                }
            }
        }
    }

    /// <summary>Alles sofort still (Spiel verlassen, Hintergrund).</summary>
    public void AllesBeenden()
    {
        lock (_sperre)
        {
            foreach (var stimme in _stimmen)
            {
                stimme.Strom?.Dispose();
                _beendet.Add(stimme.Id);
            }
            _stimmen.Clear();
        }
    }

    public bool Laeuft(int id)
    {
        lock (_sperre)
        {
            return _stimmen.Any(s => s.Id == id);
        }
    }

    /// <summary>Ist der Ton seit dem letzten Abruf zu Ende gegangen (Lied fertig)? Nimmt die Meldung weg.</summary>
    public bool Beendet(int id)
    {
        lock (_sperre)
        {
            return _beendet.Remove(id);
        }
    }

    /// <summary>Ausgabe fuellen (verschraenkt, <paramref name="kanaele"/> 1 oder 2).</summary>
    public void Fuellen(Span<float> puffer, int kanaele)
    {
        puffer.Clear();
        lock (_sperre)
        {
            if (_gesamt <= 0 || _stimmen.Count == 0)
            {
                return;
            }
            var bilder = puffer.Length / kanaele;
            foreach (var stimme in _stimmen)
            {
                var soll = stimme.Ziel * Faktor0(stimme.Gruppe);
                if (stimme.Daten is { } daten)
                {
                    MischeDaten(stimme, daten, soll, puffer, kanaele, bilder);
                }
                else if (stimme.Strom is not null)
                {
                    MischeStrom(stimme, soll, puffer, kanaele, bilder);
                }
                if (stimme.Endet && stimme.Jetzt < 0.0005f)
                {
                    stimme.Fertig = true;
                }
            }
            for (var i = _stimmen.Count - 1; i >= 0; i--)
            {
                if (_stimmen[i].Fertig)
                {
                    _stimmen[i].Strom?.Dispose();
                    _beendet.Add(_stimmen[i].Id);
                    _stimmen.RemoveAt(i);
                }
            }
        }
        for (var i = 0; i < puffer.Length; i++)
        {
            puffer[i] = Math.Clamp(puffer[i], -1f, 1f);
        }
    }

    private static void MischeDaten(Stimme stimme, TonDaten daten, float soll, Span<float> puffer, int kanaele, int bilder)
    {
        var proben = daten.Proben;
        var laenge = proben.Length;
        if (laenge < 2)
        {
            stimme.Fertig = true;
            return;
        }
        for (var b = 0; b < bilder; b++)
        {
            // eine Schleife geht von der letzten Probe weich zur ersten (siehe naechste), ein kurzer Ton endet davor
            if (stimme.Lage >= (stimme.Schleife ? laenge : laenge - 1))
            {
                if (!stimme.Schleife)
                {
                    stimme.Fertig = true;
                    return;
                }
                stimme.Lage %= laenge;
            }
            var i = (int)stimme.Lage;
            var bruch = (float)(stimme.Lage - i);
            var naechste = i + 1 < laenge ? proben[i + 1] : proben[0];
            var wert = proben[i] + ((naechste - proben[i]) * bruch);
            stimme.Jetzt += (soll - stimme.Jetzt) * Weich;
            wert *= stimme.Jetzt;
            for (var k = 0; k < kanaele; k++)
            {
                puffer[(b * kanaele) + k] += wert;
            }
            stimme.Lage += stimme.Schritt;
        }
    }

    private static void MischeStrom(Stimme stimme, float soll, Span<float> puffer, int kanaele, int bilder)
    {
        var strom = stimme.Strom!;
        var quelle = Math.Max(1, strom.Kanaele);
        bool Naechstes(Stimme s, out float l, out float r)
        {
            if (s.PufferLage + quelle > s.PufferLaenge)
            {
                s.PufferLaenge = strom.Lesen(s.Puffer, s.Puffer.Length - (s.Puffer.Length % quelle));
                s.PufferLage = 0;
                if (s.PufferLaenge < quelle)
                {
                    l = r = 0;
                    return false;
                }
            }
            l = s.Puffer[s.PufferLage];
            r = quelle > 1 ? s.Puffer[s.PufferLage + 1] : l;
            s.PufferLage += quelle;
            return true;
        }
        if (!stimme.Begonnen)
        {
            if (!Naechstes(stimme, out stimme.VorL, out stimme.VorR) || !Naechstes(stimme, out stimme.NachL, out stimme.NachR))
            {
                stimme.Fertig = true;
                return;
            }
            stimme.Begonnen = true;
        }
        for (var b = 0; b < bilder; b++)
        {
            while (stimme.Lage >= 1)
            {
                stimme.Lage -= 1;
                stimme.VorL = stimme.NachL;
                stimme.VorR = stimme.NachR;
                if (!Naechstes(stimme, out stimme.NachL, out stimme.NachR))
                {
                    stimme.Fertig = true;
                    return;
                }
            }
            var bruch = (float)stimme.Lage;
            stimme.Jetzt += (soll - stimme.Jetzt) * Weich;
            var l = (stimme.VorL + ((stimme.NachL - stimme.VorL) * bruch)) * stimme.Jetzt;
            var r = (stimme.VorR + ((stimme.NachR - stimme.VorR) * bruch)) * stimme.Jetzt;
            if (kanaele == 1)
            {
                puffer[b] += (l + r) / 2;
            }
            else
            {
                puffer[b * kanaele] += l;
                puffer[(b * kanaele) + 1] += r;
            }
            stimme.Lage += stimme.Schritt;
        }
    }
}
