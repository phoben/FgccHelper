using System.Collections.Generic;

namespace FgccHelper.Models
{
    public class StatisticItem
    {
        public string Name { get; set; }
        public int Count { get; set; }
        public string Description { get; set; }
        public List<DetailEntry> Details { get; set; }

        public StatisticItem()
        {
            Details = new List<DetailEntry>();
        }
    }
} 