using DbmsApi;
using DbmsApi.API;
using ModelConverter;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModelContertApp
{
    public partial class IfcMappingForm : Form
    {
        public IfcMappingForm(string ifcType, List<ObjectType> types)
        {
            InitializeComponent();
            this.textBoxIfcType.Text = ifcType;
            this.comboBoxBIMPlatformType.Items.AddRange(types.Select(t => t.Name).ToArray());
        }

        private void buttonConvert_Click(object sender, EventArgs e)
        {
            string type = (string)comboBoxBIMPlatformType.SelectedItem;
            IfcConverter.AddTypeConvert(this.textBoxIfcType.Text, type);
            this.Close();
        }
    }
}