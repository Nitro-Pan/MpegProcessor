using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Microsoft.Win32;

namespace MpegProcessingWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainWindowController c;

        IFrame first;
        PFrame second;

        public MainWindow() {
            InitializeComponent();
            c = new(this);
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e) {
            c.LoadImage(OriginalImage);
            RGBAPixel[,] p = MainWindowController.ConvertToMatrix(c.srcImg);
            ImageMatrix matrix = new(p);
            ImageJPEG jpeg = new(matrix);
            c.jpeg = jpeg;
            ResultImage.Source = jpeg.GetBitmap();
        }

        private void CompressButton_Click(object sender, RoutedEventArgs e) {
            Trace.WriteLine("Compressing...");
            c.jpeg.Compress();
            Trace.WriteLine("Done!");
        }

        private void UncompressButton_Click(object sender, RoutedEventArgs e) {
            Trace.WriteLine("Uncompressing...");
            ImageJPEG jpeg = c.jpeg.Decompress();
            Trace.WriteLine("Done!");
            ResultImage.Source = jpeg.GetBitmap();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e) {
            byte[] jpegStream = c.jpeg.CreateByteStream();
            SaveFileDialog files = new();
            files.Filter = "Noodle Files | *.noodle";
            if (files?.ShowDialog() ?? false) {
                File.WriteAllBytes(files.FileName, jpegStream);
            }
        }

        private void OpenCompressedButton_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog files = new();
            files.Filter = "Noodle Files | *.noodle";
            if (files?.ShowDialog() ?? false) {
                byte[] stream = File.ReadAllBytes(files.FileName);
                ImageJPEG jpeg = new(stream);
                jpeg = jpeg.Decompress();
                ResultImage.Source = jpeg.GetBitmap();
            }
        }

        private void LoadFrame1_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog files = new();
            files.Filter = "All Files | *";
            if (files?.ShowDialog() ?? false) {
                BitmapSource bmp = new BitmapImage(new(files.FileName));
                ImageMatrix i = new(MainWindowController.ConvertToMatrix(bmp));
                first = new IFrame(new ImageJPEG(i));
                Image im = new();
                im.Source = first.GetBitmap();
                im.Width = Frame0Canvas.Width;
                Frame0Canvas.Children.Add(im);
            }
        }

        private void LoadFrame2_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog files = new();
            files.Filter = "All Files | *.*";
            if (files?.ShowDialog() ?? false) {
                BitmapSource bmp = new BitmapImage(new(files.FileName));
                OriginalImage.Source = bmp;
                ImageMatrix i = new(MainWindowController.ConvertToMatrix(bmp));
                second = new(i, first);
                second.Compress();
                second.Decompress();
                Image im = new();
                im.Source = second.GetBitmap();
                im.Width = Frame1Canvas.Width;
                Frame1Canvas.Children.Add(im);
                Line[] lines = second.GetLines();
                foreach (Line l in lines) {
                    Frame1Canvas.Children.Add(l);
                }
                ResultImage.Source = second.GetBitmap();
            }
        }

        private void SaveMpeg_Click(object sender, RoutedEventArgs e) {
            byte[] mpegStream = second.GetCompressedBytes(Array.Empty<byte>());
            SaveFileDialog files = new();
            files.Filter = "Noodlm Files | *.noodlm";
            if (files?.ShowDialog() ?? false) {
                File.WriteAllBytes(files.FileName, mpegStream);
            }
        }

        private void OpenMpeg_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog files = new();
            files.Filter = "Noodlm Files | *.noodlm";
            if (files?.ShowDialog() ?? false) { 
                byte[] stream = File.ReadAllBytes(files.FileName);
                CompressionTrain train = new(stream);
                var bmps = train.GetAllBitmaps();
                OriginalImage.Source = bmps[0];
                ResultImage.Source = bmps[1];
            }
        }
    }
}
