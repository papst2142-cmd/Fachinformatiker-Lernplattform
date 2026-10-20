namespace Lernplattform.Tests;

/// <summary>Leerer Wegwerf-Ordner je Test; wird am Ende samt Inhalt entfernt.</summary>
internal sealed class TempOrdner : IDisposable
{
    public string Pfad { get; } = Directory.CreateTempSubdirectory("lernplattform_test_").FullName;

    public void Dispose()
    {
        try
        {
            foreach (var eintrag in Directory.EnumerateFileSystemEntries(Pfad, "*", SearchOption.AllDirectories))
            {
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(eintrag, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
                else
                {
                    File.SetAttributes(eintrag, FileAttributes.Normal);
                }
            }
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(Pfad, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            Directory.Delete(Pfad, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
