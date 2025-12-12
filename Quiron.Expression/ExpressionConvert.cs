using System.Linq.Expressions;

namespace Quiron.Expression
{
    public class ExpressionConvert : IExpressionConvert
    {
        #region Public methods
        public virtual Expression<Func<TTarget, bool>> Builder<TSource, TTarget>(Expression<Func<TSource, bool>> sourceExpression
            , string findIn = "", Type? typeCast = null)
        {
            ArgumentNullException.ThrowIfNull(sourceExpression.Parameters);

            var conditions = new List<(string property, ExpressionType operatorx, object? value, ExpressionType expressionType)>();
            ParseExpressionFilters(sourceExpression.Body, conditions, ExpressionType.Default);

            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(TTarget), "find");
            System.Linq.Expressions.Expression? finalExpression = null;

            foreach (var (property, operatorx, value, expressionType) in conditions)
            {
                var propertyIn = PropertyExist(parameter, property);
                if (propertyIn is null && !string.IsNullOrEmpty(findIn))
                {
                    var findInArray = findIn.Split('.');
                    var propertyRootClass = PropertyExist(parameter, findInArray[0]);

                    ArgumentNullException.ThrowIfNull(propertyRootClass);

                    if (findInArray.Length > 1)
                    {
                        for (int i = 1; i < findInArray.Length; i++)
                        {
                            propertyRootClass = PropertyExist(propertyRootClass!, findInArray[i]);
                            if (propertyRootClass is not null) break;
                        }
                    }

                    ArgumentNullException.ThrowIfNull(propertyRootClass);

                    propertyIn = PropertyExist(propertyRootClass!, property, typeCast);

                    ArgumentNullException.ThrowIfNull(propertyIn);
                }

                ArgumentNullException.ThrowIfNull(propertyIn);

                var constant = GetConstantValue(propertyIn, value);
                var comparison = ParseExpressionType(propertyIn, constant, operatorx);

                finalExpression = finalExpression == null ? comparison : CreateExpressionType(finalExpression, comparison, expressionType);
            }

            ArgumentNullException.ThrowIfNull(finalExpression);

            return System.Linq.Expressions.Expression.Lambda<Func<TTarget, bool>>(finalExpression!, parameter);
        }

        public virtual Expression<Func<TTarget, object>>[] ConvertIncludesExpression<TSource, TTarget>(Expression<Func<TSource, object>>[] viewModelIncludes
            , string findIn = "", string[]? includeProperty = null)
        {
            ArgumentNullException.ThrowIfNull(viewModelIncludes);

            var includes = new List<Expression<Func<TTarget, object>>>();
            foreach (var include in viewModelIncludes)
                includes.Add(ConvertIncludeExpression<TSource, TTarget>(include, findIn));

            if (includeProperty is not null && includeProperty.Length > 0)
            {
                var newIncludes = ConvertIncludesExpression<TTarget>(includeProperty);
                if (newIncludes is not null && newIncludes.Length > 0)
                    includes.AddRange(newIncludes);
            }

            return [.. includes];
        }

        public virtual Expression<Func<TTarget, object>>[]? ConvertIncludesExpression<TTarget>(string[]? includeProperty
            , ParameterExpression? parameter = null)
        {
            if (includeProperty is null) return null;

            var includes = new List<Expression<Func<TTarget, object>>>();
            var parameterIn = parameter is null ? System.Linq.Expressions.Expression.Parameter(typeof(TTarget), "inc") : parameter;
            System.Linq.Expressions.Expression? memberIn = null;

            for (int i = 0; i < includeProperty.Length; i++)
            {
                var propList = includeProperty[i].Split('.');
                if (propList.Length.Equals(1))
                {
                    memberIn = ConvertInclude(includes, memberIn, parameterIn, propList[0]);
                }
                else if (propList.Length > 1)
                {
                    var newIncludes = ConvertIncludesExpression<TTarget>(propList, parameterIn);
                    if (newIncludes is not null)
                        includes.AddRange(newIncludes);
                }
            }

            return [.. includes];
        }

        public virtual Expression<Func<T, bool>> CreateCustomFilters<T>(
            IEnumerable<(string PropertyName, object? Value, ExpressionType Operator)> conditions)
        {
            var extendedConditions = conditions.Select(c =>
                (c.PropertyName, c.Value, c.Operator, ExpressionType.AndAlso));

            return CreateCustomFilters<T>(extendedConditions);
        }

        public virtual Expression<Func<T, bool>> CreateCustomFilters<T>(
            IEnumerable<(string PropertyName, object? Value, ExpressionType Operator, ExpressionType AndOrET)> conditions)
        {
            System.Linq.Expressions.Expression? final = null;
            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T), "filter");

            foreach (var (propertyName, value, expressionType, andOrET) in conditions)
            {
                if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
                    continue;

                var property = System.Linq.Expressions.Expression.Property(parameter, propertyName);
                var constant = GetConstantValue(property, value);
                var comparison = ParseExpressionType(property, constant, expressionType);

                final = final is null
                    ? comparison
                    : andOrET is ExpressionType.OrElse or ExpressionType.Or
                        ? System.Linq.Expressions.Expression.OrElse(final, comparison)
                        : System.Linq.Expressions.Expression.AndAlso(final, comparison);
            }

            return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(final ??
                System.Linq.Expressions.Expression.Constant(true), parameter);
        }

        public virtual Expression<Func<T, bool>> AndIf<T>(Expression<Func<T, bool>> expr, bool condition
            , Expression<Func<T, bool>> newExpr)
        {
            return condition ? And(expr, newExpr) : expr;
        }

        public virtual Expression<Func<T, bool>> OrIf<T>(Expression<Func<T, bool>> expr, bool condition
            , Expression<Func<T, bool>> newExpr)
        {
            return condition ? Or(expr, newExpr) : expr;
        }
        #endregion

        #region Private methods
        private static Expression<Func<T, bool>> And<T>(Expression<Func<T, bool>> expr1
           , Expression<Func<T, bool>> expr2)
        {
            var param = System.Linq.Expressions.Expression.Parameter(typeof(T));
            var body = System.Linq.Expressions.Expression.AndAlso(System.Linq.Expressions.Expression.Invoke(expr1, param)
                , System.Linq.Expressions.Expression.Invoke(expr2, param));

            return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, param);
        }

        private static Expression<Func<T, bool>> Or<T>(Expression<Func<T, bool>> expr1
           , Expression<Func<T, bool>> expr2)
        {
            var param = System.Linq.Expressions.Expression.Parameter(typeof(T));
            var body = System.Linq.Expressions.Expression.Or(System.Linq.Expressions.Expression.Invoke(expr1, param)
                , System.Linq.Expressions.Expression.Invoke(expr2, param));

            return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, param);
        }

        private static MemberExpression? ConvertInclude<TTarget>(List<Expression<Func<TTarget, object>>> includes, System.Linq.Expressions.Expression? member
            , ParameterExpression parameter, string propertyName)
        {
            var entityMember = PropertyOrFieldExist(member is not null ? member : parameter, propertyName);
            if (entityMember is not null)
                includes.Add(System.Linq.Expressions.Expression.Lambda<Func<TTarget, object>>(entityMember, parameter));

            return entityMember;
        }

        private static Expression<Func<TTarget, object>> ConvertIncludeExpression<TSource, TTarget>(Expression<Func<TSource, object>> viewModelInclude
            , string findIn = "")
        {
            if (viewModelInclude.Body is MemberExpression memberExpr)
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(typeof(TTarget), "inc");

                var entityMember = PropertyOrFieldExist(parameter, memberExpr.Member.Name);
                if (entityMember is null)
                {
                    entityMember = PropertyOrFieldExist(parameter, findIn);
                    ArgumentNullException.ThrowIfNull(entityMember);
                    entityMember = PropertyOrFieldExist(entityMember, memberExpr.Member.Name);
                }

                return System.Linq.Expressions.Expression.Lambda<Func<TTarget, object>>(entityMember!, parameter);
            }

            throw new ArgumentException($"Could't convert expression '{viewModelInclude.Name}'.");
        }

        private static MemberExpression? PropertyExist(System.Linq.Expressions.Expression parameter, string propertyPath, Type? typeCast = null)
        {
            try
            {
                var property = PropertyTry(parameter, propertyPath);
                if (property is null)
                {
                    var castParameter = typeCast is not null ? System.Linq.Expressions.Expression.TypeAs(parameter, typeCast) : parameter;
                    return PropertyTry(castParameter, propertyPath);
                }

                return property;
            }
            catch { return null; }
        }

        private static MemberExpression? PropertyTry(System.Linq.Expressions.Expression parameter, string propertyPath)
        {
            try
            {
                return System.Linq.Expressions.Expression.Property(parameter, propertyPath);
            }
            catch { return null; }
        }

        private static MemberExpression? PropertyOrFieldExist(System.Linq.Expressions.Expression parameter, string propertyPath)
        {
            try { return System.Linq.Expressions.Expression.PropertyOrField(parameter, propertyPath); }
            catch { return null; }
        }

        private static void ParseExpressionFilters(System.Linq.Expressions.Expression expression, List<(string property, ExpressionType operatorx, object? value, ExpressionType expressionType)> conditions
            , ExpressionType expressionType)
        {
            if (expression is BinaryExpression binaryExpression)
            {
                if (binaryExpression.NodeType == ExpressionType.AndAlso ||
                    binaryExpression.NodeType == ExpressionType.OrElse)
                {
                    ParseExpressionFilters(binaryExpression.Left, conditions, binaryExpression.NodeType);
                    ParseExpressionFilters(binaryExpression.Right, conditions, binaryExpression.NodeType);
                }
                else
                {
                    string? propertyName = GetPropertyName(binaryExpression.Left);
                    object? value = GetConstantValue(binaryExpression.Right);

                    if (!string.IsNullOrEmpty(propertyName))
                        conditions.Add((propertyName!, binaryExpression.NodeType, value ?? null, expressionType));
                }
            }
            else if (expression is MethodCallExpression methodCall)
            {
                string? propertyName = GetPropertyName(methodCall.Object!);
                object? value = methodCall.Arguments.Count > 0 ? GetConstantValue(methodCall.Arguments[0]) : null;

                if (!string.IsNullOrEmpty(propertyName))
                    conditions.Add((propertyName!, ExpressionType.Call, value, expressionType));
            }
        }

        private static string? GetPropertyName(System.Linq.Expressions.Expression expression)
        {
            if (expression is MemberExpression memberExpression)
                return memberExpression.Member.Name;

            if (expression is UnaryExpression unaryExpression && unaryExpression.Operand is MemberExpression innerMember)
                return innerMember.Member.Name;

            return null;
        }

        private static object? GetConstantValue(System.Linq.Expressions.Expression expression)
        {
            if (expression is ConstantExpression constantExpression)
                return constantExpression.Value;

            if (expression is MemberExpression memberExpression)
            {
                var objectMember = System.Linq.Expressions.Expression.Convert(memberExpression, typeof(object));
                var getterLambda = System.Linq.Expressions.Expression.Lambda<Func<object>>(objectMember);

                return getterLambda.Compile()();
            }

            return null;
        }

        private static ConstantExpression GetConstantValue(MemberExpression propertyIn, object? value)
        {
            if (value is null)
                return System.Linq.Expressions.Expression.Constant(value);

            var targetType = Nullable.GetUnderlyingType(propertyIn.Type) ?? propertyIn.Type;
            object? typedValue = targetType.IsEnum ? Enum.ToObject(targetType, value!) : Convert.ChangeType(value, targetType);

            return System.Linq.Expressions.Expression.Constant(typedValue, targetType);
        }

        private static System.Linq.Expressions.Expression ParseExpressionType(MemberExpression property, ConstantExpression constant, ExpressionType operatorx)
        {
            return operatorx switch
            {
                ExpressionType.Equal => System.Linq.Expressions.Expression.Equal(property, constant),
                ExpressionType.NotEqual => System.Linq.Expressions.Expression.NotEqual(property, constant),
                ExpressionType.GreaterThan => System.Linq.Expressions.Expression.GreaterThan(property, constant),
                ExpressionType.GreaterThanOrEqual => System.Linq.Expressions.Expression.GreaterThanOrEqual(property, constant),
                ExpressionType.LessThan => System.Linq.Expressions.Expression.LessThan(property, constant),
                ExpressionType.LessThanOrEqual => System.Linq.Expressions.Expression.LessThanOrEqual(property, constant),
                ExpressionType.Call when property.Type == typeof(string) =>
                        System.Linq.Expressions.Expression.Call(property, typeof(string).GetMethod("Contains", [typeof(string)])!, constant),
                _ => throw new NotSupportedException($"Operator '{operatorx}' isn't supported!")
            };
        }

        private static BinaryExpression CreateExpressionType(System.Linq.Expressions.Expression? finalExpression, System.Linq.Expressions.Expression comparison, ExpressionType expressionType)
        {
            var expressionTypeResult = System.Linq.Expressions.Expression.Or(finalExpression!, comparison);
            if (expressionType.Equals(ExpressionType.AndAlso)) expressionTypeResult = System.Linq.Expressions.Expression.AndAlso(finalExpression!, comparison);
            else if (expressionType.Equals(ExpressionType.And)) expressionTypeResult =  System.Linq.Expressions.Expression.And(finalExpression!, comparison);
            else if (expressionType.Equals(ExpressionType.OrElse)) expressionTypeResult = System.Linq.Expressions.Expression.OrElse(finalExpression!, comparison);

            return expressionTypeResult;
        }
        #endregion
    }
}