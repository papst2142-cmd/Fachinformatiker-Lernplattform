using Lernplattform.Core.Update;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>Patch A 0.37.0 (Sammelliste 203): Erkennung eines Mac-Bundles, das der Benutzer nicht aendern darf.</summary>
public sealed class MacBundleTests
{
    [Fact]
    public void BundleAusDemProgrammordner()
    {
        Assert.Equal(Path.GetFullPath("/Applications/Fachinformatiker Lernplattform.app"),
            MacBundle.Von("/Applications/Fachinformatiker Lernplattform.app/Contents/MacOS"));
        Assert.Null(MacBundle.Von("/opt/lernplattform/bin"));
        Assert.Null(MacBundle.Von("/Applications/Ordner/Contents/MacOS"));
        Assert.Null(MacBundle.Von("/x.app/Inhalt/MacOS"));
    }

    [Fact]
    public void BeschreibbarNurWennAlleDreiOrdnerBeschreibbarSind()
    {
        using var temp = new TempOrdner();
        var bundle = Path.Combine(temp.Pfad, "Test.app");
        var macos = Directory.CreateDirectory(Path.Combine(bundle, "Contents", "MacOS")).FullName;
        Assert.True(MacBundle.Beschreibbar(bundle));
        Assert.False(File.Exists(Path.Combine(bundle, MacBundle.Probedatei)));
        Assert.False(File.Exists(Path.Combine(macos, MacBundle.Probedatei)));
        // Gegenprobe auf allen Systemen: Ordner fehlt
        Assert.False(MacBundle.Beschreibbar(Path.Combine(temp.Pfad, "fehlt.app")));
        // Gegenprobe wie eine Installation "fuer alle Benutzer" (Dateien gehoeren root): Contents/MacOS ohne Schreibrecht;
        // Unix-Rechte gibt es unter Windows nicht (das Programm prueft nur am Mac)
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(macos, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            try
            {
                Assert.False(MacBundle.Beschreibbar(bundle));
            }
            finally
            {
                File.SetUnixFileMode(macos, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
    }
}
