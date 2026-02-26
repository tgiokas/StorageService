using StorageService.Application.Dtos;
using StorageService.Application.Interfaces;

namespace StorageService.Application.Errors;

public static class ErrorCatalogExtensions
{
    public static Result<T> Fail<T>(this IErrorCatalog errors, string code)
    {
        var e = errors.GetError(code);
        return Result<T>.Fail(e.Message, e.Code);
    }
}
