using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MpegProcessingWindow {
    public class ImageMatrix {
        private const int SUBMATRIX_SIZE = 8;
        private byte[,] _yMatrix;
        private byte[,] _cbMatrix;
        private byte[,] _crMatrix;

        public byte[,] YMatrix {
            get { return _yMatrix; }
            private set { _yMatrix = value; }
        }

        public int yhLength { 
            get { return _yMatrix.GetLength(0); }
        }

        public int yvLength {
            get { return _yMatrix.GetLength(1); }
        }

        public int hSubMatrices { 
            get { return _yMatrix.GetLength(1) / SUBMATRIX_SIZE; }
        }

        public int vSubMatrices {
            get { return _yMatrix.GetLength(0) / SUBMATRIX_SIZE; }
        }

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

        public ImageMatrix(byte[,] Y, byte[,] Cb, byte[,] Cr, bool alreadySubsampled = true) {
            _yMatrix = Y;
            _crMatrix = alreadySubsampled ? Cr : SubsampleMatrix(Cr);
            _cbMatrix = alreadySubsampled ? Cb : SubsampleMatrix(Cb);
        }

        public int[,] GetSubmatrix(int x, int y) {
            int[,] subMatrix = new int[SUBMATRIX_SIZE, SUBMATRIX_SIZE];
            for (int nX = 0; nX < SUBMATRIX_SIZE; nX++) {
                for (int nY = 0; nY < SUBMATRIX_SIZE; nY++) {
                    if (x * SUBMATRIX_SIZE + nX >= yvLength || y * SUBMATRIX_SIZE + nY >= yhLength) {
                        subMatrix[nX, nY] = 0;
                    } else {
                        subMatrix[nX, nY] = YMatrix[x * SUBMATRIX_SIZE + nX, y * SUBMATRIX_SIZE + nY];
                    }
                }
            }
            return subMatrix;
        }

        public YCbCrPixel[,] GetExpandedYCrCbImage() { 
            YCbCrPixel[,] result = new YCbCrPixel[yhLength, yvLength];
            byte[,] expandedCr = ExpandSubsampleMatrix(_crMatrix);
            byte[,] expandedCb = ExpandSubsampleMatrix(_cbMatrix);
            for (int x = 0; x < yhLength; x++) {
                for (int y = 0; y < yvLength; y++) {
                    result[x, y] = new(_yMatrix[x, y], expandedCb[x, y], expandedCr[x, y]);
                }
            }
            return result;
        }

        public RGBAPixel[,] GetExpandedRGBAImage() { 
            RGBAPixel[,] result = new RGBAPixel[yhLength, yvLength];
            byte[,] expandedCr = ExpandSubsampleMatrix(_crMatrix);
            byte[,] expandedCb = ExpandSubsampleMatrix(_cbMatrix);
            for (int x = 0; x < yhLength; x++) {
                for (int y = 0; y < yvLength; y++) {
                    result[x, y] = new(new(_yMatrix[x, y], expandedCb[x, y], expandedCr[x, y]));
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
            byte[,] expandedMatrix = new byte[matrix.GetLength(0) * 2, matrix.GetLength(1) * 2];
            for (int x = 0; x < expandedMatrix.GetLength(0); x++) {
                for (int y = 0; y < expandedMatrix.GetLength(1); y++) {
                    expandedMatrix[x, y] = matrix[x / 2, y / 2];
                }
            }
            return expandedMatrix;
        }

        public override string ToString() {
            string res = "";
            for (int x = 0; x < yhLength; x++) {
                for (int y = 0; y < yvLength; y++) {
                    res += YMatrix[x, y] + " ";
                }
                res += '\n';
            }
            return res;
        }
    }
}
