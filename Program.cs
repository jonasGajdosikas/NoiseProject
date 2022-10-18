using System.Drawing;

namespace NoiseProject
{
    internal class Program
    {
        static void Main()
        {
#pragma warning disable CA1416 // Validate platform compatibility
            Bitmap bitmap = new(1024, 1024);
            FractalNoise noise = new("seed");
            for(int x = 0; x < bitmap.Width; x++)
            {
                for(int y = 0; y < bitmap.Height; y++)
                {
                    bitmap.SetPixel(x, y, Grayscale(noise.FractalValue(x / 128f, y / 128f, 4, persistance: 0.6f)));
                }
            }
            bitmap.Save("image.png");
#pragma warning restore CA1416 // Validate platform compatibility
        }
        static Color Grayscale(float val)
        {
            int d = 128 + (int)(256 * val);
            if (d > 255) d = 255;
            if (d < 0) d = 0;
            return Color.FromArgb(d,d,d);
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
        public float FractalValue(float x, float y, int octaves, string type = "", float persistance = .5f, float lacunarity = 2f)
        {
            float val = 0;
            float freq = 1;
            float amplitude = 1;
            for(int i = 0; i < octaves; i++)
            {
                val += Value(x * freq, y * freq, type) * amplitude;
                freq *= lacunarity;
                amplitude *= persistance;
            }
            return val * (1 - persistance);
        }
        static v2f Add(v2f a, v2f b) => new(a.X+b.X,a.Y+b.Y);
        static v2f Inv(v2f a) => new(-a.X, -a.Y);
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
            byte[] temp = BitConverter.GetBytes(Hash(x, y, seed));
            byte res = 0;
            for (int i = 0; i < 4; i++) res ^= temp[i];
            return res;
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