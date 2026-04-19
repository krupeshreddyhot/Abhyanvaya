using Abhyanvaya.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Common.Extensions
{
    public static class QueryableExtensions
    {
        public static async Task<PagedResponse<T>> ToPagedAsync<T>(
            this IQueryable<T> query,
            int pageNumber,
            int pageSize)
        {
            var total = await query.CountAsync();

            var data = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResponse<T>
            {
                TotalCount = total,
                Data = data
            };
        }
    }
}