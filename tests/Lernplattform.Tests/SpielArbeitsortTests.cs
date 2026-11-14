using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Tests;

/// <summary>
/// Welle E4 „Aufgaben an Gebäuden“ (A 0.75.0, Weisung P12-247, Nico 1a 2a 3c): eine neue Quest mit Arbeitsort im Spiel.
/// Geprueft: „wartet“ = ja stellt den Questgeber an die Stelle am Arbeitsort (Buero, Kunde, Geschaeft; Durchdringung mit der
/// Einrichtung 0 Ecken, ±0,02), nach dem Erledigen steht er wieder an seinem Platz; „wartet“ = nein laesst ihn an seinem Platz
/// und der Auftrag liegt im Raum am Arbeitsort (Geschaeft: nahe der Stelle), Text ohne „wartet dort“; das Questlog nennt Ort,
/// Raum und Etage, „Hingehen“ fuehrt zum Arbeitsort; jede Regel der Ladepruefung rot mit Gegenprobe gruen. Gegenproben: ohne
/// Arbeitsort bleibt alles wie in Q2.
/// </summary>
public sealed class SpielArbeitsortTests
{
    private const string Quest = "neu-q-3";

    private static StadtFassung Fassung0 => StadtbauFassungTests.Fassung0.Value;

    private static PyObjekt MitKarte => Fassung0.MitKarte(SpielInhaltTests.Inhalt);

    private static PyObjekt Innenraeume => Fassung0.Innenraeume;

    /// <summary>Arbeitsort an <paramref name="ort"/> (erster Raum mit freier Stelle bzw. im Geschaeft der ganze Laden).</summary>
    internal static PyObjekt Arbeitsort(string ort, bool wartet, string? raum = null, int etage = 0)
    {
        var g = Questebene.ArbeitsGrundriss(ort, etage, MitKarte, Innenraeume)!;
        var geschaeft = SpielKern.Laeden.Contains(ort) || SpielKern.SchalterOrte.Contains(ort);
        var personen = Questebene.ArbeitsortPersonen(ort, etage, MitKarte, Innenraeume, SpielQuestTests.Stadtdaten(), "ulla");
        raum ??= geschaeft ? "" : Questebene.Raeume(g).Select(r => r.Id).First(r => Questebene.ErsteFreieStelle(g, r, personen) is not null);
        var (x, y) = Questebene.ErsteFreieStelle(g, raum, personen)!.Value;
        return new PyObjekt { { "ort", ort }, { "raum", raum }, { "etage", (long)etage }, { "x", x }, { "y", y }, { "wartet", wartet } };
    }

    /// <summary>Die Probe-Abschnitte aus Q2 und eine dritte neue Quest bei Ulla (Moebelhaus) mit Arbeitsort.</summary>
    internal static PyObjekt Abschnitte(PyObjekt? arbeitsort)
    {
        var a = SpielQuestTests.Abschnitte();
        var q = new PyObjekt
        {
            { "id", Quest }, { "typ", "auswahl" }, { "questgeber", "ulla" }, { "titel", "Drucker im Technikraum" },
            { "frage", "Was prüfst du zuerst?" }, { "ticket", new List<object?> { "Ulla schickt dich: der Drucker druckt nicht." } },
            { "hilfe", new List<object?> { "Erst das Einfache." } }, { "erklaerung", new List<object?> { "Papier und Strom zuerst." } },
            { "cat", "systeme" }, { "belohnung", new PyObjekt { { "geld", 40L }, { "reputation", 2L } } }, { "ab_tag", 1L },
            { "optionen", new List<object?> { "Papier und Strom", "Treiber neu" } }, { "antwort", "Papier und Strom" },
        };
        if (arbeitsort is not null)
        {
            q.Add(Questebene.FeldArbeitsort, arbeitsort);
        }
        SpielDaten.Liste(a[Questebene.AbschnittQuests]).Add(q);
        return a;
    }

    private static PyObjekt Inhalt(PyObjekt? arbeitsort) =>
        Questebene.Anwenden(MitKarte, SpielQuestTests.Stadtdaten(Abschnitte(arbeitsort)), Innenraeume);

    private static PyObjekt? Ulla(string ort, Spielstand stand) => SpielOrte.Personen(ort, stand).FirstOrDefault(p => Py.Gleich(p["id"], "ulla"));

    private static string Ansicht(string ort) => ort == SpielDaten.OrtBuero ? "buero" : SpielKern.Laeden.Contains(ort) ? "geschaeft:" + ort
        : SpielKern.SchalterOrte.Contains(ort) ? "schalter:" + ort : "kunde:" + ort;

    public static TheoryData<string> Orte => ["buero", "talheim", "elektromarkt", "bank"];

    [Theory]
    [MemberData(nameof(Orte))]
    public void WartetJa_QuestgeberAnDerStelle_DanachZurueck(string ort)
    {
        var arbeit = Arbeitsort(ort, true);
        var inhalt = Inhalt(arbeit);
        var spiel = SpielQuestTests.AllesErledigt(inhalt);
        var auftrag = SpielDaten.Auftrag(Quest, inhalt)!;
        Assert.Contains(spiel.Stand.OffeneTickets(), a => Py.Gleich(a["id"], Quest));
        Assert.Equal(ort, SpielDaten.AuftragOrt(auftrag, inhalt));
        var ulla = Ulla(ort, spiel.Stand);
        Assert.NotNull(ulla);
        Assert.Equal([Py.F(arbeit["x"]), Py.F(arbeit["y"])], SpielDaten.Liste(ulla["platz"]).Select(Py.F));
        // im Innenraum steht sie mit gruenem „!“, und sie spricht die Figur mit „Auftrag annehmen“ an
        var innen = Innenraum.Bauen(Ansicht(ort), spiel.Stand, SpielInnenraumTests.Innenraeume.Value, SpielGrafikTests.Bibliothek.Value)!;
        var p = Assert.Single(innen.Personen, x => x.Id == "ulla");
        Assert.True(p.Auftrag);
        var (_, _, knoepfe) = innen.Nachricht(p.X, p.Z + 0.6, spiel.Stand);
        Assert.Contains(knoepfe, k => k.Aktion == "auftrag:" + Quest);
        Durchdringung(innen, p);
        // erledigt: wieder an ihrem Platz im Moebelhaus
        spiel.Loesen(Quest, Bewertung.Loesung(auftrag, null, inhalt), false);
        if (ort != "moebelhaus")
        {
            Assert.Null(Ulla(ort, spiel.Stand));
        }
        Assert.Equal([4.0, 3.0], SpielDaten.Liste(Ulla("moebelhaus", spiel.Stand)!["platz"]).Select(Py.F));
        // Gegenprobe: ohne Arbeitsort steht sie am Platz im Moebelhaus und nicht am Arbeitsort
        var ohne = SpielQuestTests.AllesErledigt(Inhalt(null));
        Assert.Null(Ulla(ort, ohne.Stand));
        Assert.NotNull(Ulla("moebelhaus", ohne.Stand));
    }

    /// <summary>
    /// Ziel 3 (Nico 3c) fuer jeden Raum jedes Arbeitsorts, den es am Tag 2 gibt: der Questgeber an der ersten freien Stelle
    /// durchdringt nichts (0 Ecken, ±0,02, vier Blickrichtungen) und steht nicht naeher als die Grenze an einer anderen Person.
    /// </summary>
    [Fact]
    public void JederRaum_ErsteFreieStelle_OhneDurchdringung()
    {
        var geprueft = 0;
        foreach (var w in Questebene.Arbeitsorte(MitKarte, Innenraeume).Where(w => w.FreiAb <= 2))
        {
            var g = Questebene.ArbeitsGrundriss(w.Ort, 0, MitKarte, Innenraeume)!;
            var raeume = w.Art == Questebene.ArbeitsortArt.Geschaeft ? [""] : Questebene.Raeume(g).Select(r => r.Id).ToList();
            foreach (var raum in raeume)
            {
                var arbeit = Arbeitsort(w.Ort, true, raum);
                var spiel = SpielQuestTests.AllesErledigt(Inhalt(arbeit));
                var innen = Innenraum.Bauen(Ansicht(w.Ort), spiel.Stand, SpielInnenraumTests.Innenraeume.Value, SpielGrafikTests.Bibliothek.Value)!;
                var p = Assert.Single(innen.Personen, x => x.Id == "ulla");
                Durchdringung(innen, p);
                foreach (var andere in innen.Personen.Where(x => x.Id != "ulla"))
                {
                    var abstand = Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(andere.X, andere.Z));
                    Assert.True(abstand >= Questebene.ArbeitsortPersonAbstand - 0.01, $"{w.Ort}/{raum}: {andere.Id} nur {abstand:0.00} entfernt");
                }
                geprueft++;
            }
        }
        // Buero 7 Raeume, Talheim 5, drei Geschaefte, zwei Schalter
        Assert.True(geprueft >= 17, "geprueft: " + geprueft);
    }

    /// <summary>
    /// Weisung P12-253 (Bild 11): wartet der Questgeber am Arbeitsort, geht die Figur beim Antippen vom Eingang bis vor ihn und
    /// durchdringt ihn dort nicht: Mitte zu Mitte mindestens zwei <see cref="StadtSpiel.FigurHalbmesser"/> plus 0,02, und keine
    /// Ecke der Spielfigur liegt im Questgeber und umgekehrt (0 Ecken, auch 0,02 naeher, vier Blickrichtungen des Questgebers).
    /// </summary>
    [Theory]
    [MemberData(nameof(Orte))]
    public void WartetJa_Angesprochen_FigurUndQuestgeberDurchdringenSichNicht(string ort)
    {
        var spiel = SpielQuestTests.AllesErledigt(Inhalt(Arbeitsort(ort, true)));
        var innen = Innenraum.Bauen(Ansicht(ort), spiel.Stand, SpielInnenraumTests.Innenraeume.Value, SpielGrafikTests.Bibliothek.Value)!;
        var p = Assert.Single(innen.Personen, x => x.Id == "ulla");
        var weg = innen.Weg(innen.StartX, innen.StartZ, p.X, p.Z, Wege.Reichweite);
        var (fx, fz) = weg.Count > 0 ? weg[^1] : (innen.StartX, innen.StartZ);
        var ende = new Vector2((float)fx, (float)fz);
        var abstand = Vector2.Distance(ende, new Vector2(p.X, p.Z));
        Assert.True(abstand >= (2 * StadtSpiel.FigurHalbmesser) + 0.02, $"{ort}: Figur nur {abstand:0.00} vor dem Questgeber");
        var grafik = SpielGrafikTests.Bibliothek.Value;
        var netzFigur = Figur.Netz(grafik, null, 1f);
        var netzUlla = Figur.Netz(grafik, p.Aussehen, 1f);
        var hin = Vector2.Normalize(new Vector2(p.X, p.Z) - ende);
        foreach (var naeher in new[] { 0f, 0.02f })
        {
            var stelle = ende + (hin * naeher);
            var figur = SpielSitzenTests.Dreiecke(netzFigur, Matrix4x4.CreateScale(Innenraum.FigurHoehe) * Matrix4x4.CreateRotationY(MathF.Atan2(hin.X, hin.Y))
                * Matrix4x4.CreateTranslation(new Vector3(stelle.X, 0, stelle.Y)));
            foreach (var blick in new[] { 0f, MathF.PI / 2, MathF.PI, 3 * MathF.PI / 2 })
            {
                var ulla = SpielSitzenTests.Dreiecke(netzUlla, Matrix4x4.CreateScale(Innenraum.FigurHoehe) * Matrix4x4.CreateRotationY(blick)
                    * Matrix4x4.CreateTranslation(new Vector3(p.X, 0, p.Z)));
                var figurInUlla = figur.SelectMany(d => new[] { d.A, d.B, d.C }).Count(e => SpielSitzenTests.Innen(e, ulla));
                var ullaInFigur = ulla.SelectMany(d => new[] { d.A, d.B, d.C }).Count(e => SpielSitzenTests.Innen(e, figur));
                Assert.True(figurInUlla == 0 && ullaInFigur == 0, $"{ort} ({naeher}, Blick {blick:0.00}): {figurInUlla} Ecken der Figur im Questgeber, {ullaInFigur} umgekehrt");
            }
        }
    }

    /// <summary>Durchdringung der stehenden Figur mit der Einrichtung: 0 Ecken (auch 0,02 daneben, vier Blickrichtungen).</summary>
    private static void Durchdringung(Innenraum innen, InnenPerson p)
    {
        var grafik = SpielGrafikTests.Bibliothek.Value;
        var netz = Figur.Netz(grafik, p.Aussehen, 1f);
        var geprueft = 0;
        foreach (var (dx, dz) in new[] { (0f, 0f), (0.02f, 0f), (-0.02f, 0f), (0f, 0.02f), (0f, -0.02f) })
        {
            foreach (var blick in new[] { 0f, MathF.PI / 2, MathF.PI, 3 * MathF.PI / 2 })
            {
                var figur = SpielSitzenTests.Dreiecke(netz, Matrix4x4.CreateScale(Innenraum.FigurHoehe) * Matrix4x4.CreateRotationY(blick)
                    * Matrix4x4.CreateTranslation(new Vector3(p.X + dx, 0, p.Z + dz)));
                var (min, max) = SpielSitzenTests.Huelle(figur);
                foreach (var stueck in innen.Stuecke)
                {
                    // Bodenbelag (Teppich, Matte: flacher als 0,1) betritt jede Figur, er zaehlt nicht
                    if (grafik.Modell(stueck.Name) is not { } m)
                    {
                        continue;
                    }
                    var moebel = SpielSitzenTests.Dreiecke(m, stueck.Lage);
                    var (mmin, mmax) = SpielSitzenTests.Huelle(moebel);
                    if (mmax.Y - mmin.Y < 0.1f || Vector3.Max(min, mmin) is var u && Vector3.Min(max, mmax) is var o && (u.X > o.X || u.Y > o.Y || u.Z > o.Z))
                    {
                        continue;
                    }
                    var figurInMoebel = figur.SelectMany(d => new[] { d.A, d.B, d.C }).Count(e => SpielSitzenTests.Innen(e, moebel));
                    var moebelInFigur = moebel.SelectMany(d => new[] { d.A, d.B, d.C }).Count(e => SpielSitzenTests.Innen(e, figur));
                    Assert.True(figurInMoebel == 0 && moebelInFigur == 0, $"{innen.Ansicht} ({p.X:0.00}/{p.Z:0.00}, Raum {Py.Str(Wege.RaumBei(p.X, p.Z, innen.Gebaeude)?["id"])}): {figurInMoebel} Ecken der Figur in {stueck.Name} ({mmin.X:0.00}..{mmax.X:0.00} / {mmin.Z:0.00}..{mmax.Z:0.00}), {moebelInFigur} umgekehrt");
                    geprueft++;
                }
            }
        }
        _ = geprueft;
    }

    [Theory]
    [InlineData("buero")]
    [InlineData("talheim")]
    [InlineData("elektromarkt")]
    public void WartetNein_QuestgeberBleibt_AuftragAmArbeitsort(string ort)
    {
        var arbeit = Arbeitsort(ort, false);
        var inhalt = Inhalt(arbeit);
        var spiel = SpielQuestTests.AllesErledigt(inhalt);
        Assert.Contains(spiel.Stand.OffeneTickets(), a => Py.Gleich(a["id"], Quest));
        // Ulla bleibt im Moebelhaus und ist nicht am Arbeitsort
        if (ort != "moebelhaus")
        {
            Assert.Null(Ulla(ort, spiel.Stand));
        }
        Assert.NotNull(Ulla("moebelhaus", spiel.Stand));
        var innen = Innenraum.Bauen(Ansicht(ort), spiel.Stand, SpielInnenraumTests.Innenraeume.Value, null)!;
        var (x, y) = (Py.F(arbeit["x"]), Py.F(arbeit["y"]));
        var (titel, text, knoepfe) = innen.Nachricht(x, y, spiel.Stand);
        Assert.Equal(Innenraum.ArbeitsortTitel, titel);
        Assert.Contains("Drucker im Technikraum", text, StringComparison.Ordinal);
        Assert.DoesNotContain("wartet dort", text, StringComparison.Ordinal);
        Assert.Contains(knoepfe, k => k.Aktion == "auftrag:" + Quest);
        // Gegenprobe: weit weg (anderer Raum bzw. ausserhalb der Reichweite) kein Auftrag hier
        var g = innen.Gebaeude;
        var weit = Questebene.Erreichbar(g).Select(Wege.Mitte)
            .Where(m => Py.Str(arbeit["raum"]) is { Length: > 0 } r ? !Py.Gleich(Wege.RaumBei(m.Item1, m.Item2, g)?["id"], r)
                : Math.Sqrt(((m.Item1 - x) * (m.Item1 - x)) + ((m.Item2 - y) * (m.Item2 - y))) > Questebene.ArbeitsortReichweite + 0.5)
            .Where(m => innen.PersonNahe(m.Item1, m.Item2) is null).First();
        Assert.NotEqual(Innenraum.ArbeitsortTitel, innen.Nachricht(weit.Item1, weit.Item2, spiel.Stand).Titel);
        // Gegenprobe: mit „wartet“ = ja gibt es den Text ohne Person nicht
        var ja = SpielQuestTests.AllesErledigt(Inhalt(Arbeitsort(ort, true)));
        Assert.Null(Questebene.AuftragOhneQuestgeber(ort, 0, x, y, g, ja.Stand));
    }

    [Fact]
    public void WartetNein_Buero_HinweisNenntDenRaum_KeinAusrufezeichenSatz()
    {
        var arbeit = Arbeitsort("buero", false, "werkstatt");
        var spiel = SpielQuestTests.AllesErledigt(Inhalt(arbeit));
        var g = SpielOrte.Gebaeude("buero", spiel.Stand);
        var flur = Questebene.Erreichbar(g).Select(Wege.Mitte).First(m => Wege.RaumBei(m.Item1, m.Item2, g) is null);
        var (_, text) = SpielOrte.BueroNachricht(flur.Item1, flur.Item2, null, spiel.Stand.Auftraege("buero"), g, spiel.Stand);
        Assert.Contains("„Drucker im Technikraum“ wartet im Raum IT-Werkstatt", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Questlog_OrtRaumEtage_WartetNurBeiJa_HingehenZumArbeitsort()
    {
        foreach (var wartet in new[] { true, false })
        {
            var arbeit = Arbeitsort("talheim", wartet, "technikraum");
            var inhalt = Inhalt(arbeit);
            var stadt = new Stadt(inhalt, SpielQuestTests.Stadtdaten(Abschnitte(arbeit))) { Fassung = Fassung0 };
            var ablauf = SpielQuestTests.AllesErledigt(inhalt);
            var spiel = new StadtSpiel(stadt, ablauf, null, SpielInnenraumTests.Innenraeume.Value);
            var e = Assert.Single(Questlog.Offen(spiel), x => x.Id == Quest);
            Assert.Equal("talheim", e.Ziel);
            Assert.Contains(spiel.Name("talheim"), e.OrtText, StringComparison.Ordinal);
            Assert.Contains("Raum Technikraum", e.OrtText, StringComparison.Ordinal);
            Assert.Equal(wartet ? "Ulla Brandt wartet dort auf dich." : "", e.Wartet);
        }
        // Gegenprobe: ohne Arbeitsort kein Satz „wartet dort“, Ort = Moebelhaus
        var ohne = Inhalt(null);
        var s = new StadtSpiel(new Stadt(ohne, SpielQuestTests.Stadtdaten(Abschnitte(null))) { Fassung = Fassung0 }, SpielQuestTests.AllesErledigt(ohne), null,
            SpielInnenraumTests.Innenraeume.Value);
        var o = Assert.Single(Questlog.Offen(s), x => x.Id == Quest);
        Assert.Equal("", o.Wartet);
        Assert.Equal("moebelhaus", o.Ziel);
    }

    public static TheoryData<string, string> Regeln => new()
    {
        { "unlesbar", "nicht lesbar" }, { "ort", "gibt es im Spiel nicht" }, { "bau", "erst mit dem Einbau" },
        { "etage", "Etage 2" }, { "raum im laden", "ein Raum wird dort nicht gewählt" }, { "raum", "den Raum" },
        { "frei ab", "erst ab Tag 12" }, { "ausserhalb", "außerhalb" }, { "anderer raum", "nicht im Raum" },
        { "moebel", "kann niemand stehen" }, { "draussen wartet", "draußen steht" },
        { "nah einrichtung", "zu nah an Wand oder Einrichtung" }, { "nah person", "zu nah an einer anderen Person" },
    };

    [Theory]
    [MemberData(nameof(Regeln))]
    public void Ladepruefung_JedeRegelRot_GegenprobeGruen(string regel, string text)
    {
        Assert.Empty(Questebene.Pruefen(MitKarte, SpielQuestTests.Stadtdaten(Abschnitte(Arbeitsort("buero", true))), Innenraeume));
        var ort = regel switch { "raum im laden" => "elektromarkt", "frei ab" => "talheim_nord", _ => "buero" };
        var a = Arbeitsort(ort, true);
        var abschnitte = Abschnitte(a);
        var g = Questebene.ArbeitsGrundriss(ort, 0, MitKarte, Innenraeume)!;
        switch (regel)
        {
            case "unlesbar":
                Py.Setzen(a, "farbe", "rot");
                break;
            case "ort":
                Py.Setzen(a, "ort", "nirgends");
                break;
            case "bau":
                Py.Setzen(a, "ort", "bau:haus");
                break;
            case "etage":
                Py.Setzen(a, "etage", 2L);
                break;
            case "raum im laden":
                Py.Setzen(a, "raum", "kasse");
                break;
            case "raum":
                Py.Setzen(a, "raum", "nirgends");
                break;
            case "ausserhalb":
                Py.Setzen(a, "y", -2.0);
                break;
            case "anderer raum":
                var (ax, ay) = Questebene.ErsteFreieStelle(g, "lager")!.Value;
                Py.Setzen(a, "x", ax);
                Py.Setzen(a, "y", ay);
                break;
            case "moebel":
                var raum = SpielDaten.ListeOderLeer(g, "raeume").Select(SpielDaten.Objekt).Single(r => Py.Gleich(r["id"], Py.Str(a["raum"])));
                var d = SpielDaten.ListeOderLeer(raum, "deko").Select(SpielDaten.Objekt).First(x => !Wege.Begehbar.Contains(Py.Str(x["typ"])) && Py.F(x["w"]) >= 0.6 && Py.F(x["h"]) >= 0.6);
                Py.Setzen(a, "x", Py.F(d["x"]) + (Py.F(d["w"]) / 2));
                Py.Setzen(a, "y", Py.F(d["y"]) + (Py.F(d["h"]) / 2));
                break;
            case "nah einrichtung":
            case "nah person":
                var personen = Questebene.ArbeitsortPersonen("buero", 0, MitKarte, Innenraeume, SpielQuestTests.Stadtdaten(), "ulla");
                var frei = Questebene.Stellen(g, []);
                var mitPersonen = Questebene.Stellen(g, personen);
                var zelle = regel == "nah einrichtung"
                    ? Questebene.Erreichbar(g).Where(z => !frei.Contains(z)).OrderBy(z => z).First(z => Wege.RaumBei(Wege.Mitte(z).Item1, Wege.Mitte(z).Item2, g) is { } r && Py.Gleich(r["id"], a["raum"]))
                    : frei.Where(z => !mitPersonen.Contains(z)).OrderBy(z => z).First(z => Wege.RaumBei(Wege.Mitte(z).Item1, Wege.Mitte(z).Item2, g) is { } r && Py.Gleich(r["id"], a["raum"]));
                Py.Setzen(a, "x", Wege.Mitte(zelle).Item1);
                Py.Setzen(a, "y", Wege.Mitte(zelle).Item2);
                break;
            case "draussen wartet":
                // Questgeber draussen (Greta aus der Probe)
                var q = SpielDaten.Liste(abschnitte[Questebene.AbschnittQuests]).Select(SpielDaten.Objekt).Single(x => Py.Gleich(x["id"], Quest));
                Py.Setzen(q, "questgeber", "neu-qg-1");
                break;
        }
        var funde = Questebene.Pruefen(MitKarte, SpielQuestTests.Stadtdaten(abschnitte), Innenraeume);
        Assert.Contains(funde, f => f.Contains(text, StringComparison.Ordinal) && f.Contains("Drucker im Technikraum", StringComparison.Ordinal));
        // nie still weg: so laedt das Spiel die Stadt nicht
        Assert.Throws<StadtbauFehler>(() => Questebene.Anwenden(MitKarte, SpielQuestTests.Stadtdaten(abschnitte), Innenraeume));
    }

    [Fact]
    public void Fingerabdruecke_OhneArbeitsortUnveraendert_AuftraegeOhneEtageFeld()
    {
        // vorhandene Auftraege tragen nie die Felder des Arbeitsorts (alte Spielstaende und Sollwerte bleiben)
        foreach (var a in SpielDaten.Liste(SpielQuestTests.Angewendet.Value["aufgaben"]).Select(SpielDaten.Objekt))
        {
            Assert.False(Py.Hat(a, Questebene.FeldOhneQuestgeber) || Py.Hat(a, Questebene.FeldArbeitsstelle) || (Py.Hat(a, "etage") && !Py.Gleich(a["id"], Quest)), Py.Str(a["id"]));
        }
        Assert.False(Questebene.HatArbeitsort(Fassung0.Stadtdaten));
        Assert.Equal(1, StadtFassung.FormatFuer(Fassung0.Stadtdaten));
        Assert.Equal(2, StadtFassung.FormatFuer(SpielQuestTests.Stadtdaten(Abschnitte(Arbeitsort("buero", true)))));
    }
}
