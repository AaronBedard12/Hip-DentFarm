using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelConverter
{
    public class DatasetCoohomClassesObj
    {
        public class Rootobject
        {
            public string id { get; set; }
            public Size size { get; set; }
            public Center center { get; set; }
            public bool customize { get; set; }
            public string cat_ids { get; set; }
            public string cat_id { get; set; }
            public string cat_name { get; set; }
            public string nyu_id { get; set; }
            public string nyu_name { get; set; }
        }

        public class Size
        {
            public float y { get; set; }
            public float x { get; set; }
            public float z { get; set; }
        }

        public class Center
        {
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
        }
    }

    public class DatasetCoohomClassesModel
    {

        public class Class1
        {
            public string group { get; set; }
            public string category { get; set; }
            public string type { get; set; }
            public int storey { get; set; }
            public string roomID { get; set; }
            public string species { get; set; }
        }

        public class Rootobject
        {
            public string _id { get; set; }
            public float height { get; set; }
            public Furniture[] furnitures { get; set; }
            public Wall[] walls { get; set; }
            public Corner[] corners { get; set; }
            public Room[] rooms { get; set; }
            public Hole[] holes { get; set; }
        }

        public class Furniture
        {
            public string id { get; set; }
            public string meshId { get; set; }
            public string matrixTransform { get; set; }
        }

        public class Wall
        {
            public string id { get; set; }
            public float[] heights { get; set; }
            public string[] cornerIds { get; set; }
        }

        public class Corner
        {
            public Position position { get; set; }
            public string id { get; set; }
        }

        public class Position
        {
            public float x { get; set; }
            public float y { get; set; }
        }

        public class Room
        {
            public string id { get; set; }
            public string[] wallIds { get; set; }
            public Position1 position { get; set; }
            public int type { get; set; }
        }

        public class Position1
        {
            public float x { get; set; }
            public float y { get; set; }
        }

        public class Hole
        {
            public string furnitureId { get; set; }
            public string id { get; set; }
            public string wallId { get; set; }
            public string hingeSide { get; set; }
            public Position2 position { get; set; }
            public string type { get; set; }
            public Size size { get; set; }
            public string openDirection { get; set; }
        }

        public class Position2
        {
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
        }

        public class Size
        {
            public float x { get; set; }
            public float y { get; set; }
        }
    }
}