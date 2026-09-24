using System.Linq.Expressions;
using HotelManagement.Models.Entities;

namespace HotelManagement.Infrastructure.Queries;

public static class GuestQueries
{
    /// <summary>
    /// Guests staff of the given hotels may see: walk-in guests created for one of the hotels,
    /// and any guest with a reservation at one of them.
    /// </summary>
    public static Expression<Func<Guest, bool>> VisibleToHotels(IReadOnlyCollection<int> hotelIds) =>
        g => (g.HotelId.HasValue && hotelIds.Contains(g.HotelId.Value)) ||
             g.Reservations.Any(r => hotelIds.Contains(r.HotelId));

    /// <summary>
    /// Combines two predicates with AND so the result still translates to SQL
    /// </summary>
    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var parameter = left.Parameters[0];
        var rightBody = new ReplaceParameterVisitor(right.Parameters[0], parameter).Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(left.Body, rightBody), parameter);
    }

    private sealed class ReplaceParameterVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly ParameterExpression _to;

        public ReplaceParameterVisitor(ParameterExpression from, ParameterExpression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == _from ? _to : base.VisitParameter(node);
    }
}
