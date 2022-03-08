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
        private ImageMatrix diffFrame;
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
            diffFrame = CalculateDiffFrame();
        }

        private (IntVector2[,], IntVector2[,], IntVector2[,]) CalculateVectors() {
            if (PrevFrame == null) {
                throw new InvalidOperationException("No previous frame, cannot create a PFrame.");
            }

            byte[,][,] tYMacros = GetYMacroBlocks();
            byte[,][,] tCbMacros = GetCbMacroBlocks();
            byte[,][,] tCrMacros = GetCrMacroBlocks();

            var matrices = PrevFrame.GetMatrix().GetSubsampledImage();
            byte[,] pYMatrix = matrices.Item1;
            byte[,] pCbMatrix = matrices.Item2;
            byte[,] pCrMatrix = matrices.Item3;

            IntVector2[,] yVectors = CreateVectors(pYMatrix, tYMacros, MACROBLOCK_SIZE);
            IntVector2[,] cbVectors = CreateVectors(pCbMatrix, tCbMacros, MACROBLOCK_SIZE / 2);
            IntVector2[,] crVectors = CreateVectors(pCrMatrix, tCrMacros, MACROBLOCK_SIZE / 2);

            return (yVectors, cbVectors, crVectors);
        }

        private IntVector2[,] CreateVectors(byte[,] prev, byte[,][,] curr, int macroBlockSize) {
            IntVector2[,] vectors = new IntVector2[curr.GetLength(0), curr.GetLength(1)];

            for (int x = 0; x < vectors.GetLength(0); x++) {
                for (int y = 0; y < vectors.GetLength(1); y++) {
                    vectors[x, y] = FindClosestMatch(prev, curr, x, y, SEARCH_RADIUS, macroBlockSize);
                }
            }

            return vectors;
        }

        private IntVector2 FindClosestMatch(byte[,] prev, byte[,][,] curr, int x, int y, int r, int macroBlockSize) {
            IntVector2 offset = new();
            int bestMAD = MAD(CreateSingleMacro(prev, x * macroBlockSize, y * macroBlockSize, macroBlockSize), curr[x, y]);

            if (bestMAD == 0) return offset;

            for (int u = -r; u < r; u++) {
                for (int v = -r; v < r; v++) {
                    if (x + u < 0 || x + u >= prev.GetLength(0) || y + v < 0 || y + v >= prev.GetLength(1)) continue;
                    if (MAD(CreateSingleMacro(prev, x * macroBlockSize + u, y * macroBlockSize + v, macroBlockSize), curr[x, y]) < bestMAD) {
                        bestMAD = MAD(CreateSingleMacro(prev, x * macroBlockSize + u, y * macroBlockSize + v, macroBlockSize), curr[x, y]);
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

        private byte[,] CreateSingleMacro(byte[,] fullMatrix, int x, int y, int macroblockSize) {
            byte[,] macro = new byte[macroblockSize, macroblockSize];
            for (int u = 0; u < macroblockSize; u++) {
                for (int v = 0; v < macroblockSize; v++) {
                    if (x + u >= fullMatrix.GetLength(0) || y + v >= fullMatrix.GetLength(1)) continue;
                    macro[u, v] = fullMatrix[x + u, y + v];
                }
            }
            return macro;
        }

        private ImageMatrix CalculateNewFrame() {
            if (PrevFrame == null) throw new InvalidOperationException();

            var matrices = PrevFrame.GetMatrix().GetSubsampledImage();
            byte[,] yM = matrices.Item1;
            byte[,] cbM = matrices.Item2;
            byte[,] crM = matrices.Item3;

            byte[,][,] newY = new byte[yVectors.GetLength(0), yVectors.GetLength(1)][,];
            byte[,][,] newCb = new byte[cbVectors.GetLength(0), cbVectors.GetLength(1)][,];
            byte[,][,] newCr = new byte[crVectors.GetLength(0), crVectors.GetLength(1)][,];

            for (int x = 0; x < newY.GetLength(0); x++) {
                for (int y = 0; y < newY.GetLength(1); y++) {
                    IntVector2 offset = yVectors[x, y];
                    newY[x, y] = CreateSingleMacro(yM, x * MACROBLOCK_SIZE + offset.X, y * MACROBLOCK_SIZE + offset.Y, MACROBLOCK_SIZE);
                }
            }

            for (int x = 0; x < newCb.GetLength(0); x++) {
                for (int y = 0; y < newCb.GetLength(1); y++) {
                    IntVector2 offset = cbVectors[x, y];
                    newCb[x, y] = CreateSingleMacro(cbM, x * MACROBLOCK_SIZE / 2 + offset.X, y * MACROBLOCK_SIZE / 2 + offset.Y, MACROBLOCK_SIZE / 2);
                }
            }

            for (int x = 0; x < newCr.GetLength(0); x++) {
                for (int y = 0; y < newCr.GetLength(1); y++) {
                    IntVector2 offset = crVectors[x, y];
                    newCr[x, y] = CreateSingleMacro(crM, x * MACROBLOCK_SIZE / 2 + offset.X, y * MACROBLOCK_SIZE / 2 + offset.Y, MACROBLOCK_SIZE / 2);
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

        private ImageMatrix CalculateDiffFrame() {
            if (PrevFrame == null) throw new InvalidOperationException();

            var calcMatrices = calculatedFrame.GetSubsampledImage();
            var thisMatrices = ThisFrame.GetSubsampledImage();

            byte[,] yM = new byte[calcMatrices.Item1.GetLength(0), calcMatrices.Item1.GetLength(1)];
            byte[,] cbM = new byte[calcMatrices.Item2.GetLength(0), calcMatrices.Item2.GetLength(1)];
            byte[,] crM = new byte[calcMatrices.Item3.GetLength(0), calcMatrices.Item3.GetLength(1)];

            for (int x = 0; x < yM.GetLength(0); x++) {
                for (int y = 0; y < yM.GetLength(1); y++) {
                    yM[x, y] = (byte) (thisMatrices.Item1[x, y] - calcMatrices.Item1[x, y]);
                }
            }

            for (int x = 0; x < cbM.GetLength(0); x++) {
                for (int y = 0; y < cbM.GetLength(1); y++) {
                    cbM[x, y] = (byte)(thisMatrices.Item2[x, y] - calcMatrices.Item2[x, y]);
                }
            }

            for (int x = 0; x < crM.GetLength(0); x++) {
                for (int y = 0; y < crM.GetLength(1); y++) {
                    crM[x, y] = (byte)(thisMatrices.Item3[x, y] - calcMatrices.Item3[x, y]);
                }
            }

            return new ImageMatrix(yM, cbM, crM);
        }

        private ImageMatrix ConstructOriginalFrame() {
            var diffMatrices = diffFrame.GetSubsampledImage();
            var lastMatrices = PrevFrame.GetMatrix().GetSubsampledImage();

            byte[,] yM = new byte[diffMatrices.Item1.GetLength(0), diffMatrices.Item1.GetLength(1)];
            byte[,] cbM = new byte[diffMatrices.Item2.GetLength(0), diffMatrices.Item2.GetLength(1)];
            byte[,] crM = new byte[diffMatrices.Item3.GetLength(0), diffMatrices.Item3.GetLength(1)];

            for (int x = 0; x < yM.GetLength(0); x++) {
                for (int y = 0; y < yM.GetLength(1); y++) {
                    yM[x, y] = (byte)(lastMatrices.Item1[x, y] + diffMatrices.Item1[x, y]);
                }
            }

            for (int x = 0; x < cbM.GetLength(0); x++) {
                for (int y = 0; y < cbM.GetLength(1); y++) {
                    cbM[x, y] = (byte)(lastMatrices.Item2[x, y] + diffMatrices.Item2[x, y]);
                }
            }

            for (int x = 0; x < crM.GetLength(0); x++) {
                for (int y = 0; y < crM.GetLength(1); y++) {
                    crM[x, y] = (byte)(lastMatrices.Item3[x, y] + diffMatrices.Item3[x, y]);
                }
            }

            return new ImageMatrix(yM, cbM, crM);
        }
        
        public Line[] GetLines() {
            Line[] lines = new Line[yVectors.GetLength(0) * yVectors.GetLength(1) + cbVectors.GetLength(0) * cbVectors.GetLength(1) + crVectors.GetLength(0) * crVectors.GetLength(1)];

            for (int x = 0; x < yVectors.GetLength(0); x++) {
                for (int y = 0; y < yVectors.GetLength(1); y++) {
                    Line l = new() {
                        X1 = x * MACROBLOCK_SIZE,
                        Y1 = y * MACROBLOCK_SIZE,
                        X2 = x * MACROBLOCK_SIZE + yVectors[x, y].X + 1,
                        Y2 = y * MACROBLOCK_SIZE + yVectors[x, y].Y + 1,
                        StrokeThickness = 1,
                        Stroke = Brushes.GreenYellow
                    };
                    lines[y * yVectors.GetLength(0) + x] = l;
                }
            }

            int cbOffset = yVectors.GetLength(0) * yVectors.GetLength(1);

            for (int x = 0; x < cbVectors.GetLength(0); x++) {
                for (int y = 0; y < cbVectors.GetLength(1); y++) {
                    Line l = new() {
                        X1 = x * MACROBLOCK_SIZE,
                        Y1 = y * MACROBLOCK_SIZE,
                        X2 = x * MACROBLOCK_SIZE + cbVectors[x, y].X + 1,
                        Y2 = y * MACROBLOCK_SIZE + cbVectors[x, y].Y + 1,
                        StrokeThickness = 1,
                        Stroke = Brushes.CornflowerBlue
                    };
                    lines[y * cbVectors.GetLength(0) + x + cbOffset] = l;
                }
            }

            int crOffset = cbOffset + cbVectors.GetLength(0) * cbVectors.GetLength(1);

            for (int x = 0; x < crVectors.GetLength(0); x++) {
                for (int y = 0; y < crVectors.GetLength(1); y++) {
                    Line l = new() {
                        X1 = x * MACROBLOCK_SIZE,
                        Y1 = y * MACROBLOCK_SIZE,
                        X2 = x * MACROBLOCK_SIZE + crVectors[x, y].X + 1,
                        Y2 = y * MACROBLOCK_SIZE + crVectors[x, y].Y + 1,
                        StrokeThickness = 1,
                        Stroke = Brushes.Red
                    };
                    lines[y * crVectors.GetLength(0) + x + crOffset] = l;
                }
            }

            return lines;
        }

        public override ImageMatrix GetMatrix() { 
            return ConstructOriginalFrame();
        }

        public override BitmapSource GetBitmap() {
            ImageJPEG j = new(ConstructOriginalFrame());
            return j.GetBitmap();
        }
    }
}
