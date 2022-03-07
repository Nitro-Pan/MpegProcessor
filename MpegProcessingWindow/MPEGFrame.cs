using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MpegProcessingWindow {
    internal abstract class MPEGFrame {
        public const int MACROBLOCK_SIZE = 16;
        public const int SEARCH_RADIUS = 8;
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

        public struct IntVector2 {
            public int X = 0;
            public int Y = 0;
        }

        public abstract BitmapSource GetBitmap();
        public abstract ImageMatrix GetMatrix();

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
    }
}
