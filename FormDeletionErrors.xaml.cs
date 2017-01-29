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
using System.Windows.Shapes;
using DuplicateFinderLib;

namespace DuplicateFinder
{
    /// <summary>
    /// Interaction logic for FormDeletionErrors.xaml
    /// </summary>
    public partial class FormDeletionErrors : Window
    {
        public FormDeletionErrors()
        {
            InitializeComponent();
        }
        public void Init(List<FileDeleterItem> results)
        {
            GridMessages.ItemsSource = results;
            ((DevExpress.Xpf.Grid.TableView)GridMessages.View).BestFitColumns();
        }
    }
}
