using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MpegProcessingWindow {
    public class RGBAPixel {
        public byte R { get; private set; }
        public byte G { get; private set; }
        public byte B { get; private set; }
        public byte A { get; private set; }

        public RGBAPixel(byte R, byte G, byte B, byte A = byte.MaxValue) {
            this.R = R;
            this.G = G;
            this.B = B;
            this.A = A;
        }

        public RGBAPixel(YCbCrPixel p) {
            RGBAPixel res = p.ToRGBA();
            R = res.R;
            G = res.G;
            B = res.B;
            A = res.A;
        }

        public YCbCrPixel ToYCrCb() {
            byte Y = (byte) ((0.299f * R) + (0.587f * G) + (0.114f * B) + 0.5f);
            byte Cb = (byte) (128 - (0.168736f * R) - (0.331264f * G) + (0.5f * B) + 0.5f);
            byte Cr = (byte) (128 + (0.5f * R) - (0.418688f * G) - (0.081312f * B) + 0.5f);
            return new(Y, Cb, Cr);
        }

        public override string ToString() {
            return $"[{R}, {G}, {B}, {A}]";
        }
    }

    public class YCbCrPixel {
        public byte Y { get; private set; }
        public byte Cr { get; private set; }
        public byte Cb { get; private set; }

        public YCbCrPixel(byte Y, byte Cb, byte Cr) {
            this.Y = Y;
            this.Cr = Cr;
            this.Cb = Cb;
        }

        public YCbCrPixel(RGBAPixel p) {
            YCbCrPixel res = p.ToYCrCb();
            Y = res.Y;
            Cr = res.Cr;
            Cb = res.Cb;
        }

        public RGBAPixel ToRGBA() {
            int Cr = this.Cr - 128;
            int Cb = this.Cb - 128;
            byte R = (byte) Math.Clamp(Y + 1.402f * Cr + 0.5f, byte.MinValue, byte.MaxValue);
            byte G = (byte) Math.Clamp(Y - 0.344136f * Cb - 0.714136f * Cr + 0.5f, byte.MinValue, byte.MaxValue);
            byte B = (byte) Math.Clamp(Y + 1.772f * Cb + 0.5f, byte.MinValue, byte.MaxValue);
            return new(R, G, B);
        }

        public override string ToString() {
            return $"[{Y}, {Cb}, {Cr}]";
        }
    }
}
