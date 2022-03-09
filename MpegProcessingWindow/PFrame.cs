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
        private DiffFrame diffFrame;
        private IntVector2[,] yVectors;
        private IntVector2[,] cbVectors;
        private IntVector2[,] crVectors;

        private float[,][,] yQuan;
        private float[,][,] cbQuan;
        private float[,][,] crQuan;

        private struct DiffFrame {
            public float[,] yDiff;
            public float[,] cbDiff;
            public float[,] crDiff;
        }

        public PFrame(ImageMatrix thisFrame, MPEGFrame prevFrame, MPEGFrame? nextFrame = null) : base(thisFrame,
                                                                                                      prevFrame, 
                                                                                                      nextFrame) { 
            this.targetFrame = thisFrame;
            var vectors = CalculateVectors();
            yVectors = vectors.Item1;
            cbVectors = vectors.Item2;
            crVectors = vectors.Item3;
            calculatedFrame = CalculateNewFrame();
            diffFrame = CalculateDiffFrame();
        }

        public PFrame(byte[] b, int width, int height, MPEGFrame prevFrame) {
            PrevFrame = prevFrame;

            var data = ReadByteStream(b, width, height);
            yQuan = data.Item1;
            cbQuan = data.Item2;
            crQuan = data.Item3;
            yVectors = data.Item4;
            cbVectors = data.Item5;
            crVectors = data.Item6;

            diffFrame = Decompress(width, height);

            ThisFrame = ConstructOriginalFrame();
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

        private DiffFrame CalculateDiffFrame() {
            if (PrevFrame == null) throw new InvalidOperationException();

            var calcMatrices = calculatedFrame.GetSubsampledImage();
            var thisMatrices = ThisFrame.GetSubsampledImage();

            float[,] yM = new float[calcMatrices.Item1.GetLength(0), calcMatrices.Item1.GetLength(1)];
            float[,] cbM = new float[calcMatrices.Item2.GetLength(0), calcMatrices.Item2.GetLength(1)];
            float[,] crM = new float[calcMatrices.Item3.GetLength(0), calcMatrices.Item3.GetLength(1)];

            for (int x = 0; x < yM.GetLength(0); x++) {
                for (int y = 0; y < yM.GetLength(1); y++) {
                    yM[x, y] = (thisMatrices.Item1[x, y] - calcMatrices.Item1[x, y]);
                }
            }

            for (int x = 0; x < cbM.GetLength(0); x++) {
                for (int y = 0; y < cbM.GetLength(1); y++) {
                    cbM[x, y] = (thisMatrices.Item2[x, y] - calcMatrices.Item2[x, y]);
                }
            }

            for (int x = 0; x < crM.GetLength(0); x++) {
                for (int y = 0; y < crM.GetLength(1); y++) {
                    crM[x, y] = (thisMatrices.Item3[x, y] - calcMatrices.Item3[x, y]);
                }
            }

            return new DiffFrame() {
                yDiff = yM,
                cbDiff = cbM,
                crDiff = crM
            };
        }

        private ImageMatrix ConstructOriginalFrame() {
            var diffMatrices = diffFrame;
            var lastMatrices = PrevFrame!.GetMatrix().GetSubsampledImage();

            float[,][,] yDiffMacros = PackMacroblocks(diffMatrices.yDiff, MACROBLOCK_SIZE);
            float[,][,] cbDiffMacros = PackMacroblocks(diffMatrices.cbDiff, MACROBLOCK_SIZE / 2);
            float[,][,] crDiffMacros = PackMacroblocks(diffMatrices.crDiff, MACROBLOCK_SIZE / 2);

            byte[,][,] yResMacros = new byte[yDiffMacros.GetLength(0), yDiffMacros.GetLength(1)][,];
            byte[,][,] cbResMacros = new byte[cbDiffMacros.GetLength(0), cbDiffMacros.GetLength(1)][,];
            byte[,][,] crResMacros = new byte[crDiffMacros.GetLength(0), crDiffMacros.GetLength(1)][,];

            for (int x = 0; x < yDiffMacros.GetLength(0); x++) {
                for (int y = 0; y < yDiffMacros.GetLength(1); y++) {
                    byte[,] lastAdjMacro = CreateSingleMacro(lastMatrices.Item1, x * MACROBLOCK_SIZE + yVectors[x, y].X, 
                                                             y * MACROBLOCK_SIZE + yVectors[x, y].Y, MACROBLOCK_SIZE);
                    yResMacros[x, y] = RestoreBlockDiff(lastAdjMacro, yDiffMacros[x, y]);
                }
            }

            for (int x = 0; x < cbDiffMacros.GetLength(0); x++) {
                for (int y = 0; y < cbDiffMacros.GetLength(1); y++) {
                    byte[,] lastAdjMacro = CreateSingleMacro(lastMatrices.Item2, x * (MACROBLOCK_SIZE / 2) + cbVectors[x, y].X, 
                                                             y * (MACROBLOCK_SIZE / 2) + cbVectors[x, y].Y, MACROBLOCK_SIZE / 2);
                    cbResMacros[x, y] = RestoreBlockDiff(lastAdjMacro, cbDiffMacros[x, y]);
                }
            }

            for (int x = 0; x < crDiffMacros.GetLength(0); x++) {
                for (int y = 0; y < crDiffMacros.GetLength(1); y++) {
                    byte[,] lastAdjMacro = CreateSingleMacro(lastMatrices.Item3, x * (MACROBLOCK_SIZE / 2) + crVectors[x, y].X, 
                                                             y * (MACROBLOCK_SIZE / 2) + crVectors[x, y].Y, MACROBLOCK_SIZE / 2);
                    crResMacros[x, y] = RestoreBlockDiff(lastAdjMacro, crDiffMacros[x, y]);
                }
            }

            byte[,] yM = UnpackMacroblocks(yResMacros);
            byte[,] cbM = UnpackMacroblocks(cbResMacros);
            byte[,] crM = UnpackMacroblocks(crResMacros);

            return new ImageMatrix(yM, cbM, crM);
        }

        public void Compress() {
            var diffMatrices = diffFrame;

            float[,][,] yMacros = PackMacroblocks(diffFrame.yDiff, BLOCK_SIZE);
            float[,][,] cbMacros = PackMacroblocks(diffFrame.cbDiff, BLOCK_SIZE);
            float[,][,] crMacros = PackMacroblocks(diffFrame.crDiff, BLOCK_SIZE);

            float[,][,] yDCT = new float[yMacros.GetLength(0), yMacros.GetLength(1)][,];
            for (int x = 0; x < yMacros.GetLength(0); x++) {
                for (int y = 0; y < yMacros.GetLength(1); y++) {
                    float[,] res = new float[yMacros[x, y].GetLength(0), yMacros[x, y].GetLength(1)];
                    float[,] floatCentered = yMacros[x, y];
                    for (int u = 0; u < res.GetLength(0); u++) {
                        for (int v = 0; v < res.GetLength(1); v++) {
                            res[u, v] = DCT.Forwards(u, v, floatCentered, res.GetLength(0), res.GetLength(1));
                        }
                    }
                    yDCT[x, y] = res;
                }
            }

            float[,][,] cbDCT = new float[cbMacros.GetLength(0), cbMacros.GetLength(1)][,];
            for (int x = 0; x < cbMacros.GetLength(0); x++) {
                for (int y = 0; y < cbMacros.GetLength(1); y++) {
                    float[,] res = new float[cbMacros[x, y].GetLength(0), cbMacros[x, y].GetLength(1)];
                    float[,] floatCentered = cbMacros[x, y];
                    for (int u = 0; u < res.GetLength(0); u++) {
                        for (int v = 0; v < res.GetLength(1); v++) {
                            res[u, v] = DCT.Forwards(u, v, floatCentered, res.GetLength(0), res.GetLength(1));
                        }
                    }
                    cbDCT[x, y] = res;
                }
            }

            float[,][,] crDCT = new float[crMacros.GetLength(0), crMacros.GetLength(1)][,];
            for (int x = 0; x < crMacros.GetLength(0); x++) {
                for (int y = 0; y < crMacros.GetLength(1); y++) {
                    float[,] res = new float[crMacros[x, y].GetLength(0), crMacros[x, y].GetLength(1)];
                    float[,] floatCentered = crMacros[x, y];
                    for (int u = 0; u < res.GetLength(0); u++) {
                        for (int v = 0; v < res.GetLength(1); v++) {
                            res[u, v] = DCT.Forwards(u, v, floatCentered, res.GetLength(0), res.GetLength(1));
                        }
                    }
                    crDCT[x, y] = res;
                }
            }

            foreach (var m in yDCT) {
                for (int x = 0; x < m.GetLength(0); x++) {
                    for (int y = 0; y < m.GetLength(1); y++) {
                        m[x, y] = MathF.Round(m[x, y] / Q_LUMINOSITY[x, y]);
                    }
                }
            }

            foreach (var m in cbDCT) {
                for (int x = 0; x < m.GetLength(0); x++) {
                    for (int y = 0; y < m.GetLength(1); y++) {
                        m[x, y] = MathF.Round(m[x, y] / Q_CHROMINANCE[x, y]);
                    }
                }
            }

            foreach (var m in crDCT) {
                for (int x = 0; x < m.GetLength(0); x++) {
                    for (int y = 0; y < m.GetLength(1); y++) {
                        m[x, y] = MathF.Round(m[x, y] / Q_CHROMINANCE[x, y]);
                    }
                }
            }

            yQuan = yDCT;
            cbQuan = cbDCT;
            crQuan = crDCT;
        }

        public void Decompress() {
            diffFrame = Decompress(PrevFrame.GetMatrix().YhLength, PrevFrame.GetMatrix().YvLength);
        }

        private DiffFrame Decompress(int width, int height) {
            float[,][,] yUnquantized = new float[yQuan.GetLength(0), yQuan.GetLength(1)][,];
            for (int u = 0; u < yUnquantized.GetLength(0); u++) {
                for (int v = 0; v < yUnquantized.GetLength(1); v++) {
                    float[,] res = new float[yQuan[u, v].GetLength(0), yQuan[u, v].GetLength(1)];
                    for (int x = 0; x < yQuan[u, v].GetLength(0); x++) {
                        for (int y = 0; y < yQuan[u, v].GetLength(1); y++) {
                            res[x, y] = Math.Clamp(yQuan[u, v][x, y] * Q_LUMINOSITY[x, y], sbyte.MinValue, sbyte.MaxValue);
                        }
                    }
                    yUnquantized[u, v] = res;
                }
            }

            float[,][,] cbUnquantized = new float[cbQuan.GetLength(0), cbQuan.GetLength(1)][,];
            for (int u = 0; u < cbUnquantized.GetLength(0); u++) {
                for (int v = 0; v < cbUnquantized.GetLength(1); v++) {
                    float[,] res = new float[cbQuan[u, v].GetLength(0), cbQuan[u, v].GetLength(1)];
                    for (int x = 0; x < cbQuan[u, v].GetLength(0); x++) {
                        for (int y = 0; y < cbQuan[u, v].GetLength(1); y++) {
                            res[x, y] = Math.Clamp(cbQuan[u, v][x, y] * Q_CHROMINANCE[x, y], sbyte.MinValue, sbyte.MaxValue);
                        }
                    }
                    cbUnquantized[u, v] = res;
                }
            }

            float[,][,] crUnquantized = new float[crQuan.GetLength(0), crQuan.GetLength(1)][,];
            for (int u = 0; u < crUnquantized.GetLength(0); u++) {
                for (int v = 0; v < crUnquantized.GetLength(1); v++) {
                    float[,] res = new float[crQuan[u, v].GetLength(0), crQuan[u, v].GetLength(1)];
                    for (int x = 0; x < crQuan[u, v].GetLength(0); x++) {
                        for (int y = 0; y < crQuan[u, v].GetLength(1); y++) {
                            res[x, y] = Math.Clamp(crQuan[u, v][x, y] * Q_CHROMINANCE[x, y], sbyte.MinValue, sbyte.MaxValue);
                        }
                    }
                    crUnquantized[u, v] = res;
                }
            }

            float[,][,] yUncompressed = new float[yUnquantized.GetLength(0), yUnquantized.GetLength(1)][,];
            for (int u = 0; u < yUncompressed.GetLength(0); u++) {
                for (int v = 0; v < yUncompressed.GetLength(1); v++) {
                    float[,] res = new float[yUnquantized[u, v].GetLength(0), yUnquantized[u, v].GetLength(1)];
                    for (int x = 0; x < res.GetLength(0); x++) {
                        for (int y = 0; y < res.GetLength(1); y++) {
                            res[x, y] = DCT.Backwards(x, y, yUnquantized[u, v], res.GetLength(0), res.GetLength(1));
                        }
                    }
                    yUncompressed[u, v] = res;
                }
            }

            float[,][,] cbUncompressed = new float[cbUnquantized.GetLength(0), cbUnquantized.GetLength(1)][,];
            for (int u = 0; u < cbUncompressed.GetLength(0); u++) {
                for (int v = 0; v < cbUncompressed.GetLength(1); v++) {
                    float[,] res = new float[cbUnquantized[u, v].GetLength(0), cbUnquantized[u, v].GetLength(1)];
                    for (int x = 0; x < res.GetLength(0); x++) {
                        for (int y = 0; y < res.GetLength(1); y++) {
                            res[x, y] = DCT.Backwards(x, y, cbUnquantized[u, v], res.GetLength(0), res.GetLength(1));
                        }
                    }
                    cbUncompressed[u, v] = res;
                }
            }

            float[,][,] crUncompressed = new float[crUnquantized.GetLength(0), crUnquantized.GetLength(1)][,];
            for (int u = 0; u < crUncompressed.GetLength(0); u++) {
                for (int v = 0; v < crUncompressed.GetLength(1); v++) {
                    float[,] res = new float[crUnquantized[u, v].GetLength(0), crUnquantized[u, v].GetLength(1)];
                    for (int x = 0; x < res.GetLength(0); x++) {
                        for (int y = 0; y < res.GetLength(1); y++) {
                            res[x, y] = DCT.Backwards(x, y, crUnquantized[u, v], res.GetLength(0), res.GetLength(1));
                        }
                    }
                    crUncompressed[u, v] = res;
                }
            }

            var fullMatrices = BuildFullMatrices(yUncompressed, cbUncompressed, crUncompressed, width, height);

            return new DiffFrame() {
                yDiff = fullMatrices.Item1,
                cbDiff = fullMatrices.Item2,
                crDiff = fullMatrices.Item3
            };
        }

        private (float[,], float[,], float[,]) BuildFullMatrices(float[,][,] yM, float[,][,] cbM, float[,][,] crM, int width, int height) {
            int lWBlocks = (int)Math.Ceiling(width / (float)BLOCK_SIZE);
            int lHBlocks = (int)Math.Ceiling(height / (float)BLOCK_SIZE);
            float[,] yMatrix = new float[width, height];
            for (int x = 0; x < lWBlocks; x++) {
                for (int y = 0; y < lHBlocks; y++) {
                    float[,] currBlock = yM[x, y];
                    for (int u = 0; u < currBlock.GetLength(0); u++) {
                        for (int v = 0; v < currBlock.GetLength(1); v++) {
                            if (x * BLOCK_SIZE + u >= width || y * BLOCK_SIZE + v >= height) break;
                            yMatrix[x * BLOCK_SIZE + u, y * BLOCK_SIZE + v] = currBlock[u, v];
                        }
                    }
                }
            }

            int cWidth = (width / 2);
            int cWBlocks = (int)Math.Ceiling(cWidth / (float)BLOCK_SIZE);
            int cHeight = (height / 2);
            int cHBlocks = (int)Math.Ceiling(cHeight / (float)BLOCK_SIZE);

            float[,] cbMatrix = new float[cWidth, cHeight];
            for (int y = 0; y < cHBlocks; y++) {
                for (int x = 0; x < cWBlocks; x++) {
                    float[,] currBlock = cbM[x, y];
                    for (int u = 0; u < currBlock.GetLength(0); u++) {
                        for (int v = 0; v < currBlock.GetLength(1); v++) {
                            if ((x * BLOCK_SIZE + u) >= cWidth || (y * BLOCK_SIZE + v) >= cHeight) break;
                            cbMatrix[x * BLOCK_SIZE + u, y * BLOCK_SIZE + v] = currBlock[u, v];
                        }
                    }
                }
            }

            float[,] crMatrix = new float[cWidth, cHeight];
            for (int x = 0; x < cWBlocks; x++) {
                for (int y = 0; y < cHBlocks; y++) {
                    float[,] currBlock = crM[x, y];
                    for (int u = 0; u < currBlock.GetLength(0); u++) {
                        for (int v = 0; v < currBlock.GetLength(1); v++) {
                            if ((x * BLOCK_SIZE + u) >= cWidth || (y * BLOCK_SIZE + v) >= cHeight) break;
                            crMatrix[x * BLOCK_SIZE + u, y * BLOCK_SIZE + v] = currBlock[u, v];
                        }
                    }
                }
            }

            return (yMatrix, cbMatrix, crMatrix);
        }

        private byte[] CreateByteStream() {

            int vectorLength = yVectors.GetLength(0) * yVectors.GetLength(1) +
                                cbVectors.GetLength(0) * cbVectors.GetLength(1) +
                                crVectors.GetLength(0) * crVectors.GetLength(1);
            int matrixLength = yQuan.GetLength(0) * yQuan.GetLength(1) * yQuan[0, 0].GetLength(0) * yQuan[0, 0].GetLength(1) +
                                cbQuan.GetLength(0) * cbQuan.GetLength(1) * cbQuan[0, 0].GetLength(0) * cbQuan[0, 0].GetLength(1) +
                                crQuan.GetLength(0) * crQuan.GetLength(1) * crQuan[0, 0].GetLength(0) * crQuan[0, 0].GetLength(1);

            byte[] bytes = new byte[matrixLength + vectorLength * 2];
            int index = 0;

            foreach (var m in yQuan) {
                var deNoodled = DeNoodle(TurnToBytes(m));
                for (int i = 0; i < deNoodled.Length; i++) {
                    bytes[index++] = deNoodled[i];
                }
            }

            foreach (var m in cbQuan) {
                var deNoodled = DeNoodle(TurnToBytes(m));
                for (int i = 0; i < deNoodled.Length; i++) {
                    bytes[index++] = deNoodled[i];
                }
            }

            foreach (var m in crQuan) {
                var deNoodled = DeNoodle(TurnToBytes(m));
                for (int i = 0; i < deNoodled.Length; i++) {
                    bytes[index++] = deNoodled[i];
                }
            }

            for (int x = 0; x < yVectors.GetLength(0); x++) {
                for (int y = 0; y < yVectors.GetLength(1); y++) {
                    bytes[index++] = (byte) yVectors[x, y].X;
                    bytes[index++] = (byte) yVectors[x, y].Y;
                }
            }

            for (int x = 0; x < cbVectors.GetLength(0); x++) {
                for (int y = 0; y < cbVectors.GetLength(1); y++) {
                    bytes[index++] = (byte)cbVectors[x, y].X;
                    bytes[index++] = (byte)cbVectors[x, y].Y;
                }
            }

            for (int x = 0; x < crVectors.GetLength(0); x++) {
                for (int y = 0; y < crVectors.GetLength(1); y++) {
                    bytes[index++] = (byte)crVectors[x, y].X;
                    bytes[index++] = (byte)crVectors[x, y].Y;
                }
            }

            List<byte> compressed = new();

            for (int i = 0; i < bytes.Length; i++) {
                byte r;
                for (r = 1; i + 1 < bytes.Length && bytes[i] == bytes[i + 1] && r != 255; r++, i++) { }
                if (bytes[i] == MRLE_KEY || r > 1) {
                    compressed.Add(MRLE_KEY);
                    compressed.Add(r);
                    compressed.Add(bytes[i]);
                } else {
                    compressed.Add(bytes[i]);
                }
            }

            return compressed.ToArray();
        }

        public static (float[,][,], float[,][,], float[,][,], IntVector2[,], IntVector2[,], IntVector2[,], int) ReadByteStream(byte[] b, int width, int height) {
            int index = 0;
            //Unmrle
            List<byte> expand = new();
            for (; index < b.Length; index++) {
                if (b[index] == MRLE_KEY) {
                    //next value is run length
                    int length = b[++index];
                    //next value is run value
                    index++;
                    for (int i = 0; i < length; i++) {
                        expand.Add(b[index]);
                    }
                } else {
                    expand.Add(b[index]);
                }
            }
            int mrleindex = index;

            index = 0;
            int lWBlocks = (int)Math.Ceiling(width / (float) BLOCK_SIZE);
            int lHBlocks = (int)Math.Ceiling(height / (float) BLOCK_SIZE);

            float[,][,] yComp = new float[lWBlocks, lHBlocks][,];
            for (int x = 0; x < yComp.GetLength(0); x++) {
                for (int y = 0; y < yComp.GetLength(1); y++) {
                    sbyte[] block = new sbyte[BLOCK_SIZE * BLOCK_SIZE];
                    for (int j = 0; j < block.Length; j++) {
                        block[j] = (sbyte)expand[index++];
                    }
                    float[,] res = ReNoodle(block);

                    yComp[x, y] = res;
                }
            }

            int cWidth = (width / 2);
            int cWBlocks = (int)Math.Ceiling(cWidth / (float)BLOCK_SIZE);
            int cHeight = (height / 2);
            int cHBlocks = (int)Math.Ceiling(cHeight / (float)BLOCK_SIZE);

            float[,][,] cbComp = new float[cWBlocks, cHBlocks][,];
            for (int x = 0; x < cbComp.GetLength(0); x++) {
                for (int y = 0; y < cbComp.GetLength(1); y++) {
                    sbyte[] block = new sbyte[BLOCK_SIZE * BLOCK_SIZE];
                    for (int j = 0; j < block.Length; j++) {
                        block[j] = (sbyte)expand[index++];
                    }
                    float[,] res = ReNoodle(block);

                    cbComp[x, y] = res;
                }
            }

            float[,][,] crComp = new float[cWBlocks, cHBlocks][,];
            for (int x = 0; x < cbComp.GetLength(0); x++) {
                for (int y = 0; y < cbComp.GetLength(1); y++) {
                    sbyte[] block = new sbyte[BLOCK_SIZE * BLOCK_SIZE];
                    for (int j = 0; j < block.Length; j++) {
                        block[j] = (sbyte)expand[index++];
                    }
                    float[,] res = ReNoodle(block);

                    crComp[x, y] = res;
                }
            }

            IntVector2[,] yVectors = new IntVector2[(int) Math.Ceiling(width / (double) MACROBLOCK_SIZE), (int) Math.Ceiling(height / (double)MACROBLOCK_SIZE)];
            IntVector2[,] cbVectors = new IntVector2[(int)Math.Ceiling(width / (double)MACROBLOCK_SIZE), (int)Math.Ceiling(height / (double)MACROBLOCK_SIZE)];
            IntVector2[,] crVectors = new IntVector2[(int)Math.Ceiling(width / (double)MACROBLOCK_SIZE), (int)Math.Ceiling(height / (double)MACROBLOCK_SIZE)];

            for (int x = 0; x < yVectors.GetLength(0); x++) {
                for (int y = 0; y < yVectors.GetLength(1); y++) {
                    yVectors[x, y].X = (sbyte) expand[index++];
                    yVectors[x, y].Y = (sbyte) expand[index++];
                }
            }

            for (int x = 0; x < cbVectors.GetLength(0); x++) {
                for (int y = 0; y < cbVectors.GetLength(1); y++) {
                    cbVectors[x, y].X = (sbyte)expand[index++];
                    cbVectors[x, y].Y = (sbyte)expand[index++];
                }
            }

            for (int x = 0; x < crVectors.GetLength(0); x++) {
                for (int y = 0; y < crVectors.GetLength(1); y++) {
                    crVectors[x, y].X = (sbyte)expand[index++];
                    crVectors[x, y].Y = (sbyte)expand[index++];
                }
            }

            return (yComp, cbComp, crComp, yVectors, cbVectors, crVectors, mrleindex);
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

        public override byte[] GetCompressedBytes(byte[] head, int frame = 0) {
            //stack properties
            Compress();
            byte[] myBytes = CreateByteStream();
            byte[] compSize = Utils.IntToByteBE(myBytes.Length);
            byte[] addToHead = new byte[head.Length + compSize.Length];
            for (int i = 0; i < head.Length; i++) {
                addToHead[i] = head[i];
            }
            for (int i = 0; i < compSize.Length; i++) {
                addToHead[i + head.Length] = compSize[i];
            }
            byte[] prevFrame = PrevFrame!.GetCompressedBytes(addToHead, ++frame);
            byte[] totalBytes = new byte[prevFrame.Length + myBytes.Length];

            // add prev frame bytes first
            for (int i = 0; i < prevFrame.Length; i++) {
                totalBytes[i] = prevFrame[i];
            }

            // add my bytes next
            for (int i = 0; i < myBytes.Length; i++) {
                totalBytes[prevFrame.Length + i] = myBytes[i];
            }

            return totalBytes;
        }
    }
}
