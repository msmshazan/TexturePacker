using System.Collections.Generic;
using Newtonsoft.Json;
namespace TexturePacker
{

    [JsonObject("Atlas")]
    public partial class Atlas
    {

        public string Name { get; set; }

        public int Width { get; set; }


        public int Height { get; set; }


        public List<AtlasImage> Images { get; set; }


        public bool IsRotated { get; set; }
        public bool IsTrimmed { get; set; }
        public bool IsPremultiplied { get; set; }
    }


}