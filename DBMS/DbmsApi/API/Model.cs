using DbmsApi.Mongo;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbmsApi.API
{
    public class Model : MongoDocument
    {
        public string Name;
        public List<ModelObject> ModelObjects = new List<ModelObject>();
        public List<Relation> Relations = new List<Relation>();
        public Properties Properties;
        public List<KeyValuePair<string, string>> Tags = new List<KeyValuePair<string, string>>();

        public Model() { }

        [JsonConstructor]
        private Model(string id, string name, Properties properties, List<ModelCatalogObject> modelObjects, List<Relation> relations, List<KeyValuePair<string, string>> tags)
        {
            Id = id;
            Name = name;
            Properties = properties;
            ModelObjects = modelObjects.Where(mco => string.IsNullOrWhiteSpace(mco.CatalogId)).Select(mco => ModelObjectConverter(mco)).ToList();
            ModelObjects.AddRange(modelObjects.Where(mco => !string.IsNullOrWhiteSpace(mco.CatalogId)).ToList());
            Relations = relations;
            Tags = tags;
        }

        protected ModelObject ModelObjectConverter(ModelCatalogObject mco)
        {
            return new ModelObject() {
                Components = mco.Components ,
                Id = mco.Id,
                Location = mco.Location,
                Name = mco.Name,
                Orientation = mco.Orientation,
                Properties = mco.Properties,
                Tags = mco.Tags,
                TypeId = mco.TypeId
            };
        }

        public void ReadFromMogoModel(MongoModel mongoModel, List<ModelObjectSplit> modelObjectSplits, List<CatalogObject> catalogObjects)
        {
            this.Id = mongoModel.Id;
            this.Name = mongoModel.Name;
            this.Properties = mongoModel.Properties;
            this.ModelObjects = mongoModel.CatalogObjectReferences.Select(mco => CatalogObjectCombiner(mco, catalogObjects)).Where(res => res != null).ToList();
            this.ModelObjects.AddRange(mongoModel.ModelObjectReferences.Select(mco => ModelObjectCombiner(mco, modelObjectSplits)).Where(res => res != null).ToList());
            this.ModelObjects.AddRange(mongoModel.ModelObjects);
            this.Relations = mongoModel.Relations;
            this.Tags = mongoModel.Tags;
        }

        private ModelObject CatalogObjectCombiner(CatalogObjectReference cmoRef, List<CatalogObject> catalogObjects)
        {
            CatalogObject catObj = catalogObjects.Find(co => co.CatalogID == cmoRef.CatalogId);
            if (catObj == null)
            {
                return null;
            }
            ModelCatalogObject catalogObject = new ModelCatalogObject()
            {
                Id = cmoRef.Id,
                CatalogId = cmoRef.CatalogId,
                Location = cmoRef.Location,
                Orientation = cmoRef.Orientation,

                Tags = cmoRef.Tags,
                Components = catObj.Components,
                Name = catObj.Name,
                Properties = catObj.Properties,
                TypeId = catObj.TypeId
            };

            return catalogObject;
        }

        private ModelObject ModelObjectCombiner(ModelObjectReference moRef, List<ModelObjectSplit> modelObjectSplits)
        {
            ModelObjectSplit splitObj = modelObjectSplits.Find(mos => mos.RefId == moRef.ModelRefId);
            if (splitObj == null)
            {
                return null;
            }
            ModelObject newMO = new ModelObject()
            {
                Id = moRef.Id,
                Name = moRef.Name,
                TypeId = moRef.TypeId,
                Location = moRef.Location,
                Orientation = moRef.Orientation,
                Properties = moRef.Properties,
                Tags = moRef.Tags,
                Components = splitObj == null ? new List<Component>() : splitObj.Components
            };

            return newMO;
        }

        //public Model Copy()
        //{
        //    Model newModel = new Model()
        //    {
        //        Name = Name,
        //        ModelObjects = ModelObjects.Select(m => m.Copy()).ToList(),
        //        Relations = Relations.Select(r => r.Copy()).ToList(),
        //        Properties = Properties.Copy(),
        //        Tags = Tags.Select(t => new KeyValuePair<string, string>(t.Key, t.Value)).ToList()
        //    };
        //}
    }
}
