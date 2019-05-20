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
            int optSize = 4096;
            int optPadding = 1;
            bool optXml = false;
            bool optBinary = false;
            bool optJson = false;
            bool optPremultiply = false;
            bool optTrim = false;
            bool optVerbose = false;
            bool optForce = false;
            bool optUnique = false;
            bool optRotate = false;

            if (args.Length < 3)
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
        [string] name
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
            [byte] img_rotated          (if --rotate enabled)");

            }
            else
            {

                /*
                TexHandle Result = new TexHandle();
                TextureLoadUtil.LoadTexture("ATLAS0.png" , ref Result);
                TextureLoadUtil.OutTexture("test.png ", ref Result);
                for (int i = 0; i < Result.Width * Result.Height; i++)
                {
                    uint pixel = ((uint*)Result.Data.ToPointer())[i];
                    Console.WriteLine($"pixel { i}: 0x{pixel:x8}");
                }
                */

                var OutputFileInfo = new FileInfo(args[1]);

                var InputDirectories = args[2].Split(',').Select(x => new DirectoryInfo(x)).ToList();


                for (int i = 3; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (arg == "-d" || arg == "--default")
                    {
                        optXml = optPremultiply = optTrim = optUnique = true;
                    }
                    else if (arg == "-x" || arg == "--xml")
                    {
                        optXml = true;
                    }
                    else if (arg == "-b" || arg == "--binary")
                    {
                        optBinary = true;
                    }
                    else if (arg == "-j" || arg == "--json")
                    {
                        optJson = true;
                    }
                    else if (arg == "-p" || arg == "--premultiply")
                    {
                        optPremultiply = true;
                    }
                    else if (arg == "-t" || arg == "--trim")
                    {
                        optTrim = true;
                    }
                    else if (arg == "-v" || arg == "--verbose")
                    {
                        optVerbose = true;
                    }
                    else if (arg == "-f" || arg == "--force")
                    {
                        optForce = true;
                    }
                    else if (arg == "-u" || arg == "--unique")
                    {
                        optUnique = true;
                    }
                    else if (arg == "-r" || arg == "--rotate")
                    {
                        optRotate = true;
                    }
                    else if (arg.Contains("--size"))
                    {
                        optSize = int.Parse(arg.Substring("--size".Length));
                    }
                    else if (arg.Contains("-s"))
                    {
                        optSize = int.Parse(arg.Substring("-s".Length));
                    }
                    else if (arg.Contains("--pad"))
                    {
                        optPadding = int.Parse(arg.Substring("--pad".Length));
                    }
                    else if (arg.Contains("-p"))
                    {
                        optPadding = int.Parse(arg.Substring("-p".Length));
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
                    if (File.Exists($"{OutputFileInfo.FullName}.hash") && !optForce)
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

                if (optVerbose)
                {



                }

                List<PackerBitmap> Bitmaps = new List<PackerBitmap>();
                List<Packer> Packers = new List<Packer>();

                foreach (var Directory in InputDirectories)
                {
                    foreach (var texfile in Directory.GetFiles("*.png"))
                    {
                        var texture = new TexHandle();
                        TextureLoadUtil.LoadTexture(texfile.FullName, ref texture);
                        Bitmaps.Add(new PackerBitmap(texture,texfile.Name,optPremultiply,optTrim));
                    }
                }

                Bitmaps.Sort();

                //Pack the bitmaps
                while (Bitmaps.Count > 0 )
                {
                    if (optVerbose)
                    {
                        // cout << "packing " << bitmaps.size() << " images..." << endl;
                    }
                    var packer = new Packer(optSize, optSize, optPadding);
                    packer.Pack(Bitmaps, optVerbose, optUnique, optRotate);
                    Packers.Add(packer);
                    if (optVerbose)
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
                    OutAtlas.Texture = new AtlasTexture();
                    OutAtlas.Texture.Name = OutputFileInfo.Name + i + ".png";
                    OutAtlas.Texture.Width = Packers[i].Width;
                    OutAtlas.Texture.Height = Packers[i].Height;
                    OutAtlas.Texture.Images = new List<AtlasImage>();
                    for (int t = 0; t < Packers[i].Bitmaps.Count; t++)
                    {
                        var Image = new AtlasImage();
                        Image.X = Packers[i].Points[t].x;
                        Image.Y = Packers[i].Points[t].y;
                        Image.Width = Packers[i].Bitmaps[t].Width;
                        Image.Height = Packers[i].Bitmaps[t].Height;
                        Image.Name = Packers[i].Bitmaps[t].Name;
                        OutAtlas.Texture.Images.Add(Image);
                    }
                    OutputAtlasData.Add(OutAtlas);

                }

                //Save the atlas image
                for (int i = 0; i < Packers.Count; ++i)
                {
                    if (optVerbose)
                    {
                     Console.WriteLine("writing png: "  +OutputFileInfo.Name + i + ".png");
                    }
                    Packers[i].SavePng(OutputFileInfo.FullName + i + ".png");
                }

                //Save the atlas binary
                if (optBinary)
                {
                    if (optVerbose)
                    {
                        Console.WriteLine("Saving binary: " + OutputFileInfo.Name + ".bin");
                    }
                    
                    for (int i = 0; i < Packers.Count; ++i)
                    {
                        //packers[i]->SaveBin(name + to_string(i), bin, optTrim, optRotate);
                    }
                        //bin.close();
                }

                //Save the atlas xml
                if (optXml)
                {
                    if (optVerbose)
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

                //Save the atlas json
                if (optJson)
                {
                    if (optVerbose)
                    {
                        Console.WriteLine("Saving json: " + OutputFileInfo.Name + ".json");
                    }

                    File.WriteAllText( OutputFileInfo.FullName +".json" , JsonConvert.SerializeObject(OutputAtlasData,Newtonsoft.Json.Formatting.Indented));
                   
                }

            }
        }
    }


    
    public partial class AtlasTexture
    {

       
        
        public string Name { get; set; }

        public int Width { get; set; }


        public int Height { get; set; }
        public List<AtlasImage> Images { get; set; }



    }


    public partial class Atlas
    {

        /// <remarks/>
        public AtlasTexture Texture { get; set; }

       

    }


    public partial class AtlasImage
    {

       
        public string Name { get; set; }

       
        public int X { get; set; }

        
        public int Y { get; set; }

       
        public int Width { get; set; }

        
        public int Height { get; set; }

        public int FrameX { get; set; }
        public int FrameY { get; set; }
        public int FrameW { get; set; }
        public int FrameH { get; set; }
    }


}