using DbmsApi;
using DbmsApi.API;
using DbmsApi.Mongo;
using ModelConverter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Xbim.Common.Step21;
using Xbim.Ifc;
using static ModelConverter.ConverterGeneral;

namespace ModelContertApp
{
    public partial class Main : Form
    {
        //JavaScriptSerializer JavaScriptSerializer = new JavaScriptSerializer() { MaxJsonLength = 2097152 / 4 * 1000 };
        List<ObjectType> Types;

        public Main()
        {
            InitializeComponent();
            IfcStore.ModelProviderFactory.UseHeuristicModelProvider();
            this.comboBoxModelUnits.DataSource = Enum.GetValues(typeof(Units));

            FetchTypes();
        }

        private void FetchTypes()
        {
            // Should avoid using this...
            ObjectTypeTree.BuildTypeTree(ObjectTypeTree.DefaultTypesList());

            Types = ObjectTypeTree.GetAllTypes();
        }

        private void buttonOpenFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Supported Types|*.ifc;*.obj";

            if (ofd.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            this.textBoxFileName.Text = ofd.FileName;
            if (Path.GetExtension(this.textBoxFileName.Text) == ".ifc")
            {
                this.textBoxSaveFileLocation.Text = Path.ChangeExtension(ofd.FileName, "bpmm");
                var model = IfcStore.Open(this.textBoxFileName.Text);
                Units unit = GetUnit(model.ModelFactors.LengthToMetresConversionFactor);
                this.comboBoxModelUnits.SelectedItem = unit;
                this.checkBoxFlipTriangles.Checked = false;
                this.checkBoxFlipYZ.Checked = false;
                this.checkBoxFlipYZ.Enabled = false;
            }
            if (Path.GetExtension(this.textBoxFileName.Text) == ".obj")
            {
                this.textBoxSaveFileLocation.Text = Path.ChangeExtension(ofd.FileName, "bpo");
                Units unit = Units.M;
                this.comboBoxModelUnits.SelectedItem = unit;
                this.checkBoxFlipTriangles.Checked = false;
                this.checkBoxFlipYZ.Checked = true;
                this.checkBoxFlipYZ.Enabled = true;
            }
        }

        private void buttonSaveFile_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(this.textBoxFileName.Text))
            {
                MessageBox.Show("Please select the input file first.");
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = Path.GetFileNameWithoutExtension(this.textBoxFileName.Text);
            if (Path.GetExtension(this.textBoxFileName.Text) == ".ifc")
            {
                sfd.Filter = "BIMPlatform Model File (*.bpm)|*.bpm|xBIM files (*.xbim)|*.xbim";
            }
            if (Path.GetExtension(this.textBoxFileName.Text) == ".obj")
            {
                sfd.Filter = "BIMPlatform Object File (*.bpo)|*.bpo";
            }

            if (sfd.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            this.textBoxSaveFileLocation.Text = sfd.FileName;
        }

        private void buttonConvert_Click(object sender, EventArgs e)
        {
            Units units = (Units)Enum.Parse(typeof(Units), this.comboBoxModelUnits.SelectedValue.ToString());
            double scale = GetScale(units);
            bool flipTriangles = this.checkBoxFlipTriangles.Checked;
            bool flipYZ = this.checkBoxFlipYZ.Checked;
            string name = Path.GetFileNameWithoutExtension(this.textBoxFileName.Text);
            string extension = Path.GetExtension(this.textBoxSaveFileLocation.Text);

            if (extension == ".xbim")
            {
                IfcStore model = IfcStore.Open(this.textBoxFileName.Text);
                IfcConverter.SaveModelAsXBIM(model, this.textBoxSaveFileLocation.Text);
            }

            if (extension == ".bpmm")
            {
                Model dbmsModel = GetDBMSApiModelFromIfc(this.textBoxFileName.Text, scale, flipTriangles);
                MongoModel mongoModel = new MongoModel(dbmsModel, "");
                List<ModelObjectSplit> moSplits = mongoModel.SplitModelObjectFromModel();
                DBMSReadWrite.WriteModel(mongoModel, this.textBoxSaveFileLocation.Text);
                string folderName = Path.GetDirectoryName(this.textBoxSaveFileLocation.Text) + "/" + name;
                Directory.CreateDirectory(folderName);
                foreach (ModelObjectSplit mos in moSplits)
                {
                    DBMSReadWrite.JSONWriteToFile(folderName + "/" + mos.RefId + ".bpmo", mos);
                }
            }

            if (extension == ".bpo")
            {
                FileStream fileStream = new FileStream(this.textBoxFileName.Text, FileMode.Open);
                CatalogObject dbmsObject = ObjConverter.ConvertObjFile(fileStream, name, scale, flipTriangles, flipYZ);
                DBMSReadWrite.WriteCatalogObject(dbmsObject, this.textBoxSaveFileLocation.Text);
                fileStream.Close();
            }
        }

        private Model GetDBMSApiModelFromIfc(string ifcFile, double scale, bool flipTriangles)
        {
            // Build up the dictionary as you go
            var model = IfcStore.Open(ifcFile);
            List<string> instanceTypes = new List<string>();
            if (model.SchemaVersion == XbimSchemaVersion.Ifc4)
            {
                instanceTypes = model.Instances.OfType<Xbim.Ifc4.Kernel.IfcProduct>().Select(s => s.GetType().ToString()).ToList();
            }
            if (model.SchemaVersion == XbimSchemaVersion.Ifc2X3)
            {
                instanceTypes = model.Instances.OfType<Xbim.Ifc2x3.Kernel.IfcProduct>().Select(s => s.GetType().ToString()).ToList();
            }
            foreach (string instanceType in instanceTypes)
            {
                if (!IfcConverter.IfcTypeConverter.ContainsKey(instanceType))
                {
                    IfcMappingForm ifcMappingForm = new IfcMappingForm(instanceType, Types);
                    ifcMappingForm.ShowDialog();
                }
            }

            IfcConverter.SaveTypeConvertDictionary();
            return IfcConverter.GetObjectsFromIFC(ifcFile, scale, flipTriangles);
        }

        private void buttonUpdateConverter_Click(object sender, EventArgs e)
        {
            MessageBox.Show("INCOMPLETE: Meant to reset the type converter");
        }
    }
}