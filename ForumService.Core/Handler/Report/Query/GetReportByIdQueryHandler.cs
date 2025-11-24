using ForumService.Contract.Message;
using ForumService.Contract.Models; 
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Report; 
using ForumService.Core.Interfaces;
using ForumService.Core.Interfaces.Post;
using ForumService.Domain.Models;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Report.Query;

namespace ForumService.Core.Handler.Report.Query
{
    public class GetReportByIdQueryHandler : IQueryHandler<GetReportByIdQuery, BaseResponseDto<ReportDto>>
    {
        private readonly IGenericRepository<Domain.Models.Report> _reportRepository;
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Domain.Models.Comment> _commentRepository;
        private readonly IKafkaProducerRepository<User> _producerRepository;

        private const string ResponseTopic = "user-forum-user";
        private const string DestinationService = "user";

        public GetReportByIdQueryHandler(
            IGenericRepository<Domain.Models.Report> reportRepository,
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.Comment> commentRepository,
            IKafkaProducerRepository<User> producerRepository)
        {
            _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _commentRepository = commentRepository ?? throw new ArgumentNullException(nameof(commentRepository));
            _producerRepository = producerRepository ?? throw new ArgumentNullException(nameof(producerRepository));
        }

        public async Task<BaseResponseDto<ReportDto>> Handle(GetReportByIdQuery request, CancellationToken cancellationToken)
        {
            var report = await _reportRepository.GetByIdAsync(request.ReportId);

            if (report == null)
            {
                return new BaseResponseDto<ReportDto>
                {
                    Status = 404,
                    Message = "Report not found.",
                    ResponseData = null
                };
            }

            var reportDto = new ReportDto
            {
                ReportId = report.ReportId,
                Status = report.Status,
                Reason = report.Reason,
                Details = report.Details,
                CreatedAt = report.CreatedAt,
                ResolvedAt = report.ResolvedAt,
                TargetType = report.TargetType,
                TargetId = report.TargetId,
                ReporterId = report.ReporterId,
                ResolvedBy = report.ResolvedBy
            };

            Guid targetAuthorId = Guid.Empty;

            if (report.TargetType == "Post")
            {
                var post = await _postRepository.GetByIdAsync(report.TargetId);
                if (post != null)
                {
                    reportDto.TargetContentSnippet = post.Content.Length > 100
                        ? post.Content.Substring(0, 100) + "..."
                        : post.Content;

                    reportDto.TargetAuthorId = post.AuthorId;
                    targetAuthorId = post.AuthorId;
                }
                else
                {
                    reportDto.TargetContentSnippet = "[Content Deleted or Not Found]";
                }
            }
            else if (report.TargetType == "Comment")
            {
                var comment = await _commentRepository.GetByIdAsync(report.TargetId);
                if (comment != null)
                {
                    reportDto.TargetContentSnippet = comment.Content.Length > 100
                        ? comment.Content.Substring(0, 100) + "..."
                        : comment.Content;

                    reportDto.TargetAuthorId = comment.AuthorId;
                    targetAuthorId = comment.AuthorId;
                }
                else
                {
                    reportDto.TargetContentSnippet = "[Content Deleted or Not Found]";
                }
            }

            var userIdsToFetch = new HashSet<Guid> { report.ReporterId };

            if (targetAuthorId != Guid.Empty) userIdsToFetch.Add(targetAuthorId);
            if (report.ResolvedBy.HasValue) userIdsToFetch.Add(report.ResolvedBy.Value);

            Dictionary<Guid, User> userMap;
            try
            {
                var allUsers = await _producerRepository.ProduceGetAllAsync(DestinationService, ResponseTopic);
                userMap = allUsers.Where(u => userIdsToFetch.Contains(u.id)).ToDictionary(u => u.id);
            }
            catch (Exception)
            {
                userMap = new Dictionary<Guid, User>();
            }

            // Reporter Info
            if (userMap.TryGetValue(report.ReporterId, out var reporter))
            {
                reportDto.ReporterFirstName = reporter.firstName;
                reportDto.ReporterLastName = reporter.lastName;
                reportDto.ReporterAvatarUrl = reporter.avatarUrl;
            }

            // ResolvedBy Info
            if (report.ResolvedBy.HasValue && userMap.TryGetValue(report.ResolvedBy.Value, out var resolver))
            {
                reportDto.ResolvedByFirstName = resolver.firstName;
                reportDto.ResolvedByLastName = resolver.lastName;
            }

            // Target Author Info
            if (targetAuthorId != Guid.Empty && userMap.TryGetValue(targetAuthorId, out var targetAuthor))
            {
                reportDto.TargetAuthorFirstName = targetAuthor.firstName;
                reportDto.TargetAuthorLastName = targetAuthor.lastName;
                reportDto.TargetAuthorAvatarUrl = targetAuthor.avatarUrl;
            }

            return new BaseResponseDto<ReportDto>
            {
                Status = 200,
                Message = "Report details retrieved successfully.",
                ResponseData = reportDto
            };
        }
    }
}