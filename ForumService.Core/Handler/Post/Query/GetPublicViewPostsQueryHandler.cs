using AutoMapper;
using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Post;
using ForumService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Query;

namespace ForumService.Core.Handler.Post.Query
{
    public class GetPublicViewPostsQueryHandler : IQueryHandler<GetPublicViewPostsQuery, BaseResponseDto<IEnumerable<PostViewDto>>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IMapper _mapper;

        public GetPublicViewPostsQueryHandler(IGenericRepository<Domain.Models.Post> postRepository, IMapper mapper)
        {
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<BaseResponseDto<IEnumerable<PostViewDto>>> Handle(GetPublicViewPostsQuery request, CancellationToken cancellationToken)
        {
            // 1. Validate limit and offset
            if (request.Limit <= 0 || request.Offset < 0)
            {
                return new BaseResponseDto<IEnumerable<PostViewDto>>
                {
                    Status = 400,
                    Message = "Limit must be positive and Offset must be non-negative.",
                    ResponseData = Enumerable.Empty<PostViewDto>()
                };
            }

            try
            {
                // 2. Xây dựng bộ lọc (filter expression) động
                Expression<Func<Domain.Models.Post, bool>> filter = p => p.Status == "Published" && !p.IsDeleted;

                if (request.AuthorId.HasValue)
                {
                    Guid authorId = request.AuthorId.Value;
                    filter = CombineExpressions(filter, p => p.AuthorId == authorId);
                }

                if (request.CategoryId.HasValue)
                {
                    Guid categoryId = request.CategoryId.Value;
                    filter = CombineExpressions(filter, p => p.CategoryId == categoryId);
                }

                if (!string.IsNullOrWhiteSpace(request.SearchKeyword))
                {
                    var lowerKeyword = request.SearchKeyword.ToLower().Trim();
                    filter = CombineExpressions(filter, p => p.Title.ToLower().Contains(lowerKeyword) ||
                                                             (p.Summary != null && p.Summary.ToLower().Contains(lowerKeyword)));
                }

                if (request.Tags != null && request.Tags.Any())
                {
                    var tags = request.Tags;
                    filter = CombineExpressions(filter, p => p.PostTags.Any(pt => tags.Contains(pt.Tag.Name)));
                }

                // 3. Xây dựng biểu thức sắp xếp (orderBy expression)
                Expression<Func<IQueryable<Domain.Models.Post>, IOrderedQueryable<Domain.Models.Post>>>? orderBy = null;
                var isDescending = request.SortOrder?.ToUpper() == "DESC";

                switch (request.SortBy?.ToLower())
                {
                    case "viewscount":
                        orderBy = q => isDescending ? q.OrderByDescending(p => p.ViewsCount) : q.OrderBy(p => p.ViewsCount);
                        break;
                    case "upvotecount":
                        orderBy = q => isDescending ? q.OrderByDescending(p => p.UpvoteCount) : q.OrderBy(p => p.UpvoteCount);
                        break;
                    case "createdat":
                        orderBy = q => isDescending ? q.OrderByDescending(p => p.CreatedAt) : q.OrderBy(p => p.CreatedAt);
                        break;
                    default:
                        orderBy = q => q.OrderByDescending(p => p.CreatedAt);
                        break;
                }

                // 4. Định nghĩa các navigation properties cần nạp dưới dạng chuỗi
                var includeProperties = new string[] { "Category", "PostTags.Tag" };

                // 5. Tính toán phân trang
                var pageSize = request.Limit;
                var pageNumber = pageSize > 0 ? (request.Offset / pageSize) + 1 : 1;

                // 6. Gọi repository với các biểu thức đã xây dựng
                var posts = await _postRepository.GetListAsyncUntracked<Domain.Models.Post>(
                    filter: filter,
                    orderBy: orderBy,
                    // Thay thế biểu thức Include bằng mảng chuỗi
                    includeProperties: includeProperties,
                    pageSize: pageSize,
                    pageNumber: pageNumber,
                    selector: p => p // Lấy toàn bộ entity Post
                );

                // 7. Map kết quả sang DTO và trả về
                var result = _mapper.Map<IEnumerable<PostViewDto>>(posts);

                return new BaseResponseDto<IEnumerable<PostViewDto>>
                {
                    Status = 200,
                    Message = posts.Any() ? "Posts retrieved successfully." : "No posts found.",
                    ResponseData = result
                };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<IEnumerable<PostViewDto>>
                {
                    Status = 500,
                    Message = $"An error occurred while retrieving posts: {ex.Message}",
                    ResponseData = Enumerable.Empty<PostViewDto>()
                };
            }
        }

        private static Expression<Func<T, bool>> CombineExpressions<T>(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
        {
            var parameter = Expression.Parameter(typeof(T));
            var leftVisitor = new ReplaceExpressionVisitor(left.Parameters[0], parameter);
            var leftBody = leftVisitor.Visit(left.Body);
            var rightVisitor = new ReplaceExpressionVisitor(right.Parameters[0], parameter);
            var rightBody = rightVisitor.Visit(right.Body);
            return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(leftBody, rightBody), parameter);
        }

        private class ReplaceExpressionVisitor : ExpressionVisitor
        {
            private readonly Expression _oldValue;
            private readonly Expression _newValue;

            public ReplaceExpressionVisitor(Expression oldValue, Expression newValue)
            {
                _oldValue = oldValue;
                _newValue = newValue;
            }

            public override Expression Visit(Expression node)
            {
                return node == _oldValue ? _newValue : base.Visit(node);
            }
        }
    }
}
