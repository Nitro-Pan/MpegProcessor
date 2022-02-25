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
        private int width { get { return matrix.yhLength; } }
        private int height { get { return matrix.yvLength; } }
        public bool isCompressed { get; private set; }
        public const int DEFAULT_DPI = 96;
        public ImageJPEG(ImageMatrix matrix) { 
            this.matrix = matrix;
            isCompressed = false;
        }

        public void Compress() { 
            isCompressed = true;
        }

        public byte[] CreateByteStream() {
            if (!isCompressed) throw new InvalidOperationException("JPEG must be manually compressed before saving into a byte stream");

            byte[] res = new byte[1];



            return res;
        }

        public BitmapSource GetBitmap() {
            WriteableBitmap bmp = new(width, height, DEFAULT_DPI, DEFAULT_DPI, PixelFormats.Bgra32, null);
            int bytesPerPixel = 4;
            int stride = bytesPerPixel * width;
            int totalBytes = stride * height;
            byte[] pixels = new byte[totalBytes];
            RGBAPixel[,] sourcePixels = matrix.GetExpandedRGBAImage();

            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    pixels[(y * width + x) * bytesPerPixel + 0] = sourcePixels[x, y].B;
                    pixels[(y * width + x) * bytesPerPixel + 1] = sourcePixels[x, y].G;
                    pixels[(y * width + x) * bytesPerPixel + 2] = sourcePixels[x, y].R;
                    pixels[(y * width + x) * bytesPerPixel + 3] = sourcePixels[x, y].A;
                }
            }
            Int32Rect rect = new(0, 0, width, height);
            bmp.WritePixels(rect, pixels, stride, 0);

            return bmp;
        }
    }
}
