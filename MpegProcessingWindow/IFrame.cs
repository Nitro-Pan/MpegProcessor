using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MpegProcessingWindow {
    internal class IFrame : MPEGFrame {
        public ImageJPEG ThisFrameJPEG { get; private set; }

        public IFrame(ImageJPEG thisFrame, MPEGFrame? prevFrame = null, MPEGFrame? nextFrame = null) {
            this.ThisFrameJPEG = thisFrame;
            this.ThisFrame = thisFrame.matrix;
            this.PrevFrame = prevFrame;
            this.NextFrame = nextFrame;
        }

        public override ImageMatrix GetMatrix() {
            return ThisFrameJPEG.matrix;
        }

        public override BitmapSource GetBitmap() {
            return ThisFrameJPEG.GetBitmap();
        }
    }
}
