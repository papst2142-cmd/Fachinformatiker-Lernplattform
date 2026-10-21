using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, L10 (Weisung P2-11 A8): Attrappe der GitHub-Contents-API wie
/// python-linie pruefung/abgleich_attrappe.py, nur auf 127.0.0.1, ohne TLS und ohne echten
/// Zugangsschluessel. Nachgeahmt werden genau die Aufrufe des Abgleichs:
///   GET  /repos/o/r/contents/lernstand.json.gz (JSON mit sha + base64, umbrochen alle 60 Zeichen;
///        mit Gross wie GitHub ab 1 MB: content leer, Rohdaten per Accept raw)
///   GET  /repos/o/r (Repo vorhanden?), GET /user (Name fuer den Commit)
///   PUT  /repos/o/r/contents/lernstand.json.gz (falscher sha 409, fehlender sha 422)
/// Steuerung ueber Eigenschaften statt POST /_ctl. Eigener kleiner HTTP-Server auf einem
/// TCP-Anschluss, damit auch kaputte Antworten (abgeschnitten, ohne Date-Kopf) moeglich sind.
/// Die Adresse kommt nur ueber den internen Konstruktor von GitHubZugang in den Abgleich (A9).
/// </summary>
internal sealed class GitHubAttrappe : IDisposable
{
    public const string Datei = "lernstand.json.gz";

    private readonly TcpListener _lauscher;
    private readonly Thread _faden;
    private readonly object _sperre = new();
    private volatile bool _ende;

    public GitHubAttrappe()
    {
        _lauscher = new TcpListener(IPAddress.Loopback, 0);
        _lauscher.Start();
        Basis = new Uri("http://127.0.0.1:" + ((IPEndPoint)_lauscher.LocalEndpoint).Port.ToString(CultureInfo.InvariantCulture));
        _faden = new Thread(Bedienen) { IsBackground = true, Name = "github-attrappe" };
        _faden.Start();
    }

    public Uri Basis { get; }

    // -- Zustand -------------------------------------------------------------

    public byte[]? Roh { get; private set; }

    public string? Sha { get; private set; }

    public int Commits { get; private set; }

    public List<(string Methode, string Pfad, int Code)> Log { get; } = [];

    /// <summary>Alle Anfragen roh (Kopf und Koerper) fuer die Suche nach dem Zugangsschluessel.</summary>
    public List<string> Anfragen { get; } = [];

    /// <summary>Alle Antworten roh (Kopf und Koerper).</summary>
    public List<string> Antworten { get; } = [];

    public List<JsonObject> PutKoerper { get; } = [];

    // -- Steuerung (wie die Steuerschluessel der Python-Attrappe) ------------

    public int? GetStatus { get; set; }

    public int? RepoStatus { get; set; }

    public int? PutStatus { get; set; }

    /// <summary>Nur die naechsten n PUTs mit PutStatus beantworten (null = alle).</summary>
    public int? PutStatusN { get; set; }

    /// <summary>"abgeschnitten" | "kein_json" | "liste" | "html".</summary>
    public string? GetBody { get; set; }

    public bool Gross { get; set; }

    /// <summary>Uhrzeit von GitHub fuer den Date-Kopf; null = Antworten ohne Date-Kopf.</summary>
    public Func<DateTimeOffset>? Datum { get; set; } = () => DateTimeOffset.UtcNow;

    /// <summary>Nach jedem GET der Datei legt "ein anderes Geraet" so viele neue Eintraege hinein (sha aendert sich).</summary>
    public int FremdProGet { get; set; }

    /// <summary>Fehlerantworten enthalten die Kopfzeilen der Anfrage (Zugangsschluessel; Test A7).</summary>
    public bool KopfZurueck { get; set; }

    /// <summary>Antworten verzoegern (Zeitueberschreitung).</summary>
    public TimeSpan Verzoegerung { get; set; }

    /// <summary>Nur das Hochladen (PUT) verzoegern; PutBegonnen wird gesetzt, sobald es angekommen ist (P2-12 A1).</summary>
    public TimeSpan PutVerzoegerung { get; set; }

    public ManualResetEventSlim PutBegonnen { get; } = new();

    /// <summary>Datei vorgeben (gzip-Bytes), sha wie git (blob).</summary>
    public void Speichern(byte[] roh)
    {
        lock (_sperre)
        {
            Roh = roh;
            Sha = Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes("blob " + roh.Length.ToString(CultureInfo.InvariantCulture) + "\0"), .. roh]));
            Commits++;
        }
    }

    /// <summary>Gespeicherte Datei als JSON-Text (entpackt) oder null.</summary>
    public string? Text()
    {
        lock (_sperre)
        {
            return Roh is null ? null : Entpacken(Roh);
        }
    }

    public static string Entpacken(byte[] roh)
    {
        using var ein = new MemoryStream(roh);
        using var gz = new GZipStream(ein, CompressionMode.Decompress);
        using var aus = new MemoryStream();
        gz.CopyTo(aus);
        return Encoding.UTF8.GetString(aus.ToArray());
    }

    public static byte[] Packen(string text)
    {
        using var aus = new MemoryStream();
        using (var gz = new GZipStream(aus, CompressionLevel.SmallestSize))
        {
            gz.Write(Encoding.UTF8.GetBytes(text));
        }
        return aus.ToArray();
    }

    private void FremdeEintraege(int anzahl)
    {
        if (Roh is null || JsonNode.Parse(Entpacken(Roh)) is not JsonObject daten)
        {
            return;
        }
        var spalten = daten["columns"]!["card_events"]!.AsArray().Select(s => (string)s!).ToList();
        var tabellen = daten["tables"]!.AsObject();
        if (tabellen["card_events"] is not JsonArray zeilen)
        {
            tabellen["card_events"] = zeilen = [];
        }
        for (var i = 0; i < anzahl; i++)
        {
            var werte = new Dictionary<string, JsonNode?>
            {
                ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                ["category"] = "Fremd",
                ["question"] = "fremd-" + Guid.NewGuid().ToString("N")[..8],
                ["mode"] = "karte",
                ["correct"] = 1,
            };
            var zeile = new JsonArray(Guid.NewGuid().ToString("N"));
            foreach (var s in spalten)
            {
                zeile.Add(werte.GetValueOrDefault(s)?.DeepClone());
            }
            zeilen.Add(zeile);
        }
        Speichern(Packen(daten.ToJsonString()));
    }

    // -- HTTP ----------------------------------------------------------------

    private void Bedienen()
    {
        while (!_ende)
        {
            TcpClient verbindung;
            try
            {
                verbindung = _lauscher.AcceptTcpClient();
            }
            catch (Exception e) when (e is SocketException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }
            try
            {
                using (verbindung)
                {
                    Beantworten(verbindung.GetStream());
                }
            }
            catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException)
            {
            }
        }
    }

    private static string? Zeile(Stream strom)
    {
        var bytes = new List<byte>();
        while (true)
        {
            var b = strom.ReadByte();
            if (b < 0)
            {
                return bytes.Count == 0 ? null : Encoding.Latin1.GetString([.. bytes]);
            }
            if (b == '\n')
            {
                return Encoding.Latin1.GetString([.. bytes]).TrimEnd('\r');
            }
            bytes.Add((byte)b);
        }
    }

    private void Beantworten(Stream strom)
    {
        var start = Zeile(strom);
        if (start is null)
        {
            return;
        }
        var teile = start.Split(' ');
        var (methode, pfad) = (teile[0], teile[1]);
        var koepfe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var roh = new StringBuilder(start).Append('\n');
        while (Zeile(strom) is { Length: > 0 } kopf)
        {
            roh.Append(kopf).Append('\n');
            var stelle = kopf.IndexOf(':');
            koepfe[kopf[..stelle].Trim()] = kopf[(stelle + 1)..].Trim();
        }
        var koerper = Array.Empty<byte>();
        if (koepfe.TryGetValue("Content-Length", out var laenge))
        {
            koerper = new byte[int.Parse(laenge, CultureInfo.InvariantCulture)];
            strom.ReadExactly(koerper);
        }
        roh.Append(Encoding.UTF8.GetString(koerper));
        lock (_sperre)
        {
            Anfragen.Add(roh.ToString());
        }
        if (Verzoegerung > TimeSpan.Zero)
        {
            Thread.Sleep(Verzoegerung);
        }
        if (methode == "PUT" && PutVerzoegerung > TimeSpan.Zero)
        {
            PutBegonnen.Set();
            Thread.Sleep(PutVerzoegerung);
        }
        var (code, inhalt, art, falscheLaenge) = Antwort(methode, pfad, koepfe, koerper, roh.ToString());
        if (code == 0)
        {
            return;
        }
        Senden(strom, methode, pfad, code, inhalt, art, falscheLaenge);
    }

    private (int Code, byte[] Inhalt, string Art, bool FalscheLaenge) Antwort(string methode, string pfad,
        Dictionary<string, string> koepfe, byte[] koerper, string anfrage)
    {
        (int, byte[], string, bool) Json(int code, JsonNode wert) => (code, Encoding.UTF8.GetBytes(
            code >= 400 && KopfZurueck ? new JsonObject { ["message"] = "Attrappe", ["anfrage"] = anfrage }.ToJsonString() : wert.ToJsonString()),
            "application/json", false);
        var teile = pfad.Split('?')[0].Trim('/').Split('/');
        var istDatei = teile.Length == 5 && teile[0] == "repos" && teile[3] == "contents" && teile[4] == Datei;
        var istRepo = teile.Length == 3 && teile[0] == "repos";
        if (!koepfe.TryGetValue("Authorization", out var zugang) || !zugang.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return Json(401, new JsonObject { ["message"] = "Bad credentials" });
        }
        lock (_sperre)
        {
            if (methode == "GET")
            {
                if (pfad == "/user")
                {
                    return Json(200, new JsonObject { ["login"] = "pruef-attrappe", ["id"] = 1 });
                }
                if (istRepo)
                {
                    var c = RepoStatus ?? 200;
                    return Json(c, c == 200 ? new JsonObject { ["full_name"] = "x/y" } : new JsonObject { ["message"] = "Not Found" });
                }
                if (!istDatei)
                {
                    return Json(404, new JsonObject { ["message"] = "Not Found" });
                }
                if (GetStatus is { } gs)
                {
                    return Json(gs, new JsonObject { ["message"] = "Attrappe" });
                }
                switch (GetBody)
                {
                    case "abgeschnitten":
                        return (200, "{\"sha\": \"abc\", \"encoding\": \"base64\", \"content\": \"H4sI"u8.ToArray(), "application/json", true);
                    case "kein_json":
                        return (200, "<<< kein JSON >>>"u8.ToArray(), "application/json", false);
                    case "html":
                        return (200, "<html><body>Captive Portal</body></html>"u8.ToArray(), "text/html", false);
                    case "liste":
                        return (200, "[1, 2, 3]"u8.ToArray(), "application/json", false);
                }
                if (Roh is null)
                {
                    return Json(404, new JsonObject { ["message"] = "Not Found" });
                }
                (int, byte[], string, bool) ergebnis;
                if (koepfe.GetValueOrDefault("Accept", "").Contains("raw", StringComparison.Ordinal))
                {
                    ergebnis = (200, Roh, "application/octet-stream", false);
                }
                else if (Gross)
                {
                    ergebnis = Json(200, new JsonObject { ["sha"] = Sha, ["encoding"] = "none", ["content"] = "", ["size"] = Roh.Length });
                }
                else
                {
                    var b64 = Convert.ToBase64String(Roh);
                    var umbrochen = new StringBuilder();
                    for (var i = 0; i < b64.Length; i += 60)
                    {
                        umbrochen.Append(b64.AsSpan(i, Math.Min(60, b64.Length - i))).Append('\n');
                    }
                    ergebnis = Json(200, new JsonObject { ["sha"] = Sha, ["encoding"] = "base64", ["content"] = umbrochen.ToString(), ["size"] = Roh.Length });
                }
                if (FremdProGet > 0 && !koepfe.GetValueOrDefault("Accept", "").Contains("raw", StringComparison.Ordinal))
                {
                    FremdeEintraege(FremdProGet);
                }
                return ergebnis;
            }
            if (methode == "PUT")
            {
                if (!istDatei)
                {
                    return Json(404, new JsonObject { ["message"] = "Not Found" });
                }
                var body = JsonNode.Parse(koerper)!.AsObject();
                PutKoerper.Add(body);
                if (PutStatus is { } ps && (PutStatusN is null || PutStatusN > 0))
                {
                    if (PutStatusN is { } n)
                    {
                        PutStatusN = n - 1;
                    }
                    return Json(ps, new JsonObject { ["message"] = "Attrappe" });
                }
                if (Roh is not null)
                {
                    if (body["sha"] is null)
                    {
                        return Json(422, new JsonObject { ["message"] = "Invalid request. \"sha\" wasn't supplied." });
                    }
                    if ((string?)body["sha"] != Sha)
                    {
                        return Json(409, new JsonObject { ["message"] = "sha does not match" });
                    }
                }
                Speichern(Convert.FromBase64String((string)body["content"]!));
                return Json(200, new JsonObject { ["content"] = new JsonObject { ["sha"] = Sha } });
            }
        }
        return Json(404, new JsonObject { ["message"] = "Not Found" });
    }

    private void Senden(Stream strom, string methode, string pfad, int code, byte[] inhalt, string art, bool falscheLaenge)
    {
        var kopf = new StringBuilder();
        kopf.Append("HTTP/1.1 ").Append(code.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(Grund(code)).Append("\r\n");
        if (Datum is { } datum)
        {
            kopf.Append("Date: ").Append(datum().UtcDateTime.ToString("r", CultureInfo.InvariantCulture)).Append("\r\n");
        }
        kopf.Append("Content-Type: ").Append(art).Append("\r\n");
        kopf.Append("Content-Length: ").Append((inhalt.Length + (falscheLaenge ? 500 : 0)).ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        kopf.Append("Connection: close\r\n\r\n");
        lock (_sperre)
        {
            Log.Add((methode, pfad, code));
            Antworten.Add(kopf + Encoding.UTF8.GetString(inhalt));
        }
        strom.Write(Encoding.ASCII.GetBytes(kopf.ToString()));
        strom.Write(inhalt);
        strom.Flush();
    }

    private static string Grund(int code) => code switch
    {
        200 => "OK",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        422 => "Unprocessable Entity",
        _ => "Status",
    };

    public void Dispose()
    {
        _ende = true;
        _lauscher.Stop();
        _faden.Join(TimeSpan.FromSeconds(5));
        PutBegonnen.Dispose();
    }
}
