using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using static TagFramework.Src.TagFiles.Tag;
using static TagEditor.UI.Windows.TagInstance;


namespace TagEditor.UI.Interfaces.Editor.Params
{
    /// <summary>
    /// Interaction logic for TagblockParam.xaml
    /// </summary>
    public partial class TagblockParam : UserControl
    {
        public string key;
        public TagblockParam(string name, tagdata_struct _tag_data, expand_link _parent, string _key)
        {
            key = _key;

            InitializeComponent();

            tag_data = _tag_data;

            // for the annoying vtable struct that has no name for no reason in particular
            Namebox.Text = name + " [" + _tag_data.blocks.Count + "]";
            parent = _parent;
        }
        public void reload(string name, tagdata_struct _tag_data, string _key){
            key = _key;
            tag_data = _tag_data;

            // for the annoying vtable struct that has no name for no reason in particular
            Namebox.Text = name + " [" + _tag_data.blocks.Count + "]";

            // reset selection
            selected_index = 0;
            indexbox.Text = selected_index.ToString();
        }
        public tagdata_struct tag_data;

        public expand_link parent;

        public int selected_index = 0; // we will soon have ascript that will alter thsi, it will then be read externally
        public int length;
        private void Button_Click(object sender, RoutedEventArgs e){
            parent.expand(false);
        }

        private void TextBox_MouseWheel(object sender, MouseWheelEventArgs e){
            int direction = 0;
            e.Handled = true;
            if (e.Delta < 0) direction = 1;
            else if (e.Delta > 0) direction = -1;
            direction += selected_index;

            if (direction < tag_data.blocks.Count && 0 <= direction){
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
                    if (new_index < tag_data.blocks.Count && 0 <= new_index){
                        indexbox.Text = new_index.ToString();
                        int old_index = selected_index;
                        selected_index = new_index;
                        parent.expand(true, old_index);
                        return;
                }} catch{}
                indexbox.Text = selected_index.ToString();
            }
        }
    }
}
