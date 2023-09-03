using DbmsApi;
using DbmsApi.API;
using DbmsApi.Mongo;
using MathPackage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Xbim.Common.Geometry;
using Xbim.Common.Step21;
using Xbim.Common.XbimExtensions;
using Xbim.Ifc;
using Xbim.Ifc.Extensions;
using Xbim.IO;
using Xbim.ModelGeometry.Scene;
using Component = DbmsApi.API.Component;

namespace ModelConverter
{
    public static class IfcConverter
    {
        private static string ifcTypeconverterFile = "../../typeConvertDict.json";
        private static Dictionary<string, string> ifcTypeConverter { get; set; }
        public static Dictionary<string, string> IfcTypeConverter
        {
            get
            {
                if (ifcTypeConverter == null)
                {
                    ifcTypeConverter = GetTypeConvertDictionary();
                }
                return ifcTypeConverter;
            }
        }

        public static void SetIfcTypeDictionary(string fileName)
        {
            ifcTypeconverterFile = fileName;
            ifcTypeConverter = null;
        }

        public static void AddTypeConvert(string key, string value)
        {
            ifcTypeConverter.Add(key, value);
        }

        // Method for getting list of objects from a IfcModel (Should be a xbim file type if using in Unity)
        public static Model GetObjectsFromIFC(string modelPath, double scale, bool flipTriangles)
        {
            var modelIfc = IfcStore.Open(modelPath);
            bool createContext = false;
            if (Path.GetExtension(modelPath) == ".xbim")
            {
                createContext = false;
            }
            if (Path.GetExtension(modelPath) == ".ifc")
            {
                createContext = true;
            }
            XbimSchemaVersion version = modelIfc.SchemaVersion;
            Model model = GetObjectsFromIFC(modelIfc, createContext, version, scale, flipTriangles);
            model.Tags.Add(new KeyValuePair<string, string>("Dataset", ConverterGeneral.Datasets.IFC.ToString()));
            return model;
        }

        // Method for getting list of objects from a IfcModel
        public static Model GetObjectsFromIFC(IfcStore model, bool createContext, XbimSchemaVersion version, double scale, bool flipTriangles)
        {
            // Get the objects:
            Dictionary<int, ModelObject> objectDict = new Dictionary<int, ModelObject>();
            if (version == XbimSchemaVersion.Ifc4)
            {
                objectDict = GetObjectInModel4(model);
            }
            if (version == XbimSchemaVersion.Ifc2X3)
            {
                objectDict = GetObjectInModel2x3(model);
            }

            Xbim3DModelContext context = new Xbim3DModelContext(model);
            if (createContext)
            {
                context.CreateContext();
            }
            List<XbimShapeGeometry> geometrys = context.ShapeGeometries().ToList();
            List<XbimShapeInstance> instances = context.ShapeInstances().ToList();

            //Check all the instances
            foreach (var instance in instances)
            {
                if (instance.RepresentationType != XbimGeometryRepresentationType.OpeningsAndAdditionsIncluded)
                {
                    continue;
                }

                if (!objectDict.ContainsKey(instance.IfcProductLabel))
                {
                    continue;
                }

                var transfor = instance.Transformation; //Transformation matrix (location point inside)

                XbimShapeGeometry geometry = context.ShapeGeometry(instance);   //Instance's geometry
                XbimRect3D box = geometry.BoundingBox; //bounding box you need

                List<int[]> triangles = new List<int[]>();
                List<XbimPoint3D> localverts = new List<XbimPoint3D>();
                List<XbimPoint3D> globalverts = new List<XbimPoint3D>();

                byte[] data = ((IXbimShapeGeometryData)geometry).ShapeData;
                //If you want to get all the faces and trinagulation use this
                using (var stream = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        var mesh = reader.ReadShapeTriangulation();

                        List<XbimFaceTriangulation> faces = mesh.Faces as List<XbimFaceTriangulation>;
                        List<XbimPoint3D> vertices = mesh.Vertices as List<XbimPoint3D>;

                        foreach (var face in faces)
                        {
                            for (int i = 0; i < face.TriangleCount; i++)
                            {
                                int index = i * 3;
                                if (flipTriangles)
                                {
                                    triangles.Add(new int[3] { face.Indices[index], face.Indices[index + 2], face.Indices[index + 1] });
                                }
                                else
                                {
                                    triangles.Add(new int[3] { face.Indices[index], face.Indices[index + 1], face.Indices[index + 2] });
                                }
                            }
                        }
                        foreach (XbimPoint3D p in vertices)
                        {
                            localverts.Add(p);
                            var tp = transfor.Transform(p);
                            globalverts.Add(tp);
                        }
                    }
                }

                Component objectComponent = new Component();
                objectComponent.Vertices = globalverts.Select(v => ConverterGeneral.VectorConverterXbim(v)).ToList();
                objectComponent.Triangles = triangles;
                objectDict[instance.IfcProductLabel].Components.Add(objectComponent);
            }

            // Next bit is for making the local center of all objects the origin:
            List<ModelObject> objectList = objectDict.Values.ToList();
            foreach (var obj in objectList)
            {
                obj.Location = ConverterGeneral.CenterObject(obj.Components, scale, obj.Orientation);
            }

            Model finalModel = new Model()
            {
                Id = model.UserDefinedId.ToString(),
                Name = Path.GetFileNameWithoutExtension(model.FileName),
                ModelObjects = objectList.Where(o => o.Components.Count > 0).ToList(),
                Properties = new DbmsApi.API.Properties(),
                Tags = new List<KeyValuePair<string, string>>(),
                Relations = new List<Relation>()
            };
            return finalModel;
        }

        public static Dictionary<int, ModelObject> GetObjectInModel4(IfcStore model)
        {
            Dictionary<int, ModelObject> objectList = new Dictionary<int, ModelObject>();
            var modelInstances = model.Instances.OfType<Xbim.Ifc4.Kernel.IfcProduct>().ToList();
            foreach (var modelInstance in modelInstances)
            {
                ModelObject instanceObject = new ModelObject
                {
                    Name = modelInstance.Name,
                    Id = modelInstance.GlobalId.ToString(),
                    TypeId = IfcTypeConverter[modelInstance.GetType().ToString()],
                    Components = new List<Component>(),
                    Properties = new DbmsApi.API.Properties(),
                    Tags = new List<KeyValuePair<string, string>>()
                };

                // Any Additional things we might want to save...
                try { instanceObject.Tags.Add(new KeyValuePair<string, string>("ContainedIn", modelInstance.IsContainedIn.Name.Value.ToString())); } catch { }

                objectList.Add(modelInstance.EntityLabel, instanceObject);
            }

            return objectList;
        }

        public static Dictionary<int, ModelObject> GetObjectInModel2x3(IfcStore model)
        {
            Dictionary<int, ModelObject> objectList = new Dictionary<int, ModelObject>();
            var modelInstances = model.Instances.OfType<Xbim.Ifc2x3.Kernel.IfcProduct>().ToList();
            foreach (var modelInstance in modelInstances)
            {
                ModelObject instanceObject = new ModelObject
                {
                    Name = modelInstance.Name,
                    Id = modelInstance.GlobalId.ToString(),
                    TypeId = IfcTypeConverter[modelInstance.GetType().ToString()],
                    Orientation = ConverterGeneral.VectorConverterXbim(modelInstance.ObjectPlacement.ToMatrix3D().GetRotationQuaternion()),
                    Properties = new DbmsApi.API.Properties(),
                    Tags = new List<KeyValuePair<string, string>>(),
                    Components = new List<Component>()
                };

                // Any Additional things we might want to save...
                try { instanceObject.Tags.Add(new KeyValuePair<string, string>("ContainedIn", modelInstance.IsContainedIn.Name.Value.ToString())); } catch { }

                objectList.Add(modelInstance.EntityLabel, instanceObject);
            }

            return objectList;
        }

        public static void SaveModelAsXBIM(IfcStore model, string FileName)
        {
            // Save IFC to the internal XBIM format, which includes geometry
            Xbim3DModelContext context = new Xbim3DModelContext(model);
            context.CreateContext();

            // Save IFC to the internal XBIM format, which includes geometry
            model.SaveAs(FileName, StorageType.Xbim);
        }

        public static void SaveModelAsWEBIM(IfcStore model, string FileName)
        {
            // Save IFC to the internal XBIM format, which includes geometry
            Xbim3DModelContext context = new Xbim3DModelContext(model);
            context.CreateContext();

            using (var wexBimFile = File.Create(FileName))
            {
                using (var wexBimBinaryWriter = new BinaryWriter(wexBimFile))
                {
                    model.SaveAsWexBim(wexBimBinaryWriter);
                    wexBimBinaryWriter.Close();
                }
                wexBimFile.Close();
            }
        }

        public static Dictionary<string, string> GetTypeConvertDictionary()
        {
            try
            {
                return DBMSReadWrite.JSONReadFromFile<Dictionary<string, string>>(ifcTypeconverterFile);
            }
            catch (FileNotFoundException)
            {
                DBMSReadWrite.JSONWriteToFile(ifcTypeconverterFile, new Dictionary<string, string>());
                return DBMSReadWrite.JSONReadFromFile<Dictionary<string, string>>(ifcTypeconverterFile);
            }
        }

        public static void SaveTypeConvertDictionary()
        {
            DBMSReadWrite.JSONWriteToFile(ifcTypeconverterFile, IfcTypeConverter);
        }

        // Method for exporting a list of IfcObjects to a IfcModel
        // TODO
    }
}