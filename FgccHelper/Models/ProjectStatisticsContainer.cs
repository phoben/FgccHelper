namespace FgccHelper.Models
{
    public class ProjectStatisticsContainer
    {
        public int PageCount { get; set; }
        public int TableCount { get; set; }
        public int BusinessProcessCount { get; set; }
        public int ReportCount { get; set; }
        public int ServerCommandCount { get; set; }
        public int CustomPluginCount { get; set; }
        public int CustomComponentCount { get; set; }
        public int ScheduledTaskCount { get; set; }
        public int ExtendedJsFileCount { get; set; }
        public int ExternalJsFileCount { get; set; }
        public int ExternalCssFileCount { get; set; }

        // Add a constructor or initialization method if needed, 
        // e.g., to set defaults or populate from another source.
        public ProjectStatisticsContainer()
        {
            // Initialize with default values if necessary
            PageCount = 0;
            TableCount = 0;
            BusinessProcessCount = 0;
            ReportCount = 0;
            ServerCommandCount = 0;
            CustomPluginCount = 0;
            CustomComponentCount = 0;
            ScheduledTaskCount = 0;
            ExtendedJsFileCount = 0;
            ExternalJsFileCount = 0;
            ExternalCssFileCount = 0;
        }
    }
} 