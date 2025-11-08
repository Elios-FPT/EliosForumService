using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using ForumService.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json; 
using System.Text;
using System.Threading.Tasks;
using System.Threading; 

namespace ForumService.Infrastructure.Implementations
{
    public class SUtilityServiceClient : ISUtilityServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SUtilityServiceClient> _logger;

        public SUtilityServiceClient(HttpClient httpClient, ILogger<SUtilityServiceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string?> UploadFileAsync(string keyPrefix, FileToUploadDto file, CancellationToken cancellationToken = default)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var fileContent = new ByteArrayContent(file.Content);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);

                content.Add(fileContent, "file", file.FileName);

                var requestUri = $"api/v1/Storage?keyPrefix={Uri.EscapeDataString(keyPrefix)}&fileName={Uri.EscapeDataString(file.FileName)}";
                var response = await _httpClient.PostAsync(requestUri, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to upload file '{FileName}'. Status: {StatusCode}. Response: {ErrorContent}", file.FileName, response.StatusCode, errorContent);
                    return null;
                }

                var result = await response.Content.ReadFromJsonAsync<BaseResponseDto<string>>(cancellationToken: cancellationToken);
                return result?.ResponseData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while uploading file '{FileName}'", file.FileName);
                return null;
            }
        }


        /// <summary>
        /// Implements the interface method to send a notification.
        /// </summary>
        public async Task SendNotificationAsync(NotificationDto request, CancellationToken cancellationToken)
        {
            const string requestUri = "api/v1/Notification";
            try
            {
                var response = await _httpClient.PostAsJsonAsync(requestUri, request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to send notification. Status: {StatusCode}. Response: {ErrorContent}. Payload: {Payload}",
                        response.StatusCode,
                        errorContent,
                        System.Text.Json.JsonSerializer.Serialize(request));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending notification to {RequestUri}. Payload: {Payload}",
                    requestUri,
                    System.Text.Json.JsonSerializer.Serialize(request));
            }
        }
    }
}