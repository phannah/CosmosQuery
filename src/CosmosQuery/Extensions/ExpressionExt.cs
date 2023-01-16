﻿using CosmosQuery.Visitors;

namespace CosmosQuery.Extensions;

internal static class ExpressionExt
{
    public static Expression ReplaceParameter(this Expression expression, ParameterExpression source, Expression target) =>
        new ParameterReplacer(source, target).Visit(expression);

    public static Expression UnQuote(this Expression expression) =>
        expression.NodeType switch
        {
            ExpressionType.Quote => ((UnaryExpression)expression).Operand.UnQuote(),
            _ => expression,
        };

    public static MethodCallExpression ToListCall(this Expression expression, Type elementType) =>
        Expression.Call
        (
            LinqMethods.EnumerableToListMethod.MakeGenericMethod(elementType),
            expression
        );
}
