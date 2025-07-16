using FgccHelper.Models;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Configuration; // For App.config if base URL is stored there

namespace FgccHelper.Services
{
    public class LeaderboardApiService
    {
        private static readonly HttpClient httpClient;
        private readonly string _apiBaseUrl;

        // Constants for API configuration
        private const string ApiBaseUrlConfigKey = "LeaderboardApiBaseUrl";
        private const string DefaultApiBaseUrl = "https://www.yugasoft.cn/fgcchelper/ServerCommand"; // 修改这里以更新默认API基地址

        static LeaderboardApiService()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // You might want to set a timeout if not already globally configured
            // httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public LeaderboardApiService()
        {
            _apiBaseUrl = ConfigurationManager.AppSettings[ApiBaseUrlConfigKey] ?? DefaultApiBaseUrl;
            
            if (string.IsNullOrWhiteSpace(_apiBaseUrl) || _apiBaseUrl.Equals(DefaultApiBaseUrl, StringComparison.OrdinalIgnoreCase) && DefaultApiBaseUrl.Contains("127.0.0.1"))
            {
                Console.WriteLine($"Warning: Leaderboard API Base URL is not configured or is using the default placeholder: {_apiBaseUrl}");
            }
        }

        public async Task<SubmitRankingApiResponse> SubmitRankingAsync(RankingSubmissionRequest submissionRequest)
        {
            if (string.IsNullOrWhiteSpace(_apiBaseUrl) || (_apiBaseUrl.Equals(DefaultApiBaseUrl, StringComparison.OrdinalIgnoreCase) && DefaultApiBaseUrl.Contains("127.0.0.1")))
            {
                Console.WriteLine("API not configured or using placeholder. Simulating successful submission.");
                await Task.Delay(500); 
                return new SubmitRankingApiResponse { ErrorCode = 0, Message = "提交成功，复杂度得分：" };
            }

            string apiUrl = $"{_apiBaseUrl.TrimEnd('/')}/submitrankings";
            try
            {
                string jsonRequest = JsonConvert.SerializeObject(submissionRequest);
                HttpContent content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await httpClient.PostAsync(apiUrl, content);

                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return JsonConvert.DeserializeObject<SubmitRankingApiResponse>(jsonResponse) 
                           ?? new SubmitRankingApiResponse { ErrorCode = -1, Message = "无法解析API响应 (成功状态码，但内容为空或无效)" };
                }
                else
                {
                    var errorResponse = JsonConvert.DeserializeObject<SubmitRankingApiResponse>(jsonResponse);
                    if (errorResponse != null && errorResponse.ErrorCode != 0)
                    {
                        return errorResponse;
                    }
                    return new SubmitRankingApiResponse 
                    { 
                        ErrorCode = (int)response.StatusCode, 
                        Message = $"API请求失败: {response.ReasonPhrase} (详情: {jsonResponse})"
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                // Network errors, DNS issues, etc.
                return new SubmitRankingApiResponse { ErrorCode = -100, Message = $"网络请求错误: {ex.Message}" };
            }
            catch (JsonException ex)
            {
                // JSON serialization/deserialization errors
                return new SubmitRankingApiResponse { ErrorCode = -101, Message = $"数据格式错误: {ex.Message}" };
            }
            catch (Exception ex)
            {
                // Other unexpected errors
                return new SubmitRankingApiResponse { ErrorCode = -999, Message = $"发生意外错误: {ex.Message}" };
            }
        }

        public async Task<GetLeaderboardApiResponse> GetLeaderboardAsync(int page = 1)
        {
            if (string.IsNullOrWhiteSpace(_apiBaseUrl) || (_apiBaseUrl.Equals(DefaultApiBaseUrl, StringComparison.OrdinalIgnoreCase) && DefaultApiBaseUrl.Contains("127.0.0.1")))
            {
                Console.WriteLine("API not configured or using placeholder. Simulating empty leaderboard fetch.");
                await Task.Delay(500);
                return new GetLeaderboardApiResponse 
                {
                    ErrorCode = 0, 
                    Message = "获取成功", 
                    Data = new System.Collections.Generic.List<RankingEntry>()
                };
            }

            string apiUrl = $"{_apiBaseUrl.TrimEnd('/')}/getrankings?page={page}";
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(apiUrl);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return JsonConvert.DeserializeObject<GetLeaderboardApiResponse>(jsonResponse) 
                           ?? new GetLeaderboardApiResponse { ErrorCode = -1, Message = "无法解析API响应 (成功状态码，但内容为空或无效)", Data = new System.Collections.Generic.List<RankingEntry>() };
                }
                else
                {
                    var errorResponse = JsonConvert.DeserializeObject<SubmitRankingApiResponse>(jsonResponse); 
                    return new GetLeaderboardApiResponse 
                    { 
                        ErrorCode = errorResponse?.ErrorCode ?? (int)response.StatusCode, 
                        Message = errorResponse?.Message ?? $"API请求失败: {response.ReasonPhrase} (详情: {jsonResponse})",
                        Data = new System.Collections.Generic.List<RankingEntry>()
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                return new GetLeaderboardApiResponse { ErrorCode = -100, Message = $"网络请求错误: {ex.Message}", Data = new System.Collections.Generic.List<RankingEntry>() };
            }
            catch (JsonException ex)
            {
                return new GetLeaderboardApiResponse { ErrorCode = -101, Message = $"数据格式错误: {ex.Message}", Data = new System.Collections.Generic.List<RankingEntry>() };
            }
            catch (Exception ex)
            {
                return new GetLeaderboardApiResponse { ErrorCode = -999, Message = $"发生意外错误: {ex.Message}", Data = new System.Collections.Generic.List<RankingEntry>() };
            }
        }
    }
} 