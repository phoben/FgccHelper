using System.Collections.Generic;
using System.Collections.ObjectModel;
using FgccHelper;

namespace FgccHelper.Models
{
    public class Project
    {
        public string ProjectName { get; set; }
        public string DesignerVersion { get; set; }
        public ObservableCollection<StatisticItem> Statistics { get; set; }

        public FgccHelper.Models.ProjectStatisticsContainer ProjectStats { get; set; }
        public ProjectType ProjectType { get; set; }
        public int ComplexityScore { get; set; }

        public Project()
        {
            Statistics = new ObservableCollection<StatisticItem>();
            ProjectStats = new FgccHelper.Models.ProjectStatisticsContainer();
        }
    }
} 