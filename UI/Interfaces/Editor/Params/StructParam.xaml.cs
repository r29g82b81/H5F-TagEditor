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
using TagFramework.Src.TagFiles;
using static TagEditor.UI.Windows.TagInstance;

namespace TagEditor.UI.Interfaces.Params
{
    /// <summary>
    /// Interaction logic for StructParam.xaml
    /// </summary>
    public partial class StructParam : UserControl
    {
        public string key;
        public StructParam(string name, Tag.thing _tag_data, int _struct_offset, string _guid, expand_link _parent, string _key)
        {
            key = _key;

            InitializeComponent();

            tag_data = _tag_data;

            // for the annoying vtable struct that has no name for no reason in particular
            if (string.IsNullOrWhiteSpace(name)) Namebox.Text = "Blank";
            else Namebox.Text = name;
            struct_offset = _struct_offset;
            guid = _guid;
            parent = _parent;
        }
        public void reload(Tag.thing _tag_data, int _struct_offset, string _key){
            key = _key;
            tag_data = _tag_data;
            struct_offset = _struct_offset; // shouldn't need to be updated?
        }
        public Tag.thing tag_data;
        public int struct_offset; // explicity used for struct types that are inlined with the parent block's params
        public string guid; // this is also because of structs, and also array's too, for both

        //public int selected_index = 0; // we will soon have ascript that will alter thsi, it will then be read externally
        public expand_link parent;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            parent.expand(false);
        }
    }
}
