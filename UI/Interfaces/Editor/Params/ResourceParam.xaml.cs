using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static TagFramework.Src.TagFiles.Tag;
using static TagEditor.UI.Windows.TagInstance;

namespace TagEditor.UI.Interfaces.Editor.Params
{
    /// <summary>
    /// Interaction logic for ResourceParam.xaml
    /// </summary>
    public partial class ResourceParam : UserControl
    {
        public string key;
        public ResourceParam(string name, tagdata_struct _tag_data, expand_link _parent, string _key)
        {
            key = _key;

            InitializeComponent();

            tag_data = _tag_data;
            Namebox.Text = name;
            parent = _parent;
        }
        public void reload(tagdata_struct _tag_data, string _key){
            key = _key;
            tag_data = _tag_data;
        }
        public tagdata_struct tag_data;
        public string guid; // this is also because of structs, and also array's too, for both

        //public int selected_index = 0; // we will soon have ascript that will alter thsi, it will then be read externally
        public expand_link parent;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            parent.expand(false);
        }
    }
}
