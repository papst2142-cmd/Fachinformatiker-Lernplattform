using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Mathe;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Core.Daten;

/// <summary>Warum ein Abgleich nicht stattfand (Codes; die Texte kommen mit Weisung P2-12, Liste P2-9).</summary>
public enum AbgleichFehler
{
    /// <summary>E5.1: Repository oder Zugangsschluessel fehlt.</summary>
    NichtEingerichtet,

    /// <summary>E5/T7: Repository nicht als „Benutzer/Name“ angegeben.</summary>
    RepositoryFormat,

    /// <summary>A3.1: Lernstand wird nicht gespeichert (Grund in <see cref="AbgleichAusnahme.Grund"/>).</summary>
    Ausgesetzt,

    /// <summary>F1: HTTP 401.</summary>
    TokenUngueltig,

    /// <summary>F2: HTTP 403.</summary>
    KeinSchreibrecht,

    /// <summary>F3: HTTP 404.</summary>
    NichtGefunden,

    /// <summary>F4: anderer HTTP-Code (in <see cref="AbgleichAusnahme.Code"/>).</summary>
    Abgelehnt,

    /// <summary>F5: keine Verbindung (auch Zeitueberschreitung).</summary>
    KeineVerbindung,

    /// <summary>F6: Antwort unvollstaendig oder kein JSON.</summary>
    Unvollstaendig,

    /// <summary>F7: Datei lernstand.json.gz beschaedigt.</summary>
    DateiKaputt,

    /// <summary>F8: Datei von einer neueren Programmversion.</summary>
    ZuNeu,

    /// <summary>F9: Hochladen dreimal mit HTTP 422 abgelehnt.</summary>
    HochladenAbgelehnt,

    /// <summary>F10: dreimal gleichzeitig geaendert (HTTP 409).</summary>
    Gleichzeitig,

    /// <summary>T5: Sicherheitskopie vor dem Abgleich gescheitert, nichts geaendert (Weisung P2-11 A6).</summary>
    SicherungGescheitert,

    /// <summary>F11: unerwarteter Fehler (Datenbank); Einzelheiten nur gefiltert in der Fehlerdatei.</summary>
    Unerwartet,
}

/// <summary>Abgleich gescheitert. Der Text enthaelt nur den Code, nie Zugangsschluessel oder Antworten von GitHub (A7).</summary>
public sealed class AbgleichAusnahme(AbgleichFehler fehler, int code = 0, string? grund = null)
    : Exception("Abgleich: " + fehler.ToString())
{
    public AbgleichFehler Fehler { get; } = fehler;

    /// <summary>HTTP-Code bei Abgelehnt.</summary>
    public int Code { get; } = code;

    /// <summary>Bei Ausgesetzt: Lernstand.NurLesenGrund.</summary>
    public string? Grund { get; } = grund;
}

/// <summary>Ergebnis wie Python SyncResult (received, sent, removed, Hinweise).</summary>
/// <param name="Sicherungen">Automatische Sicherungen vor dem Abgleich (sicherungen/vor_abgleich_*.db), meist keine.</param>
public sealed record AbgleichErgebnis(int Erhalten, int Gesendet, int Entfernt, bool UngueltigeMarke, bool MarkeGekappt,
    IReadOnlyList<string> Sicherungen);

/// <summary>
/// Phase 2, L10: Abgleich PC und Handy ueber ein privates GitHub-Repository wie fisi_sync.sync
/// (Python v0.63.2): Datei lesen, Uhrzeit von GitHub messen, Mischbetrieb merken, eigene
/// Eintraege bei falscher Uhr nachtragen, Zukunftsmarken kappen, zusammenfuehren
/// (merge_into_local ohne check_future), vergleichen, hochladen; bis zu drei Versuche.
/// Anders als Python laufen alle lokalen Schritte eines Versuchs in EINER Transaktion, und
/// davor entsteht automatisch eine Sicherung sicherungen/vor_abgleich_*.db, wenn der Versuch
/// vorhandene Eintraege aendern oder entfernen wuerde oder noch nie abgeglichen wurde
/// (Weisung P2-11 A6). Scheitert sie, bleibt alles unveraendert und nichts wird hochgeladen.
/// Uebernahme, Abgleich und Ruecksicherung schliessen sich aus (<see cref="Vorgaenge"/>).
/// </summary>
public static class Abgleich
{
    public const string RepoSchluessel = "sync_repo";
    public const string TokenSchluessel = "sync_token";
    public const string AutoSchluessel = "sync_auto";
    public const string ZuletztSchluessel = "sync_last";
    public const string SicherungsPraefix = "vor_abgleich_";
    public const int Versuche = 3;
    public const string OhneDatumZeile = "Abgleich: GitHub-Antwort ohne Date-Kopf, Uhrzeit-Prüfung entfällt (wie bis 0.62.3).";

    internal static int _ohneDatumGemeldet;     // Tests setzen zurueck

    /// <summary>Nur fuer Tests: wird im Versuch nach den lokalen Schritten (vor dem Hochladen) aufgerufen.</summary>
    internal static Action<string>? Haltepunkt { get; set; }

    /// <summary>is_configured: Repository und Zugangsschluessel eingetragen.</summary>
    public static bool Eingerichtet(JsonObject werte) =>
        Text(werte[RepoSchluessel]).Trim().Length > 0 && Text(werte[TokenSchluessel]).Trim().Length > 0;

    private static string Text(JsonNode? wert) =>
        wert is JsonValue v && v.TryGetValue<string>(out var text) ? text : "";

    /// <summary>
    /// sync: gleicht den Lernstand mit dem Repository ab. Wirft AbgleichAusnahme. geraet steht
    /// in der Commit-Nachricht („PC &lt;Rechnername&gt;“ oder „Handy“).
    /// </summary>
    public static AbgleichErgebnis Ausfuehren(Lernstand lernstand, Einstellungen einstellungen, string geraet, GitHubZugang netz,
        IReadOnlyList<(string Tabelle, string Alt, string Neu)> umbenennungen, Uhr? uhr = null, Uhrabweichung? abweichung = null,
        string? sicherungsordner = null)
    {
        uhr ??= Uhr.System;
        abweichung ??= Uhrabweichung.System;
        var werte = einstellungen.Laden();
        // A7: den eingetragenen Zugangsschluessel aus jeder Zeile der Fehlerdatei filtern
        Fehlerprotokoll.GeheimnisseSetzen(werte.Select(p => new KeyValuePair<string, object?>(p.Key, p.Value is JsonValue v
            && v.TryGetValue<string>(out var s) ? s : null)));
        var repo = Text(werte[RepoSchluessel]).Trim().Trim('/');
        var token = Text(werte[TokenSchluessel]).Trim();
        if (repo.Length == 0 || token.Length == 0)
        {
            throw new AbgleichAusnahme(AbgleichFehler.NichtEingerichtet);
        }
        if (repo.Count(c => c == '/') != 1)
        {
            throw new AbgleichAusnahme(AbgleichFehler.RepositoryFormat);
        }
        NurLesenPruefen(lernstand);     // vor jedem Zugriff aufs Netz
        var ersterAbgleich = Text(werte[ZuletztSchluessel]).Length == 0;
        var ordner = sicherungsordner ?? Path.Combine(Path.GetDirectoryName(lernstand.Pfad)!, Uebernahme.SicherungsOrdner);

        using var sperre = Vorgaenge.Sperren();
        var erhalten = 0;
        var weg = new HashSet<(string, object?)>();
        var ungueltig = 0;
        var gekappt = false;
        var abgelehnt = false;
        var sicherungen = new List<string>();
        AbgleichErgebnis? ergebnis = null;
        for (var versuch = 0; versuch < Versuche && ergebnis is null; versuch++)
        {
            var (sha, fremd, server, geraetZeit) = Holen(netz, repo, token, uhr);
            NurLesenPruefen(lernstand);     // auch nach dem Netz, vor dem Schreiben
            double? dev = server is { } s ? (Mikro(geraetZeit) - s).TotalSeconds : null;
            if (dev is null && Interlocked.Exchange(ref _ohneDatumGemeldet, 1) == 0)
            {
                Uebernahme.Protokoll(lernstand.Pfad, OhneDatumZeile);
            }
            var lokal = LokalAbgleichen(lernstand, fremd, dev, ersterAbgleich, uhr, abweichung, umbenennungen, ordner);
            if (lokal.Sicherung is not null)
            {
                sicherungen.Add(lokal.Sicherung);
                ersterAbgleich = false;
            }
            erhalten += lokal.Erhalten;
            weg.UnionWith(lokal.Weg);
            ungueltig += lokal.Ungueltig;
            gekappt = lokal.Gekappt || gekappt;
            Haltepunkt?.Invoke("vor_hochladen");

            var (gleich, gesendet) = Vergleichen(lokal.Export, fremd);
            if (gleich)
            {
                ergebnis = new AbgleichErgebnis(erhalten, 0, 0, false, false, sicherungen);
                break;
            }
            try
            {
                Hochladen(netz, repo, token, lokal.Export, sha, geraet, uhr);
            }
            catch (Konflikt k)
            {
                abgelehnt = k.Code == 422;
                continue;
            }
            ergebnis = new AbgleichErgebnis(erhalten, gesendet, 0, false, false, sicherungen);
        }
        if (ergebnis is null)
        {
            throw new AbgleichAusnahme(abgelehnt ? AbgleichFehler.HochladenAbgelehnt : AbgleichFehler.Gleichzeitig);
        }
        einstellungen.Setze(ZuletztSchluessel, Uhr.Text(uhr.Jetzt()));
        return ergebnis with { Entfernt = weg.Count, UngueltigeMarke = ungueltig > 0, MarkeGekappt = gekappt };
    }

    private static void NurLesenPruefen(Lernstand lernstand)
    {
        if (lernstand.NurLesenGrund is { } grund)
        {
            throw new AbgleichAusnahme(AbgleichFehler.Ausgesetzt, grund: grund);
        }
    }

    private static AbgleichAusnahme HttpFehler(int code) => code switch
    {
        401 => new AbgleichAusnahme(AbgleichFehler.TokenUngueltig, code),
        403 => new AbgleichAusnahme(AbgleichFehler.KeinSchreibrecht, code),
        404 => new AbgleichAusnahme(AbgleichFehler.NichtGefunden, code),
        _ => new AbgleichAusnahme(AbgleichFehler.Abgelehnt, code),
    };

    // -- Lesen (fetch_remote) ------------------------------------------------

    /// <summary>Gelesene Abgleich-Datei: Modell, rohe Marken, gekappt, mischbetrieb und Kennungen je Tabelle.</summary>
    private sealed class Fremd
    {
        public required Austausch Daten { get; init; }

        /// <summary>Datei ist ein leeres Objekt (Python: falsy).</summary>
        public required bool Leer { get; init; }

        public required Dictionary<string, JsonElement> RoheMarken { get; init; }

        public required Dictionary<string, JsonElement> Gekappt { get; init; }

        public required object? Misch { get; init; }

        public required Dictionary<string, HashSet<string>> Uids { get; init; }
    }

    /// <summary>
    /// fetch_remote: (sha, Datei oder null beim ersten Abgleich, Uhrzeit von GitHub aus der letzten
    /// erfolgreichen Antwort, Geraeteuhr in diesem Moment).
    /// </summary>
    private static (JsonNode? Sha, Fremd? Fremd, DateTimeOffset? Server, DateTimeOffset Geraet) Holen(GitHubZugang netz, string repo,
        string token, Uhr uhr)
    {
        DateTimeOffset? server = null;
        var geraet = uhr.Jetzt();
        GitHubZugang.Antwort Holen1(string pfad, string? accept = null)
        {
            var a = accept is null ? netz.Anfrage(HttpMethod.Get, pfad, token) : netz.Anfrage(HttpMethod.Get, pfad, token, null, accept);
            if (a.Datum is { } d)
            {
                server = d.ToUniversalTime();
                geraet = uhr.Jetzt();
            }
            return a;
        }
        var datei = "/repos/" + repo + "/contents/" + GitHubZugang.Datei;
        JsonNode? meta;
        try
        {
            var antwort = Holen1(datei);
            meta = JsonNode.Parse(new UTF8Encoding(false, true).GetString(antwort.Inhalt));
        }
        catch (Exception e) when (e is JsonException or DecoderFallbackException or ArgumentException)
        {
            throw new AbgleichAusnahme(AbgleichFehler.Unvollstaendig);   // kein JSON
        }
        catch (GitHubAbgelehnt e)
        {
            if (e.Code != 404)
            {
                throw HttpFehler(e.Code);
            }
            // 404 heisst entweder "Datei fehlt noch" oder "Repo nicht erreichbar"
            try
            {
                Holen1("/repos/" + repo);
            }
            catch (GitHubAbgelehnt r)
            {
                throw HttpFehler(r.Code);
            }
            return (null, null, server, geraet);
        }
        if (meta is not JsonObject objekt)
        {
            throw new AbgleichAusnahme(AbgleichFehler.Unvollstaendig);
        }
        byte[] roh;
        try
        {
            if (objekt["encoding"] is JsonValue enc && enc.TryGetValue<string>(out var art) && art == "base64"
                && Loeschmarken.Wahr(Wert(objekt["content"])))
            {
                roh = Base64(objekt["content"]);
            }
            else
            {
                // Ab 1 MB liefert GitHub den Inhalt nur noch ueber die Rohdaten
                roh = Holen1(datei, "application/vnd.github.raw").Inhalt;
            }
        }
        catch (GitHubAbgelehnt e)
        {
            throw HttpFehler(e.Code);
        }
        Austausch daten;
        try
        {
            daten = Uebernahme.LeseAbgleichDatei(roh);
        }
        catch (UebernahmeAusnahme e)
        {
            throw new AbgleichAusnahme(e.Fehler == UebernahmeFehler.ZuNeu ? AbgleichFehler.ZuNeu : AbgleichFehler.DateiKaputt);
        }
        return (objekt["sha"]?.DeepClone(), Felder(roh, daten), server, geraet);
    }

    /// <summary>JSON-Wert als Zellwert (fuer Wahr): Text, Zahl, true/false, Liste/Objekt.</summary>
    private static object? Wert(JsonNode? knoten) => knoten switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.TryGetValue<double>(out var d) => d,
        _ => knoten,
    };

    /// <summary>base64.b64decode (nicht streng): Zeichen ausserhalb des Alphabets fallen weg, falsches Auffuellen = beschaedigt.</summary>
    private static byte[] Base64(JsonNode? inhalt)
    {
        if (inhalt is not JsonValue v || !v.TryGetValue<string>(out var text))
        {
            throw new AbgleichAusnahme(AbgleichFehler.DateiKaputt);     // TypeError
        }
        var sauber = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '+' or '/' or '=')
            {
                sauber.Append(c);
            }
        }
        try
        {
            return Convert.FromBase64String(sauber.ToString());
        }
        catch (FormatException)
        {
            throw new AbgleichAusnahme(AbgleichFehler.DateiKaputt);
        }
    }

    /// <summary>Felder, die das Modell nicht traegt: rohe Marken, gekappt, mischbetrieb, Kennungen je Tabelle (_uids).</summary>
    private static Fremd Felder(byte[] roh, Austausch daten)
    {
        using var eingabe = new MemoryStream(roh);
        using var gz = new GZipStream(eingabe, CompressionMode.Decompress);
        using var dokument = JsonDocument.Parse(gz);
        var wurzel = dokument.RootElement;
        var rohMarken = new Dictionary<string, JsonElement>();
        if (wurzel.TryGetProperty("markers", out var m) && m.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in m.EnumerateObject())
            {
                rohMarken[p.Name] = p.Value.Clone();
            }
        }
        var gekappt = new Dictionary<string, JsonElement>();
        if (wurzel.TryGetProperty(Ruecksicherung.GekapptFeld, out var g) && g.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in g.EnumerateObject())
            {
                gekappt[p.Name] = p.Value.Clone();
            }
        }
        object? misch = wurzel.TryGetProperty(Loeschmarken.MischbetriebSchluessel, out var mb) && mb.ValueKind == JsonValueKind.String
            ? mb.GetString() : null;
        var uids = new Dictionary<string, HashSet<string>>();
        if (wurzel.TryGetProperty("tables", out var t) && t.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in t.EnumerateObject())
            {
                // Python {row[0] for row in rows}: Zeilen ohne Wert waeren dort ein Programmfehler
                if (p.Value.ValueKind != JsonValueKind.Array || p.Value.EnumerateArray()
                        .Any(z => z.ValueKind != JsonValueKind.Array || z.GetArrayLength() == 0))
                {
                    throw new AbgleichAusnahme(AbgleichFehler.DateiKaputt);
                }
                uids[p.Name] = p.Value.EnumerateArray().Select(z => Schluessel(z[0])).ToHashSet(StringComparer.Ordinal);
            }
        }
        return new Fremd
        {
            Daten = daten,
            Leer = wurzel.ValueKind == JsonValueKind.Object && !wurzel.EnumerateObject().Any(),
            RoheMarken = rohMarken,
            Gekappt = gekappt,
            Misch = misch,
            Uids = uids,
        };
    }

    /// <summary>Vergleichsschluessel einer Kennung wie Pythons Gleichheit (1 == 1.0 == True, "1" != 1).</summary>
    private static string Schluessel(JsonElement wert) => wert.ValueKind switch
    {
        JsonValueKind.String => "s:" + wert.GetString(),
        JsonValueKind.True => "n:1",
        JsonValueKind.False => "n:0",
        JsonValueKind.Number => wert.GetRawText().IndexOfAny(['.', 'e', 'E']) < 0
            ? "n:" + BigInteger.Parse(wert.GetRawText(), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
            : Zahlschluessel(wert.GetDouble()),
        JsonValueKind.Null => "z",
        _ => "x:" + wert.GetRawText(),
    };

    private static string Schluessel(object? wert) => wert switch
    {
        null => "z",
        string s => "s:" + s,
        long n => "n:" + n.ToString(CultureInfo.InvariantCulture),
        int n => "n:" + n.ToString(CultureInfo.InvariantCulture),
        double d => Zahlschluessel(d),
        bool b => b ? "n:1" : "n:0",
        byte[] b => "b:" + Convert.ToHexString(b),
        _ => "x:" + wert,
    };

    private static string Zahlschluessel(double d) => double.IsFinite(d) && Math.Floor(d) == d
        ? "n:" + new BigInteger(d).ToString(CultureInfo.InvariantCulture)
        : "f:" + PythonJson.Zahl(d);

    // -- Vergleich und Hochladen ----------------------------------------------

    /// <summary>
    /// Wie sync nach dem Zusammenfuehren: gleich (nichts hochzuladen), wenn es die Datei gibt und
    /// Kennungen und Marken uebereinstimmen; sonst die Zahl der eigenen Eintraege, die die Datei nicht kennt.
    /// </summary>
    private static (bool Gleich, int Gesendet) Vergleichen(PyObjekt export, Fremd? fremd)
    {
        var eigene = Uids(export);
        var andere = fremd is { Leer: false } ? fremd.Uids : new Dictionary<string, HashSet<string>>();
        if (fremd is { Leer: false } && UidsGleich(eigene, andere) && MarkenGleich((PyObjekt)export["markers"]!, fremd.RoheMarken))
        {
            return (true, 0);
        }
        return (false, eigene.Sum(p => p.Value.Count(u => !andere.TryGetValue(p.Key, out var r) || !r.Contains(u))));
    }

    /// <summary>Ergebnis eines Versuchs ohne Netz (Vergleichswerte G21).</summary>
    internal sealed record VersuchErgebnis(PyObjekt Export, int Erhalten, int Weg, int Ungueltig, bool Gekappt, bool Misch,
        bool Gleich, int Gesendet);

    /// <summary>Nur fuer Tests (G21): ein Versuch von sync ohne Netz mit gegebener Datei (null = fehlt) und Abweichung.</summary>
    internal static VersuchErgebnis Versuch(Lernstand lernstand, byte[]? datei, double? dev, Uhr uhr, Uhrabweichung abweichung,
        IReadOnlyList<(string Tabelle, string Alt, string Neu)> umbenennungen, string sicherungsordner)
    {
        var fremd = datei is null ? null : Felder(datei, Uebernahme.LeseAbgleichDatei(datei));
        var lokal = LokalAbgleichen(lernstand, fremd, dev, false, uhr, abweichung, umbenennungen, sicherungsordner);
        var (gleich, gesendet) = Vergleichen(lokal.Export, fremd);
        return new VersuchErgebnis(lokal.Export, lokal.Erhalten, lokal.Weg.Count, lokal.Ungueltig, lokal.Gekappt, lokal.Misch,
            gleich, gesendet);
    }

    /// <summary>_uids(export_local): Kennungen je Tabelle.</summary>
    private static Dictionary<string, HashSet<string>> Uids(PyObjekt export) =>
        ((PyObjekt)export["tables"]!).ToDictionary(p => p.Key,
            p => ((List<object?>)p.Value!).Select(z => Schluessel(((object?[])z!)[0])).ToHashSet(StringComparer.Ordinal));

    private static bool UidsGleich(Dictionary<string, HashSet<string>> a, Dictionary<string, HashSet<string>> b) =>
        a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var r) && p.Value.SetEquals(r));

    /// <summary>local["markers"] == raw_markers (gleiche Schluessel, gleiche Werte samt Art).</summary>
    private static bool MarkenGleich(PyObjekt eigene, Dictionary<string, JsonElement> roh) =>
        eigene.Count == roh.Count && eigene.All(p => roh.TryGetValue(p.Key, out var r)
            && (p.Value, r.ValueKind) switch
            {
                (string s, JsonValueKind.String) => s == r.GetString(),
                (null, JsonValueKind.Null) => true,
                _ => false,
            });

    private sealed class Konflikt(int code) : Exception("Konflikt")
    {
        public int Code { get; } = code;
    }

    /// <summary>upload: gzip (mtime 0) des kompakten JSON, base64, Commit mit noreply-Adresse; 409/422 = Konflikt.</summary>
    private static void Hochladen(GitHubZugang netz, string repo, string token, PyObjekt export, JsonNode? sha, string geraet, Uhr uhr)
    {
        byte[] gepackt;
        using (var ziel = new MemoryStream())
        {
            using (var gz = new GZipStream(ziel, CompressionLevel.SmallestSize, true))
            {
                gz.Write(PythonJson.Utf8(PythonJson.Text(export)));
            }
            gepackt = ziel.ToArray();
        }
        var koerper = new JsonObject
        {
            ["message"] = "Lernstand von " + (geraet.Length > 0 ? geraet : "unbekanntem Gerät") + " ("
                + uhr.Jetzt().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) + ")",
            ["content"] = Convert.ToBase64String(gepackt),
        };
        if (Loeschmarken.Wahr(Wert(sha)))
        {
            koerper["sha"] = sha!.DeepClone();
        }
        if (Committer(netz, token) is { } person)
        {
            koerper["committer"] = person;
            koerper["author"] = person.DeepClone();
        }
        try
        {
            netz.Anfrage(HttpMethod.Put, "/repos/" + repo + "/contents/" + GitHubZugang.Datei, token,
                Encoding.UTF8.GetBytes(koerper.ToJsonString()));
        }
        catch (GitHubAbgelehnt e)
        {
            if (e.Code is 409 or 422)
            {
                throw new Konflikt(e.Code);
            }
            throw HttpFehler(e.Code);
        }
    }

    /// <summary>_committer: Name und noreply-Adresse des Kontos (nie eine private Adresse), sonst null.</summary>
    private static JsonObject? Committer(GitHubZugang netz, string token)
    {
        try
        {
            if (JsonNode.Parse(netz.Anfrage(HttpMethod.Get, "/user", token).Inhalt) is JsonObject konto
                && konto["login"] is JsonValue l && l.TryGetValue<string>(out var login)
                && konto["id"] is JsonValue i && i.TryGetValue<long>(out var id))
            {
                return new JsonObject
                {
                    ["name"] = login,
                    ["email"] = id.ToString(CultureInfo.InvariantCulture) + "+" + login + "@users.noreply.github.com",
                };
            }
        }
        catch (Exception e) when (e is GitHubAbgelehnt or AbgleichAusnahme or JsonException or ArgumentException)
        {
        }
        return null;
    }

    // -- Lokale Schritte (eine Transaktion je Versuch) -------------------------

    private sealed record Lokal(int Erhalten, IReadOnlySet<(string Tabelle, object? Uid)> Weg, int Ungueltig, bool Gekappt,
        bool Misch, PyObjekt Export, string? Sicherung);

    private sealed record Schritt(int Erhalten, IReadOnlySet<(string Tabelle, object? Uid)> Weg, int Ungueltig, bool Gekappt, bool Misch);

    /// <summary>
    /// Alle lokalen Schritte eines Versuchs in einer Transaktion (BEGIN IMMEDIATE). Zuerst als Probe
    /// (Sicherungspunkt): Aendern oder entfernen sie vorhandene Eintraege (Loeschmarke, geloeschte
    /// Spieldurchgaenge, aeltere Projektfassungen, Fragetexte, Uhr-Nachtrag) oder wurde noch nie
    /// abgeglichen, wird die Probe zurueckgenommen, die Datenbank nach sicherungen/vor_abgleich_*.db
    /// gesichert und dann geschrieben. Scheitert die Sicherung: nichts geaendert (T5).
    /// </summary>
    private static Lokal LokalAbgleichen(Lernstand lernstand, Fremd? fremd, double? dev, bool ersterAbgleich, Uhr uhr,
        Uhrabweichung abweichung, IReadOnlyList<(string Tabelle, string Alt, string Neu)> umbenennungen, string ordner)
    {
        var jetzt = uhr.Jetzt().ToUniversalTime();
        var fest = new Uhr(() => jetzt, uhr.Zone);   // ein Zeitpunkt fuer Probe und Schreiben
        try
        {
            using var db = lernstand.Verbindung();
            using var tx = db.BeginTransaction(deferred: false);
            // Probe bleibt im Speicher (nichts vor dem Commit in die Datei), damit die Sicherung
            // ueber eine zweite Verbindung den gespeicherten Stand lesen kann
            using (var befehl = db.CreateCommand())
            {
                befehl.Transaction = tx;
                befehl.CommandText = "PRAGMA cache_spill = false";
                befehl.ExecuteNonQuery();
            }
            var vorher = Stand(db, tx);
            tx.Save("probe");
            var schritt = Schritte(db, tx, fremd, dev, fest, abweichung, umbenennungen);
            string? sicherung = null;
            if (ersterAbgleich || Geaendert(vorher, Stand(db, tx)))
            {
                tx.Rollback("probe");
                try
                {
                    sicherung = Uebernahme.SicherungAnlegen(lernstand, ordner, fest, SicherungsPraefix);
                }
                catch (UebernahmeAusnahme)
                {
                    throw new AbgleichAusnahme(AbgleichFehler.SicherungGescheitert);
                }
                schritt = Schritte(db, tx, fremd, dev, fest, abweichung, umbenennungen);
            }
            else
            {
                tx.Release("probe");
            }
            var export = Ruecksicherung.Austausch(db, tx);
            tx.Commit();
            return new Lokal(schritt.Erhalten, schritt.Weg, schritt.Ungueltig, schritt.Gekappt, schritt.Misch, export, sicherung);
        }
        catch (SqliteException e)
        {
            Uebernahme.Protokoll(lernstand.Pfad, "Abgleich gescheitert, nichts geaendert: SQLite-Fehler "
                + e.SqliteErrorCode.ToString(CultureInfo.InvariantCulture));
            throw new AbgleichAusnahme(AbgleichFehler.Unerwartet);
        }
        catch (Exception e) when (e is FormatException or OverflowException or ArgumentOutOfRangeException or InvalidCastException)
        {
            Uebernahme.Protokoll(lernstand.Pfad, "Abgleich gescheitert, nichts geaendert: " + e.GetType().Name);
            throw new AbgleichAusnahme(AbgleichFehler.Unerwartet);
        }
    }

    /// <summary>Inhalt aller Zeilen (Tabelle, rowid) als Text mit Art der Werte.</summary>
    private static Dictionary<(string, long), string> Stand(SqliteConnection db, SqliteTransaction tx)
    {
        var stand = new Dictionary<(string, long), string>();
        foreach (var (tabelle, _) in Uebernahme.Tabellen)
        {
            using var befehl = db.CreateCommand();
            befehl.Transaction = tx;
            befehl.CommandText = $"SELECT rowid, * FROM {tabelle}";
            using var leser = befehl.ExecuteReader();
            while (leser.Read())
            {
                var text = new StringBuilder();
                for (var i = 1; i < leser.FieldCount; i++)
                {
                    text.Append(Schluessel(leser.IsDBNull(i) ? null : leser.GetValue(i))).Append('\u0001');
                }
                stand[(tabelle, leser.GetInt64(0))] = text.ToString();
            }
        }
        return stand;
    }

    private static bool Geaendert(Dictionary<(string, long), string> vorher, Dictionary<(string, long), string> nachher) =>
        vorher.Any(p => !nachher.TryGetValue(p.Key, out var neu) || neu != p.Value);

    private static Schritt Schritte(SqliteConnection db, SqliteTransaction tx, Fremd? fremd, double? dev, Uhr uhr,
        Uhrabweichung abweichung, IReadOnlyList<(string Tabelle, string Alt, string Neu)> umbenennungen)
    {
        var misch = MischMerken(db, tx, fremd, dev, uhr);
        var daten = fremd?.Daten;
        var gekappt = false;
        if (dev is { } d)
        {
            EigeneNachtragen(db, tx, fremd, d, uhr, abweichung);
            MetaSetzen(db, tx, Uhrabweichung.Schluessel, abweichung.Messung(d, uhr));
            (gekappt, daten) = Kappen(db, tx, fremd, d, misch, uhr);
        }
        if (fremd is { Leer: false } && daten is not null)
        {
            var weg = new HashSet<(string, object?)>();
            var bericht = Uebernahme.Zusammenfuehren(db, tx, daten, umbenennungen, uhr, false,
                new AbgleichModus(misch, dev is { } d2 ? uhr.Jetzt() - Sek(d2) : null), weg);
            return new Schritt(bericht.Uebernommen, weg, bericht.UngueltigeMarken, gekappt, misch);
        }
        return new Schritt(0, new HashSet<(string, object?)>(), 0, gekappt, misch);
    }

    /// <summary>timedelta(seconds=x): auf Mikrosekunden gerundet (halb gerade).</summary>
    private static TimeSpan Sek(double sekunden) =>
        TimeSpan.FromTicks(checked((long)Math.Round(sekunden * 1e6, MidpointRounding.ToEven) * 10));

    /// <summary>Zeitpunkt auf Mikrosekunden gekuerzt (Python datetime).</summary>
    private static DateTimeOffset Mikro(DateTimeOffset t) => new(t.Ticks - t.Ticks % 10, t.Offset);

    private static object? Meta(SqliteConnection db, SqliteTransaction tx, string schluessel)
    {
        using var befehl = db.CreateCommand();
        befehl.Transaction = tx;
        befehl.CommandText = "SELECT value FROM sync_meta WHERE key = $k";
        befehl.Parameters.AddWithValue("$k", schluessel);
        using var leser = befehl.ExecuteReader();
        return leser.Read() && !leser.IsDBNull(0) ? leser.GetValue(0) : null;
    }

    private static void MetaSetzen(SqliteConnection db, SqliteTransaction tx, string schluessel, object? wert)
    {
        using var befehl = db.CreateCommand();
        befehl.Transaction = tx;
        befehl.CommandText = "INSERT OR REPLACE INTO sync_meta (key, value) VALUES ($k, $v)";
        befehl.Parameters.AddWithValue("$k", schluessel);
        befehl.Parameters.AddWithValue("$v", wert ?? DBNull.Value);
        befehl.ExecuteNonQuery();
    }

    /// <summary>
    /// _note_mixed: merkt sich, ob ein Geraet vor 0.62.4 mit abgleicht, mit der Uhrzeit von GitHub
    /// (sonst Geraeteuhr); true, solange das hoechstens 7 Tage her ist (_mixed_recent).
    /// </summary>
    private static bool MischMerken(SqliteConnection db, SqliteTransaction tx, Fremd? fremd, double? dev, Uhr uhr)
    {
        var jetzt = Mikro(uhr.Jetzt()).ToUniversalTime();
        if (dev is { } d)
        {
            jetzt -= Sek(d);
        }
        var lokal = Loeschmarken.IsoUtc(Meta(db, tx, Loeschmarken.MischbetriebSchluessel));
        var grenze = dev is not null ? Uhrabweichung.Toleranz : Loeschmarken.MischbetriebSekunden;
        if (lokal is { } l && (l - jetzt).TotalSeconds > grenze)
        {
            lokal = null;
            MetaSetzen(db, tx, Loeschmarken.MischbetriebSchluessel, "");
        }
        if (fremd is not null && !fremd.Daten.MitSchreiber)
        {
            MetaSetzen(db, tx, Loeschmarken.MischbetriebSchluessel, Uhrabweichung.Iso(jetzt));
        }
        else if (fremd is not null && Loeschmarken.IsoUtc(fremd.Misch) is { } gesehen
                 && (gesehen - jetzt).TotalSeconds <= grenze && (lokal is null || gesehen > lokal))
        {
            MetaSetzen(db, tx, Loeschmarken.MischbetriebSchluessel, Uhrabweichung.Iso(gesehen));
        }
        var zuletzt = Loeschmarken.IsoUtc(Meta(db, tx, Loeschmarken.MischbetriebSchluessel));
        return zuletzt is { } z && (jetzt - z).TotalSeconds <= Loeschmarken.MischbetriebSekunden;
    }

    /// <summary>
    /// _correct_own_entries: traegt eine neu gemessene Abweichung der Geraeteuhr in utc_offset der
    /// eigenen, noch nicht hochgeladenen Eintraege der letzten 24 h nach (Regeln wie Python 0.63.2,
    /// R-d/F19: mehrdeutig = behalten).
    /// </summary>
    private static void EigeneNachtragen(SqliteConnection db, SqliteTransaction tx, Fremd? fremd, double dev, Uhr uhr, Uhrabweichung abweichung)
    {
        if (Math.Abs(dev) <= Uhrabweichung.Toleranz)
        {
            return;
        }
        var jetzt = Mikro(uhr.Jetzt());
        var messung = Meta(db, tx, Uhrabweichung.Schluessel);
        if (abweichung.Abweichung(messung, jetzt) is { } vorher && Math.Abs(vorher) > Uhrabweichung.Toleranz)
        {
            return;
        }
        var verschiebung = PythonRunden.Runde(dev / 60.0);
        var letzte = Uhrabweichung.LetzteMessung(messung);
        DateTime? fruehestens = letzte is { } lm ? lm.Gemessen - Sek(lm.Sekunden + Uhrabweichung.Toleranz) : null;
        (DateTime Vor, DateTime Nach)? grenzen = null;
        if (dev > 0 && letzte is { } vorige)
        {
            var sprung = dev - vorige.Sekunden;
            var jetztUtc = jetzt.UtcDateTime;
            var laufend = abweichung.SprungSeit(messung, (DateTime.SpecifyKind(jetztUtc, DateTimeKind.Unspecified) - vorige.Gemessen).TotalSeconds);
            var bestaetigt = laufend is { } r && Math.Abs(r - sprung) <= Uhrabweichung.Toleranz;
            var toleranz = TimeSpan.FromSeconds(Uhrabweichung.Toleranz);
            if (Math.Abs(vorige.Sekunden) <= Uhrabweichung.Toleranz && bestaetigt)
            {
                grenzen = (vorige.Gemessen + Sek(sprung) + toleranz,
                    DateTime.SpecifyKind(jetztUtc, DateTimeKind.Unspecified) - Sek(sprung) + toleranz);
            }
            else if (Math.Abs(vorige.Sekunden) > Uhrabweichung.Toleranz && Math.Abs(sprung) <= Uhrabweichung.Toleranz && bestaetigt)
            {
                grenzen = null;
            }
            else
            {
                grenzen = (DateTime.MaxValue, DateTime.MaxValue);
            }
        }
        var bekannt = fremd is { Leer: false } ? fremd.Uids : new Dictionary<string, HashSet<string>>();
        var seit = Uhr.Text(jetzt - TimeSpan.FromSeconds(Uhrabweichung.Gueltig));
        foreach (var (tabelle, spalten) in Uebernahme.Tabellen)
        {
            if (!spalten.Contains("utc_offset"))
            {
                continue;
            }
            var auslassen = bekannt.GetValueOrDefault(tabelle) ?? [];
            var aenderungen = new List<(object Wert, long Zeile)>();
            using (var befehl = db.CreateCommand())
            {
                befehl.Transaction = tx;
                befehl.CommandText = $"SELECT rowid, uid, timestamp, utc_offset FROM {tabelle} WHERE utc_offset IS NOT NULL AND timestamp >= $s";
                befehl.Parameters.AddWithValue("$s", seit);
                using var leser = befehl.ExecuteReader();
                while (leser.Read())
                {
                    var uid = leser.IsDBNull(1) ? null : leser.GetValue(1);
                    if (auslassen.Contains(Schluessel(uid)))
                    {
                        continue;
                    }
                    var zeit = PyText.Str(leser.IsDBNull(2) ? null : leser.GetValue(2));
                    var abstand = leser.GetValue(3);
                    if (Loeschmarken.Ortszeit(PyText.Teil(zeit, 0, 19)) is not { } moment)
                    {
                        continue;
                    }
                    double? minuten = abstand switch
                    {
                        long n => n,
                        double x => x,
                        _ => null,
                    };
                    if (minuten is not { } min || !Abstaende(moment, uhr.Zone).Contains(min))
                    {
                        continue;
                    }
                    if (fruehestens is { } f && moment - TimeSpan.FromTicks(checked((long)Math.Round((min + verschiebung) * 60e6, MidpointRounding.ToEven) * 10)) < f)
                    {
                        continue;
                    }
                    var eigen = moment - TimeSpan.FromTicks(checked((long)Math.Round(min * 60e6, MidpointRounding.ToEven) * 10));
                    if (grenzen is { } g && eigen < g.Vor)
                    {
                        continue;       // sicher vor dem Stellen (Vorrang)
                    }
                    if (grenzen is { } g2 && !(eigen > g2.Nach))
                    {
                        continue;       // mehrdeutig: behalten
                    }
                    aenderungen.Add((abstand is long n2 ? n2 + verschiebung : (object)((double)abstand + verschiebung), leser.GetInt64(0)));
                }
            }
            foreach (var (wert, zeile) in aenderungen)
            {
                using var aendern = db.CreateCommand();
                aendern.Transaction = tx;
                aendern.CommandText = $"UPDATE {tabelle} SET utc_offset = $w WHERE rowid = $r";
                aendern.Parameters.AddWithValue("$w", wert);
                aendern.Parameters.AddWithValue("$r", zeile);
                aendern.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// Beide Lesarten einer Ortszeit ohne Zone (fold 0 und 1): in der doppelten Stunde und in der
    /// Luecke der Zeitumstellung beide Abstaende, sonst einer (in ganzen Minuten).
    /// </summary>
    internal static HashSet<double> Abstaende(DateTime ortszeit, TimeZoneInfo zone)
    {
        var t = DateTime.SpecifyKind(ortszeit, DateTimeKind.Unspecified);
        IEnumerable<TimeSpan> abstaende = zone.IsAmbiguousTime(t) ? zone.GetAmbiguousTimeOffsets(t)
            : zone.IsInvalidTime(t) ? [zone.GetUtcOffset(t.AddHours(-3)), zone.GetUtcOffset(t.AddHours(3))]
            : [zone.GetUtcOffset(t)];
        return abstaende.Select(a => Math.Floor(a.TotalSeconds / 60)).ToHashSet();
    }

    /// <summary>
    /// _clamp_future_markers: Marken mit Zone mehr als 300 s nach der Uhrzeit von GitHub einmal auf
    /// diese Uhrzeit setzen, lokal und in der gelesenen Datei (gekappt merkt sich [alt, neu]).
    /// Liefert (gesetzt, Datei mit den gesetzten Marken).
    /// </summary>
    private static (bool, Austausch?) Kappen(SqliteConnection db, SqliteTransaction tx, Fremd? fremd, double dev, bool misch, Uhr uhr)
    {
        if (fremd is not null && !fremd.Daten.MitSchreiber)
        {
            return (false, fremd.Daten);
        }
        var server = uhr.Ortszeit(Mikro(uhr.Jetzt()) - Sek(dev));
        var grenze = DateTime.SpecifyKind(server.UtcDateTime, DateTimeKind.Unspecified).AddSeconds(Uhrabweichung.Toleranz);
        var wertS = Uhr.MitZone(server);
        var fremdeMarken = fremd is null ? new Dictionary<string, object?>() : new Dictionary<string, object?>(fremd.Daten.Marken);
        var fremdeKappung = fremd?.Gekappt ?? [];
        bool Zukunft(object? wert) => Loeschmarken.Lesen(wert) is { Utc: { } utc } && utc > grenze;
        var geaendert = false;
        foreach (var k in Loeschmarken.Schluessel)
        {
            var fremdWert = fremdeMarken.GetValueOrDefault(k);
            var lokalWert = Meta(db, tx, k);
            if (misch && Zukunft(fremdWert))
            {
                continue;
            }
            var bekannt = fremdeKappung.TryGetValue(k, out var roh) ? Ruecksicherung.Kappung(roh.ValueKind == JsonValueKind.String
                ? (object?)roh.GetString() : JsonNode.Parse(roh.GetRawText())) : null;     // Text: als JSON lesen (_clamp_pair)
            var eigen = Ruecksicherung.Kappung(Meta(db, tx, Ruecksicherung.GekapptPraefix + k));
            if (bekannt is not null && eigen is not null && Equals(bekannt[0], eigen[0]) && !Equals(bekannt[1], eigen[1]))
            {
                // Zwei Geraete haben dieselbe Marke gleichzeitig gesetzt: die fruehere Uhrzeit gilt fuer alle
                var erste = Equals(Loeschmarken.Spaeter(bekannt[1], eigen[1]), eigen[1]) ? bekannt : eigen;
                if (Equals(lokalWert, bekannt[1]) || Equals(lokalWert, eigen[1]))
                {
                    MetaSetzen(db, tx, k, erste[1]);
                    lokalWert = erste[1];
                }
                MetaSetzen(db, tx, Ruecksicherung.GekapptPraefix + k, PaarText(erste));
                eigen = erste;
            }
            else if (bekannt is not null && eigen is null)
            {
                MetaSetzen(db, tx, Ruecksicherung.GekapptPraefix + k, PaarText(bekannt));
                eigen = bekannt;
            }
            foreach (var wert in new[] { fremdWert, lokalWert })
            {
                if (!Zukunft(wert))
                {
                    continue;
                }
                var ziel = eigen is not null && Equals(eigen[0], wert) ? eigen[1] : wertS;
                if (eigen is null || !Equals(eigen[0], wert))
                {
                    eigen = [wert, ziel];
                    MetaSetzen(db, tx, Ruecksicherung.GekapptPraefix + k, PaarText(eigen));
                }
                if (Equals(wert, fremdWert))
                {
                    fremdeMarken[k] = ziel;
                }
                if (Equals(wert, lokalWert))
                {
                    MetaSetzen(db, tx, k, ziel);
                    lokalWert = ziel;
                }
                geaendert = true;
            }
        }
        if (fremd is null)
        {
            return (geaendert, null);
        }
        var d = fremd.Daten;
        return (geaendert, new Austausch
        {
            Quelle = d.Quelle, MitSchreiber = d.MitSchreiber, Marken = fremdeMarken, Aufhebungen = d.Aufhebungen,
            Spalten = d.Spalten, Tabellen = d.Tabellen, AppVersion = d.AppVersion, Erstellt = d.Erstellt, QuellPfad = d.QuellPfad,
        });
    }

    /// <summary>json.dumps([alt, neu]) wie Python (ensure_ascii, Trenner ", ").</summary>
    private static string PaarText(List<object?> paar) => Loeschmarken.PaarText([(string)paar[0]!, (string)paar[1]!]);
}
