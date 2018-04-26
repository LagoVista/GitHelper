using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using System.ComponentModel;

namespace LagoVista.GitHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;

            _vm = new MainViewModel(Dispatcher, @"D:\nuviot");
            Page.DataContext = _vm;
            
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _vm.ScanNow();
        }


        TreeViewItem _previousTreeItem = null;

        private void TreeViewItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (_previousTreeItem != null)
            {
                _previousTreeItem.IsSelected = false;
            }
            
            var treeViewItem = sender as TreeViewItem;
            treeViewItem.IsSelected = true;
            _previousTreeItem = treeViewItem;

            var fileStatus = (treeViewItem.DataContext as GitFileStatus);
            _vm.CurrentFolder = null;
            _vm.CurrentFile = fileStatus;
        }

        private void TreeViewItem_MouseLeftButtonUp_StagedFiles(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (_previousTreeItem != null)
            {
                _previousTreeItem.IsSelected = false;
            }

            var treeViewItem = sender as TreeViewItem;
            treeViewItem.IsSelected = true;
            _previousTreeItem = treeViewItem;

            _vm.CurrentFile = null;
             _vm.CurrentFolder = treeViewItem.DataContext as GitManagedFolder;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _vm.ScanNow();
        }

        private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            /* Prevents selecting parent nodes */
            TreeViewItem tvi = FolderTree.ItemContainerGenerator.ContainerFromIndex(0) as TreeViewItem;
            if (tvi != null && tvi.IsSelected) { tvi.IsSelected = true; tvi.IsSelected = false; }
        }

    }
}
