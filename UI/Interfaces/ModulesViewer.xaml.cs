using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using TagEditor.UI.Windows;
using static TagEditor.MainWindow;
using TagFramework.Src.ModuleFiles;

namespace TagEditor.UI.Interfaces
{
    /// <summary>
    /// Interaction logic for ModulesViewer.xaml
    /// </summary>
    public partial class ModulesViewer : UserControl{
        MainWindow main;
        public ModulesViewer(MainWindow _main){
            main = _main;
            InitializeComponent();
        }
        string[] module_names_display = new string[0];
        List<string> modules_names = new();
        public List<Module> active_modules = new();
        Module? selected_module = null;
        public void update_modules(List<Module> new_modules){
            active_modules = new_modules;
            modules_names.Clear();

            foreach (var module in new_modules)
                modules_names.Add(module.module_name);

            module_names_display = modules_names.ToArray();
            modulesview.SelectedIndex = -1;
            modulesview.ItemsSource = module_names_display;
        }
        public void update_module_stats(){
            if (selected_module == null){
                files_count.Text = "Files: NULL";
                changes_count.Text = "Tags Committed: NULL";
            }else {
                int int_files_count = selected_module.module_info.FileCount;
                int int_resources_count = selected_module.module_info.ResourceCount;
                int int_tags_count = int_files_count - int_resources_count;
                files_count.Text = "Files: " + int_tags_count + "\"" + int_resources_count + "\"" + int_files_count;
                // count number of changes ready to be compiled
                int comitted_tags = 0;
                foreach(var v in selected_module.file_groups)
                    foreach(var file in v.Value)
                        if (file.file.blocks != null && file.file.has_been_edited == true) // we only commit tags that are loaded & edited
                            comitted_tags++;
                changes_count.Text = "Tags Committed: " + comitted_tags;
            }
        }
        private void modulesview_SelectionChanged(object sender, SelectionChangedEventArgs e){
            // error checking
            if (modulesview.SelectedIndex < 0) selected_module = null;
            else if (modulesview.SelectedIndex >= active_modules.Count) selected_module = null; // we should put an exception here
            else selected_module = active_modules[modulesview.SelectedIndex];
            update_module_stats();
        }

        private void Button_UnpackModule(object sender, RoutedEventArgs e){
            if (selected_module == null) return;

            int success_count = 0;
            try
            {
                // open thing prompt and select output path
                var folderDialog = new OpenFolderDialog
                {
                    // Set options here
                };
                if (folderDialog.ShowDialog() == true)
                {
                    var folderName = folderDialog.FolderName;
                    if (!string.IsNullOrWhiteSpace(folderName) && Directory.Exists(folderName))
                    {
                        foreach (var v in selected_module.file_groups)
                        {
                            string group = v.Key;
                            string group_folder = System.IO.Path.Combine(folderName, group);
                            if (!Directory.Exists(group_folder)) Directory.CreateDirectory(group_folder);
                            foreach (var tag in v.Value)
                            {
                                byte[] unpacked_tag = selected_module.get_module_file_bytes(tag.file);
                                string file_path = System.IO.Path.Combine(group_folder, tag.name);
                                File.WriteAllBytes(file_path, unpacked_tag);
                                success_count += 1;
                            }
                        }
                        main.DisplayNote("Unpack success, " + success_count + " tags successfully unpacked", null, error_level.NOTE);
                    }
                    else main.DisplayNote("Bad directory, please try again", null, error_level.WARNING);
                }
                }catch (Exception ex){ main.DisplayNote("exception during unpack, " + success_count + " tags successfully unpacked", ex, error_level.ERROR);}
        }
        private void Button_AddTag(object sender, RoutedEventArgs e){
            if (selected_module == null) return;


            try{
                // open thing prompt and select output path
                OpenFileDialog saveFileDialog1 = new OpenFileDialog();
                saveFileDialog1.ShowDialog();
                if (!string.IsNullOrWhiteSpace(saveFileDialog1.FileName)){
                    var tags = TagViewer.get_tagbytes_with_resources(saveFileDialog1.FileName, main);
                    if (tags == null){
                        // main.DisplayNote("failed to ", ex, error_level.ERROR); // error note will be covered in the function itself
                        return;
                    }
                    ModuleCompiler compiler = new(selected_module);
                    // convert resource list
                    List<byte[]> resources = new();
                    foreach (var v in tags.resource_list) resources.Add(v.Key);
                    // pack tag (it'll try and grab the tagID automatically)
                    compiler.pack_tag(tags.bytes, resources, null, null, null, false);
                    main.DisplayNote("Tag successfully packed. compile module to save changes", null, error_level.NOTE);
                    update_module_stats();
            }}catch (Exception ex){ main.DisplayNote("failed to pack tag into module, possibly corrupted module in RAM. please close and reopen this application", ex, error_level.ERROR);}
            //try{
            //    // open thing prompt and go through all 

            //    module_compiler compiler = new(selected_module);
            //    compiler.pack_tag();
            //} catch (Exception ex) { 

            //}
        }
        private void Button_CompileModule(object sender, RoutedEventArgs e){
            if (selected_module == null) return;

            
            try{
                // open thing prompt and select output path
                SaveFileDialog saveFileDialog1 = new SaveFileDialog(); 
                saveFileDialog1.Filter = "Module file|*.module";
                saveFileDialog1.ShowDialog();
                if (!string.IsNullOrWhiteSpace(saveFileDialog1.FileName)){
                    ModuleCompiler compiler = new(selected_module);
                    compiler.compile(saveFileDialog1.FileName);
                    main.DisplayNote("Tag successfully compiled. as of now, you must reload the application to continue editing tags.", null, error_level.ERROR);
            }} catch (Exception ex) { main.DisplayNote("failed to compile module, possibly corrupted output file & module in RAM. please close and reopen this application", ex, error_level.ERROR);}
        }
        private void Button_CopyModule(object sender, RoutedEventArgs e){
            if (selected_module == null) return;
            StringBuilder sb = new();
            sb.AppendLine(selected_module.module_name);
            sb.AppendLine("Head: " + selected_module.module_info.Head);
            sb.AppendLine("Version: " + selected_module.module_info.Version);
            sb.AppendLine("ModuleId: " + selected_module.module_info.ModuleId);
            sb.AppendLine("FileCount: " + selected_module.module_info.FileCount);
            sb.AppendLine("ManifestCount: " + selected_module.module_info.ManifestCount);
            sb.AppendLine("ResourceIndex: " + selected_module.module_info.ResourceIndex);
            sb.AppendLine("StringsSize: " + selected_module.module_info.StringsSize);
            sb.AppendLine("ResourceCount: " + selected_module.module_info.ResourceCount);
            sb.AppendLine("BlockCount: " + selected_module.module_info.BlockCount);
            sb.AppendLine("BuildVersion: " + selected_module.module_info.BuildVersion);
            sb.AppendLine("Checksum: " + selected_module.module_info.Checksum);
            Clipboard.SetText(sb.ToString());
        }

    }
}
