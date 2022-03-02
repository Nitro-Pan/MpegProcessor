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

namespace MpegProcessingWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainWindowController c;
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
    }
}
