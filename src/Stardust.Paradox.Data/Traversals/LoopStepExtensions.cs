﻿using System;

namespace Stardust.Paradox.Data.Traversals
{
    public static class LoopStepExtensions
    {
        public static GremlinQuery Repeat(this GremlinQuery queryBase,
            Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, "repeat({0})",
                query => expression.Invoke(new PredicateGremlinQuery(query)).CompileQuery());
        }

        public static GremlinQuery Until(this GremlinQuery queryBase,
            Func<PredicateGremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, "until({0})",
                query => expression.Invoke(new PredicateGremlinQuery(query)).CompileQuery());
        }

        public static GremlinQuery Times(this GremlinQuery queryBase, int times)
        {
            return new ComposedGremlinQuery(queryBase, $"times({queryBase.ComposeParameter(times)})");
        }

        public static GremlinQuery Loops(this GremlinQuery queryBase)
        {
            return new ComposedGremlinQuery(queryBase, "loops()");
        }

        public static GremlinQuery Coalesce(this GremlinQuery queryBase,
            params Func<GremlinQuery, GremlinQuery>[] expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, "coalesce({0})", expression);
        }

        public static GremlinQuery Choose(this GremlinQuery queryBase,
            params Func<GremlinQuery, GremlinQuery>[] expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, "choose({0})", expression);
        }

        public static GremlinQuery Branch(this GremlinQuery queryBase,
            params Func<GremlinQuery, GremlinQuery>[] expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, "branch({0})", expression);
        }

        public static GremlinQuery Option(this GremlinQuery queryBase, string value,
            Func<GremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, $"option({queryBase.ComposeParameter(value)},{{0}})",
                expression);
        }

        public static GremlinQuery Option(this GremlinQuery queryBase, long value,
            Func<GremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, $"option({queryBase.ComposeParameter(value)},{{0}})",
                expression);
        }

        public static GremlinQuery Option(this GremlinQuery queryBase, decimal value,
            Func<GremlinQuery, GremlinQuery> expression)
        {
            return new LambdaComposedGremlinQuery(queryBase, $"option({queryBase.ComposeParameter(value)},{{0}})",
                expression);
        }
    }
}