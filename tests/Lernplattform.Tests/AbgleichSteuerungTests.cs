using Lernplattform.Core.Daten;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, L10 (Weisung P2-12, E1.5): wann abgeglichen wird, mit einer Uhr-Attrappe je Auslöser.
/// Der Zeitgeber merkt sich nur Wartezeit und Aktion; der Test löst sie aus. Der Abgleich selbst
/// ist eine Zählfunktion (kein Netz).
/// </summary>
public class AbgleichSteuerungTests
{
    private static readonly AbgleichErgebnis Leer = new(0, 0, 0, false, false, []);

    /// <summary>Zeitgeber-Attrappe: geplante Aktionen mit Wartezeit; Ausloesen startet sie.</summary>
    private sealed class Zeitgeber
    {
        public List<Plan> Plaene { get; } = [];

        public IDisposable Planen(TimeSpan nach, Action was)
        {
            var plan = new Plan(nach, was);
            Plaene.Add(plan);
            return plan;
        }

        public IEnumerable<Plan> Offen => Plaene.Where(p => !p.Abgebrochen && !p.Gelaufen);

        public sealed class Plan(TimeSpan nach, Action was) : IDisposable
        {
            public TimeSpan Nach { get; } = nach;

            public bool Abgebrochen { get; private set; }

            public bool Gelaufen { get; private set; }

            public void Ausloesen()
            {
                Gelaufen = true;
                was();
            }

            public void Dispose() => Abgebrochen = true;
        }
    }

    private sealed class Aufbau
    {
        public Zeitgeber Uhr { get; } = new();

        public int Laeufe;

        public bool AutoAn { get; set; } = true;

        public List<(AbgleichErgebnis? Ergebnis, AbgleichAusnahme? Fehler)> Fertig { get; } = [];

        public Func<AbgleichErgebnis>? Arbeit { get; set; }

        public AbgleichSteuerung Steuerung(AbgleichSteuerung.Zeiten zeiten, bool synchron = true) => new(zeiten, () => AutoAn,
            () =>
            {
                Interlocked.Increment(ref Laeufe);
                return Arbeit?.Invoke() ?? Leer;
            },
            (e, f) =>
            {
                lock (Fertig)
                {
                    Fertig.Add((e, f));
                }
            },
            Uhr.Planen, synchron ? a => a() : null, Protokoll.Add);

        public List<Exception> Protokoll { get; } = [];
    }

    [Fact]
    public void StartPcNachAnderthalbSekunden()
    {
        var a = new Aufbau();
        using var s = a.Steuerung(AbgleichSteuerung.Pc);
        s.Starten();
        var plan = Assert.Single(a.Uhr.Offen);
        Assert.Equal(TimeSpan.FromSeconds(1.5), plan.Nach);
        Assert.Equal(0, a.Laeufe);
        plan.Ausloesen();
        Assert.Equal(1, a.Laeufe);
        Assert.Equal([(Leer, null)], a.Fertig);
    }

    [Fact]
    public void StartHandySofort()
    {
        var a = new Aufbau();
        using var s = a.Steuerung(AbgleichSteuerung.Handy);
        s.Starten();
        Assert.Empty(a.Uhr.Plaene);
        Assert.Equal(1, a.Laeufe);
    }

    [Theory]
    [InlineData(true, 60)]
    [InlineData(false, 30)]
    public void NachDemLernenEinmalGeplant(bool pc, int sekunden)
    {
        var a = new Aufbau();
        using var s = a.Steuerung(pc ? AbgleichSteuerung.Pc : AbgleichSteuerung.Handy);
        s.NachDemLernen();
        s.NachDemLernen();      // weitere Antworten verschieben den Abgleich nicht
        s.NachDemLernen();
        var plan = Assert.Single(a.Uhr.Plaene);
        Assert.Equal(TimeSpan.FromSeconds(sekunden), plan.Nach);
        Assert.True(s.Gelernt);
        plan.Ausloesen();
        Assert.Equal(1, a.Laeufe);
        Assert.False(s.Gelernt);
        // danach plant die naechste Antwort wieder
        s.NachDemLernen();
        Assert.Equal(2, a.Uhr.Plaene.Count);
    }

    [Fact]
    public void VerlassenNurNachDemLernen()
    {
        var a = new Aufbau();
        using var s = a.Steuerung(AbgleichSteuerung.Handy);
        s.Verlassen();
        Assert.Equal(0, a.Laeufe);
        s.NachDemLernen();
        s.Verlassen();
        Assert.Equal(1, a.Laeufe);
        s.Verlassen();          // nichts Neues gelernt
        Assert.Equal(1, a.Laeufe);
    }

    [Fact]
    public void BeendenNurNachDemLernen()
    {
        var a = new Aufbau();
        using (var s = a.Steuerung(AbgleichSteuerung.Pc))
        {
            s.VorDemBeenden();
            Assert.Equal(0, a.Laeufe);
        }
        using (var s = a.Steuerung(AbgleichSteuerung.Pc))
        {
            s.NachDemLernen();
            var plan = Assert.Single(a.Uhr.Offen);
            s.VorDemBeenden();
            Assert.Equal(1, a.Laeufe);
            Assert.True(plan.Abgebrochen);      // der geplante Abgleich entfaellt
            s.NachDemLernen();                  // nach dem Beenden plant nichts mehr
            Assert.Single(a.Uhr.Plaene);
        }
    }

    [Fact]
    public void OhneAutomatikKeinAusloeser()
    {
        var a = new Aufbau { AutoAn = false };
        using var s = a.Steuerung(AbgleichSteuerung.Pc);
        s.Starten();
        // PC prueft wie root.after(1500, auto_start) erst nach der Wartezeit
        Assert.Single(a.Uhr.Offen).Ausloesen();
        s.NachDemLernen();
        s.Verlassen();
        s.VorDemBeenden();
        Assert.Single(a.Uhr.Plaene);
        Assert.Equal(0, a.Laeufe);
        using var h = a.Steuerung(AbgleichSteuerung.Handy);
        h.Starten();
        Assert.Equal(0, a.Laeufe);
        // „Speichern und abgleichen“ gleicht immer ab
        h.Abgleichen();
        Assert.Equal(1, a.Laeufe);
    }

    [Fact]
    public async Task LaufendErGenauEinerDanach()
    {
        var a = new Aufbau();
        using var frei = new ManualResetEventSlim();
        using var begonnen = new ManualResetEventSlim();
        a.Arbeit = () =>
        {
            if (a.Laeufe == 1)
            {
                begonnen.Set();
                frei.Wait(TimeSpan.FromSeconds(20));
            }
            return Leer;
        };
        using var s = a.Steuerung(AbgleichSteuerung.Pc, synchron: false);
        s.Abgleichen();
        Assert.True(begonnen.Wait(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken));
        Assert.True(s.Laeuft);
        s.Abgleichen();
        s.Abgleichen();
        frei.Set();
        for (var i = 0; i < 200 && (a.Laeufe < 2 || s.Laeuft); i++)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.Equal(2, a.Laeufe);
        Assert.False(s.Laeuft);
    }

    [Fact]
    public void BeendenWartetAufLaufendenUndHoechstensDieFrist()
    {
        var a = new Aufbau();
        using var frei = new ManualResetEventSlim();
        using var begonnen = new ManualResetEventSlim();
        a.Arbeit = () =>
        {
            begonnen.Set();
            frei.Wait(TimeSpan.FromSeconds(20));
            return Leer;
        };
        var zeiten = new AbgleichSteuerung.Zeiten(TimeSpan.Zero, TimeSpan.FromSeconds(60), TimeSpan.FromMilliseconds(400));
        using var s = a.Steuerung(zeiten, synchron: false);
        s.NachDemLernen();
        s.Abgleichen();             // laeuft und haengt
        Assert.True(begonnen.Wait(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken));
        s.NachDemLernen();
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        s.VorDemBeenden();
        uhr.Stop();
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(3), "wartete " + uhr.Elapsed);
        Assert.Equal(1, a.Laeufe);  // der letzte Abgleich startete nicht mehr (Frist vorbei)
        frei.Set();
    }

    /// <summary>Beenden: ein laufender Abgleich endet innerhalb der Frist, danach laeuft noch der letzte.</summary>
    [Fact]
    public void BeendenWartetUndGleichtDannAb()
    {
        var a = new Aufbau();
        using var begonnen = new ManualResetEventSlim();
        a.Arbeit = () =>
        {
            if (a.Laeufe == 1)
            {
                begonnen.Set();
                Thread.Sleep(300);
            }
            return Leer;
        };
        var zeiten = new AbgleichSteuerung.Zeiten(TimeSpan.Zero, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10));
        using var s = a.Steuerung(zeiten, synchron: false);
        s.Abgleichen();
        Assert.True(begonnen.Wait(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken));
        s.NachDemLernen();
        s.VorDemBeenden();
        Assert.Equal(2, a.Laeufe);
    }

    [Fact]
    public void AutomatikNachDemStartAbgeschaltet()
    {
        var a = new Aufbau();
        using var s = a.Steuerung(AbgleichSteuerung.Pc);
        s.Starten();
        a.AutoAn = false;           // in den 1,5 s ausgeschaltet
        Assert.Single(a.Uhr.Offen).Ausloesen();
        Assert.Equal(0, a.Laeufe);
    }

    [Fact]
    public void UnerwarteterFehlerOhneAbsturz()
    {
        var a = new Aufbau { Arbeit = () => throw new InvalidOperationException("geheim ghp_xyz") };
        using var s = a.Steuerung(AbgleichSteuerung.Handy);
        s.Abgleichen();
        var (ergebnis, fehler) = Assert.Single(a.Fertig);
        Assert.Null(ergebnis);
        Assert.Equal(AbgleichFehler.Unerwartet, fehler!.Fehler);
        Assert.DoesNotContain("ghp_", fehler.Message);
        Assert.IsType<InvalidOperationException>(Assert.Single(a.Protokoll));   // Einzelheiten nur ins (gefilterte) Fehlerprotokoll
        Assert.False(s.Laeuft);
    }
}
