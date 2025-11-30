using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using ForumService.Contract.TransferObjects.Report;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.Report
{
    public static class Query
    {
        /// <summary>
        /// Query to get a paginated list of reports with filters and sorting.
        /// Mapped from GetReportsRequest.
        /// </summary>
        public record GetReportsQuery(
            int Page,
            int Size,
            string? Status,
            string? TargetType,
            Guid? ReporterId,
            Guid? TargetId,
            string? SortBy,
            string? SortOrder
        ) : IQuery<PagedResponseDto<IEnumerable<ReportDto>>>;

        public record GetReportByIdQuery(
            Guid ReportId
        ) : IQuery<BaseResponseDto<ReportDto>>;
    }
}
