using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ForumService.Contract.TransferObjects;

namespace ForumService.Core.Interfaces
{
    public interface ISUtilityServiceClient
    {
        Task<string?> UploadFileAsync(string keyPrefix, FileToUploadDto file, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a notification to the Utility Service.
        /// </summary>
        /// <param name="request">The notification payload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SendNotificationAsync(NotificationDto request, CancellationToken cancellationToken);
    }
}
