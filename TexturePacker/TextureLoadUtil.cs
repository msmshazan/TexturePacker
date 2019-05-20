using System.Runtime.InteropServices;

namespace TexturePacker
{
    public static class TextureLoadUtil
    {
        

       
        [DllImport("textureutil.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void LoadTexture(string filename , ref TexHandle Result);

        [DllImport("textureutil.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void OutTexture(string filename,ref TexHandle Output);

    }
}