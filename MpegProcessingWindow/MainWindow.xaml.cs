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

        private List<MPEGFrame> frames = new List<MPEGFrame>();
        // private IFrame first;
        // private PFrame second;

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
                frames.Add(new IFrame(new ImageJPEG(i)));
                Image im = new();
                im.Source = frames[^1].GetBitmap();
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
                frames.Add(new PFrame(i, frames[^1]));
                (frames[^1] as PFrame)?.Compress();
                (frames[^1] as PFrame)?.Decompress();
                Image im = new();
                im.Source = frames[^1].GetBitmap();
                im.Width = Frame1Canvas.Width;
                Frame1Canvas.Children.Add(im);
                Line[] lines = (frames[^1] as PFrame).GetLines();
                foreach (Line l in lines) {
                    Frame1Canvas.Children.Add(l);
                }
                ResultImage.Source = frames[^1].GetBitmap();
            }
        }

        private void SaveMpeg_Click(object sender, RoutedEventArgs e) {
            byte[] mpegStream = frames[^1].GetCompressedBytes(Array.Empty<byte>(), 0);
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
                frames.Clear();
                foreach (var frame in train.frames) {
                    frames.Add(frame);
                }
            }
        }

        private void Playback_Click(object sender, RoutedEventArgs e) {
            Playback pb = new Playback(frames);
            pb.Show();
        }
    }
}
