using FgccHelper.Models;
using System.Linq;
using System.Threading.Tasks;

namespace FgccHelper.Services
{
    public static class ComplexityCalculator
    {
        public static async Task<int> CalculateComplexityAsync(Project project)
        {
            // 模拟异步操作和计算延迟，以便观察到"计算中"状态
            await Task.Delay(1500);

            if (project == null || project.Statistics == null)
            {
                return 0;
            }

            // 从 project.Statistics 中提取各项数据
            double P = project.Statistics.FirstOrDefault(s => s.Name == "页面数量")?.Count ?? 0;
            double T = project.Statistics.FirstOrDefault(s => s.Name == "数据表数量")?.Count ?? 0;
            double B = project.Statistics.FirstOrDefault(s => s.Name == "业务流程数量")?.Count ?? 0;
            double R = project.Statistics.FirstOrDefault(s => s.Name == "报表数量")?.Count ?? 0;
            // 兼容旧版Excel导出名称和程序内部名称，服务端命令（接口）数量 vs 服务端命令数量
            double S = project.Statistics.FirstOrDefault(s => s.Name == "服务端命令数量" || s.Name == "服务端命令（接口）数量")?.Count ?? 0;
            double CP = project.Statistics.FirstOrDefault(s => s.Name == "自定义插件数量")?.Count ?? 0;
            double CC = project.Statistics.FirstOrDefault(s => s.Name == "自定义组件数量")?.Count ?? 0;
            double ST = project.Statistics.FirstOrDefault(s => s.Name == "计划任务数量")?.Count ?? 0;
            double EJ = project.Statistics.FirstOrDefault(s => s.Name == "扩展JavaScript文件数量")?.Count ?? 0;
            double XJ = project.Statistics.FirstOrDefault(s => s.Name == "外部引用的JS文件数量")?.Count ?? 0;
            double XC = project.Statistics.FirstOrDefault(s => s.Name == "外部引用的CSS文件数量")?.Count ?? 0;

            // 权重 (根据之前的讨论)
            double W_P = 5;
            double W_T = 7;
            double W_B = 10;
            double W_R = 3;
            double W_S = 9;
            double W_CP = 8;
            double W_CC = 6;
            double W_ST = 4;
            double W_EJ = 3;
            double W_XJ = 0.5;
            double W_XC = 0.5;
            double W_BT_Interaction = 0.1;
            double W_PCustom_Interaction = 0.05;

            double complexity = (P * W_P) + (T * W_T) + (B * W_B) + (R * W_R) + (S * W_S) +
                                (CP * W_CP) + (CC * W_CC) + (ST * W_ST) + (EJ * W_EJ) +
                                (XJ * W_XJ) + (XC * W_XC) +
                                (B * T * W_BT_Interaction) +
                                (P * (CC + EJ) * W_PCustom_Interaction);

            return (int)System.Math.Round(complexity);
        }
    }
} 