using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MpegProcessingWindow {
    internal abstract class MPEGFrame {
        public const int MACROBLOCK_SIZE = 16;
        public const int SEARCH_RADIUS = 15;
        public const int BLOCK_SIZE = 8;
        public const byte MRLE_KEY = 0; // this should be global, or somewhere else entirely
        public MPEGFrame? PrevFrame { get; protected set; }
        public ImageMatrix ThisFrame { get; protected set; }
        public MPEGFrame? NextFrame { get; protected set; }
        protected int LvLength {
            get {
                return (int)Math.Ceiling(ThisFrame.YvLength / (double)MACROBLOCK_SIZE);
            }
        }

        protected int LhLength {
            get {
                return (int)Math.Ceiling(ThisFrame.YhLength / (double)MACROBLOCK_SIZE);
            }
        }

        protected int CvLength {
            get {
                return (int)Math.Ceiling(ThisFrame.CbvLength / (double)(MACROBLOCK_SIZE / 2));
            }
        }

        protected int ChLength {
            get {
                return (int)Math.Ceiling(ThisFrame.CbhLength / (double)(MACROBLOCK_SIZE / 2));
            }
        }

        public static readonly int[,] Q_LUMINOSITY =
        {
            {16, 11, 10, 16, 24, 40, 51, 61},
            {12, 12, 14, 19, 26, 58, 60, 55},
            {14, 13, 16, 24, 40, 57, 69, 56},
            {14, 17, 22, 29, 51, 87, 80, 62},
            {18, 22, 37, 56, 68, 109, 103, 77},
            {24, 35, 55, 64, 81, 104, 113, 92},
            {49, 64, 78, 87, 103, 121, 120, 101},
            {72, 92, 95, 98, 112, 100, 103, 99}
        };

        public static readonly int[,] Q_CHROMINANCE =
        {
            {17, 18, 24, 47, 99, 99, 99, 99},
            {18, 21, 26, 66, 99, 99, 99, 99},
            {24, 26, 56, 99, 99, 99, 99, 99},
            {47, 66, 99, 99, 99, 99, 99, 99},
            {99, 99, 99, 99, 99, 99, 99, 99},
            {99, 99, 99, 99, 99, 99, 99, 99},
            {99, 99, 99, 99, 99, 99, 99, 99},
            {99, 99, 99, 99, 99, 99, 99, 99}
        };

        public struct IntVector2 {
            public int X = 0;
            public int Y = 0;
        }

        public MPEGFrame() { 
            //idk lmao this is really shit design but it's fine ig
        }

        public MPEGFrame(ImageMatrix thisFrame, MPEGFrame? prevFrame = null, MPEGFrame? nextFrame = null) {
            ThisFrame = thisFrame;
            PrevFrame = prevFrame;
            NextFrame = nextFrame;
        }

        public abstract BitmapSource GetBitmap();
        public abstract ImageMatrix GetMatrix();
        public abstract byte[] GetCompressedBytes(byte[] head, int frame);

        public byte[,][,] GetYMacroBlocks() {
            byte[,] YMatrix = ThisFrame.GetSubsampledImage().Item1;

            byte[,][,] YMacroBlocks = new byte[LhLength, LvLength][,];

            for (int x = 0; x < LhLength; x++) {
                for (int y = 0; y < LvLength; y++) {
                    byte[,] macroblock = new byte[MACROBLOCK_SIZE, MACROBLOCK_SIZE];
                    for (int u = 0; u < MACROBLOCK_SIZE; u++) {
                        for (int v = 0; v < MACROBLOCK_SIZE; v++) {
                            if (x * MACROBLOCK_SIZE + u >= YMatrix.GetLength(0) || y * MACROBLOCK_SIZE + v >= YMatrix.GetLength(1)) continue;
                            macroblock[u, v] = YMatrix[x * MACROBLOCK_SIZE + u, y * MACROBLOCK_SIZE + v];
                        }
                    }
                    YMacroBlocks[x, y] = macroblock;
                }
            }

            return YMacroBlocks;
        }

        public byte[,][,] GetCbMacroBlocks() {
            byte[,] CbMatrix = ThisFrame.GetSubsampledImage().Item2;

            byte[,][,] CbMacroBlocks = new byte[ChLength, CvLength][,];
            int cMacroSize = MACROBLOCK_SIZE / 2;

            for (int x = 0; x < ChLength; x++) {
                for (int y = 0; y < CvLength; y++) {
                    byte[,] macroblock = new byte[cMacroSize, cMacroSize];
                    for (int u = 0; u < cMacroSize; u++) {
                        for (int v = 0; v < cMacroSize; v++) {
                            if (x * cMacroSize + u >= CbMatrix.GetLength(0) || y * cMacroSize + v >= CbMatrix.GetLength(1)) continue;
                            macroblock[u, v] = CbMatrix[x * cMacroSize + u, y * cMacroSize + v];
                        }
                    }
                    CbMacroBlocks[x, y] = macroblock;
                }
            }

            return CbMacroBlocks;
        }

        public byte[,][,] GetCrMacroBlocks() {
            byte[,] CrMatrix = ThisFrame.GetSubsampledImage().Item3;

            byte[,][,] CrMacroBlocks = new byte[ChLength, CvLength][,];
            int cMacroSize = MACROBLOCK_SIZE / 2;

            for (int x = 0; x < ChLength; x++) {
                for (int y = 0; y < CvLength; y++) {
                    byte[,] macroblock = new byte[cMacroSize, cMacroSize];
                    for (int u = 0; u < cMacroSize; u++) {
                        for (int v = 0; v < cMacroSize; v++) {
                            if (x * cMacroSize + u >= CrMatrix.GetLength(0) || y * cMacroSize + v >= CrMatrix.GetLength(1)) continue;
                            macroblock[u, v] = CrMatrix[x * cMacroSize + u, y * cMacroSize + v];
                        }
                    }
                    CrMacroBlocks[x, y] = macroblock;
                }
            }

            return CrMacroBlocks;
        }

        public static byte[,][,] PackMacroblocks(byte[,] matrix, int macroblockSize) {
            int xLength = (int) Math.Ceiling(matrix.GetLength(0) / (double) macroblockSize);
            int yLength = (int)Math.Ceiling(matrix.GetLength(1) / (double) macroblockSize);
            byte[,][,] macroblocks = new byte[xLength, yLength][,];

            for (int x = 0; x < xLength; x++) {
                for (int y = 0; y < yLength; y++) {
                    byte[,] macroblock = new byte[macroblockSize, macroblockSize];
                    for (int u = 0; u < macroblockSize; u++) {
                        for (int v = 0; v < macroblockSize; v++) {
                            if (x * macroblockSize + u >= matrix.GetLength(0) || y * macroblockSize + v >= matrix.GetLength(1)) continue;
                            macroblock[u, v] = matrix[x * macroblockSize + u, y * macroblockSize + v];
                        }
                    }
                    macroblocks[x, y] = macroblock;
                }
            }

            return macroblocks;
        }

        public static float[,][,] PackMacroblocks(float[,] matrix, int macroblockSize) {
            int xLength = (int)Math.Ceiling(matrix.GetLength(0) / (double)macroblockSize);
            int yLength = (int)Math.Ceiling(matrix.GetLength(1) / (double)macroblockSize);
            float[,][,] macroblocks = new float[xLength, yLength][,];

            for (int x = 0; x < xLength; x++) {
                for (int y = 0; y < yLength; y++) {
                    float[,] macroblock = new float[macroblockSize, macroblockSize];
                    for (int u = 0; u < macroblockSize; u++) {
                        for (int v = 0; v < macroblockSize; v++) {
                            if (x * macroblockSize + u >= matrix.GetLength(0) || y * macroblockSize + v >= matrix.GetLength(1)) continue;
                            macroblock[u, v] = matrix[x * macroblockSize + u, y * macroblockSize + v];
                        }
                    }
                    macroblocks[x, y] = macroblock;
                }
            }

            return macroblocks;
        }

        public static byte[,] UnpackMacroblocks(byte[,][,] macroblocks) {
            int mblockSize = macroblocks[0, 0].GetLength(0);
            byte[,] matrix = new byte[macroblocks.GetLength(0) * macroblocks[0, 0].GetLength(0), macroblocks.GetLength(1) * macroblocks[0, 0].GetLength(1)];

            for (int x = 0; x < macroblocks.GetLength(0); x++) {
                for (int y = 0; y < macroblocks.GetLength(1); y++) {
                    for (int u = 0; u < macroblocks[0, 0].GetLength(0); u++) {
                        for (int v = 0; v < macroblocks[0, 0].GetLength(1); v++) {
                            matrix[x * mblockSize + u, y * mblockSize + v] = macroblocks[x, y][u, v];
                        }
                    }
                }
            }

            return matrix;
        }

        public static byte[,] RestoreBlockDiff(byte[,] a, float[,] b) {
            byte[,] res = new byte[a.GetLength(0), a.GetLength(1)];
            for (int x = 0; x < a.GetLength(0); x++) {
                for (int y = 0; y < a.GetLength(0); y++) { 
                    res[x, y] = (byte) Math.Clamp((a[x, y] + b[x, y]), byte.MinValue, byte.MaxValue);
                }
            }

            return res;
        }

        // extremely poor copied code, should be in a more generic class "Compression"
        // this entirely comes from ImageJPEG, which was designed poorly to start with.
        public float[,] CenterOnZero(byte[,] arr) {
            float[,] res = new float[arr.GetLength(0), arr.GetLength(1)];

            for (int x = 0; x < res.GetLength(0); x++) {
                for (int y = 0; y < res.GetLength(1); y++) {
                    res[x, y] = Math.Clamp(arr[x, y], byte.MinValue, byte.MaxValue) - 128;
                }
            }

            return res;
        }

        public byte[,] RestoreToBytes(float[,] arr) {
            byte[,] res = new byte[arr.GetLength(0), arr.GetLength(1)];

            for (int x = 0; x < res.GetLength(0); x++) {
                for (int y = 0; y < res.GetLength(1); y++) {
                    res[x, y] = (byte)Math.Clamp(arr[x, y], byte.MinValue, byte.MaxValue);
                }
            }

            return res;
        }

        public static float[,] ReNoodle(sbyte[] s) {
            return new float[8, 8]
            {
                {s[0], s[2], s[3], s[9], s[10], s[20], s[21], s[35]},
                {s[1], s[4], s[8], s[11], s[19], s[22], s[34], s[36]},
                {s[5], s[7], s[12], s[18], s[23], s[33], s[37], s[48]},
                {s[6], s[13], s[17], s[24], s[32], s[38], s[47], s[49]},
                {s[14], s[16], s[25], s[31], s[39], s[46], s[50], s[57]},
                {s[15], s[26], s[30], s[40], s[45], s[51], s[56], s[58]},
                {s[27], s[29], s[41], s[44], s[52], s[55], s[59], s[62]},
                {s[28], s[42], s[43], s[53], s[54], s[60], s[61], s[63]}
            };
        }

        public static byte[] DeNoodle(byte[,] s) {
            return new byte[64]
                {
                    s[0, 0], s[1, 0], s[0, 1], s[0, 2], s[1, 1], s[2, 0], s[3, 0], s[2, 1], // 0 - 7
                    s[1, 2], s[0, 3], s[0, 4], s[1, 3], s[2, 2], s[3, 1], s[4, 0], s[5, 0], // 8 - 15
                    s[4, 1], s[3, 2], s[2, 3], s[1, 4], s[0, 5], s[0, 6], s[1, 5], s[2, 4], // 16 - 23 
                    s[3, 3], s[4, 2], s[5, 1], s[6, 0], s[7, 0], s[6, 1], s[5, 2], s[4, 3], // 24 - 31
                    s[3, 4], s[2, 5], s[1, 6], s[0, 7], s[1, 7], s[2, 6], s[3, 5], s[4, 4], // 32 - 39
                    s[5, 3], s[6, 2], s[7, 1], s[7, 2], s[6, 3], s[5, 4], s[4, 5], s[3, 6], // 40 - 47
                    s[2, 7], s[3, 7], s[4, 6], s[5, 5], s[6, 4], s[7, 3], s[7, 4], s[6, 5], // 48 - 55
                    s[5, 6], s[4, 7], s[5, 7], s[6, 6], s[7, 5], s[7, 6], s[6, 7], s[7, 7]  // 56 - 63
                };
        }

        public static byte[,] TurnToBytes(float[,] arr) {
            byte[,] b = new byte[arr.GetLength(0), arr.GetLength(1)];
            for (int x = 0; x < b.GetLength(0); x++) {
                for (int y = 0; y < b.GetLength(1); y++) {
                    b[x, y] = (byte)arr[x, y];
                }
            }
            return b;
        }

        public static float[,] TurnToFloats(byte[,] arr) {
            float[,] b = new float[arr.GetLength(0), arr.GetLength(1)];
            for (int x = 0; x < b.GetLength(0); x++) {
                for (int y = 0; y < b.GetLength(1); y++) {
                    b[x, y] = arr[x, y];
                }
            }
            return b;
        }

        public static float[,] SignAndTurnToFloats(byte[,] arr) {
            float[,] b = new float[arr.GetLength(0), arr.GetLength(1)];
            for (int x = 0; x < b.GetLength(0); x++) {
                for (int y = 0; y < b.GetLength(1); y++) {
                    b[x, y] = arr[x, y];
                }
            }
            return b;
        }

        // end of extremely poor code
    }
}
