using DbmsApi.API;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DbmsApi.Mongo
{
    public class MongoModel : Model
    {
        public List<CatalogObjectReference> CatalogObjectReferences = new List<CatalogObjectReference>();
        public List<ModelObjectReference> ModelObjectReferences = new List<ModelObjectReference>();

        [JsonConstructor]
        private MongoModel(string id, string name, Properties properties, List<ModelCatalogObject> modelObjects, List<Relation> relations, List<KeyValuePair<string, string>> tags, List<CatalogObjectReference> catalogObjectReferences, List<ModelObjectReference> modelObjectReferences)
        {
            Id = id;
            Name = name;
            Properties = properties;
            ModelObjects = modelObjects.Where(mco => string.IsNullOrWhiteSpace(mco.CatalogId)).Select(mco => this.ModelObjectConverter(mco)).ToList();
            ModelObjects.AddRange(modelObjects.Where(mco => !string.IsNullOrWhiteSpace(mco.CatalogId)).ToList());
            Relations = relations;
            Tags = tags;
            this.CatalogObjectReferences = catalogObjectReferences;
            this.ModelObjectReferences = modelObjectReferences;
        }

        public MongoModel(Model model, string userName)
        {
            List<CatalogObjectReference> catalogObjs = new List<CatalogObjectReference>();
            List<ModelObjectReference> nonCatalogObjs = new List<ModelObjectReference>();
            List<ModelObject> nonCatalogMOs = new List<ModelObject>();
            foreach (ModelObject mo in model.ModelObjects)
            {
                if (mo.GetType() == typeof(ModelCatalogObject))
                {
                    ModelCatalogObject mco = mo as ModelCatalogObject;
                    catalogObjs.Add(new CatalogObjectReference()
                    {
                        CatalogId = mco.CatalogId,
                        Id = mo.Id,
                        Location = mo.Location,
                        Orientation = mo.Orientation,
                        Tags = mo.Tags
                    });
                }
                else
                {
                    nonCatalogObjs.Add(new ModelObjectReference()
                    {
                        ModelRefId = Guid.NewGuid().ToString(),
                        Id = mo.Id,
                        Location = mo.Location,
                        Orientation = mo.Orientation,
                        Tags = mo.Tags,
                        Properties = mo.Properties,
                        Name = mo.Name,
                        TypeId = mo.TypeId
                    });
                    nonCatalogMOs.Add(mo);
                }
            }

            Id = model.Id;
            Name = model.Name;
            Tags = model.Tags;
            Properties = model.Properties;
            Relations = model.Relations;
            CatalogObjectReferences = catalogObjs;
            ModelObjectReferences = nonCatalogObjs;
            ModelObjects = nonCatalogMOs;
        }

        public List<ModelObjectSplit> SplitModelObjectFromModel()
        {
            List<ModelObjectSplit> moSplits = new List<ModelObjectSplit>();
            foreach (ModelObjectReference mor in ModelObjectReferences)
            {
                ModelObject moForRef = ModelObjects.Find(mo => mo.Id == mor.Id);
                moSplits.Add(new ModelObjectSplit()
                {
                    RefId = mor.ModelRefId,
                    Components = moForRef.Components,
                });
            }

            ModelObjects = new List<ModelObject>();
            return moSplits;
        }
    }
}