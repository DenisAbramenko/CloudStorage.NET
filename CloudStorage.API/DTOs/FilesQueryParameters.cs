using System.ComponentModel.DataAnnotations;

namespace CloudStorage.API.DTOs;

public enum FileSortField
{
    CreatedAt,
    Name,
    Size
}

public enum FileSortDirection
{
    Asc,
    Desc
}

public record FilesQueryParameters
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    public const FileSortField DefaultSortBy = FileSortField.CreatedAt;

    public const FileSortDirection DefaultSortDirection = FileSortDirection.Desc;

    [Range(1, int.MaxValue, ErrorMessage = "Номер страницы должен быть больше нуля.")]
    public int Page { get; set; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "Размер страницы должен быть от 1 до 100.")]
    public int PageSize { get; set; } = DefaultPageSize;

    public FileSortField? SortBy { get; set; }

    public FileSortDirection? SortDirection { get; set; }
}