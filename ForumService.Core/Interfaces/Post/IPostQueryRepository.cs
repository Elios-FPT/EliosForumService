using ForumService.Contract.TransferObjects.Comment;
using ForumService.Contract.TransferObjects.Post;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Query;

namespace ForumService.Core.Interfaces.Post
{
    public interface IPostQueryRepository
    {
        Task<IEnumerable<PostViewDto>> GetPublicViewPostsAsync(GetPublicViewPostsQuery query);
        Task<IEnumerable<PostViewDto>> GetPendingPostsAsync(GetPendingPostsQuery query);
        Task<IEnumerable<PostViewDto>> GetArchivedPostsAsync(GetArchivedPostsQuery query);
        Task<IEnumerable<PostViewDto>> GetMyPostsAsync(GetMyPostsQuery request);
        Task<(PostViewDetailDto? Post, IEnumerable<CommentDto> Comments)> GetPostDetailsByIdAsync(Guid postId);
    }
}
