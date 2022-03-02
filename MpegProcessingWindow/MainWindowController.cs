using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace MpegProcessingWindow
{
    internal class MainWindowController
    {
        private MainWindow window;
        public BitmapSource srcImg { get; private set; }
        public BitmapSource resImg { get; private set; }
        public ImageJPEG jpeg { get; set; }

        public MainWindowController(MainWindow window) {
            this.window = window;
        }

        public void LoadImage(Image target) {
            OpenFileDialog files = new();
            files.Filter = "All Files | *";
            if (files?.ShowDialog() ?? false) {
                BitmapSource bmp = new BitmapImage(new(files.FileName));
                srcImg = bmp;
                target.Source = bmp;
            }
        }

        //move this somewhere else
        public static RGBAPixel[,] ConvertToMatrix(BitmapSource bmp) {
            int bytesPerPixel = (bmp.Format.BitsPerPixel + 7) / 8;
            int stride = bytesPerPixel * bmp.PixelWidth;
            int totalBytes = stride * bmp.PixelHeight;
            byte[] src = new byte[totalBytes];
            RGBAPixel[,] dest = new RGBAPixel[bmp.PixelWidth, bmp.PixelHeight];
            bmp.CopyPixels(src, stride, 0);

            for (int x = 0; x < bmp.PixelWidth; x++) {
                for (int y = 0; y < bmp.PixelHeight; y++) {
                    byte b = src[(y * bmp.PixelWidth + x) * bytesPerPixel];
                    byte g = src[(y * bmp.PixelWidth + x) * bytesPerPixel + 1];
                    byte r = src[(y * bmp.PixelWidth + x) * bytesPerPixel + 2];
                    dest[x, y] = new(r, g, b, 255);
                }
            }

            return dest;
        }
    }
}
