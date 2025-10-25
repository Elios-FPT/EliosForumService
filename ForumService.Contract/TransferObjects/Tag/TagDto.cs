using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.TransferObjects.Tag
{
    /// <summary>
    /// Represents basic information about a tag.
    /// </summary>
    public class TagDto
    {
        public Guid TagId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = null!;
    }
}
