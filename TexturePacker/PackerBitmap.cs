using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TexturePacker
{
    public struct PackerBitmap : IEquatable<PackerBitmap>, IComparable<PackerBitmap>
    {
        public IntPtr Data;
        public int Width;
        public int Height;
        public string Name;
        public int FrameX;
        public int FrameY;
        public int FrameW;
        public int FrameH;

        public unsafe PackerBitmap(TexHandle tex, string name, bool premultiply, bool trim)
        {
            Name = name;
            Width = (int)tex.Width;
            Height = (int)tex.Height;
            FrameX = 0;
            FrameY = 0;
            FrameW = 0;
            FrameH = 0;
            Data = tex.Data;
            uint* pixels = (uint *)Data.ToPointer();
            //Premultiply all the pixels by their alpha
            if (premultiply)
            {
                int count = (int)tex.Width * (int)tex.Height;
                for (int i = 0; i < count; ++i)
                {
                    uint c = pixels[i];
                    uint a = c >> 24;
                    float m = (a) / 255.0f;
                    uint r = (uint)((float)(c & 0xff) * m);
                    uint g = (uint)((float)((c >> 8) & 0xff) * m);
                    uint b = (uint)((float)((c >> 16) & 0xff) * m);
                    pixels[i] = (a << 24) | (b << 16) | (g << 8) | r;
                }
            }

            //TODO: skip if all corners contain opaque pixels?

            //Get pixel bounds
            int minX = (int)tex.Width - 1;
            int minY = (int)tex.Height - 1;
            int maxX = 0;
            int maxY = 0;
            if (trim)
            {
                for (int y = 0; y < tex.Height; ++y)
                {
                    for (int x = 0; x < tex.Width; ++x)
                    {
                        uint p = pixels[y * tex.Width + x];
                        if ((p >> 24) > 0)
                        {
                            minX = Math.Min(x, minX);
                            minY = Math.Min(y, minY);
                            maxX = Math.Max(x, maxX);
                            maxY = Math.Max(y, maxY);
                        }
                    }
                }
                if (maxX < minX || maxY < minY)
                {
                    minX = 0;
                    minY = 0;
                    maxX = (int)tex.Width - 1;
                    maxY = (int)tex.Height - 1;
                    Console.WriteLine("image is completely transparent " );
                }
            }
            else
            {
                minX = 0;
                minY = 0;
                maxX = (int)tex.Width - 1;
                maxY = (int)tex.Height - 1;
            }

            //Calculate our trimmed size
            Width = (maxX - minX) + 1;
            Height = (maxY - minY) + 1;
            FrameW = (int)tex.Width;
            FrameH = (int)tex.Height;

            if (Width == tex.Width && Height == tex.Height)
            {
                //If we aren't trimmed, use the loaded image data
                FrameX = 0;
                FrameY = 0;
            }
            else
            {
                //Create the trimmed image data
                var data = (uint *)(Marshal.AllocHGlobal(Width * Height*4));
                FrameX = -minX;
                FrameY = -minY;

                //Copy trimmed pixels over to the trimmed pixel array
                for (int y = minY; y <= maxY; ++y)
                    for (int x = minX; x <= maxX; ++x)
                        data[(y - minY) * Width + (x - minX)] = pixels[y * tex.Width + x];

                Data = new IntPtr(data);
            }

        }
        public PackerBitmap(int width, int height)
        {
            Name = "";
            Width = width;
            Height = height;
            FrameX = 0;
            FrameY = 0;
            FrameW = 0;
            FrameH = 0;
            Data = Marshal.AllocHGlobal(width*height);
        }

        public override bool Equals(object obj)
        {
            return obj is PackerBitmap bitmap && Equals(bitmap);
        }

        public bool Equals(PackerBitmap other)
        {
            return EqualityComparer<IntPtr>.Default.Equals(Data, other.Data) &&
                   Width == other.Width &&
                   Height == other.Height &&
                   Name == other.Name &&
                   FrameX == other.FrameX &&
                   FrameY == other.FrameY &&
                   FrameW == other.FrameW &&
                   FrameH == other.FrameH;
        }

        public override int GetHashCode()
        {
            var hashCode = -84555632;
            hashCode = hashCode * -1521134295 + EqualityComparer<IntPtr>.Default.GetHashCode(Data);
            hashCode = hashCode * -1521134295 + Width.GetHashCode();
            hashCode = hashCode * -1521134295 + Height.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + FrameX.GetHashCode();
            hashCode = hashCode * -1521134295 + FrameY.GetHashCode();
            hashCode = hashCode * -1521134295 + FrameW.GetHashCode();
            hashCode = hashCode * -1521134295 + FrameH.GetHashCode();
            return hashCode;
        }

        public int CompareTo(PackerBitmap other)
        {
            var AreaX = (Width * Height);
            var AreaY = (other.Width * other.Height);
            if (AreaX < AreaY)
            {
                return 1;
            }
            if (AreaX > AreaY)
            {
                return -1;
            }
            return 0;
        }

        public static bool operator ==(PackerBitmap left, PackerBitmap right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PackerBitmap left, PackerBitmap right)
        {
            return !(left == right);
        }
    }


}