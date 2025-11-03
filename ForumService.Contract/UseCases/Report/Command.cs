using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.Report
{
    public static class Command
    {
        /// <summary>
        /// Command to create a new report.
        /// </summary>
        public record CreateReportCommand(
            Guid ReporterId,
            string TargetType,
            Guid TargetId,
            string Reason,
            string? Details
        ) : ICommand<BaseResponseDto<Guid>>;
    }
}
