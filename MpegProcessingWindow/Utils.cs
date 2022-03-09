using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MpegProcessingWindow {
    internal class Utils {
        public static int[,] CreateUniformFromSingle(in int[] input, int width, int height) { 
            int[,] res = new int[width, height];

            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    res[x, y] = input[(y * width) + x];
                }
            }

            return res;
        }

        public static byte[] IntToByteBE(int n) {
            byte[] b = new byte[sizeof(int)];
            for (int i = 0; i < b.Length; i++) {
                b[i] = (byte) ((n >> (i * 8)) & 255);
            }
            return b;
        }
    }
}
