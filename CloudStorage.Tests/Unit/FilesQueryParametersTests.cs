using System.ComponentModel.DataAnnotations;
using CloudStorage.API.DTOs;
using Xunit;

namespace CloudStorage.Tests.Unit;

public class FilesQueryParametersTests
{
    private static List<ValidationResult> Validate(FilesQueryParameters parameters)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            parameters,
            new ValidationContext(parameters),
            results,
            validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Defaults_AreValid()
    {
        Assert.Empty(Validate(new FilesQueryParameters()));
    }

    [Fact]
    public void MaxPageSize_IsValid()
    {
        Assert.Empty(Validate(new FilesQueryParameters { PageSize = FilesQueryParameters.MaxPageSize }));
    }

    [Fact]
    public void PageBelowOne_IsInvalid()
    {
        var results = Validate(new FilesQueryParameters { Page = 0 });

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(FilesQueryParameters.Page)));
    }

    [Fact]
    public void PageSizeBelowOne_IsInvalid()
    {
        var results = Validate(new FilesQueryParameters { PageSize = 0 });

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(FilesQueryParameters.PageSize)));
    }

    [Fact]
    public void PageSizeAboveMaximum_IsInvalid()
    {
        var results = Validate(new FilesQueryParameters { PageSize = FilesQueryParameters.MaxPageSize + 1 });

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(FilesQueryParameters.PageSize)));
    }
}
