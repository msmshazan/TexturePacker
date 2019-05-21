using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security;
using System.Xml;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TexturePacker
{
    class Program
    {
        static unsafe void Main(string[] args)
        {

            int ValidateAtlasSize(int atlassize)
            {
                if (atlassize > 4096)
                {
                    atlassize = 4096;
                }
                else if (atlassize > 2048)
                {
                    atlassize = 2048;
                }
                else if (atlassize > 1024)
                {
                    atlassize = 1024;
                }
                else if (atlassize > 512)
                {
                    atlassize = 512;
                }
                else
                {
                    atlassize = 256;
                }

                return atlassize;
            }

            int AtlasSize = 4096;
            int PaddingBetweenImages = 1;
            bool OutputXML = false;
            bool OutputBinary = false;
            bool OutputJson = false;
            bool EnablePremultiply = false;
            bool EnableTrimming = false;
            bool VerboseOutput = false;
            bool ForcePack = false;
            bool CheckUnique = false;
            bool CheckRotate = false;

            if (args.Length < 4)
            {
                Console.WriteLine(@"
TexturePacker - command line texture packer
 ====================================
 
 usage:
    TexturePacker [OUTPUT] [INPUT1,INPUT2,INPUT3...] [OPTIONS...]
 
 example:
    TexturePacker bin/atlases/atlas assets/characters,assets/tiles -p -t -v -u -r
 
 options:
    -d  --default           use default settings (-x -p -t -u)
    -x  --xml               saves the atlas data as a .xml file
    -b  --binary            saves the atlas data as a .bin file
    -j  --json              saves the atlas data as a .json file
    -p  --premultiply       premultiplies the pixels of the bitmaps by their alpha channel
    -t  --trim              trims excess transparency off the bitmaps
    -v  --verbose           print to the debug console as the packer works
    -f  --force             ignore the hash, forcing the packer to repack
    -u  --unique            remove duplicate bitmaps from the atlas
    -r  --rotate            enabled rotating bitmaps 90 degrees clockwise when packing
    -s# --size#             max atlas size (# can be 4096, 2048, 1024, 512, 256, 128, or 64)
    -p# --pad#              padding between images (# can be from 0 to 16)
 
      binary format:
                    [int16] num_textures (below block is repeated this many times)
                    [byte] img_rotated          
                    [byte] img_trimmed          
                    [byte] img_premultiplied         
                        [string] name
                        [int16] atlas_width
                        [int16] atlas_height
                        [int16] num_images (below block is repeated this many times)
                            [string] img_name
                            [int16] img_x
                            [int16] img_y
                            [int16] img_width
                            [int16] img_height
                            [int16] img_frame_x         (if --trim enabled)
                            [int16] img_frame_y         (if --trim enabled)
                            [int16] img_frame_width     (if --trim enabled)
                            [int16] img_frame_height    (if --trim enabled)
");
            }
            else
            {
                var OutputFileInfo = new FileInfo(args[0]);

                var InputDirectories = args[1].Split(',').Select(x => new DirectoryInfo(Path.Combine( Directory.GetCurrentDirectory(), x))).ToList();

                for (int i = 2; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (arg == "-d" || arg == "--default")
                    {
                        OutputXML = EnablePremultiply = EnableTrimming = CheckUnique = true;
                    }
                    else if (arg == "-x" || arg == "--xml")
                    {
                        OutputXML = true;
                    }
                    else if (arg == "-b" || arg == "--binary")
                    {
                        OutputBinary = true;
                    }
                    else if (arg == "-j" || arg == "--json")
                    {
                        OutputJson = true;
                    }
                    else if (arg == "-p" || arg == "--premultiply")
                    {
                        EnablePremultiply = true;
                    }
                    else if (arg == "-t" || arg == "--trim")
                    {
                        EnableTrimming = true;
                    }
                    else if (arg == "-v" || arg == "--verbose")
                    {
                        VerboseOutput = true;
                    }
                    else if (arg == "-f" || arg == "--force")
                    {
                        ForcePack = true;
                    }
                    else if (arg == "-u" || arg == "--unique")
                    {
                        CheckUnique = true;
                    }
                    else if (arg == "-r" || arg == "--rotate")
                    {
                        CheckRotate = true;
                    }
                    else if (arg.Contains("--size"))
                    {
                        AtlasSize = ValidateAtlasSize(int.Parse(arg.Substring("--size".Length)));
                    }
                    else if (arg.Contains("-s"))
                    {
                        AtlasSize = ValidateAtlasSize(int.Parse(arg.Substring("-s".Length)));
                    }
                    else if (arg.Contains("--pad"))
                    {
                        PaddingBetweenImages = int.Parse(arg.Substring("--pad".Length));
                    }
                    else if (arg.Contains("-p"))
                    {
                        PaddingBetweenImages = int.Parse(arg.Substring("-p".Length));
                    }
                    else
                    {
                        Console.WriteLine($"unexpected argument: {arg}");
                    }
                }


                int NewHash = 0;

                for (int i = 0; i < args.Length; i++)
                {
                    NewHash += args[i].GetHashCode();
                }

                for (int i = 0; i < InputDirectories.Count; i++)
                {
                    NewHash += InputDirectories[i].GetHashCode();
                }

                if (OutputFileInfo.Directory.Exists)
                {
                    if (File.Exists($"{OutputFileInfo.FullName}.hash") && !ForcePack)
                    {
                        int oldhash = int.Parse(File.ReadAllText($"{OutputFileInfo.FullName}.hash"));
                        if (oldhash != NewHash)
                        {
                            File.WriteAllText($"{OutputFileInfo.FullName}.hash", NewHash.ToString());
                        }
                    }
                    else
                    {
                        File.WriteAllText($"{OutputFileInfo.FullName}.hash", NewHash.ToString());
                    }
                }
                else
                {
                    OutputFileInfo.Directory.Create();
                    File.WriteAllText($"{OutputFileInfo.FullName}.hash", NewHash.ToString());
                }

                if (VerboseOutput)
                {

                    Console.WriteLine("Reading all pngs ");

                }

                List<PackerBitmap> Bitmaps = new List<PackerBitmap>();
                List<Packer> Packers = new List<Packer>();

                foreach (var Directory in InputDirectories)
                {
                    foreach (var texfile in Directory.GetFiles("*.png"))
                    {
                        var texture = new TexHandle();
                        TextureLoadUtil.LoadTexture(texfile.FullName, ref texture);
                        Bitmaps.Add(new PackerBitmap(texture,texfile.Name,EnablePremultiply,EnableTrimming));
                    }
                }

                Bitmaps.Sort();

                while (Bitmaps.Count > 0 )
                {
                    if (VerboseOutput)
                    {
                        Console.WriteLine("packing " + Bitmaps.Count +" images..." );
                    }
                    var packer = new Packer(AtlasSize, AtlasSize, PaddingBetweenImages);
                    packer.Pack(Bitmaps, VerboseOutput, CheckUnique, CheckRotate);
                    Packers.Add(packer);
                    if (VerboseOutput)
                    {
                      Console.WriteLine("finished packing: "+  Packers.Count + " (" + packer.Width + " x " + packer.Height + ')' );
                    }
                    if (packer.Bitmaps.Count <= 0)
                    {
                        Console.WriteLine( "packing failed, could not fit any bitmap " );
                        return;
                    }
                }

                var OutputAtlasData = new List<Atlas>();

                for (int i = 0; i < Packers.Count; i++)
                {
                    var OutAtlas = new Atlas();
                    OutAtlas.Name = OutputFileInfo.Name + i + ".png";
                    OutAtlas.Width = Packers[i].Width;
                    OutAtlas.Height = Packers[i].Height;
                    OutAtlas.IsRotated = CheckRotate;
                    OutAtlas.IsTrimmed = EnableTrimming;
                    OutAtlas.ISPremultiplied = EnablePremultiply;
                    OutAtlas.Images = new List<AtlasImage>();
                    for (int t = 0; t < Packers[i].Bitmaps.Count; t++)
                    {
                        var Image = new AtlasImage();
                        Image.Name = Packers[i].Bitmaps[t].Name;
                        Image.X = Packers[i].Points[t].x;
                        Image.Y = Packers[i].Points[t].y;
                        Image.Width = Packers[i].Bitmaps[t].Width;
                        Image.Height = Packers[i].Bitmaps[t].Height;
                        Image.FrameX = Packers[i].Bitmaps[t].FrameX;
                        Image.FrameY = Packers[i].Bitmaps[t].FrameY;
                        Image.FrameW = Packers[i].Bitmaps[t].FrameW;
                        Image.FrameH = Packers[i].Bitmaps[t].FrameH;
                        OutAtlas.Images.Add(Image);
                    }
                    OutputAtlasData.Add(OutAtlas);

                }

                
                for (int i = 0; i < Packers.Count; ++i)
                {
                    if (VerboseOutput)
                    {
                        Console.WriteLine("writing png: "+OutputFileInfo.Name + i + ".png");
                    }
                    Packers[i].SavePng(OutputFileInfo.FullName + i + ".png");
                }

                
                if (OutputBinary)
                {
                    if (VerboseOutput)
                    {
                        Console.WriteLine("Saving binary: " + OutputFileInfo.Name + ".bin");
                    }

                    var FStream =  File.OpenWrite(OutputFileInfo.FullName + ".bin");
                    using (var stringwriter = new BinaryWriter(FStream))
                    {

                        stringwriter.Write((Int16)OutputAtlasData.Count);
                        stringwriter.Write((byte) (CheckRotate ? 0: 1));
                        stringwriter.Write((byte) (EnableTrimming ? 0: 1));
                        stringwriter.Write((byte) (EnablePremultiply ? 0: 1));
                        for (int i = 0; i < OutputAtlasData.Count; ++i)
                        {
                            stringwriter.Write(OutputAtlasData[i].Name);
                            stringwriter.Write((Int16)OutputAtlasData[i].Width);
                            stringwriter.Write((Int16)OutputAtlasData[i].Height);
                            stringwriter.Write((Int16)OutputAtlasData[i].Images.Count);
                            for (int t = 0; t < OutputAtlasData[i].Images.Count; t++)
                            {
                                stringwriter.Write(OutputAtlasData[i].Images[t].Name);
                                stringwriter.Write((Int16)OutputAtlasData[i].Images[t].X);
                                stringwriter.Write((Int16)OutputAtlasData[i].Images[t].Y);
                                stringwriter.Write((Int16)OutputAtlasData[i].Images[t].Width);
                                stringwriter.Write((Int16)OutputAtlasData[i].Images[t].Height);
                                stringwriter.Write((Int16)OutputAtlasData[i].Images[t].FrameX);
                                stringwriter.Write((Int16)OutputAtlasData[i].Images[t].FrameY);
                                stringwriter.Write((Int16)OutputAtlasData[i].Images[t].FrameW);
                                stringwriter.Write((Int16)OutputAtlasData[i].Images[t].FrameH);

                            }
                        }
                    }
                    FStream.Close();

                }

                
                if (OutputXML)
                {
                    if (VerboseOutput)
                    {
                        Console.WriteLine("Saving xml: " + OutputFileInfo.Name + ".xml");

                    }

                    using (var stringwriter = new StringWriter())
                    {
                        var serializer = new XmlSerializer(OutputAtlasData.GetType());
                        serializer.Serialize(stringwriter,OutputAtlasData);
                        File.WriteAllText(OutputFileInfo.FullName + ".xml", stringwriter.ToString());
                    }

                }

                
                if (OutputJson)
                {
                    if (VerboseOutput)
                    {
                        Console.WriteLine("Saving json: " + OutputFileInfo.Name + ".json");
                    }

                    File.WriteAllText( OutputFileInfo.FullName +".json" , JsonConvert.SerializeObject(OutputAtlasData,Newtonsoft.Json.Formatting.Indented));
                   
                }

            }
        }

       
    }
}