using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace OrderServiceGrpc.Helpers
{
    public static class EfExtensions
    {
        public static IQueryable<T> WithTracking<T>(this IQueryable<T> source, bool track) where T : class
        {
            return track ? source.AsNoTracking() : source;
        }
    }
}
