using FgccHelper.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FgccHelper.Services
{
    public static class ComplexityCalculator
    {
        public static async Task<int> CalculateComplexityAsync(Project project)
        {
            // 模拟异步操作和计算延迟，以便观察到"计算中"状态
            await Task.Delay(1000);

            if (project == null || project.Statistics == null)
            {
                return 0;
            }

            // 从 project.Statistics 中提取各项数据
            double P = project.Statistics.FirstOrDefault(s => s.Name == "页面数量")?.Count ?? 0;
            double T = project.Statistics.FirstOrDefault(s => s.Name == "数据表数量")?.Count ?? 0;
            double B = project.Statistics.FirstOrDefault(s => s.Name == "流程数量")?.Count ?? 0;
            double R = project.Statistics.FirstOrDefault(s => s.Name == "报表数量")?.Count ?? 0;
            double S = project.Statistics.FirstOrDefault(s => s.Name == "服务端命令")?.Count ?? 0;
            double CP = project.Statistics.FirstOrDefault(s => s.Name == "自定义插件数量")?.Count ?? 0;
            double CC = project.Statistics.FirstOrDefault(s => s.Name == "自定义组件数量")?.Count ?? 0;
            double ST = project.Statistics.FirstOrDefault(s => s.Name == "计划任务数量")?.Count ?? 0;
            double EJ = project.Statistics.FirstOrDefault(s => s.Name == "扩展JavaScript数量")?.Count ?? 0;
            double XJ = project.Statistics.FirstOrDefault(s => s.Name == "外部JS文件数量")?.Count ?? 0;
            double XC = project.Statistics.FirstOrDefault(s => s.Name == "外部CSS文件数量")?.Count ?? 0;

            // 基础权重
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

            // 交互权重
            double W_BT_Interaction = 0.1; //业务流程与数据表交互
            double W_PCustom_Interaction = 0.05; // 新增：页面与自定义插件交互
            double W_ST_Interaction = 0.08;  // 新增：服务端命令与数据表交互
            double W_BCP_Interaction = 0.07; // 新增：业务流程与自定义插件交互

            // 计算项目总规模用于归一化
            double totalElements = P + T + B + R + S + CP + CC + ST + EJ + XJ + XC;
            double scaleFactor = totalElements > 0 ? Math.Sqrt(totalElements) / 10 : 1;

            // 对异常高值应用非线性转换，避免单一指标过度影响复杂度
            P = TransformValue(P, 50);
            T = TransformValue(T, 30);
            B = TransformValue(B, 25);
            S = TransformValue(S, 40);
            CP = TransformValue(CP, 20);
            CC = TransformValue(CC, 30);

            // 基础复杂度计算（考虑边际递减效应）
            double baseComplexity =
                (P <= 20 ? P * W_P : 20 * W_P + (P - 20) * W_P * 0.7) +
                (T <= 15 ? T * W_T : 15 * W_T + (T - 15) * W_T * 0.7) +
                (B <= 10 ? B * W_B : 10 * W_B + (B - 10) * W_B * 0.8) +
                (R * W_R) +
                (S <= 30 ? S * W_S : 30 * W_S + (S - 30) * W_S * 0.6) +
                (CP <= 10 ? CP * W_CP : 10 * W_CP + (CP - 10) * W_CP * 0.75) +
                (CC * W_CC) +
                (ST * W_ST) +
                (EJ * W_EJ) +
                (XJ * W_XJ) +
                (XC * W_XC);

            // 交互复杂度计算（增强交互模型）
            double interactionComplexity =
                (B * T * W_BT_Interaction) +
                (P * (CC + EJ) * W_PCustom_Interaction) +
                (S * T * W_ST_Interaction) +         // 新增：服务端命令与数据表交互
                (B * CP * W_BCP_Interaction);        // 新增：业务流程与自定义插件交互

            // 应用规模因子进行归一化调整
            double adjustedInteractionComplexity = interactionComplexity * (1 + Math.Log10(scaleFactor));

            // 计算最终复杂度
            double complexity = baseComplexity + adjustedInteractionComplexity;

            // 根据项目规模进行最终调整，防止过大或过小项目的极端值
            if (totalElements > 100)
            {
                complexity = complexity * (1 + Math.Log10(totalElements / 100) * 0.2);
            }

            return (int)Math.Round(complexity);
        }

        // 辅助方法：对超过阈值的数值进行对数转换
        private static double TransformValue(double value, double threshold)
        {
            if (value <= threshold) return value;
            return threshold + Math.Log10(value - threshold + 1) * threshold / 2;
        }

    }
} 