using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;

namespace TexturePacker
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct TexHandle : IEquatable<TexHandle> ,IComparable<TexHandle>
    {
        public IntPtr Data;
        public uint Width;
        public uint Height;


        public int CompareTo(TexHandle other)
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

        public override bool Equals(object obj)
        {
            return obj is TexHandle handle && Equals(handle);
        }

        public bool Equals(TexHandle other)
        {
            return EqualityComparer<IntPtr>.Default.Equals(Data, other.Data) &&
                   Width == other.Width &&
                   Height == other.Height;
        }

        public override int GetHashCode()
        {
            var hashCode = 162180750;
            hashCode = hashCode * -1521134295 + EqualityComparer<IntPtr>.Default.GetHashCode(Data);
            hashCode = hashCode * -1521134295 + Width.GetHashCode();
            hashCode = hashCode * -1521134295 + Height.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(TexHandle left, TexHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TexHandle left, TexHandle right)
        {
            return !(left == right);
        }
    }
}