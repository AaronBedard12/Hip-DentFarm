using DbmsApi.API;
using MathPackage;
using Newtonsoft.Json;
using ObjLoader.Loader.Data.VertexData;
using ObjLoader.Loader.Loaders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ModelConverter.DatasetCoohomClassesModel;
using static ModelConverter.DatasetCoohomClassesObj;

namespace ModelConverter
{
    public class DatasetConverterCoohom
    {
        // Models

        public static Model ConvertCoohomModel(string path, double scale, bool flipTriangles, bool flipYZ)
        {
            string id = Path.GetFileNameWithoutExtension(path);
            FileStream fileStream = new FileStream(path + "\\floorplan.obj", FileMode.Open);
            Model model = GetModelFromObj(fileStream, path, id, scale, flipTriangles, flipYZ);
            fileStream.Close();

            DatasetCoohomClassesModel.Rootobject inputInfo;
            DirectoryInfo taskDirectory = new DirectoryInfo(path);
            FileInfo[] taskFiles = taskDirectory.GetFiles("scene_meta_L*.json");
            using (StreamReader r = new StreamReader(taskFiles.First().FullName))
            {
                string json = r.ReadToEnd();
                inputInfo = JsonConvert.DeserializeObject<DatasetCoohomClassesModel.Rootobject>(json);
            }

            List<ModelCatalogObject> modelCatalogObjects = new List<ModelCatalogObject>();
            foreach (var furniture in inputInfo.furnitures)
            {
                Matrix4 translateMatrix = new Matrix4(furniture.matrixTransform.Split(',').Select(v => Convert.ToDouble(v)).ToArray());
                Utils.GetLocationAndOrientationFromTranslationMatrix(translateMatrix, out Vector3D loc, out Vector4D orient);
                ModelCatalogObject mco = new ModelCatalogObject()
                {
                    CatalogId = furniture.meshId,
                    Id = furniture.id,
                    Name = furniture.meshId,
                    Components = new List<Component>(),
                    Properties = new DbmsApi.API.Properties(),
                    Tags = new List<KeyValuePair<string, string>>(),
                    TypeId = "N/A",
                    Location = loc,
                    Orientation = orient
                };

                // Sanity Check:
                Matrix4 tempMat = Utils.GetTranslationMatrixFromLocationOrientation(loc, orient);
                bool checkMats = Matrix4.Equal(tempMat, translateMatrix);
                if (!checkMats)
                {
                    int i = 0;
                }

                model.ModelObjects.Add(mco);
            }

            model.Tags.Add(new KeyValuePair<string, string>("Dataset", ConverterGeneral.Datasets.COOHOM.ToString()));
            return model;
        }

        public static Model GetModelFromObj(FileStream fileStream, string path, string id, double scale, bool flipTriangles, bool flipYZ)
        {
            IObjLoader objLoader = new ObjLoaderFactory().Create();
            LoadResult result = objLoader.Load(fileStream);

            List<DatasetCoohomClassesModel.Class1> inputInfo;
            using (StreamReader r = new StreamReader(path + "\\architecture_meta.json"))
            {
                string json = r.ReadToEnd();
                inputInfo = JsonConvert.DeserializeObject<List<DatasetCoohomClassesModel.Class1>>(json);
            }

            Dictionary<string, List<Component>> componentGroups = new Dictionary<string, List<Component>>();
            foreach (var group in result.Groups)
            {
                //  Get faces
                List<Tuple<Vector3D, List<Vector3D>>> faces = group.Faces.Select(face => ObjConverter.GetFace(face, result, flipYZ)).ToList();

                // Attempt to tesellate the faces
                List<int> triangles = new List<int>();
                List<Vector3D> finalVertices = new List<Vector3D>();
                foreach (Tuple<Vector3D, List<Vector3D>> face in faces)
                {
                    if (face.Item2.Count < 3)
                    {
                        continue;
                    }

                    List<int> tempTriangleInts = Utils.EarClip(face.Item2, face.Item1);
                    foreach (int intVal in tempTriangleInts)
                    {
                        triangles.Add(intVal + finalVertices.Count);
                    }
                    finalVertices.AddRange(face.Item2);
                }

                List<int[]> trianglesTuples = new List<int[]>();
                for (int index = 0; index < triangles.Count; index += 3)
                {
                    if (flipTriangles)
                    {
                        trianglesTuples.Add(new int[3] { triangles[index], triangles[index + 2], triangles[index + 1] });
                    }
                    else
                    {
                        trianglesTuples.Add(new int[3] { triangles[index], triangles[index + 1], triangles[index + 2] });
                    }
                }

                Component component = new Component()
                {
                    Triangles = trianglesTuples,
                    Vertices = finalVertices.Select(v => v.Copy()).ToList(),
                    MaterialId = group.Material != null ? group.Material.Name : "Default",
                    Properties = new DbmsApi.API.Properties(),
                    Tags = new List<KeyValuePair<string, string>>()
                };

                List<string> numberVals = group.Name.Split('_').Where(i => double.TryParse(i, out double j) == true).ToList();
                numberVals.RemoveAt(numberVals.Count - 1);
                string groupName = string.Join("_", numberVals.Select(i => Convert.ToDouble(i)));
                if (componentGroups.ContainsKey(groupName))
                {
                    componentGroups[groupName].Add(component);
                }
                else
                {
                    componentGroups[groupName] = new List<Component>() { component };
                }
            }

            List<ModelObject> modelObjects = new List<ModelObject>();
            foreach (var comp in componentGroups)
            {
                string type = "N/A";
                foreach (Class1 info in inputInfo)
                {
                    List<string> numberVals = info.group.Split('_').Where(i => double.TryParse(i, out double j) == true).ToList();
                    numberVals.RemoveAt(numberVals.Count - 1);
                    string groupName = string.Join("_", numberVals.Select(i => Convert.ToDouble(i)));
                    if (groupName == comp.Key)
                    {
                        type = info.type;
                        break;
                    }
                }
                ModelObject newModelObject = new ModelObject()
                {
                    Components = comp.Value,
                    Id = comp.Key,
                    Name = comp.Key,
                    Properties = new DbmsApi.API.Properties(),
                    Tags = new List<KeyValuePair<string, string>>(),
                    TypeId = type
                };

                newModelObject.Orientation = new Vector4D(0, 0, 0, 1);
                newModelObject.Location = ConverterGeneral.CenterObject(newModelObject.Components, scale, newModelObject.Orientation);
                modelObjects.Add(newModelObject);
            }

            Model model = new Model()
            {
                Id = id,
                ModelObjects = modelObjects,
                Name = id,
                Properties = new DbmsApi.API.Properties(),
                Relations = new List<Relation>(),
                Tags = new List<KeyValuePair<string, string>>()
            };

            return model;
        }

        // Objects

        public static CatalogObject ConvertCoohomCatalogObject(string path, double scale, bool flipTriangles, bool flipYZ)
        {
            string id = Path.GetFileNameWithoutExtension(path);
            FileStream fileStream = new FileStream(path + "\\mesh.obj", FileMode.Open);
            IObjLoader objLoader = new ObjLoaderFactory().Create();
            LoadResult result = objLoader.Load(fileStream);
            CatalogObject catalogObject = ObjConverter.ConvertObjFile(result, id, scale, flipTriangles, flipYZ);
            fileStream.Close();
            catalogObject.CatalogID = id;

            DatasetCoohomClassesObj.Rootobject inputInfo = null;
            using (StreamReader r = new StreamReader(path + "\\meta.json"))
            {
                string json = r.ReadToEnd();
                inputInfo = JsonConvert.DeserializeObject<DatasetCoohomClassesObj.Rootobject>(json);
            }

            catalogObject.TypeId = inputInfo.nyu_name;
            catalogObject.Name = inputInfo.nyu_name;
            catalogObject.Tags.Add(new KeyValuePair<string, string>("Dataset", ConverterGeneral.Datasets.COOHOM.ToString()));
            return catalogObject;
        }
    }
}
