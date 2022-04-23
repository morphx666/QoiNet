using DirectBitmapLib;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

// https://github.com/phoboslab/qoi

namespace QoiNet {
    public static class QoiNet {
        private const byte QOI_OP_INDEX = 0x00; /* 00xxxxxx */
        private const byte QOI_OP_DIFF = 0x40; /* 01xxxxxx */
        private const byte QOI_OP_LUMA = 0x80; /* 10xxxxxx */
        private const byte QOI_OP_RUN = 0xc0; /* 11xxxxxx */
        private const byte QOI_OP_RGB = 0xfe; /* 11111110 */
        private const byte QOI_OP_RGBA = 0xff; /* 11111111 */

        private const byte QOI_MASK_2 = 0xc0; /* 11000000 */

        private const int QOI_MAGIC = ('q' << 24) | ('o' << 16) | ('i' << 8) | 'f';
        private const uint QOI_PIXELS_MAX = 400000000u;
        private const int QOI_HEADER_SIZE = 14;
        private static readonly byte[] qoiPadding = { 0, 0, 0, 0, 0, 0, 0, 1 };
        private static readonly int qoiPaddingSize = sizeof(byte);// * qoiPadding.Length;

        public record Description {
            public uint Width;
            public uint Height;
            public byte Channels;
            public byte ColorSpace;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct QoiRgba {
            public struct SRgba {
                public byte R;
                public byte G;
                public byte B;
                public byte A;
            }

            [FieldOffset(0)]
            public SRgba Rgba;

            [FieldOffset(0)]
            public uint V;
        }

        public static (byte[] Bytes, Description Description)? Encode(Bitmap bitmap) {
            DirectBitmap dbmp = new(bitmap);
            byte[] pixels = dbmp.Bits;

            Description description = new();
            description.Width = (uint)dbmp.Width;
            description.Height = (uint)dbmp.Height;
            description.Channels = (byte)dbmp.BytesPerPixel;

            if(description.Width == 0 || description.Height == 0 ||
               description.Channels < 3 || description.Channels > 4 ||
               description.ColorSpace > 1 ||
               description.Height >= QOI_PIXELS_MAX / description.Width) {
                return null;
            }

            int maxSize = (int)(description.Width * description.Height * (description.Channels + 1) +
                                QOI_HEADER_SIZE + qoiPaddingSize);
            byte[] bytes = new byte[maxSize];

            int p = 0;
            Write32(bytes, ref p, QOI_MAGIC);
            Write32(bytes, ref p, (int)description.Width);
            Write32(bytes, ref p, (int)description.Height);
            bytes[p++] = description.Channels;
            bytes[p++] = description.ColorSpace;

            int run = 0;
            QoiRgba[] index = new QoiRgba[64];
            QoiRgba pxPrev = new();
            pxPrev.Rgba.A = 255;
            QoiRgba px = pxPrev;

            int pxLen = (int)(description.Width * description.Height * description.Channels);
            int pxEnd = pxLen - description.Channels;

            for(int pxPos = 0; pxPos < pxLen; pxPos += description.Channels) {
                px.Rgba.R = pixels[pxPos + 2];
                px.Rgba.G = pixels[pxPos + 1];
                px.Rgba.B = pixels[pxPos + 0];
                if(description.Channels == 4) px.Rgba.A = pixels[pxPos + 3];

                if(px.V == pxPrev.V) {
                    run++;
                    if(run == 62 || pxPos == pxEnd) {
                        bytes[p++] = (byte)(QOI_OP_RUN | (run - 1));
                        run = 0;
                    }
                } else {
                    if(run > 0) {
                        bytes[p++] = (byte)(QOI_OP_RUN | (run - 1));
                        run = 0;
                    }

                    int indexPos = ColorHash(px.Rgba);

                    if(index[indexPos].V == px.V) {
                        bytes[p++] = (byte)(QOI_OP_INDEX | indexPos);
                    } else {
                        index[indexPos] = px;

                        if(px.Rgba.A == pxPrev.Rgba.A) {
                            sbyte vr = (sbyte)(px.Rgba.R - pxPrev.Rgba.R);
                            sbyte vg = (sbyte)(px.Rgba.G - pxPrev.Rgba.G);
                            sbyte vb = (sbyte)(px.Rgba.B - pxPrev.Rgba.B);

                            sbyte vg_r = (sbyte)(vr - vg);
                            sbyte vg_b = (sbyte)(vb - vg);

                            if(vr > -3 && vr < 2 &&
                               vg > -3 && vg < 2 &&
                               vb > -3 && vb < 2) {
                                bytes[p++] = (byte)(QOI_OP_DIFF | (vr + 2) << 4 | (vg + 2) << 2 | (vb + 2));
                            } else if(vg_r > -9 && vg_r < 8 &&
                                      vg > -33 && vg < 32 &&
                                      vg_b > -9 && vg_b < 8) {
                                bytes[p++] = (byte)(QOI_OP_LUMA | (vg + 32));
                                bytes[p++] = (byte)((vg_r + 8) << 4 | (vg_b + 8));
                            } else {
                                bytes[p++] = QOI_OP_RGB;
                                bytes[p++] = px.Rgba.R;
                                bytes[p++] = px.Rgba.G;
                                bytes[p++] = px.Rgba.B;
                            }
                        } else {
                            bytes[p++] = QOI_OP_RGBA;
                            bytes[p++] = px.Rgba.R;
                            bytes[p++] = px.Rgba.G;
                            bytes[p++] = px.Rgba.B;
                            bytes[p++] = px.Rgba.A;
                        }

                    }
                }
                pxPrev = px;
            }

            for(int i = 0; i < qoiPaddingSize; i++) {
                bytes[p++] = qoiPadding[i];
            }

            return (bytes[0..p], description);
        }

        public static (byte[] Bytes, Description Description)? Decode(byte[] bytes) {
            int size = bytes.Length;
            if(size < QOI_HEADER_SIZE + qoiPaddingSize) return null;

            int p = 0;
            Description description = new();
            int header_magic = Read32(bytes, ref p);
            description.Width = (uint)Read32(bytes, ref p);
            description.Height = (uint)Read32(bytes, ref p);
            description.Channels = bytes[p++];
            description.ColorSpace = bytes[p++];

            if(description.Width == 0 || description.Height == 0 ||
               description.Channels < 3 || description.Channels > 4 ||
               description.ColorSpace > 1 ||
               header_magic != QOI_MAGIC ||
               description.Height >= QOI_PIXELS_MAX / description.Width) {
                return null;
            }

            int channels = description.Channels;
            int pxLen = (int)(description.Width * description.Height * channels);
            byte[] pixels = new byte[pxLen];
            QoiRgba[] index = new QoiRgba[64];
            QoiRgba px = new();
            px.Rgba.A = 255;

            int run = 0;
            int chunksLen = size - qoiPaddingSize;
            for(int pxPos = 0; pxPos < pxLen; pxPos += channels) {
                if(run > 0) {
                    run--;
                } else if(p < chunksLen) {
                    int b1 = bytes[p++];

                    if(b1 == QOI_OP_RGB) {
                        px.Rgba.R = bytes[p++];
                        px.Rgba.G = bytes[p++];
                        px.Rgba.B = bytes[p++];
                    } else if(b1 == QOI_OP_RGBA) {
                        px.Rgba.R = bytes[p++];
                        px.Rgba.G = bytes[p++];
                        px.Rgba.B = bytes[p++];
                        px.Rgba.A = bytes[p++];
                    } else if((b1 & QOI_MASK_2) == QOI_OP_INDEX) {
                        px = index[b1];
                    } else if((b1 & QOI_MASK_2) == QOI_OP_DIFF) {
                        px.Rgba.R += (byte)(((b1 >> 4) & 0x03) - 2);
                        px.Rgba.G += (byte)(((b1 >> 2) & 0x03) - 2);
                        px.Rgba.B += (byte)((b1 & 0x03) - 2);
                    } else if((b1 & QOI_MASK_2) == QOI_OP_LUMA) {
                        int b2 = bytes[p++];
                        int vg = (b1 & 0x3f) - 32;
                        px.Rgba.R += (byte)(vg - 8 + ((b2 >> 4) & 0x0f));
                        px.Rgba.G += (byte)vg;
                        px.Rgba.B += (byte)(vg - 8 + (b2 & 0x0f));
                    } else if((b1 & QOI_MASK_2) == QOI_OP_RUN) {
                        run = (b1 & 0x3f);
                    }

                    index[ColorHash(px.Rgba)] = px;
                }

                pixels[pxPos + 2] = px.Rgba.R;
                pixels[pxPos + 1] = px.Rgba.G;
                pixels[pxPos + 0] = px.Rgba.B;
                if(channels == 4) pixels[pxPos + 3] = px.Rgba.A;
            }

            return (pixels, description);
        }

        public static Bitmap? FromQoiFile(string fileName) {
            byte[] data = File.ReadAllBytes(fileName);
            var r = Decode(data);
            if(r == null) return null; // Throw Exception

            var b = r.Value.Bytes;
            var d = r.Value.Description;

            DirectBitmap dbmp = new((int)d.Width, (int)d.Height, d.Channels);
            Array.Copy(b, 0, dbmp.Bits, 0, b.Length);
            return dbmp.Bitmap;
        }

        public static void ToQoiFile(Bitmap bitmap, string fileName) {
            var r = Encode(bitmap);
            if(r == null) return; // Throw Exception

            File.WriteAllBytes(fileName, r.Value.Bytes);
        }

        private static byte ColorHash(QoiRgba.SRgba c) {
            return (byte)((c.R * 3 + c.G * 5 + c.B * 7 + c.A * 11) % 64);
        }

        private static int Read32(byte[] bytes, ref int p) {
            uint a = bytes[p++];
            uint b = bytes[p++];
            uint c = bytes[p++];
            uint d = bytes[p++];
            return (int)(a << 24 | b << 16 | c << 8 | d);
        }

        private static void Write32(byte[] bytes, ref int p, int v) {
            bytes[p++] = (byte)((0xff000000 & v) >> 24);
            bytes[p++] = (byte)((0x00ff0000 & v) >> 16);
            bytes[p++] = (byte)((0x0000ff00 & v) >> 8);
            bytes[p++] = (byte)(0x000000ff & v);
        }
    }
}