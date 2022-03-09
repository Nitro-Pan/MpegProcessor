using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MpegProcessingWindow {
    internal class CompressionTrain {
        public List<MPEGFrame> frames;
        public int MRLE_KEY = 0;

        public CompressionTrain(byte[] allBytes) {
            frames = new List<MPEGFrame>();

            int index = 0;

            int nFrames = 0;
            for (int i = 0; i < sizeof(int); i++) {
                nFrames |= allBytes[index++] << (ImageJPEG.Header.BYTE_LENGTH * i);
            }

            List<int> compressedLengths = new();
            for (int i = 0; i < nFrames; i++) {
                int add = 0;
                for (int j = 0; j < sizeof(int); j++) {
                    add |= allBytes[index++] << (ImageJPEG.Header.BYTE_LENGTH * j);
                }
                compressedLengths.Add(add);
            }

            int width = 0;
            int height = 0;

            for (int i = 0; i < sizeof(int); i++) {
                width |= allBytes[index++] << (ImageJPEG.Header.BYTE_LENGTH * i);
            }

            for (int i = 0; i < sizeof(int); i++) {
                height |= allBytes[index++] << (ImageJPEG.Header.BYTE_LENGTH * i);
            }

            byte[] firstBytes = new byte[compressedLengths[0]];
            for (int i = 0; i < compressedLengths[0]; i++) { 
                firstBytes[i] = allBytes[index++];
            }

            frames.Add(new IFrame(firstBytes, width, height));
            for (int i = 1; i < nFrames; i++) {
                byte[] pBytes = new byte[compressedLengths[i]];
                for (int j = 0; j < pBytes.Length; j++) {
                    pBytes[j] = allBytes[index++];
                }
                frames.Add(new PFrame(pBytes, width, height, frames[i - 1]));
            }
        }

        public List<BitmapSource> GetAllBitmaps() {
            List<BitmapSource> bmps = new();

            foreach (var frame in frames) {
                bmps.Add(frame.GetBitmap());
            }

            return bmps;
        }
    }
}
