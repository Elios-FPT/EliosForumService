using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ForumService.Contract.TransferObjects
{

    public class PagedResult<T>
    {
        [JsonPropertyName("content")]
        public List<T> Content { get; set; } = new List<T>();

        [JsonPropertyName("totalElements")]
        public long TotalElements { get; set; }

        [JsonPropertyName("totalPages")]
        public int TotalPages { get; set; }
    }
}
