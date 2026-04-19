using Abhyanvaya.Domain.Entities;
using System.Linq;

namespace Abhyanvaya.Application.Common.Extensions
{
    public static class SearchExtensions
    {
        public static IQueryable<Student> ApplySearch(
            this IQueryable<Student> query,
            string search)
        {
            search = search.ToLower();

            return query.Where(x =>
                x.StudentNumber.ToLower().Contains(search) ||
                x.Name.ToLower().Contains(search) ||
                x.MobileNumber.Contains(search));
        }
    }
}
