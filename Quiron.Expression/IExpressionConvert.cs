using System.Linq.Expressions;

namespace Quiron.Expression
{
    public interface IExpressionConvert
    {
        Expression<Func<TTarget, bool>> Builder<TSource, TTarget>(Expression<Func<TSource, bool>> sourceExpression
            , string findIn = "", Type? typeCast = null);

        Expression<Func<TTarget, object>>[] ConvertIncludesExpression<TSource, TTarget>(Expression<Func<TSource, object>>[] viewModelIncludes
            , string findIn = "", string[]? includeProperty = null);

        Expression<Func<TTarget, object>>[]? ConvertIncludesExpression<TTarget>(string[]? includeProperty
            , ParameterExpression? parameter = null);

        Expression<Func<T, bool>> CreateCustomFilters<T>(
            IEnumerable<(string PropertyName, object? Value, ExpressionType Operator)> conditions);

        Expression<Func<T, bool>> CreateCustomFilters<T>(
            IEnumerable<(string PropertyName, object? Value, ExpressionType Operator, ExpressionType AndOrET)> conditions);
    }
}