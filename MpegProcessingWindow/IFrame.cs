using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MpegProcessingWindow {
    internal class IFrame : MPEGFrame {
        public ImageJPEG ThisFrameJPEG { get; private set; }

        public IFrame(ImageJPEG thisFrame, MPEGFrame? prevFrame = null, MPEGFrame? nextFrame = null) : base(thisFrame.matrix, 
                                                                                                            prevFrame, 
                                                                                                            nextFrame) {
            this.ThisFrameJPEG = thisFrame;
        }

        public IFrame(byte[] bytes, int width, int height) {
            ImageJPEG t = new(bytes, width, height);
            ThisFrameJPEG = t.Decompress();
            ThisFrame = ThisFrameJPEG.matrix;
        }

        public override ImageMatrix GetMatrix() {
            return ThisFrameJPEG.matrix;
        }

        public override BitmapSource GetBitmap() {
            return ThisFrameJPEG.GetBitmap();
        }

        public override byte[] GetCompressedBytes(byte[] head, int frame) {
            ThisFrameJPEG.Compress();
            if (PrevFrame == null) {
                byte[] bytes = ThisFrameJPEG.CreateByteStream();
                byte[] frameCount = Utils.IntToByteBE(++frame);
                byte[] myLength = Utils.IntToByteBE(bytes.Length - sizeof(int) * 2);
                byte[] finalHead = new byte[frameCount.Length + myLength.Length + head.Length];

                for (int i = 0; i < frameCount.Length; i++) { 
                    finalHead[i] = frameCount[i];
                }

                for (int i = 0; i < myLength.Length; i++) { 
                    finalHead[i + frameCount.Length] = myLength[i];
                }

                for (int i = 0; i < head.Length; i++) {
                    finalHead[i + frameCount.Length + myLength.Length] = head[i];
                }

                byte[] final = new byte[finalHead.Length + bytes.Length];
                for (int i = 0; i < finalHead.Length; i++) {
                    final[i] = finalHead[i];
                }

                for (int i = 0; i < bytes.Length; i++) { 
                    final [i + finalHead.Length] = bytes[i];
                }

                return final;
            }
            //stack properties
            // CreateByteStream contains header data, very much unwanted for MPEG.
            byte[] myBytesWithHeader = ThisFrameJPEG.CreateByteStream();
            byte[] myBytes = new byte[myBytesWithHeader.Length - sizeof(int) * 2];

            // remove header data if a previous frame exists
            for (int i = 0; i < myBytesWithHeader.Length - sizeof(int) * 2; i++) {
                myBytes[i] = myBytesWithHeader[i + sizeof(int) * 2];
            }

            byte[] compSize = Utils.IntToByteBE(myBytes.Length);
            byte[] addToHead = new byte[head.Length + compSize.Length];
            for (int i = 0; i < compSize.Length; i++) {
                addToHead[i] = compSize[i];
            }
            for (int i = 0; i < head.Length; i++) {
                addToHead[i + compSize.Length] = head[i];
            }
            byte[] prevFrame = PrevFrame.GetCompressedBytes(addToHead, ++frame);

            byte[] totalBytes = new byte[prevFrame.Length + myBytes.Length];

            // put other bytes first, those frames are before me
            for (int i = 0; i < prevFrame.Length; i++) {
                totalBytes[i] = prevFrame[i];
            }

            // add my bytes next
            for (int i = 0; i < myBytes.Length; i++) {
                totalBytes[prevFrame.Length + i] = myBytes[i];
            }

            return totalBytes; 
        }

        public static (ImageJPEG.Header, float[][,], float[][,], float[][,]) ReadByteStream(byte[] b, int width, int height) {
            return ImageJPEG.ReadByteStream(b);
        }
    }
}
