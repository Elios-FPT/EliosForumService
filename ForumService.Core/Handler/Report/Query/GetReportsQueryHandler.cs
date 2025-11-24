using ForumService.Contract.Message;
using ForumService.Contract.Models; 
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Report; 
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Report.Query;

namespace ForumService.Core.Handler.Report.Query
{
    public class GetReportsQueryHandler : IQueryHandler<GetReportsQuery, BaseResponseDto<IEnumerable<ReportDto>>>
    {
        private readonly IGenericRepository<Domain.Models.Report> _reportRepository;
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Domain.Models.Comment> _commentRepository;
        private readonly IKafkaProducerRepository<User> _producerRepository; 

        private const string ResponseTopic = "user-forum-user";
        private const string DestinationService = "user";

        public GetReportsQueryHandler(
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

        public async Task<BaseResponseDto<IEnumerable<ReportDto>>> Handle(GetReportsQuery request, CancellationToken cancellationToken)
        {
            if (request.Limit <= 0 || request.Offset < 0)
            {
                return new BaseResponseDto<IEnumerable<ReportDto>>
                {
                    Status = 400,
                    Message = "Limit must be positive and Offset must be non-negative.",
                    ResponseData = Enumerable.Empty<ReportDto>()
                };
            }

            try
            {
                Expression<Func<Domain.Models.Report, bool>> filter = r =>
                    (string.IsNullOrEmpty(request.Status) || r.Status == request.Status) &&
                    (string.IsNullOrEmpty(request.TargetType) || r.TargetType == request.TargetType) &&
                    (!request.ReporterId.HasValue || r.ReporterId == request.ReporterId) &&
                    (!request.TargetId.HasValue || r.TargetId == request.TargetId);

                int pageSize = request.Limit;
                int pageNumber = (request.Offset / request.Limit) + 1;

                Func<IQueryable<Domain.Models.Report>, IOrderedQueryable<Domain.Models.Report>> orderBy = q => q.OrderByDescending(r => r.CreatedAt);
                if (!string.IsNullOrEmpty(request.SortBy))
                {
                    bool isAscending = string.Equals(request.SortOrder, "ASC", StringComparison.OrdinalIgnoreCase);
                    if (request.SortBy.Equals("CreatedAt", StringComparison.OrdinalIgnoreCase))
                        orderBy = isAscending ? q => q.OrderBy(r => r.CreatedAt) : q => q.OrderByDescending(r => r.CreatedAt);
                    else if (request.SortBy.Equals("Status", StringComparison.OrdinalIgnoreCase))
                        orderBy = isAscending ? q => q.OrderBy(r => r.Status) : q => q.OrderByDescending(r => r.Status);
                }

                Expression<Func<IQueryable<Domain.Models.Report>, IOrderedQueryable<Domain.Models.Report>>> orderByExpression = q => orderBy(q);

                var reportsList = (await _reportRepository.GetListAsync(
                    filter: filter,
                    orderBy: orderByExpression,
                    pageNumber: pageNumber,
                    pageSize: pageSize
                )).ToList();

                if (!reportsList.Any())
                {
                    return new BaseResponseDto<IEnumerable<ReportDto>>
                    {
                        Status = 200,
                        Message = "No reports found.",
                        ResponseData = Enumerable.Empty<ReportDto>()
                    };
                }

                var postIds = reportsList.Where(r => r.TargetType == "Post").Select(r => r.TargetId).Distinct().ToList();
                var commentIds = reportsList.Where(r => r.TargetType == "Comment").Select(r => r.TargetId).Distinct().ToList();

                var posts = postIds.Any() 
                    ? await _postRepository.GetListAsync(p => postIds.Contains(p.PostId)) 
                    : Enumerable.Empty<Domain.Models.Post>();

                var comments = commentIds.Any() 
                    ? await _commentRepository.GetListAsync(c => commentIds.Contains(c.CommentId)) 
                    : Enumerable.Empty<Domain.Models.Comment>();

                var postMap = posts.ToDictionary(p => p.PostId);
                var commentMap = comments.ToDictionary(c => c.CommentId);

                var userIdsToFetch = new HashSet<Guid>();
                userIdsToFetch.UnionWith(reportsList.Select(r => r.ReporterId));
                userIdsToFetch.UnionWith(reportsList.Where(r => r.ResolvedBy.HasValue).Select(r => r.ResolvedBy.Value));
                userIdsToFetch.UnionWith(posts.Select(p => p.AuthorId));
                userIdsToFetch.UnionWith(comments.Select(c => c.AuthorId));

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

                var reportDtos = new List<ReportDto>();

                foreach (var report in reportsList)
                {
                    var dto = new ReportDto
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
                        ResolvedBy = report.ResolvedBy,
                        ModeratorNote  = report.ModeratorNote
                    };

                    Guid targetAuthorId = Guid.Empty;
                    if (report.TargetType == "Post" && postMap.TryGetValue(report.TargetId, out var post))
                    {
                        dto.TargetContentSnippet = post.Content.Length > 100 ? post.Content.Substring(0, 100) + "..." : post.Content;
                        dto.TargetAuthorId = post.AuthorId;
                        targetAuthorId = post.AuthorId;
                        dto.TargetContentDetail = post.Content;
                    }
                    else if (report.TargetType == "Comment" && commentMap.TryGetValue(report.TargetId, out var comment))
                    {
                        dto.TargetContentSnippet = comment.Content.Length > 100 ? comment.Content.Substring(0, 100) + "..." : comment.Content;
                        dto.TargetAuthorId = comment.AuthorId;
                        targetAuthorId = comment.AuthorId;
                        dto.TargetContentDetail = comment.Content;
                    }

                    if (userMap.TryGetValue(report.ReporterId, out var reporter))
                    {
                        dto.ReporterFirstName = reporter.firstName;
                        dto.ReporterLastName = reporter.lastName;
                        dto.ReporterAvatarUrl = reporter.avatarUrl;
                    }

                    if (report.ResolvedBy.HasValue && userMap.TryGetValue(report.ResolvedBy.Value, out var resolver))
                    {
                        dto.ResolvedByFirstName = resolver.firstName;
                        dto.ResolvedByLastName = resolver.lastName;
                    }

                    if (targetAuthorId != Guid.Empty && userMap.TryGetValue(targetAuthorId, out var targetAuthor))
                    {
                        dto.TargetAuthorFirstName = targetAuthor.firstName;
                        dto.TargetAuthorLastName = targetAuthor.lastName;
                        dto.TargetAuthorAvatarUrl = targetAuthor.avatarUrl;
                    }

                    reportDtos.Add(dto);
                }

                return new BaseResponseDto<IEnumerable<ReportDto>>
                {
                    Status = 200,
                    Message = "Reports retrieved successfully.",
                    ResponseData = reportDtos
                };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<IEnumerable<ReportDto>>
                {
                    Status = 500,
                    Message = $"An error occurred: {ex.Message}",
                    ResponseData = Enumerable.Empty<ReportDto>()
                };
            }
        }
    }
}