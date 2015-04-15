using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

/* Implementing IQueryable:
 * https://msdn.microsoft.com/en-us/library/bb546158(v=vs.110).aspx
 * http://blogs.msdn.com/b/mattwar/archive/2008/11/18/linq-links.aspx
 *
 * JQL field reference:
 * https://confluence.atlassian.com/display/JIRA/Advanced+Searching+-+Fields+Reference
 */
namespace TechTalk.JiraRestClient
{
    internal sealed class QueryableIssueCollection<TIssueFields> : IQueryable<Issue<TIssueFields>> where TIssueFields : IssueFields, new()
    {
        private readonly Expression expression;
        private readonly IQueryProvider queryProvider;
        public QueryableIssueCollection(IJiraClient<TIssueFields> jiraQuery)
        {
            this.queryProvider = new QueryableIssueCollectionProvider<TIssueFields>(jiraQuery);
            this.expression = System.Linq.Expressions.Expression.Constant(this);
        }

        public QueryableIssueCollection(IQueryProvider queryProvider, Expression expression)
        {
            if (queryProvider == null) throw new ArgumentNullException("queryProvider");
            if (expression == null) throw new ArgumentNullException("expression");
            if (!typeof(IQueryable<>).MakeGenericType(ElementType).IsAssignableFrom(expression.Type))
                throw new ArgumentOutOfRangeException("The expression result type does not fit with the query element type");

            this.queryProvider = queryProvider;
            this.expression = expression;
        }

        public Type ElementType { get { return typeof(Issue<TIssueFields>); } }

        public Expression Expression { get { return expression; } }

        public IQueryProvider Provider { get { return queryProvider; } }

        public IEnumerator<Issue<TIssueFields>> GetEnumerator()
        {
            return Provider.Execute<IEnumerable<Issue<TIssueFields>>>(Expression).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return this.GetEnumerator(); }
    }
    internal sealed class QueryEnumerator<TElement> : IEnumerable<TElement>
    {
        private readonly IQueryable<TElement> queryable;
        public QueryEnumerator(IQueryable<TElement> queryable)
        {
            this.queryable = queryable;
        }

        public IEnumerator<TElement> GetEnumerator()
        {
            return queryable.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return queryable.GetEnumerator();
        }
    }

    internal static class QueryEnumerator
    {
        public static IEnumerable<TElement> Create<TElement>(IQueryable<TElement> queryable)
        {
            return new QueryEnumerator<TElement>(queryable);
        }
    }

    internal sealed class QueryableIssueCollectionProvider<TIssueFields> : IQueryProvider where TIssueFields : IssueFields, new()
    {
        private readonly Type resultType = typeof(IEnumerable<Issue<TIssueFields>>);
        private readonly MethodInfo whereMethod = typeof(System.Linq.Queryable).GetMethods()
            .Where(m => m.Name == "Where").Single(m => IsPredicate(m))
            .MakeGenericMethod(typeof(Issue<TIssueFields>));
        private readonly MethodInfo skipMethod = typeof(System.Linq.Queryable).GetMethod("Skip")
            .MakeGenericMethod(typeof(Issue<TIssueFields>));

        private readonly IJiraClient<TIssueFields> jiraQuery;
        public QueryableIssueCollectionProvider(IJiraClient<TIssueFields> jiraQuery)
        {
            this.jiraQuery = jiraQuery;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            var subexpression = FindSubexpression(expression);
            var subquery = new QueryableIssueCollection<TIssueFields>(this, subexpression.Expression);
            if (subexpression.Complete) return (IQueryable<TElement>)(object)subquery;

            var baseQuery = QueryEnumerator.Create(subquery).AsQueryable();
            var topExpression = SplitExpression(expression, subexpression.Expression, baseQuery);
            return baseQuery.Provider.CreateQuery<TElement>(topExpression);
        }

        public IQueryable CreateQuery(Expression expression)
        {
            return this.CreateQuery<object>(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            var subexpression = FindSubexpression(expression);
            var result = jiraQuery.EnumerateIssuesByQuery(
                CreateJql(subexpression.Expression),
                GetStart(subexpression.Expression));

            if (subexpression.Complete) return (TResult)result;

            var queryable = result.AsQueryable();
            var trimmedExpression = SplitExpression(expression, subexpression.Expression, queryable);
            return queryable.Provider.Execute<TResult>(trimmedExpression);
        }

        public object Execute(Expression expression)
        {
            return this.Execute<object>(expression);
        }

        private static bool IsPredicate(MethodInfo methodInfo)
        {
            var funcArg = methodInfo.GetParameters()[1].ParameterType
                .GetGenericArguments()[0].GetGenericArguments()[1];
            return funcArg == typeof(bool);
        }

        private string CreateJql(Expression expression)
        {
            if (expression == null) return string.Empty;

            var jqlList = new List<string>();
            while (expression.NodeType != ExpressionType.Constant)
            {
                var callExpression = (MethodCallExpression)expression;
                expression = callExpression.Arguments.First();
                if (callExpression.Method != whereMethod) continue;

                var operand = ((UnaryExpression)callExpression.Arguments[1]).Operand;
                var predicate = ((Expression<Func<Issue<TIssueFields>, bool>>)operand).Body;
                jqlList.Add(GetFormula(predicate));
            }
            return string.Join(" AND ", jqlList);
        }

        private string GetFormula(Expression predicate)
        {
            if (predicate is ConstantExpression)
                return ((ConstantExpression)predicate).Value.ToString();

            if (predicate is UnaryExpression)
            {
                var unaryExpr = (UnaryExpression)predicate;
                if (unaryExpr.NodeType == ExpressionType.Quote)
                    return GetFormula(((LambdaExpression)unaryExpr.Operand).Body);
                if (unaryExpr.NodeType != ExpressionType.Not) Error();
                return string.Format("(NOT{0})", GetFormula(unaryExpr.Operand));
            }

            if (predicate is BinaryExpression)
            {
                var left = ((BinaryExpression)predicate).Left;
                var right = ((BinaryExpression)predicate).Right;
                return string.Format("({1}{0}{2})",
                    GetOperator(predicate.NodeType),
                    GetFormula(left), GetFormula(right));
            }

            if (predicate is MemberExpression)
            {
                var chain = new Stack<string>(5);
                var memberExpr = (MemberExpression)predicate;
                while (memberExpr.Expression.NodeType == ExpressionType.MemberAccess)
                {
                    chain.Push(memberExpr.Member.Name);
                    memberExpr = (MemberExpression)memberExpr.Expression;
                }
                chain.Push(memberExpr.Member.Name);
                var name = string.Join(".", chain);
                return LookupJqlName(name);
            }

            if (predicate is MethodCallExpression)
            {
                var callExpression = (MethodCallExpression)predicate;
                if (callExpression.Method.ReflectedType != typeof(Enumerable)
                    || callExpression.Method.Name != "Contains") Error();
                var collectionExpr = (MemberExpression)callExpression.Arguments[0];
                var value = collectionExpr.Expression == null ? (object)null
                    : ((ConstantExpression)collectionExpr.Expression).Value;
                var collection = collectionExpr.Member is FieldInfo
                    ? ((FieldInfo)collectionExpr.Member).GetValue(value)
                    : ((PropertyInfo)collectionExpr.Member).GetValue(value, null);
                return string.Format("({0} IN ({1}))", GetFormula(callExpression.Arguments[1]),
                    string.Join(",", ((IEnumerable)collection).OfType<object>()));
            }

            Error();
            return null;
        }

        private string LookupJqlName(string field)
        {
            switch (field)
            {
                case "id":
                    return "id";
                case "fields.assignee.name":
                    return "assignee";
            }

            Error();
            return null;
        }

        private string GetOperator(ExpressionType expressionType)
        {
            switch (expressionType)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                    return " AND ";
                case ExpressionType.Equal:
                    return "=";
                case ExpressionType.GreaterThan:
                    return ">";
                case ExpressionType.GreaterThanOrEqual:
                    return ">=";
                case ExpressionType.LessThan:
                    return "<";
                case ExpressionType.LessThanOrEqual:
                    return "<=";
                case ExpressionType.NotEqual:
                    return "!=";
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    return " OR ";
            }

            Error();
            return null;
        }

        private void Error() { throw new InvalidOperationException(); }

        private int GetStart(Expression expression)
        {
            var callExpression = expression as MethodCallExpression;
            if (callExpression == null) return 0;
            if (callExpression.Method != skipMethod) return 0;
            if (callExpression.Arguments.Count != 2) return 0;

            var argument = callExpression.Arguments[1] as ConstantExpression;
            if (argument == null) return 0;
            return (int)argument.Value;
        }

        private Subexpression FindSubexpression(Expression expression)
        {
            if (expression == null) return new Subexpression { Complete = true };

            if (expression.NodeType == ExpressionType.Constant)
                return new Subexpression { Expression = expression, Complete = true };

            Expression handled = null;
            var current = expression;
            while (current.NodeType != ExpressionType.Constant)
            {
                var callExpression = (MethodCallExpression)current;
                current = callExpression.Arguments.First();

                if (resultType.IsAssignableFrom(callExpression.Type))
                {
                    if (callExpression.Method == skipMethod)
                        handled = callExpression;
                    else if (callExpression.Method == whereMethod)
                    {
                        var isOkay = false; // could build query expression here
                        try { GetFormula(callExpression.Arguments[1]); isOkay = true; }
                        catch { handled = null; /* could not generate jql expression */ }
                        if (isOkay && handled == null) handled = callExpression;
                    }
                }
                else /* wrong type */ handled = null;
            }

            return new Subexpression
            {
                Expression = handled ?? current,
                Complete = (handled == expression)
            };
        }

        private Expression SplitExpression(Expression mainExpression, Expression handledExpression, IQueryable<Issue<TIssueFields>> expressionRoot)
        {
            if (mainExpression == handledExpression) return Expression.Constant(expressionRoot);

            var expression = (MethodCallExpression)mainExpression;
            var arguments = new Expression[expression.Arguments.Count];
            Array.Copy(expression.Arguments.ToArray(), arguments, arguments.Length);
            arguments[0] = SplitExpression(expression.Arguments[0], handledExpression, expressionRoot);
            return Expression.Call(expression.Method, arguments);
        }

        private class Subexpression
        {
            public Expression Expression { get; set; }
            public bool Complete { get; set; }
        }
    }
}
