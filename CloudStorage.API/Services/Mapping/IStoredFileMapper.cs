using CloudStorage.API.DTOs;
using CloudStorage.API.Models;

namespace CloudStorage.API.Services.Mapping;

public interface IStoredFileMapper
{
    StoredFileDto ToDto(StoredFile file);

    List<StoredFileDto> ToDtoList(IEnumerable<StoredFile> files);

    PagedResult<StoredFileDto> ToDtoPage(PagedResult<StoredFile> page);
}