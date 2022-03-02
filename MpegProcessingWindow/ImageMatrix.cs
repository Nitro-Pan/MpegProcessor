using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MpegProcessingWindow {
    public class ImageMatrix {
        public const int SUBMATRIX_SIZE = 8;
        private byte[,] _yMatrix;
        private byte[,] _cbMatrix;
        private byte[,] _crMatrix;

        private byte[,] YMatrix {
            get { return _yMatrix; }
            set { _yMatrix = value; }
        }

        private byte[,] CbMatrix {
            get { return _cbMatrix; }
            set { _cbMatrix = value; }
        }

        private byte[,] CrMatrix {
            get { return _crMatrix; }
            set { _crMatrix = value; }
        }

        public int YhLength { get { return YMatrix.GetLength(0); } }
        public int YvLength { get { return YMatrix.GetLength(1); } }
        public int YhSubMatrices { get { return (int) Math.Ceiling(YhLength / (double) SUBMATRIX_SIZE); } }
        public int YvSubMatrices { get { return (int) Math.Ceiling(YvLength / (double) SUBMATRIX_SIZE); } }

        public int CbhLength { get { return CbMatrix.GetLength(0); } }
        public int CbvLength { get { return CbMatrix.GetLength(1); } }
        public int CbhSubMatrices { get { return (int) Math.Ceiling(CbhLength / (double) SUBMATRIX_SIZE); } }
        public int CbvSubMatrices { get { return (int) Math.Ceiling(CbvLength / (double) SUBMATRIX_SIZE); } }

        public int CrhLength { get { return CrMatrix.GetLength(0); } }
        public int CrvLength { get { return CrMatrix.GetLength(1); } }
        public int CrhSubMatrices { get { return (int) Math.Ceiling(CrhLength / (double) SUBMATRIX_SIZE); } }
        public int CrvSubMatrices { get { return (int) Math.Ceiling(CrvLength / (double) SUBMATRIX_SIZE); } }

        public ImageMatrix(RGBAPixel[,] matrix) {
            int width = matrix.GetLength(0);
            int height = matrix.GetLength(1);
            _yMatrix = new byte[width, height];
            byte[,] cbMatrix = new byte[width, height];
            byte[,] crMatrix = new byte[width, height];

            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    YCbCrPixel p = matrix[x, y].ToYCrCb();
                    _yMatrix[x, y] = p.Y;
                    cbMatrix[x, y] = p.Cb;
                    crMatrix[x, y] = p.Cr;
                }
            }

            _cbMatrix = SubsampleMatrix(cbMatrix);
            _crMatrix = SubsampleMatrix(crMatrix);
        }

        public ImageMatrix(byte[] src, int width, int height, int bytesPerPixel) {
            _yMatrix = new byte[width, height];
            byte[,] cbMatrix = new byte[width, height];
            byte[,] crMatrix = new byte[width, height];

            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    byte b = src[(y * width + x) * bytesPerPixel];
                    byte g = src[(y * width + x) * bytesPerPixel + 1];
                    byte r = src[(y * width + x) * bytesPerPixel + 2];
                    YCbCrPixel p = new RGBAPixel(r, g, b).ToYCrCb();
                    _yMatrix[x, y] = p.Y;
                    cbMatrix[x, y] = p.Cb;
                    crMatrix[x, y] = p.Cr;
                }
            }

            _cbMatrix = SubsampleMatrix(cbMatrix);
            _crMatrix = SubsampleMatrix(crMatrix);
        }

        public ImageMatrix(byte[,] Y, byte[,] Cb, byte[,] Cr, bool alreadySubsampled = true) {
            _yMatrix = Y;
            _crMatrix = alreadySubsampled ? Cr : SubsampleMatrix(Cr);
            _cbMatrix = alreadySubsampled ? Cb : SubsampleMatrix(Cb);
        }

        public byte[,] GetYSubmatrix(int x, int y) {
            if (x >= YhSubMatrices || y >= YvSubMatrices) throw new ArgumentOutOfRangeException("Cannot get a submatrix past the edge of this matrix");

            byte[,] subMatrix = new byte[SUBMATRIX_SIZE, SUBMATRIX_SIZE];
            for (int nX = 0; nX < SUBMATRIX_SIZE; nX++) {
                for (int nY = 0; nY < SUBMATRIX_SIZE; nY++) {
                    if (x * SUBMATRIX_SIZE + nX >= YhLength || y * SUBMATRIX_SIZE + nY >= YvLength) {
                        subMatrix[nX, nY] = 0;
                    } else {
                        subMatrix[nX, nY] = YMatrix[x * SUBMATRIX_SIZE + nX, y * SUBMATRIX_SIZE + nY];
                    }
                }
            }
            return subMatrix;
        }

        public byte[,] GetCbSubmatrix(int x, int y) {
            if (x >= CbhSubMatrices || y >= CbvSubMatrices) throw new ArgumentOutOfRangeException("Cannot get a submatrix past the edge of this matrix");

            byte[,] subMatrix = new byte[SUBMATRIX_SIZE, SUBMATRIX_SIZE];
            for (int nX = 0; nX < SUBMATRIX_SIZE; nX++) {
                for (int nY = 0; nY < SUBMATRIX_SIZE; nY++) {
                    if (x * SUBMATRIX_SIZE + nX >= CbhLength || y * SUBMATRIX_SIZE + nY >= CbvLength) {
                        subMatrix[nX, nY] = 0;
                    } else {
                        subMatrix[nX, nY] = CbMatrix[x * SUBMATRIX_SIZE + nX, y * SUBMATRIX_SIZE + nY];
                    }
                }
            }
            return subMatrix;
        }

        public byte[,] GetCrSubmatrix(int x, int y) {
            if (x >= CrhSubMatrices || y >= CrvSubMatrices) throw new ArgumentOutOfRangeException("Cannot get a submatrix past the edge of this matrix");

            byte[,] subMatrix = new byte[SUBMATRIX_SIZE, SUBMATRIX_SIZE];
            for (int nX = 0; nX < SUBMATRIX_SIZE; nX++) {
                for (int nY = 0; nY < SUBMATRIX_SIZE; nY++) {
                    if (x * SUBMATRIX_SIZE + nX >= CrhLength || y * SUBMATRIX_SIZE + nY >= CrvLength) {
                        subMatrix[nX, nY] = 0;
                    } else {
                        subMatrix[nX, nY] = CrMatrix[x * SUBMATRIX_SIZE + nX, y * SUBMATRIX_SIZE + nY];
                    }
                }
            }
            return subMatrix;
        }

        public YCbCrPixel[,] GetExpandedYCrCbImage() {
            YCbCrPixel[,] result = new YCbCrPixel[YhLength, YvLength];
            byte[,] expandedCr = ExpandSubsampleMatrix(_crMatrix);
            byte[,] expandedCb = ExpandSubsampleMatrix(_cbMatrix);
            for (int x = 0; x < YhLength; x++) {
                for (int y = 0; y < YvLength; y++) {
                    result[x, y] = new(_yMatrix[x, y], expandedCb[x, y], expandedCr[x, y]);
                }
            }
            return result;
        }

        public RGBAPixel[,] GetExpandedRGBAImage() {
            RGBAPixel[,] result = new RGBAPixel[YhLength, YvLength];
            byte[,] expandedCr = ExpandSubsampleMatrix(_crMatrix);
            byte[,] expandedCb = ExpandSubsampleMatrix(_cbMatrix);
            for (int x = 0; x < YhLength; x++) {
                for (int y = 0; y < YvLength; y++) {
                    result[x, y] = new YCbCrPixel(_yMatrix[x, y], expandedCb[x, y], expandedCr[x, y]).ToRGBA();
                }
            }
            return result;
        }

        public (byte[,], byte[,], byte[,]) GetSubsampledImage() {
            return (_yMatrix, _cbMatrix, _crMatrix);
        }

        private byte[,] SubsampleMatrix(byte[,] matrix) {
            byte[,] subMatrix = new byte[matrix.GetLength(0) / 2, matrix.GetLength(1) / 2];
            for (int x = 0; x < subMatrix.GetLength(0); x++) {
                for (int y = 0; y < subMatrix.GetLength(1); y++) {
                    subMatrix[x, y] = matrix[x * 2, y * 2];
                }
            }
            return subMatrix;
        }

        private byte[,] ExpandSubsampleMatrix(byte[,] matrix) {
            byte[,] expandedMatrix = new byte[YMatrix.GetLength(0), YMatrix.GetLength(1)];
            for (int x = 0; x < expandedMatrix.GetLength(0); x++) {
                for (int y = 0; y < expandedMatrix.GetLength(1); y++) {
                    int xi = x / 2 >= matrix.GetLength(0) ? (x - 1) / 2 : x / 2;
                    int yi = y / 2 >= matrix.GetLength(1) ? (y - 1) / 2 : y / 2;
                    expandedMatrix[x, y] = matrix[xi, yi];
                }
            }
            return expandedMatrix;
        }

        public override string ToString() {
            string res = "";
            for (int x = 0; x < YhLength; x++) {
                for (int y = 0; y < YvLength; y++) {
                    res += YMatrix[x, y] + " ";
                }
                res += '\n';
            }
            return res;
        }
    }
}
