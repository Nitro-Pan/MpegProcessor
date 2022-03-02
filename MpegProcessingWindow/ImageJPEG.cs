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
        private readonly ImageMatrix matrix;
        private int Width { get { return matrix.YhLength; } }
        private int Height { get { return matrix.YvLength; } }
        public bool IsCompressed { get; private set; }
        public const int DEFAULT_DPI = 96;

        private float[][,] yCompressed;
        private float[][,] cbCompressed;
        private float[][,] crCompressed;

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

        public ImageJPEG(ImageMatrix matrix) { 
            this.matrix = matrix;
            IsCompressed = false;
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

            (byte[,], byte[,], byte[,]) fullMatrices = BuildFullMatrices(yUncompressed, cbUncompressed, crUncompressed, Width, Height);

            return new(new(fullMatrices.Item1, fullMatrices.Item2, fullMatrices.Item3));
        }

        private (byte[,], byte[,], byte[,]) BuildFullMatrices(byte[][,] yM, byte[][,] cbM, byte[][,] crM, int width, int height) {
            int lWBlocks = (int) Math.Ceiling(width / 8.0f);
            int lHBlocks = (int) Math.Ceiling(height / 8.0f);
            byte[,] yMatrix = new byte[width, height];
            for (int x = 0; x < lWBlocks; x++) {
                for (int y = 0; y < lHBlocks; y++) {
                    int index = y * lWBlocks + x;
                    byte[,] currBlock = yM[index];
                    for (int u = 0; u < currBlock.GetLength(0); u++) {
                        for (int v = 0; v < currBlock.GetLength(1); v++) {
                            if (x*8 + u >= width || y*8 + v >= height) break;
                            yMatrix[x*8 + u, y*8 + v] = currBlock[u, v];
                        }
                    }
                }
            }

            int cWidth = (width / 2);
            int cWBlocks = (int) Math.Ceiling(cWidth / 8.0f);
            int cHeight = (height / 2);
            int cHBlocks = (int)Math.Ceiling(cHeight / 8.0f);

            byte[,] cbMatrix = new byte[cWidth, cHeight];
            for (int y = 0; y < cHBlocks; y++) {
                for (int x = 0; x < cWBlocks; x++) {
                    int index = y * (cWBlocks) + x;
                    byte[,] currBlock = cbM[index];
                    for (int u = 0; u < currBlock.GetLength(0); u++) {
                        for (int v = 0; v < currBlock.GetLength(1); v++) {
                            if ((x*8 + u) >= cWidth || (y*8 + v) >= cHeight) break;
                            cbMatrix[x*8 + u, y*8 + v] = currBlock[u, v];
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
                            if ((x*8 + u) >= cWidth || (y*8 + v) >= cHeight) break;
                            crMatrix[x*8 + u, y*8 + v] = currBlock[u, v];
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

            byte[] res = new byte[1];



            return res;
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
