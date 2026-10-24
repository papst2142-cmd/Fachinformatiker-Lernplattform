using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Kamera der 3D-Stichprobe: schaut schraeg von oben auf einen Zielpunkt am Boden (Y = 0).
/// Ziehen verschiebt das Ziel am Boden, Zoomen aendert den Abstand. Matrizen mit
/// System.Numerics (Zeilenvektoren); die Speicherfolge passt so ohne Umdrehen zu GLSL
/// (<c>uMvp * vec4(p, 1)</c>).
/// </summary>
public sealed class Kamera
{
    public const float AbstandMin = 5f;
    public const float AbstandMax = 40f;

    public Kamera(float zielX, float zielZ, float abstand)
    {
        ZielX = zielX;
        ZielZ = zielZ;
        Abstand = Math.Clamp(abstand, AbstandMin, AbstandMax);
    }

    public float ZielX { get; private set; }

    public float ZielZ { get; private set; }

    public float Abstand { get; private set; }

    /// <summary>Neigung ueber dem Boden in Grad.</summary>
    public float Neigung { get; init; } = 52f;

    /// <summary>Drehung um die Hochachse in Grad (0 = Blick nach -Z).</summary>
    public float Drehung { get; init; } = 0f;

    public float Sichtfeld { get; init; } = 40f;

    /// <summary>Grenzen fuer das Ziel (Welt), damit man die Stadt nicht verliert.</summary>
    public (float X1, float Z1, float X2, float Z2) Grenzen { get; init; } = (-100, -100, 100, 100);

    public Vector3 Ziel => new(ZielX, 0, ZielZ);

    public Vector3 Auge
    {
        get
        {
            var n = Neigung * MathF.PI / 180f;
            var d = Drehung * MathF.PI / 180f;
            var waagrecht = Abstand * MathF.Cos(n);
            return Ziel + new Vector3(MathF.Sin(d) * waagrecht, Abstand * MathF.Sin(n), MathF.Cos(d) * waagrecht);
        }
    }

    public Matrix4x4 Sicht => Matrix4x4.CreateLookAt(Auge, Ziel, Vector3.UnitY);

    public Matrix4x4 Projektion(float breite, float hoehe) =>
        Matrix4x4.CreatePerspectiveFieldOfView(Sichtfeld * MathF.PI / 180f, Math.Max(breite, 1) / Math.Max(hoehe, 1), 0.5f, 200f);

    public void Setzen(float zielX, float zielZ)
    {
        ZielX = Math.Clamp(zielX, Grenzen.X1, Grenzen.X2);
        ZielZ = Math.Clamp(zielZ, Grenzen.Z1, Grenzen.Z2);
    }

    /// <summary>
    /// Ziehen um <paramref name="dx"/>/<paramref name="dy"/> Bildpunkte bei Bildhoehe <paramref name="hoehe"/>:
    /// der Boden folgt dem Finger ungefaehr (Massstab aus Abstand und Sichtfeld).
    /// </summary>
    public void Verschieben(float dx, float dy, float hoehe)
    {
        var proPunkt = 2f * Abstand * MathF.Tan(Sichtfeld * MathF.PI / 360f) / Math.Max(hoehe, 1);
        var d = Drehung * MathF.PI / 180f;
        var rechts = new Vector2(MathF.Cos(d), -MathF.Sin(d));
        var vor = new Vector2(-MathF.Sin(d), -MathF.Cos(d));
        var n = Neigung * MathF.PI / 180f;
        var schritt = (-dx * proPunkt * rechts) + (dy * proPunkt / MathF.Max(MathF.Sin(n), 0.3f) * vor);
        Setzen(ZielX + schritt.X, ZielZ + schritt.Y);
    }

    /// <summary>Faktor &gt; 1 = naeher heran.</summary>
    public void Zoomen(float faktor)
    {
        if (faktor > 0)
        {
            Abstand = Math.Clamp(Abstand / faktor, AbstandMin, AbstandMax);
        }
    }

    /// <summary>Punkt am Boden (Y = 0) unter dem Bildpunkt (x, y) oder null (Blick in den Himmel).</summary>
    public Vector2? Boden(float x, float y, float breite, float hoehe)
    {
        if (!Matrix4x4.Invert(Sicht * Projektion(breite, hoehe), out var umgekehrt))
        {
            return null;
        }
        var nx = (2f * x / Math.Max(breite, 1)) - 1f;
        var ny = 1f - (2f * y / Math.Max(hoehe, 1));
        var nah = Vector4.Transform(new Vector4(nx, ny, 0f, 1f), umgekehrt);
        var fern = Vector4.Transform(new Vector4(nx, ny, 1f, 1f), umgekehrt);
        var a = new Vector3(nah.X, nah.Y, nah.Z) / nah.W;
        var b = new Vector3(fern.X, fern.Y, fern.Z) / fern.W;
        if (MathF.Abs(b.Y - a.Y) < 1e-6f)
        {
            return null;
        }
        var t = -a.Y / (b.Y - a.Y);
        if (t < 0)
        {
            return null;
        }
        var p = a + ((b - a) * t);
        return new Vector2(p.X, p.Z);
    }
}
