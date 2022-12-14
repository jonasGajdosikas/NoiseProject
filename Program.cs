using System.Drawing;
using System.Runtime.InteropServices;

namespace NoiseProject
{
    internal class Program
    {
        static void Main()
        {
#pragma warning disable CA1416 // Validate platform compatibility
            
            Bitmap bitmap = new(1024, 1024);
            string seed = "seed";
            MapGen Generator = new(seed, new(4, persistance: .7f) { Scale = .1f}, t => t * t * t);
            for(int x = 0; x < bitmap.Width; x++)
            {
                for(int y = 0; y < bitmap.Height; y++)
                {
                    bitmap.SetPixel(x, y, Generator.GetColor(x / 128f, y / 128f));
                }
            }
            bitmap.Save($"image {seed}.png");
#pragma warning restore CA1416 // Validate platform compatibility
        }

    }
    enum TerrainTypes
    {
        Sea,
        Beach,
        Intermediate,
        Mountains
    }
    class MapGen
    {
        readonly FractalNoise Noise;
        readonly string Seed;
        readonly MapSettings heightSettings;
        readonly TerrainSettings[] terrainSettings;
        readonly Func<float, float> HeightRamp;
        readonly static int typeAmt = Enum.GetNames<TerrainTypes>().Length;
        public MapGen(string seed, MapSettings heightS, Func<float,float> heightRamp)
        {
            Seed = seed;
            Noise = new(Seed);
            heightSettings = heightS;
            HeightRamp = heightRamp;
            terrainSettings = new TerrainSettings[typeAmt];
            for (int i = 0; i < typeAmt; i++) terrainSettings[i] = new(t => t);
            SetTerrain(new TerrainSettings(t => MathF.Sqrt(t)) { deepColor = new Ucolor() { rgba = 0x291c12ff }, deepHeight = 0.5f, highColor = new Ucolor() { rgba = 0xeeeeeeff }, highHeight = 1.0f }, TerrainTypes.Mountains) ;
        }
        public void SetTerrain(TerrainSettings newSettings, TerrainTypes terrainType)
        {
            terrainSettings[(int)terrainType] = newSettings;
        }
        public Color GetColor(float x, float y)
        {
            float height = HeightRamp(Noise.FractalValue(x, y, heightSettings));
            return Grayscale(height);
            for (int i = 0; i < typeAmt; i++)
            {
                if (height >= terrainSettings[i].deepHeight && height <= terrainSettings[i].highHeight) return terrainSettings[i].GetColor(height);
            }
            return Color.AliceBlue;
        }
        public static Color Grayscale(float val)
        {
            int d = 128 + (int)(256 * val);
            if (d > 255) d = 255;
            if (d < 0) d = 0;
            return Color.FromArgb(d, d, d);
        }
    }
    public class TerrainSettings
    {
        public Ucolor deepColor;
        public Ucolor highColor;
        public float deepHeight;
        public float highHeight;
        public Func<float, float> ColorRamp;
        public Color GetColor(float h)
        {
            if (h < deepHeight) h = deepHeight;
            if (h > highHeight) h = highHeight;
            float t = ColorRamp((h - deepHeight) / (highHeight - deepHeight));
            return Ucolor.Lerp(deepColor, highColor, t).Color;
        }
        public TerrainSettings(Func<float, float> func)
        {
            deepColor = new Ucolor();
            highColor = new Ucolor();
            ColorRamp = func;
        }
    }
    public class MapSettings
    {
        public int Octaves;
        public float Persistance;
        public float Lacunarity;
        public float Scale = 1f;
        public float mult;
        public MapSettings(int octaves = 3, float persistance = .5f, float lacunarity = 2f)
        {
            Octaves = octaves; Persistance = persistance; Lacunarity = lacunarity;
            mult = (1 - persistance) / (1 - Pow(persistance, octaves));
        }
        static float Pow(float b, int e)
        {
            float res = 1f;
            while (e > 0)
            {
                if ((e & 1) == 1) res *= b;
                b *= b;
                e >>= 1;
            }
            return res;
        }
    }
    public class FractalNoise
    {
        public readonly string Seed;
        public FractalNoise(string seed)
        {
            Seed = seed;
        }
        static readonly v2f[] grad =
        {
            new(1,1), new(-1,1), new(1,-1), new(-1,-1), new(1,0), new(-1,0), new(0,1), new(0,-1)
        };
        public static v2f v2fromTheta(float theta) => new(MathF.Cos(theta), MathF.Sin(theta));
        public static float DirectionFromHash(byte hash) => MathF.Tau * hash / 255f;
        public static v2f Skew(v2f vec) => new(vec.X + 0.366f * vec.Sum, vec.Y + 0.366f * vec.Sum);
        public static v2f Unskew(v2f vec) => new(vec.X - 0.211325f * vec.Sum, vec.Y - 0.211325f * vec.Sum);
        public static float Fade(float t) => t * (-2f * t * t + 1.5f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Dot(v2f a, v2f b) => a.X * b.X + a.Y * b.Y;
        public static int Fastfloor(float t) => (t > 0) ? (int)t : (int)t - 1;
        public float Value(float x_in, float y_in, string type = "")
        {
            /* some code sourced from a paper by Stefan Gustavson
             * his paper halped me greatly to understand how simplex noise works
             */
            const float F2 = 0.366f;
            const float G2 = 0.211325f;
            v2f vec = new(x_in, y_in);
            int i = Fastfloor(vec.X + F2 * vec.Sum);
            int j = Fastfloor(vec.Y + F2 * vec.Sum);
            v2f V0 = Unskew(new(i , j));
            v2f[] vecs = new v2f[3];
            v2f[] grads = new v2f[3];

            vecs[0] = Add(vec, Inv(V0));
            grads[0] = grad[HashFunction.Hash(i, j, Seed + type) & 7];
            int i1, j1;
            if (vecs[0].X > vecs[0].Y) { i1 = 1; j1 = 0; }
            else { i1 = 0; j1 = 1; }
            vecs[1] = Add(vecs[0], new(G2 - i1, G2 - j1));
            grads[1] = grad[HashFunction.Hash(i + i1, j + j1, Seed + type) & 7];

            vecs[2] = Add(vecs[0], new(2f * G2 - 1));
            grads[2] = grad[HashFunction.Hash(i + 1, j + 1, Seed + type) & 7];

            float val = 0;
            float t;
            for (int k = 0; k < 3; k++)
            {
                t = .5f - Dot(vecs[k], vecs[k]);
                if (t < 0) continue;
                t *= t;
                val += t * t * Dot(vecs[k], grads[k]);
            }
            return val * 70f;
        }
        public float FractalValue(float x, float y, MapSettings settings, string type = "")
        {
            float val = 0;
            float freq = settings.Scale;
            float amplitude = 1;
            for(int i = 0; i < settings.Octaves; i++)
            {
                val += Value(x * freq, y * freq, type) * amplitude;
                freq *= settings.Lacunarity;
                amplitude *= settings.Persistance;
            }
            return val * settings.mult;
        }
        static v2f Add(v2f a, v2f b) => new(a.X+b.X,a.Y+b.Y);
        static v2f Inv(v2f a) => new(-a.X, -a.Y);
    }
    [StructLayout(LayoutKind.Explicit)]
    public class Ucolor
    {
        [FieldOffset(0)]
        public byte r;
        [FieldOffset(1)]
        public byte g;
        [FieldOffset(2)]
        public byte b;
        [FieldOffset(3)]
        public byte a;

        [FieldOffset(0)]
        public uint rgba;
        public byte hash
        {
            get
            {
                return ((byte)(r ^ g ^ b ^ a));
            }
        }
        public Ucolor()
        {
            r = 0; g = 0; b = 0; a = 0; rgba = 0;
        }
        public Color Color => Color.FromArgb(a, r, g, b);
        public static Ucolor Lerp(Ucolor a, Ucolor b, float t)
        {
            return new Ucolor()
            {
                r = (byte)(a.r + (byte)((b.r - a.r) * t)),
                g = (byte)(a.g + (byte)((b.g - a.g) * t)),
                b = (byte)(a.b + (byte)((b.b - a.b) * t)),
                a = (byte)(a.a + (byte)((b.a - a.a) * t))
            };
        }
        public static Ucolor FromColor(Color color)
        {
            return new()
            {
                r = color.R,
                g = color.G,
                b = color.B,
                a = color.A
            };
        }
    }
    public class HashFunction
    {
        public static uint Hash(int x, int y, string seed)
        {
            string str = "x:" + x.ToString() + ", y:" + y.ToString();
            return HashString(str, seed);
        }
        public static byte Hash8(int x, int y, string seed)
        {
            Ucolor temp = new() { rgba = Hash(x, y, seed) };
            return temp.hash;
        }
        public static uint HashString(string text, string salt = "")
        {
            if (string.IsNullOrEmpty(text)) return 0;
            byte[] textbytes = System.Text.Encoding.UTF8.GetBytes(text + salt);
            /* implementation of SuperFastHash by Davy Landman
             * sourced from http://landman-code.blogspot.com/2009/02/c-superfasthash-and-murmurhash2.html
             * 
             */
            int len = textbytes.Length;
            uint hash = (uint)len;
            int remB = len & 3;
            int numL = len >> 2;
            int curI = 0;
            while (numL > 0)
            {
                hash += (ushort)(textbytes[curI++] | textbytes[curI++] << 8);
                uint tmp = (uint)((uint)(textbytes[curI++] | textbytes[curI++] << 8) << 11) ^ hash;
                hash = (hash << 16) ^ tmp;
                hash += hash >> 11;
                numL--;
            }
            switch (remB)
            {
                case 3:
                    hash += (ushort)(textbytes[curI++] | textbytes[curI++] << 8);
                    hash ^= hash << 16;
                    hash ^= ((uint)textbytes[curI]) << 18;
                    hash += hash << 11;
                    break;
                case 2:
                    hash += (ushort)(textbytes[curI++] | textbytes[curI] << 8);
                    hash ^= hash << 11;
                    hash += hash >> 17;
                    break;
                case 1: 
                    hash += textbytes[curI];
                    hash ^= hash << 10;
                    hash += hash >> 1;
                    break;
                default:
                    break;
            }
            hash ^= hash << 3;
            hash += hash >> 5;
            hash ^= hash << 4;
            hash += hash >> 17;
            hash ^= hash << 25;
            hash += hash >> 6;

            return hash;
        }
    }
#pragma warning disable IDE1006 // Naming Styles
    public struct v2f
#pragma warning restore IDE1006 // Naming Styles
    {
        public v2f(float x, float y)
        {
            X = x;
            Y = y;
        }
        public v2f (float val)
        {
            X = Y = val;
        }
        public float X;
        public float Y;
        public float Sum => X + Y;
    }
}