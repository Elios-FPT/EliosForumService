namespace ForumService.Contract.Shared
{
    public class BasePagingDto<T>
    {
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public IEnumerable<T>? PagedData { get; set; }
    }
}
