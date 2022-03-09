using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MpegProcessingWindow {
    internal class ImageJPEG {
        public readonly ImageMatrix matrix;
        private int Width { get { return matrix.YhLength; } }
        private int Height { get { return matrix.YvLength; } }
        public bool IsCompressed { get; private set; }
        public const int DEFAULT_DPI = 96;
        public const byte MRLE_KEY = 0;
        public const int BLOCK_SIZE = 8;

        private float[][,] yCompressed;
        private float[][,] cbCompressed;
        private float[][,] crCompressed;

        public Header head;

        private byte[] byteStream;

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

        public struct Header {
            public int width;
            public int height;
            public const int BYTE_LENGTH = 8;

            /// <summary>
            /// Get the bytes of this header as big endian number and little endian text
            /// </summary>
            /// <returns>The bytes of this header as big endian</returns>
            public byte[] GetBytes() {
                byte[] b = new byte[sizeof(int) * 2];
                for (int i = 0; i < sizeof(int); i++) {
                    b[i] = (byte) ((width >> (BYTE_LENGTH * i)) & 0xFF);
                }
                for(int i = 0; i < sizeof(int); i++) {
                    b[i + sizeof(int)] = (byte) ((height >> (BYTE_LENGTH * i)) & 0xFF);
                }
                return b;
            }
        }

        public ImageJPEG(ImageMatrix matrix) { 
            this.matrix = matrix;
            head = new Header {
                width = matrix.YhLength,
                height = matrix.YvLength
            };
            IsCompressed = false;
        }

        public ImageJPEG(byte[] b) {
            var compressed = ReadByteStream(b);
            head = compressed.Item1;
            yCompressed = compressed.Item2;
            cbCompressed = compressed.Item3;
            crCompressed = compressed.Item4;
            IsCompressed = true;
        }

        public ImageJPEG(byte[] b, int width, int height) {
            var compressed = ReadByteStream(b, width, height);
            head = compressed.Item1;
            yCompressed = compressed.Item2;
            cbCompressed = compressed.Item3;
            crCompressed = compressed.Item4;
            IsCompressed = true;
        }

        public void Compress() {
            byte[][,] YSubMatrices = new byte[matrix.YhSubMatrices * matrix.YvSubMatrices][,];
            for (int x = 0; x < matrix.YhSubMatrices; x++) {
                for (int y = 0; y < matrix.YvSubMatrices; y++) {
                    YSubMatrices[(y * matrix.YhSubMatrices) + x] = matrix.GetYSubmatrix(x, y);
                }
            }

            byte[][,] CbSubMatrices = new byte[matrix.CbhSubMatrices * matrix.CbvSubMatrices][,];
            for (int x = 0; x < matrix.CbhSubMatrices; x++) {
                for (int y = 0; y < matrix.CbvSubMatrices; y++) {
                    CbSubMatrices[(y * matrix.CbhSubMatrices) + x] = matrix.GetCbSubmatrix(x, y);
                }
            }

            byte[][,] CrSubMatrices = new byte[matrix.CrhSubMatrices * matrix.CrvSubMatrices][,];
            for (int x = 0; x < matrix.CrhSubMatrices; x++) {
                for (int y = 0; y < matrix.CrvSubMatrices; y++) {
                    CrSubMatrices[(y * matrix.CrhSubMatrices) + x] = matrix.GetCrSubmatrix(x, y);
                }
            }

            yCompressed = new float[matrix.YhSubMatrices * matrix.YvSubMatrices][,];
            for (int i = 0; i < YSubMatrices.Length; i++) {
                float[,] res = new float[YSubMatrices[0].GetLength(0), YSubMatrices[0].GetLength(1)];
                float[,] zeroCentered = CenterOnZero(YSubMatrices[i]);
                for (int u = 0; u < res.GetLength(0); u++) {
                    for (int v = 0; v < res.GetLength(1); v++) {
                        res[u, v] = DCT.Forwards(u, v, zeroCentered, res.GetLength(0), res.GetLength(1));
                    }
                }
                yCompressed[i] = res;
            }

            cbCompressed = new float[matrix.CbhSubMatrices * matrix.CbvSubMatrices][,];
            for (int i = 0; i < CbSubMatrices.Length; i++) { 
                float[,] res = new float[CbSubMatrices[0].GetLength(0), CbSubMatrices[0].GetLength(1)];
                float[,] zeroCentered = CenterOnZero(CbSubMatrices[i]);
                for (int u = 0; u < res.GetLength(0); u++) {
                    for (int v = 0; v < res.GetLength(1); v++) {
                        res[u, v] = DCT.Forwards(u, v, zeroCentered, res.GetLength(0), res.GetLength(1));
                    }
                }
                cbCompressed[i] = res;
            }

            crCompressed = new float[matrix.CrhSubMatrices * matrix.CrvSubMatrices][,];
            for (int i = 0; i < CrSubMatrices.Length; i++) {
                float[,] res = new float[CrSubMatrices[0].GetLength(0), CrSubMatrices[0].GetLength(1)];
                float[,] zeroCentered = CenterOnZero(CrSubMatrices[i]);
                for (int u = 0; u < res.GetLength(0); u++) {
                    for (int v = 0; v < res.GetLength(1); v++) {
                        res[u, v] = DCT.Forwards(u, v, zeroCentered, res.GetLength(0), res.GetLength(1));
                    }
                }
                crCompressed[i] = res;
            }

            for (int i = 0; i < yCompressed.Length; i++) {
                for (int x = 0; x < yCompressed[i].GetLength(0); x++) {
                    for (int y = 0; y < yCompressed[i].GetLength(1); y++) {
                        yCompressed[i][x, y] = (float) Math.Round(yCompressed[i][x, y] / Q_LUMINOSITY[x, y]);
                    }
                }
            }

            for (int i = 0; i < cbCompressed.Length; i++) {
                for (int x = 0; x < cbCompressed[i].GetLength(0); x++) {
                    for (int y = 0; y < cbCompressed[i].GetLength(1); y++) {
                        cbCompressed[i][x, y] = (float) Math.Round(cbCompressed[i][x, y] / Q_CHROMINANCE[x, y]);
                    }
                }
            }

            for (int i = 0; i < crCompressed.Length; i++) {
                for (int x = 0; x < crCompressed[i].GetLength(0); x++) {
                    for (int y = 0; y < crCompressed[i].GetLength(1); y++) {
                        crCompressed[i][x, y] = (float) Math.Round(crCompressed[i][x, y] / Q_CHROMINANCE[x, y]);
                    }
                }
            }

            head = new Header {
                width = matrix.YhLength,
                height = matrix.YvLength
            };

            IsCompressed = true;
        }

        public ImageJPEG Decompress() {
            if (!IsCompressed) throw new InvalidOperationException("JPEG must be manually compressed before saving into a byte stream");

            float[][,] yUnquantized = new float[yCompressed.Length][,];
            for (int i = 0; i < yUnquantized.Length; i++) {
                float[,] res = new float[yCompressed[i].GetLength(0), yCompressed[i].GetLength(1)];
                for (int x = 0; x < yCompressed[i].GetLength(0); x++) {
                    for (int y = 0; y < yCompressed[i].GetLength(1); y++) { 
                        res[x, y] = yCompressed[i][x, y] * Q_LUMINOSITY[x, y];
                    }
                }
                yUnquantized[i] = res;
            }

            float[][,] cbUnquantized = new float[cbCompressed.Length][,];
            for (int i = 0; i < cbUnquantized.Length; i++) {
                float[,] res = new float[cbCompressed[i].GetLength(0), cbCompressed[i].GetLength(1)];
                for (int x = 0; x < cbCompressed[i].GetLength(0); x++) {
                    for (int y = 0; y < cbCompressed[i].GetLength(1); y++) {
                        res[x, y] = cbCompressed[i][x, y] * Q_CHROMINANCE[x, y];
                    }
                }
                cbUnquantized[i] = res;
            }

            float[][,] crUnquantized = new float[crCompressed.Length][,];
            for (int i = 0; i < crUnquantized.Length; i++) {
                float[,] res = new float[crCompressed[i].GetLength(0), crCompressed[i].GetLength(1)];
                for (int x = 0; x < crCompressed[i].GetLength(0); x++) {
                    for (int y = 0; y < crCompressed[i].GetLength(1); y++) {
                        res[x, y] = crCompressed[i][x, y] * Q_CHROMINANCE[x, y];
                    }
                }
                crUnquantized[i] = res;
            }

            byte[][,] yUncompressed = new byte[yUnquantized.Length][,];
            for (int i = 0; i < yUncompressed.Length; i++) {
                float[,] res = new float[yUnquantized[0].GetLength(0), yUnquantized[0].GetLength(1)];
                for (int x = 0; x < res.GetLength(0); x++) {
                    for (int y = 0; y < res.GetLength(1); y++) {
                        res[x, y] = DCT.Backwards(x, y, yUnquantized[i], res.GetLength(0), res.GetLength(1));
                    }
                }
                yUncompressed[i] = RestoreToBytes(res);
            }

            byte[][,] cbUncompressed = new byte[cbUnquantized.Length][,];
            for (int i = 0; i < cbUncompressed.Length; i++) {
                float[,] res = new float[cbUnquantized[0].GetLength(0), cbUnquantized[0].GetLength(1)];
                for (int x = 0; x < res.GetLength(0); x++) {
                    for (int y = 0; y < res.GetLength(1); y++) {
                        res[x, y] = DCT.Backwards(x, y, cbUnquantized[i], res.GetLength(0), res.GetLength(1));
                    }
                }
                cbUncompressed[i] = RestoreToBytes(res);
            }

            byte[][,] crUncompressed = new byte[crUnquantized.Length][,];
            for (int i = 0; i < crUncompressed.Length; i++) {
                float[,] res = new float[crUnquantized[0].GetLength(0), crUnquantized[0].GetLength(1)];
                for (int x = 0; x < res.GetLength(0); x++) {
                    for (int y = 0; y < res.GetLength(1); y++) {
                        res[x, y] = DCT.Backwards(x, y, crUnquantized[i], res.GetLength(0), res.GetLength(1));
                    }
                }
                crUncompressed[i] = RestoreToBytes(res);
            }

            (byte[,], byte[,], byte[,]) fullMatrices = BuildFullMatrices(yUncompressed, cbUncompressed, crUncompressed, head.width, head.height);

            IsCompressed = false;

            return new(new ImageMatrix(fullMatrices.Item1, fullMatrices.Item2, fullMatrices.Item3));
        }

        private (byte[,], byte[,], byte[,]) BuildFullMatrices(byte[][,] yM, byte[][,] cbM, byte[][,] crM, int width, int height) {
            int lWBlocks = (int) Math.Ceiling(width / (float) BLOCK_SIZE);
            int lHBlocks = (int) Math.Ceiling(height / (float) BLOCK_SIZE);
            byte[,] yMatrix = new byte[width, height];
            for (int x = 0; x < lWBlocks; x++) {
                for (int y = 0; y < lHBlocks; y++) {
                    int index = y * lWBlocks + x;
                    byte[,] currBlock = yM[index];
                    for (int u = 0; u < currBlock.GetLength(0); u++) {
                        for (int v = 0; v < currBlock.GetLength(1); v++) {
                            if (x * BLOCK_SIZE + u >= width || y* BLOCK_SIZE + v >= height) break;
                            yMatrix[x * BLOCK_SIZE + u, y * BLOCK_SIZE + v] = currBlock[u, v];
                        }
                    }
                }
            }

            int cWidth = (width / 2);
            int cWBlocks = (int) Math.Ceiling(cWidth / (float) BLOCK_SIZE);
            int cHeight = (height / 2);
            int cHBlocks = (int)Math.Ceiling(cHeight / (float) BLOCK_SIZE);

            byte[,] cbMatrix = new byte[cWidth, cHeight];
            for (int y = 0; y < cHBlocks; y++) {
                for (int x = 0; x < cWBlocks; x++) {
                    int index = y * (cWBlocks) + x;
                    byte[,] currBlock = cbM[index];
                    for (int u = 0; u < currBlock.GetLength(0); u++) {
                        for (int v = 0; v < currBlock.GetLength(1); v++) {
                            if ((x * BLOCK_SIZE + u) >= cWidth || (y * BLOCK_SIZE + v) >= cHeight) break;
                            cbMatrix[x * BLOCK_SIZE + u, y * BLOCK_SIZE + v] = currBlock[u, v];
                        }
                    }
                }
            }

            byte[,] crMatrix = new byte[cWidth, cHeight];
            for (int x = 0; x < cWBlocks; x++) {
                for (int y = 0; y < cHBlocks; y++) {
                    int index = y * (cWBlocks) + x;
                    byte[,] currBlock = crM[index];
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

        private float[,] CenterOnZero(byte[,] arr) {
            float[,] res = new float[arr.GetLength(0), arr.GetLength(1)];

            for (int x = 0; x < res.GetLength(0); x++) {
                for (int y = 0; y < res.GetLength(1); y++) {
                    res[x, y] = Math.Clamp(arr[x, y], byte.MinValue, byte.MaxValue) - 128;
                }
            }

            return res;
        }

        private byte[,] RestoreToBytes(float[,] arr) {
            byte[,] res = new byte[arr.GetLength(0), arr.GetLength(1)];

            for (int x = 0; x < res.GetLength(0); x++) {
                for (int y = 0; y < res.GetLength(1); y++) {
                    res[x, y] = (byte) Math.Clamp(arr[x, y] + 128, byte.MinValue, byte.MaxValue);
                }
            }

            return res;
        }

        public byte[] CreateByteStream() {
            if (!IsCompressed) throw new InvalidOperationException("JPEG must be manually compressed before saving into a byte stream");

            int yLength = yCompressed.Length * BLOCK_SIZE * BLOCK_SIZE;
            int cbLength = cbCompressed.Length * BLOCK_SIZE * BLOCK_SIZE;
            int crLength = crCompressed.Length * BLOCK_SIZE * BLOCK_SIZE;
            byte[] run = new byte[yLength + cbLength + crLength];
            int runIndex = 0;

            foreach (float[,] arr in yCompressed) {
                byte[] b = DeNoodle(TurnToBytes(arr));
                for (int i = 0; i < b.Length; i++) {
                    run[runIndex++] = b[i];
                }
            }

            foreach (float[,] arr in cbCompressed) {
                byte[] b = DeNoodle(TurnToBytes(arr));
                for (int i = 0; i < b.Length; i++) {
                    run[runIndex++] = b[i];
                }
            }

            foreach (float[,] arr in crCompressed) {
                byte[] b = DeNoodle(TurnToBytes(arr));
                for (int i = 0; i < b.Length; i++) {
                    run[runIndex++] = b[i];
                }
            }

            List<byte> compressed = new();
            compressed.AddRange(head.GetBytes());

            for (int i = 0; i < run.Length; i++) {
                byte r;
                for (r = 1; i + 1 < run.Length && run[i] == run[i + 1] && r != 255; r++, i++) { }
                if (run[i] == MRLE_KEY || r > 1) {
                    compressed.Add(MRLE_KEY);
                    compressed.Add(r);
                    compressed.Add(run[i]);
                } else { 
                    compressed.Add(run[i]);
                }
            }

            return compressed.ToArray();
        }

        public static (Header, float[][,], float[][,], float[][,]) ReadByteStream(byte[] b) {
            int width = 0;
            int height = 0;
            int index = 0;

            for (int i = 0; i < sizeof(int); i++) {
                width |= b[index++] << (Header.BYTE_LENGTH * i);
            }

            for (int i = 0; i < sizeof(int); i++) {
                height |= b[index++] << (Header.BYTE_LENGTH * i);
            }

            Header h = new Header {
                width = width,
                height = height
            };

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

            index = 0;
            int lWBlocks = (int)Math.Ceiling(width / (float)BLOCK_SIZE);
            int lHBlocks = (int)Math.Ceiling(height / (float)BLOCK_SIZE);

            float[][,] yComp = new float[lWBlocks * lHBlocks][,];
            for (int i = 0; i < yComp.Length; i++) {
                sbyte[] block = new sbyte[BLOCK_SIZE * BLOCK_SIZE];
                for (int j = 0; j < block.Length; j++) {
                    block[j] = (sbyte) expand[index++];
                }
                float[,] res = ReNoodle(block);
                
                yComp[i] = res;
            }

            int cWidth = (width / 2);
            int cWBlocks = (int)Math.Ceiling(cWidth / (float)BLOCK_SIZE);
            int cHeight = (height / 2);
            int cHBlocks = (int)Math.Ceiling(cHeight / (float)BLOCK_SIZE);

            float[][,] cbComp = new float[cWBlocks * cHBlocks][,];
            for (int i = 0; i < cbComp.Length; i++) {
                sbyte[] block = new sbyte[BLOCK_SIZE * BLOCK_SIZE];
                for (int j = 0; j < block.Length; j++) {
                    block[j] = (sbyte) expand[index++];
                }
                float[,] res = ReNoodle(block);

                cbComp[i] = res;
            }

            float[][,] crComp = new float[cWBlocks * cHBlocks][,];
            for (int i = 0; i < crComp.Length; i++) {
                sbyte[] block = new sbyte[BLOCK_SIZE * BLOCK_SIZE];
                for (int j = 0; j < block.Length; j++) {
                    block[j] = (sbyte) expand[index++];
                }
                float[,] res = ReNoodle(block);

                crComp[i] = res;
            }

            return (h, yComp, cbComp, crComp);
        }

        public static (Header, float[][,], float[][,], float[][,]) ReadByteStream(byte[] b, int width, int height) {
            int index = 0;

            Header h = new Header {
                width = width,
                height = height
            };

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

            index = 0;
            int lWBlocks = (int)Math.Ceiling(width / (float)BLOCK_SIZE);
            int lHBlocks = (int)Math.Ceiling(height / (float)BLOCK_SIZE);

            float[][,] yComp = new float[lWBlocks * lHBlocks][,];
            for (int i = 0; i < yComp.Length; i++) {
                sbyte[] block = new sbyte[BLOCK_SIZE * BLOCK_SIZE];
                for (int j = 0; j < block.Length; j++) {
                    block[j] = (sbyte)expand[index++];
                }
                float[,] res = ReNoodle(block);

                yComp[i] = res;
            }

            int cWidth = (width / 2);
            int cWBlocks = (int)Math.Ceiling(cWidth / (float)BLOCK_SIZE);
            int cHeight = (height / 2);
            int cHBlocks = (int)Math.Ceiling(cHeight / (float)BLOCK_SIZE);

            float[][,] cbComp = new float[cWBlocks * cHBlocks][,];
            for (int i = 0; i < cbComp.Length; i++) {
                sbyte[] block = new sbyte[BLOCK_SIZE * BLOCK_SIZE];
                for (int j = 0; j < block.Length; j++) {
                    block[j] = (sbyte)expand[index++];
                }
                float[,] res = ReNoodle(block);

                cbComp[i] = res;
            }

            float[][,] crComp = new float[cWBlocks * cHBlocks][,];
            for (int i = 0; i < crComp.Length; i++) {
                sbyte[] block = new sbyte[BLOCK_SIZE * BLOCK_SIZE];
                for (int j = 0; j < block.Length; j++) {
                    block[j] = (sbyte)expand[index++];
                }
                float[,] res = ReNoodle(block);

                crComp[i] = res;
            }

            return (h, yComp, cbComp, crComp);
        }

        private static float[,] ReNoodle(byte[] s) {
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

        private static float[,] ReNoodle(sbyte[] s) {
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

        private static byte[] DeNoodle(byte[,] s) {
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

        private static byte[,] TurnToBytes(float[,] arr) {
            byte[,] b = new byte[arr.GetLength(0), arr.GetLength(1)];
            for (int x = 0; x < b.GetLength(0); x++) {
                for (int y = 0; y < b.GetLength(1); y++) {
                    b[x, y] = (byte)arr[x, y];
                }
            }
            return b;
        }

        public BitmapSource GetBitmap() {
            WriteableBitmap bmp = new(Width, Height, DEFAULT_DPI, DEFAULT_DPI, PixelFormats.Bgra32, null);
            int bytesPerPixel = 4;
            int stride = bytesPerPixel * Width;
            int totalBytes = stride * Height;
            byte[] pixels = new byte[totalBytes];
            RGBAPixel[,] sourcePixels = matrix.GetExpandedRGBAImage();

            for (int x = 0; x < Width; x++) {
                for (int y = 0; y < Height; y++) {
                    pixels[(y * Width + x) * bytesPerPixel + 0] = sourcePixels[x, y].B;
                    pixels[(y * Width + x) * bytesPerPixel + 1] = sourcePixels[x, y].G;
                    pixels[(y * Width + x) * bytesPerPixel + 2] = sourcePixels[x, y].R;
                    pixels[(y * Width + x) * bytesPerPixel + 3] = sourcePixels[x, y].A;
                }
            }
            Int32Rect rect = new(0, 0, Width, Height);
            bmp.WritePixels(rect, pixels, stride, 0);

            return bmp;
        }
    }
}
