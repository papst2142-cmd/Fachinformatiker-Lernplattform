using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.89.0 (Inhaltspakete W3, Weisung P12-297 W3-4): ein nachgestelltes GitHub fuer den Start-Test. Es liefert unter
/// <c>/releases</c> die Release-Liste <c>Testdaten/pakete_echt/github_releases.json</c> (erzeugt von
/// <c>paket_release.py github-probe</c>, Adressen auf diesen Server) und unter <c>/&lt;datei&gt;.fipaket</c> die echten
/// Paketdateien. Die App laedt darueber mit der echten GitHub-Quelle (<c>GithubQuelle</c>, Pruefung gegen den digest), nur die
/// Adresse ist eine andere. Nur 127.0.0.1, nur GET, je Anfrage eine Verbindung; nur im Start-Test.
/// </summary>
internal sealed class ProbeGithub : IDisposable
{
    private readonly TcpListener _hoerer = new(IPAddress.Loopback, 0);
    private readonly string _ordner;
    private readonly CancellationTokenSource _ende = new();
    private readonly List<string> _abfragen = [];

    public ProbeGithub(string ordner)
    {
        _ordner = ordner;
        _hoerer.Start();
        _ = Task.Run(AnnehmenAsync);
    }

    public string Wurzel => "http://127.0.0.1:" + ((IPEndPoint)_hoerer.LocalEndpoint).Port;

    /// <summary>Adresse der Release-Liste (Wert fuer <c>LERNPLATTFORM_PAKET_QUELLE</c>).</summary>
    public string Liste => Wurzel + "/releases";

    /// <summary>Abgefragte Pfade in der Reihenfolge der Anfragen.</summary>
    public IReadOnlyList<string> Abfragen
    {
        get
        {
            lock (_abfragen)
            {
                return [.. _abfragen];
            }
        }
    }

    private async Task AnnehmenAsync()
    {
        while (!_ende.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _hoerer.AcceptTcpClientAsync(_ende.Token).ConfigureAwait(false);
            }
            catch (Exception x) when (x is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }
            _ = Task.Run(() => AntwortenAsync(client));
        }
    }

    private async Task AntwortenAsync(TcpClient client)
    {
        using var _ = client;
        try
        {
            var strom = client.GetStream();
            using var leser = new StreamReader(strom, Encoding.ASCII, false, 1024, leaveOpen: true);
            var zeile = await leser.ReadLineAsync(_ende.Token).ConfigureAwait(false) ?? "";
            while (!string.IsNullOrEmpty(await leser.ReadLineAsync(_ende.Token).ConfigureAwait(false)))
            {
            }
            var teile = zeile.Split(' ');
            var pfad = teile.Length > 1 ? teile[1] : "/";
            lock (_abfragen)
            {
                _abfragen.Add(pfad);
            }
            var (status, inhalt) = Inhalt(pfad);
            var kopf = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: application/octet-stream\r\nContent-Length: {inhalt.Length}\r\nConnection: close\r\n\r\n");
            await strom.WriteAsync(kopf, _ende.Token).ConfigureAwait(false);
            await strom.WriteAsync(inhalt, _ende.Token).ConfigureAwait(false);
            await strom.FlushAsync(_ende.Token).ConfigureAwait(false);
        }
        catch (Exception x) when (x is IOException or OperationCanceledException or ObjectDisposedException or SocketException)
        {
        }
    }

    private (string Status, byte[] Inhalt) Inhalt(string pfad)
    {
        if (pfad == "/releases")
        {
            var text = File.ReadAllText(Path.Combine(_ordner, "github_releases.json")).Replace("http://PAKETPROBE", Wurzel, StringComparison.Ordinal);
            return ("200 OK", Encoding.UTF8.GetBytes(text));
        }
        var name = Path.GetFileName(pfad);
        var datei = Path.Combine(_ordner, name);
        return name.EndsWith(".fipaket", StringComparison.Ordinal) && File.Exists(datei) ? ("200 OK", File.ReadAllBytes(datei)) : ("404 Not Found", []);
    }

    private int _weg;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _weg, 1) == 1)
        {
            return;
        }
        _ende.Cancel();
        _hoerer.Stop();
        _ende.Dispose();
    }
}
