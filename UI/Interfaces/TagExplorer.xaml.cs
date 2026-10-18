using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Shapes;
using TagFramework.Src.TagFiles;
using TagFramework.Src.ModuleFiles;
using TagFramework.Src.MapInfoFiles;



namespace TagEditor.UI.Windows{
    public class directory_item{
        public directory_item(string n, string a, bool i_f, List<directory_item>? c, bool i_m, string p, Module.unpacked_module_file? m_f, Module? m){
            name = n;
            alias = a;
            is_folder = i_f;
            children = c;
            is_module = i_m;
            path = p;
            module_file = m_f;
            source_module = m;}
        public string name;
        public string alias;
        public bool is_folder;
        // if folder than use this
        public List<directory_item>? children; // it would be a good idea to cache all child files & directories so we can calculate which items we're displaying to virtualize the list

        public bool is_module;
        public string path; // used as literal path for non-modules, used as toplevel dictionary key for module folders
        // if module then use these
        public Module.unpacked_module_file? module_file;
        public Module? source_module;
    }

    public partial class TagExplorer : System.Windows.Controls.UserControl{
        public MainWindow main; // only used for output. IE sending a call to the main window to open a tag, errors


        // structs used to map out directory structures
        List<directory_item> top_level_folders = new();
        List<Module> open_modules = new();

        public TagExplorer(MainWindow _main){
            main = _main;
            InitializeComponent();
            fileTemplate = (DataTemplate)FindResource("FileItem");
        }
        DataTemplate fileTemplate = null;


        // this is only run for local files, not module files
        public List<directory_item> recursive_folder_mapping(string folder){
            if (!Directory.Exists(folder)) throw new Exception("folder was not valid!!");

            List<directory_item> output = new();
            foreach (string file in System.IO.Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly))
                output.Add(new directory_item(System.IO.Path.GetFileName(file), System.IO.Path.GetFileName(file), false, null, false, file, null, null));
            foreach (string file in System.IO.Directory.GetDirectories(folder, "*", SearchOption.TopDirectoryOnly))
                output.Add(new directory_item(System.IO.Path.GetFileName(file), System.IO.Path.GetFileName(file), true, recursive_folder_mapping(file), false, file, null, null));
            return output;
        }
        public void OpenDirectory(bool clear_previous){
            System.Windows.Forms.FolderBrowserDialog openFolderDialogue = new System.Windows.Forms.FolderBrowserDialog();
            var result = openFolderDialogue.ShowDialog();
            string directory = openFolderDialogue.SelectedPath;
            if ((result == DialogResult.OK) && (!string.IsNullOrWhiteSpace(directory)) && Directory.Exists(directory)){
                if (clear_previous) Button_CloseDirectory(null, null); // only due of formality, else we'd have that single line
                // configure the directory item, so we can parse it

                directory_item folder = new directory_item(System.IO.Path.GetFileName(directory), System.IO.Path.GetFileName(directory), true, recursive_folder_mapping(directory), false, directory, null, null);
                top_level_folders.Add(folder);

                tag_view.Items.Add(CreateTreeDirectory(folder));
        }}
        public List<directory_item> module_folder_mapping(Module mod){

            List<directory_item> output = new();
            // first process the folders
            foreach(var list in mod.file_groups){ // List<Module.indexed_module_file>

                // process all children into list
                List<directory_item> group_files = new();
                foreach (var file in list.Value) // List<module_structs.module.indexed_module_file>    
                    group_files.Add(new directory_item(file.name, file.alias, false, null, true, file.name, file.file, mod));
                
                // then create folder
                output.Add(new directory_item(list.Key, list.Key, true, group_files, true, list.Key, null, mod));
            }

            return output;
        }
        public void OpenModule(bool clear_previous){
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            var result = openFileDialog.ShowDialog();
            string file = openFileDialog.FileName;
            if ((result == true) && (!string.IsNullOrWhiteSpace(file)) && File.Exists(file)){
                // then open the selected module
                Module mod;
                try{
                    mod = new Module(file);
                    directory_item folder = new directory_item(mod.module_name, mod.module_name, true, module_folder_mapping(mod), true, file, null, mod);
                    if (clear_previous) Button_CloseDirectory(null, null); // only due of formality, else we'd have that single line
                    top_level_folders.Add(folder);
                    open_modules.Add(mod);
                    main.TagViewer_UpdateModules(open_modules);
                    // call a refresh on the module viewer window
                    tag_view.Items.Add(CreateTreeDirectory(folder));

                } catch{main.DisplayNote("failed to open module: " + file, null, MainWindow.error_level.WARNING);}
        }}
        public void OpenMapinfo(bool clear_previous){
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            var result = openFileDialog.ShowDialog();
            string file = openFileDialog.FileName;
            if ((result == true) && (!string.IsNullOrWhiteSpace(file)) && File.Exists(file)){
                //// then open the selected mapinfo
                //try{
                //    if (!file.EndsWith(".mapinfo")) throw new Exception("file is likely not a mapinfo file!");

                //    if (clear_previous) Button_CloseDirectory(null, null); // only due of formality, else we'd have that single line
                //    List<Module> modules = MapInfo.open_mapinfo(file);
                //    foreach(Module mod in modules){
                //        try{
                //            directory_item folder = new directory_item(mod.module_name, mod.module_name, true, module_folder_mapping(mod), true, file, null, mod);
                //            top_level_folders.Add(folder);
                //            open_modules.Add(mod);
                //            tag_view.Items.Add(CreateTreeDirectory(folder));
                //        } catch{main.DisplayNote("failed to open module: " + mod.module_file_path, null, MainWindow.error_level.WARNING);}

                //    }


                //} catch{main.DisplayNote("failed to open module: " + file, null, MainWindow.error_level.WARNING);}
                //main.TagViewer_UpdateModules(open_modules);
        }}

        public void try_open_tag(string tagname, string tagIDname) {
            if (tagIDname == "FFFFFFFF")
            {
                main.DisplayNote("this tag reference is null.", null, MainWindow.error_level.NOTE);
                return;
            }
            foreach (var v in top_level_folders){
                if (recurse_try_open_tag(v, tagname, tagIDname))
                    return;
            }

            main.DisplayNote("could not find file: " + tagname + ", maybe try opening a .mapinfo file?", null, MainWindow.error_level.WARNING);
        }
        private bool recurse_try_open_tag(directory_item folder, string tagname, string tagIDname){
            foreach(var v in folder.children){
                if (v.is_folder){
                    if (recurse_try_open_tag(v, tagname, tagIDname))
                        return true;
                } 
                else if (tagIDname == v.name) 
                {
                    // then open the tag, and return true
                    if (v.is_module) // handle module file
                        main.TagViewer_OpenModuleTag(v);
                    else // handle as regular disk file
                        main.TagViewer_OpenTag(v.path);
                    return true;
                }
            }
            return false;
        }

        public void TreeViewItem_Expanded(object sender, RoutedEventArgs e){
            TreeViewItem item = e.Source as TreeViewItem;
            directory_item expanded_item = (item.Tag as directory_item);
            if (expanded_item.is_folder){
                if (item.Items.Count != 1 || item.Items[0] != "Loading...") return; // do not do anything if this folder has already had its content loaded
                item.Items.Clear(); // remove the placeholder loading... item


                // since we're using a unified format, theres no need to have separate handling for local folders & module folders, especially as all contained content is already loaded
                //if (expanded_item.is_module){ // open as module folder
                //    if (expanded_item.children == null) return; // theres no children to load?
                //    foreach (directory_item subDir in expanded_item.children){ }
                //}else 
                try{ // open as regular folder
                    if (expanded_item.children == null) return; // theres no children to load?
                    foreach (directory_item subDir in expanded_item.children){
                        if (subDir.is_folder){ // process folders
                            item.Items.Add(CreateTreeDirectory(subDir));
                        } else { // process files
                            var new_item = CreateTreeFile(subDir);
                            item.Items.Add(new_item);
                            if ( (!FilenameMatches(subDir.name)) || (subDir.name.Contains("[") && (ShowResources.IsChecked != true)) )
                                new_item.Visibility = Visibility.Collapsed;

                }}} catch { main.DisplayNote("Unable to open child of "+ expanded_item.path, null, MainWindow.error_level.NOTE);}

            }else { // open as potential tag file
                if (expanded_item.is_module) // handle module file
                    main.TagViewer_OpenModuleTag(expanded_item);
                else // handle as regular disk file
                    main.TagViewer_OpenTag(expanded_item.path);
            }
        }
        public void TreeViewItem_Closed(object sender, RoutedEventArgs e){
            if (ClearChildren.IsChecked == true){ // heck you nullable boolean
                TreeViewItem item = e.Source as TreeViewItem;
                item.Items.Clear();
                item.Items.Add("Loading...");
        }}
        private TreeViewItem CreateTreeDirectory(directory_item dir){
            TreeViewItem item = CreateTreeDir(dir);
            item.Items.Add("Loading...");
            return item;
        }
        private TreeViewItem CreateTreeDir(directory_item dir){
            TreeViewItem item = new TreeViewItem();
            item.Header = dir.alias;
            item.Tag = dir;
            return item;
        }
        private TreeViewItem CreateTreeFile(directory_item dir){
            TreeViewItem item = new TreeViewItem();
            item.Header = dir.alias;
            item.HeaderTemplate = fileTemplate;
            item.Tag = dir;
            return item;
        }
        private void Button_OpenAll(object sender, RoutedEventArgs e) => Recurs_OpenAll(tag_view.Items);
        private void Recurs_OpenAll(ItemCollection items){
            foreach (System.Windows.Controls.TreeViewItem item in items){
                if (item.Items.Count == 0 || item.IsExpanded == true) return;
                item.IsExpanded = true;
                TreeViewItem_Expanded(null, new RoutedEventArgs(null, item));
                Recurs_OpenAll(item.Items);
        }}
        private void Button_CloseAll(object sender, RoutedEventArgs e){
            foreach(TreeViewItem item in tag_view.Items){
                item.IsExpanded = false;
                item.Items.Clear();
                item.Items.Add("Loading...");
        }}

        private void Button_OpenDirectory(object sender, RoutedEventArgs e) => OpenDirectory(true);
        private void Button_AddDirectory(object sender, RoutedEventArgs e) => OpenDirectory(false);
        private void Button_OpenModule(object sender, RoutedEventArgs e) => OpenModule(true);
        private void Button_AddModule(object sender, RoutedEventArgs e) => OpenModule(false);
        private void Button_OpenMapInfo(object sender, RoutedEventArgs e) => OpenMapinfo(true);
        private void Button_AddMapInfo(object sender, RoutedEventArgs e) => OpenMapinfo(false);

        private void Button_CloseDirectory(object sender, RoutedEventArgs e){
            tag_view.Items.Clear();
            top_level_folders.Clear();
            // update module viewer window
            open_modules.Clear();
            main.TagViewer_UpdateModules(open_modules);

        }


        private void Toggle_ShowResources(object sender, RoutedEventArgs e) => Recurs_ToggleResources(tag_view.Items);
        private void Recurs_ToggleResources(ItemCollection items){
            foreach (System.Windows.Controls.TreeViewItem item in items){
                directory_item? expandedDir = item.Tag as directory_item;
                if (expandedDir != null && item.IsExpanded) Recurs_ToggleResources(item.Items);
                else if ((item.Header as string).Contains("[")) item.Visibility = (ShowResources.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        }}
        // its filtern time, enter key must be entered when searching all mode
        private void TextBlock_KeyDown(object sender, System.Windows.Input.KeyEventArgs e){
            if (SearchAll.IsChecked == true && e.Key == Key.Enter) SearchMatches(tag_view.Items);}
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e){
            if (SearchAll.IsChecked != true) SearchMatches(tag_view.Items);}
        public bool SearchMatches(ItemCollection items){
            bool output = false;
            foreach (System.Windows.Controls.TreeViewItem item in items)
                output |= SearchMatch(item);
            return output;
        }
        public bool SearchMatch(TreeViewItem item){
            directory_item? expandedDir = item.Tag as directory_item;
            string name = item.Header as string;
            bool had_match = false;
            if (expandedDir != null){
                if ((SearchFolderNames.IsChecked == true) && name_matches(name)) had_match = true;
                if (!item.IsExpanded && (SearchAll.IsChecked == false)) goto END; // nothing more to check
                if (item.Items.Count == 0) goto END; // no children to check
                if (item.Items[0] != "Loading..."){ // if the children are already loaded
                    if (!SearchMatches(item.Items)) goto END;
                    had_match = true;
                    item.IsExpanded = true; 
                }else{ // if the children are not loaded, load them
                    TreeViewItem? result = null; // SearchClosedMatch(expandedDir);
                    if (result == null) goto END; // no children, go to end
                    item.Items.Clear();
                    item.IsExpanded = true;
                    had_match = true;
                    for (int i = 0; i < result.Items.Count; i++){ // BIG MASSIVE BALLS, JESUS ON A TRACTOR
                        TreeViewItem v = result.Items[i] as TreeViewItem;
                        result.Items.RemoveAt(i);
                        item.Items.Add(v);
            }}}
            else if (FilenameMatches(name)){ // can't use goto false, cause thats reserved for folders
                item.Visibility = Visibility.Visible;
                return true;
            }else{
                item.Visibility = Visibility.Collapsed;
                return false;
            }
        END:
            if (had_match) item.Visibility = Visibility.Visible;
            else if (HideFolders.IsChecked == true) item.Visibility = Visibility.Collapsed;
            return had_match;
        }
        // deprecated as we're going to update to a newer system in the future that will work very differently
        //public TreeViewItem? SearchClosedMatch(directory_item directory){
        //    List<TreeViewItem> matched_children = new();
        //    bool match = false;
        //    foreach (DirectoryInfo subDir in directory.GetDirectories()){
        //        TreeViewItem? subItem = SearchClosedMatch(subDir);
        //        if (subItem != null){
        //            matched_children.Add(subItem);
        //            match = true;
        //    }}
        //    if (!match){
        //        foreach (FileInfo file in directory.GetFiles()) 
        //            if (FilenameMatches(file.Name) && ((!file.Name.Contains("[")) || ShowResources.IsChecked == true)) match = true;
        //        if ((SearchFolderNames.IsChecked == true) && name_matches(directory.Name)) match = true;
        //    }
        //    if (match){
        //        TreeViewItem output = CreateTreeDir(directory);
        //        output.IsExpanded = true;
        //        foreach (DirectoryInfo subDir in directory.GetDirectories()){
        //            bool was_found_before = false;
        //            for (int i = 0; i < matched_children.Count; i++){
        //                TreeViewItem matched_item = matched_children[i];
        //                if (subDir.Name == matched_item.Header as string){
        //                    was_found_before = true;
        //                    output.Items.Add(matched_item);
        //                    matched_item.IsExpanded = true;
        //                    matched_children.RemoveAt(i); // cleanup for a little bit of efficiency
        //                    break;
        //            }}
        //            if (!was_found_before){
        //                TreeViewItem hiddenitem = CreateTreeDirectory(subDir);
        //                if (HideFolders.IsChecked == true) hiddenitem.Visibility = Visibility.Collapsed;
        //                output.Items.Add(hiddenitem);
        //        }}
        //        foreach (FileInfo subFile in directory.GetFiles()){
        //            TreeViewItem new_item = CreateTreeFile(subFile);
        //            output.Items.Add(new_item);
        //            if (!FilenameMatches(subFile.Name) || (subFile.Name.Contains("[") && (ShowResources.IsChecked == false)))
        //                new_item.Visibility = Visibility.Collapsed;
        //        }
        //        return output;
        //    }
        //    return null;
        //}
        public bool FilenameMatches(string name){
            if ((ShowResources.IsChecked == true) || (!name.Contains("["))) if (name_matches(name)) return true;
            return false;
        }

        private void HideFolders_Click(object sender, RoutedEventArgs e){
            if (HideFolders.IsChecked != true)
                Recurse_UnhideFolders(tag_view.Items);
        }
        private void Recurse_UnhideFolders(ItemCollection items){
            foreach (System.Windows.Controls.TreeViewItem item in items){
                directory_item? expandedDir = item.Tag as directory_item;
                if (expandedDir != null){
                    item.Visibility = Visibility.Visible;
                    Recurse_UnhideFolders(item.Items);
        }}}
        private bool name_matches(string name) => name?.IndexOf(SearchBox.Text, (CaseSensitve.IsChecked == true) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) >= 0;

        private void Button_ClearSearch(object sender, RoutedEventArgs e){
            SearchBox.Text = "";
        }

    }
}
