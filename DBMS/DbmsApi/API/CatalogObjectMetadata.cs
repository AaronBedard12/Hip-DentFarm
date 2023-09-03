using DbmsApi.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace DbmsApi.API
{
    public class CatalogObjectMetadata
    {
        [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
        public string CatalogObjectId;

        public string Name;
        public string Type;
        public Properties Properties;
        public List<KeyValuePair<string, string>> Tags = new List<KeyValuePair<string, string>>();

        [JsonConstructor]
        [BsonConstructor]
        public CatalogObjectMetadata() { }

        public CatalogObjectMetadata(MongoCatalogObject catalogObject)
        {
            CatalogObjectId = catalogObject.Id;
            Name = catalogObject.Name;
            Properties = catalogObject.Properties;
            Type = catalogObject.TypeId;
            Tags = catalogObject.Tags;
        }

        public override string ToString()
        {
            string typeString = Type == null ? "N/A" : Type.ToString();
            return Name + " (" + typeString + ")";
        }
    }
}