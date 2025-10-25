using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Core.Interfaces.Tag
{
    public interface ITagQueryRepository
    {
        Task<IEnumerable<Domain.Models.Tag>> GetTagNamesByPostIdAsync(Guid postId, CancellationToken cancellationToken = default);
    }
}
