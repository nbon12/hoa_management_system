namespace HOAManagementCompany.Models
{
    public class PaginationParameters
    {
        private const int MaxPageSize = 100; // Define max page size to prevent abuse
        public int PageNumber { get; set; } = 1;

        private int _pageSize = 10;
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = (value > MaxPageSize) ? MaxPageSize : value;
        }

        public string? SearchTerm { get; set; } // Example: for searching
        public string? OrderBy { get; set; }    // Example: for sorting field
        public bool OrderDesc { get; set; } = false; // Example: for sort direction
    }
} 