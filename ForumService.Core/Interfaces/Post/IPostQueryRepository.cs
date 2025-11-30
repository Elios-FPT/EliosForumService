using ForumService.Contract.TransferObjects.Comment;
using ForumService.Contract.TransferObjects.Post;
using ForumService.Domain.Models;
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
        Task<(IEnumerable<Domain.Models.Post> Posts, int TotalCount)> GetPublicViewPostsAsync(GetPublicViewPostsQuery query);
        Task<(IEnumerable<Domain.Models.Post> Posts, int TotalCount)> GetModeratorPublicViewPostsAsync(GetModeratorPublicPostsQuery query);
        Task<(IEnumerable<Domain.Models.Post> Posts, int TotalCount)> GetPendingPostsAsync(GetPendingPostsQuery query);
        Task<(IEnumerable<Domain.Models.Post> Posts, int TotalCount)> GetArchivedPostsAsync(GetArchivedPostsQuery query);
        Task<(IEnumerable<Domain.Models.Post> Posts, int TotalCount)> GetMyPostsAsync(GetMyPostsQuery request);
    }
}
