using DbmsApi.API;
using MathPackage;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ModelConverter.Dataset3DFRONTClasses;

namespace ModelConverter
{
    public static class DatasetConverter3DFRONT
    {
        // Models

        public static Model Convert3DFRONTModel(string path, double scale, bool flipTriangles, bool flipYZ)
        {
            Rootobject inputModel = null;
            using (StreamReader r = new StreamReader(path))
            {
                string json = r.ReadToEnd();
                inputModel = JsonConvert.DeserializeObject<Rootobject>(json);
            }

            Model newModel = new Model()
            {
                Id = inputModel.uid,
                Name = inputModel.uid,
                ModelObjects = new List<ModelObject>(),
                Properties = new DbmsApi.API.Properties(),
                Relations = new List<Relation>(),
                Tags = new List<KeyValuePair<string, string>>()
            };

            Dictionary<string, ModelObject> modelObjects = GetModelObjects(inputModel, flipTriangles, flipYZ);
            List<ModelCatalogObject> modelCatalogObj = GetCatalogObjects(inputModel);
            foreach (ModelCatalogObject mo in modelCatalogObj)
            {
                if (modelObjects.TryGetValue(mo.Id, out ModelObject existingMO))
                {
                    int i = 0; // Something weird here...
                }
                else
                {
                    modelObjects.Add(mo.Id, mo);
                }
            }

            // For each object, find the room it is referenced in and its location in that room
            foreach (Room r in inputModel.scene.room)
            {
                foreach (Child c in r.children)
                {
                    if (modelObjects.ContainsKey(c._ref))
                    {
                        ModelObject objOfInterest = modelObjects[c._ref];
                        Vector3D loc;
                        Vector4D orient;
                        if (flipYZ)
                        {
                            loc = new Vector3D(c.pos[0], c.pos[2], c.pos[1]);
                            orient = new Vector4D(c.rot[0], c.rot[2], c.rot[1], c.rot[3]);
                        }
                        else
                        {
                            loc = new Vector3D(c.pos[0], c.pos[1], c.pos[2]);
                            orient = new Vector4D(c.rot[0], c.rot[1], c.rot[2], c.rot[3]);
                        }

                        if (objOfInterest.GetType() == typeof(ModelCatalogObject))
                        {
                            objOfInterest.Location = loc;
                        }
                        else
                        {
                            objOfInterest.Location = ConverterGeneral.CenterObject(objOfInterest.Components, scale, orient);
                        }
                        objOfInterest.Orientation = orient;
                    }
                }
            }

            newModel.ModelObjects = CombineModelObjects(modelObjects);
            newModel.Tags.Add(new KeyValuePair<string, string>("Dataset", ConverterGeneral.Datasets._3DFRONT.ToString()));

            return newModel;
        }

        public static Dictionary<string, ModelObject> GetModelObjects(Rootobject rootobject, bool flipTriangles, bool flipYZ)
        {
            Dictionary<string, ModelObject> objectList = new Dictionary<string, ModelObject>();
            foreach (Dataset3DFRONTClasses.Mesh m in rootobject.mesh)
            {
                Component newComponent = new Component()
                {
                    Vertices = ConvertArayToVectList(m.xyz, flipYZ),
                    Triangles = ConvertArayToTriangleList(m.faces.Select(f => (int)f).ToArray(), flipTriangles),
                    MaterialId = m.material
                };

                ModelObject modelObject = new ModelObject()
                {
                    Id = m.uid,
                    TypeId = m.type,
                    Components = new List<Component>() { newComponent },
                    Properties = new DbmsApi.API.Properties(),
                    Tags = new List<KeyValuePair<string, string>>(),
                    Name = m.constructid
                };

                objectList.Add(modelObject.Id, modelObject);
            }

            return objectList;
        }

        public static List<ModelObject> CombineModelObjects(Dictionary<string, ModelObject> modelObjects)
        {
            Dictionary<string, ModelObject> combinedObjects = new Dictionary<string, ModelObject>();
            foreach (ModelObject modelObj in modelObjects.Values)
            {
                if (modelObj.GetType() == typeof(ModelCatalogObject))
                {
                    combinedObjects.Add(modelObj.Id, modelObj);
                    continue;
                }

                if (combinedObjects.ContainsKey(modelObj.Name))
                {
                    ModelObject currentObj = combinedObjects[modelObj.Name];

                    CombineComponents(ref currentObj, modelObj.Components.First(), modelObj.Location, modelObj.Orientation);
                    currentObj.TypeId += "_" + modelObj.TypeId;
                }
                else
                {
                    combinedObjects.Add(modelObj.Name, new ModelObject()
                    {
                        Id = modelObj.Name,
                        Name = modelObj.Name,
                        Components = modelObj.Components,
                        Location = modelObj.Location,
                        Orientation = modelObj.Orientation,
                        Properties = modelObj.Properties,
                        Tags = modelObj.Tags,
                        TypeId = modelObj.TypeId
                    });
                }
            }

            return combinedObjects.Values.ToList();
        }

        public static void CombineComponents(ref ModelObject mainObject, Component component, Vector3D cLoc, Vector4D cOrient)
        {
            List<Vector3D> allVects = new List<Vector3D>();
            foreach (Component comp in mainObject.Components)
            {
                Matrix4 translationMatrix1 = Utils.GetTranslationMatrixFromLocationOrientation(mainObject.Location, mainObject.Orientation);
                List<Vector3D> globalVerticies1 = comp.Vertices.Select(v => Matrix4.Multiply(translationMatrix1, v.Get4D(1.0)).Get3D()).ToList();
                allVects.AddRange(globalVerticies1);
            }

            Matrix4 translationMatrix2 = Utils.GetTranslationMatrixFromLocationOrientation(cLoc, cOrient);
            List<Vector3D> globalVerticies2 = component.Vertices.Select(v => Matrix4.Multiply(translationMatrix2, v.Get4D(1.0)).Get3D()).ToList();
            allVects.AddRange(globalVerticies2);

            double minX = allVects.Min(v => v.x);
            double minY = allVects.Min(v => v.y);
            double minZ = allVects.Min(v => v.z);
            double maxX = allVects.Max(v => v.x);
            double maxY = allVects.Max(v => v.y);
            double maxZ = allVects.Max(v => v.z);
            Vector3D newLoc = new Vector3D((minX + maxX) / 2.0, (minY + maxY) / 2.0, (minZ + maxZ) / 2.0);
            Vector4D newOrient = new Vector4D(0, 0, 0, 1);

            Matrix4 translationMatrix3 = Utils.GetTranslationMatrixFromLocationOrientation(newLoc, newOrient);
            Matrix4 translationMatrix4 = translationMatrix3.GetInverse(out bool hasInvers);
            if (hasInvers)
            {
                foreach (Component comp in mainObject.Components)
                {
                    Matrix4 translationMatrix1 = Utils.GetTranslationMatrixFromLocationOrientation(mainObject.Location, mainObject.Orientation);
                    comp.Vertices = comp.Vertices.Select(v => Matrix4.Multiply(translationMatrix1, v.Get4D(1.0)).Get3D()).ToList();
                    comp.Vertices = comp.Vertices.Select(v => Matrix4.Multiply(translationMatrix4, v.Get4D(1.0)).Get3D()).ToList();
                }
                component.Vertices = globalVerticies2.Select(v => Matrix4.Multiply(translationMatrix4, v.Get4D(1.0)).Get3D()).ToList();
            }

            mainObject.Components.Add(component);
            mainObject.Location = newLoc;
            mainObject.Orientation = newOrient;
        }

        public static List<Vector3D> ConvertArayToVectList(float[] array, bool flipYZ)
        {
            List<Vector3D> returnList = new List<Vector3D>();
            for (int i = 0; i < array.Length; i += 3)
            {
                Vector3D newVect;
                if (flipYZ)
                {
                    newVect = new Vector3D(array[i], array[i + 2], array[i + 1]);
                }
                else
                {
                    newVect = new Vector3D(array[i], array[i + 1], array[i + 2]);
                }
                returnList.Add(newVect);
            }
            return returnList;
        }

        public static List<int[]> ConvertArayToTriangleList(int[] array, bool flipTriangles)
        {
            List<int[]> returnList = new List<int[]>();
            for (int i = 0; i < array.Length; i += 3)
            {
                int[] newTri = new int[3];
                if (flipTriangles)
                {
                    newTri = new int[3] { array[i], array[i + 2], array[i + 1] };
                }
                else
                {
                    newTri = new int[3] { array[i], array[i + 1], array[i + 2] };
                }
                returnList.Add(newTri);
            }
            return returnList;
        }

        public static List<ModelCatalogObject> GetCatalogObjects(Rootobject rootobject)
        {
            List<ModelCatalogObject> objectList = new List<ModelCatalogObject>();
            foreach (Furniture f in rootobject.furniture)
            {
                if (!f.valid)
                {
                    continue;
                }
                ModelCatalogObject mco = new ModelCatalogObject()
                {
                    Id = f.uid,
                    CatalogId = f.jid,
                    Name = f.title,
                    Components = new List<Component>(),
                    Properties = new DbmsApi.API.Properties(),
                    TypeId = "N/A",
                    Tags = new List<KeyValuePair<string, string>>()
                };
                objectList.Add(mco);
            }

            return objectList;
        }

        // Objects

        public static CatalogObject Convert3DFRONTCatalogObject(string path, double scale, bool flipTriangles, bool flipYZ)
        {
            string id = Path.GetFileNameWithoutExtension(path);
            FileStream fileStream = new FileStream(path + "\\raw_model.obj", FileMode.Open);
            CatalogObject catalogObject = ObjConverter.ConvertObjFile(fileStream, id, scale, flipTriangles, flipYZ);
            fileStream.Close();
            catalogObject.CatalogID = id;
            catalogObject.Tags.Add(new KeyValuePair<string, string>("Dataset", ConverterGeneral.Datasets._3DFRONT.ToString()));
            return catalogObject;
        }
    }
}
