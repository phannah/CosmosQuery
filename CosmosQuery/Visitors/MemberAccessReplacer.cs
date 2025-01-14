﻿using System.Linq.Expressions;

namespace CosmosQuery.Visitors
{
    internal sealed class MemberAccessReplacer : ExpressionVisitor
    {
        private readonly Type currentType;
        private readonly MemberExpression replacement;

        private MemberAccessReplacer(Type currentType, MemberExpression replacement)
        {
            this.currentType = currentType;
            this.replacement = replacement;
        }

        public static Expression Replace(Type currentType, MemberExpression replacement, Expression expression) =>
            new MemberAccessReplacer(currentType, replacement).Visit(expression);

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.NodeType == ExpressionType.MemberAccess 
                && this.currentType == node.Expression!.Type)
            {                
                return Expression.MakeMemberAccess(this.replacement, node.Member);
            }
            return base.VisitMember(node);
        }
    }
}
