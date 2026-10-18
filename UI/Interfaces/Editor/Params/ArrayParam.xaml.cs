using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using  TagFramework.Src.TagFiles;
using static TagEditor.UI.Windows.TagInstance;

namespace TagEditor.UI.Interfaces.Editor.Params
{
    /// <summary>
    /// Interaction logic for ArrayParam.xaml
    /// </summary>
    public partial class ArrayParam : UserControl
    {
        public string key;
        public ArrayParam(string name, Tag.thing _tag_data, int _struct_offset, string _guid, expand_link _parent, int _length, int _struct_size, string _key)
        {
            key = _key;
            InitializeComponent();

            tag_data = _tag_data;

            // for the annoying vtable struct that has no name for no reason in particular
            Namebox.Text = name + " [" + _length + "]";
            struct_offset = _struct_offset;
            guid = _guid;
            parent = _parent;
            length = _length;
            struct_size = _struct_size;
        }
        public void reload(Tag.thing _tag_data, int _struct_offset, string _key){
            key = _key;
            // name does not need updating? as the size is fixed between every instance
            tag_data = _tag_data;
            struct_offset = _struct_offset;
            // this does not actually need to be reset, but we're going to do it anyway
            selected_index = 0;
            indexbox.Text = selected_index.ToString();
            // we should force it to reload its content? considering we changed the selected index
        }
        public Tag.thing tag_data;
        public int struct_offset; // explicity used for struct types that are inlined with the parent block's params
        public string guid; // this is also because of structs, and also array's too, for both

        public expand_link parent;

        public int selected_index = 0; // we will soon have ascript that will alter thsi, it will then be read externally
        public int length;
        public int struct_size;
        private void Button_Click(object sender, RoutedEventArgs e){
            parent.expand(false);
        }

        private void TextBox_MouseWheel(object sender, MouseWheelEventArgs e){
            int direction = 0;
            e.Handled = true;
            if (e.Delta < 0) direction = 1;
            else if (e.Delta > 0) direction = -1;
            direction += selected_index;

            if (direction < length && 0 <= direction){
                int old_index = selected_index;
                selected_index = direction;
                indexbox.Text = selected_index.ToString();
                if (parent.is_opened) parent.expand(true, old_index);
        }}

        private void TextBox_KeyDown(object sender, KeyEventArgs e){ // test for enter, if so then
            if (e.Key == Key.Enter){
                // read the value in the text box, compare it with the current index
                // if the box is already open, take no action if those values match
                // otherwise if they match but isn't open, then continue
                try{int new_index = Convert.ToInt32(indexbox.Text);
                    if (new_index == selected_index && (!parent.is_opened)) return;
                    if (new_index < length && 0 <= new_index){
                        int old_index = selected_index;
                        selected_index = new_index;
                        indexbox.Text = selected_index.ToString();
                        parent.expand(true, old_index);
                        return;
                }} catch{}
                indexbox.Text = selected_index.ToString();
            }
        }
    }
}
