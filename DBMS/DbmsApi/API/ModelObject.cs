using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathPackage;
using MongoDB.Bson.Serialization.Attributes;

namespace DbmsApi.API
{
    public abstract class BaseObject
    {
        public string Id; // This is the ID within the model, not the catalogID
        public Vector3D Location;
        public Vector4D Orientation;
        public Properties Properties;
        public List<KeyValuePair<string, string>> Tags = new List<KeyValuePair<string, string>>();
    }

    public class ModelObject : BaseObject
    {
        public string Name;
        public string TypeId;

        public List<Component> Components = new List<Component>();
    }

    public class ModelCatalogObject : ModelObject
    {
        public string CatalogId;
    }

    public class ModelObjectReference : BaseObject
    {
        [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
        public string ModelRefId;

        public string Name;
        public string TypeId;
    }

    public class ModelObjectSplit
    {
        public string RefId;
        public List<Component> Components = new List<Component>();
    }
}