using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MpegProcessingWindow {
    public static class DCT {
        public static float Forwards(int u, int v, in int[,] h, int N, int M) {
            float result = 0;

            for (int x = 0; x < N; x++) {
                for (int y = 0; y < M; y++) {
                    float inner = MathF.Cos((u * MathF.PI * (2 * x + 1)) / (2 * N));
                    inner *= MathF.Cos((v * MathF.PI * (2 * y + 1)) / (2 * M));
                    inner *= h[x, y];
                    result += inner;
                }
            }

            return result *= C(u) * C(v) * (2 / MathF.Sqrt(M * N));
        }

        public static float Backwards(int x, int y, in int[,] H, int N, int M) {
            float result = 0;
            for (int u = 0; u < N; u++) { 
                for (int v = 0; v < M; v++) {
                    float inner = C(u) * C(v);
                    inner *= MathF.Cos((u * MathF.PI * (2 * x + 1)) / (2 * N));
                    inner *= MathF.Cos((v * MathF.PI * (2 * y + 1)) / (2 * M));
                    inner *= H[u, v];
                    result += inner;
                }
            }

            return result *= 2 / MathF.Sqrt(M * N);
        }

        private static float C(int u) {
            return u == 0 ? 1 / MathF.Sqrt(2) : 1;
        }
    }
}
