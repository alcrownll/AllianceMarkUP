using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels
{
    public sealed class PagedResultModel<T>
    {
        public IReadOnlyList<T> Items { get; init; } = new List<T>();
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
    }
}
