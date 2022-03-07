using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MpegProcessingWindow {
    internal class PFrame : MPEGFrame {
        private ImageMatrix targetFrame;
        private ImageMatrix calculatedFrame;
        private IntVector2[,] yVectors;
        private IntVector2[,] cbVectors;
        private IntVector2[,] crVectors;

        public PFrame(ImageMatrix thisFrame, MPEGFrame prevFrame, MPEGFrame? nextFrame = null) { 
            this.PrevFrame = prevFrame;
            this.NextFrame = nextFrame;
            this.targetFrame = thisFrame;
            this.ThisFrame = thisFrame;
            var vectors = CalculateVectors();
            yVectors = vectors.Item1;
            cbVectors = vectors.Item2;
            crVectors = vectors.Item3;
            calculatedFrame = CalculateNewFrame();
        }

        private (IntVector2[,], IntVector2[,], IntVector2[,]) CalculateVectors() {
            if (PrevFrame == null) {
                throw new InvalidOperationException("No previous frame, cannot create a PFrame.");
            }

            byte[,][,] tYMacros = GetYMacroBlocks();
            byte[,][,] tCbMacros = GetCbMacroBlocks();
            byte[,][,] tCrMacros = GetCrMacroBlocks();

            byte[,][,] pYMacros = PrevFrame.GetYMacroBlocks();
            byte[,][,] pCbMacros = PrevFrame.GetCbMacroBlocks();
            byte[,][,] pCrMacros = PrevFrame.GetCrMacroBlocks();

            IntVector2[,] yVectors = CreateVectors(pYMacros, tYMacros);
            IntVector2[,] cbVectors = CreateVectors(pCbMacros, tCbMacros);
            IntVector2[,] crVectors = CreateVectors(pCrMacros, tCrMacros);

            return (yVectors, cbVectors, crVectors);
        }

        private IntVector2[,] CreateVectors(byte[,][,] prev, byte[,][,] curr) {
            IntVector2[,] vectors = new IntVector2[prev.GetLength(0), prev.GetLength(1)];

            for (int x = 0; x < vectors.GetLength(0); x++) {
                for (int y = 0; y < vectors.GetLength(1); y++) {
                    vectors[x, y] = FindClosestMatch(prev, curr, x, y, SEARCH_RADIUS);
                }
            }

            return vectors;
        }

        private IntVector2 FindClosestMatch(byte[,][,] prev, byte[,][,] curr, int x, int y, int r) {
            IntVector2 offset = new();
            int bestMAD = MAD(prev[x, y], curr[x, y]);

            if (bestMAD == 0) return offset;

            for (int u = -r; u < r; u++) {
                for (int v = -r; v < r; v++) {
                    if (x + u < 0 || x + u >= prev.GetLength(0) || y + v < 0 || y + v >= prev.GetLength(1)) continue;
                    if (MAD(prev[x + u, y + v], curr[x, y]) < bestMAD) {
                        bestMAD = MAD(prev[x + u, y + v], curr[x, y]);
                        offset = new IntVector2() { X = u, Y = v };
                    }
                }
            }

            return offset;
        }

        private int MAD(byte[,] prev, byte[,] curr) {
            int result = 0;

            for (int x = 0; x < prev.GetLength(0); x++) {
                for (int y = 0; y < prev.GetLength(1); y++) {
                    result += Math.Abs(prev[x, y] - curr[x, y]);
                }
            }

            return result;
        }

        private ImageMatrix CalculateNewFrame() {
            if (PrevFrame == null) throw new InvalidOperationException();

            byte[,][,] yM = PrevFrame.GetYMacroBlocks();
            byte[,][,] cbM = PrevFrame.GetCbMacroBlocks();
            byte[,][,] crM = PrevFrame.GetCrMacroBlocks();

            byte[,][,] newY = new byte[yM.GetLength(0), yM.GetLength(1)][,];
            byte[,][,] newCb = new byte[cbM.GetLength(0), cbM.GetLength(1)][,];
            byte[,][,] newCr = new byte[crM.GetLength(0), crM.GetLength(1)][,];

            for (int x = 0; x < yM.GetLength(0); x++) {
                for (int y = 0; y < yM.GetLength(1); y++) {
                    IntVector2 offset = yVectors[x, y];
                    newY[x, y] = yM[x + offset.X, y + offset.Y];
                }
            }

            for (int x = 0; x < cbM.GetLength(0); x++) {
                for (int y = 0; y < cbM.GetLength(1); y++) {
                    IntVector2 offset = cbVectors[x, y];
                    newCb[x, y] = cbM[x + offset.X, y + offset.Y];
                }
            }

            for (int x = 0; x < crM.GetLength(0); x++) {
                for (int y = 0; y < crM.GetLength(1); y++) {
                    IntVector2 offset = crVectors[x, y];
                    newCr[x, y] = crM[x + offset.X, y + offset.Y];
                }
            }

            ImageMatrix prev = PrevFrame.GetMatrix();

            byte[,] compY = new byte[prev.YhLength, prev.YvLength];
            byte[,] compCb = new byte[prev.CbhLength, prev.CbvLength];
            byte[,] compCr = new byte[prev.CrhLength, prev.CrvLength];

            int lMacroSize = newY[0, 0].GetLength(0);
            int cMacroSize = newCb[0, 0].GetLength(0);

            for (int x = 0; x < compY.GetLength(0); x++) {
                for (int y = 0; y < compY.GetLength(1); y++) {
                    compY[x, y] = newY[x / lMacroSize, y / lMacroSize][x % lMacroSize, y % lMacroSize];
                }
            }

            for (int x = 0; x < compCb.GetLength(0); x++) {
                for (int y = 0; y < compCb.GetLength(1); y++) {
                    compCb[x, y] = newCb[x / cMacroSize, y / cMacroSize][x % cMacroSize, y % cMacroSize];
                }
            }

            for (int x = 0; x < compCr.GetLength(0); x++) {
                for (int y = 0; y < compCr.GetLength(1); y++) {
                    compCr[x, y] = newCr[x / cMacroSize, y / cMacroSize][x % cMacroSize, y % cMacroSize];
                }
            }

            return new ImageMatrix(compY, compCb, compCr);
        }

        public Line[] GetLines() {
            Line[] lines = new Line[yVectors.GetLength(0) * yVectors.GetLength(1)];

            for (int x = 0; x < yVectors.GetLength(0); x++) {
                for (int y = 0; y < yVectors.GetLength(1); y++) {
                    Line l = new Line() {
                        X1 = x * MACROBLOCK_SIZE,
                        Y1 = y * MACROBLOCK_SIZE,
                        X2 = x * MACROBLOCK_SIZE + yVectors[x, y].X * MACROBLOCK_SIZE + 1,
                        Y2 = y * MACROBLOCK_SIZE + yVectors[x, y].Y * MACROBLOCK_SIZE + 1,
                        StrokeThickness = 1,
                        Stroke = Brushes.GreenYellow
                    };
                    lines[y * yVectors.GetLength(0) + x] = l;
                }
            }

            return lines;
        }

        public override ImageMatrix GetMatrix() { 
            return targetFrame;
        }

        public override BitmapSource GetBitmap() {
            ImageJPEG j = new(calculatedFrame);
            return j.GetBitmap();
        }
    }
}
