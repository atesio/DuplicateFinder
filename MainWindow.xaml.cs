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
using DevExpress.Xpf.Bars;
using DuplicateFinderLib;

namespace DuplicateFinder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            _config=new Config();
            _config.InitNew();
            BindConfig();
        }

        private Config _config;
        private string _LastConfigFileName = null;

        private void BindConfig()
        {
            GridFolders.ItemsSource = _config.Folders;
            GridExtensions.ItemsSource = _config.Extensions;
        }

        private void ButtonScan_Click(object sender, RoutedEventArgs e)
        {
            _config.BuildFileList();
            _config.Start(Progress);
            GridResults.ItemsSource = new DuplicateFiles(_config.Files);
            GridResults.ExpandAllGroups();
            GridResultsView.BestFitColumns();
            ColumnMarkedForDeletion.Width = 32; // best fit makes it huge because of the group
            var totalSize = _config.Files.GetMaximumSizeGain();
            TextTotalSize.Text = HumanReadableFileSize.GetReadableSize(totalSize);
            _selectedSize = 0;
        }

        private Microsoft.Win32.FileDialog GetBrowseFileDialog(bool forSave)
        {
            Microsoft.Win32.FileDialog dialog;
            if (forSave)
                dialog = new Microsoft.Win32.SaveFileDialog();
            else
                dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.AddExtension = false;
            dialog.Filter = "Duplicate finder files (*.dup)|*.dup|All files (*.*)|*.*";
            return dialog;
        }
        private void ButtonSaveAs_OnItemClick(object sender, ItemClickEventArgs e)
        {
            var dialog = GetBrowseFileDialog(true);
            if (dialog.ShowDialog()!=true) return;
            dialog.CheckPathExists = true;
            var path = dialog.FileName;
            _config.Save(path);
            _LastConfigFileName = path;
        }

        private void ButtonSave_OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (_LastConfigFileName == null)
            {
                ButtonSaveAs_OnItemClick(sender, e);
                return;
            }
            _config.Save(_LastConfigFileName);
        }

        private void ButtonOpen_OnItemClick(object sender, ItemClickEventArgs e)
        {
            var dialog = GetBrowseFileDialog(false);
            if (dialog.ShowDialog() != true) return;
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            var path = dialog.FileName;
            _config = Config.Load(path);
            BindConfig();
            _LastConfigFileName = path;
            GridResults.ItemsSource = null;
        }

        private void ButtonNew_OnItemClick(object sender, ItemClickEventArgs e)
        {
            _config=new Config();
            _config.InitNew();
            BindConfig();
        }

        private long _selectedSize;
        private void GridResultsView_CellValueChanged(object sender, DevExpress.Xpf.Grid.CellValueChangedEventArgs e)
        {
            if (e.Column.Equals(ColumnMarkedForDeletion))
            {
                var file = (File)GridResults.SelectedItem;
                ChangeSelectedSize(file, (bool) e.Value);
            }
        }

        private void ChangeSelectedSize(File file, bool added)
        {
            if (added)
                _selectedSize += file.Size;
            else
                _selectedSize -= file.Size;
            TextSelectionSize.Text = HumanReadableFileSize.GetReadableSize(_selectedSize);
        }

        private void ButtonDelete_OnClick(object sender, RoutedEventArgs e)
        {
            var deleter = new FileDeleter();
            var dupes = GridResults.ItemsSource as DuplicateFiles;
            if (dupes == null) return;
            var selected = dupes.Where(f => f.MarkedForDeletion);
            deleter.AddFiles(selected.Select(dupe=>dupe.Path));
            deleter.Start(Progress);
            var deletedFiles = (from item in deleter.Results where string.IsNullOrEmpty(item.Message) select item).ToList();
            foreach (var deletedFile in deletedFiles)
            {
                ChangeSelectedSize(dupes.Find(deletedFile.Path), false);
                dupes.DeleteFile(deletedFile.Path);
            }
            var errors = (from item in deleter.Results where !string.IsNullOrEmpty(item.Message) select item).ToList();
            if (errors.Any())
            {
                var form = new FormDeletionErrors();
                form.Init(errors);
                form.ShowDialog();
            }
        }

        private void TableView_CellValueChanged_DeleteIfEmpty(object sender, DevExpress.Xpf.Grid.CellValueChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Value as string))
            {
                var view = sender as DevExpress.Xpf.Grid.TableView;
                view.DeleteRow(e.RowHandle);
            }
        }
        
        private void MenuCopyPathToClipboard_OnItemClick(object sender, ItemClickEventArgs e)
        {
            var file = GridResults.SelectedItem as File;
            if (file == null) return;
            Clipboard.SetData(DataFormats.Text, file.Path);
        }

        private void MenuShowExplorer_OnItemClick(object sender, ItemClickEventArgs e)
        {
            var file = GridResults.SelectedItem as File;
            if (file==null) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
            {
                FileName = file.FolderPath,
                UseShellExecute = true,
                Verb = "open"
            });
        }

        private void MenuOpenSelectedFile_OnItemClick(object sender, ItemClickEventArgs e)
        {
            var file = GridResults.SelectedItem as File;
            if (file == null) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
            {
                FileName = file.Path,
                UseShellExecute = true,
                Verb = "open"
            });
        }

        private void GridResultsView_ShowGridMenu(object sender, DevExpress.Xpf.Grid.GridMenuEventArgs e)
        {
            MenuSelectAllFilesInSelectedFolder.ItemLinks.Clear();
            var file = GridResults.SelectedItem as File;
            if (file == null) return;
            var path = file.Path;
            while (true)
            {
                path = System.IO.Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(path)) break;
                var menu = new BarButtonItem();
                menu.Content = path;
                menu.Tag = path;
                menu.ItemClick+=MenuSelectMatchingFilesOnItemClick;
                MenuSelectAllFilesInSelectedFolder.ItemLinks.Add(menu);
            }
        }

        private void MenuSelectMatchingFilesOnItemClick(object sender, ItemClickEventArgs itemClickEventArgs)
        {
            var menu = sender as BarButtonItem;
            var path = menu.Tag as string;
            var file = GridResults.SelectedItem as File;
            if (file == null) return;
            var files = (IEnumerable<File>)GridResults.ItemsSource;
            foreach (var f in files)
            {
                if (!f.Path.StartsWith(path)) continue;
                if (f.MarkedForDeletion) continue;
                f.MarkedForDeletion = true;
                ChangeSelectedSize(f, true);
            }
        }
    }
}
