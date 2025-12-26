using System.Linq.Expressions;
using System.Reflection;

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
                }

                System.Linq.Expressions.Expression? comparison = null;
                if (propertyIn is null && property.Contains('.'))
                {
                    comparison = GetPropertyExpressionWithAny(parameter, property, value!, operatorx);

                    ArgumentNullException.ThrowIfNull(comparison);
                }
                else
                {
                    ArgumentNullException.ThrowIfNull(propertyIn);

                    var constant = GetConstantValue(propertyIn, value);
                    comparison = ParseExpressionType(propertyIn, constant, operatorx);
                }

                finalExpression = finalExpression is null ? comparison : CreateExpressionType(finalExpression, comparison, expressionType);
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

                var property = GetPropertyExpressionWithAny(parameter, propertyName, value, expressionType);
                var constant = GetConstantValue(property, value);
                var comparison = ParseExpressionType(property, constant, expressionType);

                final = final is null
                    ? comparison
                    : andOrET is ExpressionType.OrElse or ExpressionType.Or
                        ? System.Linq.Expressions.Expression.OrElse(final, comparison)
                        : System.Linq.Expressions.Expression.AndAlso(final, comparison);
            }

            return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(
                final ?? System.Linq.Expressions.Expression.Constant(true),
                parameter
            );
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
        private static bool IsContainsMethod(MethodCallExpression methodCall)
        {
            return methodCall.Method.Name == nameof(Enumerable.Contains)
                   && methodCall.Arguments.Count.Equals(2);
        }

        private static bool IsAnyMethod(MethodCallExpression methodCall)
        {
            return methodCall.Method.Name == nameof(Enumerable.Any)
                && methodCall.Arguments.Count.Equals(2)
                && methodCall.Arguments[1] is LambdaExpression;
        }

        private static bool IsInOperator(object? value)
        {
            return value is System.Collections.IEnumerable && value is not string;
        }

        private static bool IsEnumerableButNotString(Type type)
        {
            return type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
        }

        private static bool IsEnumerableValue(object value)
        {
            return value is System.Collections.IEnumerable && value is not string;
        }

        private static System.Linq.Expressions.Expression GetPropertyExpression(System.Linq.Expressions.Expression parameter
            , string propertyPath)
        {
            System.Linq.Expressions.Expression current = parameter;

            foreach (var member in propertyPath.Split('.'))
                current = System.Linq.Expressions.Expression.Property(current, member);
    
            return current;
        }

        private static System.Linq.Expressions.Expression GetPropertyExpressionWithAny(System.Linq.Expressions.Expression parameter
            , string propertyPath, object value, ExpressionType operatorx)
        {
            var parts = propertyPath.Split('.');
            System.Linq.Expressions.Expression current = parameter;

            for (int i = 0; i < parts.Length; i++)
            {
                var propertyInfo = current.Type.GetProperty(parts[i])
                    ?? throw new InvalidOperationException(
                        $"Property '{parts[i]}' not found on '{current.Type.Name}'");

                if (IsEnumerableButNotString(propertyInfo.PropertyType))
                {
                    var collection = System.Linq.Expressions.Expression.Property(current, propertyInfo);
                    var elementType = propertyInfo.PropertyType.GetGenericArguments()[0];

                    var innerPath = string.Join('.', parts.Skip(i + 1));

                    return BuildAnyExpression(collection, elementType, innerPath, value, operatorx);
                }

                var propertyIn = System.Linq.Expressions.Expression.Property(current, propertyInfo);

                if (propertyInfo.PropertyType == typeof(string) ||
                    propertyInfo.PropertyType == typeof(int) ||
                    propertyInfo.PropertyType == typeof(long) ||
                    propertyInfo.PropertyType == typeof(Enum) ||
                    propertyInfo.PropertyType == typeof(DateOnly) ||
                    propertyInfo.PropertyType == typeof(DateTime))
                {
                    var constant = GetConstantValue(propertyIn, value);
                    current = ParseExpressionType(propertyIn, constant, operatorx);
                }
                else
                {
                    current = propertyIn;
                }
            }

            return current;
        }

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
                if (methodCall.Arguments is null)
                    return;

                if (IsAnyMethod(methodCall))
                {
                    ParseAnyExpression(methodCall, conditions, expressionType);
                    return;
                }

                string? propertyName = GetPropertyName(IsContainsMethod(methodCall) ? methodCall.Arguments[1] : methodCall.Object!);
                object? value = methodCall.Arguments.Count > 0 ? GetConstantValue(methodCall.Arguments[0]) : null;

                if (!string.IsNullOrEmpty(propertyName))
                    conditions.Add((propertyName!, ExpressionType.Call, value, expressionType));
            }
        }

        private static string? GetPropertyName(System.Linq.Expressions.Expression expression)
        {
            if (expression is UnaryExpression unary)
                return GetPropertyName(unary.Operand);

            if (expression is MemberExpression member)
            {
                var parent = GetPropertyName(member.Expression!);
                return string.IsNullOrEmpty(parent) ? member.Member.Name : $"{parent}.{member.Member.Name}";
            }

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

        private static ConstantExpression GetConstantValue(System.Linq.Expressions.Expression propertyIn, object? value)
        {
            if (value is null)
                return System.Linq.Expressions.Expression.Constant(null, propertyIn.Type);

            var targetType = Nullable.GetUnderlyingType(propertyIn.Type) ?? propertyIn.Type;

            if (IsEnumerableValue(value))
            {
                var enumerable = ((System.Collections.IEnumerable)value)
                    .Cast<object>()
                    .Select(val =>
                    {
                        if (targetType.IsEnum)
                        {
                            var numericValue = Convert.ChangeType(val, Enum.GetUnderlyingType(targetType));
                            return Enum.ToObject(targetType, numericValue);
                        }

                        return Convert.ChangeType(val, targetType);
                    })
                    .ToList();

                var listType = typeof(List<>).MakeGenericType(targetType);
                var typedList = Activator.CreateInstance(listType)!;

                foreach (var item in enumerable)
                    listType.GetMethod("Add")!.Invoke(typedList, [item]);

                return System.Linq.Expressions.Expression.Constant(typedList, listType);
            }

            object typedValue = targetType.IsEnum ? Enum.ToObject(targetType, value) : Convert.ChangeType(value, targetType);

            return System.Linq.Expressions.Expression.Constant(typedValue, targetType);
        }

        private static System.Linq.Expressions.Expression BuildContainsExpression(System.Linq.Expressions.Expression property
            , System.Linq.Expressions.Expression constant)
        {
            var elementType = property.Type;

            var containsMethod = typeof(Enumerable)
                .GetMethods()
                .First(method => method.Name == nameof(Enumerable.Contains) 
                              && method.GetParameters().Length == 2)
                .MakeGenericMethod(elementType);

            return System.Linq.Expressions.Expression.Call(containsMethod, constant, property);
        }

        private static System.Linq.Expressions.Expression BuildAnyExpression(System.Linq.Expressions.Expression collection
            , Type elementType, string innerPropertyPath, object value, ExpressionType operatorx)
        {
            var parameter = System.Linq.Expressions.Expression.Parameter(elementType, "inner");
            var innerProperty = GetPropertyExpression(parameter, innerPropertyPath);

            System.Linq.Expressions.Expression body;

            if (operatorx == ExpressionType.Call && IsInOperator(value))
            {
                var constant = System.Linq.Expressions.Expression.Constant(value, typeof(IEnumerable<>)
                    .MakeGenericType(innerProperty.Type));

                body = BuildContainsExpression(innerProperty, constant);
            }
            else
            {
                var constant = GetConstantValue(innerProperty, value);
                body = ParseExpressionType(innerProperty, constant, operatorx);
            }

            var anyMethod = typeof(Enumerable)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .First(m => m.Name == nameof(Enumerable.Any)
                         && m.GetParameters().Length == 2)
                .MakeGenericMethod(elementType);

            return System.Linq.Expressions.Expression.Call(anyMethod, collection,
                System.Linq.Expressions.Expression.Lambda(body, parameter));
        }

        private static System.Linq.Expressions.Expression ParseExpressionType(System.Linq.Expressions.Expression property
            , ConstantExpression constant, ExpressionType operatorx)
        {
            if (property.Type == typeof(bool))
                return property;

            if (operatorx == ExpressionType.Call && IsEnumerableButNotString(constant.Type))
                return BuildContainsExpression(property, constant);
    
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

        private static void ParseAnyExpression(MethodCallExpression methodCall
            , List<(string property, ExpressionType operatorx, object? value, ExpressionType expressionType)> conditions
            , ExpressionType expressionType)
        {
            var collectionExpression = methodCall.Arguments[0];
            var collectionName = GetPropertyName(collectionExpression);

            if (collectionName is null)
                return;

            var lambda = (LambdaExpression)methodCall.Arguments[1];
            var body = lambda.Body;

            if (body is BinaryExpression binary)
            {
                var innerProperty = GetPropertyName(binary.Left);
                var value = GetConstantValue(binary.Right);

                if (!string.IsNullOrEmpty(innerProperty))
                {
                    conditions.Add((
                        $"{collectionName}.{innerProperty}",
                        binary.NodeType,
                        value,
                        expressionType
                    ));
                }
            }
            else if (body is MethodCallExpression innerMethod)
            {
                var innerProperty = GetPropertyName(innerMethod.Object!);
                var value = GetConstantValue(innerMethod.Arguments[0]);

                if (!string.IsNullOrEmpty(innerProperty))
                {
                    conditions.Add((
                        $"{collectionName}.{innerProperty}",
                        ExpressionType.Call,
                        value,
                        expressionType
                    ));
                }
            }
        }

        private static BinaryExpression CreateExpressionType(System.Linq.Expressions.Expression? finalExpression
            , System.Linq.Expressions.Expression comparison, ExpressionType expressionType)
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