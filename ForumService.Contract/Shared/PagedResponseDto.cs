using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.Shared
{
    public class PagedResponseDto<T> : BaseResponseDto<T>
    {
        public PaginationMetaData Pagination { get; set; }

        public PagedResponseDto()
        {
        }

        public PagedResponseDto(T data, int page, int pageSize, int totalItems)
        {
            Status = 200;
            Message = "Success";
            ResponseData = data;
            Pagination = new PaginationMetaData
            {
                Page = page <= 0 ? 1 : page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = pageSize > 0 ? (totalItems + pageSize - 1) / pageSize : 0
            };
        }
    }

    public class PaginationMetaData
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }
}
