using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace MpegProcessingWindow {
    /// <summary>
    /// Interaction logic for playback.xaml
    /// </summary>
    public partial class Playback : Window {
        List<MPEGFrame> frames;
        Image currentFrame;
        int frameIndex = -1;
        bool isPlaying;
        public Playback(List<MPEGFrame> frames) {
            InitializeComponent();
            this.frames = frames;
            GoToNextImage();
        }

        private void GoToNextImage() {
            Image imageToAdd = new();
            frameIndex++;
            imageToAdd.Source = frames[frameIndex %= frames.Count].GetBitmap();
            MovieCanvas.Children.Add(imageToAdd);
            MovieCanvas.Children.Remove(currentFrame);
            currentFrame = imageToAdd;
        }

        private async void PlayFrames() {
            isPlaying = true;
            while (isPlaying) {
                Application.Current.Dispatcher.Invoke(GoToNextImage);
                await Task.Delay(100);
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e) {
            PlayFrames();
        }
    }
}
