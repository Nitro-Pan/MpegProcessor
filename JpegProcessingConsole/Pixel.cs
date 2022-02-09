using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JpegProcessingConsole
{
    public class RGBAPixel
    {
        public byte R { get; private set; }
        public byte G { get; private set; }
        public byte B { get; private set; }
        public byte A { get; private set; }

        public RGBAPixel(byte R, byte G, byte B, byte A) {
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
            byte Y = (byte) Math.Clamp((0.299 * R) + (0.587 * G) + (0.114 * B) + 0.5, 0, 255);
            byte Cb = (byte) Math.Clamp(128 - (0.168736 * R) - (0.331264 * G) + (0.5 * B) + 0.5, 0, 255);
            byte Cr = (byte) Math.Clamp(128 + (0.5 * R) - (0.418688 * G) - (0.081312 * B) + 0.5, 0, 255);
            return new(Y, Cr, Cb);
        }

        public override string ToString() {
            return $"[{R}, {G}, {B}, {A}]";
        }
    }

    public class YCbCrPixel
    {
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
            byte R = (byte) Math.Clamp(Y + 1.402 * (Cr - 128) + 0.5, 0, 255);
            byte G = (byte) Math.Clamp(Y - 0.344136 * (Cb - 128) - 0.714136 * (Cr - 128) + 0.5, 0, 255);
            byte B = (byte) Math.Clamp(Y + 1.772 * (Cb - 128) + 0.5, 0, 255);
            return new(R, G, B, 255);
        }

        public override string ToString() {
            return $"[{Y}, {Cb}, {Cr}]";
        }
    }
}
