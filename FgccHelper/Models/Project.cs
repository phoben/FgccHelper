using System.Collections.Generic;

namespace FgccHelper.Models
{
    public class Project
    {
        public string ProjectName { get; set; }
        public string DesignerVersion { get; set; }
        public List<StatisticItem> Statistics { get; set; }

        public Project()
        {
            Statistics = new List<StatisticItem>();
        }
    }
} 