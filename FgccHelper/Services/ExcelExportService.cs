using ClosedXML.Excel;
using FgccHelper.Models; // Assuming Project, StatisticItem, DetailEntry are here
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FgccHelper.Services
{
    public class ExcelExportService
    {
        public async Task ExportProjectToExcelAsync(Project projectData, string filePath)
        {
            await Task.Run(() =>
            {
                using (var workbook = new XLWorkbook())
                {
                    // --- Sheet 1: 项目概况 ---
                    var overviewSheet = workbook.Worksheets.Add("项目概况");

                    // Project Info
                    overviewSheet.Cell("A1").Value = "项目名称:";
                    overviewSheet.Cell("A1").Style.Font.Bold = true;
                    overviewSheet.Cell("B1").Value = projectData.ProjectName;

                    overviewSheet.Cell("A2").Value = "设计器版本:";
                    overviewSheet.Cell("A2").Style.Font.Bold = true;
                    overviewSheet.Cell("B2").Value = projectData.DesignerVersion;

                    // Statistics Overview Table Headers (Row 4)
                    overviewSheet.Cell("A4").Value = "统计项名称";
                    overviewSheet.Cell("B4").Value = "数量";
                    overviewSheet.Cell("C4").Value = "描述";
                    var overviewHeaderRow = overviewSheet.Range("A4:C4");
                    overviewHeaderRow.Style.Font.Bold = true;
                    overviewHeaderRow.Style.Fill.BackgroundColor = XLColor.LightGray;

                    // Statistics Data
                    int currentRow = 5;
                    if (projectData.Statistics != null)
                    {
                        foreach (var statItem in projectData.Statistics)
                        {
                            overviewSheet.Cell(currentRow, 1).Value = statItem.Name;
                            overviewSheet.Cell(currentRow, 2).Value = statItem.Count;
                            overviewSheet.Cell(currentRow, 3).Value = statItem.Description;
                            currentRow++;
                        }
                    }
                    overviewSheet.Columns().AdjustToContents(); // Adjust column widths for overview sheet

                    // --- Subsequent Sheets: Details for each StatisticItem ---
                    if (projectData.Statistics != null)
                    {
                        foreach (var statItem in projectData.Statistics)
                        {
                            // Create sheet only if there are details
                            if (statItem.Details != null && statItem.Details.Any())
                            {
                                // Sheet name might need sanitization if it contains invalid characters for Excel sheet names
                                string sheetName = SanitizeSheetName(statItem.Name);
                                var detailSheet = workbook.Worksheets.Add(sheetName);

                                // Detail Table Headers (Row 1)
                                detailSheet.Cell("A1").Value = "名称";
                                detailSheet.Cell("B1").Value = "大小";
                                detailSheet.Cell("C1").Value = "文件类型";
                                var detailHeaderRow = detailSheet.Range("A1:C1");
                                detailHeaderRow.Style.Font.Bold = true;
                                detailHeaderRow.Style.Fill.BackgroundColor = XLColor.LightGray;

                                // Detail Data
                                int detailCurrentRow = 2;
                                foreach (var detailEntry in statItem.Details)
                                {
                                    detailSheet.Cell(detailCurrentRow, 1).Value = detailEntry.Name;
                                    detailSheet.Cell(detailCurrentRow, 2).Value = detailEntry.Size;
                                    detailSheet.Cell(detailCurrentRow, 3).Value = detailEntry.FileType;
                                    detailCurrentRow++;
                                }
                                detailSheet.Columns().AdjustToContents(); // Adjust column widths for detail sheet
                            }
                        }
                    }
                    workbook.SaveAs(filePath);
                }
            });
        }

        // Excel sheet names have restrictions (e.g., length <= 31, no certain chars like / \ ? * [ ] )
        private string SanitizeSheetName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return "DefaultSheet";

            string sanitized = rawName;
            // Remove invalid characters
            char[] invalidChars = { '/', '\\', '?', '*', '[', ']', ':' }; // Add more if needed
            foreach (char c in invalidChars)
            {
                sanitized = sanitized.Replace(c.ToString(), "");
            }

            // Trim to max length (31 characters)
            if (sanitized.Length > 31)
            {
                sanitized = sanitized.Substring(0, 31);
            }
            // Ensure not empty after sanitization
            return string.IsNullOrWhiteSpace(sanitized) ? "Sheet" : sanitized;
        }
    }
} 