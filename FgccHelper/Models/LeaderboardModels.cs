using System.Collections.Generic;
using Newtonsoft.Json; // Or System.Text.Json if preferred and configured

namespace FgccHelper.Models // Assuming models are in FgccHelper.Models namespace
{
    // --- Request Model for Submitting to Leaderboard ---
    public class RankingSubmissionRequest
    {
        [JsonProperty("projectName")]
        public string ProjectName { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("complexityScore")]
        public int ComplexityScore { get; set; } // Assuming score is int, adjust if double/long

        // Detailed stats from the document
        [JsonProperty("pageCount")]
        public int PageCount { get; set; }

        [JsonProperty("tableCount")]
        public int TableCount { get; set; }

        [JsonProperty("businessProcessCount")]
        public int BusinessProcessCount { get; set; }

        [JsonProperty("reportCount")]
        public int ReportCount { get; set; }

        [JsonProperty("serverCommandCount")]
        public int ServerCommandCount { get; set; }

        [JsonProperty("customPluginCount")]
        public int CustomPluginCount { get; set; }

        [JsonProperty("customComponentCount")]
        public int CustomComponentCount { get; set; }

        [JsonProperty("scheduledTaskCount")]
        public int ScheduledTaskCount { get; set; }

        [JsonProperty("extendedJsFileCount")]
        public int ExtendedJsFileCount { get; set; }

        [JsonProperty("externalJsFileCount")]
        public int ExternalJsFileCount { get; set; }

        [JsonProperty("externalCssFileCount")]
        public int ExternalCssFileCount { get; set; }

        [JsonProperty("clientVersion")]
        public string ClientVersion { get; set; } // FgccHelper client version

        [JsonProperty("designerVersion")]
        public string DesignerVersion { get; set; } // Forguncy Designer version
    }

    // --- Response Models for Getting Leaderboard ---
    public class RankingEntry
    {
        [JsonProperty("rank")]
        public int Rank { get; set; } // This might be client-calculated or server-provided

        [JsonProperty("projectName")]
        public string ProjectName { get; set; }

        [JsonProperty("complexityScore")]
        public int ComplexityScore { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("dominanceDuration")]
        public int DominanceDuration { get; set; } // In days

        [JsonProperty("email")]
        public string Email { get; set; } // Optional

        // Calculated property for display, matching XAML binding
        public string DominanceDurationText => $"已霸榜 {DominanceDuration} 天";
    }

    public class LeaderboardData
    {
        [JsonProperty("rankings")]
        public List<RankingEntry> Rankings { get; set; }

        [JsonProperty("totalCount")]
        public int TotalCount { get; set; }
    }

    // Generic API response structure
    public class LeaderboardApiResponse<T>
    {
        [JsonProperty("errorCode")]
        public int ErrorCode { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("data")]
        public T Data { get; set; }
    }

    // Specific type for the GetLeaderboard API response
    public class GetLeaderboardApiResponse : LeaderboardApiResponse<List<RankingEntry>>
    {}

    // Specific type for the Submit API response (if it also returns ErrorCode/Message)
    // If submission response has no specific data part other than ErrorCode/Message,
    // we can use LeaderboardApiResponse<object> or a non-generic version.
    public class SubmitRankingApiResponse
    {
        [JsonProperty("errorCode")]
        public int ErrorCode { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
        // Potentially other fields if the API returns more on submission.
    }
} 