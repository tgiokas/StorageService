using Storage.Application.Dtos;
using Storage.Application.Interfaces;

namespace Storage.Application.Errors;

public static class ErrorCatalogExtensions
{
    public static Result<T> Fail<T>(this IErrorCatalog errors, string code)
    {
        var e = errors.GetError(code);
        return Result<T>.Fail(e.Message, e.Code);
    }
}
