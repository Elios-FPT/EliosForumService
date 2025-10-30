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
        Task<IEnumerable<Domain.Models.Post>> GetPublicViewPostsAsync(GetPublicViewPostsQuery query);
        Task<IEnumerable<Domain.Models.Post>> GetModeratorPublicViewPostsAsync(GetModeratorPublicPostsQuery query);
        Task<IEnumerable<Domain.Models.Post>> GetPendingPostsAsync(GetPendingPostsQuery query);
        Task<IEnumerable<Domain.Models.Post>> GetArchivedPostsAsync(GetArchivedPostsQuery query);
        Task<IEnumerable<Domain.Models.Post>> GetMyPostsAsync(GetMyPostsQuery request);
    }
}
